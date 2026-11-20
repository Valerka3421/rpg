using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Aethelgard.Core;
using Aethelgard.Game;

namespace Aethelgard.Render;

/// <summary>
/// Save/load slot selection screen.
/// Ported from Python render/screens_renderer.py menu_saves mode.
///
/// Shows 3 save slots with: class, level, timestamp.
/// Controls: 1-3 to save/load, D to delete, Esc to back.
/// </summary>
public class SaveSlotScreen
{
    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _sb;
    private SpriteFont? _font;
    private Texture2D? _blank;

    public enum Mode { Save, Load }
    public Mode CurrentMode = Mode.Save;

    public SaveSlotScreen(GraphicsDevice device, SpriteBatch sb)
    {
        _device = device;
        _sb = sb;
    }

    public void LoadFont(SpriteFont font) => _font = font;

    /// <summary>
    /// Draw the save slot screen.
    /// Returns the slot number the user selected, or 0.
    /// </summary>
    public int Draw(KeyboardState keys, KeyboardState prevKeys, MouseState mouse)
    {
        int sw = Game1.ScreenW, sh = Game1.ScreenH;

        _sb.Draw(Blank, new Rectangle(0, 0, sw, sh), new Color(12, 12, 18));

        if (_font == null) return 0;

        string title = CurrentMode == Mode.Save ? "SAVE GAME" : "LOAD GAME";
        var titleSize = _font.MeasureString(title);
        _sb.DrawString(_font, title,
            new Vector2((sw - titleSize.X) / 2, 80), Config.ColGold);

        int slotW = 500, slotH = 80;
        int startX = (sw - slotW) / 2;
        int startY = 140;
        int gap = 12;

        int selectedSlot = 0;

        for (int i = 1; i <= 3; i++)
        {
            int y = startY + (i - 1) * (slotH + gap);
            var rect = new Rectangle(startX, y, slotW, slotH);

            bool exists = SaveSystem.SlotExists(i);
            bool mouseOver = rect.Intersects(new Rectangle(mouse.X, mouse.Y, 1, 1));

            // Background
            _sb.Draw(Blank, rect,
                mouseOver ? new Color(40, 35, 25) : new Color(25, 23, 18));
            _sb.Draw(Blank, new Rectangle(rect.X, rect.Y, rect.Width, 2),
                exists ? Config.ColGold : new Color(50, 45, 38));

            // Slot number
            _sb.DrawString(_font, $"Slot {i}", new Vector2(rect.X + 12, rect.Y + 8),
                Config.ColText);

            if (exists)
            {
                var info = SaveSystem.SlotInfo(i);
                if (info != null)
                {
                    var (level, className, timestamp) = info.Value;
                    _sb.DrawString(_font, $"{className}  Lv.{level}",
                        new Vector2(rect.X + 12, rect.Y + 28), Config.ColGold);
                    _sb.DrawString(_font, timestamp.ToString("yyyy-MM-dd HH:mm"),
                        new Vector2(rect.X + 12, rect.Y + 48), Config.ColDim);
                }
            }
            else
            {
                _sb.DrawString(_font, "[ empty ]",
                    new Vector2(rect.X + 12, rect.Y + 30), Config.ColDim);
            }

            // Number key hint
            _sb.DrawString(_font, $"[{i}]",
                new Vector2(rect.X + rect.Width - 30, rect.Y + 8), Config.ColDim);

            // Click
            if (mouseOver && mouse.LeftButton == ButtonState.Pressed)
            {
                selectedSlot = i;
            }
        }

        // Key input
        for (int i = 1; i <= 3; i++)
        {
            if (keys.IsKeyDown(Keys.D1 + i - 1) && !prevKeys.IsKeyDown(Keys.D1 + i - 1))
                selectedSlot = i;
        }

        // Delete mode: D + number
        if (keys.IsKeyDown(Keys.D) && !prevKeys.IsKeyDown(Keys.D))
        {
            for (int i = 1; i <= 3; i++)
            {
                if (keys.IsKeyDown(Keys.D1 + i - 1))
                {
                    SaveSystem.DeleteSlot(i);
                }
            }
        }

        // Hint
        _sb.DrawString(_font,
            "1-3: select slot  D+number: delete  Esc: back",
            new Vector2(startX, startY + 3 * (slotH + gap) + 20), Config.ColDim);

        return selectedSlot;
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
