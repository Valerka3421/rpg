using System;
using System.Collections.Generic;
using System.Linq;

namespace Aethelgard.Core;

/// <summary>
/// Dungeon floor generator: rooms connected by corridors.
/// Ported from Python core/world.py:generate_dungeon.
///
/// Algorithm:
/// 1. Place random rooms (non-overlapping)
/// 2. Connect rooms with L-corridors
/// 3. Place stairs down (deepest room) and up (entry)
/// 4. Spawn monsters scaled by depth
/// 5. Place chests with loot
/// 6. On boss floors (5, 10, 15, 20): add boss arena + locked door
/// </summary>
public static class DungeonGen
{
    private static readonly Random Rng = new();

    public class Room
    {
        public int X, Y, W, H;
        public int CX => X + W / 2;
        public int CY => Y + H / 2;

        public Room(int x, int y, int w, int h)
        {
            X = x; Y = y; W = w; H = h;
        }

        public bool Intersects(Room other, int padding = 1)
        {
            return X - padding < other.X + other.W
                && X + W + padding > other.X
                && Y - padding < other.Y + other.H
                && Y + H + padding > other.Y;
        }

        /// <summary>
        /// Check if a point is inside this room.
        /// Used for boss arena containment check.
        /// </summary>
        public bool Contains(int x, int y)
        {
            return x >= X && x < X + W && y >= Y && y < Y + H;
        }
    }

    /// <summary>
    /// Generate a dungeon floor.
    /// </summary>
    /// <param name="depth">Floor depth (1-20). Higher = more monsters, better loot.</param>
    public static Dungeon Generate(int depth)
    {
        // Use full 140×70 map (was 60×40 — right half was empty)
        int w = Config.MapW, h = Config.MapH;
        var d = new Dungeon(w, h);

        // 1. Fill with walls
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                d.Tiles[y][x] = Tile.Wall;

        // 2. Place rooms: 14-24, wider (up to 12×9)
        var rooms = new List<Room>();
        int maxRooms = Rng.Next(14, 25);
        int attempts = 0;

        while (rooms.Count < maxRooms && attempts < 900)
        {
            attempts++;
            int rw = Rng.Next(5, 13);
            int rh = Rng.Next(4, 10);
            int rx = Rng.Next(4, w - rw - 4);
            int ry = Rng.Next(4, h - rh - 4);

            var room = new Room(rx, ry, rw, rh);
            if (rooms.Any(r => r.Intersects(room)))
                continue;

            rooms.Add(room);

            // Carve room floor
            for (int y = ry; y < ry + rh; y++)
                for (int x = rx; x < rx + rw; x++)
                    d.Tiles[y][x] = Tile.Floor;

            // Connect with previous room using 2-cell-wide L-corridor
            if (rooms.Count > 1)
                CarveCorridor2Wide(d, rooms[^2], rooms[^1]);
        }

        // Store rooms for boss arena logic
        d.Rooms = rooms.Select(r => (r.X, r.Y, r.W, r.H)).ToList();

        // (Corridors already carved during room placement via CarveCorridor2Wide)

        // 4. Find entry and deepest room for stairs
        var entryRoom = rooms[0];
        var deepestRoom = rooms[^1];

        // Stairs down (to next floor)
        d.Tiles[deepestRoom.CY][deepestRoom.CX] = Tile.Stairs;
        d.StairsPos = (deepestRoom.CX, deepestRoom.CY);

        // Stairs up (to previous floor) — at entry
        if (depth > 1)
        {
            d.Tiles[entryRoom.CY][entryRoom.CX] = Tile.UpStairs;
        }
        else
        {
            // Floor 1: exit to overworld
            d.Tiles[entryRoom.CY][entryRoom.CX] = Tile.Exit;
        }

        // 5. Boss floor: add boss arena + locked door
        if (MonsterData.BossByDepth.ContainsKey(depth))
        {
            PlaceBossArena(d, deepestRoom, depth);
        }

        // 6. Spawn monsters
        SpawnDungeonMonsters(d, depth, rooms);

        // 7. Place chests
        PlaceChests(d, depth, rooms);

        return d;
    }

    /// <summary>
    /// Carve a 2-cell-wide L-corridor between two rooms.
    /// Ported from Python _connect_l_corridor (2-wide version).
    /// </summary>
    private static void CarveCorridor2Wide(Dungeon d, Room a, Room b)
    {
        int ax = a.CX, ay = a.CY;
        int bx = b.CX, by = b.CY;
        bool hFirst = Rng.Next(2) == 0;

        if (hFirst)
        {
            CarveH2(d, ax, bx, ay);
            CarveV2(d, ay, by, bx);
        }
        else
        {
            CarveV2(d, ay, by, ax);
            CarveH2(d, ax, bx, by);
        }
    }

    private static void CarveH2(Dungeon d, int x0, int x1, int y)
    {
        int xmin = Math.Min(x0, x1), xmax = Math.Max(x0, x1);
        for (int x = xmin; x <= xmax; x++)
        {
            for (int dy = 0; dy <= 1; dy++)
            {
                int yy = y + dy;
                if (x >= 0 && x < d.W && yy >= 0 && yy < d.H)
                    d.Tiles[yy][x] = Tile.Floor;
            }
        }
    }

    private static void CarveV2(Dungeon d, int y0, int y1, int x)
    {
        int ymin = Math.Min(y0, y1), ymax = Math.Max(y0, y1);
        for (int y = ymin; y <= ymax; y++)
        {
            for (int dx = 0; dx <= 1; dx++)
            {
                int xx = x + dx;
                if (xx >= 0 && xx < d.W && y >= 0 && y < d.H)
                    d.Tiles[y][xx] = Tile.Floor;
            }
        }
    }

