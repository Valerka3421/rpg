using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using StbTrueType = StbTrueTypeSharp.StbTrueType;

namespace Aethelgard.Render;

/// <summary>
/// Runtime font generator — creates SpriteFont texture at runtime via
/// stb_truetype (StbTrueTypeSharp, MIT). Полностью кроссплатформенно:
/// Windows / Linux / macOS, без System.Drawing и без MGCB content pipeline
/// (MGCB падает на путях с кириллицей).
///
/// Шрифт берётся из Content/fonts (DejaVu Sans идёт в комплекте, свободная
/// лицензия), с фолбэком на системные шрифты.
///
/// Набор символов: ASCII 32–126 + кириллица (А–я, Ё, ё) + типографика
/// («» — – … · № • ↑↓←→ × §).
/// </summary>
public static class RuntimeFont
{
    /// <summary>Все символы, доступные в шрифте (в порядке сетки).</summary>
    public static readonly List<char> CharsetQuery = BuildCharset();

    private static byte[]? _cachedFontData;
    private static byte[]? _cachedFontDataBold;

    private static List<char> BuildCharset()
    {
        var chars = new List<char>();
        // ASCII printable
        for (int i = 32; i <= 126; i++) chars.Add((char)i);
        // Кириллица: Ё/ё + А-Я + а-я
        chars.Add('Ё'); chars.Add('ё');
        for (int i = 0x410; i <= 0x44F; i++) chars.Add((char)i);
        // Типографика и служебные знаки UI
        chars.AddRange(new[] { '«', '»', '—', '–', '…', '·', '№', '•',
            '↑', '↓', '←', '→', '×', '§', '’' });
        // SpriteFont требует строго возрастающий порядок кодов
        return chars.Distinct().OrderBy(c => (int)c).ToList();
    }

    /// <summary>
    /// Найти TTF-файл шрифта: сначала в комплекте (Content/fonts),
    /// затем системные пути. Возвращает null, если ничего не найдено.
    /// </summary>
    private static string? LocateFontFile(bool bold)
    {
        string[] bundled = bold
            ? new[] { "Content/fonts/DejaVuSans-Bold.ttf", "Content/fonts/DejaVuSans.ttf" }
            : new[] { "Content/fonts/DejaVuSans.ttf" };
        foreach (var p in bundled)
            if (File.Exists(p)) return Path.GetFullPath(p);

        string[] candidates = bold
            ? new[] {
                "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
                "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf",
                "C:/Windows/Fonts/arialbd.ttf", "C:/Windows/Fonts/arial.ttf" }
            : new[] {
                "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
                "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
                "C:/Windows/Fonts/arial.ttf" };
        return candidates.FirstOrDefault(File.Exists);
    }

    private static byte[] GetFontData(bool bold)
    {
        if (bold && _cachedFontDataBold != null) return _cachedFontDataBold;
        if (!bold && _cachedFontData != null) return _cachedFontData;
        string? path = LocateFontFile(bold) ?? LocateFontFile(!bold)
            ?? throw new FileNotFoundException("Не найден ни один TTF-шрифт для RuntimeFont");
        var bytes = File.ReadAllBytes(path);
        if (bold) _cachedFontDataBold = bytes; else _cachedFontData = bytes;
        return bytes;
    }

