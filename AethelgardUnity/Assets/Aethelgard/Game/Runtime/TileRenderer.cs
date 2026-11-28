using System;
using System.Collections.Generic;
using UnityEngine;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace Aethelgard.Render;

/// <summary>
/// Процедурные тайлы 64×64 — порт MonoGame TileRenderer (оригинал:
/// render/tiles/ Python-версии). Текстуры генерируются PixelCanvas'ом
/// и кэшируются по ключу kind_variant_band (уровень света).
/// </summary>
public class TileRenderer
{
    private readonly Dictionary<string, Texture2D> _cache = new();

    public Texture2D GetTile(string kind, int variant, float light)
    {
        int band = (int)Math.Round(Math.Clamp(light, 0.3f, 1.0f) * 20);
        string key = $"{kind}_{variant}_{band}";
        if (_cache.TryGetValue(key, out var tex)) return tex;
        tex = GenerateTile(kind, variant, band / 20f);
        _cache[key] = tex;
        return tex;
    }

    private Texture2D GenerateTile(string kind, int variant, float light)
    {
        var canvas = new PixelCanvas(Config.TileSize, Config.TileSize);
        canvas.Clear(new Color32(0, 0, 0, 0));

        switch (kind)
        {
            case "wall": case "wall_moss": case "wall_torch":
                DrawWall(canvas, variant, kind); break;
            case "floor": DrawFloor(canvas, variant); break;
            case "grass_pine": case "grass_birch": case "grass_rock":
            case "grass_autumn": case "grass_swamp": case "grass_dark":
            case "grass_meadow": case "grass_dead":
                DrawGrass(canvas, variant, kind); break;
            case "road": DrawRoad(canvas, variant); break;
            case "village_floor": DrawVillageFloor(canvas, variant); break;
            case "stairs": DrawStairs(canvas); break;
            case "exit": DrawExit(canvas); break;
            case "gate": DrawGate(canvas); break;
            default: DrawFloor(canvas, variant); break;
        }

        if (light < 1f) canvas.Multiply(light);
        return canvas.ToTexture();
    }

    private static void DrawWall(PixelCanvas canvas, int variant, string kind)
    {
        canvas.FillRect(0, 0, 64, 64, Config.WallMortar.U());
        var rng = new System.Random(variant * 31 + 2);
        int yOffset = 0;
        for (int row = 0; row < 4; row++)
        {
            int h = 16, offset = (row % 2) * 16;
            for (int x = -offset; x < 64; x += 32)
            {
                var shade = Config.WallBase.Mul(0.85f + (float)rng.NextDouble() * 0.3f);
                canvas.FillRect(x, yOffset, 30, h - 2, shade);
                canvas.FillRect(x + 29, yOffset, 2, h, Config.WallMortar.U());
            }
            canvas.FillRect(0, yOffset + h - 1, 64, 1, Config.WallMortar.U());
            yOffset += h;
        }
        for (int i = 0; i < 20; i++)
            canvas.FillRect(rng.Next(0, 64), rng.Next(0, 64), 2, 2,
                new Color32(0, 0, 0, 40));
        if (kind == "wall_torch")
        {
            canvas.FillRect(48, 8, 16, 16, new Color32(255, 170, 94, 80));
            canvas.FillRect(52, 4, 8, 8, new Color32(255, 200, 100, 120));
        }
    }

    private static void DrawFloor(PixelCanvas canvas, int variant)
    {
        canvas.FillRect(0, 0, 64, 64, Config.FloorBase.U());
        var rng = new System.Random(variant * 13 + 7);
        for (int y = 0; y < 64; y += 16)
            for (int x = 0; x < 64; x += 16)
                canvas.FillRect(x + 1, y + 1, 14, 14, Config.FloorBase.Mul(0.9f + (float)rng.NextDouble() * 0.2f));
        for (int y = 0; y < 64; y += 16) canvas.FillRect(0, y, 64, 1, Config.FloorGrout.U());
        for (int x = 0; x < 64; x += 16) canvas.FillRect(x, 0, 1, 64, Config.FloorGrout.U());
        for (int i = 0; i < 8; i++)
            canvas.FillRect(rng.Next(0, 60), rng.Next(0, 60), 2, 1, new Color32(0, 0, 0, 50));
    }

