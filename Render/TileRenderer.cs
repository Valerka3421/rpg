using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Aethelgard.Render;

/// <summary>
/// Procedural tile renderer — draws 64×64 tiles, caches them.
/// Ported from Python render/tiles/.
///
/// Текстуры генерируются программно (PixelCanvas) и кэшируются. Это
/// принципиально: генерация через RenderTarget2D во время активного
/// SpriteBatch сбрасывала текущую цель рендера и ломала кадр.
/// </summary>
public class TileRenderer : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly Dictionary<string, Texture2D> _cache = new();
    // Порядок вставки ключей для выброса: раньше при переполнении кэш
    // сбрасывался ЦЕЛИКОМ, и движение игрока (меняющее световые полосы
    // соседних тайлов) вызывало массовые регенерации текстур в кадр — фризы.
    // Выброс старейших по вставке — O(1) амортизированно на кадр.
    private readonly LinkedList<string> _insertOrder = new();

    // После DeviceReset (переключение fullscreen, смена разрешения, потеря
    // контекста) все созданные текстуры становятся недействительными — кэш
    // нужно очистить, иначе рисуются «инвалидные» текстуры.
    private static readonly List<TileRenderer> _instances = new();
    private static bool _hooked;

    public TileRenderer(GraphicsDevice device)
    {
        _device = device;
        if (!_hooked)
        {
            _hooked = true;
            device.DeviceResetting += OnDeviceResetting;
        }
        _instances.Add(this);
    }

    private static void OnDeviceResetting(object? sender, EventArgs e)
    {
        foreach (var t in _instances)
        {
            t.ClearCache();
        }
    }

    /// <summary>Очистить кэш процедурных текстур.</summary>
    public void ClearCache()
    {
        foreach (var tex in _cache.Values)
        {
            try { tex.Dispose(); } catch { }
        }
        _cache.Clear();
        _insertOrder.Clear();
    }

    public void Dispose()
    {
        _instances.Remove(this);
        ClearCache();
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

        // Мягкий лимит: выбрасываем только старейшие по вставке записи,
        // а не весь кэш — пере-генерация одной текстуры дешевле,
        // чем полный сброс при каждом переполнении.
        while (_cache.Count > 2000 && _insertOrder.Count > 0)
        {
            var oldest = _insertOrder.Last!.Value;
            _insertOrder.RemoveLast();
            if (_cache.Remove(oldest, out var stale))
            {
                try { stale.Dispose(); } catch { }
            }
        }

        tex = GenerateTile(kind, variant, band / 20f);
        _cache[key] = tex;
        _insertOrder.AddFirst(key);
        return tex;
    }

    private Texture2D GenerateTile(string kind, int variant, float light)
    {
        var canvas = new PixelCanvas(Config.TileSize, Config.TileSize);
        canvas.Clear(Color.Transparent);

        switch (kind)
        {
            case "wall":
            case "wall_moss":
            case "wall_torch":
                DrawWall(canvas, variant, kind);
                break;
            case "floor":
                DrawFloor(canvas, variant);
                break;
            case "grass_pine":
            case "grass_birch":
            case "grass_rock":
            case "grass_autumn":
            case "grass_swamp":
            case "grass_dark":
            case "grass_meadow":
            case "grass_dead":
                DrawGrass(canvas, variant, kind);
                break;
            case "road":
                DrawRoad(canvas, variant);
                break;
            case "village_floor":
                DrawVillageFloor(canvas, variant);
                break;
            case "stairs":
                DrawStairs(canvas);
                break;
            case "exit":
                DrawExit(canvas);
                break;
            case "gate":
                DrawGate(canvas);
                break;
            default:
                DrawFloor(canvas, variant);
                break;
        }

        // Затемнение по уровню освещения. Раньше для этого создавался
        // отдельный RenderTarget2D; теперь просто масштабируем пиксели.
        if (light < 1f)
            canvas.Multiply(light);

        return canvas.ToTexture(_device);
    }

    private static void DrawWall(PixelCanvas canvas, int variant, string kind)
    {
        canvas.FillRect(0, 0, 64, 64, Config.WallMortar);

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
                float f = 0.85f + (float)rng.NextDouble() * 0.3f;
                shade = new Color(
                    (int)(shade.R * f), (int)(shade.G * f), (int)(shade.B * f));
                canvas.FillRect(rect, shade);
                canvas.FillRect(x + 29, yOffset, 2, h, Config.WallMortar);
            }
            canvas.FillRect(0, yOffset + h - 1, 64, 1, Config.WallMortar);
            yOffset += h;
        }

        for (int i = 0; i < 20; i++)
        {
            int x = rng.Next(0, 64), y = rng.Next(0, 64);
            canvas.FillRect(x, y, 2, 2, new Color(0, 0, 0, 40));
        }

        if (kind == "wall_torch")
        {
            var glow = new Color(255, 170, 94, 80);
            canvas.FillRect(48, 8, 16, 16, glow);
            canvas.FillRect(52, 4, 8, 8, new Color(255, 200, 100, 120));
        }
    }

    private static void DrawFloor(PixelCanvas canvas, int variant)
    {
        canvas.FillRect(0, 0, 64, 64, Config.FloorBase);

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
                canvas.FillRect(x + 1, y + 1, 14, 14, c);
            }
        }

        for (int y = 0; y < 64; y += 16)
            canvas.FillRect(0, y, 64, 1, Config.FloorGrout);
        for (int x = 0; x < 64; x += 16)
            canvas.FillRect(x, 0, 1, 64, Config.FloorGrout);

        for (int i = 0; i < 8; i++)
        {
            int x = rng.Next(0, 60), y = rng.Next(0, 60);
            canvas.FillRect(x, y, 2, 1, new Color(0, 0, 0, 50));
        }
    }

    private static void DrawGrass(PixelCanvas canvas, int variant, string kind)
    {
        // kind приходит как "grass_pine" — раньше switch по "pine" не
        // срабатывал, и все биомы получали один цвет.
        string biome = kind.StartsWith("grass_", StringComparison.Ordinal)
            ? kind["grass_".Length..]
            : kind;

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
        canvas.FillRect(0, 0, 64, 64, base_);

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
            canvas.FillRect(x, y, 1, 2, c);
        }

        if (biome == "meadow" && rng.NextDouble() < 0.15)
        {
            int x = rng.Next(8, 56), y = rng.Next(8, 56);
            canvas.FillRect(x, y, 2, 2, new Color(220, 180, 60));
        }
    }

    private static void DrawRoad(PixelCanvas canvas, int variant)
    {
        canvas.FillRect(0, 0, 64, 64, Config.RoadColor);

        var rng = new Random(variant * 23 + 5);
        for (int i = 0; i < 15; i++)
        {
            int x = rng.Next(0, 64), y = rng.Next(0, 64);
            canvas.FillRect(x, y, 2, 2, new Color(80, 70, 50, 80));
        }

        for (int i = 0; i < 4; i++)
        {
            int x = rng.Next(4, 60), y = rng.Next(4, 60);
            canvas.FillRect(x, y, 3, 2, Config.Stone);
        }
    }

    private static void DrawVillageFloor(PixelCanvas canvas, int variant)
    {
        canvas.FillRect(0, 0, 64, 64, new Color(50, 45, 38));

        var rng = new Random(variant * 29 + 3);
        for (int y = 0; y < 64; y += 21)
        {
            for (int x = 0; x < 64; x += 21)
            {
                float f = 0.9f + (float)rng.NextDouble() * 0.2f;
                var c = new Color(
                    (int)(100 * f), (int)(90 * f), (int)(70 * f));
                canvas.FillRect(x + 1, y + 1, 19, 19, c);
            }
        }
    }

    private static void DrawStairs(PixelCanvas canvas)
    {
        canvas.FillRect(0, 0, 64, 64, Config.FloorBase);
        for (int i = 0; i < 5; i++)
        {
            int y = 10 + i * 9;
            int w = 40 - i * 4;
            int x = (64 - w) / 2;
            var c = i % 2 == 0 ? Config.ColStairs : new Color(180, 150, 60);
            canvas.FillRect(x, y, w, 7, c);
        }
    }

    private static void DrawExit(PixelCanvas canvas)
    {
        canvas.FillRect(0, 0, 64, 64, Config.FloorBase);
        canvas.FillRect(12, 0, 40, 64, new Color(120, 110, 80, 100));
        canvas.FillRect(20, 0, 24, 64, new Color(180, 160, 100, 80));
    }

    private static void DrawGate(PixelCanvas canvas)
    {
        canvas.FillRect(0, 0, 64, 64, Config.FloorBase);
        canvas.FillRect(0, 0, 12, 64, Config.WallBase);
        canvas.FillRect(52, 0, 12, 64, Config.WallBase);
        canvas.FillRect(0, 0, 64, 8, Config.WallBase);
        canvas.FillRect(12, 8, 40, 56, new Color(10, 8, 6));
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
