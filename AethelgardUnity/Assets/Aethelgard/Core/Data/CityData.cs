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
    /// City merchants and citizens — full parity with Python core/data/city.py.
    /// Данные перенесены из data/city.json в CityDefs.cs.
    /// </summary>
    public static partial class CityData
    {
        public static readonly Dictionary<string, NpcDef> Npcs = CityDefs.Npcs;
        public static readonly Dictionary<string, (int X, int Y)> NpcPositions = CityDefs.NpcPositions;
        public static readonly Dictionary<string, (string Key, int Price)[]> CityShopStock = CityDefs.CityShopStock;
        public static readonly Dictionary<string, string> NpcNames = CityDefs.NpcNames;
        public static readonly Dictionary<string, (string Ch, Color Color)> NpcGlyphs = CityDefs.NpcGlyphs;
        public static readonly Dictionary<string, string> NpcShops = CityDefs.NpcShops;
        public static readonly (int X, int Y) QuestBoardPos = CityDefs.QuestBoardPos;
        public static readonly (int X, int Y) ElderPos = CityDefs.ElderPos;
    
        public class NpcDef
        {
            public string Key = "";
            public string Name = "";
            public string Ch = "?";
            public Color Color = Color.White;
            public bool IsGuard;
            public bool IsShop;
    
            /// <summary>Позиция NPC на карте города.</summary>
            public (int X, int Y) Pos =>
                NpcPositions.TryGetValue(Key, out var pos) ? pos : (0, 0);
    
            /// <summary>Список товаров лавки этого NPC.</summary>
            public List<RecipeData.ShopEntry> ShopStock => MakeCityShopStock(Key);
        }
    
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
}