    private static void DrawGrass(PixelCanvas canvas, int variant, string kind)
    {
        string biome = kind.StartsWith("grass_", StringComparison.Ordinal)
            ? kind["grass_".Length..] : kind;
        XnaColor baseC = biome switch
        {
            "pine" => new XnaColor(45, 55, 38),
            "birch" => new XnaColor(55, 62, 42),
            "rock" => new XnaColor(80, 75, 65),
            "autumn" => new XnaColor(65, 55, 35),
            "swamp" => new XnaColor(40, 50, 35),
            "dark" => new XnaColor(35, 40, 30),
            "meadow" => new XnaColor(60, 70, 45),
            "dead" => new XnaColor(50, 45, 38),
            _ => Config.GrassBase,
        };
        canvas.FillRect(0, 0, 64, 64, baseC.U());
        var rng = new System.Random(variant * 17 + 11);
        var dark = baseC.Mul(0.7f);
        var light = baseC.Mul(1.2f);
        for (int i = 0; i < 40; i++)
        {
            var c = rng.Next(3) == 0 ? light : dark;
            canvas.FillRect(rng.Next(0, 64), rng.Next(0, 64), 1, 2, c);
        }
        if (biome == "meadow" && rng.NextDouble() < 0.15)
            canvas.FillRect(rng.Next(8, 56), rng.Next(8, 56), 2, 2,
                new Color32(220, 180, 60, 255));
    }

    private static void DrawRoad(PixelCanvas canvas, int variant)
    {
        canvas.FillRect(0, 0, 64, 64, Config.RoadColor.U());
        var rng = new System.Random(variant * 23 + 5);
        for (int i = 0; i < 15; i++)
            canvas.FillRect(rng.Next(0, 64), rng.Next(0, 64), 2, 2,
                new Color32(80, 70, 50, 80));
        for (int i = 0; i < 4; i++)
            canvas.FillRect(rng.Next(4, 60), rng.Next(4, 60), 3, 2, Config.Stone.U());
    }

    private static void DrawVillageFloor(PixelCanvas canvas, int variant)
    {
        canvas.FillRect(0, 0, 64, 64, new Color32(50, 45, 38, 255));
        var rng = new System.Random(variant * 29 + 3);
        for (int y = 0; y < 64; y += 21)
            for (int x = 0; x < 64; x += 21)
            {
                float f = 0.9f + (float)rng.NextDouble() * 0.2f;
                canvas.FillRect(x + 1, y + 1, 19, 19,
                    new Color32((byte)(100 * f), (byte)(90 * f), (byte)(70 * f), 255));
            }
    }

    private static void DrawStairs(PixelCanvas canvas)
    {
        canvas.FillRect(0, 0, 64, 64, Config.FloorBase.U());
        for (int i = 0; i < 5; i++)
        {
            int y = 10 + i * 9, w = 40 - i * 4;
            var c = i % 2 == 0 ? Config.ColStairs.U() : new Color32(180, 150, 60, 255);
            canvas.FillRect((64 - w) / 2, y, w, 7, c);
        }
    }

    private static void DrawExit(PixelCanvas canvas)
    {
        canvas.FillRect(0, 0, 64, 64, Config.FloorBase.U());
        canvas.FillRect(12, 0, 40, 64, new Color32(120, 110, 80, 100));
        canvas.FillRect(20, 0, 24, 64, new Color32(180, 160, 100, 80));
    }

    private static void DrawGate(PixelCanvas canvas)
    {
        canvas.FillRect(0, 0, 64, 64, Config.FloorBase.U());
        canvas.FillRect(0, 0, 12, 64, Config.WallBase.U());
        canvas.FillRect(52, 0, 12, 64, Config.WallBase.U());
        canvas.FillRect(0, 0, 64, 8, Config.WallBase.U());
        canvas.FillRect(12, 8, 40, 56, new Color32(10, 8, 6, 255));
    }

    /// <summary>Тип тайла для отрисовки (parity MonoGame TileRenderer.TileKindAt).</summary>
    public static string TileKindAt(Dungeon d, int x, int y)
    {
        var tile = d.TileAt(x, y);
        if (tile == Tile.Wall) return Overworld.WallKind(x, y);
        if (tile == Tile.Stairs) return "stairs";
        if (tile == Tile.Exit) return "exit";
        if (tile == Tile.Gate) return "gate";
        return Overworld.GroundKind(x, y);
    }
}
