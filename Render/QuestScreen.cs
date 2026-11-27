using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
namespace Aethelgard.Render;

/// <summary>
/// Доска заданий — новая реализация на UiKit (единая UI-база).
/// Секции: доступные / активные (с прогресс-барами) / выполненные.
/// Строки с hover, тултипы с описанием и наградами.
/// Порт python render/screens_renderer.py:draw_quests.
/// </summary>
public class QuestScreen
{
    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _sb;
    private SpriteFont? _font;

    private static readonly UiKit K = UiKit.Kit;

    public QuestScreen(GraphicsDevice device, SpriteBatch sb)
    {
        _device = device;
        _sb = sb;
    }

    public void LoadFont(SpriteFont font) => _font = font;

    public void Draw(GameController game)
    {
        var mouse = Mouse.GetState();
        K.Begin(_sb, _device, mouse);

        int sw = AethelgardGame.ScreenW, sh = AethelgardGame.ScreenH;
        var panel = new Rectangle((sw - 1000) / 2, (sh - 640) / 2, 1000, 640);
        K.Panel(panel);

        // ── Шапка ──
        K.Text("ДОСКА ЗАДАНИЙ", new Vector2(panel.X + 20, panel.Y + 14),
            UiKit.Theme.GoldBright, 2);
        K.Text("принимай, выполняй, сдавай",
            new Vector2(panel.X + 20, panel.Y + 48),
            UiKit.Theme.TextMuted, 4, shadow: false);

        var available = game.Quests.Available.ToList();
        var completed = game.Quests.Completed.ToList();
        K.Text($"Доступно: {available.Count}   Активно: {game.Quests.Active.Count}   Сдать: {completed.Count}",
            new Vector2(panel.Right - 20 - K.FontWidth(
                $"Доступно: {available.Count}   Активно: {game.Quests.Active.Count}   Сдать: {completed.Count}", 4),
                panel.Y + 24), UiKit.Theme.TextDim, 4, shadow: false);

        int y = panel.Y + 74;
        int contentBottom = panel.Bottom - 36;

        // ── Доступные квесты (клавиатура: Enter — принять) ──
        if (available.Count > 0)
        {
            var rect = SectionRect(panel.X + 20, y, panel.Width - 40, Math.Min(available.Count, 4));
            K.Section(rect, "Доступные");
            y = DrawQuestRows(game, available, rect, selectedIndexOffset: 0,
                trailing: "Enter — принять");
            y += UiKit.Gap;
        }

        // ── Активные квесты с прогресс-барами ──
        if (game.Quests.Active.Count > 0)
        {
            var rect = SectionRect(panel.X + 20, y, panel.Width - 40,
                Math.Min(game.Quests.Active.Count, 3), perRow: 58);
            K.Section(rect, "Активные");
            y = DrawActiveRows(game, rect);
            y += UiKit.Gap;
        }

        // ── Выполненные (сдать для награды) ──
        if (completed.Count > 0)
        {
            var rect = SectionRect(panel.X + 20, y, panel.Width - 40, Math.Min(completed.Count, 3));
            K.Section(rect, "Выполненные — сдайте для награды");
            DrawQuestRows(game, completed, rect,
                selectedIndexOffset: available.Count, trailing: "Enter — сдать!",
                trailingColor: UiKit.Theme.Xp);
        }

        // ── Подсказка внизу ──
        K.TextCentered("↑/↓: выбор   Enter: принять/сдать   Esc: закрыть",
            new Rectangle(panel.X, panel.Bottom - 28, panel.Width, 22),
            UiKit.Theme.TextMuted, 4, shadow: false);

        K.End();
        K.EndInputFrame();
    }

    /// <summary>Прямоугольник секции по числу строк.</summary>
    private Rectangle SectionRect(int x, int y, int w, int rows, int perRow = 48) =>
        new(x, y, w, 26 + rows * (perRow + 4) + 6);

