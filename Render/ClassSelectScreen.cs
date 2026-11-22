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
/// Click or press 1-4 to select. Click "Begin" or press Enter to start.
/// </summary>
public class ClassSelectScreen
{
    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _sb;
    private SpriteFont? _font;
    private Texture2D? _blank;

    private static readonly string[] StarterClasses = { "warrior", "mage", "rogue", "artisan" };
    public int Selected = 0;

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

        // Background
        _sb.Draw(Blank, new Rectangle(0, 0, sw, sh), new Color(12, 12, 18));

        if (_font == null) return null;

        // Title
        string title = "ВЫБОР КЛАССА";
        var titleSize = _font.MeasureString(title);
        _sb.DrawString(_font, title,
            new Vector2((sw - titleSize.X) / 2, 60),
            Config.ColGold);

        // Class cards
        int cardW = 240, cardH = 320;
        int gap = 20;
        int totalW = StarterClasses.Length * cardW + (StarterClasses.Length - 1) * gap;
        int startX = (sw - totalW) / 2;
        int cardY = 130;

        // Handle number key selection
        for (int i = 0; i < StarterClasses.Length; i++)
        {
            var key = Keys.D1 + i;
            if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key))
                Selected = i;
        }

        for (int i = 0; i < StarterClasses.Length; i++)
        {
            var def = ClassData.Get(StarterClasses[i]);
            if (def == null) continue;

            int cx = startX + i * (cardW + gap);
            bool isSelected = i == Selected;

            // Card background
            var cardColor = isSelected
                ? new Color(40, 35, 25)
                : new Color(25, 23, 18);
            _sb.Draw(Blank, new Rectangle(cx, cardY, cardW, cardH), cardColor);

            // Card border
            var borderColor = isSelected ? def.Color : new Color(50, 45, 38);
            _sb.Draw(Blank, new Rectangle(cx, cardY, cardW, 3), borderColor);
            _sb.Draw(Blank, new Rectangle(cx, cardY + cardH - 3, cardW, 3), borderColor);
            _sb.Draw(Blank, new Rectangle(cx, cardY, 3, cardH), borderColor);
            _sb.Draw(Blank, new Rectangle(cx + cardW - 3, cardY, 3, cardH), borderColor);

            // Class icon — large circle in class color
            int iconCx = cx + cardW / 2;
            int iconCy = cardY + 60;
            DrawFilledCircle(iconCx, iconCy, 28, def.Color);

            // Class name
            var nameSize = _font.MeasureString(def.Name);
            _sb.DrawString(_font, def.Name,
                new Vector2(iconCx - nameSize.X / 2, cardY + 100),
                Config.ColText);

            // Number key hint
            _sb.DrawString(_font, $"[{i + 1}]",
                new Vector2(cx + 12, cardY + 12),
                Config.ColDim);

            // Description (wrapped)
            int descY = cardY + 130;
            var descLines = WrapText(def.Desc, cardW - 20, _font);
            foreach (var line in descLines)
            {
                _sb.DrawString(_font, line, new Vector2(cx + 10, descY), Config.ColDim);
                descY += 16;
            }

            // Starting stats
            descY += 8;
            _sb.DrawString(_font, "Стартовые бонусы:",
                new Vector2(cx + 10, descY), Config.ColText);
            descY += 18;

            if (def.StrBonus > 0)
            {
                _sb.DrawString(_font, $"+{def.StrBonus} STR", new Vector2(cx + 10, descY),
                    new Color(200, 100, 80));
                descY += 14;
            }
            if (def.AgiBonus > 0)
            {
                _sb.DrawString(_font, $"+{def.AgiBonus} AGI", new Vector2(cx + 10, descY),
                    new Color(120, 200, 120));
                descY += 14;
            }
            if (def.VitBonus > 0)
            {
                _sb.DrawString(_font, $"+{def.VitBonus} VIT", new Vector2(cx + 10, descY),
                    new Color(200, 80, 80));
                descY += 14;
            }
            if (def.IntBonus > 0)
            {
                _sb.DrawString(_font, $"+{def.IntBonus} INT", new Vector2(cx + 10, descY),
                    new Color(120, 150, 220));
                descY += 14;
            }
            if (def.LukBonus > 0)
            {
                _sb.DrawString(_font, $"+{def.LukBonus} LUK", new Vector2(cx + 10, descY),
                    new Color(220, 200, 100));
                descY += 14;
            }

            // Mouse click on card
            var mouseRect = new Rectangle(mouse.X, mouse.Y, 1, 1);
            if (mouseRect.Intersects(new Rectangle(cx, cardY, cardW, cardH))
                && mouse.LeftButton == ButtonState.Pressed)
            {
                Selected = i;
            }
        }

        // Begin button
        int btnW = 200, btnH = 40;
        int btnX = (sw - btnW) / 2;
        int btnY = cardY + cardH + 40;

        bool mouseOverBtn = new Rectangle(btnX, btnY, btnW, btnH)
            .Intersects(new Rectangle(mouse.X, mouse.Y, 1, 1));
        bool mouseClick = mouseOverBtn && mouse.LeftButton == ButtonState.Pressed;

        _sb.Draw(Blank, new Rectangle(btnX, btnY, btnW, btnH),
            mouseOverBtn ? new Color(60, 50, 30) : new Color(30, 28, 22));
        _sb.Draw(Blank, new Rectangle(btnX, btnY, btnW, 2), Config.ColGold);

        string btnText = "НАЧАТЬ ИГРУ";
        var btnSize = _font.MeasureString(btnText);
        _sb.DrawString(_font, btnText,
            new Vector2(btnX + (btnW - btnSize.X) / 2, btnY + 10),
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
