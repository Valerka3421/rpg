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

    /// <summary>Очистить кэш процедурных текстур (DeviceReset — рисование
    /// не идёт, dispose безопасен сразу).</summary>
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

    // «Кладбище» выброшенных текстур: Dispose посреди открытого SpriteBatch
    // (Deferred) рисовал по удалённому GPU-ресурсу — старейший тайл обычно
    // уже в очереди этого кадра. Освобождаем только в начале следующего кадра.
    private readonly List<Texture2D> _graveyard = new();

    /// <summary>Вызывается рендерами в начале Draw (до SpriteBatch.Begin).</summary>
    public void DrainDisposals()
    {
        if (_graveyard.Count == 0) return;
        foreach (var tex in _graveyard)
        {
            try { tex.Dispose(); } catch { }
        }
        _graveyard.Clear();
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
                _graveyard.Add(stale);   // dispose отложен до следующего кадра
        }

        tex = GenerateTile(kind, variant, band / 20f);
        _cache[key] = tex;
        _insertOrder.AddFirst(key);
        return tex;
    }

    private Texture2D GenerateTile(string kind, int variant, float light)
        => TileArt.GenerateTile(kind, variant, light).ToTexture(_device);

    /// <summary>Determine tile kind for rendering at (x,y) on the dungeon.</summary>
    public static string TileKindAt(Dungeon d, int x, int y) => TileArt.TileKindAt(d, x, y);
 }
