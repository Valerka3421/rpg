using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
namespace Aethelgard.Render;

/// <summary>
/// Dev console overlay — text input + output log.
/// Ported from Python render/screens_renderer.py dev_console mode.
///
/// F12 or ` to toggle. Semi-transparent dark panel at top of screen.
/// Shows: output history + input buffer + cursor.
/// </summary>
public class DevConsoleScreen
{
    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _sb;
    private SpriteFont? _font;
    private Texture2D? _blank;

    public DevConsoleScreen(GraphicsDevice device, SpriteBatch sb)
    {
        _device = device;
        _sb = sb;
    }

    public void LoadFont(SpriteFont font) => _font = font;

    public void Draw(DevConsole console)
    {
        if (!console.Open || _font == null) return;

        int sw = AethelgardGame.ScreenW;
        int panelH = 300;

        // Dark panel at top
        _sb.Draw(Blank, new Rectangle(0, 0, sw, panelH), new Color(10, 10, 15, 235));
        _sb.Draw(Blank, new Rectangle(0, panelH, sw, 2), Config.ColGold);

        // Title
        _sb.DrawString(_font, "[ КОНСОЛЬ РАЗРАБОТЧИКА ]", new Vector2(8, 6), Config.ColGold);

        // Output
        int y = 28;
        var lines = console.Output.Split('\n');
        foreach (var line in lines)
        {
            if (line.Length > 0)
            {
                _sb.DrawString(_font, line, new Vector2(8, y), Config.ColText);
            }
            y += 16;
        }

        // Error
        if (!string.IsNullOrEmpty(console.Error))
        {
            _sb.DrawString(_font, console.Error, new Vector2(8, y),
                new Color(220, 80, 80));
            y += 16;
        }

        // Input prompt
        int inputY = panelH - 24;
        _sb.Draw(Blank, new Rectangle(0, inputY - 2, sw, 24),
            new Color(20, 20, 25, 240));
        _sb.DrawString(_font, "> " + console.Buffer + "_",
            new Vector2(8, inputY), Config.ColText);

        // Hint
        _sb.DrawString(_font, "Enter: ввод  ↑/↓: история  Esc: закрыть",
            new Vector2(sw - 280, inputY), Config.ColDim);
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
