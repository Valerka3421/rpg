using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
namespace Aethelgard.Render;

/// <summary>
/// Экран выбора класса — точный порт python render/menu_renderer.draw_class_select:
/// слева компактный список 380×96, справа крупная панель предпросмотра
/// (портрет, производные статы, полоски атрибутов, стартовый набор, кнопка
/// «ИГРАТЬ ЗА …»). Hover меняет выбор, повторный клик/Enter — старт.
/// </summary>
public class ClassSelectScreen
{
    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _sb;
    private SpriteFont? _font;
    private Texture2D? _blank;

    private static readonly string[] StarterClasses = { "warrior", "mage", "rogue", "artisan" };
    public int Selected = 0;

    /// <summary>Пользователь нажал кнопку «Назад» (Esc обрабатывается в Game).</summary>
    public bool BackRequested;

    private static readonly Dictionary<string, string> ClassRoles = new()
    {
        ["warrior"] = "Воин ближнего боя",
        ["mage"] = "Маг стихий",
        ["rogue"] = "Мастер ловкости",
        ["artisan"] = "Мастер ремёсел",
    };

    public ClassSelectScreen(GraphicsDevice device, SpriteBatch sb)
    {
        _device = device;
        _sb = sb;
    }

    public void LoadFont(SpriteFont font) => _font = font;

    private SpriteFont F1 => Fonts.Ready ? Fonts.H1 : _font!;
    private SpriteFont F2 => Fonts.Ready ? Fonts.H2 : _font!;
    private SpriteFont FBody => _font!;
    private SpriteFont FSmall => Fonts.Ready ? Fonts.Small : _font!;
    private SpriteFont FTiny => Fonts.Ready ? Fonts.Tiny : _font!;

    // ── Геометрия (menu_controller.button_rects / class_preview_rect) ──
    private static Rectangle CardRect(int i) => new(70, 118 + i * 110, 380, 96);

    private static Rectangle PreviewRect()
    {
        int sw = AethelgardGame.ScreenW, sh = AethelgardGame.ScreenH;
        const int px = 484, pw = 0, py = 112;
        int width = sw - px - 70;
        int height = sh - py - 96;
        return new Rectangle(px, py, width, height);
    }

    private static Rectangle StartRect()
    {
        var p = PreviewRect();
        int bw = Math.Min(340, Math.Max(220, (int)(p.Width * 0.36)));
        const int bh = 52;
        return new Rectangle(p.X + (p.Width - bw) / 2, p.Y + p.Height - bh - 22, bw, bh);
    }

    private static readonly Rectangle BackRect = new(34, 28, 52, 48);

