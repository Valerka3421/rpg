using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Aethelgard.Core;

/// <summary>
/// Generic entity: monster, NPC, or player base.
/// Ported from Python core/entities.py:Entity.
/// </summary>
public class Entity
{
    public int X, Y;
    public int Hp, MaxHp;
    public int Atk, Df;
    public string Name = "";
    public char Ch = '?';
    public Color Color = Color.White;
    public int XpValue;
    public bool Boss;
    public bool IsMerchant;
    public bool IsNpc;
    public string? NpcType;
    public string? Ability;
    public bool Immortal;

    /// <summary>Monster data key (e.g. "rat", "boss_goblin_king").</summary>
    public string MonsterKey = "";

    // Guard NPC fields
    public int HomeX, HomeY;
    public float GuardTimer;

    // Facing direction for ranged attacks
    public (int Dx, int Dy) Facing = (1, 0);

    // Status effects: { "burning": turns_left, "slow": turns_left, ... }
    public Dictionary<string, int> Statuses = new();

    // Timers
    public float ActTimer;
    public float BreathTimer;
    public float AnimTime;
    public float AttackAnim;
    public float MoveBob;

    public Entity(int x, int y, int hp, int maxHp, int atk, int df,
                  string name, char ch, Color color, int xpValue = 0,
                  bool boss = false, string? ability = null)
    {
        X = x; Y = y;
        Hp = hp; MaxHp = maxHp;
        Atk = atk; Df = df;
        Name = name; Ch = ch; Color = color;
        XpValue = xpValue;
        Boss = boss;
        Ability = ability;
    }

    public bool Alive => Hp > 0;

    public bool HasStatus(string key) => Statuses.ContainsKey(key);

    public void AddStatus(string key, int? duration = null)
    {
        var def = StatusData.Get(key);
        if (def == null) return;
        int d = duration ?? def.Duration;
        Statuses[key] = Math.Max(Statuses.GetValueOrDefault(key), d);
    }

    public void RemoveStatus(string key) => Statuses.Remove(key);
    public void ClearStatuses() => Statuses.Clear();
}

/// <summary>
/// Player character with stats, inventory, equipment.
/// Ported from Python core/entities.py:Player.
/// </summary>
public class Player : Entity
{
    public string ClassKey = "default";
    public string ClassName = "Приключенец";
    public int ClassTier = 1;
    public string ClassBase = "default";
    public int ClassChanges;
    public int Level = 1;
    public int Xp = 0;
    public int XpNext = Config.XpToNext(1);
    public int FreePoints = 0;
    public int Gold = 0;

    // Base stats
    public int Str = 5, Agi = 5, Vit = 5, Luk = 5, DfStat = 5, IntStat = 5;

    // Derived stats (recalculated by Recalc)
    public int MaxMana;
    public int Mana;
    public float DodgeChance;
    public float CritChance;
    public float MoveSpeed = 1.0f;

    // Internal bonuses from equipment/skills
    public float WCritBonus, WDodgeBonus, ADodgeBonus, SkillDodge, SkillCrit;

    // Equipment
    public string? Weapon, Armor, Head, Legs, Boots;

    // Compatibility aliases
    public string? Helmet { get => Head; set => Head = value; }
    public string? Leggings { get => Legs; set => Legs = value; }

    // Inventory: itemKey → count
    public Dictionary<string, int> Inventory = new();

    // Perks
    public bool DoubleStrike, Regen, Vampire, Thorns, Greedy;
    public bool GodModeStat;

    // Skill progression
    public int SkillPoints = 1;
    public int StepCounter = 0;
    public Dictionary<string, RecipeData.SkillProgress> Skills = new();
    public HashSet<string> LearnedNodes = new();
    public HashSet<string> Blueprints = new();

    // Class tier / quickbar / active skills
    public string?[] Quickbar = new string?[Config.QuickbarSlots];
    public Dictionary<string, float> ActiveSkillCd = new();

    // Movement cooldown
    public float MoveTimer = 0f;

