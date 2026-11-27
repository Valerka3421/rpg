using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
namespace Aethelgard.Render;

/// <summary>
/// Quest board screen — accept and turn in quests.
/// Ported from Python render/screens_renderer.py:draw_quests.
///
/// Shows: available quests, active quests, completed quests.
/// </summary>
public class QuestScreen
{
    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _sb;
    private SpriteFont? _font;
    private Texture2D? _blank;

    public QuestScreen(GraphicsDevice device, SpriteBatch sb)
    {
        _device = device;
        _sb = sb;
    }

    public void LoadFont(SpriteFont font) => _font = font;

    public void Draw(GameController game)
    {
        int sw = AethelgardGame.ScreenW, sh = AethelgardGame.ScreenH;

        // Dark overlay
        _sb.Draw(Blank, new Rectangle(0, 0, sw, sh), new Color(0, 0, 0, 180));

        // Panel
        int panelW = 600, panelH = 500;
        int px = (sw - panelW) / 2;
        int py = (sh - panelH) / 2;
        _sb.Draw(Blank, new Rectangle(px, py, panelW, panelH), new Color(20, 18, 14, 240));
        _sb.Draw(Blank, new Rectangle(px, py, panelW, 2), Config.ColGold);
        _sb.Draw(Blank, new Rectangle(px, py + panelH - 2, panelW, 2), Config.ColGold);

        if (_font == null) return;

        // Title
        _sb.DrawString(_font, "ДОСКА ЗАДАНИЙ", new Vector2(px + 16, py + 10), Config.ColGold);

        int y = py + 40;

        // Available quests
        var available = game.Quests.Available.ToList();
        if (available.Count > 0)
        {
            _sb.DrawString(_font, "Доступные:", new Vector2(px + 16, y), Config.ColText);
            y += 24;
            for (int i = 0; i < available.Count; i++)
            {
                var def = QuestData.Get(available[i]);
                if (def == null) continue;
                bool selected = i == game.QuestSelected;
                DrawQuestRow(def, px + 16, y, panelW - 32, selected, "Enter — принять");
                y += 48;
            }
        }

        // Active quests
        if (game.Quests.Active.Count > 0)
        {
            y += 8;
            _sb.DrawString(_font, "Активные:", new Vector2(px + 16, y), Config.ColText);
            y += 24;
            foreach (var (key, progress) in game.Quests.Active)
            {
                var def = QuestData.Get(key);
                if (def == null) continue;
                string progressText = def.Type == "reach"
                    ? ""
                    : $"  [{progress}/{def.Count}]";
                DrawQuestRow(def, px + 16, y, panelW - 32, false, progressText);
                y += 48;
            }
        }

        // Completed quests
        var completed = game.Quests.Completed.ToList();
        if (completed.Count > 0)
        {
            y += 8;
            _sb.DrawString(_font, "Выполненные (сдайте для награды):", new Vector2(px + 16, y),
                new Color(120, 200, 120));
            y += 24;
            for (int i = 0; i < completed.Count; i++)
            {
                var def = QuestData.Get(completed[i]);
                if (def == null) continue;
                int selIdx = available.Count + i;
                bool selected = selIdx == game.QuestSelected;
                DrawQuestRow(def, px + 16, y, panelW - 32, selected, "Enter — сдать!");
                y += 48;
            }
        }

        // Hint
        _sb.DrawString(_font, "↑/↓: выбор  Enter: принять/сдать  Esc: закрыть",
            new Vector2(px + 16, py + panelH - 24), Config.ColDim);
    }

    private void DrawQuestRow(QuestData.Def def, int x, int y, int w, bool selected,
        string actionText)
    {
        var bg = selected ? new Color(50, 45, 25) : new Color(25, 23, 18);
        _sb.Draw(Blank, new Rectangle(x, y, w, 44), bg);
        _sb.Draw(Blank, new Rectangle(x, y, w, 1), new Color(50, 45, 38));

        _sb.DrawString(_font, def.Name, new Vector2(x + 8, y + 4),
            selected ? Config.ColGold : Config.ColText);
        _sb.DrawString(_font, def.Desc, new Vector2(x + 8, y + 22), Config.ColDim);

        // Rewards
        string rewards = $"+{def.RewardGold}g  +{def.RewardXp} XP";
        if (def.RewardItems.Count > 0)
            rewards += $"  +{ItemData.DisplayName(def.RewardItems.Keys.First())}";
        _sb.DrawString(_font, rewards, new Vector2(x + w - 180, y + 4), Config.ColGold);

        // Action hint
        if (!string.IsNullOrEmpty(actionText))
            _sb.DrawString(_font, actionText, new Vector2(x + w - 180, y + 22),
                new Color(200, 180, 100));
    }

    /// <summary>
    /// Статистика забега (Tab) — parity python _quests.py:draw_run_stats.
    /// </summary>
    public void DrawRunStats(GameController game)
    {
        int sw = AethelgardGame.ScreenW, sh = AethelgardGame.ScreenH;

        _sb.Draw(Blank, new Rectangle(0, 0, sw, sh), new Color(0, 0, 0, 180));

        int panelW = 980, panelH = 600;
        int px = (sw - panelW) / 2, py = (sh - panelH) / 2;
        _sb.Draw(Blank, new Rectangle(px, py, panelW, panelH), new Color(20, 18, 14, 240));
        _sb.Draw(Blank, new Rectangle(px, py, panelW, 2), Config.ColGold);
        _sb.Draw(Blank, new Rectangle(px, py + panelH - 2, panelW, 2), Config.ColGold);

        if (_font == null) return;

        _sb.DrawString(_font, "СТАТИСТИКА ЗАБЕГА", new Vector2(px + 20, py + 12),
            Config.ColGold);

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

        int lx = px + 20, rx = px + panelW / 2 + 30;
        int y = py + 52;
        _sb.DrawString(_font, "─── ОБЩЕЕ ───", new Vector2(lx, y), new Color(190, 172, 138));
        _sb.DrawString(_font, "─── УБИТО ПО ТИПАМ ───", new Vector2(rx, y),
            new Color(190, 172, 138));
        y += 34;

        foreach (var (name, val) in left)
        {
            _sb.DrawString(_font, name, new Vector2(lx, y), Config.ColDim);
            _sb.DrawString(_font, val, new Vector2(lx + 220, y), Config.ColText);
            y += 30;
        }

        y = py + 86;
        bool has = false;
        foreach (var kv in game.KillsByCh.OrderByDescending(kv => kv.Value))
        {
            if (kv.Value == 0) continue;
            has = true;
            _sb.DrawString(_font, $"{kv.Key}:", new Vector2(rx, y), Config.ColDim);
            _sb.DrawString(_font, kv.Value.ToString(), new Vector2(rx + 260, y),
                Config.ColText);
            y += 30;
            if (y > py + panelH - 30) break;
        }
        if (!has)
            _sb.DrawString(_font, "(пока никого)", new Vector2(rx, y), Config.ColDim);

        _sb.DrawString(_font, "Tab/Esc — закрыть",
            new Vector2(px + 20, py + panelH - 26), Config.ColDim);
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