    /// <summary>
    /// Строки квестов внутри секции. selected по глобальному индексу
    /// (доступные + выполненные — единый список выбора клавиатурой).
    /// </summary>
    private int DrawQuestRows(GameController game, List<string> keys, Rectangle section,
        int selectedIndexOffset, string trailing, Color? trailingColor = null)
    {
        int y = section.Y + 30;
        foreach (var key in keys)
        {
            var def = QuestData.Get(key);
            if (def == null) continue;
            int idx = selectedIndexOffset + keys.IndexOf(key);
            bool selected = idx == game.QuestSelected;

            var rowRect = new Rectangle(section.X + 8, y, section.Width - 16, 48);
            // Имя рисует K.Row; описание и правый текст — сами (нужен свой цвет)
            K.Row(rowRect, def.Name, null, selected: selected, enabled: true);

            _sb.DrawString(Fonts.Small, def.Desc, new Vector2(rowRect.X + 10, y + 26),
                UiKit.Theme.TextDim);

            string rewardText = $"+{def.RewardGold} з. +{def.RewardXp} XP";
            K.Text(rewardText,
                new Vector2(rowRect.Right - K.FontWidth(rewardText, 4) - 10, y + 5),
                UiKit.Theme.Gold, 4, shadow: false);
            K.Text(trailing,
                new Vector2(rowRect.Right - K.FontWidth(trailing, 4) - 10, y + 26),
                trailingColor ?? UiKit.Theme.GoldBright, 4, shadow: false);

            // Тултип: описание + награды
            if (K.Hoverable(rowRect))
            {
                var lines = new List<(string, Color)>();
                foreach (var line in Wrap(def.Desc, 250, Fonts.Small))
                    lines.Add((line, UiKit.Theme.TextDim));
                lines.Add(($"+{def.RewardGold} з.  +{def.RewardXp} XP", UiKit.Theme.Gold));
                foreach (var it in def.RewardItems)
                    lines.Add(($"  + {ItemData.DisplayName(it.Key)}×{it.Value}", UiKit.Theme.Xp));
                K.Tooltip(rowRect, def.Name, lines);
            }

            y += 52;
            if (y + 48 > section.Bottom) break;
        }
        return y;
    }

    /// <summary>Активные квесты: имя, прогресс-бар, тултип.</summary>
    private int DrawActiveRows(GameController game, Rectangle section)
    {
        int y = section.Y + 30;
        foreach (var (key, state) in game.Quests.Active)
        {
            var def = QuestData.Get(key); // сгенерированные квесты здесь не показываем (parity)
            if (def == null) continue;

            var rowRect = new Rectangle(section.X + 8, y, section.Width - 16, 58);
            K.Row(rowRect, def.Name, null, selected: false, enabled: true);
            _sb.DrawString(Fonts.Small, def.Desc, new Vector2(rowRect.X + 10, y + 26),
                UiKit.Theme.TextDim);

            // Прогресс-бар: reached/kill-счётчик
            double frac = state.Count > 0 ? (double)state.Progress / state.Count : 0;
            int barW = 220;
            K.Bar(rowRect.Right - barW - 12, y + 8, barW, 14, frac, UiKit.Theme.Xp);
            string prog = $"{state.Progress}/{state.Count}";
            K.Text(prog, new Vector2(rowRect.Right - barW - 12 + (barW - K.FontWidth(prog, 4)) / 2,
                y + 8), UiKit.Theme.GoldBright, 4, shadow: false);

            if (frac >= 1.0)
            {
                string ready = "Готово — сдай!";
                K.Text(ready, new Vector2(rowRect.Right - K.FontWidth(ready, 4) - 12, y + 34),
                    UiKit.Theme.Xp, 4, shadow: false);
            }

            // Тултип
            if (K.Hoverable(rowRect))
            {
                var lines = new List<(string, Color)>
                {
                    (def.Type == "reach" ? "Достигни нужной глубины"
                        : def.Type == "craft" ? "Скрафти предметы"
                        : def.Type == "collect" ? "Собери предметы"
                        : "Убей врагов", UiKit.Theme.TextDim),
                    ($"Прогресс: {state.Progress}/{state.Count}", UiKit.Theme.Gold),
                    ($"Награда: +{def.RewardGold} з.  +{def.RewardXp} XP", UiKit.Theme.Gold),
                };
                K.Tooltip(rowRect, def.Name, lines);
            }

            y += 62;
            if (y + 58 > section.Bottom) break;
        }
        return y;
    }

