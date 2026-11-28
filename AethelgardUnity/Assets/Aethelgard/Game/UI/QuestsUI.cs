using System.Linq;
    using Aethelgard.Systems;
    using UnityEngine;
    using UnityEngine.UI;
    using Screens = Aethelgard.Systems.ScreenMode;

    namespace Aethelgard.UI
    {

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Aethelgard.BaseClass;
    using Aethelgard.Entities;
    using Aethelgard.Entities.Monsters;
    using Aethelgard.Structs;
    using Aethelgard.World;
    using Aethelgard.Systems;
    using Aethelgard.Data;
    using Aethelgard.Net;
    using Aethelgard.Audio;
    /// <summary>
    /// Доска заданий и статистика забега (порт Render/QuestScreen.cs).
    /// </summary>
    public class QuestsUI
    {
        private readonly UIManager _ui;
        private readonly UITheme _t;
        private readonly GameController _game;

        public RectTransform Root = null!;
        private GameObject? _panel;
        private string _sig = "";
        private int? _accept;
        private int? _turnIn;
        private bool _close;

        public QuestsUI(UIManager ui)
        {
            _ui = ui;
            _t = ui.Theme;
            _game = ui.Game;
        }

        public void Build()
        {
            var go = UiKit.Node("Quests", null);
            Root = UiKit.Attach(go, _ui.Root);
            Root.gameObject.SetActive(false);
        }

        public void Draw()
        {
            var quests = _game.Quests;
            string sig = $"{quests.Available.Count}|{quests.Active.Count}|{quests.Completed.Count}|{_game.QuestSelected}";
            if (sig != _sig)
            {
                _sig = sig;
                Rebuild(quests);
            }
            if (_accept.HasValue) { _game.AcceptQuest(_accept.Value); _accept = null; _sig = ""; }
            if (_turnIn.HasValue) { _game.TurnInQuest(_turnIn.Value); _turnIn = null; _sig = ""; }
            if (_close) { _close = false; _game.ScreenMode = Screens.Game; _sig = ""; }
        }

        private void Rebuild(QuestLog quests)
        {
            if (_panel != null) UnityEngine.Object.Destroy(_panel);
            UiKit.ScrimOnce(Root, _t);
            _panel = UiKit.Panel(Root, 410, 120, 1100, 840, _t, "QuestPanel");
            var parent = _panel.transform;

            UiKit.Label(parent, 0, 20, 1100, 44, "ДОСКА ЗАДАНИЙ", _t.H1Size, _t.Title,
                _t, TextAnchor.MiddleCenter, "Title");
            UiKit.CloseButton(parent, 1100 - 10, 10, () => _close = true, _t);

            int y = 100;
            var available = quests.Available.ToList();
            if (available.Count > 0)
            {
                UiKit.Label(parent, 40, y, 600, 22, "ДОСТУПНЫЕ", _t.SmallSize,
                    _t.Section, _t, TextAnchor.UpperLeft, "AvailTitle");
                y += 30;
                for (int i = 0; i < available.Count; i++)
                {
                    var state = QuestData.Get(available[i]);
                    if (state == null) continue;
                    DrawQuest(parent, y, state.Name, state.Desc,
                        $"+{state.RewardGold} з.  +{state.RewardXp} XP",
                        "[ВЗЯТЬ]", () => _accept = i, available[i] == QuestSelectedName(),
                        _t.QuestStateTake);
                    y += 92;
                }
            }

            if (quests.Active.Count > 0)
            {
                UiKit.Label(parent, 40, y, 600, 22, "АКТИВНЫЕ", _t.SmallSize,
                    _t.Section, _t, TextAnchor.UpperLeft, "ActiveTitle");
                y += 30;
                foreach (var kv in quests.Active)
                {
                    var def = QuestData.Get(kv.Key);
                    if (def == null) continue;
                    string prog = def.Type == "reach" ? "" : $"[{kv.Value.Progress}/{kv.Value.Count}]";
                    DrawQuest(parent, y, def.Name, $"{prog} {def.Desc}", "",
                        "[АКТИВЕН]", null, false, _t.QuestStateActive);
                    y += 84;
                }
            }

            var completed = quests.Completed.ToList();
            if (completed.Count > 0)
            {
                UiKit.Label(parent, 40, y, 800, 22, "ВЫПОЛНЕННЫЕ — СДАТЬ ДЛЯ НАГРАДЫ",
                    _t.SmallSize, _t.QuestStateDone, _t, TextAnchor.UpperLeft, "DoneTitle");
                y += 30;
                for (int i = 0; i < completed.Count; i++)
                {
                    var def = QuestData.Get(completed[i]);
                    if (def == null) continue;
                    int idx = available.Count + i;
                    DrawQuest(parent, y, def.Name, def.Desc,
                        $"+{def.RewardGold} з.  +{def.RewardXp} XP",
                        "[СДАТЬ]", () => _turnIn = idx, idx == _game.QuestSelected,
                        _t.QuestStateDone);
                    y += 92;
                }
            }
        }

        private string QuestSelectedName()
        {
            var avail = _game.Quests.Available.ToList();
            if (_game.QuestSelected < avail.Count) return avail[_game.QuestSelected];
            return "";
        }

        private int _rowIdx;

        private void DrawQuest(Transform parent, int y, string name, string desc,
            string reward, string action, System.Action? onClick, bool selected,
            Color stateColor)
        {
            _rowIdx = 0;
            var row = UiKit.Panel(parent, 40, y, 1020, 76, _t, "QuestRow" + _rowIdx++);
            // Карточки python-final: синие панели (единственные холодные в UI)
            var card = row.GetComponent<Image>();
            card.color = selected ? _t.QuestCardSelFill : _t.QuestCardFill;
            for (int i = 0; i < row.transform.childCount; i++)
            {
                var img = row.transform.GetChild(i).GetComponent<Image>();
                if (img != null) img.color = selected ? _t.Gold : _t.QuestCardBorder;
            }
            UiKit.Label(row.transform, 20, 8, 500, 24, name, _t.BodySize,
                selected ? _t.Gold : _t.Text, _t, TextAnchor.UpperLeft, "N");
            UiKit.Label(row.transform, 20, 36, 640, 34, desc, _t.SmallSize, _t.TextDim,
                _t, TextAnchor.UpperLeft, "D");
            if (!string.IsNullOrEmpty(reward))
                UiKit.Label(row.transform, 660, 10, 340, 22, reward, _t.SmallSize,
                    _t.QuestReward, _t, TextAnchor.UpperRight, "R");
            if (!string.IsNullOrEmpty(action))
                UiKit.Label(row.transform, 820, 26, 180, 42, action, _t.SmallSize,
                    stateColor, _t, TextAnchor.MiddleCenter, "State");
            if (onClick != null)
            {
                // Клик по всей карточке берёт/сдаёт квест
                var btn = row.AddComponent<Button>();
                btn.targetGraphic = card;
                btn.onClick.AddListener(() => onClick());
            }
        }

        /// <summary>Статистика забега (Tab) — parity python draw_run_stats.</summary>
        public void DrawRunStats()
        {
            var p = _game.Player;
            string sig = $"run|{_game.KillsTotal}|{p?.Level}";
            if (sig != _sig)
            {
                _sig = sig;
                RebuildRunStats();
            }
            if (_close) { _close = false; _game.ScreenMode = Screens.Game; _sig = ""; }
        }

        private void RebuildRunStats()
        {
            if (_panel != null) UnityEngine.Object.Destroy(_panel);
            UiKit.ScrimOnce(Root, _t);
            _panel = UiKit.Panel(Root, 360, 130, 1200, 820, _t, "RunStatsPanel");
            var parent = _panel.transform;

            UiKit.Label(parent, 0, 20, 1200, 44, "СТАТИСТИКА ЗАБЕГА", _t.H1Size,
                _t.Title, _t, TextAnchor.MiddleCenter, "Title");
            UiKit.CloseButton(parent, 1200 - 10, 10, () => _close = true, _t);

            var ts = DateTime.Now - _game.RunStart;
            int mins = (int)ts.TotalSeconds / 60, secs = (int)ts.TotalSeconds % 60;
            var p = _game.Player!;

            (string, string)[] left =
            {
                ("Время", $"{mins}:{secs:D2}"),
                ("Убито мобов", _game.KillsTotal.ToString()),
                ("Боссов", _game.BossKills.ToString()),
                ("Уровень", p.Level.ToString()),
                ("Глубина", _game.Depth.ToString()),
                ("Золото сейчас", p.Gold.ToString()),
                ("Золото добыто", _game.GoldEarned.ToString()),
                ("Зелий", _game.PotionsUsed.ToString()),
                ("Крафтов", _game.ItemsCrafted.ToString()),
                ("Квестов сдано", _game.QuestsDone.ToString()),
            };
            int y = 110;
            foreach (var (name, val) in left)
            {
                UiKit.Label(parent, 80, y, 300, 24, name, _t.BodySize, _t.InkDim, _t,
                    TextAnchor.UpperLeft, "L" + y);
                UiKit.Label(parent, 380, y, 200, 24, val, _t.BodySize, _t.Ink, _t,
                    TextAnchor.UpperRight, "V" + y);
                y += 32;
            }

            UiKit.Label(parent, 700, 110, 400, 24, "── УБИТО ПО ТИПАМ ──",
                _t.SmallSize, _t.Section, _t, TextAnchor.UpperLeft, "KillsTitle");
            int ry = 146;
            foreach (var kv in _game.KillsByCh.OrderByDescending(kv => kv.Value))
            {
                if (kv.Value == 0) continue;
                UiKit.Label(parent, 700, ry, 300, 22, kv.Key, _t.BodySize, _t.InkDim,
                    _t, TextAnchor.UpperLeft, "K" + ry);
                UiKit.Label(parent, 1000, ry, 120, 22, kv.Value.ToString(), _t.BodySize,
                    _t.Ink, _t, TextAnchor.UpperRight, "Kv" + ry);
                ry += 28;
                if (ry > 700) break;
            }

        }
    }
}
