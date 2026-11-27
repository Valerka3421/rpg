using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace Aethelgard.Render;

/// <summary>
/// Анимированный фон главного меню — медленный полёт камеры над процедурным
/// миром (parity render/menu_background.py). Террейн строится один раз
/// на CPU и кэшируется в текстуру больше экрана; кадр только смещает её,
/// поверх летят угольки и рисуется виньетка. Палитра сумеречного мира:
/// вода, песок, трава, лесные кластеры, дорога и руины.
/// </summary>
public class MenuBackground
{
    private readonly GraphicsDevice _device;
    private readonly Texture2D _blank;
    private Texture2D? _terrain;   // мир + вертикальное затемнение
    private Texture2D? _vignette;  // затемнение по краям (фиксировано на экране)
    private int _w, _h;
    private int _builtSw, _builtSh;
    private float[] _embers = Array.Empty<float>();   // x, y, speed, phase × 44

    private const int PadX = 260, PadY = 200;

    public MenuBackground(GraphicsDevice device)
    {
        _device = device;
        _blank = new Texture2D(device, 1, 1);
        _blank.SetData(new[] { Color.White });
        // DeviceReset инвалидирует _terrain/_vignette/_blank: раньше кэш
        // считался валидным (_terrain != null) и меню рисовало мусор
        GpuReset.Register(device, () =>
        {
            try { _terrain?.Dispose(); } catch { }
            try { _vignette?.Dispose(); } catch { }
            _terrain = null;
            _vignette = null;
            _builtSw = 0;
            _builtSh = 0;
        });
    }

    public void Draw(SpriteBatch sb, int sw, int sh, float t)
    {
        EnsureBuilt(sw, sh);

        // Плавный полёт по эллипсу: камера «дышит» над миром.
        float marginX = Math.Max(1, _w - sw);
        float marginY = Math.Max(1, _h - sh);
        float ox = marginX / 2 + MathF.Sin(t * 0.07f) * (marginX / 2 - 10);
        float oy = marginY / 2 + MathF.Cos(t * 0.05f) * (marginY / 2 - 10);
        sb.Draw(_terrain, new Vector2(-ox, -oy), Color.White);

        // Угольки/светлячки (44 шт.)
        var emberColor = new Color(240, 190, 110);
        for (int i = 0; i < _embers.Length; i += 4)
        {
            float ex = _embers[i], ey = _embers[i + 1];
            float speed = _embers[i + 2], phase = _embers[i + 3];
            float yy = ((ey - t * 14 * speed) % sh + sh) % sh;
            float xx = ex + MathF.Sin(t * speed + phase) * 12;
            float glow = MathF.Max(0.15f, 0.5f + 0.5f * MathF.Sin(t * 1.4f + phase));
            FillCircle(sb, xx, yy, 1 + (int)(glow * 2), emberColor);
        }

        if (_vignette != null)
            sb.Draw(_vignette, Vector2.Zero, Color.White);
    }

    // ─────────────────────────── ПОСТРОЕНИЕ ───────────────────────────

