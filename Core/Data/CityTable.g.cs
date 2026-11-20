using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Aethelgard.Core;

public static partial class CityData
{
    public static readonly Dictionary<string, (string Key, int Price)[]> CityShopStock = new()
    {
        ["blacksmith"] = new[] { ("iron_ingot", 12), ("steel_ingot", 22), ("mithril_ingot", 40), ("ore", 10), ("wood", 4), ("stone", 3) },
        ["alchemist"] = new[] { ("potion_s", 8), ("potion_m", 16), ("potion_l", 30), ("mana_s", 10), ("mana_m", 22), ("magic_powder", 18) },
        ["innkeeper"] = new[] { ("fried_mushrooms", 6), ("berry_compote", 8), ("roast_venison", 12), ("roast_boar", 16), ("roast_bear", 22) },
    };

    public static readonly Dictionary<string, string> NpcNames = new() { ["guard"] = "Стражник", ["blacksmith"] = "Кузнец", ["alchemist"] = "Алхимик", ["innkeeper"] = "Трактирщик" };
    public static readonly Dictionary<string, (string Ch, Color Color)> NpcGlyphs = new() { ["guard"] = ("G", new Color(196, 200, 220)), ["blacksmith"] = ("K", new Color(206, 152, 88)), ["alchemist"] = ("A", new Color(156, 126, 224)), ["innkeeper"] = ("I", new Color(214, 172, 112)) };
    public static readonly Dictionary<string, string> NpcShops = new() { ["blacksmith"] = "Кузнец", ["alchemist"] = "Алхимик", ["innkeeper"] = "Трактирщик" };
}
