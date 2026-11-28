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
    /// Item definitions and helpers — full parity with Python core/data/items.py.
    /// Данные перенесены из data/items.json в ItemDefs.cs (128 предметов).
    /// </summary>
    public static partial class ItemData
    {
        public static readonly Dictionary<string, Def> All = ItemDefs.Build();
    
        /// <summary>Цена предмета в магазине.</summary>
    
        public enum ItemType
        {
            Weapon, Armor, Helmet, Leggings, Boots,
            Heal, Mana, Buff, Poison, Food,
            Scroll, Rune, Resource, Material, Book,
            Key, Blueprint, Gold,
        }
    
        public class RecipeVariant
        {
            public Dictionary<string, int> Materials = new();
            public int Count = 1;
        }
    
        public class Def
        {
            public string Key = "";
            public string Ch = "?";
            public string Name = "";
            public string Type = "";
            public ItemType Kind;
            public double Value;
            public Color Color = Color.White;
            public int Tier = 1;
            public int Sell;
            public int ShopPrice;
    
            public int Price => ShopPrice;
            public string? Category;
            public int LevelReq = 1;
            public Dictionary<string, int>? Recipe;
            public List<RecipeVariant>? Recipes;
            public string? Skill;
            public int SkillReq = 1;
            public string? Desc;
            public string? WType;
            public string? ArmorType;
            public double CritBonus, DodgeBonus;
            public int DfBonus, HpBonus, ManaBonus;
            public int Heal, Mana;
            public string? Status;
            public string? Effect;
            public int ManaCost;
            public string? Stat;
            public string? Teaches;
            public bool Special;
            public string[] CraftClasses = Array.Empty<string>();
            public string? BlueprintRequired;
    
            public bool HasRecipe => Recipe != null || (Recipes != null && Recipes.Count > 0);
            public int IntValue => (int)Math.Round(Value);
        }
    
        // ── Rarity ──
        public static readonly (int Level, double Chance)[] RarityChances =
        {
            (0, 0.85), (1, 0.10), (2, 0.04), (3, 0.01),
        };
    
        public static readonly Dictionary<int, Color?> RarityColors = new()
        {
            [0] = null,
            [1] = new Color(170, 220, 170),
            [2] = new Color(140, 190, 255),
            [3] = new Color(255, 180, 80),
        };
    
        private static readonly Random Rng = new();
    
        public static ItemType KindOf(string type) => type switch
        {
            "weapon" => ItemType.Weapon,
            "armor" => ItemType.Armor,
            "head" => ItemType.Helmet,
            "legs" => ItemType.Leggings,
            "boots" => ItemType.Boots,
            "heal" => ItemType.Heal,
            "mana" => ItemType.Mana,
            "buff" => ItemType.Buff,
            "poison" => ItemType.Poison,
            "food" => ItemType.Food,
            "scroll" => ItemType.Scroll,
            "rune" => ItemType.Rune,
            "resource" => ItemType.Resource,
            "material" => ItemType.Material,
            "book" => ItemType.Book,
            "key" => ItemType.Key,
            "blueprint" => ItemType.Blueprint,
            "gold" => ItemType.Gold,
            _ => ItemType.Material,
        };
    
        public static Def? Get(string? key) => string.IsNullOrEmpty(key) ? null : All.GetValueOrDefault(key);
    
        public static (string? Base, int Rarity) ParseItemKey(string? fullKey)
        {
            if (string.IsNullOrEmpty(fullKey)) return (null, 0);
            // Virtual stack suffix "#N" is not part of the item name.
            int hash = fullKey.IndexOf('#');
            if (hash >= 0) fullKey = fullKey[..hash];
            int plus = fullKey.LastIndexOf('+');
            if (plus >= 0)
            {
                var baseKey = fullKey[..plus];
                var lvlPart = fullKey[(plus + 1)..];
                if (int.TryParse(lvlPart, out int lvl)) return (baseKey, lvl);
                return (fullKey, 0);
            }
            return (fullKey, 0);
        }
    
        public static string MakeLootKey(string baseKey, int rarity) =>
            rarity <= 0 ? baseKey : $"{baseKey}+{rarity}";
    
        public static int RollRarity()
        {
            double r = Rng.NextDouble();
            double c = 0;
            foreach (var (level, chance) in RarityChances)
            {
                c += chance;
                if (r < c) return level;
            }
            return 0;
        }
    
        public static Def GetItemData(string? fullKey)
        {
            var (baseKey, _) = ParseItemKey(fullKey);
            return Get(baseKey) ?? new Def { Key = baseKey ?? "", Name = baseKey ?? "?" };
        }
    
        public static string DisplayName(string? fullKey)
        {
            var (baseKey, lvl) = ParseItemKey(fullKey);
            var def = Get(baseKey);
            string name = def?.Name ?? baseKey ?? "?";
            return lvl > 0 ? $"{name} +{lvl}" : name;
        }
    
        public static Color DisplayColor(string? fullKey)
        {
            var (baseKey, lvl) = ParseItemKey(fullKey);
            if (RarityColors.TryGetValue(lvl, out var c) && c.HasValue) return c.Value;
            return Get(baseKey)?.Color ?? new Color(200, 200, 200);
        }
    
        public static int SellPrice(string? fullKey)
        {
            var (baseKey, lvl) = ParseItemKey(fullKey);
            return (Get(baseKey)?.Sell ?? 0) + lvl * 10;
        }
    
        /// <summary>Python item_category — craft tab / skill category.</summary>
        public static string CategoryOf(string? itemKey)
        {
            var def = Get(itemKey);
            if (def == null) return "blacksmith";
            if (def.Category != null && RecipeData.Skills.ContainsKey(def.Category))
                return def.Category;
            return def.Type switch
            {
                "weapon" or "armor" or "head" or "legs" or "boots" or "material" => "blacksmith",
                "heal" or "mana" or "poison" or "buff" => "alchemy",
                "scroll" or "rune" or "blueprint" => "magic_engineering",
                _ => def.Skill != null && RecipeData.Skills.ContainsKey(def.Skill) ? def.Skill : "blacksmith",
            };
        }
    
        /// <summary>All recipe variants for an item: [(materials, count)].</summary>
        public static List<RecipeVariant> RecipesOf(string? itemKey)
        {
            var def = Get(itemKey);
            var result = new List<RecipeVariant>();
            if (def == null) return result;
            if (def.Recipes != null)
            {
                foreach (var v in def.Recipes)
                {
                    if (v.Materials.Count > 0)
                        result.Add(new RecipeVariant
                        {
                            Materials = new Dictionary<string, int>(v.Materials),
                            Count = Math.Max(1, v.Count),
                        });
                }
            }
            if (result.Count == 0 && def.Recipe != null && def.Recipe.Count > 0)
                result.Add(new RecipeVariant { Materials = new Dictionary<string, int>(def.Recipe), Count = 1 });
            return result;
        }
    
        /// <summary>(materials, count, index) — first affordable variant when index is null.</summary>
        public static (Dictionary<string, int>? Mats, int Count, int Index) SelectRecipe(
            string? itemKey, IReadOnlyDictionary<string, int> inventory, int? recipeIndex = null)
        {
            var variants = RecipesOf(itemKey);
            if (variants.Count == 0) return (null, 0, -1);
            if (recipeIndex.HasValue && recipeIndex.Value >= 0 && recipeIndex.Value < variants.Count)
            {
                var v = variants[recipeIndex.Value];
                return (v.Materials, v.Count, recipeIndex.Value);
            }
            for (int i = 0; i < variants.Count; i++)
            {
                bool ok = true;
                foreach (var (mat, cnt) in variants[i].Materials)
                    if (inventory.GetValueOrDefault(mat) < cnt) { ok = false; break; }
                if (ok) return (variants[i].Materials, variants[i].Count, i);
            }
            return (variants[0].Materials, variants[0].Count, 0);
        }
    
        public static bool IsEquipment(Def def) =>
            def.Kind is ItemType.Weapon or ItemType.Armor or ItemType.Helmet or ItemType.Leggings or ItemType.Boots;
    
        public static bool IsStackable(Def def) => !IsEquipment(def);
    
        private static readonly Random LootRng = new();
    
        /// <summary>
        /// Пул возможного лута сундуков на глубине depth. В оригинале
        /// DungeonGen вызывал несуществующий ItemData.ChestLoot(depth).
        /// </summary>
        public static string[] ChestLoot(int depth)
        {
            var consumables = All.Values
                .Where(d => d.Kind is ItemType.Heal or ItemType.Mana or ItemType.Food
                         or ItemType.Poison or ItemType.Buff or ItemType.Scroll
                         or ItemType.Rune or ItemType.Material or ItemType.Resource)
                .Where(d => d.LevelReq <= depth + 3 && d.Tier <= 1 + depth / 4)
                .Select(d => d.Key)
                .ToList();
            if (consumables.Count == 0) consumables.Add("potion_s");
    
            var loot = new List<string>();
            for (int i = 0; i < 8; i++)
                loot.Add(consumables[LootRng.Next(consumables.Count)]);
    
            // На глубоких этажах в сундуках иногда встречается снаряжение
            if (depth >= 3)
            {
                var gear = All.Values
                    .Where(d => IsEquipment(d) && d.Tier <= 1 + depth / 5 && d.LevelReq <= depth + 2)
                    .Select(d => d.Key)
                    .ToList();
                if (gear.Count > 0)
                    loot[0] = gear[LootRng.Next(gear.Count)];
            }
            return loot.ToArray();
        }
    
        private static Dictionary<string, List<(string Item, double Chance)>>? _dropTable;
    
        /// <summary>
        /// Таблица выпадения предметов по ключу монстра. В оригинале
        /// DungeonGen.RollDrops обращался к несуществующему ItemData.DropTable.
        /// Строится из таблицы MonsterDrops (ключ — глиф монстра).
        /// </summary>
        public static Dictionary<string, List<(string Item, double Chance)>> DropTable
        {
            get
            {
                if (_dropTable != null) return _dropTable;
                var table = new Dictionary<string, List<(string, double)>>();
                foreach (var def in MonsterData.All.Values)
                {
                    if (!MonsterData.MonsterDrops.TryGetValue(def.Ch, out var drops))
                        continue;
                    table[def.Key] = drops.ToList();
                }
                _dropTable = table;
                return _dropTable;
            }
        }
    }
}