    /// <summary>Возвращает ключ класса при старте, иначе null.</summary>
    public string? Draw(KeyboardState keys, KeyboardState prevKeys, MouseState mouse)
    {
        BackRequested = false;
        _lastMouseX = mouse.X;
        _lastMouseY = mouse.Y;
        int sw = AethelgardGame.ScreenW, sh = AethelgardGame.ScreenH;
        if (_font == null) return null;

        // Фон: тёплый градиент (10,9,7) → (30,24,18).
        for (int y = 0; y < sh; y++)
        {
            float t = sh <= 1 ? 0 : (float)y / (sh - 1);
            var c = new Color(
                (int)(10 + t * 20), (int)(9 + t * 15), (int)(7 + t * 11));
            _sb.Draw(Blank, new Rectangle(0, y, sw, 1), c);
        }

        // Заголовок и подсказка.
        DrawCentered(F1, "ВЫБОР КЛАССА", 30, new Color(214, 178, 98), shadow: true);
        DrawCentered(FSmall,
            "Наведи или выбери класс — справа показано, как он выглядит и с чем начинает",
            80, new Color(150, 140, 120));

        bool upPress = (keys.IsKeyDown(Keys.Up) && !prevKeys.IsKeyDown(Keys.Up))
            || (keys.IsKeyDown(Keys.W) && !prevKeys.IsKeyDown(Keys.W));
        bool downPress = (keys.IsKeyDown(Keys.Down) && !prevKeys.IsKeyDown(Keys.Down))
            || (keys.IsKeyDown(Keys.S) && !prevKeys.IsKeyDown(Keys.S));
        if (upPress) Selected = (Selected + StarterClasses.Length - 1) % StarterClasses.Length;
        if (downPress) Selected = (Selected + 1) % StarterClasses.Length;

        // Наведение мышью меняет выбор (python handle_mouse_motion).
        bool click = mouse.LeftButton == ButtonState.Pressed;
        for (int i = 0; i < StarterClasses.Length; i++)
        {
            if (CardRect(i).Contains(mouse.X, mouse.Y))
                Selected = i;
        }

        // ── Левая колонка: карточки ──
        for (int i = 0; i < StarterClasses.Length; i++)
        {
            var def = ClassData.Get(StarterClasses[i]);
            if (def == null) continue;
            var rect = CardRect(i);
            bool sel = i == Selected;
            DrawButtonBg(rect, sel);
            DrawClassCard(rect, def, sel, StarterClasses[i]);
            if (sel) DrawSelectionArrow(rect.X - 16, rect.Y + rect.Height / 2);
        }

        // ── Правая колонка: предпросмотр ──
        var selDef = ClassData.Get(StarterClasses[Selected]);
        var start = StartRect();
        bool overStart = start.Contains(mouse.X, mouse.Y);
        if (selDef != null)
            DrawPreview(selDef, StarterClasses[Selected]);

        // Кнопка «Назад».
        bool overBack = BackRect.Contains(mouse.X, mouse.Y);
        DrawButtonBg(BackRect, overBack);
        DrawBackArrow(BackRect, overBack);

        // Нижняя правая подсказка.
        const string hint = "↑/↓ — выбор · Enter — начать · Esc — назад";
        var hs = FSmall.MeasureString(hint);
        _sb.DrawString(FSmall, hint,
            new Vector2(sw - 70 - hs.X, sh - 46), new Color(98, 90, 74));

        // Ввод.
        bool enter = keys.IsKeyDown(Keys.Enter) && !prevKeys.IsKeyDown(Keys.Enter);
        bool backKey = keys.IsKeyDown(Keys.Escape) && !prevKeys.IsKeyDown(Keys.Escape);
        if (backKey || (click && overBack))
        {
            BackRequested = true;
            return null;
        }
        if (enter || (click && overStart))
            return StarterClasses[Selected];
        return null;
    }

    private void DrawClassCard(Rectangle rect, ClassData.Def cls, bool selected,
        string classKey)
    {
        var color = cls.Color;
        var nameColor = selected ? color : new Color(150, 140, 120);
        _sb.DrawString(F2, cls.Name, new Vector2(rect.X + 22, rect.Y + 12), nameColor);

        string tierLabel = $"{cls.Tier}-я ступень";
        var ts = FTiny.MeasureString(tierLabel);
        _sb.DrawString(FTiny, tierLabel,
            new Vector2(rect.Right - 20 - ts.X, rect.Y + 16), new Color(98, 90, 74));

        string desc = FitText(cls.Desc, rect.Width - 44, FSmall);
        _sb.DrawString(FSmall, desc, new Vector2(rect.X + 22, rect.Y + 44),
            selected ? new Color(222, 214, 198) : new Color(98, 90, 74));

        var st = ClassData.ClassStats(classKey);
        string stats = $"STR {st.GetValueOrDefault("str", 5)} · AGI {st.GetValueOrDefault("agi", 5)} · " +
                       $"VIT {st.GetValueOrDefault("vit", 5)} · LUK {st.GetValueOrDefault("luk", 5)}";
        _sb.DrawString(FTiny, stats, new Vector2(rect.X + 22, rect.Y + 66),
            new Color(98, 90, 74));
    }