    /// <summary>Перенос текста по ширине (для тултипов).</summary>
    private static IEnumerable<string> Wrap(string text, int maxW, SpriteFont f)
    {
        foreach (var paragraph in text.Split('\n'))
        {
            string line = "";
            foreach (var word in paragraph.Split(' '))
            {
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (f.MeasureString(candidate).X > maxW && line.Length > 0)
                {
                    yield return line;
                    line = word;
                }
                else line = candidate;
            }
            if (line.Length > 0) yield return line;
        }
    }

    /// <summary>
    /// Статистика забега (Tab) — parity python _quests.py:draw_run_stats.
    /// Две колонки-секции на UiKit: общее / убито по типам.
    /// </summary>
    public void DrawRunStats(GameController game)
    {
        var mouse = Mouse.GetState();
        K.Begin(_sb, _device, mouse);

        int sw = AethelgardGame.ScreenW, sh = AethelgardGame.ScreenH;
        var panel = new Rectangle((sw - 1100) / 2, (sh - 640) / 2, 1100, 640);
        K.Panel(panel);

        K.Text("СТАТИСТИКА ЗАБЕГА", new Vector2(panel.X + 20, panel.Y + 14),
            UiKit.Theme.GoldBright, 2);

        var ts = DateTime.Now - game.RunStart;
        int mins = (int)ts.TotalSeconds / 60, secs = (int)ts.TotalSeconds % 60;
        var questsActive = game.Quests.Active.Count;

        (string, string)[] left =
        {
            ("Время", $"{mins}:{secs:D2}"),
            ("Убито мобов", game.KillsTotal.ToString()),
            ("Боссов", game.BossKills.ToString()),
            ("Уровень", game.Player.Level.ToString()),
            ("Глубина", game.Depth.ToString()),
            ("Золото сейчас", game.Player.Gold.ToString()),
            ("Золото добыто", game.GoldEarned.ToString()),
            ("Зелий", game.PotionsUsed.ToString()),
            ("Крафтов", game.ItemsCrafted.ToString()),
            ("Квестов сдано", game.QuestsDone.ToString()),
            ("Квестов активно", questsActive.ToString()),
        };

        int colW = panel.Width / 2 - 30;
        var leftRect = new Rectangle(panel.X + 20, panel.Y + 60, colW, 26 + left.Length * 28 + 8);
        K.Section(leftRect, "Общее");
        int ly = leftRect.Y + 32;
        foreach (var (name, val) in left)
        {
            K.Text(name, new Vector2(leftRect.X + 12, ly), UiKit.Theme.TextDim, 4, shadow: false);
            K.Text(val, new Vector2(leftRect.X + colW - 12 - K.FontWidth(val, 4), ly),
                UiKit.Theme.GoldBright, 4, shadow: false);
            ly += 28;
        }

        // Правая колонка — убито по типам
        var rightRect = new Rectangle(leftRect.Right + UiKit.Gap, leftRect.Y,
            panel.Width - 40 - colW - UiKit.Gap, leftRect.Height);
        K.Section(rightRect, "Убито по типам");
        int ry = rightRect.Y + 32;
        bool has = false;
        foreach (var kv in game.KillsByCh.OrderByDescending(kv => kv.Value))
        {
            if (kv.Value == 0) continue;
            has = true;
            K.Text(kv.Key, new Vector2(rightRect.X + 12, ry), UiKit.Theme.Text, 4, shadow: false);
            K.Text(kv.Value.ToString(),
                new Vector2(rightRect.Right - 12 - K.FontWidth(kv.Value.ToString(), 4), ry),
                UiKit.Theme.GoldBright, 4, shadow: false);
            ry += 28;
            if (ry > rightRect.Bottom - 8) break;
        }
        if (!has)
            K.Text("(пока никого)", new Vector2(rightRect.X + 12, ry),
                UiKit.Theme.TextMuted, 4, shadow: false);

        K.TextCentered("Tab/Esc — закрыть",
            new Rectangle(panel.X, panel.Bottom - 28, panel.Width, 22),
            UiKit.Theme.TextMuted, 4, shadow: false);

        K.End();
        K.EndInputFrame();
    }
}
