using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Aethelgard.Core;

/// <summary>
/// City merchants and citizens — full parity with Python core/data/city.py.
/// The raw tables live in CityTable.g.cs.
/// </summary>
public static partial class CityData
{
    public class NpcDef
    {
        public string Key = "";
        public string Name = "";
        public string Ch = "?";
        public Color Color = Color.White;
        public bool IsGuard;
        public bool IsShop;
    }

    public static readonly Dictionary<string, NpcDef> Npcs = new()
    {
        ["guard"] = new() { Key = "guard", Name = "Стражник", Ch = "G", Color = new(196, 200, 220), IsGuard = true },
        ["blacksmith"] = new() { Key = "blacksmith", Name = "Кузнец", Ch = "K", Color = new(206, 152, 88), IsShop = true },
        ["alchemist"] = new() { Key = "alchemist", Name = "Алхимик", Ch = "A", Color = new(156, 126, 224), IsShop = true },
        ["innkeeper"] = new() { Key = "innkeeper", Name = "Трактирщик", Ch = "I", Color = new(214, 172, 112), IsShop = true },
    };

    // Positions used by the city map (see Core/Locations.cs).
    public static readonly Dictionary<string, (int X, int Y)> NpcPositions = new()
    {
        ["guard"] = (50, 44),
        ["blacksmith"] = (40, 32),
        ["alchemist"] = (60, 32),
        ["innkeeper"] = (48, 31),
    };

    public static readonly (int X, int Y) QuestBoardPos = (48, 37);
    public static readonly (int X, int Y) ElderPos = (52, 35);

    public static List<RecipeData.ShopEntry> MakeCityShopStock(string npcType)
    {
        var stock = new List<RecipeData.ShopEntry>();
        if (!CityShopStock.TryGetValue(npcType, out var rows)) return stock;
        foreach (var (key, price) in rows)
            stock.Add(new RecipeData.ShopEntry { Key = key, Price = price, Sold = false, Infinite = true });
        return stock;
    }

    public static bool IsCityShopNpc(string? npcType) =>
        npcType != null && CityShopStock.ContainsKey(npcType);

    public static NpcDef? NpcAt(int x, int y)
    {
        foreach (var (key, pos) in NpcPositions)
            if (pos == (x, y)) return Npcs.GetValueOrDefault(key);
        return null;
    }

    public static NpcDef? NpcNear(int x, int y)
    {
        foreach (var (key, pos) in NpcPositions)
            if (Math.Max(Math.Abs(pos.X - x), Math.Abs(pos.Y - y)) <= 1)
                return Npcs.GetValueOrDefault(key);
        return null;
    }
}
