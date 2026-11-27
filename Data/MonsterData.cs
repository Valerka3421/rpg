using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Aethelgard.Data;

/// <summary>
/// Монстры, боссы и масштабирование — полная паритет с
/// Python core/data/monsters.py. ДАННЫЕ ВИДОВ ЖИВУТ В КЛАССАХ МОНСТРОВ
/// (Entities/Monsters/*.cs, перенос из data/monsters.json); здесь —
/// реестр All, построенный из классов через MonsterFactory, и таблицы
/// дропа/боссов/кристаллов маны в коде.
/// </summary>
public static partial class MonsterData
{
    // ── Реестр видов: строится из классов монстров ──
    public static readonly Dictionary<string, Def> All;

    // ── Боссы по этажам (5/10/15/20) ──
    public static readonly Dictionary<int, string> BossByDepth = new()
    {
        [5] = "boss_g_5",
        [10] = "boss_l_10",
        [15] = "boss_v_15",
        [20] = "boss_a_20",
    };

    // ── Гарантированные дропы боссов (по глифу) ──
    public static readonly Dictionary<string, string[]> BossDrops = new()
    {
        ["G"] = new[] { "goblin_ear", "goblin_ear", "bone" },
        ["L"] = new[] { "bone", "bone", "wraith_essence", "wraith_essence" },
        ["V"] = new[] { "vampire_fang", "vampire_fang", "vampire_fang" },
        ["A"] = new[] { "dragon_scale", "dragon_scale", "dragon_scale", "dragon_scale", "dragon_scale" },
    };

    // ── Шансы материалов с монстров (по глифу) ──
    public static readonly Dictionary<string, (string Item, double Chance)[]> MonsterDrops = new()
    {
        ["r"] = new[] { ("rat_tail", 0.5), ("mushroom", 0.1), ("hide", 0.3) },
        ["g"] = new[] { ("goblin_ear", 0.5), ("wood", 0.15), ("hide", 0.3) },
        ["a"] = new[] { ("goblin_ear", 0.45), ("wood", 0.2), ("hide", 0.25) },
        ["s"] = new[] { ("bone", 0.7), ("stone", 0.15) },
        ["S"] = new[] { ("goblin_ear", 0.45), ("mushroom", 0.25) },
        ["O"] = new[] { ("orc_tusk", 0.5), ("ore", 0.15) },
        ["T"] = new[] { ("ogre_leather", 0.5), ("wood", 0.3), ("stone", 0.2), ("hide", 0.3) },
        ["D"] = new[] { ("demon_scale", 0.5), ("ore", 0.2) },
        ["v"] = new[] { ("vampire_fang", 0.5), ("bone", 0.1) },
        ["d"] = new[] { ("dragon_scale", 0.45), ("ore", 0.2) },
        ["Y"] = new[] { ("golem_core", 0.35), ("stone", 0.7), ("ore", 0.2) },
        ["W"] = new[] { ("wraith_essence", 0.5), ("bone", 0.15) },
        ["e"] = new[] { ("venison", 0.7), ("bone", 0.2) },
        ["w"] = new[] { ("wolf_pelt", 0.6), ("venison", 0.2), ("bone", 0.15), ("hide", 0.25) },
        ["b"] = new[] { ("bear_pelt", 0.6), ("bear_claw", 0.5), ("venison", 0.25), ("hide", 0.3) },
        ["j"] = new[] { ("slime_gel", 0.8), ("mushroom", 0.15) },
        ["p"] = new[] { ("boar_tusk", 0.55), ("venison", 0.55), ("bone", 0.1), ("hide", 0.2) },
        ["c"] = new[] { ("venison", 0.45), ("hide", 0.3), ("bone", 0.15) },
        ["m"] = new[] { ("slime_gel", 0.5), ("hide", 0.3), ("mushroom", 0.1) },
        ["u"] = new[] { ("bone", 0.55), ("hide", 0.4), ("stone", 0.1) },
    };

    // ── Кристаллы маны ──
    public static readonly Dictionary<string, string> ManaCrystalBySize = new()
    {
        ["small"] = "mana_crystal_small",
        ["normal"] = "mana_crystal",
        ["large"] = "mana_crystal_large",
    };

    public static readonly Dictionary<string, int> ManaPowderByCrystal = new()
    {
        ["mana_crystal_small"] = 1,
        ["mana_crystal"] = 2,
        ["mana_crystal_large"] = 4,
    };

