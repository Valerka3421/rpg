using System;
using UnityEngine;

namespace Aethelgard.Render;

/// <summary>
/// Программный холст для процедурных текстур (порт MonoGame PixelCanvas).
/// Пиксели собираются в памяти и один раз загружаются в Texture2D
/// (RGBA32, straight alpha). Все тайлы/спрайты/иконки игры генерируются им.
/// </summary>
public sealed class PixelCanvas
{
    public int Width { get; }
    public int Height { get; }
    public Color32[] Pixels { get; }

    public PixelCanvas(int width, int height)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        Pixels = new Color32[Width * Height];
    }

    public void Clear(Color32 color)
    {
        for (int i = 0; i < Pixels.Length; i++) Pixels[i] = color;
    }

    /// <summary>Заливка прямоугольника с source-over смешиванием.</summary>
    public void FillRect(int x, int y, int w, int h, Color32 color)
    {
        if (w <= 0 || h <= 0) return;
        int x0 = Math.Max(0, x);
        int y0 = Math.Max(0, y);
        int x1 = Math.Min(Width, x + w);
        int y1 = Math.Min(Height, y + h);
        if (x0 >= x1 || y0 >= y1) return;

        for (int py = y0; py < y1; py++)
        {
            int row = py * Width;
            for (int px = x0; px < x1; px++)
                Pixels[row + px] = Blend(color, Pixels[row + px]);
        }
    }

    public void SetPixel(int x, int y, Color32 color) =>
        FillRect(x, y, 1, 1, color);

    public void FillCircle(int cx, int cy, int radius, Color32 color)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            int w = (int)Math.Sqrt(radius * radius - dy * dy);
            FillRect(cx - w, cy + dy, w * 2, 1, color);
        }
    }

    /// <summary>Умножение яркости (затемнение по уровню света).</summary>
    public void Multiply(float factor)
    {
        factor = Mathf.Clamp01(factor);
        for (int i = 0; i < Pixels.Length; i++)
        {
            var c = Pixels[i];
            Pixels[i] = new Color32(
                (byte)(c.r * factor), (byte)(c.g * factor),
                (byte)(c.b * factor), c.a);
        }
    }

    public Texture2D ToTexture()
    {
        var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixels32(Pixels);
        tex.Apply(false, false);
        return tex;
    }

    private static Color32 Blend(Color32 src, Color32 dst)
    {
        if (src.a == 255) return src;
        if (src.a == 0) return dst;
        float sa = src.a / 255f;
        float inv = 1f - sa;
        return new Color32(
            (byte)Math.Min(255, src.r * sa + dst.r * inv),
            (byte)Math.Min(255, src.g * sa + dst.g * inv),
            (byte)Math.Min(255, src.b * sa + dst.b * inv),
            (byte)Math.Min(255, src.a + dst.a * inv));
    }
}

/// <summary>Конвертация цветов палитры Core (Microsoft.Xna shim) в Unity.</summary>
public static class ColorExt
{
    public static Color32 U(this Microsoft.Xna.Framework.Color c) =>
        new(c.R, c.G, c.B, c.A);

    public static Color32 Mul(this Microsoft.Xna.Framework.Color c, float f) =>
        new((byte)(c.R * f), (byte)(c.G * f), (byte)(c.B * f), c.A);
}
