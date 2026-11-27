using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Aethelgard.Render;

/// <summary>
/// Общие UI-хелперы. Текст в кнопках/вкладках центрируется по горизонтали
/// и вертикали — единообразно во всех экранах.
/// </summary>
/// <summary>
/// Общий набор шрифтов интерфейса — паритет с python render/theme.py:
/// H1 = 40 bold, H2 = 22 bold, BODY = 18, SMALL = 14, TINY = 12.
/// Заполняется в AethelgardGame.LoadContent.
/// </summary>
public static class Fonts
{
    public static SpriteFont H1 = null!;
    public static SpriteFont H2 = null!;
    public static SpriteFont Body = null!;
    public static SpriteFont Small = null!;
    public static SpriteFont Tiny = null!;

    public static bool Ready => H1 != null && H2 != null && Body != null;
}

public static class Ui
{
    /// <summary>Нарисовать текст по центру прямоугольника.</summary>
    public static void TextCentered(SpriteBatch sb, SpriteFont font, string text,
        Rectangle rect, Color color, bool shadow = true)
    {
        var size = font.MeasureString(text);
        var pos = new Vector2(
            rect.X + (rect.Width - size.X) / 2f,
            rect.Y + (rect.Height - size.Y) / 2f);
        if (shadow)
            sb.DrawString(font, text, pos + new Vector2(1, 1), new Color(0, 0, 0, 160));
        sb.DrawString(font, text, pos, color);
    }

    /// <summary>Кэш PNG-иконок навыков (assets/skills/...).</summary>
    private static readonly Dictionary<string, Texture2D?> Cache = new();

    /// <summary>
    /// Очистка кэша при DeviceReset (вызывается из хука SpriteStore) —
    /// инвалидированные текстуры рисуются неправильно, кэш пересоберётся.
    /// </summary>
    internal static void ClearCache()
    {
        foreach (var tex in Cache.Values)
        {
            try { tex?.Dispose(); } catch { }
        }
        Cache.Clear();
    }

    public static Texture2D? SkillIcon(GraphicsDevice device, string subDir, string name)
    {
        string key = subDir + "/" + name;
        if (Cache.TryGetValue(key, out var cached)) return cached;

        Texture2D? tex = null;
        try
        {
            string path = Path.Combine("assets", "skills", subDir, name + ".png");
            if (File.Exists(path))
            {
                using var fs = File.OpenRead(path);
                tex = Texture2D.FromStream(device, fs);
            }
        }
        catch { tex = null; }
        Cache[key] = tex;
        return tex;
    }
}
