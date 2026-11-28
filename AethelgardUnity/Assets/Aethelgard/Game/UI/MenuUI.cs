using Aethelgard.Runtime;
    using Aethelgard.Render;
    using System;
    using System.Collections.Generic;
    using Aethelgard.Systems;
    using UnityEngine;
    using UnityEngine.UI;
    using XnaKeys = Microsoft.Xna.Framework.Input.Keys;
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
    /// Главное меню и все подменю (saves/settings/controls/credits/mp/mpjoin)
    /// + меню паузы. Точная компоновка python-final (menu_renderer.py +
    /// menu_controller.py): кнопки 400×56 по центру с шагом 80, заголовок
    /// с двойным подчёркиванием, стрелка-выделение, градиент подменю.
    /// </summary>
    public class MenuUI
    {
        private readonly UIManager _ui;
        private readonly UITheme _t;
        private readonly GameController _game;

        public RectTransform Root = null!;
        public bool FromGame;

        private GameObject? _panel;
        private readonly List<GameObject> _buttons = new();
        private readonly List<string> _labels = new();
        private readonly List<Button> _selButtons = new();
        private string _lastMode = "";
        private readonly string[] _pauseItems =
            { "Вернуться в игру", "Сохранить игру", "Загрузить игру", "Настройки", "Выйти в главное меню" };
        private string _ipBuffer = "";
        private int _pauseSelected;
        private bool _pauseBuilt;
        private GameObject? _pausePanel;
        private readonly List<Button> _pauseButtons = new();

        // python-final menu_controller.button_rects(): 400×56, y=200, шаг 80
        private const int BtnW = 400;
        private const int BtnH = 56;
        private const int BtnY = 200;
        private const int BtnStep = 80;
        private const int BtnX = (1920 - 400) / 2; // 760

        private static readonly (int W, int H)[] Resolutions =
            { (1280, 720), (1600, 900), (1920, 1080), (2560, 1440) };

        public MenuUI(UIManager ui)
        {
            _ui = ui;
            _t = ui.Theme;
            _game = ui.Game;
        }

        public void Build()
        {
            var go = UiKit.Node("Menu", null);
            Root = UiKit.Attach(go, _ui.Root);
            Root.gameObject.SetActive(false);

            // Анимированный ландшафт python-final (menu_background.py)
            MenuBackdrop.Create(Root, _t);
        }

        public void OpenMain() { _game.ScreenMode = Screens.Menu; FromGame = false; }
        public void OpenFromGame() { }
        public void OpenSaves(bool saveMode)
        {
            _game.ScreenMode = Screens.MenuSaves;
            _saveMode = saveMode;
        }
        public void OpenSettings() => _game.ScreenMode = Screens.MenuSettings;
        private bool _saveMode = false;

        /// <summary>Обработка ввода меню (клавиатура, как в MonoGame-версии).</summary>
        public void Handle(Microsoft.Xna.Framework.Input.KeyboardState keys,
            Microsoft.Xna.Framework.Input.KeyboardState prevKeys,
            XnaMouse mouse, XnaMouse prevMouse)
        {
            string mode = _game.ScreenMode;

            // Esc — на уровень вверх (кнопка «←» в левом верхнем углу)
            bool esc = keys.IsKeyDown(XnaKeys.Escape) && !prevKeys.IsKeyDown(XnaKeys.Escape);
            if (esc && mode != Screens.Menu)
            {
                MenuBack(mode);
                return;
            }

            var items = ItemsFor(mode);
            if (items.Length == 0) return;

            bool up = keys.IsKeyDown(XnaKeys.Up) && !prevKeys.IsKeyDown(XnaKeys.Up);
            bool down = keys.IsKeyDown(XnaKeys.Down) && !prevKeys.IsKeyDown(XnaKeys.Down);
            bool enter = keys.IsKeyDown(XnaKeys.Enter) && !prevKeys.IsKeyDown(XnaKeys.Enter);

            if (mode == Screens.MenuMpJoin)
            {
                HandleIpTyping(keys, prevKeys);
                // Enter подтверждает подключение (было доступно только мышью)
                if (enter) Activate(mode, _sel);
                return;
            }

            // Выбор не должен «переезжать» между экранами с разным числом пунктов
            if (_sel >= items.Length) _sel = 0;
            if (up) _sel = (_sel + items.Length - 1) % items.Length;
            if (down) _sel = (_sel + 1) % items.Length;
            if (enter) Activate(mode, _sel);
        }
        private int _sel;

        private void HandleIpTyping(Microsoft.Xna.Framework.Input.KeyboardState keys,
            Microsoft.Xna.Framework.Input.KeyboardState prevKeys)
        {
            foreach (var k in keys.GetPressedKeys())
            {
                if (prevKeys.IsKeyDown(k)) continue;
                if (k == XnaKeys.Backspace && _ipBuffer.Length > 0)
                    _ipBuffer = _ipBuffer[..^1];
                else if (k >= XnaKeys.D0 && k <= XnaKeys.D9)
                    _ipBuffer += (char)('0' + (k - XnaKeys.D0));
                else if (k >= XnaKeys.NumPad0 && k <= XnaKeys.NumPad9)
                    _ipBuffer += (char)('0' + (k - XnaKeys.NumPad0));
                else if (k == XnaKeys.OemPeriod) _ipBuffer += ".";
                else if (k >= XnaKeys.A && k <= XnaKeys.Z) _ipBuffer += (char)('a' + (k - XnaKeys.A));
                else if (k == XnaKeys.Space || k == XnaKeys.Tab) _ipBuffer += ":";
            }
            // Перерисовываем только при изменении буфера (каждый кадр текало GC)
            if (_ipBuffer != _lastIp)
            {
                _lastIp = _ipBuffer;
                _lastMode = "";
            }
        }
        private string _lastIp = "";

        // Точная структура python-final (menu_controller.MENU_START): 6 пунктов,
        // «Создатели» вместо «О игре», «Управление» — строка ВНУТРИ настроек
        private string[] ItemsFor(string mode) => mode switch
        {
            Screens.Menu => new[] { "Начать игру", "Загрузить игру", "Мультиплеер", "Настройки", "Создатели", "Выйти" },
            Screens.MenuSaves => new[] { "СЛОТ 1", "СЛОТ 2", "СЛОТ 3" },
            // Порядок python-final: разрешение, полный экран, громкость, FPS,
            // мини-карта, управление
            Screens.MenuSettings => new[] { "РАЗРЕШЕНИЕ", "ПОЛНЫЙ ЭКРАН", "ЗВУК", "FPS", "МИНИ-КАРТА", "УПРАВЛЕНИЕ" },
            Screens.MenuControls => Array.Empty<string>(),
            Screens.MenuCredits => Array.Empty<string>(),
            Screens.MenuMultiplayer => new[] { "СОЗДАТЬ ИГРУ (НОВЫЙ МИР)", "СОЗДАТЬ ИГРУ (ПРОДОЛЖИТЬ)", "ПОДКЛЮЧИТЬСЯ" },
            Screens.MenuMpJoin => new[] { "ПОДКЛЮЧИТЬСЯ" },
            _ => Array.Empty<string>(),
        };

        private void Activate(string mode, int idx)
        {
            switch (mode)
            {
                case Screens.Menu:
                    switch (idx)
                    {
                        case 0: _ui.OnStartNew(1); break;
                        case 1: OpenSaves(false); break;
                        case 2: _game.ScreenMode = Screens.MenuMultiplayer; break;
                        case 3: _game.ScreenMode = Screens.MenuSettings; break;
                        case 4: _game.ScreenMode = Screens.MenuCredits; break;
                        case 5: _ui.OnExit(); break;
                    }
                    break;
                case Screens.MenuSaves:
                {
                    int slot = idx + 1;
                    if (_saveMode) _ui.OnSaveSlot(slot);
                    else _ui.OnLoadSlot(slot);
                    break;
                }
                case Screens.MenuSettings:
                    if (idx == 0) CycleResolution();
                    else if (idx == 1) _ui.OnFullscreen(!_fullscreen);
                    else if (idx == 3) { _game.Settings.ShowFps = !_game.Settings.ShowFps; _ui.ApplyHudSettings(); }
                    else if (idx == 4) { _game.Settings.ShowMinimap = !_game.Settings.ShowMinimap; _ui.ApplyHudSettings(); }
                    else if (idx == 5) _game.ScreenMode = Screens.MenuControls;
                    _game.Settings.Save();
                    break;
                case Screens.MenuMultiplayer:
                    if (idx == 0) _ui.OnMpCreate();
                    else if (idx == 1) _ui.OnMpHost(false);
                    else if (idx == 2) _game.ScreenMode = Screens.MenuMpJoin;
                    break;
                case Screens.MenuMpJoin:
                    if (idx == 0) _ui.OnMpJoin(_ipBuffer);
                    break;
            }
        }

        /// <summary>Кнопка «Назад» (BACK_RECT (34,28,52,48)) и Esc — как в python.
        /// Уровни: настройки→меню, управление→настройки, mpjoin→мультиплеер.</summary>
        private void MenuBack(string mode)
        {
            if (mode == Screens.MenuControls)
            {
                _game.ScreenMode = Screens.MenuSettings;
                _sel = 5;
            }
            else if (mode == Screens.MenuMpJoin)
                _game.ScreenMode = Screens.MenuMultiplayer;
            else
            {
                _game.ScreenMode = Screens.Menu;
                _sel = 0;
            }
        }

        private int _resIdx = 2;
        private bool _fullscreen;

        private void CycleResolution()
        {
            _resIdx = (_resIdx + 1) % Resolutions.Length;
            var (w, h) = Resolutions[_resIdx];
            _ui.OnResolution(w, h);
        }

        /// <summary>Отрисовка меню (перестройка при смене режима).</summary>
        public void Draw(string mode)
        {
            if (mode != _lastMode)
            {
                _lastMode = mode;
                Rebuild(mode);
            }
            else if (mode == Screens.MenuMpJoin)
            {
                Rebuild(mode); // IP-поле меняется при вводе
            }
            ApplyMenuSelection();
        }

        /// <summary>
        /// Клавиатурная подсветка python-final: градиент (96,76,44), золотая
        /// рамка, светлая верхняя линия, стрелка слева (MenuButtonSkin).
        /// </summary>
        private void ApplyMenuSelection()
        {
            for (int i = 0; i < _selButtons.Count && i < _labels.Count; i++)
            {
                var btn = _selButtons[i];
                if (btn == null) continue; // кнопка уже уничтожена (перестройка)
                bool sel = i == _sel;
                UiKit.SetKeyboardSelected(btn, sel);
            }
        }

        private void ClearButtons()
        {
            foreach (var b in _buttons) UnityEngine.Object.Destroy(b);
            _buttons.Clear();
            _labels.Clear();
            _selButtons.Clear();
        }

        private void Rebuild(string mode)
        {
            ClearButtons();
            if (_panel != null) UnityEngine.Object.Destroy(_panel);
            _panel = UiKit.Node("MenuPanel", Root);
            UiKit.Attach(_panel, Root);
            var pt = _panel.transform;

            bool main = mode == Screens.Menu;
            if (!main)
            {
                // Подменю: вертикальный градиент (10,9,7)→(30,24,18) вместо ландшафта
                var bg = UiKit.Node("SubMenuBg", pt);
                UiKit.Attach(bg, pt);
                var img = UiKit.Img(bg, _t.PanelDark);
                img.sprite = UiKit.BtnGradient();
                img.type = Image.Type.Simple;
                img.color = UITheme.C(30, 24, 18); // верх; низ темнее (×0.545)
                bg.transform.SetAsFirstSibling();
            }

            string title = mode switch
            {
                Screens.MenuSaves => _saveMode ? "СОХРАНИТЬ" : "ЗАГРУЗИТЬ",
                Screens.MenuSettings => "НАСТРОЙКИ",
                Screens.MenuControls => "УПРАВЛЕНИЕ",
                Screens.MenuCredits => "СОЗДАТЕЛИ",
                Screens.MenuMultiplayer => "МУЛЬТИПЛЕЕР",
                Screens.MenuMpJoin => "ПОДКЛЮЧЕНИЕ",
                _ => "ЭТЕЛЬГАРД",
            };
            int ty = main ? 100 : 80;
            // Заголовок с чёрной тенью (+3,+3), как в menu_renderer.draw_main
            UiKit.Label(pt, 3, ty + 3, 1920, 52, title, _t.H1Size,
                Color.black, _t, TextAnchor.MiddleCenter, "TitleShadow");
            UiKit.Label(pt, 0, ty, 1920, 52, title, _t.H1Size,
                _t.Title, _t, TextAnchor.MiddleCenter, "Title");

            if (main)
            {
                // Двойное подчёркивание y=155: (96,78,46) 440px и (176,142,82) 300px
                UiKit.Solid(pt, (1920 - 440) / 2, 155, 440, 3, UITheme.C(96, 78, 46), "Underline1");
                UiKit.Solid(pt, (1920 - 300) / 2, 155, 300, 2, UITheme.C(176, 142, 82), "Underline2");
            }
            else
            {
                // Кнопка «←» (BACK_RECT (34,28,52,48)) — как в python-final
                var back = UiKit.WidgetButton(pt, 34, 28, 52, 48, "←", _t.H2Size, _t);
                back.onClick.AddListener(() =>
                {
                    MenuBack(mode);
                    _lastMode = "";
                });
            }

            string sub = mode switch
            {
                Screens.Menu => "Роглайк-приключение в мире Этельгард",
                Screens.MenuMpJoin => "Введите IP:порт хоста (Tab — разделитель)",
                _ => "",
            };
            if (sub.Length > 0)
                UiKit.Label(pt, 0, main ? 172 : 140, 1920, 30, sub,
                    _t.BodySize, _t.TextDim, _t, TextAnchor.MiddleCenter, "Subtitle");

            if (mode == Screens.MenuControls)
            {
                // Две колонки разделов, как в menu_renderer.draw_controls
                DrawControlsColumn(pt, ControlsLeft, 132);
                DrawControlsColumn(pt, ControlsRight, 132);
            }
            if (mode == Screens.MenuCredits)
            {
                // Точное содержимое экрана «Создатели» (menu_renderer.draw_credits)
                UiKit.Label(pt, 0, 128, 1920, 26, "Игра разработана командой EVA",
                    _t.BodySize, _t.TextDim, _t, TextAnchor.MiddleCenter, "CrTeam");
                int y = 190;
                foreach (var (cat, names) in CreditsSections)
                {
                    UiKit.Label(pt, 0, y, 1920, 30, cat, _t.H2Size, _t.Title, _t,
                        TextAnchor.MiddleCenter, "CrCat" + y);
                    y += 40;
                    foreach (var name in names)
                    {
                        UiKit.Label(pt, 0, y, 1920, 26, name, _t.BodySize, _t.Text, _t,
                            TextAnchor.MiddleCenter, "CrName" + y);
                        y += 32;
                    }
                    y += 16;
                }
                UiKit.Label(pt, 0, y + 8, 1920, 24, "Все права на изображения принадлежат ей",
                    _t.SmallSize, _t.TextMuted, _t, TextAnchor.MiddleCenter, "CrFooter");
            }

            var items = ItemsFor(mode);
            // Подменю — строки 700×46, шаг 60 (settings), иначе кнопки 400×56
            bool compact = !main && mode != Screens.MenuMultiplayer && mode != Screens.MenuMpJoin;
            int y2 = main ? BtnY : 160;
            for (int i = 0; i < items.Length; i++)
            {
                string label = items[i];
                // Динамические подписи настроек (порядок python-final)
                if (mode == Screens.MenuSettings)
                {
                    if (i == 0) label = $"РАЗРЕШЕНИЕ: {Resolutions[_resIdx].W}×{Resolutions[_resIdx].H}";
                    if (i == 1) label = $"ПОЛНЫЙ ЭКРАН: {(_fullscreen ? "ВКЛ" : "ВЫКЛ")}";
                    if (i == 2) label = $"ЗВУК: {Mathf.RoundToInt(_game.Settings.Volume * 10)}";
                    if (i == 3) label = $"FPS: {(_game.Settings.ShowFps ? "ВКЛ" : "ВЫКЛ")}";
                    if (i == 4) label = $"МИНИ-КАРТА: {(_game.Settings.ShowMinimap ? "ВКЛ" : "ВЫКЛ")}";
                }
                if (mode == Screens.MenuSaves && i < 3)
                {
                    var info = SaveSystem.SlotInfo(i + 1);
                    label = info.HasValue
                        ? $"{label}: {info.Value.Class} ур.{info.Value.Level} ({info.Value.Timestamp:dd.MM HH:mm})"
                        : $"{label} — пусто";
                }
                if (mode == Screens.MenuMpJoin && i == 0 && _ipBuffer.Length > 0)
                    label = $"ПОДКЛЮЧИТЬСЯ: {_ipBuffer}";

                int idx = i;
                int bw = compact ? 700 : BtnW;
                int bh = compact ? 46 : BtnH;
                int bx = (1920 - bw) / 2;
                var btn = UiKit.WidgetButton(pt, bx, y2, bw, bh,
                    label, compact ? _t.BodySize : _t.H2Size, _t, menuArrow: true);
                btn.onClick.AddListener(() =>
                {
                    if (mode == Screens.MenuMpJoin)
                    {
                        if (idx == 0) Activate(mode, idx);
                    }
                    else Activate(mode, idx);
                    _lastMode = ""; // перерисовать с новыми значениями
                });
                _buttons.Add(btn.gameObject);
                _labels.Add(label);
                _selButtons.Add(btn);
                y2 += compact ? 60 : BtnStep;
            }

            // Подсказка ESC
            UiKit.Label(pt, 0, 1020, 1920, 24, "Esc — назад",
                _t.TinySize, _t.TextMuted, _t, TextAnchor.MiddleCenter, "Hint");
        }

        // ── Экран «Управление»: секции из menu_renderer.draw_controls ──

        private static readonly (string, (string, string)[])[] ControlsLeft =
        {
            ("Движение", new[]
            {
                ("W A S D / ↑↓←→", "Шаг"),
                ("Пробел", "Атака (по ближайшему врагу)"),
                ("E", "Собрать ресурс рядом"),
                ("T", "Говорить с NPC / магазин / доска"),
                ("Лестницы", "↓ вниз · ↑ наверх · выход на глубине 1"),
            }),
            ("Окна", new[]
            {
                ("I", "Инвентарь (1–9 — использовать)"),
                ("Shift+1–8", "Назначить предмет на быструю панель"),
                ("U / V", "Снять оружие / броню"),
                ("H / B / N", "Снять шлем / поножи / обувь"),
                ("C", "Характеристики (1–6 — вложить очко)"),
                ("K", "Крафт ([ / ] — размер партии)"),
                ("N", "Древо навыков"),
                ("L", "Квесты (Enter — взять/сдать)"),
                ("Tab", "Статистика забега"),
            }),
        };

        private static readonly (string, (string, string)[])[] ControlsRight =
        {
            ("Карта и бой", new[]
            {
                ("M", "Мини-карта вкл/выкл"),
                ("B", "Большая карта мира"),
                ("1–8", "Быстрая панель предметов"),
                ("F1–F10", "Активные навыки класса"),
                ("H", "Быстрое зелье лечения"),
            }),
            ("Прочее", new[]
            {
                ("F5 / F9", "Сохранить / загрузить"),
                ("PgUp/PgDn · [ ]", "Прокрутка журнала событий"),
                ("Home / End", "Журнал в начало / в конец"),
                ("Магазин", "1–9 — купить · S — продать"),
                ("", "R — обновить ассортимент"),
                ("F12 / `", "Консоль разработчика (по паролю)"),
                ("Esc", "Меню / закрыть окно"),
            }),
        };

        private void DrawControlsColumn(Transform pt,
            (string, (string, string)[])[] sections, int colX)
        {
            int y = 132;
            foreach (var (cat, rows) in sections)
            {
                UiKit.Label(pt, colX, y, 700, 26, cat, _t.H2Size, _t.Title, _t,
                    TextAnchor.UpperLeft, "CtlCat" + colX + y);
                y += 32;
                foreach (var (keyStr, desc) in rows)
                {
                    if (keyStr.Length > 0)
                        UiKit.Label(pt, colX + 20, y, 200, 24, keyStr, _t.BodySize,
                            UITheme.C(196, 176, 138), _t, TextAnchor.UpperLeft,
                            "CtlK" + colX + y);
                    UiKit.Label(pt, colX + 220, y, 480, 24, desc, _t.BodySize,
                        _t.Text, _t, TextAnchor.UpperLeft, "CtlD" + colX + y);
                    y += 26;
                }
                y += 10;
            }
        }

        private static readonly (string, string[])[] CreditsSections =
        {
            ("Создатели", new[] { "Dodik", "Don't talk to people", "And don't write" }),
            ("Художник", new[] { "Лизабет еБатори" }),
        };

        // ── Пауза (та же компоновка, что главное меню — как в python-final) ──

        public void DrawPause(float dt)
        {
            if (!_pauseBuilt) RebuildPause(); // ленивая сборка (для превью/генерации)

            var keys = InputState.Keys;
            var prevKeys = InputState.PrevKeys;
            bool up = keys.IsKeyDown(XnaKeys.Up) && !prevKeys.IsKeyDown(XnaKeys.Up);
            bool down = keys.IsKeyDown(XnaKeys.Down) && !prevKeys.IsKeyDown(XnaKeys.Down);
            bool enter = keys.IsKeyDown(XnaKeys.Enter) && !prevKeys.IsKeyDown(XnaKeys.Enter);
            if (up) { _pauseSelected = (_pauseSelected + _pauseItems.Length - 1) % _pauseItems.Length; UpdatePauseSelection(); }
            if (down) { _pauseSelected = (_pauseSelected + 1) % _pauseItems.Length; UpdatePauseSelection(); }
            if (enter) _ui.PauseChoice(_pauseSelected);
        }

        private void RebuildPause()
        {
            _pauseBuilt = true;
            if (_pausePanel != null) UnityEngine.Object.Destroy(_pausePanel);
            _pausePanel = UiKit.Node("PausePanel", Root);
            UiKit.Attach(_pausePanel, Root);
            var pt = _pausePanel.transform;
            // Затемнение как у главного меню
            var dim = UiKit.Node("Dim", pt);
            UiKit.Attach(dim, pt);
            UiKit.Img(dim, UITheme.C(6, 5, 4, 70f / 255f));

            UiKit.Label(pt, 3, 103, 1920, 52, "ПАУЗА", _t.H1Size, Color.black, _t,
                TextAnchor.MiddleCenter, "TitleShadow");
            UiKit.Label(pt, 0, 100, 1920, 52, "ПАУЗА", _t.H1Size, _t.Title, _t,
                TextAnchor.MiddleCenter, "Title");
            UiKit.Solid(pt, (1920 - 440) / 2, 155, 440, 3, UITheme.C(96, 78, 46), "Underline1");
            UiKit.Solid(pt, (1920 - 300) / 2, 155, 300, 2, UITheme.C(176, 142, 82), "Underline2");

            int y = 200;
            for (int i = 0; i < _pauseItems.Length; i++)
            {
                int idx = i;
                var btn = UiKit.WidgetButton(pt, BtnX, y, BtnW, BtnH,
                    _pauseItems[i], _t.H2Size, _t, menuArrow: true);
                btn.onClick.AddListener(() => _ui.PauseChoice(idx));
                _pauseButtons.Add(btn);
                y += BtnStep;
            }
            UiKit.Label(pt, 0, 1020, 1920, 24,
                "Esc — продолжить", _t.TinySize, _t.TextMuted, _t,
                TextAnchor.MiddleCenter, "Hint");
            UpdatePauseSelection();
        }

        private void UpdatePauseSelection()
        {
            for (int i = 0; i < _pauseButtons.Count; i++)
            {
                if (_pauseButtons[i] == null) continue;
                UiKit.SetKeyboardSelected(_pauseButtons[i], i == _pauseSelected);
            }
        }

    }
}
