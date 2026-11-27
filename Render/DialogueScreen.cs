using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
namespace Aethelgard.Render;

/// <summary>
/// Диалоговый экран — новая реализация на UiKit (единая UI-база).
/// Нижняя панель: портретная зона с первой буквой имени NPC, имя золотом,
/// текст с переносом строк, подсказки клавиш.
/// Порт python game/dialogue_controller.py.
/// </summary>
public class DialogueScreen
{
    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _sb;
    private SpriteFont? _font;

    private static readonly UiKit K = UiKit.Kit;

    public DialogueScreen(GraphicsDevice device, SpriteBatch sb)
    {
        _device = device;
        _sb = sb;
    }

    public void LoadFont(SpriteFont font) => _font = font;

    public void Draw(GameController game)
    {
        if (game.DialogueText == null || game.DialogueNpc == null) return;

        var mouse = Mouse.GetState();
        K.Begin(_sb, _device, mouse);

        int sw = AethelgardGame.ScreenW, sh = AethelgardGame.ScreenH;

        // Панель диалога внизу экрана
        int boxW = sw - 160, boxH = 200;
        int bx = (sw - boxW) / 2;
        int by = sh - boxH - 40;
        var panel = new Rectangle(bx, by, boxW, boxH);
        K.Panel(panel, dimWorldBehind: false);

        // ── Портретная зона слева: рамка + инициал NPC ──
        int portraitSize = boxH - 32;
        var portraitRect = new Rectangle(panel.X + 16, panel.Y + 16, portraitSize, portraitSize);
        Rect(portraitRect, UiKit.Theme.BgSlot);
        Frame(portraitRect, UiKit.Theme.Border);
        Rect(new Rectangle(portraitRect.X, portraitRect.Y, portraitRect.Width, 2), UiKit.Theme.Gold);
        string initial = game.DialogueNpc.Substring(0, 1).ToUpperInvariant();
        K.TextCentered(initial, portraitRect, UiKit.Theme.GoldBright, 2);

        // ── Текстовая зона справа от портрета ──
        int textX = portraitRect.Right + 16;
        int textW = panel.Right - 16 - textX;

        K.Text(game.DialogueNpc, new Vector2(textX, panel.Y + 18),
            UiKit.Theme.GoldBright, 3);
        Rect(new Rectangle(textX, panel.Y + 48, textW, 1), UiKit.Theme.BorderSoft);

        // Текст диалога с переносом по ширине
        int textY = panel.Y + 58;
        var font = Fonts.Body;
        foreach (var line in WrapText(game.DialogueText, textW - 8, font))
        {
            _sb.DrawString(font, line, new Vector2(textX, textY), UiKit.Theme.Text);
            textY += 20;
            if (textY > panel.Bottom - 40) break; // защита от переполнения
        }

        // ── Подсказки клавиш внизу справа ──
        K.KeyHint(panel.Right - 260, panel.Bottom - 30, "Enter", "далее");
        K.KeyHint(panel.Right - 150, panel.Bottom - 30, "Esc", "закрыть");

        K.End();
        K.EndInputFrame();
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

    // ── Локальные примитивы (поверх UiKit-палитры) ──

    private Texture2D? _blank;
    private Texture2D Blank
    {
        get
        {
            if (_blank == null)
            {
                _blank = new Texture2D(_device, 1, 1);
                _blank.SetData(new[] { Color.White });
                GpuReset.Register(_device, () =>
                {
                    try { _blank?.Dispose(); } catch { }
                    _blank = null;
                });
            }
            return _blank;
        }
    }

    private void Rect(Rectangle r, Color c) => _sb.Draw(Blank, r, c);

    private void Frame(Rectangle r, Color c)
    {
        Rect(new Rectangle(r.X, r.Y, r.Width, 1), c);
        Rect(new Rectangle(r.X, r.Bottom - 1, r.Width, 1), c);
        Rect(new Rectangle(r.X, r.Y, 1, r.Height), c);
        Rect(new Rectangle(r.Right - 1, r.Y, 1, r.Height), c);
    }
}