    private void EnsureBuilt(int sw, int sh)
    {
        if (_terrain != null && _builtSw == sw && _builtSh == sh) return;

        _w = sw + PadX * 2;
        _h = sh + PadY * 2;
        var px = new Color[_w * _h];

        var rng = new Random(20241114);
        int cell = 48;
        int gw = _w / cell + 2, gh = _h / cell + 2;
        var grid = new float[gh, gw];
        for (int y = 0; y < gh; y++)
            for (int x = 0; x < gw; x++)
                grid[y, x] = (float)rng.NextDouble();
        for (int pass = 0; pass < 2; pass++)          // размытие
        {
            var next = new float[gh, gw];
            for (int y = 0; y < gh; y++)
                for (int x = 0; x < gw; x++)
                {
                    float total = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                            total += grid[(y + dy + gh) % gh, (x + dx + gw) % gw];
                    next[y, x] = total / 9f;
                }
            grid = next;
        }

        var water = new Color(34, 52, 62);
        var waterDeep = new Color(24, 38, 50);
        var sand = new Color(96, 86, 60);
        var grassA = new Color(48, 58, 38);
        var grassB = new Color(62, 72, 44);
        var grassC = new Color(78, 84, 50);

        for (int py = 0; py < _h; py += cell / 2)
            for (int pxx = 0; pxx < _w; pxx += cell / 2)
            {
                int gy = py / cell, gx = pxx / cell;
                float v = (grid[gy % gh, gx % gw] + grid[(gy + 1) % gh, gx % gw]
                           + grid[gy % gh, (gx + 1) % gw]) / 3f;
                Color c;
                if (v < 0.30f) c = v < 0.24f ? waterDeep : water;
                else if (v < 0.36f) c = sand;
                else
                {
                    float tt = Math.Min(1f, (v - 0.36f) / 0.5f);
                    c = tt < 0.4f
                        ? Lerp(grassA, grassB, tt / 0.4f)
                        : Lerp(grassB, grassC, (tt - 0.4f) / 0.6f);
                }
                FillRect(px, _w, _h, pxx, py, cell / 2, cell / 2, c);
            }

        // Леса: кластеры тёмных кругов.
        int clusters = _w * _h / 26000;
        for (int i = 0; i < clusters; i++)
        {
            int cx = rng.Next(_w), cy = rng.Next(_h);
            if (grid[(cy / cell) % gh, (cx / cell) % gw] < 0.42f) continue;
            int n = rng.Next(2, 6);
            for (int k = 0; k < n; k++)
            {
                int tx = cx + rng.Next(-18, 19);
                int ty = cy + rng.Next(-18, 19);
                int r = rng.Next(8, 17);
                FillCircle(px, _w, _h, tx + 3, ty + 4, r, new Color(26, 34, 24));
                FillCircle(px, _w, _h, tx, ty, r, new Color(34, 44, 32));
                FillCircle(px, _w, _h, tx - 2, ty - 2, Math.Max(2, r - 4),
                    new Color(46, 58, 38));
            }
        }

        // Извилистая дорога.
        int rx = -20;
        int ry = rng.Next((int)(_h * 0.3f), (int)(_h * 0.7f));
        var pts = new List<(int X, int Y)>();
        while (rx < _w + 40)
        {
            pts.Add((rx, ry));
            rx += rng.Next(18, 35);
            ry = Math.Max(40, Math.Min(_h - 40, ry + rng.Next(-10, 11)));
        }
        var road = new Color(94, 82, 60);
        var roadLight = new Color(112, 98, 70);
        for (int i = 0; i + 1 < pts.Count; i++)
            DrawLine(px, _w, _h, pts[i].X, pts[i].Y, pts[i + 1].X, pts[i + 1].Y,
                7, road);
        for (int i = 0; i + 1 < pts.Count; i++)
            DrawLine(px, _w, _h, pts[i].X, pts[i].Y, pts[i + 1].X, pts[i + 1].Y,
                3, roadLight);

        // Руины/камни для масштаба.
        for (int i = 0; i < 26; i++)
        {
            int sx = rng.Next(_w), sy = rng.Next(_h);
            int size = rng.Next(5, 13);
            FillRect(px, _w, _h, sx, sy, size, Math.Max(4, size - 3),
                new Color(70, 66, 58));
            FillRect(px, _w, _h, sx, sy, size, 1, new Color(100, 94, 80));
            FillRect(px, _w, _h, sx, sy, 1, Math.Max(4, size - 3),
                new Color(100, 94, 80));
        }

        // Туман/затемнение поверх мира, чтобы меню читалось.
        for (int yy = 0; yy < _h; yy++)
        {
            float a = Math.Min(1f, (70 + 110f * yy / _h) / 255f);
            for (int xx = 0; xx < _w; xx++)
            {
                var d = px[yy * _w + xx];
                px[yy * _w + xx] = new Color(
                    (int)(d.R * (1 - a) + 8 * a),
                    (int)(d.G * (1 - a) + 7 * a),
                    (int)(d.B * (1 - a) + 6 * a));
            }
        }

        // Пересоздание при смене разрешения: освобождаем прежние текстуры
        // (раньше ~1920×1480 утекали на каждую смену разрешения)
        try { _terrain?.Dispose(); } catch { }
        try { _vignette?.Dispose(); } catch { }
        _terrain = null;
        _vignette = null;

        _terrain = new Texture2D(_device, _w, _h, false, SurfaceFormat.Color);
        _terrain.SetData(px);

        // Виньетка: 70 вложенных рамок (5,4,3).
        var vp = new Color[sw * sh];
        for (int i = 0; i < 70; i++)
        {
            int alpha = Math.Min(255, (int)(2 + (70 - i) * 1.2f));
            var c = new Color(5, 4, 3, alpha);
            int x0 = i, y0 = i, x1 = sw - i - 1, y1 = sh - i - 1;
            if (x0 > x1 || y0 > y1) break;
            for (int x = x0; x <= x1; x++)
            {
                vp[y0 * sw + x] = c;
                vp[y1 * sw + x] = c;
            }
            for (int y = y0; y <= y1; y++)
            {
                vp[y * sw + x0] = c;
                vp[y * sw + x1] = c;
            }
        }
        _vignette = new Texture2D(_device, sw, sh, false, SurfaceFormat.Color);
        _vignette.SetData(vp);

        // Угольки.
        _embers = new float[44 * 4];
        for (int i = 0; i < 44; i++)
        {
            _embers[i * 4] = (float)(rng.NextDouble() * sw);
            _embers[i * 4 + 1] = (float)(rng.NextDouble() * sh);
            _embers[i * 4 + 2] = 0.2f + (float)rng.NextDouble() * 0.5f;
            _embers[i * 4 + 3] = (float)rng.NextDouble() * 6.28f;
        }

        _builtSw = sw;
        _builtSh = sh;
    }

