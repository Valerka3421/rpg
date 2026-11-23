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
/// Shows 4 class cards with name, description, starting stats, and color.
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
        string hint = "Стрелки ← → или 1–4 — выбрать · Enter — начать · Esc — назад";
        var hintSize = _font.MeasureString(hint);
        _sb.DrawString(_font, hint,
            new Vector2((sw - hintSize.X) / 2, 84), Config.ColDim);

        // Class cards
        int cardW = 260, cardH = 400;
        int gap = 24;
        int totalW = StarterClasses.Length * cardW + (StarterClasses.Length - 1) * gap;
        int startX = (sw - totalW) / 2;
        int cardY = 130;

        // Keyboard navigation: arrows + digits
        bool leftPress = keys.IsKeyDown(Keys.Left) && !prevKeys.IsKeyDown(Keys.Left);
        bool rightPress = keys.IsKeyDown(Keys.Right) && !prevKeys.IsKeyDown(Keys.Right);
        if (leftPress) Selected = (Selected + StarterClasses.Length - 1) % StarterClasses.Length;
        if (rightPress) Selected = (Selected + 1) % StarterClasses.Length;
        for (int i = 0; i < StarterClasses.Length; i++)
        {
            var key = Keys.D1 + i;
            if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key))
                Selected = i;
        }

        _hover = -1;
        for (int i = 0; i < StarterClasses.Length; i++)
        {
            var def = ClassData.Get(StarterClasses[i]);
            if (def == null) continue;

            int cx = startX + i * (cardW + gap);
            int cy = cardY;
            bool isSelected = i == Selected;

            // Hover detection
            bool isHover = mouse.X >= cx && mouse.X < cx + cardW
                && mouse.Y >= cy && mouse.Y < cy + cardH;
            if (isHover)
            {
                _hover = i;
                if (mouse.LeftButton == ButtonState.Pressed)
                    Selected = i;
            }

            // Selected card слегка «приподнимается»
            if (isSelected) cy -= 6;

            // Тень под карточкой
            _sb.Draw(Blank, new Rectangle(cx + 6, cy + 8, cardW, cardH),
                new Color(0, 0, 0, 120));

            // Card background
            var cardColor = isSelected ? new Color(44, 38, 28) : new Color(26, 24, 19);
            _sb.Draw(Blank, new Rectangle(cx, cy, cardW, cardH), cardColor);

            // Цветная шапка карточки
            _sb.Draw(Blank, new Rectangle(cx, cy, cardW, 6), def.Color);

            // Card border — толще и ярче у выбранной
            var borderColor = isSelected ? def.Color
                : isHover ? new Color(90, 82, 66) : new Color(52, 47, 39);
            int bw = isSelected ? 4 : 2;
            _sb.Draw(Blank, new Rectangle(cx, cy, cardW, bw), borderColor);
            _sb.Draw(Blank, new Rectangle(cx, cy + cardH - bw, cardW, bw), borderColor);
            _sb.Draw(Blank, new Rectangle(cx, cy, bw, cardH), borderColor);
            _sb.Draw(Blank, new Rectangle(cx + cardW - bw, cy, bw, cardH), borderColor);

            // Class icon — large circle in class color (+ тёмная подложка)
            int iconCx = cx + cardW / 2;
            int iconCy = cy + 72;
            DrawFilledCircle(iconCx, iconCy + 3, 34, new Color(0, 0, 0, 110));
            DrawFilledCircle(iconCx, iconCy, 34, def.Color);
            DrawFilledCircle(iconCx, iconCy, 26, new Color(
                Math.Min(255, def.Color.R + 30),
                Math.Min(255, def.Color.G + 30),
                Math.Min(255, def.Color.B + 30)));

            // Class name
            var nameSize = _font.MeasureString(def.Name);
            _sb.DrawString(_font, def.Name,
                new Vector2(iconCx - nameSize.X / 2, cy + 118),
                isSelected ? Config.ColGold : Config.ColText);

            // Number key hint
            _sb.DrawString(_font, $"[{i + 1}]",
                new Vector2(cx + 12, cy + 14), Config.ColDim);

            // Золотой маркер у выбранного класса
            if (isSelected)
                _sb.Draw(Blank, new Rectangle(cx + cardW - 26, cy + 16, 14, 4),
                    Config.ColGold);

            // Description (wrapped)
            int descY = cy + 152;
            var descLines = WrapText(def.Desc, cardW - 24, _font);
            for (int li = 0; li < descLines.Count && li < 3; li++)
            {
                _sb.DrawString(_font, descLines[li], new Vector2(cx + 12, descY),
                    Config.ColDim);
                descY += 26;
            }

            // Starting stats
            descY = Math.Max(descY, cy + 240) + 6;
            _sb.DrawString(_font, "Стартовые бонусы:",
                new Vector2(cx + 12, descY), Config.ColText);
            descY += 26;

            if (def.StrBonus > 0)
            {
                _sb.DrawString(_font, $"+{def.StrBonus} STR", new Vector2(cx + 12, descY),
                    new Color(210, 110, 90));
                descY += 22;
            }
            if (def.AgiBonus > 0)
            {
                _sb.DrawString(_font, $"+{def.AgiBonus} AGI", new Vector2(cx + 12, descY),
                    new Color(130, 205, 130));
                descY += 22;
            }
            if (def.VitBonus > 0)
            {
                _sb.DrawString(_font, $"+{def.VitBonus} VIT", new Vector2(cx + 12, descY),
                    new Color(210, 90, 90));
                descY += 22;
            }
            if (def.IntBonus > 0)
            {
                _sb.DrawString(_font, $"+{def.IntBonus} INT", new Vector2(cx + 12, descY),
                    new Color(130, 160, 225));
                descY += 22;
            }
            if (def.LukBonus > 0)
            {
                _sb.DrawString(_font, $"+{def.LukBonus} LUK", new Vector2(cx + 12, descY),
                    new Color(225, 205, 110));
                descY += 22;
            }
            if (def.DfStatBonus > 0)
            {
                _sb.DrawString(_font, $"+{def.DfStatBonus} DEF", new Vector2(cx + 12, descY),
                    new Color(180, 180, 190));
                descY += 22;
            }
        }

        // Begin button
        int btnW = 260, btnH = 48;
        int btnX = (sw - btnW) / 2;
        int btnY = cardY + cardH + 34;

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
        string btnText = $"НАЧАТЬ ИГРУ — {selDef?.Name ?? ""}";
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
