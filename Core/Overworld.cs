using System;
using System.Collections.Generic;

namespace Aethelgard.Core;

/// <summary>
/// Overworld layout constants — biomes, village, roads, dungeon entrance.
/// Ported from Python core/overworld.py.
/// </summary>
public static class Overworld
{
    // ── Grid 3×3 for biome regions ──
    public static readonly int[] RegionX = { 0, 33, 66, 100 };
    public static readonly int[] RegionY = { 0, 23, 46, 70 };

    // ── Village ──
    public static readonly (int, int) VillageCenter = (50, 35);
    public static readonly (int X, int Y, int W, int H) VillageRect =
        (34, 24, 32, 21);

    // ── City wall gate ──
    public const int CityGateWidth = 3;

    // ── Dungeon entrance ──
    public static readonly (int, int) DungeonPos = (83, 10);
    public const int DungeonRuinsRadius = 10;

    // ── Road width (half) ──
    public const int RoadHalfWidth = 1;

    // ── Biome names by grid cell ──
    public static readonly Dictionary<(int, int), string> BiomeNames = new()
    {
        { (0, 0), "pine" },
        { (1, 0), "birch" },
        { (2, 0), "rock" },
        { (0, 1), "autumn" },
        { (2, 1), "swamp" },
        { (0, 2), "dark" },
        { (1, 2), "meadow" },
        { (2, 2), "dead" },
    };

    // ── Road segments: (x0, y0, x1, y1) ──
    public static readonly (int, int, int, int)[] RoadSegments = {
        // West: village to left edge
        (33, 35, 0, 35),
        // East: village to right edge of legacy map
        (65, 35, 99, 35),
        // North: village to top edge
        (50, 23, 50, 0),
        // South: village to bottom edge
        (50, 45, 50, 69),
        // Road to dungeon entrance
        (65, 30, 83, 30),
        (83, 30, 83, 10),
    };

    // ── Cardinal gates on map edges ──
    public static readonly Dictionary<string, (int, int)> GatePositions = new()
    {
        { "north", (50, 0) },
        { "south", (50, 69) },
        { "west", (0, 35) },
        { "east", (99, 35) },
    };

    /// <summary>
    /// Check if (x,y) is within the village rectangle.
    /// </summary>
    public static bool InVillage(int x, int y)
    {
        var (vx, vy, vw, vh) = VillageRect;
        return x >= vx && x < vx + vw && y >= vy && y < vy + vh;
    }

    /// <summary>
    /// Check if (x,y) is on any road segment (including half-width).
    /// </summary>
    public static bool IsRoad(int x, int y)
    {
        foreach (var (x0, y0, x1, y1) in RoadSegments)
        {
            if (y0 == y1)
            {
                if (Math.Min(x0, x1) <= x && x <= Math.Max(x0, x1)
                    && Math.Abs(y - y0) <= RoadHalfWidth)
                    return true;
            }
            else if (x0 == x1)
            {
                if (Math.Min(y0, y1) <= y && y <= Math.Max(y0, y1)
                    && Math.Abs(x - x0) <= RoadHalfWidth)
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Biome name at (x,y), or null for village center.
    /// </summary>
    public static string? BiomeAt(int x, int y)
    {
        int rx = -1, ry = -1;
        for (int i = 0; i < 3; i++)
        {
            if (x >= RegionX[i] && x < RegionX[i + 1]) rx = i;
            if (y >= RegionY[i] && y < RegionY[i + 1]) ry = i;
        }
        if (rx < 0 || ry < 0) return null;
        if (rx == 1 && ry == 1) return null; // village center
        return BiomeNames.GetValueOrDefault((rx, ry));
    }

    /// <summary>
    /// City wall cells — ring around village with gates.
    /// </summary>
    public static HashSet<(int, int)> CityWallCells()
    {
        var (vx, vy, vw, vh) = VillageRect;
        var (cx, cy) = VillageCenter;
        int half = CityGateWidth / 2;
        var cells = new HashSet<(int, int)>();

        for (int x = vx; x < vx + vw; x++)
        {
            if (Math.Abs(x - cx) > half)
            {
                cells.Add((x, vy));
                cells.Add((x, vy + vh - 1));
            }
        }
        for (int y = vy; y < vy + vh; y++)
        {
            if (Math.Abs(y - cy) > half)
            {
                cells.Add((vx, y));
                cells.Add((vx + vw - 1, y));
            }
        }
        return cells;
    }

    /// <summary>
    /// Determine wall kind at (x,y) for rendering — "tree_pine", "rock", "wall", etc.
    /// </summary>
    public static string WallKind(int x, int y)
    {
        if (InVillage(x, y)) return "wall";
        string? biome = BiomeAt(x, y);
        if (biome == null) return "wall";
        return biome switch
        {
            "pine" => "tree_pine",
            "birch" => "tree_birch",
            "rock" => "rock",
            "autumn" => "tree_autumn",
            "swamp" => "tree_swamp",
            "dark" => "tree_dark",
            "meadow" => "tree_meadow",
            "dead" => "tree_dead",
            _ => "wall",
        };
    }

    /// <summary>
    /// Ground kind at (x,y) for rendering — "floor", "road", "grass_pine", etc.
    /// </summary>
    public static string GroundKind(int x, int y)
    {
        if (IsRoad(x, y)) return "road";
        if (InVillage(x, y)) return "village_floor";
        string? biome = BiomeAt(x, y);
        if (biome == null) return "floor";
        return $"grass_{biome}";
    }
}