    private void DrawPreview(ClassData.Def cls, string classKey)
    {
        var p = PreviewRect();
        var color = cls.Color;

        // Панель + рамка цвета класса.
        _sb.Draw(Blank, p, new Color(26, 22, 18));
        DrawBorder(p, new Color((int)color.R, (int)color.G, (int)color.B, 90));

        // Заголовок.
        _sb.DrawString(F1, cls.Name, new Vector2(p.X + 28, p.Y + 18), color);
        string role = ClassRoles.GetValueOrDefault(cls.Base, "Приключенец");
        _sb.DrawString(FSmall, $"{role} · {cls.Tier}-я ступень",
            new Vector2(p.X + 28, p.Y + 58), new Color(150, 140, 120));

        // Описание (2 строки).
        var descLines = WrapText(cls.Desc, p.Width - 56, FSmall);
        int descY = p.Y + 78;
        for (int i = 0; i < descLines.Count && i < 2; i++)
        {
            _sb.DrawString(FSmall, descLines[i], new Vector2(p.X + 28, descY),
                new Color(222, 214, 198));
            descY += 18;
        }

        // Портрет.
        var start = StartRect();
        int limitY = start.Y - 14;
        int portrait = Math.Min(180, Math.Max(140, (int)(p.Height * 0.26)));
        int bx = p.X + 28;
        int by = descY + 8;
        _sb.Draw(Blank, new Rectangle(bx, by, portrait, portrait), new Color(16, 14, 11));
        DrawBorder(new Rectangle(bx, by, portrait, portrait),
            new Color((int)color.R, (int)color.G, (int)color.B, 120));
        DrawPlayerPortrait(classKey, bx + 4, by + 4, portrait - 8, color);
        var capSize = FTiny.MeasureString("как выглядит в игре");
        _sb.DrawString(FTiny, "как выглядит в игре",
            new Vector2(bx + portrait / 2 - capSize.X / 2, by + portrait + 6),
            new Color(98, 90, 74));

        // Стартовый набор.
        int gearY = by + portrait + 26;
        _sb.DrawString(FSmall, "СТАРТОВЫЙ НАБОР", new Vector2(bx, gearY),
            new Color(206, 166, 82));
        var gear = StartGear(cls);
        int gy = gearY + 24;
        for (int i = 0; i < gear.Count && i < 3; i++)
        {
            _sb.DrawString(FSmall, $"• {gear[i]}", new Vector2(bx, gy),
                new Color(150, 140, 120));
            gy += 17;
        }
        if (gear.Count > 3)
            _sb.DrawString(FSmall, $"• и ещё {gear.Count - 3}", new Vector2(bx, gy),
                new Color(98, 90, 74));

        // Правая колонка — чипы производных статов.
        int sx = bx + portrait + 34;
        var stats = PreviewStats(cls, classKey);
        var chips = new (string Label, string Value, Color Color)[]
        {
            ("ATK", stats.Atk.ToString(), new Color(222, 150, 110)),
            ("DEF", stats.Df.ToString(), new Color(150, 170, 200)),
            ("HP", stats.MaxHp.ToString(), new Color(220, 84, 64)),
            ("MP", stats.MaxMana.ToString(), new Color(120, 160, 255)),
            ("Уклонение", $"{Math.Round(stats.Dodge * 100)}%", new Color(150, 166, 178)),
            ("Крит", $"{Math.Round(stats.Crit * 100)}%", new Color(232, 178, 74)),
        };
        int rightW = p.Right - 28 - sx;
        const int gap = 8;
        int chipW = Math.Max(96, (rightW - gap * 2) / 3);
        const int chipH = 48;
        for (int i = 0; i < chips.Length; i++)
        {
            int col = i % 3, row = i / 3;
            var cr = new Rectangle(sx + col * (chipW + gap), by + row * (chipH + gap),
                chipW, chipH);
            _sb.Draw(Blank, cr, new Color(34, 29, 23));
            DrawBorder(cr, new Color(70, 60, 46));
            _sb.DrawString(FTiny, chips[i].Label,
                new Vector2(cr.X + 12, cr.Y + 5), new Color(98, 90, 74));
            var vs = F2.MeasureString(chips[i].Value);
            _sb.DrawString(F2, chips[i].Value,
                new Vector2(cr.Right - 12 - vs.X, cr.Y + 18), chips[i].Color);
        }

        // Атрибуты с полосками.
        var st = ClassData.ClassStats(classKey);
        var attrs = new (string Label, int Value, Color Color)[]
        {
            ("STR", st.GetValueOrDefault("str", 5), new Color(220, 120, 100)),
            ("AGI", st.GetValueOrDefault("agi", 5), new Color(140, 200, 130)),
            ("VIT", st.GetValueOrDefault("vit", 5), new Color(220, 120, 110)),
            ("LUK", st.GetValueOrDefault("luk", 5), new Color(220, 200, 110)),
            ("DEF", st.GetValueOrDefault("df_stat", 5), new Color(150, 170, 210)),
            ("INT", st.GetValueOrDefault("int", 5), new Color(130, 170, 240)),
        };
        int ay = by + 2 * (chipH + gap) + 8;
        _sb.DrawString(FSmall, "БАЗОВЫЕ ХАРАКТЕРИСТИКИ", new Vector2(sx, ay),
            new Color(206, 166, 82));
        ay += 22;
        const int maxVal = 12;
        foreach (var (label, value, barColor) in attrs)
        {
            _sb.DrawString(FSmall, label, new Vector2(sx, ay - 1),
                new Color(222, 214, 198));
            var track = new Rectangle(sx + 52, ay + 4, Math.Max(24, rightW - 80), 10);
            DrawBar(track, Math.Min(1f, value / (float)maxVal), barColor,
                new Color(20, 18, 15));
            var vs = FSmall.MeasureString(value.ToString());
            _sb.DrawString(FSmall, value.ToString(),
                new Vector2(p.Right - 28 - vs.X, ay - 1), barColor);
            ay += 20;
        }

        // Перк.
        string perk = string.IsNullOrEmpty(cls.Perk) ? cls.Desc : cls.Perk;
        _sb.DrawString(FSmall, "ОСОБЕННОСТЬ", new Vector2(sx, ay + 6),
            new Color(206, 166, 82));
        int ty = ay + 28;
        foreach (var line in WrapText(perk, rightW, FSmall))
        {
            if (ty > limitY) break;
            _sb.DrawString(FSmall, line, new Vector2(sx, ty),
                new Color(222, 214, 198));
            ty += 19;
        }

        // Кнопка «ИГРАТЬ ЗА …».
        bool hover = start.Contains((int)_lastMouseX, (int)_lastMouseY);
        var bg = Shade(color, hover ? 1.15f : 0.92f);
        _sb.Draw(Blank, start, bg);
        DrawBorder(start, Shade(color, 1.4f));
        int sum = color.R + color.G + color.B;
        var labelColor = sum > 420 ? new Color(20, 16, 12) : new Color(250, 246, 238);
        string labelText = FitText($"ИГРАТЬ ЗА {cls.Name.ToUpperInvariant()}",
            start.Width - 20, F2);
        var ls = F2.MeasureString(labelText);
        _sb.DrawString(F2, labelText,
            new Vector2(start.X + (start.Width - ls.X) / 2,
                start.Y + (start.Height - ls.Y) / 2), labelColor);
    }

