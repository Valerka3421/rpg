using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Aethelgard.Core;
using Aethelgard.Game;

namespace Aethelgard.Render;

/// <summary>
/// Dialogue screen — NPC text box with portrait.
/// Ported from Python game/dialogue_controller.py.
/// </summary>
public class DialogueScreen
{
    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _sb;
    private SpriteFont? _font;
    private Texture2D? _blank;

    public DialogueScreen(GraphicsDevice device, SpriteBatch sb)
    {
        _device = device;
        _sb = sb;
    }

    public void LoadFont(SpriteFont font) => _font = font;

    public void Draw(GameController game)
    {
        if (game.DialogueText == null || game.DialogueNpc == null) return;

        int sw = Game1.ScreenW, sh = Game1.ScreenH;

        // Dark overlay
        _sb.Draw(Blank, new Rectangle(0, 0, sw, sh), new Color(0, 0, 0, 160));

        // Dialogue box at bottom
        int boxW = sw - 80, boxH = 140;
        int bx = (sw - boxW) / 2;
        int by = sh - boxH - 40;

        _sb.Draw(Blank, new Rectangle(bx, by, boxW, boxH), new Color(20, 18, 14, 240));
        _sb.Draw(Blank, new Rectangle(bx, by, boxW, 2), Config.ColGold);
        _sb.Draw(Blank, new Rectangle(bx, by + boxH - 2, boxW, 2), Config.ColGold);

        if (_font == null) return;

        // NPC name
        _sb.DrawString(_font, game.DialogueNpc, new Vector2(bx + 16, by + 10), Config.ColGold);

        // Dialogue text (word-wrapped)
        var lines = WrapText(game.DialogueText, boxW - 32, _font);
        int textY = by + 36;
        foreach (var line in lines)
        {
            _sb.DrawString(_font, line, new Vector2(bx + 16, textY), Config.ColText);
            textY += 18;
        }

        // Continue hint
        _sb.DrawString(_font, "[Enter/T/Esc to continue]",
            new Vector2(bx + boxW - 200, by + boxH - 20), Config.ColDim);
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
