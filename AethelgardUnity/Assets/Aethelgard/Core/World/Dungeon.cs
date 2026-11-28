using Microsoft.Xna.Framework;
    using Aethelgard.Data;
    
    namespace Aethelgard.World
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
    /// Карта уровня: сетка тайлов + сущности + предметы. Перенесена из
    /// Entities.cs в отдельный файл слоя World. Комнаты — единый тип
    /// Structs.Room (в оригинале Room объявлялся дважды).
    /// </summary>
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
        /// <summary>Внутренний флаг: карта новая/заменена — нужно переоткрыть туман вокруг игрока.</summary>
        public bool _revealDirty = true;
        public int Depth = 1;
    
        /// <summary>Сток торговца на этаже (MERCHANT_FLOORS), генерируется по MakeShopStock.</summary>
        public List<RecipeData.ShopEntry>? MerchantStock;
    
        /// <summary>Имя локации для HUD (village/pine/...), null = подземелье/оверворлд.</summary>
        public string? LocationKey;
    
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
    
        /// <summary>
        /// Открыть «туман войны» в круге заданного радиуса вокруг точки.
        /// Вызывается при спавне игрока и после каждого его шага, поэтому
        /// мини-карта и большая карта всегда показывают ровно то, что игрок видел.
        /// </summary>
        public void RevealAround(int cx, int cy, int radius)
        {
            // Туман войны отключён (Config.FogOfWarEnabled = false): радиус
            // больше карты → просто отмечаем всю карту исследованной.
            if (radius >= W && radius >= H)
            {
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                        Explored.Add((x, y));
                return;
            }
            int r2 = radius * radius;
            int x0 = Math.Max(0, cx - radius), x1 = Math.Min(W - 1, cx + radius);
            int y0 = Math.Max(0, cy - radius), y1 = Math.Min(H - 1, cy + radius);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r2)
                        Explored.Add((x, y));
        }
    
        public bool IsExplored(int x, int y) => Explored.Contains((x, y));
    }
}
