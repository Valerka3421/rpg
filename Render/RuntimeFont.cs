using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace Aethelgard.Render;

/// <summary>
/// Runtime font generator — creates SpriteFont texture via System.Drawing.
/// Replaces MonoGame Content Pipeline (MGCB) which fails on paths with
/// Cyrillic characters or special symbols.
///
/// Generates a 256×256 texture with ASCII chars 32-126 in a grid,
/// then wraps it as a SpriteFont for MonoGame rendering.
/// </summary>
public static class RuntimeFont
{
    /// <summary>
    /// Create a SpriteFont from System.Drawing at runtime.
    /// Uses Arial or fallback font installed on the system.
    /// </summary>
    public static SpriteFont? Create(GraphicsDevice device, int fontSize = 14)
    {
        try
        {
            // Character range: ASCII 32 (space) to 126 (~)
            int charStart = 32;
            int charEnd = 126;
            int charCount = charEnd - charStart + 1;

            // Grid layout: 16 chars per row
            int cols = 16;
            int rows = (charCount + cols - 1) / cols;
            int cellW = fontSize + 4;
            int cellH = fontSize + 6;
            int texW = cols * cellW;
            int texH = rows * cellH;

            // Create bitmap with System.Drawing
            using var bmp = new Bitmap(texW, texH, PixelFormat.Format32bppArgb);
            using var g = System.Drawing.Graphics.FromImage(bmp);
            g.Clear(System.Drawing.Color.Transparent);
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            // Try Arial, fallback to generic sans-serif
            var fontNames = new[] { "Arial", "Segoe UI", "DejaVu Sans",
                "Liberation Sans", "Microsoft Sans Serif", "Verdana" };
            System.Drawing.Font? font = null;
            foreach (var name in fontNames)
            {
                try
                {
                    font = new System.Drawing.Font(name, fontSize, FontStyle.Regular);
                    break;
                }
                catch { }
            }
            if (font == null)
                font = new System.Drawing.Font(FontFamily.GenericSansSerif, fontSize);

            // Draw each character into its cell
            var glyphBounds = new List<Rectangle>();
            var cropping = new List<Rectangle>();
            var kerning = new List<Vector3>();

            var brush = new SolidBrush(System.Drawing.Color.White);
            var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };

            for (int i = 0; i < charCount; i++)
            {
                char c = (char)(charStart + i);
                int col = i % cols;
                int row = i / cols;
                int x = col * cellW;
                int y = row * cellH;

                // Draw character centered in cell
                var rect = new System.Drawing.RectangleF(x, y, cellW, cellH);
                g.DrawString(c.ToString(), font, brush, rect, format);

                // Glyph bounds in texture
                glyphBounds.Add(new Rectangle(x, y, cellW, cellH));
                cropping.Add(new Rectangle(0, 0, cellW, cellH));

                // Simple kerning: advance by cell width
                float advance = cellW;
                kerning.Add(new Vector3(0, advance, 0));
            }

            // Convert Bitmap to Texture2D
            var data = new uint[texW * texH];
            var bmpData = bmp.LockBits(
                new System.Drawing.Rectangle(0, 0, texW, texH),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            System.Runtime.InteropServices.Marshal.Copy(bmpData.Scan0,
                (int[])(object)data, 0, texW * texH);
            bmp.UnlockBits(bmpData);

            // Convert BGRA → RGBA for MonoGame
            var colors = new Color[texW * texH];
            for (int i = 0; i < data.Length; i++)
            {
                uint pixel = data[i];
                byte b = (byte)(pixel & 0xFF);
                byte grn = (byte)((pixel >> 8) & 0xFF);
                byte r = (byte)((pixel >> 16) & 0xFF);
                byte a = (byte)((pixel >> 24) & 0xFF);
                colors[i] = new Color(r, grn, b, a);
            }

            var texture = new Texture2D(device, texW, texH, false, SurfaceFormat.Color);
            texture.SetData(colors);

            // Build SpriteFont from texture + glyph data
            // MonoGame SpriteFont constructor:
            // (Texture2D texture, List<Rectangle> glyphBounds, List<Rectangle> cropping,
            //  List<char> characters, int lineSpacing, float spacing,
            //  char? defaultCharacter, bool useKerning)
            var characters = new List<char>();
            for (int i = charStart; i <= charEnd; i++)
                characters.Add((char)i);

            var spriteFont = new SpriteFont(
                texture,
                glyphBounds,
                cropping,
                characters,
                cellH,       // lineSpacing
                0f,          // spacing
                kerning,     // kerning (List<Vector3>)
                '?'          // defaultCharacter (char?)
            );

            return spriteFont;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
