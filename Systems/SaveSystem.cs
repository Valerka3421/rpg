using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace Aethelgard.Systems;

/// <summary>
/// Save/load system — JSON serialization with versioned slots.
/// Ported from Python core/save.py (SAVE_VERSION = 8).
///
/// Format: JSON file with:
/// - version: 8
/// - player: stats, inventory, equipment, class, level, position, mana,
///   perks, blueprints, hotbar, xp_next (пересчитывается при загрузке)
/// - world: mode, depth, location
/// - floors: кэш этажей подземелья (тайлы строками цифр, монстры, предметы,
///   harvestables, казна, запертые двери)
/// - quests: полный журнал (available/active/completed/turned_in)
/// - timestamp
///
/// Атомарная запись через .tmp + File.Replace (как в Python os.replace).
/// Save slots: 1-3, stored in "saves/" directory.
/// </summary>
public static class SaveSystem
{
    public const int SaveVersion = Config.SaveVersion;  // 8
    public static readonly string SaveDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Aethelgard", "saves");

    /// <summary>
    /// Единые опции сериализации. Свойства (а не поля) выбраны осознанно:
    /// System.Text.Json по умолчанию игнорирует public-поля, из-за чего
    /// сейвы раньше сохранялись как пустые объекты.
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Последняя ошибка Save/Load (для UI и тестов).</summary>
    public static string? LastError { get; private set; }

    public sealed class SaveData
    {
        public int Version { get; set; } = SaveVersion;
        public DateTime Timestamp { get; set; }
        public PlayerData Player { get; set; } = new();
        public WorldData World { get; set; } = new();
        public QuestDataSave Quests { get; set; } = new();
        public List<FloorData> Floors { get; set; } = new();
    }

