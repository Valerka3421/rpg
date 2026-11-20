using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Aethelgard.Core;

namespace Aethelgard.Render;

/// <summary>
/// Procedural tile renderer — draws 64×64 tiles to RenderTarget2D, caches them.
/// Ported from Python render/tiles/.
///
/// Hybrid approach: tiles are generated procedurally on first access,
/// then cached as textures. This preserves the Stoneshard palette.
/// </summary>
public class TileRenderer
{
    private readonly GraphicsDevice _device;
    private readonly Dictionary<string, Texture2D> _cache = new();
    private readonly SpriteBatch _sb;

    public TileRenderer(GraphicsDevice device)
    {
        _device = device;
        _sb = new SpriteBatch(device);
    }

    /// <summary>
    /// Get (or generate) a tile texture by kind + variant + light level.
    /// Key format: "kind_variant_band"
    /// </summary>
    public Texture2D GetTile(string kind, int variant, float light)
    {
        int band = (int)Math.Round(Math.Clamp(light, 0.3f, 1.0f) * 20);
        string key = $"{kind}_{variant}_{band}";

        if (_cache.TryGetValue(key, out var tex))
            return tex;

        // Generate base tile
        var baseTex = GenerateTile(kind, variant);
        // Apply lighting
        if (band < 20)
            baseTex = ApplyLight(baseTex, band / 20f);

        _cache[key] = baseTex;
        return baseTex;
    }

    private Texture2D GenerateTile(string kind, int variant)
    {
        var rt = new RenderTarget2D(_device, Config.TileSize, Config.TileSize,
            false, SurfaceFormat.Color, DepthFormat.None);
        _device.SetRenderTarget(rt);
        _device.Clear(Color.Transparent);

        _sb.Begin();

        switch (kind)
        {
            case "wall":
            case "wall_moss":
            case "wall_torch":
                DrawWall(_sb, variant, kind);
                break;
            case "floor":
                DrawFloor(_sb, variant);
                break;
            case "grass_pine":
            case "grass_birch":
            case "grass_rock":
            case "grass_autumn":
            case "grass_swamp":
            case "grass_dark":
            case "grass_meadow":
            case "grass_dead":
                DrawGrass(_sb, variant, kind);
                break;
            case "road":
                DrawRoad(_sb, variant);
                break;
            case "village_floor":
                DrawVillageFloor(_sb, variant);
                break;
            case "stairs":
                DrawStairs(_sb);
                break;
            case "exit":
                DrawExit(_sb);
                break;
            case "gate":
                DrawGate(_sb);
                break;
            default:
                DrawFloor(_sb, variant);
                break;
        }

        _sb.End();
        _device.SetRenderTarget(null);
        return rt;
    }

    private void DrawWall(SpriteBatch sb, int variant, string kind)
    {
        // Fill with mortar
        sb.Draw(blank, new Rectangle(0, 0, 64, 64), Config.WallMortar);

        // Stone blocks — 4 rows with offset
        var rng = new Random(variant * 31 + 2);
        int[] rowHeights = { 16, 16, 16, 16 };
        int yOffset = 0;
        for (int row = 0; row < 4; row++)
        {
            int h = rowHeights[row];
            int offset = (row % 2) * 16;
            for (int x = -offset; x < 64; x += 32)
            {
                var rect = new Rectangle(x, yOffset, 30, h - 2);
                var shade = Config.WallBase;
                // Random shade variation
                float f = 0.85f + (float)rng.NextDouble() * 0.3f;
                shade = new Color(
                    (int)(shade.R * f), (int)(shade.G * f), (int)(shade.B * f));
                sb.Draw(blank, rect, shade);
                // Mortar lines
                sb.Draw(blank, new Rectangle(x + 29, yOffset, 2, h), Config.WallMortar);
            }
            sb.Draw(blank, new Rectangle(0, yOffset + h - 1, 64, 1), Config.WallMortar);
            yOffset += h;
        }

        // Grime — dark spots
        for (int i = 0; i < 20; i++)
        {
            int x = rng.Next(0, 64), y = rng.Next(0, 64);
            sb.Draw(blank, new Rectangle(x, y, 2, 2),
                new Color(0, 0, 0, 40));
        }

        // Torch variant — add glow
        if (kind == "wall_torch")
        {
            var glow = new Color(255, 170, 94, 80);
            sb.Draw(blank, new Rectangle(48, 8, 16, 16), glow);
            sb.Draw(blank, new Rectangle(52, 4, 8, 8), new Color(255, 200, 100, 120));
        }
    }

