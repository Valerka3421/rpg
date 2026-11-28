using System;
using System.Collections.Generic;
using System.Linq;

namespace Aethelgard.World;

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
    /// <summary>Отдельный генератор для дропа — не влияет на карту.</summary>
    private static readonly Random DropRng = new();

    /// <summary>
    /// Generate a dungeon floor.
    /// </summary>
    /// <param name="depth">Floor depth (1-20). Higher = more monsters, better loot.</param>
    /// <param name="seed">Зерно. null — стабильное зерно от глубины, поэтому
    /// один и тот же этаж воспроизводим.</param>
    public static Dungeon Generate(int depth, int? seed = null)
    {
        var rng = new Random(seed ?? (Config.WorldSeed + depth * 7919));
        // Use full 140×70 map (was 60×40 — right half was empty)
        int w = Config.MapW, h = Config.MapH;
        var d = new Dungeon(w, h);

        // 1. Fill with walls
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                d.Tiles[y][x] = Tile.Wall;

        // 2. Place rooms: 14-24, wider (up to 12×9)
        var rooms = new List<Room>();
        int maxRooms = rng.Next(14, 25);
        int attempts = 0;

        while (rooms.Count < maxRooms && attempts < 900)
        {
            attempts++;
            int rw = rng.Next(5, 13);
            int rh = rng.Next(4, 10);
            int rx = rng.Next(4, w - rw - 4);
            int ry = rng.Next(4, h - rh - 4);

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
                CarveCorridor2Wide(d, rooms[^2], rooms[^1], rng);
        }

        // Store rooms for boss arena logic
        d.Rooms = rooms; // Structs.Room — единый тип комнат

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

        // 5. Boss floor: boss arena + treasury (5×5) behind a locked door.
        // Treasury contains a chest and the stairs down (parity with
        // core/world.py generate_dungeon boss complex).
        if (MonsterData.BossByDepth.ContainsKey(depth))
        {
            PlaceBossComplex(d, deepestRoom, depth, rooms);
        }

        // 5b. Merchant on floors 3/6/9/12/15/18 (MERCHANT_FLOORS)
        if (Config.MerchantFloors.Contains(depth))
        {
            PlaceMerchant(d, rooms[0], depth);
        }

        // 5c. Quest board on boss floors (dmin=depth, dmax=depth+4)
        if (MonsterData.BossByDepth.ContainsKey(depth))
        {
            PlaceQuestBoard(d, entryRoom);
        }

        // 6. Spawn monsters
        SpawnDungeonMonsters(d, depth, rooms, rng);

        // 7. Place chests
        PlaceChests(d, depth, rooms, rng);

        return d;
    }

    /// <summary>
    /// Carve a 2-cell-wide L-corridor between two rooms.
    /// Ported from Python _connect_l_corridor (2-wide version).
    /// </summary>
    private static void CarveCorridor2Wide(Dungeon d, Room a, Room b, Random rng)
    {
        int ax = a.CX, ay = a.CY;
        int bx = b.CX, by = b.CY;
        bool hFirst = rng.Next(2) == 0;

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

    private static void PlaceBossComplex(Dungeon d, Room arenaRoom, int depth,
        List<Room> rooms)
    {
        // Expand the deepest room into an arena
        int ax = Math.Max(0, arenaRoom.X - 3);
        int ay = Math.Max(0, arenaRoom.Y - 3);
        int aw = Math.Min(d.W, arenaRoom.X + arenaRoom.W + 3) - ax;
        int ah = Math.Min(d.H, arenaRoom.Y + arenaRoom.H + 3) - ay;

        for (int y = ay; y < ay + ah; y++)
            for (int x = ax; x < ax + aw; x++)
                if (d.Tiles[y][x] == Tile.Wall)
                    d.Tiles[y][x] = Tile.Floor;

        // Treasury 5×5 to the right of the arena
        int tx = Math.Min(d.W - 7, ax + aw + 2);
        int ty = Math.Max(1, Math.Min(d.H - 7, arenaRoom.Y));
        const int tsz = 5; // TREASURY_SIZE
        for (int y = ty; y < ty + tsz; y++)
            for (int x = tx; x < tx + tsz; x++)
                d.Tiles[y][x] = Tile.Floor;

        // Corridor from arena to treasury + single locked door
        int corridorY = ty + tsz / 2;
        for (int x = ax + aw; x < tx; x++)
        {
            d.Tiles[corridorY][x] = Tile.Floor;
            if (x > ax + aw) d.Tiles[corridorY + 1][x] = Tile.Floor;
        }
        int doorX = tx - 1;
        if (doorX > ax + aw && doorX < d.W)
        {
            d.Tiles[corridorY][doorX] = Tile.LockedDoor;
            d.BossDoorCells.Add((doorX, corridorY));
        }

        // Chest with boss loot inside the treasury
        int chestX = tx + 1, chestY = ty + 1;
        d.Tiles[chestY][chestX] = Tile.Chest;
        d.TreasuryPos = (chestX, chestY);
        var loot = new Dictionary<string, int>();
        foreach (var item in ItemData.ChestLoot(depth).Take(3))
            loot[item] = loot.GetValueOrDefault(item) + 1;
        loot["gold_large"] = loot.GetValueOrDefault("gold_large") + 1;
        d.ChestLoot[(chestX, chestY)] = loot;

        // Stairs down inside the treasury (the way onward is locked)
        int stairsX = tx + 3, stairsY = ty + 3;
        d.Tiles[stairsY][stairsX] = Tile.Stairs;
        d.StairsPos = (stairsX, stairsY);

        // Spawn boss in arena center
        string bossKey = MonsterData.BossByDepth[depth];
        var boss = MonsterData.CreateEntity(bossKey, depth);
        boss.X = arenaRoom.CX;
        boss.Y = arenaRoom.CY;
        d.Monsters.Add(boss);
    }

    private static void PlaceMerchant(Dungeon d, Room room, int depth)
    {
        // Merchant in a corner of the first room
        int mx = room.X + 1, my = room.Y + 1;
        if (d.Tiles[my][mx] != Tile.Floor) { mx = room.CX; my = room.CY; }
        var merchant = new Entity(mx, my, 10, 10, 0, 0, "Merchant", 'M',
            Config.ColMerch, 0)
        { IsNpc = true, IsMerchant = true, NpcType = "merchant" };
        d.Monsters.Add(merchant);
        // Random stock for this floor
        d.MerchantStock = RecipeData.MakeShopStock(depth);
    }

    private static void PlaceQuestBoard(Dungeon d, Room room)
    {
        int bx = room.X + room.W - 2, by = room.Y + 1;
        if (d.Tiles[by][bx] != Tile.Floor) { bx = room.X + 1; by = room.Y + 1; }
        var board = new Entity(bx, by, 1, 1, 0, 0, "Quest Board", 'B',
            new Microsoft.Xna.Framework.Color(206, 170, 96), 0)
        { IsNpc = true, NpcType = "quest_board" };
        d.Monsters.Add(board);
    }

    private static void SpawnDungeonMonsters(Dungeon d, int depth, List<Room> rooms, Random rng)
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
            int j = rng.Next(i + 1);
            (floorCells[i], floorCells[j]) = (floorCells[j], floorCells[i]);
        }

        int spawned = 0;
        foreach (var (x, y) in floorCells)
        {
            if (spawned >= monsterCount) break;
            string key = pool[rng.Next(pool.Length)];
            var m = MonsterData.CreateEntity(key, depth);
            m.X = x; m.Y = y;
            d.Monsters.Add(m);
            spawned++;
        }

        Rules.InitActionTimers(d.Monsters);
    }

    private static void PlaceChests(Dungeon d, int depth, List<Room> rooms, Random rng)
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
            int j = rng.Next(i + 1);
            (floorCells[i], floorCells[j]) = (floorCells[j], floorCells[i]);
        }

        for (int i = 0; i < Math.Min(chestCount, floorCells.Count); i++)
        {
            var (x, y) = floorCells[i];
            d.Tiles[y][x] = Tile.Chest;
            // Pre-generate loot
            string lootKey = lootTable[rng.Next(lootTable.Length)];
            d.Items[(x, y)] = lootKey;
        }
    }

    /// <summary>
    /// Roll loot drops when a monster is killed.
    /// Таблица MONSTER_DROPS по глифу; у босса — BOSS_DROPS + boss_key.
    /// </summary>
    public static List<string> RollDrops(string monsterKey, char monsterCh, int depth,
        double dropBonus = 0)
    {
        var drops = new List<string>();
        dropBonus = Math.Max(0, dropBonus);

        var def = MonsterData.Get(monsterKey) ?? MonsterData.ByCh(monsterCh.ToString());
        if (def != null)
        {
            if (def.Boss && MonsterData.BossDrops.TryGetValue(def.Ch, out var bossTable))
            {
                // BOSS_DROPS — гарантированные списки с количеством
                foreach (var item in bossTable)
                    drops.Add(item);
                drops.Add("boss_key"); // ключ — 100% дроп с босса
                return drops;
            }
            if (MonsterData.MonsterDrops.TryGetValue(def.Ch, out var table))
            {
                foreach (var (item, chance) in table)
                    if (DropRng.NextDouble() < chance + dropBonus)
                        drops.Add(item);
            }
            return drops;
        }

        // Fallback: старая таблица по ключу
        if (ItemData.DropTable.TryGetValue(monsterKey, out var fallback))
        {
            foreach (var (item, chance) in fallback)
                if (DropRng.NextDouble() < chance + dropBonus)
                    drops.Add(item);
        }
        return drops;
    }
}