    private float _lastMouseX = -1, _lastMouseY = -1;

    private static Player.StatResult PreviewStats(ClassData.Def cls, string classKey)
    {
        var st = ClassData.ClassStats(classKey);
        return Player.ComputeStats(
            st.GetValueOrDefault("str", 5), st.GetValueOrDefault("agi", 5),
            st.GetValueOrDefault("vit", 5), st.GetValueOrDefault("luk", 5),
            st.GetValueOrDefault("df_stat", 5), st.GetValueOrDefault("int", 5),
            1, cls.StartWeapon, cls.StartArmor, null, null, null,
            cls.Effects ?? new Dictionary<string, double>());
    }

    private static List<string> StartGear(ClassData.Def cls)
    {
        var gear = new List<string>();
        if (!string.IsNullOrEmpty(cls.StartWeapon)
            && ItemData.Get(cls.StartWeapon) is { } w)
            gear.Add(w.Name);
        if (!string.IsNullOrEmpty(cls.StartArmor)
            && ItemData.Get(cls.StartArmor) is { } a)
            gear.Add(a.Name);
        if (cls.StartItems != null)
        {
            foreach (var (key, count) in cls.StartItems)
            {
                if (ItemData.Get(key) is { } it)
                    gear.Add($"{count}× {it.Name}");
            }
        }
        if (gear.Count == 0) gear.Add("Пустые руки — всё впереди");
        return gear;
    }

