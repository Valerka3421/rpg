using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Aethelgard.Core;
using Aethelgard.Game;

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
        int sw = Game1.ScreenW, sh = Game1.ScreenH;

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
        _sb.DrawString(_font, "QUEST BOARD", new Vector2(px + 16, py + 10), Config.ColGold);

        int y = py + 40;

        // Available quests
        var available = game.Quests.Available.ToList();
        if (available.Count > 0)
        {
            _sb.DrawString(_font, "Available:", new Vector2(px + 16, y), Config.ColText);
            y += 24;
            for (int i = 0; i < available.Count; i++)
            {
                var def = QuestData.Get(available[i]);
                if (def == null) continue;
                bool selected = i == game.QuestSelected;
                DrawQuestRow(def, px + 16, y, panelW - 32, selected, "Enter to accept");
                y += 48;
            }
        }

        // Active quests
        if (game.Quests.Active.Count > 0)
        {
            y += 8;
            _sb.DrawString(_font, "Active:", new Vector2(px + 16, y), Config.ColText);
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
            _sb.DrawString(_font, "Completed (turn in for rewards):", new Vector2(px + 16, y),
                new Color(120, 200, 120));
            y += 24;
            for (int i = 0; i < completed.Count; i++)
            {
                var def = QuestData.Get(completed[i]);
                if (def == null) continue;
                int selIdx = available.Count + i;
                bool selected = selIdx == game.QuestSelected;
                DrawQuestRow(def, px + 16, y, panelW - 32, selected, "Enter to turn in!");
                y += 48;
            }
        }

        // Hint
        _sb.DrawString(_font, "Up/Down: select  Enter: accept/turn in  Esc: close",
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
        if (def.RewardItems.Length > 0)
            rewards += $"  +{ItemData.DisplayName(def.RewardItems[0])}";
        _sb.DrawString(_font, rewards, new Vector2(x + w - 180, y + 4), Config.ColGold);

        // Action hint
        if (!string.IsNullOrEmpty(actionText))
            _sb.DrawString(_font, actionText, new Vector2(x + w - 180, y + 22),
                new Color(200, 180, 100));
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
