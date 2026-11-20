using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Aethelgard.Core;

/// <summary>
/// World generation: overworld with biomes, village, roads, dungeon entrance.
/// Ported from Python core/world.py:generate_overworld.
/// </summary>
public static class World
{
    private static readonly Random Rng = new(42);

    // Forest density per biome
    private static readonly Dictionary<string, float> BiomeDensity = new()
    {
        { "pine", 0.55f }, { "birch", 0.42f }, { "rock", 0.30f },
        { "autumn", 0.46f }, { "swamp", 0.36f }, { "dark", 0.66f },
        { "meadow", 0.20f }, { "dead", 0.40f },
    };

    /// <summary>
    /// Generate the overworld map.
    /// Steps:
    /// 1. Fill with Floor
    /// 2. Plant forests (Wall tiles) in 8 biome regions
    /// 3. Build village (houses, walls, stalls)
    /// 4. Carve roads (Wall → Floor)
    /// 5. Clear dungeon entrance area + place stairs
    /// 6. Place cardinal gates
    /// 7. Clean roads (remove stray Wall tiles left by step 3-5)
    /// </summary>
    public static Dungeon GenerateOverworld()
    {
        var d = new Dungeon(Config.MapW, Config.MapH);

        // 1. Fill floor (already done by Dungeon constructor)

        // 2. Plant forests
        PlantForests(d);

        // 3. Build village
        BuildVillage(d);

        // 4. Carve roads
        foreach (var (x0, y0, x1, y1) in Overworld.RoadSegments)
            CarveRoad(d, x0, y0, x1, y1);

        // 5. Dungeon entrance clearing + stairs
        ClearCircle(d, Overworld.DungeonPos, Overworld.DungeonRuinsRadius);
        var (ex, ey) = Overworld.DungeonPos;
        d.Tiles[ey][ex] = Tile.Stairs;
        d.StairsPos = (ex, ey);

        // 6. Cardinal gates
        foreach (var (_, pos) in Overworld.GatePositions)
        {
            var (gx, gy) = pos;
            if (gx >= 0 && gx < d.W && gy >= 0 && gy < d.H)
                d.Tiles[gy][gx] = Tile.Gate;
        }

        // 7. Clean roads — remove stray Wall tiles and items
        CleanRoads(d);

        // 8. Spawn some monsters
        SpawnMonsters(d);

        return d;
    }

    private static void PlantForests(Dungeon d)
    {
        for (int rx = 0; rx < 3; rx++)
        {
            for (int ry = 0; ry < 3; ry++)
            {
                if (rx == 1 && ry == 1) continue; // village center

                string? biome = Overworld.BiomeNames.GetValueOrDefault((rx, ry));
                if (biome == null) continue;

                float density = BiomeDensity.GetValueOrDefault(biome, 0.4f);
                int x0 = Overworld.RegionX[rx], x1 = Overworld.RegionX[rx + 1];
                int y0 = Overworld.RegionY[ry], y1 = Overworld.RegionY[ry + 1];

                for (int y = y0 + 1; y < y1 - 1; y++)
                {
                    for (int x = x0 + 1; x < x1 - 1; x++)
                    {
                        if (Overworld.InVillage(x, y)) continue;
                        if (Overworld.IsRoad(x, y)) continue;
                        if (Rng.NextDouble() < density)
                            d.Tiles[y][x] = Tile.Wall;
                    }
                }
            }
        }
    }

    private static void BuildVillage(Dungeon d)
    {
        // City wall ring
        foreach (var (wx, wy) in Overworld.CityWallCells())
        {
            if (wx >= 0 && wx < d.W && wy >= 0 && wy < d.H)
                d.Tiles[wy][wx] = Tile.Wall;
        }

        // Well
        var (wellX, wellY) = (Overworld.VillageCenter.Item1, Overworld.VillageCenter.Item2 - 3);
        if (wellX >= 0 && wellX < d.W && wellY >= 0 && wellY < d.H)
            d.Tiles[wellY][wellX] = Tile.Wall;
    }

    private static void CarveRoad(Dungeon d, int x0, int y0, int x1, int y1)
    {
        int half = Overworld.RoadHalfWidth;
        if (y0 == y1)
        {
            int xmin = Math.Min(x0, x1), xmax = Math.Max(x0, x1);
            for (int x = xmin; x <= xmax; x++)
                for (int y = y0 - half; y <= y0 + half; y++)
                    if (x >= 0 && x < d.W && y >= 0 && y < d.H)
                        d.Tiles[y][x] = Tile.Floor;
        }
        else if (x0 == x1)
        {
            int ymin = Math.Min(y0, y1), ymax = Math.Max(y0, y1);
            for (int y = ymin; y <= ymax; y++)
                for (int x = x0 - half; x <= x0 + half; x++)
                    if (x >= 0 && x < d.W && y >= 0 && y < d.H)
                        d.Tiles[y][x] = Tile.Floor;
        }
    }