    private void DrawFloor(SpriteBatch sb, int variant)
    {
        sb.Draw(blank, new Rectangle(0, 0, 64, 64), Config.FloorBase);

        // Stone tiles pattern
        var rng = new Random(variant * 13 + 7);
        for (int y = 0; y < 64; y += 16)
        {
            for (int x = 0; x < 64; x += 16)
            {
                float f = 0.9f + (float)rng.NextDouble() * 0.2f;
                var c = new Color(
                    (int)(Config.FloorBase.R * f),
                    (int)(Config.FloorBase.G * f),
                    (int)(Config.FloorBase.B * f));
                sb.Draw(blank, new Rectangle(x + 1, y + 1, 14, 14), c);
            }
        }

        // Grout lines
        for (int y = 0; y < 64; y += 16)
            sb.Draw(blank, new Rectangle(0, y, 64, 1), Config.FloorGrout);
        for (int x = 0; x < 64; x += 16)
            sb.Draw(blank, new Rectangle(x, 0, 1, 64), Config.FloorGrout);

        // Random cracks/grime
        for (int i = 0; i < 8; i++)
        {
            int x = rng.Next(0, 60), y = rng.Next(0, 60);
            sb.Draw(blank, new Rectangle(x, y, 2, 1), new Color(0, 0, 0, 50));
        }
    }

    private void DrawGrass(SpriteBatch sb, int variant, string biome)
    {
        // Base grass color varies by biome
        Color base_ = biome switch
        {
            "pine" => new Color(45, 55, 38),
            "birch" => new Color(55, 62, 42),
            "rock" => new Color(80, 75, 65),
            "autumn" => new Color(65, 55, 35),
            "swamp" => new Color(40, 50, 35),
            "dark" => new Color(35, 40, 30),
            "meadow" => new Color(60, 70, 45),
            "dead" => new Color(50, 45, 38),
            _ => Config.GrassBase,
        };
        sb.Draw(blank, new Rectangle(0, 0, 64, 64), base_);

        // Grass texture — small dots
        var rng = new Random(variant * 17 + 11);
        var dark = new Color(
            (int)(base_.R * 0.7f), (int)(base_.G * 0.7f), (int)(base_.B * 0.7f));
        var light = new Color(
            Math.Min(255, (int)(base_.R * 1.2f)),
            Math.Min(255, (int)(base_.G * 1.2f)),
            Math.Min(255, (int)(base_.B * 1.2f)));

        for (int i = 0; i < 40; i++)
        {
            int x = rng.Next(0, 64), y = rng.Next(0, 64);
            var c = rng.Next(3) == 0 ? light : dark;
            sb.Draw(blank, new Rectangle(x, y, 1, 2), c);
        }

        // Occasional small flower in meadow
        if (biome == "meadow" && rng.NextDouble() < 0.15)
        {
            int x = rng.Next(8, 56), y = rng.Next(8, 56);
            sb.Draw(blank, new Rectangle(x, y, 2, 2), new Color(220, 180, 60));
        }
    }

    private void DrawRoad(SpriteBatch sb, int variant)
    {
        sb.Draw(blank, new Rectangle(0, 0, 64, 64), Config.RoadColor);

        // Dirt texture
        var rng = new Random(variant * 23 + 5);
        for (int i = 0; i < 15; i++)
        {
            int x = rng.Next(0, 64), y = rng.Next(0, 64);
            sb.Draw(blank, new Rectangle(x, y, 2, 2),
                new Color(80, 70, 50, 80));
        }

        // Small stones
        for (int i = 0; i < 4; i++)
        {
            int x = rng.Next(4, 60), y = rng.Next(4, 60);
            sb.Draw(blank, new Rectangle(x, y, 3, 2), Config.Stone);
        }
    }