    static MonsterData()
    {
        // Данные видов берём из классов монстров (перенесены из monsters.json).
        All = new Dictionary<string, Def>();
        foreach (var key in MonsterFactory.AllKeys)
        {
            var monster = MonsterFactory.Create(key)
                ?? throw new InvalidOperationException($"No species class for monster: {key}");
            All[key] = monster.Def;
        }
    }

    /// <summary>
    /// Пул монстров для этажа глубины depth. В оригинале DungeonGen вызывал
    /// несуществующий MonsterData.PoolForDepth(depth). Выбор из пула —
    /// взвешенный по глубине (см. DungeonGen.SpawnDungeonMonsters): «устаревшие»
    /// для глубины монстры имеют экспоненциально меньший вес.
    /// </summary>
    public static string[] PoolForDepth(int depth)
    {
        var pool = All.Values
            .Where(d => !d.OverworldOnly && !d.Boss && d.MinDepth <= depth)
            .Select(d => d.Key)
            .ToArray();
        return pool.Length > 0 ? pool : new[] { "rat" };
    }

    public class Def
    {
        public string Key = "";
        public string Name = "";
        public string Ch = "?";
        public int Hp, Atk, Df, Xp;
        public Color Color = Color.White;
        public int MinDepth = 1;
        public string? Ability;
        public bool Boss;
        public int BossDepth;
        public bool OverworldOnly;
        public bool Passive;
        public bool Flees;
        public bool Gold = true;
    }

    private static Random Rng => Random.Shared;   // потокобезопасно: дёргается и из серверного потока MP

    public const double ManaCrystalChance = 0.08;

    public static Def? Get(string? key) =>
        string.IsNullOrEmpty(key) ? null : All.GetValueOrDefault(key);

    public static Def? ByCh(string ch) =>
        All.Values.FirstOrDefault(m => m.Ch == ch);

    /// <summary>Normal monsters: HP grows noticeably, ATK/DEF moderately.</summary>
    public static (int Hp, int Atk, int Df) ScaleStats(int hp, int atk, int df, int depth)
    {
        double hpFactor = 1 + (depth - 1) * 0.10;
        return ((int)(hp * hpFactor), atk + (depth - 1) / 4, df + (depth - 1) / 5);
    }

    /// <summary>Bosses scale softer so the final dragon is reachable.</summary>
    public static (int Hp, int Atk, int Df) ScaleBossStats(int hp, int atk, int df, int depth)
    {
        double hpFactor = 1 + (depth - 1) * 0.06;
        return ((int)(hp * hpFactor), atk + (depth - 1) / 6, df + (depth - 1) / 8);
    }

    public static string[] RollMonsterDrops(string ch)
    {
        var result = new List<string>();
        if (MonsterDrops.TryGetValue(ch, out var table))
            foreach (var (item, chance) in table)
                if (Rng.NextDouble() < chance) result.Add(item);
        return result.ToArray();
    }

    public static string ManaCrystalFor(Entity monster)
    {
        int hp = Math.Max(1, monster.MaxHp);
        if (monster.Boss || hp >= 60) return ManaCrystalBySize["large"];
        if (hp >= 18) return ManaCrystalBySize["normal"];
        return ManaCrystalBySize["small"];
    }

    public static string? RollManaCrystal(Entity monster, double chance = ManaCrystalChance)
    {
        if (Rng.NextDouble() >= chance) return null;
        return ManaCrystalFor(monster);
    }

    public static int ManaCrystalPowder(string? crystalKey) =>
        crystalKey != null ? ManaPowderByCrystal.GetValueOrDefault(crystalKey) : 0;

    public static string? BossKeyForDepth(int depth) =>
        BossByDepth.TryGetValue(depth, out var key) ? key : null;

    /// <summary>
    /// Создать масштабированного монстра/босса. Делегирует фабрике видов
    /// MonsterFactory (каждый вид — отдельный класс со своим Spawn).
    /// </summary>
    public static Entity CreateEntity(string key, int depth)
    {
        if (!All.ContainsKey(key))
            throw new ArgumentException($"Unknown monster: {key}");
        var monster = MonsterFactory.Create(key)
            ?? throw new ArgumentException($"No species class for monster: {key}");
        var list = new List<Entity>();
        monster.Spawn(depth, list);
        return monster;
    }
}