    private static void CarveCorridor(Dungeon d, int x0, int y0, int x1, int y1)
    {
        // L-shaped corridor: random order of horizontal/vertical
        bool hFirst = Rng.Next(2) == 0;

        if (hFirst)
        {
            CarveH(d, x0, x1, y0);
            CarveV(d, y0, y1, x1);
        }
        else
        {
            CarveV(d, y0, y1, x0);
            CarveH(d, x0, x1, y1);
        }
    }

    private static void CarveH(Dungeon d, int x0, int x1, int y)
    {
        int xmin = Math.Min(x0, x1), xmax = Math.Max(x0, x1);
        for (int x = xmin; x <= xmax; x++)
        {
            if (x >= 0 && x < d.W && y >= 0 && y < d.H)
                d.Tiles[y][x] = Tile.Floor;
        }
    }

    private static void CarveV(Dungeon d, int y0, int y1, int x)
    {
        int ymin = Math.Min(y0, y1), ymax = Math.Max(y0, y1);
        for (int y = ymin; y <= ymax; y++)
        {
            if (x >= 0 && x < d.W && y >= 0 && y < d.H)
                d.Tiles[y][x] = Tile.Floor;
        }
    }

    private static void PlaceBossArena(Dungeon d, Room room, int depth)
    {
        // Expand the deepest room into an arena
        int ax = Math.Max(0, room.X - 3);
        int ay = Math.Max(0, room.Y - 3);
        int aw = Math.Min(d.W, room.X + room.W + 3) - ax;
        int ah = Math.Min(d.H, room.Y + room.H + 3) - ay;

        for (int y = ay; y < ay + ah; y++)
            for (int x = ax; x < ax + aw; x++)
                if (d.Tiles[y][x] == Tile.Wall)
                    d.Tiles[y][x] = Tile.Floor;

        // Place locked door at arena entrance
        int doorX = room.X + room.W / 2;
        int doorY = Math.Max(0, room.Y - 3);
        if (doorY >= 0 && doorX >= 0 && doorX < d.W)
            d.Tiles[doorY][doorX] = Tile.LockedDoor;

        // Spawn boss
        string bossKey = MonsterData.BossByDepth[depth];
        var boss = MonsterData.CreateEntity(bossKey, depth);
        boss.X = room.CX;
        boss.Y = room.CY;
        d.Monsters.Add(boss);
    }

    private static void SpawnDungeonMonsters(Dungeon d, int depth, List<Room> rooms)
    {
        var pool = MonsterData.PoolForDepth(depth);
        int monsterCount = Math.Min(15, 4 + depth);

        var floorCells = new List<(int, int)>();
        foreach (var room in rooms.Skip(1)) // skip entry room
        {
            for (int y = room.Y; y < room.Y + room.H; y++)
                for (int x = room.X; x < room.X + room.W; x++)
                    if (d.Tiles[y][x] == Tile.Floor && d.MonsterAt(x, y) == null)
                        floorCells.Add((x, y));
        }

        // Shuffle and pick
        for (int i = floorCells.Count - 1; i > 0; i--)
        {
            int j = Rng.Next(i + 1);
            (floorCells[i], floorCells[j]) = (floorCells[j], floorCells[i]);
        }

        int spawned = 0;
        foreach (var (x, y) in floorCells)
        {
            if (spawned >= monsterCount) break;
            string key = pool[Rng.Next(pool.Length)];
            var m = MonsterData.CreateEntity(key, depth);
            m.X = x; m.Y = y;
            d.Monsters.Add(m);
            spawned++;
        }

        Rules.InitActionTimers(d.Monsters);
    }

    private static void PlaceChests(Dungeon d, int depth, List<Room> rooms)
    {
        int chestCount = Math.Min(3, 1 + depth / 5);
        var lootTable = ItemData.ChestLoot(depth);

        var floorCells = new List<(int, int)>();
        foreach (var room in rooms.Skip(1))
        {
            for (int y = room.Y; y < room.Y + room.H; y++)
                for (int x = room.X; x < room.X + room.W; x++)
                    if (d.Tiles[y][x] == Tile.Floor
                        && d.MonsterAt(x, y) == null
                        && d.Tiles[y][x] != Tile.Stairs
                        && d.Tiles[y][x] != Tile.Chest)
                        floorCells.Add((x, y));
        }

        for (int i = floorCells.Count - 1; i > 0; i--)
        {
            int j = Rng.Next(i + 1);
            (floorCells[i], floorCells[j]) = (floorCells[j], floorCells[i]);
        }

        for (int i = 0; i < Math.Min(chestCount, floorCells.Count); i++)
        {
            var (x, y) = floorCells[i];
            d.Tiles[y][x] = Tile.Chest;
            // Pre-generate loot
            string lootKey = lootTable[Rng.Next(lootTable.Length)];
            d.Items[(x, y)] = lootKey;
        }
    }

    /// <summary>
    /// Roll loot drops when a monster is killed.
    /// </summary>
    public static List<string> RollDrops(string monsterKey, int depth)
    {
        var drops = new List<string>();
        if (!ItemData.DropTable.TryGetValue(monsterKey, out var table))
            return drops;

        foreach (var (item, chance) in table)
        {
            if (Rng.NextDouble() < chance)
                drops.Add(item);
        }

        // Boss drops: always a boss key
        if (MonsterData.All.TryGetValue(monsterKey, out var def) && def.Boss)
        {
            drops.Add("boss_key");
            // Boss also drops gold ore
            drops.Add("gold_ore");
            drops.Add("gold_ore");
        }

        return drops;
    }
}
