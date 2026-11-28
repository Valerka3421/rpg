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

namespace Aethelgard.UI;

/// <summary>
/// Главное меню и все подменю (saves/settings/controls/credits/mp/mpjoin)
/// + меню паузы. Порт Render/MainMenuScreen.cs и PauseScreen: тёмный фон,
/// орнаментированный заголовок «ЭТЕЛЬГАРД», бронзовые кнопки.
/// </summary>
public class MenuUI
{
    private readonly UIManager _ui;
    private readonly UITheme _t;
    private readonly GameController _game;

    public RectTransform Root = null!;
    public bool FromGame;

    private GameObject? _panel;
    private Text? _title;
    private Text? _subtitle;
    private readonly List<GameObject> _buttons = new();
    private readonly List<string> _labels = new();
    private readonly List<Button> _selButtons = new();
    private readonly List<Button> _buttonList = new();
    private string _lastMode = "";
    private readonly string[] _pauseItems =
        { "ПРОДОЛЖИТЬ", "СОХРАНИТЬ", "ЗАГРУЗИТЬ", "НАСТРОЙКИ", "В ГЛАВНОЕ МЕНЮ" };
    private string _ipBuffer = "";
    private int _pauseSelected;

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

        // Фон: тёплый градиент (заменяет анимированный MenuBackground —
        // полосы-«хребты» и угольки создаются в MenuBackdrop)
        MenuBackdrop.Create(Root, _t);
        UiKit.SetButtonBaseColor(_t.Bronze);
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
        var items = ItemsFor(mode);
        if (items.Length == 0) return;

        bool up = keys.IsKeyDown(XnaKeys.Up) && !prevKeys.IsKeyDown(XnaKeys.Up);
        bool down = keys.IsKeyDown(XnaKeys.Down) && !prevKeys.IsKeyDown(XnaKeys.Down);
        bool enter = keys.IsKeyDown(XnaKeys.Enter) && !prevKeys.IsKeyDown(XnaKeys.Enter);

