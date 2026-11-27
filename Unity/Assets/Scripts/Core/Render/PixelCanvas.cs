using System;
using Microsoft.Xna.Framework;

namespace Aethelgard.Render;

/// <summary>
/// Небольшой программный холст для процедурных текстур.
///
/// Зачем: раньше тайлы и иконки рисовались через RenderTarget2D прямо во
/// время активного SpriteBatch. Смена render target внутри Begin/End ломает
/// пакет (и сбрасывает целевой буфер), поэтому текстуры генерировались
/// нестабильно. Здесь пиксели собираются в памяти и один раз загружаются
/// в Texture2D — графическое устройство не переключается.
///
/// Пиксели хранятся в premultiplied alpha, как их пишет AlphaBlend в
/// RenderTarget2D. Это сохраняет корректное смешивание полупрозрачных
/// прямоугольников и при последующей отрисовке SpriteBatch.
///
/// Класс partial: буфер (этот файл) лежит в Unity-ядре и используется
/// Unity-версией рендера тайлов; GPU-загрузка (ToTexture) — в движковом
/// слое MonoGame (Render/PixelCanvas.Gpu.cs).
/// </summary>
public sealed partial class PixelCanvas
{
    public int Width { get; }
    public int Height { get; }
    public Color[] Pixels { get; }

    public PixelCanvas(int width, int height)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        Pixels = new Color[Width * Height];
    }

    public void Clear(Color color)
    {
        color = Premultiply(color);
        for (int i = 0; i < Pixels.Length; i++)
            Pixels[i] = color;
    }

    /// <summary>Заливка прямоугольника (координаты обрезаются по холсту).</summary>
    public void FillRect(int x, int y, int w, int h, Color color)
    {
        if (w <= 0 || h <= 0) return;
        int x0 = Math.Max(0, x);
        int y0 = Math.Max(0, y);
        int x1 = Math.Min(Width, x + w);
        int y1 = Math.Min(Height, y + h);
        if (x0 >= x1 || y0 >= y1) return;

        color = Premultiply(color);
        for (int py = y0; py < y1; py++)
        {
            int row = py * Width;
            for (int px = x0; px < x1; px++)
                Pixels[row + px] = Blend(color, Pixels[row + px]);
        }
    }

    public void FillRect(Rectangle rect, Color color) =>
        FillRect(rect.X, rect.Y, rect.Width, rect.Height, color);

    public void SetPixel(int x, int y, Color color)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return;
        int i = y * Width + x;
        Pixels[i] = Blend(Premultiply(color), Pixels[i]);
    }

    /// <summary>Заливка круга построчно (как DrawFilledCircle в старом коде).</summary>
    public void FillCircle(Vector2 center, int radius, Color color)
    {
        if (radius <= 0) return;
        int cx = (int)center.X;
        int cy = (int)center.Y;
        for (int dy = -radius; dy <= radius; dy++)
        {
            int w = (int)Math.Sqrt(radius * radius - dy * dy);
            FillRect(cx - w, cy + dy, w * 2, 1, color);
        }
    }

    /// <summary>Умножение цвета (яркость/затемнение) без изменения размера.</summary>
    public void Multiply(float factor)
    {
        factor = Math.Clamp(factor, 0f, 1f);
        for (int i = 0; i < Pixels.Length; i++)
        {
            var c = Pixels[i];
            Pixels[i] = new Color(
                (int)(c.R * factor),
                (int)(c.G * factor),
                (int)(c.B * factor),
                (int)(c.A * factor));
        }
    }

    private static Color Premultiply(Color c)
    {
        if (c.A == 255 || c.A == 0) return c;
        return new Color(
            (byte)(c.R * c.A / 255),
            (byte)(c.G * c.A / 255),
            (byte)(c.B * c.A / 255),
            c.A);
    }

    /// <summary>Source-over для premultiplied alpha.</summary>
    private static Color Blend(Color src, Color dst)
    {
        if (src.A == 255) return src;
        if (src.A == 0) return dst;

        float inv = 1f - src.A / 255f;
        return new Color(
            (byte)Math.Min(255, src.R + dst.R * inv),
            (byte)Math.Min(255, src.G + dst.G * inv),
            (byte)Math.Min(255, src.B + dst.B * inv),
            (byte)Math.Min(255, src.A + dst.A * inv));
    }
}
