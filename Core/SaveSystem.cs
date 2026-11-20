using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aethelgard.Core;
using Aethelgard.Game;

namespace Aethelgard.Core;

/// <summary>
/// Save/load system — JSON serialization with versioned slots.
/// Ported from Python core/save.py.
///
/// Format: JSON file with:
/// - version: 1
/// - player: stats, inventory, equipment, class, level, position
/// - world: mode, depth
/// - quests: available, active, completed, turned_in
/// - timestamp
///
/// Save slots: 1-3, stored in "saves/" directory.
/// </summary>
public static class SaveSystem
{
    public const int SaveVersion = 1;
    public static readonly string SaveDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Aethelgard", "saves");

    public class SaveData
    {
        public int Version { get; set; } = SaveVersion;
        public DateTime Timestamp { get; set; }
        public PlayerData Player { get; set; } = new();
        public WorldData World { get; set; } = new();
        public QuestDataSave Quests { get; set; } = new();
    }

    public class PlayerData
    {
        public int X, Y;
        public int Hp, MaxHp, Mana, MaxMana;
        public int Level, Xp, XpNext, FreePoints, Gold;
        public int Str, Agi, Vit, Luk, DfStat, IntStat;
        public string ClassKey = "warrior";
        public string ClassName = "Warrior";
        public int ClassTier = 1;
        public string? Weapon, Armor, Head, Legs, Boots;
        public Dictionary<string, int> Inventory = new();
        public bool DoubleStrike, Regen, Vampire, Thorns, Greedy;
        public int SkillPoints;
        public List<string> LearnedNodes = new();
        public Dictionary<string, int> CraftLevels = new();
        public Dictionary<string, int> CraftXp = new();
    }

    public class WorldData
    {
        public string Mode = "overworld";
        public int Depth = 0;
    }

    public class QuestDataSave
    {
        public List<string> Available = new();
        public Dictionary<string, int> Active = new();
        public List<string> Completed = new();
        public List<string> TurnedIn = new();
    }

    /// <summary>
    /// Save game to a slot.
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
                },
                Quests = new QuestDataSave
                {
                    Available = game.Quests.Available.ToList(),
                    Active = new Dictionary<string, int>(game.Quests.Active),
                    Completed = game.Quests.Completed.ToList(),
                    TurnedIn = game.Quests.TurnedIn.ToList(),
                },
            };

            string path = SlotPath(slot);
            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = true,
            });
            File.WriteAllText(path, json);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Load game from a slot. Returns null if failed.
    /// </summary>
    public static SaveData? Load(int slot)
    {
        try
        {
            string path = SlotPath(slot);
            if (!File.Exists(path)) return null;

            string json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<SaveData>(json);
            if (data == null) return null;

            // Version migration would go here
            if (data.Version > SaveVersion)
                return null;  // too new

            return data;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Check if a save slot exists.
    /// </summary>
    public static bool SlotExists(int slot)
    {
        return File.Exists(SlotPath(slot));
    }

    /// <summary>
    /// Get slot info (level, class, timestamp) without full load.
    /// </summary>
    public static (int Level, string Class, DateTime Timestamp)? SlotInfo(int slot)
    {
        var data = Load(slot);
        if (data == null) return null;
        return (data.Player.Level, data.Player.ClassName, data.Timestamp);
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
            p.Level = pd.Level; p.Xp = pd.Xp; p.XpNext = pd.XpNext;
            p.FreePoints = pd.FreePoints; p.Gold = pd.Gold;
            p.Str = pd.Str; p.Agi = pd.Agi; p.Vit = pd.Vit;
            p.Luk = pd.Luk; p.DfStat = pd.DfStat; p.IntStat = pd.IntStat;
            p.ClassKey = pd.ClassKey; p.ClassName = pd.ClassName;
            p.ClassTier = pd.ClassTier;
            p.Weapon = pd.Weapon; p.Armor = pd.Armor;
            p.Head = pd.Head; p.Legs = pd.Legs; p.Boots = pd.Boots;
            p.Inventory = new Dictionary<string, int>(pd.Inventory);
            p.DoubleStrike = pd.DoubleStrike; p.Regen = pd.Regen;
            p.Vampire = pd.Vampire; p.Thorns = pd.Thorns; p.Greedy = pd.Greedy;
            p.SkillPoints = pd.SkillPoints;
            p.LearnedNodes = new HashSet<string>(pd.LearnedNodes);
            p.CraftLevels = new Dictionary<string, int>(pd.CraftLevels);
            p.CraftXp = new Dictionary<string, int>(pd.CraftXp);
            p.Recalc();

            // Restore world state
            // Note: dungeon floors are not saved in this version — only overworld state
            // In a full implementation, floor cache would be serialized

            // Restore quests
            game.Quests.Available = new HashSet<string>(data.Quests.Available);
            game.Quests.Active = new Dictionary<string, int>(data.Quests.Active);
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
            ClassTier = p.ClassTier,
            Weapon = p.Weapon, Armor = p.Armor,
            Head = p.Head, Legs = p.Legs, Boots = p.Boots,
            Inventory = new Dictionary<string, int>(p.Inventory),
            DoubleStrike = p.DoubleStrike, Regen = p.Regen,
            Vampire = p.Vampire, Thorns = p.Thorns, Greedy = p.Greedy,
            SkillPoints = p.SkillPoints,
            LearnedNodes = p.LearnedNodes.ToList(),
            CraftLevels = new Dictionary<string, int>(p.CraftLevels),
            CraftXp = new Dictionary<string, int>(p.CraftXp),
        };
    }

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
