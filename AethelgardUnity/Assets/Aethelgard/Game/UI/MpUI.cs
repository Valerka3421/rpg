using System;
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
    /// Панели мультиплеера (порт Render/MpRenderer): чат, список игроков,
    /// режимы инвентарь/магазин/навыки/обмен, статистика сервера.
    /// </summary>
    public class MpUI
    {
        private readonly UIManager _ui;
        private readonly UITheme _t;
        private readonly GameController _game;
        public RectTransform Root = null!;
        private GameObject? _panel;
        private Text? _players;
        private Text? _chat;
        private Text? _status;

        public MpUI(UIManager ui)
        {
            _ui = ui; _t = ui.Theme; _game = ui.Game;
        }

        public void Build()
        {
            var go = UiKit.Node("Mp", null);
            Root = UiKit.Attach(go, _ui.Root);
            Root.gameObject.SetActive(false);
        }

        public void Draw(float dt)
        {
            if (_panel == null) BuildPanel();
            if (_players != null)
            {
                var snap = _game.MpSnapshot;
                string info = "Сервер: подключено игроков — " +
                    (snap?.Players?.Count ?? 0);
                if (snap?.Players != null)
                    info += "\n" + string.Join("\n", snap.Players.Select(pl =>
                        $"{pl.ClassName} · ур.{pl.Level} · HP {pl.Hp}/{pl.MaxHp}"));
                _players.text = info;
            }
            if (_chat != null)
                _chat.text = string.Join("\n", _game.ChatLog.TakeLast(12));
            if (_status != null)
            {
                var local = _game.MpSnapshot?.Players?.FirstOrDefault(
                    pl => pl.ClassName == _game.Player?.Name);
                _status.text = local != null
                    ? $"HP {local.Hp}/{local.MaxHp} · XP {local.Xp}/{local.XpNext} · {_game.Depth} эт."
                    : "Ожидание синхронизации...";
            }
        }

        private void BuildPanel()
        {
            _panel = UiKit.Panel(Root, 1460, 180, 440, 700, _t, "MpPanel");
            var parent = _panel.transform;

            UiKit.Label(parent, 0, 16, 440, 30, "МУЛЬТИПЛЕЕР", _t.H2Size, _t.Gold, _t,
                TextAnchor.MiddleCenter, "Title");
            _status = UiKit.Label(parent, 20, 60, 400, 24, "", _t.SmallSize, _t.Ink, _t,
                TextAnchor.UpperLeft, "Status");
            _players = UiKit.Label(parent, 20, 92, 400, 200, "", _t.SmallSize, _t.InkDim,
                _t, TextAnchor.UpperLeft, "Players");
            _chat = UiKit.Label(parent, 20, 300, 400, 280, "", _t.SmallSize, _t.Ink, _t,
                TextAnchor.UpperLeft, "Chat");
            var chatGo = UiKit.Node("ChatInput", null);
            UiKit.Place(chatGo, parent, 20, 590, 400, 34);
            UiKit.Img(chatGo, _t.Inset);
            UiKit.Label(chatGo.transform, 8, 4, 380, 26,
                "Enter — чат (вводится через консоль игры)", _t.TinySize, _t.InkFaint,
                _t, TextAnchor.UpperLeft, "ChatHint");
            UiKit.Label(parent, 20, 640, 400, 40,
                "Esc — выйти из мультиплеера", _t.TinySize, _t.InkFaint, _t,
                TextAnchor.UpperLeft, "EscHint");
        }
    }
}
