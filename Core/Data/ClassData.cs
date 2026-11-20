using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Aethelgard.Core;

/// <summary>
/// Character classes and their evolutions — full parity with
/// Python core/data/classes.py. The raw table lives in ClassTable.g.cs.
/// </summary>
public static partial class ClassData
{
    public class ActiveSkill
    {
        public string Key = "";
        public string Name = "";
        public string Kind = "";
        public int Mana;
        public double Cooldown;
        public int Damage;
        public string? Desc;
    }

    public class Def
    {
        public string Key = "";
        public string Name = "";
        public string Desc = "";
        public string Base = "";
        public int Tier = 1;
        public string[] Next = Array.Empty<string>();
        public Dictionary<string, int> Bonus = new();
        public Dictionary<string, int>? Stats;
        public string[] Weapons = Array.Empty<string>();
        public string[] ArmorTypes = Array.Empty<string>();
        public Dictionary<string, double> Effects = new();
        public Color Color = Color.White;
        public string? StartWeapon;
        public string? StartArmor;
        public Dictionary<string, int> StartItems = new();
        public string Perk = "";
        public List<ActiveSkill> ActiveSkills = new();
    }

    public static readonly int[] ClassChangeLevels = { 5, 15 };

    public static Def? Get(string? key) =>
        string.IsNullOrEmpty(key) ? null : All.GetValueOrDefault(key);

    public static Def Data(string? key) =>
        Get(key) ?? All.GetValueOrDefault("default") ?? new Def { Key = "default", Name = "Adventurer" };

    /// <summary>Full class stats for the class-select preview.</summary>
    public static Dictionary<string, int> ClassStats(string classKey)
    {
        var cls = Data(classKey);
        if (cls.Stats != null)
        {
            var stats = new Dictionary<string, int>(cls.Stats);
            var baseCls = All.GetValueOrDefault(cls.Base) ?? Data("default");
            if (baseCls.Stats != null)
                foreach (var (k, v) in baseCls.Stats)
                    if (!stats.ContainsKey(k)) stats[k] = v;
            if (!stats.ContainsKey("int")) stats["int"] = 5;
            return stats;
        }
        var baseStats = new Dictionary<string, int>
        {
            ["str"] = 5, ["agi"] = 5, ["vit"] = 5, ["luk"] = 5, ["df_stat"] = 5, ["int"] = 5,
        };
        var bc = All.GetValueOrDefault(cls.Base);
        if (bc?.Stats != null)
            foreach (var (k, v) in bc.Stats) baseStats[k] = v;
        foreach (var (k, v) in cls.Bonus)
            baseStats[k] = baseStats.GetValueOrDefault(k) + v;
        return baseStats;
    }

    /// <summary>Can the class wear the item? Returns (ok, reason).</summary>
    public static (bool Ok, string Reason) CanUse(string classKey, string? fullKey)
    {
        var cls = Data(classKey);
        var (baseKey, rarity) = ItemData.ParseItemKey(fullKey);
        var item = ItemData.Get(baseKey);
        if (item == null) return (true, "");
        // class_req is not present in the current data table but is supported.
        var wtype = item.WType;
        if (!string.IsNullOrEmpty(wtype) && Array.IndexOf(cls.Weapons, wtype) < 0)
        {
            if (cls.Effects.GetValueOrDefault("plain_weapons") > 0 && rarity == 0)
                return (true, "");
            return (false, $"оружие: {WeaponNames.GetValueOrDefault(wtype, wtype)}");
        }
        var atype = item.ArmorType;
        if (!string.IsNullOrEmpty(atype) && Array.IndexOf(cls.ArmorTypes, atype) < 0)
            return (false, $"броня: {ArmorNames.GetValueOrDefault(atype, atype)}");
        return (true, "");
    }
}
