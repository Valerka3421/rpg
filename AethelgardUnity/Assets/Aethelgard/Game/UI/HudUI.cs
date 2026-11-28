using XnaKeys = Microsoft.Xna.Framework.Input.Keys;
    using Aethelgard.Runtime;
    using Aethelgard.Render;
    using Aethelgard.Systems;
    using UnityEngine;
    using UnityEngine.UI;

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
    /// HUD — точная компоновка python-final (game_renderer.draw_hud):
    /// нижняя полоса 170px (9,8,7) с hairline, карточка героя 400px
    /// (HP 180×18 с пульсацией <30%, MP 180×12, XP 180×10, ATK/DEF),
    /// лог-панель справа («ЛОГ», строки 20px), хотбар 10 слотов 44px gap 6
    /// над полосой, миникарта справа сверху (scale 2, рамка).
    /// </summary>
    public partial class HudUI
    {
        private readonly UIManager _ui;
        private readonly UITheme _t;
        private readonly GameController _game;

        public RectTransform Root = null!;

        private const int HudH = 170;
        private const int HudTop = 1080 - HudH; // 910

        private Text _plate = null!;
        private (GameObject root, Image fill, Text overlay) _hp;
        private (GameObject root, Image fill, Text overlay) _mp;
        private (GameObject root, Image fill, Text overlay) _xp;
        private Text _atkDef = null!;
        private Text _locName = null!;
        private Text _points = null!;
        private Text _gold = null!;
        private readonly Text[] _hotbar = new Text[10];
        private readonly Image[] _hotbarFill = new Image[10];
        private readonly Button[] _toolbar = new Button[6];
        private Text _log = null!;
        private RawImage _minimap = null!;
        private Texture2D? _miniTex;
        private RectTransform _playerDot = null!;

        // История сообщений (порт лога python; Core даёт только ShowMessage)
        private readonly List<string> _history = new();
        private string _lastSeen = "";
        private float _pulse;

        public HudUI(UIManager ui)
        {
            _ui = ui;
            _t = ui.Theme;
            _game = ui.Game;
        }

        public void Build()
        {
            var go = UiKit.Node("Hud", null);
            Root = UiKit.Attach(go, _ui.Root);
            Root.gameObject.SetActive(false);

            // ── Полоса HUD снизу: BG_HUD (9,8,7) + hairline PANEL_BORDER_DARK ──
            UiKit.Solid(Root, 0, HudTop, 1920, HudH, _t.BgHud, "HudBg");
            UiKit.Solid(Root, 0, HudTop - 1, 1920, 1, _t.PanelBorderDark, "HudEdge");

            // ── Карточка героя: T.panel((8, y0+4, 400, h)) ──
            var hero = UiKit.Panel(Root, 8, HudTop + 4, 400, HudH - 8, _t, "HeroCard");
            var ht = hero.transform;
            _hp = UiKit.Bar(ht, 30, 20, 180, 18, _t, _t.Hp);
            _mp = UiKit.Bar(ht, 30, 44, 180, 12, _t, _t.ManaPy);
            _xp = UiKit.Bar(ht, 30, 62, 180, 10, _t, _t.Xp);
            _plate = UiKit.Label(ht, 220, 18, 170, 40, "", _t.SmallSize, _t.TitleLight, _t,
                TextAnchor.UpperLeft, "Plate");
            _atkDef = UiKit.Label(ht, 220, 44, 170, 22, "", _t.TinySize, _t.TextDim, _t,
                TextAnchor.UpperLeft, "AtkDef");
            _gold = UiKit.Label(ht, 220, 62, 170, 22, "", _t.SmallSize, _t.Gold, _t,
                TextAnchor.UpperLeft, "Gold");
            _points = UiKit.Label(ht, 30, 78, 180, 22, "", _t.TinySize, _t.TextDim, _t,
                TextAnchor.UpperLeft, "Points");
            _locName = UiKit.Label(ht, 30, 100, 340, 22, "", _t.TinySize, _t.TextMuted, _t,
                TextAnchor.UpperLeft, "Location");

            // ── Лог-панель: T.panel((416, y0+4, SCREEN_W-424, h)) ──
            var logPanel = UiKit.Panel(Root, 416, HudTop + 4, 1920 - 424, HudH - 8, _t, "LogPanel");
            var lt = logPanel.transform;
            UiKit.Label(lt, 10, 6, 100, 16, "ЛОГ", _t.TinySize, _t.TextMuted, _t,
                TextAnchor.UpperLeft, "LogCaption");
            UiKit.Separator(lt, 10, 24, 1920 - 444, _t);
            _log = UiKit.Label(lt, 10, 30, 1920 - 444, HudH - 44, "", _t.SmallSize, _t.ColText, _t,
                TextAnchor.UpperLeft, "LogText");

            // ── Хотбар: 10 слотов 44px gap 6, по центру над HUD ──
            const int slot = 44, gap = 6;
            int totalW = 10 * slot + 9 * gap;
            int hx = (1920 - totalW) / 2;
            int hy = HudTop - 52;
            var hotbarBg = UiKit.Node("Hotbar", null);
            UiKit.Place(hotbarBg, Root, hx - 6, hy - 6, totalW + 12, slot + 12);
            UiKit.Img(hotbarBg, _t.HotbarContainer);
            UiKit.Frame((RectTransform)hotbarBg.transform, _t.HotbarBorder, 1);
            for (int i = 0; i < 10; i++)
            {
                var s = UiKit.Slot(hotbarBg.transform, 6 + i * (slot + gap), 6, slot, _t, "Hotbar" + i);
                UiKit.Frame((RectTransform)s.transform, _t.HotbarBorder, 1);
                var txt = UiKit.Label(s.transform, 0, 0, slot, slot, "", _t.TinySize,
                    _t.Gold, _t, TextAnchor.MiddleCenter, "Item");
                _hotbar[i] = txt;
                _hotbarFill[i] = s.GetComponent<Image>();
            }

            // ── Кнопки интерфейса в полосе HUD (реальные биндинги Core) ──
            string[] names = { "Инв (I)", "Навыки (N)", "Крафт (K)", "Статы (C)", "Карта (M)", "Пауза (Esc)" };
            var keys = new XnaKeys?[]
            {
                Microsoft.Xna.Framework.Input.Keys.I,
                Microsoft.Xna.Framework.Input.Keys.N,
                Microsoft.Xna.Framework.Input.Keys.K,
                Microsoft.Xna.Framework.Input.Keys.C,
                Microsoft.Xna.Framework.Input.Keys.M,
                Microsoft.Xna.Framework.Input.Keys.Escape,
            };
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var btn = UiKit.WidgetButton(Root, 1570 + (i % 2) * 172, HudTop + 8 + (i / 2) * 54,
                    164, 48, names[i], _t.TinySize, _t);
                btn.onClick.AddListener(() =>
                {
                    if (keys[idx] is { } k)
                        _game.HandleInput(Pressed(k), InputState.PrevKeys, Time.deltaTime);
                });
                _toolbar[i] = btn;
            }

            // ── Миникарта справа сверху (margin 8, scale 2) ──
            var miniFrame = UiKit.Node("Minimap", null);
            UiKit.Place(miniFrame, Root, 1920 - 8 - 96, 8, 96, 72);
            UiKit.Img(miniFrame, _t.MiniFrame);
            UiKit.Frame((RectTransform)miniFrame.transform, _t.MiniFrameBorder, 1);
            var rawGo = UiKit.Node("MiniMap", miniFrame.transform);
            UiKit.Place(rawGo, miniFrame.transform, 4, 4, 88, 64);
            _minimap = rawGo.AddComponent<RawImage>();
            _minimap.raycastTarget = false;
            _playerDot = UiKit.Solid(miniFrame.transform, 0, 0, 4, 4, Color.white, "PlayerDot").rectTransform;

            // ── FPS-счётчик слева сверху (настройка «FPS» в меню) ──
            var fps = UiKit.Label(Root, 8, 8, 120, 22, "", _t.SmallSize,
                _t.Text, _t, TextAnchor.UpperLeft, "Fps");
            fps.gameObject.AddComponent<FpsCounter>();

            // Видимость по настройкам (переключаются в «Настройки» меню)
            miniFrame.SetActive(_game.Settings.ShowMinimap);
            fps.gameObject.SetActive(_game.Settings.ShowFps);
        }

        /// <summary>Обновить видимость миникарты/FPS после смены настроек.</summary>
        public void ApplySettingsVisibility()
        {
            var mini = Root.Find("Minimap");
            if (mini != null) mini.gameObject.SetActive(_game.Settings.ShowMinimap);
            var fps = Root.Find("Fps");
            if (fps != null) fps.gameObject.SetActive(_game.Settings.ShowFps);
        }
    }

    /// <summary>Счётчик FPS (простое усреднение за полсекунды).</summary>
    public class FpsCounter : MonoBehaviour
    {
        private Text _txt = null!;
        private int _frames;
        private float _time;

        private void OnEnable() => _txt = GetComponent<Text>();

        private void Update()
        {
            _frames++;
            _time += Time.unscaledDeltaTime;
            if (_time >= 0.5f && _txt != null)
            {
                _txt.text = $"FPS {Mathf.RoundToInt(_frames / _time)}";
                _frames = 0;
                _time = 0f;
            }
        }
    }

    // Продолжение HudUI после вставленного счётчика FPS
    public partial class HudUI
    {
        private static Microsoft.Xna.Framework.Input.KeyboardState Pressed(XnaKeys k) =>
            new(new[] { k });

        public void Draw()
        {
            var p = _game.Player;
            if (p == null) return;
            _pulse += Time.deltaTime;

            string loc = _game.WorldMode == "dungeon" ? $"Подземелье d{_game.Depth}" : "Поверхность";
            _plate.text = $"{p.ClassName}\nУр. {p.Level}";
            _locName.text = loc;

            float hpFrac = p.MaxHp > 0 ? (float)p.Hp / p.MaxHp : 0f;
            _hp.fill.rectTransform.anchorMax = new Vector2(hpFrac, 1f);
            // Пульсация HP <30% между HP и HP_CRIT (синус 800 мс)
            if (hpFrac < 0.3f)
                _hp.fill.color = Color.Lerp(_t.Hp, _t.HpCrit,
                    0.5f + 0.5f * Mathf.Sin(_pulse * (2f * Mathf.PI / 0.8f)));
            else
                _hp.fill.color = _t.Hp;
            _hp.overlay.text = $"{p.Hp}/{p.MaxHp}";
            float mpFrac = p.MaxMana > 0 ? (float)p.Mana / p.MaxMana : 0f;
            _mp.fill.rectTransform.anchorMax = new Vector2(mpFrac, 1f);
            _mp.overlay.text = $"{p.Mana}/{p.MaxMana}";
            float xpFrac = p.XpNext > 0 ? (float)p.Xp / p.XpNext : 0f;
            _xp.fill.rectTransform.anchorMax = new Vector2(xpFrac, 1f);
            _xp.overlay.text = "";

            _atkDef.text = $"ATK {p.Atk} · DEF {p.Df}";
            _gold.text = $"◆ {p.Gold}";
            _points.text = p.FreePoints > 0 || p.SkillPoints > 0
                ? $"Очки: {p.FreePoints} · Навыки: {p.SkillPoints}" : "";

            for (int i = 0; i < 10 && i < p.Quickbar.Length; i++)
            {
                var key = p.Quickbar[i];
                _hotbar[i].text = key != null && ItemData.Get(key) is { } it
                    ? it.Name : $"{i + 1}";
            }

            DrawLog();
            DrawMinimap(p);
        }

        /// <summary>Лог: копим последние сообщения из ShowMessage, как в python.
        /// История сбрасывается при новом забеге (иначе лог тянулся между
        /// рестартами и загрузками).</summary>
        private void DrawLog()
        {
            if (_game.RunStart != _logRunStart)
            {
                _logRunStart = _game.RunStart;
                _history.Clear();
                _lastSeen = "";
            }
            var msg = _game.LastMessage;
            if (msg != null && msg != _lastSeen && _game.MessageTimer > 0)
            {
                _lastSeen = msg;
                _history.Add(msg);
                if (_history.Count > 7) _history.RemoveAt(0);
            }
            _log.text = string.Join("\n", _history);
        }
        private DateTime _logRunStart;

        private void DrawMinimap(Player p)
        {
            const int w = 44, h = 32; // 2px на тайл в поле 88×64
            if (_miniTex == null)
            {
                _miniTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                _miniTex.filterMode = FilterMode.Point;
                _minimap.texture = _miniTex;
            }
            var map = _game.CurrentMap;
            if (map == null) return;
            bool overworld = _game.WorldMode != "dungeon";
            var colors = new Color32[w * h];
            int pxx = p.X - w / 2, pyy = p.Y - h / 2;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    colors[(h - 1 - y) * w + x] =
                        MapPalette.TileColor(map, pxx + x, pyy + y, overworld);
            _miniTex.SetPixels32(colors);
            _miniTex.Apply();

            _playerDot.anchoredPosition = new Vector2(
                48 - 2, 36 - 2); // центр миникарты
        }
    }
}