    public sealed class PlayerData
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public int Mana { get; set; }
        public int MaxMana { get; set; }
        public int Level { get; set; }
        public int Xp { get; set; }
        public int XpNext { get; set; }
        public int FreePoints { get; set; }
        public int Gold { get; set; }
        public int Str { get; set; }
        public int Agi { get; set; }
        public int Vit { get; set; }
        public int Luk { get; set; }
        public int DfStat { get; set; }
        public int IntStat { get; set; }
        public string ClassKey { get; set; } = "warrior";
        public string ClassName { get; set; } = "Warrior";
        public int ClassTier { get; set; } = 1;
        public string ClassBase { get; set; } = "warrior";
        public int ClassChanges { get; set; }
        public string? Weapon { get; set; }
        public string? Armor { get; set; }
        public string? Head { get; set; }
        public string? Legs { get; set; }
        public string? Boots { get; set; }
        // Совместимость со старыми именами слотов (helmet/leggings)
        public string? Helmet { get => Head; set => Head = value; }
        public string? Leggings { get => Legs; set => Legs = value; }
        public Dictionary<string, int> Inventory { get; set; } = new();
        public bool DoubleStrike { get; set; }
        public bool Regen { get; set; }
        public bool Vampire { get; set; }
        public bool Thorns { get; set; }
        public bool Greedy { get; set; }
        public int SkillPoints { get; set; }
        public int StepCounter { get; set; }
        public List<string> LearnedNodes { get; set; } = new();
        public List<string> Blueprints { get; set; } = new();
        public List<string?> Hotbar { get; set; } = new();
        public Dictionary<string, int> CraftLevels { get; set; } = new();
        public Dictionary<string, int> CraftXp { get; set; } = new();
        public Dictionary<string, int> Statuses { get; set; } = new();
    }

    public sealed class WorldData
    {
        public string Mode { get; set; } = "overworld";
        public int Depth { get; set; }
        public string? Location { get; set; }
    }

    public sealed class QuestDataSave
    {
        public List<string> Available { get; set; } = new();
        public List<ActiveQuestSave> Active { get; set; } = new();
        public List<string> Completed { get; set; } = new();
        public List<string> TurnedIn { get; set; } = new();
    }

    public sealed class ActiveQuestSave
    {
        public string Key { get; set; } = "";
        public int Progress { get; set; }
        public int Count { get; set; }
        public bool Generated { get; set; }
        public string Type { get; set; } = "";
        public string Target { get; set; } = "";
        public string Name { get; set; } = "";
        public string Desc { get; set; } = "";
        public int RewardGold { get; set; }
        public int RewardXp { get; set; }
    }

    /// <summary>Сериализованный этаж подземелья (parity с dungeon_to_dict).</summary>
    public sealed class FloorData
    {
        public int Depth { get; set; }
        public int W { get; set; }
        public int H { get; set; }
        public List<string> Tiles { get; set; } = new();          // строки цифр по y
        public List<RoomSave> Rooms { get; set; } = new();
        public List<MonsterSave> Monsters { get; set; } = new();
        public List<ItemSave> Items { get; set; } = new();
        public PointSave? StairsPos { get; set; }
        public List<HarvestSave> Harvestables { get; set; } = new();
        public PointSave? TreasuryPos { get; set; }
        public List<PointSave> BossDoorCells { get; set; } = new();
        public List<ChestLootSave> ChestLoot { get; set; } = new();
        public List<ShopEntrySave> MerchantStock { get; set; } = new();
        public List<TransitionSave> Transitions { get; set; } = new();
        public List<PointSave> BarrierTiles { get; set; } = new();
        public List<PointSave> Explored { get; set; } = new();
        public string? LocationKey { get; set; }
    }

    /// <summary>Переход между картами (ворота/выход локации).</summary>
    public sealed class TransitionSave
    {
        public int X { get; set; }
        public int Y { get; set; }
        public string To { get; set; } = "";
        public int EntryX { get; set; }
        public int EntryY { get; set; }
    }

    /// <summary>Точка на карте. Отдельный тип вместо кортежа:
    /// System.Text.Json не сериализует ValueTuple как контракт.</summary>
    public sealed class PointSave
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    public sealed class RoomSave
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int W { get; set; }
        public int H { get; set; }
    }

    public sealed class MonsterSave
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public int Atk { get; set; }
        public int Df { get; set; }
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
        public char Ch { get; set; }
        public string Color { get; set; } = "255,255,255";
        public int XpValue { get; set; }
        public bool Boss { get; set; }
        public bool Immortal { get; set; }
        public bool IsNpc { get; set; }
        public string? Ability { get; set; }
        public string? NpcType { get; set; }
        public Dictionary<string, int> Statuses { get; set; } = new();
    }

    public sealed class ItemSave
    {
        public int X { get; set; }
        public int Y { get; set; }
        public string Key { get; set; } = "";
    }

    public sealed class HarvestSave
    {
        public int X { get; set; }
        public int Y { get; set; }
        public string Kind { get; set; } = "";
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public bool Has { get; set; }
        public double ReadyAt { get; set; }
        public double SpawnedAt { get; set; }
    }

    public sealed class ChestLootSave
    {
        public int X { get; set; }
        public int Y { get; set; }
        public Dictionary<string, int> Loot { get; set; } = new();
    }

    public sealed class ShopEntrySave
    {
        public string Key { get; set; } = "";
        public int Price { get; set; }
    }

    /// <summary>
    /// Save game to a slot (атомарная запись через .tmp).
    /// </summary>
    public static bool Save(GameController game, int slot)
    {
        try
        {
            Directory.CreateDirectory(SaveDir);
            var data = new SaveData
            {
                Timestamp = DateTime.Now,
                Player = PlayerToData(game.Player),
                World = new WorldData
                {
                    Mode = game.WorldMode,
                    Depth = game.Depth,
                    Location = game.CurrentMap.LocationKey,
                },
                Quests = new QuestDataSave
                {
                    Available = game.Quests.Available.ToList(),
                    Active = game.Quests.Active.Select(kv => new ActiveQuestSave
                    {
                        Key = kv.Key,
                        Progress = kv.Value.Progress,
                        Count = kv.Value.Count,
                        Generated = kv.Value.Generated,
                        Type = kv.Value.Type,
                        Target = kv.Value.Target,
                        Name = kv.Value.Name,
                        Desc = kv.Value.Desc,
                        RewardGold = kv.Value.RewardGold,
                        RewardXp = kv.Value.RewardXp,
                    }).ToList(),
                    Completed = game.Quests.Completed.ToList(),
                    TurnedIn = game.Quests.TurnedIn.ToList(),
                },
            };

            // Все посещённые этажи подземелья: иначе после загрузки
            // терялись открытые сундуки, лут и ассортимент торговцев.
            foreach (var (depth, floor) in game.GetFloorCache().OrderBy(kv => kv.Key))
                data.Floors.Add(FloorToData(depth, floor));

            // Все посещённые локации (LocationKey заполняется в FloorToData).
            foreach (var (_, map) in game.GetLocationCache().OrderBy(kv => kv.Key))
                data.Floors.Add(FloorToData(0, map));

            string path = SlotPath(slot);
            string json = Serialize(data);
            // Атомарная запись: .tmp + replace
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
            LastError = null;
            return true;
        }
        catch (Exception ex)
        {
            LastError = $"Save slot {slot}: {ex.Message}";
            System.Diagnostics.Debug.WriteLine(LastError);
            return false;
        }
    }

    /// <summary>Сериализация в JSON (публичная — используется тестами).</summary>
    public static string Serialize(SaveData data) =>
        JsonSerializer.Serialize(data, JsonOptions);

    /// <summary>Десериализация из JSON с проверкой версии.</summary>
    public static SaveData? Deserialize(string json)
    {
        var data = JsonSerializer.Deserialize<SaveData>(json, JsonOptions);
        if (data == null) return null;
        if (data.Version > SaveVersion) return null;  // too new
        return data;
    }

    /// <summary>
    /// Load game from a slot. Returns null if failed.
    /// Версия новее текущей → отказ (core/save.py).
    /// </summary>
    public static SaveData? Load(int slot)
    {
        try
        {
            string path = SlotPath(slot);
            if (!File.Exists(path)) return null;

            string json = File.ReadAllText(path);
            var data = Deserialize(json);
            if (data == null)
            {
                LastError = $"Save slot {slot}: invalid or unsupported save format";
                return null;
            }

            // Миграция старых версий (1–7) принимается, поля по умолчанию
            LastError = null;
            return data;
        }
        catch (Exception ex)
        {
            LastError = $"Load slot {slot}: {ex.Message}";
            System.Diagnostics.Debug.WriteLine(LastError);
            return null;
        }
    }

    /// <summary>
    /// Check if a save slot exists.
    /// </summary>
    public static bool SlotExists(int slot) => File.Exists(SlotPath(slot));

    /// <summary>
    /// Get slot info (level, class, timestamp) without full load.
    /// </summary>
    public static (int Level, string Class, DateTime Timestamp)? SlotInfo(int slot)
    {
        var data = Load(slot);
        if (data == null) return null;
        return (data.Player.Level, data.Player.ClassName, data.Timestamp);
    }

    /// <summary>Подробная инфа слота для меню (parity core/save.py:save_slot_info).</summary>
    public sealed record SlotInfoEx(int Level, string ClassKey, string Class,
        DateTime Timestamp, string WorldMode, int Depth, int Gold);

    public static SlotInfoEx? SlotInfoFull(int slot)
    {
        var data = Load(slot);
        if (data == null) return null;
        return new SlotInfoEx(data.Player.Level, data.Player.ClassKey,
            data.Player.ClassName, data.Timestamp, data.World.Mode,
            data.World.Depth, data.Player.Gold);
    }

    /// <summary>
    /// Delete a save slot.
    /// </summary>
    public static bool DeleteSlot(int slot)
    {
        try
        {
            string path = SlotPath(slot);
            if (File.Exists(path))
            {
                File.Delete(path);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Apply loaded save data to the game.
    /// </summary>
    public static bool ApplyLoad(GameController game, SaveData data)
    {
        try
        {
            // Restore player
            var p = game.Player;
            var pd = data.Player;
            p.X = pd.X; p.Y = pd.Y;
            p.Hp = pd.Hp; p.MaxHp = pd.MaxHp;
            p.Mana = pd.Mana; p.MaxMana = pd.MaxMana;
            p.Level = pd.Level; p.Xp = pd.Xp;
            // xp_next пересчитывается при загрузке (чинит старую кривую XP)
            p.XpNext = Config.XpToNext(Math.Max(1, pd.Level));
            p.FreePoints = pd.FreePoints; p.Gold = pd.Gold;
            p.Str = pd.Str; p.Agi = pd.Agi; p.Vit = pd.Vit;
            p.Luk = pd.Luk; p.DfStat = pd.DfStat; p.IntStat = pd.IntStat;
            p.ClassKey = pd.ClassKey; p.ClassName = pd.ClassName;
            game.RunStart = DateTime.Now;   // статистика забега начинается заново
            p.ClassTier = pd.ClassTier;
            p.ClassBase = string.IsNullOrEmpty(pd.ClassBase) ? pd.ClassKey : pd.ClassBase;
            p.ClassChanges = pd.ClassChanges;
            p.Weapon = pd.Weapon; p.Armor = pd.Armor;
            p.Head = pd.Head ?? pd.Helmet; p.Legs = pd.Legs ?? pd.Leggings;
            p.Boots = pd.Boots;
            p.Inventory = new Dictionary<string, int>(pd.Inventory);
            p.DoubleStrike = pd.DoubleStrike; p.Regen = pd.Regen;
            p.Vampire = pd.Vampire; p.Thorns = pd.Thorns; p.Greedy = pd.Greedy;
            p.SkillPoints = pd.SkillPoints;
            p.StepCounter = pd.StepCounter;
            p.LearnedNodes = new HashSet<string>(pd.LearnedNodes);
            p.Blueprints = new HashSet<string>(pd.Blueprints);
            for (int i = 0; i < pd.Hotbar.Count && i < p.Quickbar.Length; i++)
                p.Quickbar[i] = pd.Hotbar[i];
            p.CraftLevels = new Dictionary<string, int>(pd.CraftLevels);
            p.CraftXp = new Dictionary<string, int>(pd.CraftXp);
            p.Statuses = new Dictionary<string, int>(pd.Statuses);
            p.Recalc();

            // Restore floor cache
            foreach (var fd in data.Floors)
            {
                var floor = FloorFromData(fd);
                if (floor != null)
                    game.SetFloorCache(fd.Depth, floor, fd.LocationKey);
            }

            // Restore world mode/depth/location
            if (data.World.Mode == "dungeon" && data.World.Depth > 0)
            {
                game.SetWorldMode("dungeon", data.World.Depth);
            }
            else if (!string.IsNullOrEmpty(data.World.Location))
            {
                game.SetWorldMode("overworld", 0, data.World.Location);
            }

            // Restore quests (полное состояние, включая generated)
            game.Quests.Available = new HashSet<string>(data.Quests.Available);
            game.Quests.Active = new Dictionary<string, QuestState>();
            foreach (var q in data.Quests.Active)
            {
                var qDef = QuestData.Get(q.Key);
                game.Quests.Active[q.Key] = new QuestState
                {
                    Progress = q.Progress,
                    Count = q.Generated ? q.Count : (qDef?.Count ?? q.Count),
                    Generated = q.Generated,
                    Type = q.Generated ? q.Type : (qDef?.Type ?? ""),
                    Target = q.Generated ? q.Target : (qDef?.Target ?? ""),
                    Name = q.Generated ? q.Name : (qDef?.Name ?? ""),
                    Desc = q.Generated ? q.Desc : (qDef?.Desc ?? ""),
                    RewardGold = q.Generated ? q.RewardGold : (qDef?.RewardGold ?? 0),
                    RewardXp = q.Generated ? q.RewardXp : (qDef?.RewardXp ?? 0),
                };
            }
            game.Quests.Completed = new HashSet<string>(data.Quests.Completed);
            game.Quests.TurnedIn = new HashSet<string>(data.Quests.TurnedIn);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static PlayerData PlayerToData(Player p)
    {
        return new PlayerData
        {
            X = p.X, Y = p.Y,
            Hp = p.Hp, MaxHp = p.MaxHp,
            Mana = p.Mana, MaxMana = p.MaxMana,
            Level = p.Level, Xp = p.Xp, XpNext = p.XpNext,
            FreePoints = p.FreePoints, Gold = p.Gold,
            Str = p.Str, Agi = p.Agi, Vit = p.Vit,
            Luk = p.Luk, DfStat = p.DfStat, IntStat = p.IntStat,
            ClassKey = p.ClassKey, ClassName = p.ClassName,
            ClassTier = p.ClassTier, ClassBase = p.ClassBase,
            ClassChanges = p.ClassChanges,
            Weapon = p.Weapon, Armor = p.Armor,
            Head = p.Head, Legs = p.Legs, Boots = p.Boots,
            Inventory = new Dictionary<string, int>(p.Inventory),
            DoubleStrike = p.DoubleStrike, Regen = p.Regen,
            Vampire = p.Vampire, Thorns = p.Thorns, Greedy = p.Greedy,
            SkillPoints = p.SkillPoints,
            StepCounter = p.StepCounter,
            LearnedNodes = p.LearnedNodes.ToList(),
            Blueprints = p.Blueprints.ToList(),
            Hotbar = p.Quickbar.ToList(),
            CraftLevels = new Dictionary<string, int>(p.CraftLevels),
            CraftXp = new Dictionary<string, int>(p.CraftXp),
            Statuses = new Dictionary<string, int>(p.Statuses),
        };
    }

    // ── Floor serialization ──

    /// <summary>
    /// Сериализует карту целиком. public — используется и синглом, и
    /// мультиплеером (хост сохраняет все посещённые этажи/локации).
    /// </summary>
    public static FloorData FloorToData(int depth, Dungeon d)
    {
        var fd = new FloorData
        {
            Depth = depth,
            W = d.W, H = d.H,
            StairsPos = ToPoint(d.StairsPos),
            TreasuryPos = ToPoint(d.TreasuryPos),
            LocationKey = d.LocationKey,
        };

        // Тайлы строками цифр (компактно, как в Python dungeon_to_dict)
        for (int y = 0; y < d.H; y++)
        {
            var sb = new System.Text.StringBuilder(d.W);
            for (int x = 0; x < d.W; x++)
                sb.Append((int)d.Tiles[y][x]);
            fd.Tiles.Add(sb.ToString());
        }

        foreach (var r in d.Rooms)
            fd.Rooms.Add(new RoomSave { X = r.X, Y = r.Y, W = r.W, H = r.H });

        foreach (var m in d.Monsters)
        {
            fd.Monsters.Add(new MonsterSave
            {
                X = m.X, Y = m.Y,
                Hp = m.Hp, MaxHp = m.MaxHp, Atk = m.Atk, Df = m.Df,
                Key = m.MonsterKey, Name = m.Name, Ch = m.Ch,
                Color = $"{m.Color.R},{m.Color.G},{m.Color.B}",
                XpValue = m.XpValue,
                Boss = m.Boss, Immortal = m.Immortal, IsNpc = m.IsNpc,
                Ability = m.Ability, NpcType = m.NpcType,
                Statuses = new Dictionary<string, int>(m.Statuses),
            });
        }

        foreach (var ((x, y), key) in d.Items)
            fd.Items.Add(new ItemSave { X = x, Y = y, Key = key });

        foreach (var h in HarvestSystem.Serialize(d))
        {
            fd.Harvestables.Add(new HarvestSave
            {
                X = Convert.ToInt32(h["x"]), Y = Convert.ToInt32(h["y"]),
                Kind = h["kind"]?.ToString() ?? "",
                Hp = Convert.ToInt32(h["hp"]), MaxHp = Convert.ToInt32(h["max_hp"]),
                Has = Convert.ToBoolean(h["has"]),
                ReadyAt = Convert.ToDouble(h["ready_at"]),
                SpawnedAt = Convert.ToDouble(h["spawned_at"]),
            });
        }

        foreach (var c in d.BossDoorCells)
            fd.BossDoorCells.Add(new PointSave { X = c.Item1, Y = c.Item2 });

        foreach (var ((x, y), tr) in d.Transitions)
        {
            fd.Transitions.Add(new TransitionSave
            {
                X = x, Y = y, To = tr.To,
                EntryX = tr.Entry.Item1, EntryY = tr.Entry.Item2,
            });
        }

        foreach (var (x, y) in d.BarrierTiles)
            fd.BarrierTiles.Add(new PointSave { X = x, Y = y });

        foreach (var (x, y) in d.Explored)
            fd.Explored.Add(new PointSave { X = x, Y = y });

        foreach (var ((x, y), loot) in d.ChestLoot)
            fd.ChestLoot.Add(new ChestLootSave { X = x, Y = y, Loot = new Dictionary<string, int>(loot) });

        if (d.MerchantStock != null)
        {
            foreach (var e in d.MerchantStock)
                fd.MerchantStock.Add(new ShopEntrySave { Key = e.Key, Price = e.Price });
        }

        return fd;
    }

    /// <summary>Восстанавливает карту из <see cref="FloorToData"/>.</summary>
    public static Dungeon? FloorFromData(FloorData fd)
    {
        try
        {
            var d = new Dungeon(fd.W, fd.H) { LocationKey = fd.LocationKey };
            for (int y = 0; y < fd.H && y < fd.Tiles.Count; y++)
            {
                string row = fd.Tiles[y];
                for (int x = 0; x < fd.W && x < row.Length; x++)
                    if (Enum.TryParse<Tile>(row[x].ToString(), out var t))
                        d.Tiles[y][x] = t;
            }
            foreach (var r in fd.Rooms)
                d.Rooms.Add(new Room(r.X, r.Y, r.W, r.H));
            d.StairsPos = ToTuple(fd.StairsPos);
            d.TreasuryPos = ToTuple(fd.TreasuryPos);
            foreach (var c in fd.BossDoorCells) d.BossDoorCells.Add((c.X, c.Y));
            foreach (var t in fd.Transitions)
                d.Transitions[(t.X, t.Y)] = (t.To, (t.EntryX, t.EntryY));
            foreach (var c in fd.BarrierTiles) d.BarrierTiles.Add((c.X, c.Y));
            foreach (var c in fd.Explored) d.Explored.Add((c.X, c.Y));
            foreach (var c in fd.ChestLoot) d.ChestLoot[(c.X, c.Y)] = new Dictionary<string, int>(c.Loot);
            if (fd.MerchantStock.Count > 0)
            {
                d.MerchantStock = fd.MerchantStock.Select(e =>
                    new RecipeData.ShopEntry { Key = e.Key, Price = e.Price }).ToList();
            }
            foreach (var m in fd.Monsters)
            {
                var colParts = m.Color.Split(',');
                var color = colParts.Length == 3
                    ? new Microsoft.Xna.Framework.Color(
                        byte.TryParse(colParts[0], out var rr) ? rr : (byte)255,
                        byte.TryParse(colParts[1], out var gg) ? gg : (byte)255,
                        byte.TryParse(colParts[2], out var bb) ? bb : (byte)255)
                    : Microsoft.Xna.Framework.Color.White;
                var e = new Entity(m.X, m.Y, m.Hp, m.MaxHp, m.Atk, m.Df,
                    m.Name, m.Ch, color, m.XpValue, m.Boss, m.Ability)
                {
                    MonsterKey = m.Key,
                    Immortal = m.Immortal,
                    IsNpc = m.IsNpc,
                    NpcType = m.NpcType,
                };
                foreach (var (k, v) in m.Statuses) e.Statuses[k] = v;
                d.Monsters.Add(e);
            }
            foreach (var it in fd.Items) d.Items[(it.X, it.Y)] = it.Key;
            foreach (var h in fd.Harvestables)
            {
                d.Harvestables[(h.X, h.Y)] = new HarvestRecord
                {
                    Kind = h.Kind, Hp = h.Hp, MaxHp = h.MaxHp,
                    Has = h.Has, ReadyAt = h.ReadyAt, SpawnedAt = h.SpawnedAt,
                };
            }
            return d;
        }
        catch
        {
            return null;
        }
    }

    private static PointSave? ToPoint((int X, int Y)? p) =>
        p is { } v ? new PointSave { X = v.X, Y = v.Y } : null;

    private static (int X, int Y)? ToTuple(PointSave? p) =>
        p is null ? null : (p.X, p.Y);

    private static string SlotPath(int slot)
    {
        return Path.Combine(SaveDir, $"savegame_{slot}.json");
    }

    /// <summary>
    /// Get all existing save slots.
    /// </summary>
    public static List<int> GetExistingSlots()
    {
        var slots = new List<int>();
        for (int i = 1; i <= 3; i++)
        {
            if (SlotExists(i))
                slots.Add(i);
        }
        return slots;
    }
}
