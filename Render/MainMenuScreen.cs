using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
namespace Aethelgard.Render;

/// <summary>
/// Главное меню и подменю: сохранения, настройки, управление, создатели,
/// мультиплеер и ввод IP. Полный паритет с python game/menu_controller.py
/// + render/menu_renderer.py:
///
///   Главное:   Начать игру · Загрузить игру · Мультиплеер · Настройки ·
///              Создатели · Выйти
///   Пауза:     Вернуться в игру · Сохранить игру · Загрузить игру ·
///              Настройки · Выйти в главное меню
///   Мультиплеер: Продолжить/Начать новую (если есть сохранение MP),
///              Присоединиться — с вводом IP хоста.
/// </summary>
public class MainMenuScreen
{
    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _sb;
    // Загружаются в AethelgardGame.LoadContent до первого Draw.
    private SpriteFont _font = null!;
    private SpriteFont _fontBig = null!;
    private Texture2D? _blank;

    public enum Mode { Main, Saves, Settings, Controls, Credits, Multiplayer, MpJoin }
    public Mode CurrentMode = Mode.Main;

    public bool FromGame = false;   // меню открыто из паузы
    public bool SaveMode = false;   // экран сейвов: true — сохранить

    public int Selection;
    public int SelectionSaves;
    public int SelectionSettings;
    public int MpSelection;
    public string MpIp = "localhost";

    public static readonly int[][] Resolutions =
    {
        new[] { 1280, 720 }, new[] { 1366, 768 },
        new[] { 1600, 900 }, new[] { 1920, 1080 },
    };

    // ── Колбэки, задаёт AethelgardGame ──
    public Action? OnStartNew;             // «Начать игру» → выбор класса
    public Action<int>? OnSaveSlot;        // сохранить в слот 1-3
    public Action<int>? OnLoadSlot;        // загрузить слот 1-3
    public Action? OnResume;               // вернуться в игру (из паузы)
    public Action? OnExitToMenu;           // выйти в главное меню (из паузы)
    public Action? OnExit;                 // выход из игры
    public Action<bool>? OnMpHost;         // хост: false — продолжить, true — новая
    public Action? OnMpCreate;             // хост: создать игру (выбор класса)
    public Action<string>? OnMpJoin;       // подключиться к "host:port"
    public Action<int[]>? OnResolution;    // смена разрешения
    public Action<bool>? OnFullscreen;     // полный экран
    public Action<int>? OnVolume;          // громкость 0..10
    public Func<Settings>? GetSettings;

    private static readonly string[] MainItems =
        { "start", "load", "multiplayer", "settings", "credits", "exit" };

    private static readonly string[] PauseItems =
        { "resume", "save", "load", "settings", "exit_to_menu" };

    public MainMenuScreen(GraphicsDevice device, SpriteBatch sb)
    {
        _device = device;
        _sb = sb;
    }

    public void LoadFont(SpriteFont font, SpriteFont fontBig)
    {
        _font = font;
        _fontBig = fontBig;
    }

    public void OpenMain()
    {
        CurrentMode = Mode.Main;
        FromGame = false;
        Selection = 0;
    }

    public void OpenFromGame()
    {
        CurrentMode = Mode.Main;
        FromGame = true;
        Selection = 0;
    }

    public void OpenSaves(bool saveMode)
    {
        CurrentMode = Mode.Saves;
        SaveMode = saveMode;
        SelectionSaves = 0;
    }

    public void OpenSettings()
    {
        CurrentMode = Mode.Settings;
        SelectionSettings = 0;
    }

    private string[] Items => FromGame ? PauseItems : MainItems;

    private string[] Labels => FromGame
        ? new[] { "Вернуться в игру", "Сохранить игру", "Загрузить игру",
                  "Настройки", "Выйти в главное меню" }
        : new[] { "Начать игру", "Загрузить игру", "Мультиплеер", "Настройки",
                  "Создатели", "Выйти" };

    /// <summary>Есть ли сохранение мультиплеера (мир или игроки).</summary>
    public static bool HasMpSave()
    {
        foreach (var fname in new[] { "savegame_mp_world.json", "savegame_mp_players.json" })
            if (File.Exists(Path.Combine(Config.DataDir, fname)))
                return true;
        return false;
    }

    private string[] MpItems =>
        HasMpSave()
            ? new[] { "continue", "new", "join" }
            : new[] { "create", "join" };

