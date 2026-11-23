using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
namespace Aethelgard.Render;

/// <summary>
/// Class selection screen — pick from 4 starter classes.
/// Ported from Python render/screens_renderer.py class_select mode.
///
/// Left column — vertical list of 4 classes; right column — full info
/// panel of the selected class (icon, description, perk, stats, equipment).
/// Mouse hover / click, arrow keys or 1-4 to select, Enter or "Начать" to start.
/// </summary>
public class ClassSelectScreen
{
    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _sb;
    private SpriteFont? _font;
    private Texture2D? _blank;

    private static readonly string[] StarterClasses = { "warrior", "mage", "rogue", "artisan" };
    public int Selected = 0;

    // Hover state (для подсветки карточки под курсором)
    private int _hover = -1;

    public ClassSelectScreen(GraphicsDevice device, SpriteBatch sb)
    {
        _device = device;
        _sb = sb;
    }

    public void LoadFont(SpriteFont font) => _font = font;

    /// <summary>
    /// Draw the class selection screen.
    /// Returns the selected class key when user presses Enter, or null.
    /// </summary>
    public string? Draw(KeyboardState keys, KeyboardState prevKeys, MouseState mouse)
    {
        int sw = AethelgardGame.ScreenW;
        int sh = AethelgardGame.ScreenH;

        if (_font == null) return null;

        // Background — мягкий градиент из вертикальных полос
        for (int y = 0; y < sh; y += 4)
        {
            float t = y / (float)sh;
            var c = new Color(
                (int)(14 + 8 * t), (int)(13 + 8 * t), (int)(20 + 10 * t));
            _sb.Draw(Blank, new Rectangle(0, y, sw, 4), c);
        }

        // Title
        string title = "ВЫБОР КЛАССА";
        var titleSize = _font.MeasureString(title);
        _sb.DrawString(_font, title,
            new Vector2((sw - titleSize.X) / 2 + 2, 46 + 2), new Color(0, 0, 0, 160));
        _sb.DrawString(_font, title,
            new Vector2((sw - titleSize.X) / 2, 46), Config.ColGold);

        // Subtitle / hint
        string hint = "↑ ↓ или 1–4 — выбрать · Enter — начать · Esc — назад";
        var hintSize = _font.MeasureString(hint);
        _sb.DrawString(_font, hint,
            new Vector2((sw - hintSize.X) / 2, 84), Config.ColDim);

        // ── Layout: list on the left, details on the right ──
        int margin = 60;
        int top = 130;
        int bottom = sh - 110;   // место для кнопки «Начать»
        int panelH = bottom - top;
        int listW = 300;
        int listX = margin;
        int detailX = listX + listW + 24;
        int detailW = sw - detailX - margin;
        int rowH = (panelH - (StarterClasses.Length - 1) * 12) / StarterClasses.Length;

        // Keyboard navigation: up/down + digits
        bool upPress = (keys.IsKeyDown(Keys.Up) && !prevKeys.IsKeyDown(Keys.Up))
            || (keys.IsKeyDown(Keys.W) && !prevKeys.IsKeyDown(Keys.W));
        bool downPress = (keys.IsKeyDown(Keys.Down) && !prevKeys.IsKeyDown(Keys.Down))
            || (keys.IsKeyDown(Keys.S) && !prevKeys.IsKeyDown(Keys.S));
        if (upPress) Selected = (Selected + StarterClasses.Length - 1) % StarterClasses.Length;
        if (downPress) Selected = (Selected + 1) % StarterClasses.Length;
        for (int i = 0; i < StarterClasses.Length; i++)
        {
            var key = Keys.D1 + i;
            if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key))
                Selected = i;
        }

        // ── Left: list of classes ──
        _hover = -1;
        for (int i = 0; i < StarterClasses.Length; i++)
        {
            var def = ClassData.Get(StarterClasses[i]);
            if (def == null) continue;

            int cy = top + i * (rowH + 12);
            bool isSelected = i == Selected;

            // Hover detection + click to select
            bool isHover = mouse.X >= listX && mouse.X < listX + listW
                && mouse.Y >= cy && mouse.Y < cy + rowH;
            if (isHover)
            {
                _hover = i;
                if (mouse.LeftButton == ButtonState.Pressed)
                    Selected = i;
            }

            // Row shadow + background
            _sb.Draw(Blank, new Rectangle(listX + 4, cy + 5, listW, rowH),
                new Color(0, 0, 0, 110));
            _sb.Draw(Blank, new Rectangle(listX, cy, listW, rowH),
                isSelected ? new Color(44, 38, 28) : new Color(26, 24, 19));

            // Colored left stripe + border
            int bw = isSelected ? 3 : 2;
            var borderColor = isSelected ? def.Color
                : isHover ? new Color(90, 82, 66) : new Color(52, 47, 39);
            _sb.Draw(Blank, new Rectangle(listX, cy, 5, rowH), def.Color);
            _sb.Draw(Blank, new Rectangle(listX, cy, listW, bw), borderColor);
            _sb.Draw(Blank, new Rectangle(listX, cy + rowH - bw, listW, bw), borderColor);
            _sb.Draw(Blank, new Rectangle(listX + listW - bw, cy, bw, rowH), borderColor);

            // Small class icon (circle) with number inside
            int iconCx = listX + 42;
            int iconCy = cy + rowH / 2;
            DrawFilledCircle(iconCx, iconCy + 2, 22, new Color(0, 0, 0, 100));
            DrawFilledCircle(iconCx, iconCy, 22, def.Color);
            DrawFilledCircle(iconCx, iconCy, 17, new Color(
                Math.Min(255, def.Color.R + 30),
                Math.Min(255, def.Color.G + 30),
                Math.Min(255, def.Color.B + 30)));
            string num = (i + 1).ToString();
            var numSize = _font.MeasureString(num);
            _sb.DrawString(_font, num,
                new Vector2(iconCx - numSize.X / 2, iconCy - numSize.Y / 2 - 1),
                new Color(20, 18, 14));

            // Class name + short role line
            bool nameGold = isSelected;
            _sb.DrawString(_font, def.Name,
                new Vector2(listX + 78, cy + rowH / 2 - 24),
                nameGold ? Config.ColGold : Config.ColText);
            string role = def.Tier > 1 ? $"Уровень {def.Tier}" : "Стартовый класс";
            _sb.DrawString(_font, role,
                new Vector2(listX + 78, cy + rowH / 2 + 4), Config.ColDim);

            // Gold marker on the selected row
            if (isSelected)
                _sb.Draw(Blank, new Rectangle(listX + listW - 22, cy + rowH / 2 - 2, 12, 4),
                    Config.ColGold);
        }

        // ── Right: full info of the selected class ──
        var sel = ClassData.Get(StarterClasses[Selected]);
        if (sel != null)
        {
            // Panel shadow + background + border
            _sb.Draw(Blank, new Rectangle(detailX + 6, top + 6, detailW, panelH),
                new Color(0, 0, 0, 120));
            _sb.Draw(Blank, new Rectangle(detailX, top, detailW, panelH),
                new Color(30, 27, 21));
            var sbColor = sel.Color;
            _sb.Draw(Blank, new Rectangle(detailX, top, detailW, 3), sbColor);
            _sb.Draw(Blank, new Rectangle(detailX, top + panelH - 3, detailW, 3), sbColor);
            _sb.Draw(Blank, new Rectangle(detailX, top, 3, panelH), sbColor);
            _sb.Draw(Blank, new Rectangle(detailX + detailW - 3, top, 3, panelH), sbColor);

            int pad = 28;
            int innerW = detailW - pad * 2;
            int y = top + 26;

            // Icon + name + tier on one line
            int iconCx = detailX + pad + 34;
            int iconCy = y + 34;
            DrawFilledCircle(iconCx, iconCy + 3, 34, new Color(0, 0, 0, 110));
            DrawFilledCircle(iconCx, iconCy, 34, sel.Color);
            DrawFilledCircle(iconCx, iconCy, 26, new Color(
                Math.Min(255, sel.Color.R + 30),
                Math.Min(255, sel.Color.G + 30),
                Math.Min(255, sel.Color.B + 30)));

            _sb.DrawString(_font, sel.Name,
                new Vector2(detailX + pad + 86, y), Config.ColGold);
            string tierText = sel.Tier > 1 ? $"Класс {sel.Tier} уровня" : "Стартовый класс";
            _sb.DrawString(_font, tierText,
                new Vector2(detailX + pad + 86, y + 36), Config.ColDim);

            y += 96;
            _sb.Draw(Blank, new Rectangle(detailX + pad, y, innerW, 2),
                new Color(90, 80, 60, 140));
            y += 18;

            // Description (wrapped, full)
            var descLines = WrapText(sel.Desc, innerW, _font);
            foreach (var line in descLines)
            {
                _sb.DrawString(_font, line, new Vector2(detailX + pad, y), Config.ColText);
                y += 26;
            }
            y += 10;

            // Perk
            if (!string.IsNullOrEmpty(sel.Perk))
            {
                _sb.DrawString(_font, "Особенность:",
                    new Vector2(detailX + pad, y), Config.ColGold);
                y += 26;
                var perkLines = WrapText(sel.Perk, innerW, _font);
                foreach (var line in perkLines)
                {
                    _sb.DrawString(_font, line, new Vector2(detailX + pad, y), Config.ColDim);
                    y += 24;
                }
                y += 10;
            }

            // Stats — 2 columns, 3 rows
            _sb.DrawString(_font, "Стартовые статы:",
                new Vector2(detailX + pad, y), Config.ColText);
            y += 28;
            var stats = Aethelgard.Data.ClassData.ClassStats(StarterClasses[Selected]);
            var statEntries = new (string Key, string Label, Color Color)[]
            {
                ("str", "STR — Сила", new Color(210, 110, 90)),
                ("agi", "AGI — Ловкость", new Color(130, 205, 130)),
                ("vit", "VIT — Живучесть", new Color(210, 90, 90)),
                ("luk", "LUK — Удача", new Color(225, 205, 110)),
                ("df_stat", "DEF — Защита", new Color(180, 180, 190)),
                ("int", "INT — Интеллект", new Color(130, 160, 225)),
            };
            int colW = innerW / 2;
            for (int si = 0; si < statEntries.Length; si++)
            {
                var (sKey, sLabel, sColor) = statEntries[si];
                int val = stats.GetValueOrDefault(sKey);
                int sx = detailX + pad + (si % 2) * colW;
                int sy = y + (si / 2) * 26;
                _sb.DrawString(_font, sLabel, new Vector2(sx, sy), sColor);
                _sb.DrawString(_font, val.ToString(),
                    new Vector2(sx + colW - 40, sy), Config.ColText);
            }
            y += 3 * 26 + 12;

            // Equipment summary
            string weapons = sel.Weapons != null && sel.Weapons.Length > 0
                ? string.Join(", ", sel.Weapons) : "—";
            string armor = sel.ArmorTypes != null && sel.ArmorTypes.Length > 0
                ? string.Join(", ", sel.ArmorTypes) : "—";
            int valX = detailX + pad + 110;
            _sb.DrawString(_font, "Оружие:", new Vector2(detailX + pad, y), Config.ColText);
            var wLines = WrapText(weapons, innerW - 110, _font);
            _sb.DrawString(_font, wLines[0],
                new Vector2(valX, y), Config.ColDim);
            y += 26;
            for (int li = 1; li < wLines.Count; li++)
            {
                _sb.DrawString(_font, wLines[li],
                    new Vector2(valX, y), Config.ColDim);
                y += 24;
            }
            _sb.DrawString(_font, "Броня:", new Vector2(detailX + pad, y), Config.ColText);
            _sb.DrawString(_font, armor,
                new Vector2(valX, y), Config.ColDim);
        }

        // Begin button (под левой колонкой)
        int btnW = listW, btnH = 48;
        int btnX = listX;
        int btnY = bottom + 20;

        bool mouseOverBtn = mouse.X >= btnX && mouse.X < btnX + btnW
            && mouse.Y >= btnY && mouse.Y < btnY + btnH;
        bool mouseClick = mouseOverBtn && mouse.LeftButton == ButtonState.Pressed;

        // Тень кнопки
        _sb.Draw(Blank, new Rectangle(btnX + 4, btnY + 5, btnW, btnH),
            new Color(0, 0, 0, 130));
        _sb.Draw(Blank, new Rectangle(btnX, btnY, btnW, btnH),
            mouseOverBtn ? new Color(66, 54, 32) : new Color(38, 34, 27));
        // Рамка кнопки
        var bc = mouseOverBtn ? Config.ColGold : new Color(140, 118, 70);
        _sb.Draw(Blank, new Rectangle(btnX, btnY, btnW, 2), bc);
        _sb.Draw(Blank, new Rectangle(btnX, btnY + btnH - 2, btnW, 2), bc);
        _sb.Draw(Blank, new Rectangle(btnX, btnY, 2, btnH), bc);
        _sb.Draw(Blank, new Rectangle(btnX + btnW - 2, btnY, 2, btnH), bc);

        var selDef = ClassData.Get(StarterClasses[Selected]);
        string btnText = $"НАЧАТЬ — {selDef?.Name ?? ""}";
        var btnSize = _font.MeasureString(btnText);
        _sb.DrawString(_font, btnText,
            new Vector2(btnX + (btnW - btnSize.X) / 2, btnY + 12),
            mouseOverBtn ? Config.ColGold : Config.ColText);

        // Enter or click to confirm
        if ((keys.IsKeyDown(Keys.Enter) && !prevKeys.IsKeyDown(Keys.Enter)) || mouseClick)
        {
            return StarterClasses[Selected];
        }

        return null;
    }

    private List<string> WrapText(string text, int maxW, SpriteFont font)
    {
        var lines = new List<string>();
        var words = text.Split(' ');
        var current = "";

        foreach (var word in words)
        {
            var test = current.Length > 0 ? current + " " + word : word;
            var size = font.MeasureString(test);
            if (size.X > maxW && current.Length > 0)
            {
                lines.Add(current);
                current = word;
            }
            else
            {
                current = test;
            }
        }
        if (current.Length > 0)
            lines.Add(current);

        return lines;
    }

    private void DrawFilledCircle(int cx, int cy, int r, Color color)
    {
        for (int dy = -r; dy <= r; dy++)
        {
            int w = (int)Math.Sqrt(r * r - dy * dy);
            _sb.Draw(Blank, new Rectangle(cx - w, cy + dy, w * 2, 1), color);
        }
    }

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