    // ── Рисование примитивов ──

    private void DrawButtonBg(Rectangle rect, bool selected)
    {
        _sb.Draw(Blank, new Rectangle(rect.X + 6, rect.Y + 8, rect.Width, rect.Height),
            new Color(0, 0, 0, 110));
        var top = selected ? new Color(96, 76, 44) : new Color(44, 39, 31);
        var bot = selected ? new Color(56, 44, 25) : new Color(24, 21, 17);
        for (int i = 0; i < rect.Height; i++)
        {
            float t = rect.Height <= 1 ? 0 : (float)i / (rect.Height - 1);
            _sb.Draw(Blank, new Rectangle(rect.X, rect.Y + i, rect.Width, 1),
                new Color((int)(top.R * (1 - t) + bot.R * t),
                          (int)(top.G * (1 - t) + bot.G * t),
                          (int)(top.B * (1 - t) + bot.B * t)));
        }
        var border = selected ? new Color(206, 166, 82) : new Color(72, 62, 46);
        DrawBorder(rect, border);
        if (selected)
        {
            _sb.Draw(Blank, new Rectangle(rect.X + 10, rect.Y + 1, rect.Width - 20, 1),
                new Color(236, 206, 142));
            _sb.Draw(Blank, new Rectangle(rect.X + 2, rect.Y + 2,
                rect.Width - 4, rect.Height - 4), new Color(206, 166, 82, 24));
        }
    }