    public Player(string classKey = "default")
        : base(0, 0, 25, 25, 4, 1, "Герой", '@', Config.ColPlayer, 0)
    {
        if (!ClassData.All.ContainsKey(classKey)) classKey = "default";
        var cls = ClassData.Data(classKey);
        var stats = ClassData.ClassStats(classKey);
        ClassKey = classKey;
        ClassName = cls.Name;
        ClassTier = cls.Tier;
        ClassBase = string.IsNullOrEmpty(cls.Base) ? classKey : cls.Base;
        ClassChanges = Math.Max(0, ClassTier - 1);
        Str = stats.GetValueOrDefault("str", 5);
        Agi = stats.GetValueOrDefault("agi", 5);
        Vit = stats.GetValueOrDefault("vit", 5);
        Luk = stats.GetValueOrDefault("luk", 5);
        DfStat = stats.GetValueOrDefault("df_stat", 5);
        IntStat = stats.GetValueOrDefault("int", 5);
        Weapon = cls.StartWeapon;
        Armor = cls.StartArmor;
        foreach (var (k, v) in cls.StartItems)
            Inventory[k] = Inventory.GetValueOrDefault(k) + v;
        Skills = RecipeData.DefaultSkills();
        Recalc();
        Hp = MaxHp;
        Mana = MaxMana;
    }

    // ── Derived stats ──
    public class StatResult
    {
        public int MaxHp, Atk, Df, MaxMana;
        public float Dodge, Crit;
        public float WCritBonus, WDodgeBonus, ADodgeBonus, SkillDodge, SkillCrit;
    }

    /// <summary>Single formula used by recalc and the class-select preview.</summary>
    public static StatResult ComputeStats(
        int str, int agi, int vit, int luk, int dfStat, int intStat, int level,
        string? weapon, string? armor, string? head, string? legs, string? boots,
        IReadOnlyDictionary<string, double> effects)
    {
        int wb = 0, ab = 0, hpExtra = 0;
        float wCrit = 0, wDodge = 0, aDodge = 0;
        int wDf = 0, manaExtra = 0;

        void Slot(string slot, string? key)
        {
            if (string.IsNullOrEmpty(key)) return;
            var (baseKey, lvl) = ItemData.ParseItemKey(key);
            var it = ItemData.Get(baseKey);
            if (it == null) return;
            int value = it.IntValue + lvl;
            if (slot == "weapon")
            {
                wb = value;
                wCrit = (float)it.CritBonus;
                wDodge = (float)it.DodgeBonus;
                wDf = it.DfBonus;
            }
            else
            {
                ab += value;
                aDodge += (float)it.DodgeBonus;
            }
            hpExtra += it.HpBonus;
            manaExtra += it.ManaBonus;
        }

        Slot("weapon", weapon);
        Slot("armor", armor);
        Slot("head", head);
        Slot("legs", legs);
        Slot("boots", boots);

        float skillDodge = (float)effects.GetValueOrDefault("dodge");
        float skillCrit = (float)effects.GetValueOrDefault("crit");

        float dodgeBase = Math.Min(Config.DodgeCap, agi * 0.015f);
        float dodgeTotal = Math.Max(0f, Math.Min(Config.DodgeTotalCap,
            dodgeBase + wDodge + aDodge + skillDodge));
        float critBase = Math.Min(Config.CritBaseCap, luk * 0.02f);
        float critTotal = Math.Min(Config.CritTotalCap, critBase + wCrit + skillCrit);

        return new StatResult
        {
            MaxHp = 15 + vit * 2 + hpExtra + (int)effects.GetValueOrDefault("hp"),
            Atk = 2 + str / 2 + wb + (int)effects.GetValueOrDefault("atk"),
            Df = dfStat / 2 + ab + wDf + (int)effects.GetValueOrDefault("df"),
            MaxMana = 10 + level * 2 + intStat * 2 + manaExtra + (int)effects.GetValueOrDefault("mana"),
            Dodge = dodgeTotal,
            Crit = critTotal,
            WCritBonus = wCrit,
            WDodgeBonus = wDodge,
            ADodgeBonus = aDodge,
            SkillDodge = skillDodge,
            SkillCrit = skillCrit,
        };
    }

    /// <summary>Aggregate passive bonuses from class nodes and statuses.</summary>
    public Dictionary<string, double> Effects()
    {
        var totals = SkillTree.Aggregate(this);
        foreach (var status in Statuses.Keys)
        {
            if (!StatusData.Mods.TryGetValue(status, out var mods)) continue;
            foreach (var (key, value) in mods)
                totals[key] = totals.GetValueOrDefault(key) + value;
        }
        return totals;
    }

    public double Effect(string name, double def = 0) =>
        Effects().GetValueOrDefault(name, def);

    public double Lifesteal => Effect("lifesteal");
    public double GoldPct => Effect("gold_pct");
    public double XpPct => Effect("xp_pct");
    public double QuestGoldPct => Effect("quest_gold");
    public double QuestXpPct => Effect("quest_xp");
    public int VisionBonus => (int)Effect("fov");