        if (mode == Screens.MenuMpJoin)
        {
            HandleIpTyping(keys, prevKeys);
            return;
        }

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
            else if (k == XnaKeys.OemPeriod) _ipBuffer += ".";
            else if (k >= XnaKeys.A && k <= XnaKeys.Z) _ipBuffer += (char)('a' + (k - XnaKeys.A));
            else if (k == XnaKeys.Space) _ipBuffer += ":";
        }
        _lastMode = ""; // перерисовать
    }

    private string[] ItemsFor(string mode) => mode switch
    {
        Screens.Menu => new[] { "ПРОДОЛЖИТЬ", "НОВАЯ ИГРА", "МУЛЬТИПЛЕЕР", "НАСТРОЙКИ", "УПРАВЛЕНИЕ", "ОБ ИГРЕ", "ВЫХОД" },
        Screens.MenuSaves => new[] { "СЛОТ 1", "СЛОТ 2", "СЛОТ 3", "НАЗАД" },
        Screens.MenuSettings => new[] { "РАЗРЕШЕНИЕ", "ПОЛНЫЙ ЭКРАН", "ЗВУК", "НАЗАД" },
        Screens.MenuControls => new[] { "НАЗАД" },
        Screens.MenuCredits => new[] { "НАЗАД" },
        Screens.MenuMultiplayer => new[] { "СОЗДАТЬ ИГРУ (НОВЫЙ МИР)", "СОЗДАТЬ ИГРУ (ПРОДОЛЖИТЬ)", "ПОДКЛЮЧИТЬСЯ", "НАЗАД" },
        Screens.MenuMpJoin => new[] { "ПОДКЛЮЧИТЬСЯ", "НАЗАД" },
        _ => Array.Empty<string>(),
    };

    private void Activate(string mode, int idx)
    {
        switch (mode)
        {
            case Screens.Menu:
                switch (idx)
                {
                    case 0: _ui.OnLoadSlot(_game.CurrentSlot); break;
                    case 1: _ui.OnStartNew(1); break;
                    case 2: _game.ScreenMode = Screens.MenuMultiplayer; break;
                    case 3: _game.ScreenMode = Screens.MenuSettings; break;
                    case 4: _game.ScreenMode = Screens.MenuControls; break;
                    case 5: _game.ScreenMode = Screens.MenuCredits; break;
                    case 6: _ui.OnExit(); break;
                }
                break;
            case Screens.MenuSaves:
                if (idx < 3)
                {
                    int slot = idx + 1;
                    if (_saveMode) _ui.OnSaveSlot(slot);
                    else _ui.OnLoadSlot(slot);
                }
                else
                {
                    _game.ScreenMode = FromGame ? Screens.Menu : Screens.Menu;
                    _game.ScreenMode = Screens.Menu;
                }
                break;
            case Screens.MenuSettings:
                if (idx == 0) CycleResolution();
                else if (idx == 1) _ui.OnFullscreen(!_fullscreen);
                else if (idx == 3) _game.ScreenMode = Screens.Menu;
                break;
            case Screens.MenuControls:
            case Screens.MenuCredits:
                _game.ScreenMode = Screens.Menu;
                break;
            case Screens.MenuMultiplayer:
                if (idx == 0) _ui.OnMpCreate();
                else if (idx == 1) _ui.OnMpHost(false);
                else if (idx == 2) _game.ScreenMode = Screens.MenuMpJoin;
                else _game.ScreenMode = Screens.Menu;
                break;
            case Screens.MenuMpJoin:
                if (idx == 0) _ui.OnMpJoin(_ipBuffer);
                else _game.ScreenMode = Screens.MenuMultiplayer;
                break;
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
    /// Клавиатурная подсветка: выбранная кнопка — светлее, с маркером «▶ ».
    /// </summary>
    private void ApplyMenuSelection()
    {
        for (int i = 0; i < _selButtons.Count && i < _labels.Count; i++)
        {
            bool sel = i == _sel;
            UiKit.SetKeyboardSelected(_selButtons[i], sel);
            var txt = _selButtons[i].GetComponentInChildren<Text>();
            if (txt != null)
                txt.text = sel ? "▶  " + _labels[i] : _labels[i];
        }
    }

    private void ClearButtons()
    {
        foreach (var b in _buttons) UnityEngine.Object.Destroy(b);
        _buttons.Clear();
        _buttonList.Clear();
    }

    private void Rebuild(string mode)
    {
        ClearButtons();
        if (_panel != null) UnityEngine.Object.Destroy(_panel);
        _panel = UiKit.Panel(Root, 560, 140, 800, 800, _t, "MenuPanel");

        string title = mode switch
        {
            Screens.MenuSaves => _saveMode ? "СОХРАНИТЬ" : "ЗАГРУЗИТЬ",
            Screens.MenuSettings => "НАСТРОЙКИ",
            Screens.MenuControls => "УПРАВЛЕНИЕ",
            Screens.MenuCredits => "ОБ ИГРЕ",
            Screens.MenuMultiplayer => "МУЛЬТИПЛЕЕР",
            Screens.MenuMpJoin => "ПОДКЛЮЧЕНИЕ",
            _ => "ЭТЕЛЬГАРД",
        };
        _title = UiKit.Label(_panel.transform, 0, 36, 800, 56, title,
            _t.H1Size, _t.Gold, _t, TextAnchor.MiddleCenter, "Title");

        string sub = mode switch
        {
            Screens.Menu => "Роглайк-приключение в мире Этельгард",
            Screens.MenuMpJoin => "Введите IP:порт хоста (Tab — разделитель)",
            Screens.MenuControls => ControlHelp,
            Screens.MenuCredits => CreditsText,
            _ => "",
        };
        _subtitle = UiKit.Label(_panel.transform, 40, 104, 720, 140, sub,
            _t.SmallSize, _t.InkDim, _t, TextAnchor.UpperCenter, "Subtitle");

        var items = ItemsFor(mode);
        int y = 260;
        for (int i = 0; i < items.Length; i++)
        {
            string label = items[i];
            // Динамические подписи настроек
            if (mode == Screens.MenuSettings)
            {
                if (i == 0) label = $"РАЗРЕШЕНИЕ: {Resolutions[_resIdx].W}×{Resolutions[_resIdx].H}";
                if (i == 1) label = $"ПОЛНЫЙ ЭКРАН: {(_fullscreen ? "ВКЛ" : "ВЫКЛ")}";
                if (i == 2) label = $"ЗВУК: {Mathf.RoundToInt(_game.Settings.Volume * 10)}";
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
            var btn = UiKit.WidgetButton(_panel.transform, 140, y, 520, _t.ButtonHeight,
                label, _t.BodySize, _t);
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
            y += _t.ButtonHeight + 14;
        }

        // Подсказка ESC
        UiKit.Label(_panel.transform, 0, 740, 800, 24, "Esc — назад",
            _t.TinySize, _t.InkFaint, _t, TextAnchor.MiddleCenter, "Hint");
    }

    // ── Пауза (тот же визуал, пункты MENU_PAUSE) ──

    public void DrawPause(float dt)
    {
        var keys = InputState.Keys;
        var prevKeys = InputState.PrevKeys;
        bool up = keys.IsKeyDown(XnaKeys.Up) && !prevKeys.IsKeyDown(XnaKeys.Up);
        bool down = keys.IsKeyDown(XnaKeys.Down) && !prevKeys.IsKeyDown(XnaKeys.Down);
        bool enter = keys.IsKeyDown(XnaKeys.Enter) && !prevKeys.IsKeyDown(XnaKeys.Enter);
        if (up) { _pauseSelected = (_pauseSelected + _pauseItems.Length - 1) % _pauseItems.Length; RebuildPause(); }
        if (down) { _pauseSelected = (_pauseSelected + 1) % _pauseItems.Length; RebuildPause(); }
        if (enter) _ui.PauseChoice(_pauseSelected);
    }

    private GameObject? _pausePanel;

    private void RebuildPause()
    {
        if (_pausePanel != null) UnityEngine.Object.Destroy(_pausePanel);
        _pausePanel = UiKit.Panel(Root, 660, 240, 600, 620, _t, "PausePanel");
        UiKit.Label(_pausePanel.transform, 0, 32, 600, 56, "ПАУЗА",
            _t.H1Size, _t.Gold, _t, TextAnchor.MiddleCenter, "Title");
        int y = 140;
        for (int i = 0; i < _pauseItems.Length; i++)
        {
            int idx = i;
            var btn = UiKit.WidgetButton(_pausePanel.transform, 80, y, 440,
                _t.ButtonHeight,
                (_pauseSelected == i ? "▶  " : "") + _pauseItems[i], _t.BodySize, _t);
            btn.onClick.AddListener(() => _ui.PauseChoice(idx));
            y += _t.ButtonHeight + 14;
        }
        UiKit.Label(_pausePanel.transform, 0, 550, 600, 24,
            "Esc — продолжить", _t.TinySize, _t.InkFaint, _t,
            TextAnchor.MiddleCenter, "Hint");
    }

    private const string ControlHelp =
        "WASD/стрелки — движение · Пробел — атака · E — взаимодействие\n" +
        "I — инвентарь · K — навыки · C — характеристики · Q — квесты\n" +
        "M — карта · B — большая карта · 1–8 — хотбар · F — зелье\n" +
        "T — говорить с NPC · Enter — диалог · Esc — пауза/назад";

    private const string CreditsText =
        "ЭТЕЛЬГАРД — роглайк-RPG (порт с Python/pygame на Unity).\n" +
        "Стиль интерфейса: Stoneshards (тёмное дерево, бронза, золото).\n" +
        "Все спрайты, тайлы и звуки генерируются процедурно.\n\n" +
        "Спасибо за игру!";
}