    private void DrawBorder(Rectangle r, Color c)
    {
        _sb.Draw(Blank, new Rectangle(r.X, r.Y, r.Width, 2), c);
        _sb.Draw(Blank, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), c);
        _sb.Draw(Blank, new Rectangle(r.X, r.Y, 2, r.Height), c);
        _sb.Draw(Blank, new Rectangle(r.Right - 2, r.Y, 2, r.Height), c);
    }

    private void DrawSelectionArrow(int x, int y)
    {
        var color = new Color(206, 166, 82);
        for (int i = 0; i < 12; i++)
        {
            int h = (int)Math.Round(14.0 * (12 - i) / 12.0);
            if (h <= 0) continue;
            _sb.Draw(Blank, new Rectangle(x + i, y - h / 2, 1, h), color);
        }
    }

    private void DrawBackArrow(Rectangle rect, bool hover)
    {
        var color = hover ? new Color(238, 222, 188) : new Color(200, 190, 170);
        int cx = rect.X + rect.Width / 2, cy = rect.Y + rect.Height / 2;
        int shaft = Math.Max(8, Math.Min(rect.Width, rect.Height) / 4);
        int head = Math.Max(6, Math.Min(rect.Width, rect.Height) / 5);
        int tip = cx - shaft - head / 2;
        _sb.Draw(Blank, new Rectangle(tip + head / 2, cy - 1, shaft * 2, 3), color);
        for (int i = 0; i < head; i++)
        {
            int h = (int)Math.Round(2.0 * i);
            if (h <= 0) continue;
            _sb.Draw(Blank, new Rectangle(tip + i, cy - h, 1, h * 2), color);
        }
    }

    private void DrawBar(Rectangle track, float value, Color color, Color bg)
    {
        _sb.Draw(Blank, track, bg);
        int w = (int)(track.Width * Math.Clamp(value, 0f, 1f));
        if (w > 0)
            _sb.Draw(Blank, new Rectangle(track.X, track.Y, w, track.Height), color);
    }

    /// <summary>Простой силуэт персонажа в цвете класса.</summary>
    private void DrawPlayerPortrait(string classKey, int x, int y, int size, Color color)
    {
        int cx = x + size / 2;
        int headR = size / 6;
        int headY = y + size / 5;
        DrawFilledCircle(cx, headY, headR, Shade(color, 1.15f));
        // Торс
        int bodyW = size / 3;
        int bodyTop = headY + headR + 2;
        int bodyH = (int)(size * 0.34);
        _sb.Draw(Blank, new Rectangle(cx - bodyW / 2, bodyTop, bodyW, bodyH),
            Shade(color, 0.9f));
        // Руки
        _sb.Draw(Blank, new Rectangle(cx - bodyW / 2 - size / 12, bodyTop + 2,
            size / 12, bodyH - 4), Shade(color, 0.75f));
        _sb.Draw(Blank, new Rectangle(cx + bodyW / 2, bodyTop + 2,
            size / 12, bodyH - 4), Shade(color, 0.75f));
        // Ноги
        int legsTop = bodyTop + bodyH;
        int legsH = (int)(size * 0.28);
        _sb.Draw(Blank, new Rectangle(cx - bodyW / 3, legsTop, bodyW / 4, legsH),
            Shade(color, 0.6f));
        _sb.Draw(Blank, new Rectangle(cx + bodyW / 12, legsTop, bodyW / 4, legsH),
            Shade(color, 0.6f));
    }

    private void DrawFilledCircle(int cx, int cy, int r, Color color)
    {
        for (int dy = -r; dy <= r; dy++)
        {
            int w = (int)Math.Sqrt(r * r - dy * dy);
            _sb.Draw(Blank, new Rectangle(cx - w, cy + dy, w * 2, 1), color);
        }
    }

    private void DrawCentered(SpriteFont font, string text, int y, Color color,
        bool shadow = false)
    {
        int sw = AethelgardGame.ScreenW;
        var size = font.MeasureString(text);
        if (shadow)
            _sb.DrawString(font, text,
                new Vector2((sw - size.X) / 2 + 2, y + 2), new Color(0, 0, 0, 160));
        _sb.DrawString(font, text, new Vector2((sw - size.X) / 2, y), color);
    }

    // ── Текст ──

    private static string FitText(string text, int maxW, SpriteFont font)
    {
        if (font.MeasureString(text).X <= maxW) return text;
        string cut = text.TrimEnd();
        while (cut.Length > 0 && font.MeasureString(cut + "…").X > maxW)
            cut = cut[..^1].TrimEnd();
        return cut + "…";
    }

    private static List<string> WrapText(string text, int maxW, SpriteFont font)
    {
        var lines = new List<string>();
        var words = (text ?? "").Split(' ');
        var current = "";
        foreach (var word in words)
        {
            var test = current.Length > 0 ? current + " " + word : word;
            if (font.MeasureString(test).X > maxW && current.Length > 0)
            {
                lines.Add(current);
                current = word;
            }
            else
            {
                current = test;
            }
        }
        if (current.Length > 0) lines.Add(current);
        return lines;
    }

    private static Color Shade(Color c, float k) => new(
        Math.Clamp((int)(c.R * k), 0, 255),
        Math.Clamp((int)(c.G * k), 0, 255),
        Math.Clamp((int)(c.B * k), 0, 255));

    private Texture2D Blank
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
}
