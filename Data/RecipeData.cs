using System;
using System.Collections.Generic;

namespace Aethelgard.Data;

/// <summary>
/// Craft skills, recipes, shop stock and XP curves — full parity with
/// Python core/data/recipes.py. Данные перенесены из data/recipes.json
/// в RecipeDefs.cs.
/// </summary>
public static partial class RecipeData
{
    public static readonly Dictionary<string, SkillDef> Skills = RecipeDefs.Skills;
    public static readonly string[] SkillOrder = RecipeDefs.SkillOrder;
    public static readonly (string Key, string Name)[] CraftTabs = RecipeDefs.CraftTabs;

    public class SkillDef
    {
        public string Key = "";
        public string Name = "";
        public string Desc = "";
    }

    public class SkillProgress
    {
        public int Level = 1;
        public int Xp;
    }

    public class ShopEntry
    {
        public string Key = "";
        public int Price;
        public bool Sold;
        public bool Infinite;
    }

    /// <summary>XP needed to go from level → level+1 for a craft skill.</summary>
    public static int SkillXpToNext(int level) =>
        Math.Max(10, (int)(45 * Math.Pow(Math.Max(1, level), 1.35)));

    public static Dictionary<string, SkillProgress> DefaultSkills()
    {
        var result = new Dictionary<string, SkillProgress>();
        foreach (var key in SkillOrder)
            result[key] = new SkillProgress();
        return result;
    }

    public static int CraftSkillReward(string itemKey, int count = 1)
    {
        var item = ItemData.Get(itemKey);
        if (item == null) return 1;
        return Math.Max(1, (int)((3 + item.Tier * 2 + item.LevelReq) * Math.Max(1, count)));
    }

    public static List<(string Key, ItemData.Def Item)> GetCraftableItems()
    {
        var result = new List<(string, ItemData.Def)>();
        foreach (var (key, item) in ItemData.All)
            if (item.HasRecipe) result.Add((key, item));
        return result;
    }

    public static int XpToNext(int level) =>
        Math.Max(1, (int)(Config.XpBase * Math.Pow(Math.Max(1, level), Config.XpExponent)));

    public static int RerollPrice(int depth) => 15 + depth * 3;

    public static int BookPriceAtDepth(int basePrice, int depth) =>
        (int)(basePrice * (1 + depth * 0.1));

    /// <summary>Random dungeon-merchant stock for the given depth.
    /// seed задаёт воспроизводимый ассортимент (по умолчанию — от глубины).</summary>
    public static List<ShopEntry> MakeShopStock(int depth, int? seed = null)
    {
        var rng = new Random(seed ?? (Config.WorldSeed + depth * 104729));
        var stock = new List<ShopEntry>();
        void Add(string key, int price) =>
            stock.Add(new ShopEntry { Key = key, Price = price });

        var perks = new (string Key, double Chance, int Price)[]
        {
            ("book_double", 0.05, 120), ("book_regen", 0.10, 80),
            ("book_vampire", 0.08, 120), ("book_thorns", 0.10, 100),
            ("book_greedy", 0.10, 90),
        };
        foreach (var (bk, chance, price) in perks)
            if (rng.NextDouble() < chance) Add(bk, BookPriceAtDepth(price, depth));

        foreach (var bk in new[] { "book_str", "book_agi", "book_vit", "book_luk", "book_def", "book_int" })
            if (rng.NextDouble() < 0.25) Add(bk, BookPriceAtDepth(80, depth));

        if (depth >= 3 && rng.NextDouble() < 0.15)
        {
            var bp = new[] { "blueprint_artisan_blade", "blueprint_philosophers_stone", "blueprint_grand_elixir" };
            Add(bp[rng.Next(bp.Length)], BookPriceAtDepth(180, depth));
        }

        if (rng.NextDouble() < 0.20) Add("scroll_heal", BookPriceAtDepth(60, depth));
        if (rng.NextDouble() < 0.15) Add("scroll_fireball", BookPriceAtDepth(80, depth));
        if (depth >= 5 && rng.NextDouble() < 0.10) Add("scroll_freeze", BookPriceAtDepth(100, depth));

        int tier = Math.Min(3, Math.Max(1, (depth + 2) / 3));
        string potionKey = tier == 1 ? "potion_s" : tier == 2 ? "potion_m" : "potion_l";
        string[] weaponPool = tier == 1 ? new[] { "dagger", "shortsword" }
            : tier == 2 ? new[] { "sword", "axe" } : new[] { "greatsword", "warhammer" };
        string[] armorPool = tier == 1 ? new[] { "padded", "leather" }
            : tier == 2 ? new[] { "chain", "scale" } : new[] { "plate", "mithril" };
        string[] headPool = tier == 1 ? new[] { "leather_cap" }
            : tier == 2 ? new[] { "leather_cap", "iron_helm" } : new[] { "iron_helm", "steel_helm", "crown" };
        string[] legsPool = tier == 1 ? new[] { "cloth_pants" }
            : tier == 2 ? new[] { "leather_legs", "chain_legs", "plate_greaves" } : new[] { "plate_greaves", "plate_legs" };
        string[] bootsPool = tier == 1 ? new[] { "leather_boots" }
            : tier == 2 ? new[] { "leather_boots", "chain_boots" } : new[] { "chain_boots", "plate_boots" };

        if (rng.NextDouble() < 0.45) Add(headPool[rng.Next(headPool.Length)], 10 * tier);
        if (rng.NextDouble() < 0.45) Add(legsPool[rng.Next(legsPool.Length)], 8 * tier);
        if (rng.NextDouble() < 0.40) Add(bootsPool[rng.Next(bootsPool.Length)], 7 * tier);

        Add(potionKey, 5 * tier);
        Add(potionKey, 5 * tier);
        if (tier >= 2) Add(tier >= 3 ? "mana_m" : "mana_s", 4 * tier);

        string wk = weaponPool[rng.Next(weaponPool.Length)];
        string ak = armorPool[rng.Next(armorPool.Length)];
        int wr = ItemData.RollRarity();
        int ar = ItemData.RollRarity();
        Add(ItemData.MakeLootKey(wk, wr), 15 * tier + wr * 10);
        Add(ItemData.MakeLootKey(ak, ar), 12 * tier + ar * 10);
        Add(headPool[0], 8 * tier);
        Add(legsPool[rng.Next(legsPool.Length)], 8 * tier);
        Add(bootsPool[rng.Next(bootsPool.Length)], 6 * tier);
        return stock;
    }
}
