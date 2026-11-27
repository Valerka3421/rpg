using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
namespace Aethelgard.Render;

/// <summary>
/// Панель журнала с вкладками «Всё · Бой · Предметы · Общение · Опыт»
/// над HUD (журнал событий python). Вкладки асинхронные: одно событие
/// может попадать в несколько вкладок. Прокрутка PgUp/PgDn/Home/End,
/// вкладки переключаются кликом. Ввод сообщения — Y (доступен и в
/// одиночной игре), Enter — отправить в «Общение».
/// </summary>
public static class ChatPanel
{
    public const int W = 560, H = 170, TabH = 22, LineH = 16;

    // Кэш MeasureString: строки журнала статичны, но раньше каждая строка
    // измерялась в цикле обрезки каждый кадр. Лимит против роста.
    private static readonly Dictionary<string, float> _measureCache = new();

    private static float TextWidth(SpriteFont font, string text)
    {
        if (_measureCache.TryGetValue(text, out var w)) return w;
        w = font.MeasureString(text).X;
        if (_measureCache.Count > 512) _measureCache.Clear();
        _measureCache[text] = w;
        return w;
    }

    public static readonly string[] TabNames =
        { "Всё", "Бой", "Предметы", "Общение", "Опыт" };

    public static Rectangle PanelRect =>
        new(8, AethelgardGame.ScreenH - Config.HudHeight - H, W, H);

    public static Rectangle TabRect(int i) =>
        new(PanelRect.X + 4 + i * 110, PanelRect.Y + 4, 106, TabH);

    public static Rectangle InputRect =>
        new(PanelRect.X + 4, PanelRect.Bottom - 24, W - 8, 20);

    public static Rectangle ListRect =>
        new(PanelRect.X + 4, PanelRect.Y + TabH + 8, W - 8,
            H - TabH - 38);

    public static void Draw(SpriteBatch sb, SpriteFont font, GameController game,
        Texture2D blank, MouseState mouse)
    {
        var panel = PanelRect;
        sb.Draw(blank, panel, new Color(10, 9, 7, 215));
        sb.Draw(blank, new Rectangle(panel.X, panel.Y, panel.Width, 1),
            new Color(96, 78, 46));
        sb.Draw(blank, new Rectangle(panel.X, panel.Bottom - 1, panel.Width, 1),
            new Color(96, 78, 46));
        sb.Draw(blank, new Rectangle(panel.X, panel.Y, 1, panel.Height),
            new Color(96, 78, 46));
        sb.Draw(blank, new Rectangle(panel.Right - 1, panel.Y, 1, panel.Height),
            new Color(96, 78, 46));

        // ── Вкладки ──
        for (int i = 0; i < TabNames.Length; i++)
        {
            var rect = TabRect(i);
            bool active = i == game.ChatViewTab;
            bool hover = rect.Contains(mouse.X, mouse.Y);
            sb.Draw(blank, rect,
                active ? new Color(48, 40, 28) : hover
                    ? new Color(34, 29, 24) : new Color(24, 21, 17));
            sb.Draw(blank, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2),
                active ? new Color(206, 166, 82) : new Color(70, 60, 46));
            var size = font.MeasureString(TabNames[i]);
            sb.DrawString(font, TabNames[i],
                new Vector2(rect.X + (rect.Width - size.X) / 2,
                            rect.Y + (rect.Height - size.Y) / 2),
                active ? new Color(238, 222, 188) : new Color(150, 140, 125));
        }

        // ── Сообщения выбранной вкладки ──
        var list = game.ChatViewTab switch
        {
            1 => game.Chat.TabSafe(GameChat.Tab.Combat),
            2 => game.Chat.TabSafe(GameChat.Tab.Items),
            3 => game.Chat.TabSafe(GameChat.Tab.Social),
            4 => game.Chat.TabSafe(GameChat.Tab.Xp),
            _ => game.Chat.AllSafe,
        };
        int visible = ListRect.Height / LineH;
        int maxScroll = Math.Max(0, list.Count - visible);
        int scroll = Math.Clamp(game.ChatScroll, 0, maxScroll);
        int start = list.Count - visible - scroll;
        int y = ListRect.Y;
        for (int i = 0; i < visible; i++)
        {
            int idx = start + i;
            if (idx < 0 || idx >= list.Count) break;
            var e = list[idx];
            string text = e.Text;
            // обрезаем по ширине панели (ширина кэшируется: MeasureString
            // в while-цикле на каждую строку каждый кадр давило на GC)
            while (text.Length > 3 && TextWidth(font, text) > ListRect.Width - 4)
                text = text[..^2];
            sb.DrawString(font, text, new Vector2(ListRect.X + 2, y), e.Color);
            y += LineH;
        }

        // ── Полоса ввода ──
        if (game.ChatInputOpen)
        {
            var input = InputRect;
            sb.Draw(blank, input, new Color(24, 21, 17));
            sb.Draw(blank, new Rectangle(input.X, input.Y, input.Width, 1),
                new Color(110, 91, 62));
            sb.Draw(blank, new Rectangle(input.X, input.Bottom - 1, input.Width, 1),
                new Color(110, 91, 62));
            bool cursor = Environment.TickCount / 500 % 2 == 0;
            sb.DrawString(font, "Сообщение: " + game.ChatLogInput + (cursor ? "_" : ""),
                new Vector2(input.X + 4, input.Y + 3), new Color(228, 220, 200));
        }
        else
        {
            string hint = $"Y — написать · PgUp/PgDn — прокрутка";
            sb.DrawString(font, hint,
                new Vector2(InputRect.X + 4, InputRect.Y + 4),
                new Color(110, 102, 90));
        }
    }
}
