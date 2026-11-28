using System;
    using System.Collections.Generic;
    using Microsoft.Xna.Framework;
    
    namespace Aethelgard.Data
    {
    
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Aethelgard.BaseClass;
    using Aethelgard.Entities;
    using Aethelgard.Entities.Monsters;
    using Aethelgard.Structs;
    using Aethelgard.World;
    using Aethelgard.Systems;
    using Aethelgard.Data;
    using Aethelgard.Net;
    using Aethelgard.Audio;
    /// <summary>
    /// Character classes and their evolutions — full parity with
    /// Python core/data/classes.py. Данные перенесены из data/classes.json
    /// в ClassDefs.cs (42 класса).
    /// </summary>
    public static partial class ClassData
    {
        public static readonly Dictionary<string, Def> All = ClassDefs.All;
        public static readonly string[] Order = ClassDefs.Order;
        public static readonly Dictionary<string, string> WeaponNames = ClassDefs.WeaponNames;
        public static readonly Dictionary<string, string> ArmorNames = ClassDefs.ArmorNames;
    
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
    
            // Бонусы характеристик класса (использовались GameController, Net/GameCore
            // и ClassSelectScreen, но в оригинале не были объявлены).
            public int StrBonus => Bonus.GetValueOrDefault("str");
            public int AgiBonus => Bonus.GetValueOrDefault("agi");
            public int VitBonus => Bonus.GetValueOrDefault("vit");
            public int LukBonus => Bonus.GetValueOrDefault("luk");
            public int IntBonus => Bonus.GetValueOrDefault("int");
            public int DfStatBonus => Bonus.GetValueOrDefault("df_stat");
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
    
        /// <summary>
        /// Доступные повышения класса. В оригинале вызывалось GameController'ом,
        /// но метод не существовал. Первое повышение — на 5 уровне, второе — на 15.
        /// </summary>
        public static string[] AvailablePromotions(Player player)
        {
            var def = Data(player.ClassKey);
            if (def.Next.Length == 0) return Array.Empty<string>();
            int idx = Math.Min(player.ClassChanges, ClassChangeLevels.Length - 1);
            if (player.Level < ClassChangeLevels[idx]) return Array.Empty<string>();
            return def.Next;
        }
    
        /// <summary>Повысить класс игрока до newClassKey (если доступно).
        /// Порт core/skills.py:change_class — бонусы статов цели, снятие узлов
        /// прежней ветки с возвратом очков, +2 очка навыка за повышение.</summary>
        public static bool Promote(Player player, string newClassKey)
        {
            if (!AvailablePromotions(player).Contains(newClassKey)) return false;
            var def = Get(newClassKey);
            if (def == null) return false;
    
            // Бонусы статов цели (target['bonus'])
            foreach (var (stat, value) in def.Bonus)
            {
                switch (stat)
                {
                    case "str": player.Str += value; break;
                    case "agi": player.Agi += value; break;
                    case "vit": player.Vit += value; break;
                    case "luk": player.Luk += value; break;
                    case "df_stat": player.DfStat += value; break;
                    case "int": player.IntStat += value; break;
                }
            }
    
            // release_class_nodes: узлы прежней ветки снимаются, очки возвращаются
            int refunded = ReleaseClassNodes(player, newClassKey);
    
            player.ClassKey = def.Key;
            player.ClassName = def.Name;
            player.ClassTier = def.Tier;
            player.ClassBase = string.IsNullOrEmpty(def.Base) ? def.Key : def.Base;
            player.ClassChanges = Math.Max(0, def.Tier - 1);
            player.SkillPoints += 2 + refunded;   // +2 за повышение + рефанд
            player.Recalc();
            return true;
        }
    
        /// <summary>Снять узлы прежних классовых веток. Возвращает число
        /// возвращённых очков (порт release_class_nodes).</summary>
        public static int ReleaseClassNodes(Player player, string keepClassKey)
        {
            var keepBase = BaseKeyOf(keepClassKey);
            int refund = 0;
            var kept = new List<string>();
            foreach (var key in player.LearnedNodes)
            {
                if (BaseKeyOf(key) != null && BaseKeyOf(key) != keepBase)
                    refund++;
                else
                    kept.Add(key);
            }
            if (refund > 0)
            {
                player.LearnedNodes.Clear();
                foreach (var k in kept) player.LearnedNodes.Add(k);
            }
            return refund;
        }
    
        /// <summary>Префикс ветки узла/класса → базовый класс (w_/m_/r_/a_/d_).</summary>
        private static string? BaseKeyOf(string keyOrClass)
        {
            if (keyOrClass.Length >= 2 && keyOrClass[1] == '_' && "wmrad".Contains(keyOrClass[0]))
                return keyOrClass[..1] switch
                {
                    "w" => "warrior", "m" => "mage", "r" => "rogue",
                    "a" => "artisan", "d" => "default", _ => null,
                };
            // классовый ключ (warrior, knight, …) → его базовый класс
            var def = Get(keyOrClass);
            while (def != null && def.Tier > 1)
                def = Get(def.Base);
            return def?.Key;
        }
    }
}