    public void Recalc()
    {
        int oldMax = MaxHp;
        int oldMana = MaxMana;
        var effects = Effects();
        var stats = ComputeStats(Str, Agi, Vit, Luk, DfStat, IntStat, Level,
            Weapon, Armor, Head, Legs, Boots, effects);
        MaxHp = stats.MaxHp;
        MaxMana = stats.MaxMana;
        Atk = stats.Atk;
        Df = stats.Df;
        WCritBonus = stats.WCritBonus;
        WDodgeBonus = stats.WDodgeBonus;
        ADodgeBonus = stats.ADodgeBonus;
        SkillDodge = stats.SkillDodge;
        SkillCrit = stats.SkillCrit;
        DodgeChance = stats.Dodge;
        CritChance = stats.Crit;
        if (MaxHp > oldMax) Hp += MaxHp - oldMax;
        if (MaxMana > oldMana) Mana += MaxMana - oldMana;
        Hp = Math.Min(Hp, MaxHp);
        Mana = Math.Min(Mana, MaxMana);
    }

    public float Dodge =>
        Math.Max(0f, Math.Min(Config.DodgeTotalCap,
            Math.Min(Config.DodgeCap, Agi * 0.015f) + WDodgeBonus + ADodgeBonus + SkillDodge));

    public float Crit =>
        Math.Min(Config.CritTotalCap,
            Math.Min(Config.CritBaseCap, Luk * 0.02f) + WCritBonus + SkillCrit);

    public float AttackCooldown
    {
        get
        {
            double speed = Math.Max(0, Effect("attack_speed"));
            double frenzy = Math.Max(0, Effect("frenzy"));
            if (frenzy > 0)
                speed += frenzy * (1.0 - Hp / (double)Math.Max(1, MaxHp));
            return (float)(Config.PlayerAttackCooldown / (1.0 + speed));
        }
    }

    public float MoveCooldown
    {
        get
        {
            double speed = Math.Max(0, Effect("move_speed"));
            return (float)(Config.PlayerMoveCooldown / (1.0 + speed));
        }
    }

    public double RageBonus
    {
        get
        {
            double rage = Math.Max(0, Effect("rage"));
            if (rage > 0 && Hp < MaxHp * 0.5) return 1.0 + rage;
            return 1.0;
        }
    }

    public bool HasCompanion => (int)Effect("summon") != 0;

    public List<ClassData.ActiveSkill> ActiveSkills() =>
        ClassData.Data(ClassKey).ActiveSkills;

    public bool AssignQuickbar(int slotIdx, string? itemKey)
    {
        if (slotIdx < 0 || slotIdx >= Config.QuickbarSlots) return false;
        Quickbar[slotIdx] = itemKey;
        return true;
    }

    public bool ClearQuickbarSlot(int slotIdx)
    {
        if (slotIdx < 0 || slotIdx >= Config.QuickbarSlots) return false;
        Quickbar[slotIdx] = null;
        return true;
    }

    // ── Inventory ──
    public void AddItem(string key, int count = 1) =>
        Inventory[key] = Inventory.GetValueOrDefault(key) + count;

    public bool RemoveItem(string key, int count = 1)
    {
        if (!Inventory.TryGetValue(key, out int have) || have < count) return false;
        have -= count;
        if (have <= 0) Inventory.Remove(key);
        else Inventory[key] = have;
        return true;
    }

    public int ItemCount(string key) => Inventory.GetValueOrDefault(key);

    public bool EquipItem(string key)
    {
        var def = ItemData.Get(key);
        if (def == null) return false;
        string? slot = def.Kind switch
        {
            ItemData.ItemType.Weapon => "weapon",
            ItemData.ItemType.Armor => "armor",
            ItemData.ItemType.Helmet => "head",
            ItemData.ItemType.Leggings => "legs",
            ItemData.ItemType.Boots => "boots",
            _ => null,
        };
        if (slot == null) return false;
        var (ok, _) = ClassData.CanUse(ClassKey, key);
        if (!ok) return false;

        string? current = slot switch
        {
            "weapon" => Weapon, "armor" => Armor, "head" => Head,
            "legs" => Legs, "boots" => Boots, _ => null,
        };
        if (current != null) AddItem(current, 1);
        switch (slot)
        {
            case "weapon": Weapon = key; break;
            case "armor": Armor = key; break;
            case "head": Head = key; break;
            case "legs": Legs = key; break;
            case "boots": Boots = key; break;
        }
        RemoveItem(key, 1);
        Recalc();
        return true;
    }