    private void DrawVillageFloor(SpriteBatch sb, int variant)
    {
        // Cobbled village floor
        sb.Draw(blank, new Rectangle(0, 0, 64, 64), new Color(50, 45, 38));

        var rng = new Random(variant * 29 + 3);
        for (int y = 0; y < 64; y += 21)
        {
            for (int x = 0; x < 64; x += 21)
            {
                float f = 0.9f + (float)rng.NextDouble() * 0.2f;
                var c = new Color(
                    (int)(100 * f), (int)(90 * f), (int)(70 * f));
                sb.Draw(blank, new Rectangle(x + 1, y + 1, 19, 19), c);
            }
        }
    }

    private void DrawStairs(SpriteBatch sb)
    {
        sb.Draw(blank, new Rectangle(0, 0, 64, 64), Config.FloorBase);
        // Steps going down
        for (int i = 0; i < 5; i++)
        {
            int y = 10 + i * 9;
            int w = 40 - i * 4;
            int x = (64 - w) / 2;
            var c = i % 2 == 0 ? Config.ColStairs : new Color(180, 150, 60);
            sb.Draw(blank, new Rectangle(x, y, w, 7), c);
        }
    }

    private void DrawExit(SpriteBatch sb)
    {
        sb.Draw(blank, new Rectangle(0, 0, 64, 64), Config.FloorBase);
        // Daylight arch
        sb.Draw(blank, new Rectangle(12, 0, 40, 64),
            new Color(120, 110, 80, 100));
        sb.Draw(blank, new Rectangle(20, 0, 24, 64),
            new Color(180, 160, 100, 80));
    }

    private void DrawGate(SpriteBatch sb)
    {
        sb.Draw(blank, new Rectangle(0, 0, 64, 64), Config.FloorBase);
        // Stone arch
        sb.Draw(blank, new Rectangle(0, 0, 12, 64), Config.WallBase);
        sb.Draw(blank, new Rectangle(52, 0, 12, 64), Config.WallBase);
        sb.Draw(blank, new Rectangle(0, 0, 64, 8), Config.WallBase);
        // Dark passage
        sb.Draw(blank, new Rectangle(12, 8, 40, 56),
            new Color(10, 8, 6));
    }

    /// <summary>
    /// Apply lighting (darken) to a tile texture.
    /// </summary>
    private Texture2D ApplyLight(Texture2D baseTex, float light)
    {
        // Simple: create a darkened copy
        var rt = new RenderTarget2D(_device, Config.TileSize, Config.TileSize,
            false, SurfaceFormat.Color, DepthFormat.None);
        _device.SetRenderTarget(rt);
        _device.Clear(Color.Transparent);
        _sb.Begin();
        _sb.Draw(baseTex, Vector2.Zero, Color.White * light);
        _sb.End();
        _device.SetRenderTarget(null);
        return rt;
    }

    // Lazy-initialized 1×1 white texture for drawing rectangles
    private Texture2D? _blank;
    private Texture2D blank
    {
        get
        {
            if (_blank == null)
            {
                _blank = new Texture2D(_device, 1, 1);
                _blank.SetData(new[] { Color.White });
            }
            return _blank;
        }
    }

    /// <summary>
    /// Determine tile kind for rendering at (x,y) on the dungeon.
    /// </summary>
    public static string TileKindAt(Dungeon d, int x, int y)
    {
        var tile = d.TileAt(x, y);
        if (tile == Tile.Wall)
        {
            return Overworld.WallKind(x, y);
        }
        if (tile == Tile.Stairs) return "stairs";
        if (tile == Tile.Exit) return "exit";
        if (tile == Tile.Gate) return "gate";
        return Overworld.GroundKind(x, y);
    }
}