    /// <summary>
    /// Create a SpriteFont at runtime. Все глифы растеризуются stb_truetype
    /// на общий базлайн — ровная строка без «прыжков» по вертикали.
    /// </summary>
    public static unsafe SpriteFont? Create(GraphicsDevice device, int fontSize = 14,
        bool bold = false)
    {
        try
        {
            var data = GetFontData(bold);
            var pixels = new Color[1]; // убедимся, что тип доступен до fixed

            fixed (byte* pData = data)
            {
                var info = new StbTrueType.stbtt_fontinfo();
                if (StbTrueType.stbtt_InitFont(info, pData, 0) == 0)
                    return null;

                int charCount = CharsetQuery.Count;

                // Grid layout. cellW/cellH включают запас: wide-глифы («—», «Ш»)
                // и пустые поля 2px по краям ячейки, чтобы линейный сэмплер
                // не «подтягивал» края соседних глифов (эффект призрачного текста).
                const int pad = 3;
                int cols = 16;
                int rows = (charCount + cols - 1) / cols;
                int cellW = fontSize + pad * 2;
                int cellH = fontSize + pad * 2 + 6; // доп. запас по вертикали: выносные элементы («Й», «Ц», скобки)
                int texW = cols * cellW;
                int texH = rows * cellH;

                float scale = StbTrueType.stbtt_ScaleForPixelHeight(info, fontSize);
                int ascent0 = 0, descent0 = 0, lineGap0 = 0;
                StbTrueType.stbtt_GetFontVMetrics(info, &ascent0, &descent0, &lineGap0);
                float ascent = ascent0 * scale;      // от базлайна вверх
                float descent = descent0 * scale;    // отрицательное
                float lineH = ascent - descent;

                var texPixels = new Color[texW * texH];
                var glyphBounds = new List<Rectangle>();
                var cropping = new List<Rectangle>();
                var kerning = new List<Vector3>();

                for (int i = 0; i < charCount; i++)
                {
                    char c = CharsetQuery[i];
                    int col = i % cols;
                    int row = i / cols;
                    int cellX = col * cellW;
                    int cellY = row * cellH;

                    // Растеризуем глиф
                    int gw = 0, gh = 0;
                    int xoff = 0, yoff = 0;
                    byte* bmpPtr = StbTrueType.stbtt_GetCodepointBitmap(
                        info, scale, scale, c, &gw, &gh, &xoff, &yoff);
                    var bmp = new byte[gw * gh];
                    if (gw > 0 && gh > 0)
                        Marshal.Copy((IntPtr)bmpPtr, bmp, 0, gw * gh);
                    StbTrueType.stbtt_FreeBitmap(bmpPtr, null);

                    // Базлайн общий для всех глифов — центрируем строку в ячейке
                    float baselineY = cellY + (cellH - lineH) / 2f + ascent;

                    // Позиция битмапа относительно базлайна
                    int ix0 = 0, iy0 = 0, ix1 = 0, iy1 = 0;
                    StbTrueType.stbtt_GetCodepointBitmapBox(
                        info, c, scale, scale,
                        &ix0, &iy0, &ix1, &iy1);
                    int destX = cellX + ix0;
                    int destY = (int)MathF.Round(baselineY + iy0);

                    if (gw > 0 && gh > 0)
                    {
                        // Клампим внутрь ячейки с полем pad, чтобы широкие глифы
                        // не вылезли к соседям (иначе — призрачные артефакты).
                        int loX = cellX + Math.Min(pad, Math.Max(0, (cellW - gw) / 2));
                        int hiX = cellX + cellW - gw - Math.Min(pad, Math.Max(0, (cellW - gw) / 2));
                        destX = hiX >= loX ? Math.Clamp(destX, loX, hiX)
                                       : cellX + (cellW - gw) / 2;
                        int loY = cellY + Math.Min(pad, Math.Max(0, (cellH - gh) / 2));
                        int hiY = cellY + cellH - gh - Math.Min(pad, Math.Max(0, (cellH - gh) / 2));
                        destY = hiY >= loY ? Math.Clamp(destY, loY, hiY)
                                       : cellY + (cellH - gh) / 2;
                        for (int y = 0; y < gh; y++)
                        {
                            int dy = destY + y;
                            if (dy < 0 || dy >= texH) continue;
                            for (int x = 0; x < gw; x++)
                            {
                                int dx = destX + x;
                                if (dx < 0 || dx >= texW) continue;
                                byte a = bmp[y * gw + x];
                                if (a > 0)
                                    texPixels[dy * texW + dx] = new Color(255, 255, 255, (int)a);
                            }
                        }
                    }

                    // Advance из метрик шрифта — точная типографика
                    int advW = 0, lsb = 0;
                    StbTrueType.stbtt_GetCodepointHMetrics(info, c, &advW, &lsb);
                    float advance = Math.Max(advW * scale + 1f, fontSize * 0.25f);

                    // Квадрат: реальный прямоугольник пикселей глифа в ячейке.
                    // Dest = перо + Cropping; источник = BoundsInTexture, поэтому
                    // Cropping = смещение пикселей внутри ячейки (как при полной ячейке).
                    int gx = Math.Max(0, destX - cellX);
                    int gy = Math.Max(0, destY - cellY);
                    int gr = Math.Min(cellW, destX - cellX + gw);
                    int gb = Math.Min(cellH, destY - cellY + gh);
                    int qW = Math.Max(1, gr - gx);
                    int qH = Math.Max(1, gb - gy);
                    glyphBounds.Add(new Rectangle(cellX + gx, cellY + gy, qW, qH));
                    cropping.Add(new Rectangle(gx, gy, qW, qH));
                    kerning.Add(new Vector3(0, Math.Min(advance, cellW), 0));
                }

                var texture = new Texture2D(device, texW, texH, false, SurfaceFormat.Color);
                texture.SetData(texPixels);

                // Build SpriteFont from texture + glyph data
                var spriteFont = new SpriteFont(
                    texture,
                    glyphBounds,
                    cropping,
                    CharsetQuery,
                    (int)MathF.Round(lineH) + 1,  // lineSpacing — реальная высота строки
                    0f,          // spacing
                    kerning,     // kerning (List<Vector3>)
                    '?'          // defaultCharacter (char?)
                );

                return spriteFont;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RuntimeFont] FAIL size={fontSize}: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }
}