    public bool UnequipSlot(string slot)
    {
        string? current = slot switch
        {
            "weapon" => Weapon, "armor" => Armor, "head" => Head,
            "legs" => Legs, "boots" => Boots, _ => null,
        };
        if (current == null) return false;
        AddItem(current, 1);
        switch (slot)
        {
            case "weapon": Weapon = null; break;
            case "armor": Armor = null; break;
            case "head": Head = null; break;
            case "legs": Legs = null; break;
            case "boots": Boots = null; break;
        }
        Recalc();
        return true;
    }

    public void GainXp(int amount)
    {
        if (amount <= 0) return;
        Xp += (int)(amount * (1.0 + XpPct));
        int guard = 0;
        while (Xp >= XpNext && guard++ < Config.XpLevelGuard)
        {
            Xp -= XpNext;
            Level++;
            XpNext = Config.XpToNext(Level);
            FreePoints += 5;
            SkillPoints += 1;
            Recalc();
            Hp = MaxHp;
            Mana = MaxMana;
        }
    }
}

/// <summary>Rectangular dungeon room.</summary>
public class Room
{
    public int X, Y, W, H;
    public Room(int x, int y, int w, int h) { X = x; Y = y; W = w; H = h; }
    public (int X, int Y) Center => (X + W / 2, Y + H / 2);
    public bool Contains(int x, int y) => x >= X && x < X + W && y >= Y && y < Y + H;
    public bool Intersects(Room other, int pad = 1) =>
        !(X + W + pad < other.X || other.X + other.W + pad < X ||
          Y + H + pad < other.Y || other.Y + other.H + pad < Y);
}

/// <summary>Dungeon/map: tile grid + entities + items.</summary>
public class Dungeon
{
    public int W, H;
    public Tile[][] Tiles;       // [y][x]
    public List<Room> Rooms = new();
    public List<Entity> Monsters = new();
    public Dictionary<(int, int), string> Items = new();
    public (int, int)? StairsPos;
    public Dictionary<(int, int), HarvestRecord> Harvestables = new();
    public Dictionary<(int, int), (string To, (int, int) Entry)> Transitions = new();
    public HashSet<(int, int)> BarrierTiles = new();
    public (int, int)? TreasuryPos;
    public HashSet<(int, int)> BossDoorCells = new();
    public Dictionary<(int, int), Dictionary<string, int>> ChestLoot = new();
    public HashSet<(int, int)> Explored = new();
    public int Depth = 1;

    public Dungeon(int w, int h)
    {
        W = w; H = h;
        Tiles = new Tile[h][];
        for (int y = 0; y < h; y++)
        {
            Tiles[y] = new Tile[w];
            for (int x = 0; x < w; x++)
                Tiles[y][x] = Tile.Wall;
        }
    }

    public Tile TileAt(int x, int y)
    {
        if (x < 0 || x >= W || y < 0 || y >= H) return Tile.Wall;
        return Tiles[y][x];
    }

    public void SetTile(int x, int y, Tile t)
    {
        if (x < 0 || x >= W || y < 0 || y >= H) return;
        Tiles[y][x] = t;
    }

    public bool IsWalkable(int x, int y)
    {
        var t = TileAt(x, y);
        if (t is Tile.Wall or Tile.LockedDoor or Tile.Chest) return false;
        if (Harvestables.ContainsKey((x, y))) return false;
        return true;
    }

    public Entity? MonsterAt(int x, int y) =>
        Monsters.Find(m => m.X == x && m.Y == y && m.Alive);

    public Entity? NpcAt(int x, int y) =>
        Monsters.Find(m => m.X == x && m.Y == y && m.IsNpc && m.Alive);
}

public class FloatText
{
    public float X, Y;
    public string Text = "";
    public Color Color;
    public float Life = 0.7f;
}

/// <summary>Flying projectile: arrow, magic orb, lightning etc.</summary>
public class Projectile
{
    public float X, Y;
    public float Tx, Ty;
    public string Kind = "arrow";
    public float Speed = 12.0f;
    public int Damage;
    public Color Color = Color.White;
    public bool Alive = true;
    public int SourcePid = -1;
    public char TargetCh = '\0';
    public float Life = 2.0f;
}

public class Message
{
    public string Text = "";
    public Color Color = Config.ColText;
    public Message() { }
    public Message(string text, Color? color = null) { Text = text; Color = color ?? Config.ColText; }
}
