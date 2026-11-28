using System;
    using Aethelgard.Render;
    using Aethelgard.Systems;
    using UnityEngine;
    using UnityEngine.UI;
    using XnaMouse = Microsoft.Xna.Framework.Input.MouseState;
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
    /// Прочие экраны: диалог NPC, большая карта, дев-консоль,
    /// экран смерти/победы (закрывает пробел паритета с python overlay_renderer),
    /// всплывающие сообщения.
    /// </summary>

    // ── Диалог с NPC ──
    public class DialogueUI
    {
        private readonly UIManager _ui;
        private readonly UITheme _t;
        private readonly GameController _game;
        public RectTransform Root = null!;
        private GameObject? _panel;
        private string _sig = "";
        private static readonly System.Collections.Generic.Dictionary<string, Color> RoleColors =
            new()
            {
                ["guard"] = new Color(0.43f, 0.55f, 0.82f),
                ["blacksmith"] = new Color(0.82f, 0.47f, 0.35f),
                ["alchemist"] = new Color(0.47f, 0.75f, 0.43f),
                ["innkeeper"] = new Color(0.86f, 0.67f, 0.35f),
                ["merchant"] = new Color(0.9f, 0.75f, 0.39f),
                ["elder"] = new Color(0.78f, 0.67f, 0.47f),
                ["quest_board"] = new Color(0.9f, 0.78f, 0.43f),
                ["default"] = new Color(0.59f, 0.55f, 0.47f),
            };

        public DialogueUI(UIManager ui)
        {
            _ui = ui; _t = ui.Theme; _game = ui.Game;
        }

        public void Build()
        {
            var go = UiKit.Node("Dialogue", null);
            Root = UiKit.Attach(go, _ui.Root);
            Root.gameObject.SetActive(false);
        }

        public void Draw(XnaMouse mouse)
        {
            string sig = $"{_game.DialogueNpc}|{Hash(_game.DialogueText)}";
            if (sig == _sig) return;
            _sig = sig;
            if (_game.DialogueText == null || _game.DialogueNpc == null) return;

            if (_panel != null) UnityEngine.Object.Destroy(_panel);
            _panel = UiKit.Panel(Root, 360, 780, 1200, 250, _t, "DialoguePanel");
            var parent = _panel.transform;

            string roleKey = FindNpcRole(_game.DialogueNpc);
            var role = RoleColors.GetValueOrDefault(roleKey, RoleColors["default"]);
            var portrait = UiKit.Slot(parent, -60, -110, 160, _t, "Portrait");
            UiKit.Frame((RectTransform)portrait.transform, role, 3);
            UiKit.Label(parent, 60, 60, 1080, 30, _game.DialogueNpc.ToUpperInvariant(),
                _t.H2Size, _t.Gold, _t, TextAnchor.UpperLeft, "Name");
            UiKit.Label(parent, 60, 110, 1080, 120, _game.DialogueText, _t.BodySize,
                _t.Ink, _t, TextAnchor.UpperLeft, "Text");
            UiKit.Label(parent, 800, 210, 380, 24, "Enter / T / Esc — далее",
                _t.TinySize, _t.InkFaint, _t, TextAnchor.LowerRight, "Hint");
        }

        private string FindNpcRole(string name)
        {
            foreach (var n in _game.AllNpcs())
                if (n.Name == name && n is Entity { NpcType: { } t }) return t;
            return "default";
        }

        private static string Hash(string? s) => s?.GetHashCode().ToString() ?? "";
    }

    // ── Большая карта ──
    public class BigmapUI
    {
        private readonly UIManager _ui;
        private readonly UITheme _t;
        private readonly GameController _game;
        public RectTransform Root = null!;
        private GameObject? _panel;
        private bool _built;

        public BigmapUI(UIManager ui)
        {
            _ui = ui; _t = ui.Theme; _game = ui.Game;
        }

        public void Build()
        {
            var go = UiKit.Node("Bigmap", null);
            Root = UiKit.Attach(go, _ui.Root);
            Root.gameObject.SetActive(false);
        }

        public void Draw()
        {
            var map = _game.CurrentMap;
            if (map == null || _built) return;
            _built = true;

            _panel = UiKit.Panel(Root, 260, 40, 1400, 1000, _t, "BigmapPanel");
            var parent = _panel.transform;
            UiKit.Label(parent, 0, 20, 1400, 40, "КАРТА МИРА", _t.H2Size, _t.Gold, _t,
                TextAnchor.MiddleCenter, "Title");

            // Карта — RawImage с текстурой, сгенерированной из MapPalette
            int mw = Config.MapW, mh = Config.MapH;
            var tex = new Texture2D(mw, mh, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var colors = new Color32[mw * mh];
            bool overworld = _game.WorldMode != "dungeon";
            for (int y = 0; y < mh; y++)
                for (int x = 0; x < mw; x++)
                    colors[(mh - 1 - y) * mw + x] = MapPalette.TileColor(map, x, y, overworld);
            tex.SetPixels32(colors);
            tex.Apply();
            var rawGo = new GameObject("MapImage", typeof(RectTransform));
            UiKit.Place(rawGo, parent, 100, 80, 1200, 800);
            var raw = rawGo.AddComponent<RawImage>();
            raw.texture = tex;
            raw.raycastTarget = false;

            // Маркеры: игрок
            int px = 100 + _game.Player!.X * 1200 / mw;
            int py = 80 + (mh - 1 - _game.Player.Y) * 800 / mh;
            UiKit.Solid(parent, px - 5, py - 5, 10, 10, MapPalette.Player, "PlayerDot");

            UiKit.Label(parent, 0, 920, 1400, 24, "B/Esc — закрыть", _t.TinySize,
                _t.InkFaint, _t, TextAnchor.MiddleCenter, "Hint");
        }
    }

    // ── Дев-консоль ──
    public class ConsoleUI
    {
        private readonly UIManager _ui;
        private readonly UITheme _t;
        private readonly GameController _game;
        public RectTransform Root = null!;
        private GameObject? _panel;
        private Text? _output;
        private Text? _input;

        public ConsoleUI(UIManager ui)
        {
            _ui = ui; _t = ui.Theme; _game = ui.Game;
        }

        public void Build()
        {
            var go = UiKit.Node("DevConsole", null);
            Root = UiKit.Attach(go, _ui.Root);
            Root.gameObject.SetActive(false);
        }

        public void Draw()
        {
            Root.gameObject.SetActive(true); // консоль рисуется поверх всего
            var c = _game.Console;
            if (_panel == null)
            {
                _panel = UiKit.Panel(Root, 200, 400, 1520, 620, _t, "ConsolePanel");
                UiKit.Label(_panel.transform, 20, 14, 800, 24, "КОНСОЛЬ РАЗРАБОТЧИКА (F12)",
                    _t.SmallSize, _t.Gold, _t, TextAnchor.UpperLeft, "Title");
                _output = UiKit.Label(_panel.transform, 20, 46, 1480, 440, c.Output,
                    _t.SmallSize, _t.Ink, _t, TextAnchor.UpperLeft, "Output");
                _input = UiKit.Label(_panel.transform, 20, 520, 1480, 30, "> " + c.Buffer,
                    _t.BodySize, _t.Gold, _t, TextAnchor.UpperLeft, "Input");
            }
            _output!.text = c.Output + (string.IsNullOrEmpty(c.Error) ? "" : $"\n{c.Error}");
            _input!.text = "> " + c.Buffer;
        }
    }

    // ── Экран смерти / победы (parity python overlay_renderer) ──
    public class DeathUI
    {
        private readonly UIManager _ui;
        private readonly UITheme _t;
        private readonly GameController _game;
        public RectTransform Root = null!;

        public DeathUI(UIManager ui)
        {
            _ui = ui; _t = ui.Theme; _game = ui.Game;
        }

        public void Build()
        {
            var go = UiKit.Node("DeathVictory", null);
            Root = UiKit.Attach(go, _ui.Root);
            Root.gameObject.SetActive(false);
        }

        public void Draw()
        {
            if (_rootPanel != null) return;
            bool victory = _game.Victory;
            // overlay_renderer.py: dim (7,6,5,225); смерть — панель 700×480
            // PANEL_DARK с рамкой HP_CRIT; победа — 520×170 с рамкой WIN
            UiKit.Solid(Root, 0, 0, 1920, 1080, UITheme.C(7, 6, 5, 225f / 255f), "Dim");
            _rootPanel = UiKit.Panel(Root, victory ? 700 : 610, victory ? 430 : 260,
                victory ? 520 : 700, victory ? 170 : 480, _t, "OverlayPanel");
            var parent = _rootPanel.transform;

            UiKit.Label(parent, 0, 30, victory ? 520 : 700, 60, victory ? "ПОБЕДА!" : "ВЫ ПОГИБЛИ",
                _t.H1Size, victory ? _t.Win : _t.HpCrit, _t, TextAnchor.MiddleCenter,
                "BigTitle");

            if (!victory)
            {
                var ts = DateTime.Now - _game.RunStart;
                int mins = (int)ts.TotalSeconds / 60, secs = (int)ts.TotalSeconds % 60;
                var p = _game.Player!;
                var stats = new (string, string)[]
                {
                    ("Убито мобов", _game.KillsTotal.ToString()),
                    ("Боссов", _game.BossKills.ToString()),
                    ("Уровень", p.Level.ToString()),
                    ("Золото добыто", _game.GoldEarned.ToString()),
                    ("Время", $"{mins}:{secs:D2}"),
                };
                int y = 130;
                foreach (var (name, val) in stats)
                {
                    UiKit.Label(parent, 120, y, 340, 24, name, _t.BodySize, _t.TextDim, _t,
                        TextAnchor.UpperLeft, "S" + y);
                    UiKit.Label(parent, 460, y, 220, 24, val, _t.BodySize, _t.Text, _t,
                        TextAnchor.UpperRight, "Sv" + y);
                    y += 34;
                }

                var restart = UiKit.WidgetButton(parent, 140, y + 40, 250, 50,
                    "R — Заново", _t.BodySize, _t);
                restart.onClick.AddListener(() =>
                {
                    _game.Victory = false;
                    _game.NewGame(p.ClassKey);
                    Clear();
                });
                var load = UiKit.WidgetButton(parent, 420, y + 40, 250, 50,
                    "F9 — Загрузить", _t.BodySize, _t);
                load.onClick.AddListener(() =>
                {
                    var data = SaveSystem.Load(_game.CurrentSlot);
                    if (data != null)
                    {
                        _game.Victory = false;
                        SaveSystem.ApplyLoad(_game, data);
                        _game.ScreenMode = Screens.Game;
                        Clear();
                    }
                    else _game.ShowMessage("Слот пуст");
                });
            }
            else
            {
                UiKit.Label(parent, 0, 100, 520, 30, "Esc — продолжить", _t.SmallSize,
                    _t.TextDim, _t, TextAnchor.MiddleCenter, "Hint");
            }
        }

        private GameObject? _rootPanel;

        private void Clear()
        {
            if (_rootPanel != null) UnityEngine.Object.Destroy(_rootPanel);
            _rootPanel = null;
        }
    }

    // ── Всплывающее сообщение ──
    public class MessageUI
    {
        private readonly UIManager _ui;
        private readonly UITheme _t;
        private readonly GameController _game;
        public RectTransform Root = null!;
        private GameObject? _panel;
        private Text? _label;

        public MessageUI(UIManager ui)
        {
            _ui = ui; _t = ui.Theme; _game = ui.Game;
        }

        public void Build()
        {
            var go = UiKit.Node("Message", null);
            Root = UiKit.Attach(go, _ui.Root);
            Root.gameObject.SetActive(false);
        }

        public void Draw()
        {
            bool show = _game.LastMessage != null && _game.MessageTimer > 0;
            Root.gameObject.SetActive(show);
            if (!show) return;
            if (_panel == null)
            {
                _panel = UiKit.Panel(Root, 660, 940, 600, 70, _t, "MsgPanel");
                _label = UiKit.Label(_panel.transform, 10, 10, 580, 50,
                    _game.LastMessage ?? "", _t.BodySize, _t.Gold, _t,
                    TextAnchor.MiddleCenter, "Msg");
            }
            if (_label != null) _label.text = _game.LastMessage ?? "";
        }
    }
}