    private static void ClearCircle(Dungeon d, (int, int) center, int radius)
    {
        var (cx, cy) = center;
        for (int y = Math.Max(0, cy - radius); y <= Math.Min(d.H - 1, cy + radius); y++)
        {
            for (int x = Math.Max(0, cx - radius); x <= Math.Min(d.W - 1, cx + radius); x++)
            {
                if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius)
                    d.Tiles[y][x] = Tile.Floor;
            }
        }
    }

    /// <summary>
    /// Remove stray Wall tiles and items from road cells.
    /// Fixes "invisible blockers on road" bug from Python version.
    /// </summary>
    private static void CleanRoads(Dungeon d)
    {
        foreach (var (x0, y0, x1, y1) in Overworld.RoadSegments)
        {
            if (y0 == y1)
            {
                for (int x = Math.Min(x0, x1); x <= Math.Max(x0, x1); x++)
                    for (int y = y0 - 1; y <= y0 + 1; y++)
                        CleanCell(d, x, y);
            }
            else if (x0 == x1)
            {
                for (int y = Math.Min(y0, y1); y <= Math.Max(y0, y1); y++)
                    for (int x = x0 - 1; x <= x0 + 1; x++)
                        CleanCell(d, x, y);
            }
        }
    }

    private static void CleanCell(Dungeon d, int x, int y)
    {
        if (x < 0 || x >= d.W || y < 0 || y >= d.H) return;
        if (d.Tiles[y][x] == Tile.Wall)
            d.Tiles[y][x] = Tile.Floor;
        d.Harvestables.Remove((x, y));
        d.Items.Remove((x, y));
    }

    private static void SpawnMonsters(Dungeon d)
    {
        // Spawn a few monsters in forest areas, away from village
        var biomeMonsters = new Dictionary<string, string[]>
        {
            { "pine", new[] { "deer", "wolf", "rat" } },
            { "birch", new[] { "deer", "rat" } },
            { "rock", new[] { "wolf", "bear" } },
            { "autumn", new[] { "deer", "boar" } },
            { "swamp", new[] { "slime", "rat" } },
            { "dark", new[] { "wolf", "bear" } },
            { "meadow", new[] { "deer", "boar" } },
            { "dead", new[] { "wolf", "rat" } },
        };

        var monsterData = new Dictionary<string, (int Hp, int Atk, char Ch, string Name)>
        {
            { "rat", (3, 1, 'r', "Rat") },
            { "wolf", (8, 3, 'w', "Wolf") },
            { "deer", (5, 1, 'd', "Deer") },
            { "boar", (10, 2, 'b', "Boar") },
            { "bear", (15, 4, 'B', "Bear") },
            { "slime", (4, 1, 's', "Slime") },
        };

        int spawned = 0;
        for (int attempt = 0; attempt < 200 && spawned < 25; attempt++)
        {
            int x = Rng.Next(1, d.W - 1);
            int y = Rng.Next(1, d.H - 1);
            if (d.Tiles[y][x] != Tile.Floor) continue;
            if (Overworld.InVillage(x, y)) continue;
            if (Overworld.IsRoad(x, y)) continue;
            if (d.MonsterAt(x, y) != null) continue;

            string? biome = Overworld.BiomeAt(x, y);
            if (biome == null) continue;

            var pool = biomeMonsters.GetValueOrDefault(biome);
            if (pool == null || pool.Length == 0) continue;

            string key = pool[Rng.Next(pool.Length)];
            if (!monsterData.ContainsKey(key)) continue;

            var (hp, atk, ch, name) = monsterData[key];
            var color = key switch
            {
                "wolf" => new Microsoft.Xna.Framework.Color(130, 130, 140),
                "bear" => new Microsoft.Xna.Framework.Color(100, 70, 40),
                "deer" => new Microsoft.Xna.Framework.Color(150, 120, 80),
                "boar" => new Microsoft.Xna.Framework.Color(90, 70, 50),
                "slime" => new Microsoft.Xna.Framework.Color(100, 180, 100),
                _ => new Microsoft.Xna.Framework.Color(170, 150, 130),
            };

            d.Monsters.Add(new Entity(x, y, hp, hp, atk, 0, name, ch, color, hp));
            spawned++;
        }
    }

    /// <summary>
    /// Compute field of view from (px,py) with given radius.
    /// Simple shadowcasting — returns set of visible (x,y).
    /// </summary>
    public static HashSet<(int, int)> ComputeFov(Dungeon d, int px, int py, int radius)
    {
        var visible = new HashSet<(int, int)> { (px, py) };

        // Simple ray-cast FOV: 8 octants
        for (int angle = 0; angle < 360; angle += 3)
        {
            double rad = angle * Math.PI / 180.0;
            double dx = Math.Cos(rad), dy = Math.Sin(rad);
            double x = px, y = py;

            for (int step = 0; step < radius; step++)
            {
                x += dx;
                y += dy;
                int ix = (int)Math.Round(x);
                int iy = (int)Math.Round(y);

                if (ix < 0 || ix >= d.W || iy < 0 || iy >= d.H) break;
                visible.Add((ix, iy));

                if (d.Tiles[iy][ix] == Tile.Wall) break;
            }
        }
        return visible;
    }
}
