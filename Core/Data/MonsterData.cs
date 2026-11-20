using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Aethelgard.Core;

/// <summary>
/// Monsters, bosses and stat scaling — full parity with
/// Python core/data/monsters.py. The raw table lives in MonsterTable.g.cs.
/// </summary>
public static partial class MonsterData
{
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

    private static readonly Random Rng = new();

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

    /// <summary>Create a scaled monster/boss entity.</summary>
    public static Entity CreateEntity(string key, int depth)
    {
        if (!All.TryGetValue(key, out var def))
            throw new ArgumentException($"Unknown monster: {key}");
        int hp, atk, df;
        if (def.Boss)
            (hp, atk, df) = ScaleBossStats(def.Hp, def.Atk, def.Df, depth);
        else
            (hp, atk, df) = ScaleStats(def.Hp, def.Atk, def.Df, depth);
        var e = new Entity(0, 0, hp, hp, atk, df, def.Name, def.Ch[0], def.Color, def.Xp,
                           boss: def.Boss, ability: def.Ability)
        {
            MonsterKey = def.Key,
        };
        return e;
    }
}