    private string[] MpLabels =>
        HasMpSave()
            ? new[] { "Продолжить игру", "Начать новую игру", "Присоединиться" }
            : new[] { "Создать игру", "Присоединиться к игре" };

    // ── Геометрия кнопок (menu_controller.button_rects) ──
    private Rectangle MainButtonRect(int i) =>
        new((AethelgardGame.ScreenW - 400) / 2, 200 + i * 80, 400, 56);

    private Rectangle SavesRect(int i) =>
        new((AethelgardGame.ScreenW - 700) / 2, 180 + i * 130, 700, 110);

    private Rectangle SettingsRect(int i) =>
        new((AethelgardGame.ScreenW - 700) / 2, 160 + i * 60, 700, 46);

    private Rectangle ClassStartRect(int i) =>
        new((AethelgardGame.ScreenW - 460) / 2,
            240 + (MpItems.Length == 2 ? 40 : 0) + i * 80, 460, 60);

    private Rectangle MpJoinRect() =>
        new((AethelgardGame.ScreenW - 460) / 2, 380, 460, 56);

    // ─────────────────────────── INPUT ───────────────────────────

    /// <summary>
    /// Обработка клавиатуры. Вызывать до отрисовки.
    /// </summary>
    public void HandleInput(KeyboardState keys, KeyboardState prevKeys)
    {
        bool up = keys.IsKeyDown(Keys.Up) && !prevKeys.IsKeyDown(Keys.Up)
            || keys.IsKeyDown(Keys.W) && !prevKeys.IsKeyDown(Keys.W);
        bool down = keys.IsKeyDown(Keys.Down) && !prevKeys.IsKeyDown(Keys.Down)
            || keys.IsKeyDown(Keys.S) && !prevKeys.IsKeyDown(Keys.S);
        bool confirm = keys.IsKeyDown(Keys.Enter) && !prevKeys.IsKeyDown(Keys.Enter)
            || keys.IsKeyDown(Keys.Space) && !prevKeys.IsKeyDown(Keys.Space);
        bool back = keys.IsKeyDown(Keys.Escape) && !prevKeys.IsKeyDown(Keys.Escape);

        switch (CurrentMode)
        {
            case Mode.Main:
            {
                int n = Items.Length;
                if (up) Selection = (Selection + n - 1) % n;
                else if (down) Selection = (Selection + 1) % n;
                else if (confirm) Activate();
                else if (back) { if (FromGame) OnResume?.Invoke(); else OnExit?.Invoke(); }
                break;
            }
            case Mode.Saves:
            {
                if (up) SelectionSaves = (SelectionSaves + 2) % 3;
                else if (down) SelectionSaves = (SelectionSaves + 1) % 3;
                else if (confirm) SlotAction(SelectionSaves + 1);
                else if (keys.IsKeyDown(Keys.N) && !prevKeys.IsKeyDown(Keys.N)
                         && !SaveMode && !SaveSystem.SlotExists(SelectionSaves + 1))
                {
                    // Пустой слот — начать новую игру
                    OnStartNew?.Invoke();
                }
                else if (keys.IsKeyDown(Keys.D) && !prevKeys.IsKeyDown(Keys.D)
                         && !SaveMode)
                    SaveSystem.DeleteSlot(SelectionSaves + 1);
                else if (back) Back();
                break;
            }
            case Mode.Settings:
            {
                if (up) SelectionSettings = (SelectionSettings + 5) % 6;
                else if (down) SelectionSettings = (SelectionSettings + 1) % 6;
                else if (keys.IsKeyDown(Keys.Left) && !prevKeys.IsKeyDown(Keys.Left))
                    AdjustSetting(-1);
                else if (keys.IsKeyDown(Keys.Right) && !prevKeys.IsKeyDown(Keys.Right)
                         || confirm)
                    AdjustSetting(1);
                else if (back)
                {
                    GetSettings?.Invoke().Save();
                    Back();
                }
                break;
            }
            case Mode.Controls:
            case Mode.Credits:
                if (back || confirm) Back();
                break;
            case Mode.Multiplayer:
            {
                int n = MpItems.Length;
                if (up) MpSelection = (MpSelection + n - 1) % n;
                else if (down) MpSelection = (MpSelection + 1) % n;
                else if (confirm) ActivateMp();
                else if (back) Back();
                break;
            }
            case Mode.MpJoin:
            {
                bool ctrl = keys.IsKeyDown(Keys.LeftControl)
                    || keys.IsKeyDown(Keys.RightControl);
                if (keys.IsKeyDown(Keys.Back) && !prevKeys.IsKeyDown(Keys.Back))
                    MpIp = MpIp.Length > 0 ? MpIp[..^1] : MpIp;
                else if (confirm) ConnectMp();
                else if (back) CurrentMode = Mode.Multiplayer;
                else if (!ctrl)
                {
                    foreach (var (key, ch) in IpKeyMap)
                    {
                        if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key)
                            && MpIp.Length < 40)
                        {
                            MpIp += ch;
                            break;
                        }
                    }
                }
                break;
            }
        }
    }

    private static readonly Dictionary<Keys, char> IpKeyMap = new()
    {
        [Keys.A] = 'a', [Keys.B] = 'b', [Keys.C] = 'c', [Keys.D] = 'd',
        [Keys.E] = 'e', [Keys.F] = 'f', [Keys.G] = 'g', [Keys.H] = 'h',
        [Keys.I] = 'i', [Keys.J] = 'j', [Keys.K] = 'k', [Keys.L] = 'l',
        [Keys.M] = 'm', [Keys.N] = 'n', [Keys.O] = 'o', [Keys.P] = 'p',
        [Keys.Q] = 'q', [Keys.R] = 'r', [Keys.S] = 's', [Keys.T] = 't',
        [Keys.U] = 'u', [Keys.V] = 'v', [Keys.W] = 'w', [Keys.X] = 'x',
        [Keys.Y] = 'y', [Keys.Z] = 'z',
        [Keys.D0] = '0', [Keys.D1] = '1', [Keys.D2] = '2', [Keys.D3] = '3',
        [Keys.D4] = '4', [Keys.D5] = '5', [Keys.D6] = '6', [Keys.D7] = '7',
        [Keys.D8] = '8', [Keys.D9] = '9',
        [Keys.OemPeriod] = '.', [Keys.OemMinus] = '-', [Keys.OemSemicolon] = ':',
        [Keys.NumPad0] = '0', [Keys.NumPad1] = '1', [Keys.NumPad2] = '2',
        [Keys.NumPad3] = '3', [Keys.NumPad4] = '4', [Keys.NumPad5] = '5',
        [Keys.NumPad6] = '6', [Keys.NumPad7] = '7', [Keys.NumPad8] = '8',
        [Keys.NumPad9] = '9', [Keys.Decimal] = '.',
    };

    private void Activate()
    {
        if (FromGame)
        {
            switch (PauseItems[Math.Min(Selection, PauseItems.Length - 1)])
            {
                case "resume": OnResume?.Invoke(); break;
                case "save": OpenSaves(true); break;
                case "load": OpenSaves(false); break;
                case "settings": OpenSettings(); break;
                case "exit_to_menu": OnExitToMenu?.Invoke(); break;
            }
            return;
        }
        switch (MainItems[Math.Min(Selection, MainItems.Length - 1)])
        {
            case "start": OnStartNew?.Invoke(); break;
            case "load": OpenSaves(false); break;
            case "multiplayer":
                MpSelection = 0;
                CurrentMode = Mode.Multiplayer;
                break;
            case "settings": OpenSettings(); break;
            case "credits": CurrentMode = Mode.Credits; break;
            case "exit": OnExit?.Invoke(); break;
        }
    }

    private void ActivateMp()
    {
        var items = MpItems;
        if (MpSelection >= items.Length) { MpSelection = 0; return; }
        switch (items[MpSelection])
        {
            case "continue": OnMpHost?.Invoke(false); break;   // продолжить
            case "new": OnMpHost?.Invoke(true); break;         // новая игра
            case "create": OnMpCreate?.Invoke(); break;        // создать
            case "join": CurrentMode = Mode.MpJoin; break;
        }
    }

    private void ConnectMp()
    {
        string host = MpIp.Trim().Length > 0 ? MpIp.Trim() : "localhost";
        int port = int.TryParse(Environment.GetEnvironmentVariable("ETHELGARD_PORT"),
            out var p) ? p : 9000;
        if (port <= 0 || port >= 65536) port = 9000;
        // Формат "host:port"
        var parts = host.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[1], out var hp)
            && hp > 0 && hp < 65536)
        {
            host = parts[0];
            port = hp;
        }
        OnMpJoin?.Invoke($"{host}:{port}");
    }

    private void SlotAction(int slot)
    {
        if (SaveMode)
        {
            if (SaveSystem.SlotExists(slot))
                OnSaveSlot?.Invoke(slot);
            else
                OnSaveSlot?.Invoke(slot);
        }
        else if (SaveSystem.SlotExists(slot))
            OnLoadSlot?.Invoke(slot);
    }

    private void AdjustSetting(int delta)
    {
        var s = GetSettings?.Invoke();
        if (s == null) return;
        switch (SelectionSettings)
        {
            case 0: // Разрешение
                int idx = 0;
                for (int i = 0; i < Resolutions.Length; i++)
                    if (Resolutions[i][0] == s.Resolution[0]
                        && Resolutions[i][1] == s.Resolution[1]) idx = i;
                idx = (idx + delta + Resolutions.Length) % Resolutions.Length;
                s.Resolution = new[] { Resolutions[idx][0], Resolutions[idx][1] };
                s.Fullscreen = false;
                OnResolution?.Invoke(s.Resolution);
                break;
            case 1: // Полный экран
                s.Fullscreen = !s.Fullscreen;
                OnFullscreen?.Invoke(s.Fullscreen);
                break;
            case 2: // Громкость
                s.Volume = Math.Clamp(s.Volume + delta, 0, 10);
                OnVolume?.Invoke(s.Volume);
                break;
            case 3: // FPS
                s.ShowFps = !s.ShowFps;
                break;
            case 4: // Мини-карта
                s.ShowMinimap = !s.ShowMinimap;
                break;
            case 5: // Управление
                CurrentMode = Mode.Controls;
                break;
        }
    }

    private void Back()
    {
        if (FromGame && CurrentMode == Mode.Saves)
        {
            CurrentMode = Mode.Main;
            return;
        }
        CurrentMode = Mode.Main;
        Selection = 0;
    }

    // ─────────────────────────── MOUSE ───────────────────────────

    public void HandleMouse(MouseState mouse, MouseState prevMouse)
    {
        bool click = mouse.LeftButton == ButtonState.Pressed
            && prevMouse.LeftButton != ButtonState.Pressed;
        if (!click) return;

        var pos = new Rectangle(mouse.X, mouse.Y, 1, 1);
        switch (CurrentMode)
        {
            case Mode.Main:
                for (int i = 0; i < Items.Length; i++)
                {
                    if (MainButtonRect(i).Intersects(pos))
                    {
                        Selection = i;
                        Activate();
                        return;
                    }
                }
                break;
            case Mode.Saves:
                for (int i = 0; i < 3; i++)
                {
                    if (SavesRect(i).Intersects(pos))
                    {
                        SelectionSaves = i;
                        SlotAction(i + 1);
                        return;
                    }
                }
                break;
            case Mode.Settings:
                for (int i = 0; i < 6; i++)
                {
                    if (SettingsRect(i).Intersects(pos))
                    {
                        SelectionSettings = i;
                        if (i == 5) CurrentMode = Mode.Controls;
                        else AdjustSetting(1);
                        return;
                    }
                }
                break;
            case Mode.Multiplayer:
                for (int i = 0; i < MpItems.Length; i++)
                {
                    if (ClassStartRect(i).Intersects(pos))
                    {
                        MpSelection = i;
                        ActivateMp();
                        return;
                    }
                }
                break;
            case Mode.MpJoin:
                if (MpJoinRect().Intersects(pos)) ConnectMp();
                break;
        }
    }

    // ─────────────────────────── DRAW ───────────────────────────

    public void Draw(GameController game)
    {
        int sw = AethelgardGame.ScreenW, sh = AethelgardGame.ScreenH;
        _sb.Draw(Blank, new Rectangle(0, 0, sw, sh), new Color(14, 12, 10));
        GradientBg();
        if (_font == null || _fontBig == null) return;

        switch (CurrentMode)
        {
            case Mode.Main: DrawMain(); break;
            case Mode.Saves: DrawSaves(game); break;
            case Mode.Settings: DrawSettings(game); break;
            case Mode.Controls: DrawControls(); break;
            case Mode.Credits: DrawCredits(); break;
            case Mode.Multiplayer: DrawMultiplayer(); break;
            case Mode.MpJoin: DrawMpJoin(); break;
        }
    }

    private void GradientBg()
    {
        // Вертикальный градиент (10,9,7) → (30,24,18), как в python
        int h = AethelgardGame.ScreenH;
        for (int y = 0; y < h; y++)
        {
            float t = (float)y / h;
            var c = new Color(
                (int)(10 + t * 20), (int)(9 + t * 15), (int)(7 + t * 11));
            _sb.Draw(Blank, new Rectangle(0, y, AethelgardGame.ScreenW, 1), c);
        }
    }

    private void DrawTitle(string title, int y = 100)
    {
        int cx = AethelgardGame.ScreenW / 2;
        var size = _fontBig!.MeasureString(title);
        _sb.DrawString(_fontBig, title,
            new Vector2(cx - size.X / 2 + 3, y + 3), new Color(0, 0, 0));
        _sb.DrawString(_fontBig, title,
            new Vector2(cx - size.X / 2, y), new Color(214, 178, 98));
    }

    private void DrawSubtitle(string text, int y = 172)
    {
        int cx = AethelgardGame.ScreenW / 2;
        var size = _font.MeasureString(text);
        _sb.DrawString(_font, text,
            new Vector2(cx - size.X / 2, y), new Color(130, 120, 105));
    }

    private void DrawMain()
    {
        DrawTitle("ЭТЕЛЬГАРД");
        DrawSubtitle("Открытый мир · Подземелья · Дракон");

        var labels = Labels;
        for (int i = 0; i < labels.Length; i++)
        {
            var rect = MainButtonRect(i);
            bool sel = i == Selection;
            DrawButtonBg(rect, sel);
            var color = sel ? new Color(238, 222, 188) : new Color(200, 190, 170);
            var size = _fontBig.MeasureString(labels[i]);
            _sb.DrawString(_fontBig, labels[i],
                new Vector2(rect.X + (rect.Width - size.X) / 2,
                            rect.Y + (rect.Height - size.Y) / 2), color);
            if (sel)
                DrawArrow(rect.X - 22, rect.Y + rect.Height / 2);
        }

        if (FromGame)
        {
            DrawHint("Esc — вернуться в игру", AethelgardGame.ScreenH - 40);
        }
    }

    private void DrawButtonBg(Rectangle rect, bool selected)
    {
        _sb.Draw(Blank, rect,
            selected ? new Color(48, 40, 28) : new Color(26, 22, 18));
        _sb.Draw(Blank, new Rectangle(rect.X, rect.Y, rect.Width, 2),
            selected ? new Color(206, 166, 82) : new Color(80, 68, 50));
        _sb.Draw(Blank, new Rectangle(rect.X, rect.Y + rect.Height - 2,
            rect.Width, 2), selected ? new Color(120, 96, 52) : new Color(50, 42, 32));
    }

    private void DrawArrow(int x, int y)
    {
        _sb.Draw(Blank, new Rectangle(x, y - 7, 4, 14), new Color(206, 166, 82));
        _sb.Draw(Blank, new Rectangle(x + 4, y - 4, 4, 8), new Color(206, 166, 82));
        _sb.Draw(Blank, new Rectangle(x + 8, y - 1, 4, 2), new Color(206, 166, 82));
    }

    private void DrawHint(string text, int y)
    {
        int cx = AethelgardGame.ScreenW / 2;
        var size = _font.MeasureString(text);
        _sb.DrawString(_font, text, new Vector2(cx - size.X / 2, y),
            new Color(110, 102, 90));
    }

    private void DrawSaves(GameController game)
    {
        DrawTitle(SaveMode ? "СОХРАНИТЬ ИГРУ" : "ЗАГРУЗИТЬ ИГРУ", 80);

        for (int slot = 1; slot <= 3; slot++)
        {
            var rect = SavesRect(slot - 1);
            bool sel = slot - 1 == SelectionSaves;
            bool exists = SaveSystem.SlotExists(slot);
            DrawButtonBg(rect, sel);
            if (sel) DrawArrow(rect.X - 22, rect.Y + rect.Height / 2);

            _sb.DrawString(_fontBig, $"Слот {slot}",
                new Vector2(rect.X + 76, rect.Y + 14),
                sel ? new Color(238, 222, 188) : new Color(200, 190, 170));

            if (exists)
            {
                var info = SaveSystem.SlotInfo(slot);
                if (info.HasValue)
                {
                    _sb.DrawString(_font,
                        $"{info.Value.Class}  Ур.{info.Value.Level}",
                        new Vector2(rect.X + 76, rect.Y + 50), Config.ColGold);
                    _sb.DrawString(_font,
                        info.Value.Timestamp.ToString("dd.MM.yyyy HH:mm"),
                        new Vector2(rect.X + rect.Width - 190, rect.Y + 16),
                        new Color(120, 112, 100));
                }
                if (SaveMode)
                    _sb.DrawString(_font, "клик перезапишет",
                        new Vector2(rect.X + rect.Width - 190, rect.Y + 80),
                        new Color(208, 158, 82));
            }
            else
            {
                _sb.DrawString(_font,
                    SaveMode ? "Пусто — клик сохранит сюда"
                             : "Пусто — начните новую игру (N)",
                    new Vector2(rect.X + 76, rect.Y + 50), new Color(120, 112, 100));
            }
        }

        DrawHint(SaveMode
            ? "Enter/клик — сохранить · Esc — назад"
            : "Enter/клик — загрузить · N — новая игра · D — удалить · Esc — назад",
            AethelgardGame.ScreenH - 40);
    }

    private void DrawSettings(GameController game)
    {
        DrawTitle("НАСТРОЙКИ");
        var s = GetSettings?.Invoke();
        if (s == null) return;

        string[][] rows =
        {
            new[] { "Разрешение", $"{s.Resolution[0]}×{s.Resolution[1]}" },
            new[] { "Полный экран", s.Fullscreen ? "Вкл" : "Выкл" },
            new[] { "Громкость", $"{s.Volume} / 10" },
            new[] { "Показывать FPS", s.ShowFps ? "Вкл" : "Выкл" },
            new[] { "Мини-карта", s.ShowMinimap ? "Вкл" : "Выкл" },
            new[] { "Управление", "→" },
        };

        for (int i = 0; i < rows.Length; i++)
        {
            var rect = SettingsRect(i);
            bool sel = i == SelectionSettings;
            DrawButtonBg(rect, sel);
            _sb.DrawString(_fontBig, rows[i][0],
                new Vector2(rect.X + 28, rect.Y + 8),
                sel ? new Color(238, 222, 188) : new Color(200, 190, 170));
            var valColor = sel ? new Color(206, 166, 82) : new Color(140, 132, 118);
            _sb.DrawString(_fontBig, rows[i][1],
                new Vector2(rect.X + rect.Width - 130, rect.Y + 8), valColor);
            if (sel && i != 5)
            {
                DrawArrow(rect.X + rect.Width - 200, rect.Y + rect.Height / 2);
            }
        }

        DrawHint("←/→ — изменить · Esc — назад (настройки сохраняются)",
            AethelgardGame.ScreenH - 40);
    }

    private void DrawControls()
    {
        DrawTitle("УПРАВЛЕНИЕ", 60);

        (string, (string, string)[])[] sections =
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
            }),
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
                ("F12 / `", "Консоль разработчика (по паролю)"),
                ("Esc", "Меню / закрыть окно"),
            }),
        };

        var columns = new[] { sections.Take(2).ToArray(), sections.Skip(2).ToArray() };
        int[] colXs = { 100, 700 };
        for (int c = 0; c < columns.Length; c++)
        {
            int y = 132;
            foreach (var (cat, rows) in columns[c])
            {
                _sb.DrawString(_fontBig, cat, new Vector2(colXs[c], y),
                    new Color(214, 178, 98));
                y += 32;
                foreach (var (keyStr, desc) in rows)
                {
                    if (keyStr.Length > 0)
                        _sb.DrawString(_font, keyStr, new Vector2(colXs[c] + 20, y),
                            new Color(196, 176, 138));
                    _sb.DrawString(_font, desc, new Vector2(colXs[c] + 200, y),
                        new Color(214, 206, 192));
                    y += 26;
                }
                y += 10;
            }
        }

        DrawHint("Esc — назад", AethelgardGame.ScreenH - 40);
    }

    private void DrawCredits()
    {
        DrawTitle("СОЗДАТЕЛИ", 56);
        DrawSubtitle("Игра разработана командой EVA", 128);

        (string, string[])[] sections =
        {
            ("Создатели", new[] { "Dodik", "Don't talk to people", "And don't write" }),
            ("Художник", new[] { "Лизабет еБатори" }),
        };
        int cx = AethelgardGame.ScreenW / 2;
        int y = 190;
        foreach (var (cat, names) in sections)
        {
            var catSize = _fontBig.MeasureString(cat);
            _sb.DrawString(_fontBig, cat, new Vector2(cx - catSize.X / 2, y),
                new Color(214, 178, 98));
            y += 40;
            foreach (var name in names)
            {
                var size = _font.MeasureString(name);
                _sb.DrawString(_font, name, new Vector2(cx - size.X / 2, y),
                    new Color(214, 206, 192));
                y += 32;
            }
            y += 16;
        }
        _sb.DrawString(_font, "Все права на изображения принадлежат ей",
            new Vector2(cx - _font.MeasureString("Все права на изображения принадлежат ей").X / 2,
                y + 8), new Color(120, 112, 100));
        var verSize = _font.MeasureString("от 11.2026");
        _sb.DrawString(_font, "от 11.2026",
            new Vector2(cx - verSize.X / 2, AethelgardGame.ScreenH - 84),
            new Color(206, 166, 82));
        DrawHint("Esc — назад", AethelgardGame.ScreenH - 40);
    }

    private void DrawMultiplayer()
    {
        DrawTitle("МУЛЬТИПЛЕЕР");
        DrawSubtitle("Играй с другом по локальной сети");

        if (HasMpSave())
            DrawSubtitle("● Есть сохранение мультиплеера", 180);
        else
            DrawSubtitle("Сохранения нет", 180);

        var labels = MpLabels;
        for (int i = 0; i < labels.Length; i++)
        {
            var rect = ClassStartRect(i);
            bool sel = i == MpSelection;
            DrawButtonBg(rect, sel);
            var size = _fontBig.MeasureString(labels[i]);
            _sb.DrawString(_fontBig, labels[i],
                new Vector2(rect.X + (rect.Width - size.X) / 2,
                            rect.Y + (rect.Height - size.Y) / 2),
                sel ? new Color(238, 222, 188) : new Color(200, 190, 170));
            if (sel) DrawArrow(rect.X - 22, rect.Y + rect.Height / 2);
        }

        DrawHint("Esc — назад", AethelgardGame.ScreenH - 40);
    }

    private void DrawMpJoin()
    {
        DrawTitle("ПРИСОЕДИНИТЬСЯ");
        int cx = AethelgardGame.ScreenW / 2;
        var prompt = "Введи IP-адрес хоста:";
        var pSize = _font.MeasureString(prompt);
        _sb.DrawString(_font, prompt, new Vector2(cx - pSize.X / 2, 200),
            new Color(214, 206, 192));

        var field = new Rectangle(cx - 230, 240, 460, 56);
        _sb.Draw(Blank, field, new Color(24, 21, 17));
        _sb.Draw(Blank, new Rectangle(field.X, field.Y, field.Width, 2),
            new Color(110, 91, 62));
        _sb.Draw(Blank, new Rectangle(field.X, field.Bottom - 2, field.Width, 2),
            new Color(110, 91, 62));
        _sb.Draw(Blank, new Rectangle(field.X, field.Y, 2, field.Height),
            new Color(110, 91, 62));
        _sb.Draw(Blank, new Rectangle(field.Right - 2, field.Y, 2, field.Height),
            new Color(110, 91, 62));

        bool cursor = Environment.TickCount / 500 % 2 == 0;
        string text = MpIp + (cursor ? "_" : "");
        var tSize = _fontBig.MeasureString(text);
        _sb.DrawString(_fontBig, text,
            new Vector2(cx - tSize.X / 2, field.Y + field.Height / 2 - tSize.Y / 2),
            new Color(228, 220, 200));

        DrawSubtitle("Например: localhost или 192.168.1.5", 316);

        var btn = MpJoinRect();
        DrawButtonBg(btn, true);
        var bSize = _fontBig.MeasureString("Подключиться");
        _sb.DrawString(_fontBig, "Подключиться",
            new Vector2(btn.X + (btn.Width - bSize.X) / 2,
                        btn.Y + (btn.Height - bSize.Y) / 2),
            new Color(238, 222, 188));

        DrawHint("Enter — подключиться · Backspace — стереть · Esc — назад",
            AethelgardGame.ScreenH - 40);
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