    private static Color Lerp(Color a, Color b, float k) => new(
        (int)(a.R + (b.R - a.R) * k),
        (int)(a.G + (b.G - a.G) * k),
        (int)(a.B + (b.B - a.B) * k));

    private static void FillRect(Color[] px, int w, int h, int x, int y,
        int rw, int rh, Color c)
    {
        int x0 = Math.Max(0, x), y0 = Math.Max(0, y);
        int x1 = Math.Min(w, x + rw), y1 = Math.Min(h, y + rh);
        for (int yy = y0; yy < y1; yy++)
            for (int xx = x0; xx < x1; xx++)
                px[yy * w + xx] = c;
    }

    private static void FillCircle(Color[] px, int w, int h, int cx, int cy,
        int r, Color c)
    {
        for (int dy = -r; dy <= r; dy++)
        {
            int yy = cy + dy;
            if (yy < 0 || yy >= h) continue;
            int cw = (int)Math.Sqrt(r * r - dy * dy);
            int x0 = Math.Max(0, cx - cw), x1 = Math.Min(w, cx + cw);
            for (int xx = x0; xx < x1; xx++)
                px[yy * w + xx] = c;
        }
    }

    private static void DrawLine(Color[] px, int w, int h, int x0, int y0,
        int x1, int y1, int thick, Color c)
    {
        float dx = x1 - x0, dy = y1 - y0;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        int half = thick / 2;
        if (len < 1) { FillRect(px, w, h, x0, y0, thick, thick, c); return; }
        for (float t = 0; t <= len; t += 1f)
            FillRect(px, w, h, (int)(x0 + dx * t / len) - half,
                (int)(y0 + dy * t / len) - half, thick, thick, c);
    }

    /// <summary>Заливка круга спрайт-батчем (угольки).</summary>
    private void FillCircle(SpriteBatch sb, float cx, float cy, int r, Color c)
    {
        for (int dy = -r; dy <= r; dy++)
        {
            int cw = (int)Math.Sqrt(r * r - dy * dy);
            sb.Draw(_blank,
                new Rectangle((int)cx - cw, (int)cy + dy, cw * 2, 1), c);
        }
    }
}
