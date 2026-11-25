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
    // Анимированный фон главного меню (render/menu_background.py).
    private readonly MenuBackground _bg;
    private readonly System.Diagnostics.Stopwatch _clock =
        System.Diagnostics.Stopwatch.StartNew();
    private int _mouseX = -1, _mouseY = -1;

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
    public Action<int>? OnStartNew;        // «Начать игру» → выбор класса (слот)
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

    private static readonly string[] PauseLabels =
        { "Вернуться в игру", "Сохранить игру", "Загрузить игру",
          "Настройки", "Выйти в главное меню" };

    public MainMenuScreen(GraphicsDevice device, SpriteBatch sb)
    {
        _device = device;
        _sb = sb;
        _bg = new MenuBackground(device);
    }

    public void LoadFont(SpriteFont font, SpriteFont fontBig)
    {
        _font = font;
        _fontBig = fontBig;
    }

    // Набор шрифтов python theme.py с фолбэком на загруженные.
    private SpriteFont F1 => Fonts.Ready ? Fonts.H1 : _fontBig;
    private SpriteFont F2 => Fonts.Ready ? Fonts.H2 : _fontBig;
    private SpriteFont FBody => _font;
    private SpriteFont FSmall => Fonts.Ready ? Fonts.Small : _font;
    private SpriteFont FTiny => Fonts.Ready ? Fonts.Tiny : _font;

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
        // Python: центр по вертикали — 2 кнопки → y=240, 3 кнопки → y=200.
        new((AethelgardGame.ScreenW - 460) / 2,
            (MpItems.Length == 2 ? 240 : 200) + i * 80, 460, 60);

    private Rectangle MpJoinRect() =>
        new((AethelgardGame.ScreenW - 460) / 2, 380, 460, 56);

    // Общая кнопка «Назад» во всех подменю (menu_controller.BACK_RECT).
    private static readonly Rectangle BackRect = new(34, 28, 52, 48);

    // Мусорка у заполненного слота (menu_controller.slot_delete_rect).
    private Rectangle SlotDeleteRect(int i)
    {
        var r = SavesRect(i);
        return new Rectangle(r.X + r.Width - 32 - 10, r.Y + 10, 32, 32);
    }

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
                    // Пустой слот — начать новую игру именно в этом слоте.
                    OnStartNew?.Invoke(SelectionSaves + 1);
                }
                else if (keys.IsKeyDown(Keys.D) && !prevKeys.IsKeyDown(Keys.D))
                    SaveSystem.DeleteSlot(SelectionSaves + 1);
                else if (back) Back();
                break;
            }
            case Mode.Settings:
            {
                if (up) SelectionSettings = (SelectionSettings + 5) % 6;
                else if (down) SelectionSettings = (SelectionSettings + 1) % 6;
                else if (keys.IsKeyDown(Keys.Left) && !prevKeys.IsKeyDown(Keys.Left)
                         || keys.IsKeyDown(Keys.A) && !prevKeys.IsKeyDown(Keys.A))
                    AdjustSetting(-1);
                else if (keys.IsKeyDown(Keys.Right) && !prevKeys.IsKeyDown(Keys.Right)
                         || keys.IsKeyDown(Keys.D) && !prevKeys.IsKeyDown(Keys.D)
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
                // Python: Esc/Enter/Space → меню настроек (пункт «Управление»).
                if (back || confirm) { CurrentMode = Mode.Settings; SelectionSettings = 5; }
                break;
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
                // Ctrl+V — вставить адрес из буфера (menu_controller._paste_mp_ip).
                if (ctrl && keys.IsKeyDown(Keys.V) && !prevKeys.IsKeyDown(Keys.V))
                    PasteMpIp();
                else if (keys.IsKeyDown(Keys.Back) && !prevKeys.IsKeyDown(Keys.Back))
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
            case "start": OnStartNew?.Invoke(1); break;
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

    /// <summary>
    /// Вставить адрес из буфера обмена, отбросив лишние символы
    /// (game/menu_controller.py:_paste_mp_ip). Допустимы только символы
    /// IPv4 / имени хоста / порта; итоговая длина — не больше 40.
    /// </summary>
    private void PasteMpIp()
    {
        string text = ClipboardText.Read();
        if (text.Length == 0) return;
        var cleaned = new System.Text.StringBuilder();
        foreach (char raw in text.Trim().ToLowerInvariant())
        {
            if (raw is >= 'a' and <= 'z' or >= '0' and <= '9'
                or '.' or '-' or ':')
                cleaned.Append(raw);
        }
        if (cleaned.Length == 0) return;
        int room = Math.Max(0, 40 - MpIp.Length);
        MpIp += cleaned.ToString(0, Math.Min(room, cleaned.Length));
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

    private MouseState _lastMouse;   // для подсветки кнопок под курсором

    public void HandleMouse(MouseState mouse, MouseState prevMouse)
    {
        _lastMouse = mouse;
        _mouseX = mouse.X; _mouseY = mouse.Y;
        bool click = mouse.LeftButton == ButtonState.Pressed
            && prevMouse.LeftButton != ButtonState.Pressed;

        // Наведение меняет выбор — только при движении мыши
        // (menu_controller.handle_mouse_motion).
        if (!click)
        {
            if (mouse.X != prevMouse.X || mouse.Y != prevMouse.Y)
                HoverSelect();
            return;
        }

        var pos = new Rectangle(mouse.X, mouse.Y, 1, 1);

        // Общая кнопка «Назад» во всех подменю.
        if (CurrentMode != Mode.Main && BackRect.Intersects(pos))
        {
            MenuBack();
            return;
        }

        // Кликабельные стрелки ◄ ► в настройках.
        if (CurrentMode == Mode.Settings)
        {
            foreach (var (row, dir, rect) in SettingsArrowRects())
            {
                if (rect.Intersects(pos))
                {
                    SelectionSettings = row;
                    AdjustSetting(dir);
                    return;
                }
            }
        }

        // Мусорка у заполненного слота.
        if (CurrentMode == Mode.Saves)
        {
            for (int i = 0; i < 3; i++)
            {
                if (SaveSystem.SlotExists(i + 1) && SlotDeleteRect(i).Intersects(pos))
                {
                    SaveSystem.DeleteSlot(i + 1);
                    if (SelectionSaves >= 3) SelectionSaves = 0;
                    return;
                }
            }
        }

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
                // Клик по строке только выбирает (python menu_controller).
                for (int i = 0; i < 6; i++)
                {
                    if (SettingsRect(i).Intersects(pos))
                    {
                        SelectionSettings = i;
                        if (i == 5) CurrentMode = Mode.Controls;
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

    private void HoverSelect()
    {
        var pos = new Rectangle(_mouseX, _mouseY, 1, 1);
        switch (CurrentMode)
        {
            case Mode.Main:
                for (int i = 0; i < Items.Length; i++)
                    if (MainButtonRect(i).Intersects(pos)) Selection = i;
                break;
            case Mode.Saves:
                for (int i = 0; i < 3; i++)
                    if (SavesRect(i).Intersects(pos)) SelectionSaves = i;
                break;
            case Mode.Settings:
                for (int i = 0; i < 6; i++)
                    if (SettingsRect(i).Intersects(pos)) SelectionSettings = i;
                break;
            case Mode.Multiplayer:
                for (int i = 0; i < MpItems.Length; i++)
                    if (ClassStartRect(i).Intersects(pos)) MpSelection = i;
                break;
        }
    }

    /// <summary>Кнопка «Назад»: возврат на уровень вверх (menu_controller.menu_back).</summary>
    private void MenuBack()
    {
        switch (CurrentMode)
        {
            case Mode.Settings:
                GetSettings?.Invoke().Save();
                Back();
                break;
            case Mode.Controls:
                CurrentMode = Mode.Settings;
                SelectionSettings = 5;
                break;
            case Mode.MpJoin:
                CurrentMode = Mode.Multiplayer;
                break;
            default:
                Back();
                break;
        }
    }

    /// <summary>Кликабельные стрелки ◄ ► у значений настроек.</summary>
    private IEnumerable<(int Row, int Dir, Rectangle Rect)> SettingsArrowRects()
    {
        var s = GetSettings?.Invoke();
        if (s == null || _font == null) yield break;
        string[] vals =
        {
            $"{s.Resolution[0]}×{s.Resolution[1]}",
            s.Fullscreen ? "Вкл" : "Выкл",
            $"{s.Volume} / 10",
            s.ShowFps ? "Вкл" : "Выкл",
            s.ShowMinimap ? "Вкл" : "Выкл",
        };
        for (int i = 0; i < 5; i++)
        {
            var r = SettingsRect(i);
            int vc = r.X + r.Width - 100;
            int vw = (int)F2.MeasureString(vals[i]).X;
            int ay = r.Y + r.Height / 2;
            int lb = vc - vw / 2 - 16;
            int rb = vc + vw / 2 + 16;
            // Зоны совпадают с нарисованными треугольниками.
            yield return (i, -1, new Rectangle(lb - 12, ay - 10, 14, 20));
            yield return (i, 1, new Rectangle(rb - 2, ay - 10, 14, 20));
        }
    }

    // ─────────────────────────── DRAW ───────────────────────────

    /// <summary>
    /// Меню паузы (python: screen_mode == 'menu' с from_game=True рисует
    /// тот же draw_main). Использует выделение PauseScreen.
    /// </summary>
    public void DrawPause(int selection)
    {
        int sw = AethelgardGame.ScreenW, sh = AethelgardGame.ScreenH;
        _bg.Draw(_sb, sw, sh, (float)_clock.Elapsed.TotalSeconds);
        _sb.Draw(Blank, new Rectangle(0, 0, sw, sh), new Color(6, 5, 4, 70));
        if (_font == null) return;

        DrawTitle("ЭТЕЛЬГАРД");
        int cx = sw / 2;
        _sb.Draw(Blank, new Rectangle(cx - 220, 155, 440, 1), new Color(96, 78, 46));
        _sb.Draw(Blank, new Rectangle(cx - 150, 158, 300, 1), new Color(176, 142, 82));
        DrawSubtitle("Открытый мир · Подземелья · Дракон", 172, FSmall,
            new Color(150, 140, 120));

        for (int i = 0; i < PauseItems.Length; i++)
        {
            var rect = MainButtonRect(i);
            bool sel = i == selection;
            DrawButtonBg(rect, sel);
            var color = sel ? new Color(238, 222, 188) : new Color(200, 190, 170);
            var size = F2.MeasureString(PauseLabels[i]);
            _sb.DrawString(F2, PauseLabels[i],
                new Vector2(rect.X + (rect.Width - size.X) / 2,
                            rect.Y + (rect.Height - size.Y) / 2), color);
            if (sel) DrawArrow(rect.X - 22, rect.Y + rect.Height / 2);
        }
    }

    public void Draw(GameController game)
    {
        int sw = AethelgardGame.ScreenW, sh = AethelgardGame.ScreenH;
        if (CurrentMode == Mode.Main)
        {
            // Анимированный фон: полёт камеры над миром + угольки + виньетка
            // (render/menu_background.py), поверх — затемняющий слой.
            _bg.Draw(_sb, sw, sh, (float)_clock.Elapsed.TotalSeconds);
            _sb.Draw(Blank, new Rectangle(0, 0, sw, sh),
                new Color(6, 5, 4, 70));
        }
        else
        {
            _sb.Draw(Blank, new Rectangle(0, 0, sw, sh), new Color(14, 12, 10));
            GradientBg();
        }
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

        // Единая кнопка «Назад» во всех подменю (menu_renderer.draw_back_button).
        if (CurrentMode != Mode.Main) DrawBackButton();
    }

    private void DrawBackButton()
    {
        var rect = BackRect;
        bool hover = rect.Contains(_mouseX, _mouseY);
        DrawButtonBg(rect, hover);
        var color = hover ? new Color(238, 222, 188) : new Color(200, 190, 170);
        int cx = rect.X + rect.Width / 2, cy = rect.Y + rect.Height / 2;
        int shaft = 10, head = 8;
        // Вал стрелки влево
        _sb.Draw(Blank, new Rectangle(cx - shaft / 2, cy - 1, shaft + head / 2, 3), color);
        // Наконечник — три полоски убывающей высоты
        int tip = cx - shaft / 2 - head;
        _sb.Draw(Blank, new Rectangle(tip + head - 3, cy - 7, 3, 15), color);
        _sb.Draw(Blank, new Rectangle(tip + head - 6, cy - 4, 3, 9), color);
        _sb.Draw(Blank, new Rectangle(tip, cy - 1, 3, 3), color);
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
        var size = F1.MeasureString(title);
        _sb.DrawString(F1, title,
            new Vector2(cx - size.X / 2 + 3, y + 3), new Color(0, 0, 0));
        _sb.DrawString(F1, title,
            new Vector2(cx - size.X / 2, y), new Color(214, 178, 98));
    }

    private void DrawSubtitle(string text, int y = 172, SpriteFont? font = null,
        Color? color = null)
    {
        int cx = AethelgardGame.ScreenW / 2;
        var f = font ?? FSmall;
        var size = f.MeasureString(text);
        _sb.DrawString(f, text,
            new Vector2(cx - size.X / 2, y), color ?? new Color(150, 140, 120));
    }

    private void DrawMain()
    {
        DrawTitle("ЭТЕЛЬГАРД");
        // Две линии-разделителя под заголовком (menu_renderer.draw_main).
        int cx = AethelgardGame.ScreenW / 2;
        _sb.Draw(Blank, new Rectangle(cx - 220, 155, 440, 1), new Color(96, 78, 46));
        _sb.Draw(Blank, new Rectangle(cx - 150, 158, 300, 1), new Color(176, 142, 82));
        DrawSubtitle("Открытый мир · Подземелья · Дракон", 172, FSmall,
            new Color(150, 140, 120));

        var labels = Labels;
        for (int i = 0; i < labels.Length; i++)
        {
            var rect = MainButtonRect(i);
            bool sel = i == Selection;
            DrawButtonBg(rect, sel);
            var color = sel ? new Color(238, 222, 188) : new Color(200, 190, 170);
            var size = F2.MeasureString(labels[i]);
            _sb.DrawString(F2, labels[i],
                new Vector2(rect.X + (rect.Width - size.X) / 2,
                            rect.Y + (rect.Height - size.Y) / 2), color);
            if (sel)
                DrawArrow(rect.X - 22, rect.Y + rect.Height / 2);
        }
    }

    /// <summary>Подсвечена ли кнопка курсором (hover).</summary>
    private bool Hovered(Rectangle rect)
    {
        return _lastMouse.X >= rect.X && _lastMouse.X < rect.Right
            && _lastMouse.Y >= rect.Y && _lastMouse.Y < rect.Bottom;
    }

    private void DrawButtonBg(Rectangle rect, bool selected)
    {
        // Тень под кнопкой (base_renderer._button_parts: смещение ≈ +2).
        _sb.Draw(Blank, new Rectangle(rect.X + 6, rect.Y + 8, rect.Width, rect.Height),
            new Color(0, 0, 0, 110));

        // Вертикальный градиент: выбранная (96,76,44)→(56,44,25),
        // обычная (44,39,31)→(24,21,17).
        Color topC = selected ? new Color(96, 76, 44) : new Color(44, 39, 31);
        Color botC = selected ? new Color(56, 44, 25) : new Color(24, 21, 17);
        for (int i = 0; i < rect.Height; i++)
        {
            float t = rect.Height <= 1 ? 0 : (float)i / (rect.Height - 1);
            var c = new Color(
                (int)(topC.R * (1 - t) + botC.R * t),
                (int)(topC.G * (1 - t) + botC.G * t),
                (int)(topC.B * (1 - t) + botC.B * t));
            _sb.Draw(Blank, new Rectangle(rect.X, rect.Y + i, rect.Width, 1), c);
        }

        // Рамка по всему периметру
        var border = selected ? new Color(206, 166, 82) : new Color(72, 62, 46);
        _sb.Draw(Blank, new Rectangle(rect.X, rect.Y, rect.Width, 2), border);
        _sb.Draw(Blank, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), border);
        _sb.Draw(Blank, new Rectangle(rect.X, rect.Y, 2, rect.Height), border);
        _sb.Draw(Blank, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), border);

        if (selected)
        {
            // Верхняя подсветка и внутренний золотой тинт.
            _sb.Draw(Blank, new Rectangle(rect.X + 10, rect.Y + 1, rect.Width - 20, 1),
                new Color(236, 206, 142));
            _sb.Draw(Blank, new Rectangle(rect.X + 2, rect.Y + 2,
                rect.Width - 4, rect.Height - 4), new Color(206, 166, 82, 24));
        }
    }

    private void DrawArrow(int x, int y)
    {
        // Треугольник ► (polygon (ax,ay-7),(ax+12,ay),(ax,ay+7)).
        var color = new Color(206, 166, 82);
        for (int i = 0; i < 12; i++)
        {
            int h = (int)Math.Round(14.0 * (12 - i) / 12.0);
            if (h <= 0) continue;
            _sb.Draw(Blank, new Rectangle(x + i, y - h / 2, 1, h), color);
        }
    }

    /// <summary>Треугольник ◄ с основанием в baseX (вершина левее).</summary>
    private void DrawArrowLeft(int baseX, int y)
    {
        var color = new Color(176, 142, 82);
        for (int i = 0; i < 10; i++)
        {
            int h = (int)Math.Round(14.0 * i / 9.0);
            if (h <= 0) continue;
            _sb.Draw(Blank, new Rectangle(baseX - 10 + i, y - h / 2, 1, h), color);
        }
    }

    /// <summary>Треугольник ► с основанием в baseX (вершина правее).</summary>
    private void DrawArrowRight(int baseX, int y)
    {
        var color = new Color(206, 166, 82);
        for (int i = 0; i < 10; i++)
        {
            int h = (int)Math.Round(14.0 * (10 - i) / 10.0);
            if (h <= 0) continue;
            _sb.Draw(Blank, new Rectangle(baseX + i, y - h / 2, 1, h), color);
        }
    }

    private void DrawHint(string text, int y)
    {
        int cx = AethelgardGame.ScreenW / 2;
        var size = FSmall.MeasureString(text);
        _sb.DrawString(FSmall, text, new Vector2(cx - size.X / 2, y),
            new Color(98, 90, 74));
    }

    private void DrawSaves(GameController game)
    {
        DrawTitle(SaveMode ? "СОХРАНИТЬ ИГРУ" : "ЗАГРУЗИТЬ ИГРУ", 80);

        for (int slot = 1; slot <= 3; slot++)
        {
            var rect = SavesRect(slot - 1);
            bool sel = slot - 1 == SelectionSaves;
            bool exists = SaveSystem.SlotExists(slot);
            var info = exists ? SaveSystem.SlotInfoFull(slot) : null;
            DrawButtonBg(rect, sel);
            if (sel) DrawArrow(rect.X - 22, rect.Y + rect.Height / 2);

            // ── Иконка класса 44×44 слева ──
            int iconSize = 44;
            int ix = rect.X + 16;
            int iy = rect.Y + (rect.Height - iconSize) / 2;
            if (info == null)
            {
                DrawBox(new Rectangle(ix, iy, iconSize, iconSize),
                    new Color(34, 29, 23), new Color(72, 62, 46));
                var dash = F2.MeasureString("—");
                _sb.DrawString(F2, "—",
                    new Vector2(ix + (iconSize - dash.X) / 2, iy + (iconSize - dash.Y) / 2),
                    new Color(98, 90, 74));
            }
            else
            {
                var cls = ClassData.Get(info.ClassKey) ?? ClassData.Get("default");
                var clsColor = cls?.Color ?? Color.White;
                DrawBox(new Rectangle(ix, iy, iconSize, iconSize),
                    new Color(30, 26, 21), clsColor);
                // Портрет-заглушка: буква класса в цвете класса.
                string letter = string.IsNullOrEmpty(info.Class) ? "?"
                    : info.Class[..1].ToUpperInvariant();
                var ls = F2.MeasureString(letter);
                _sb.DrawString(F2, letter,
                    new Vector2(ix + (iconSize - ls.X) / 2, iy + (iconSize - ls.Y) / 2),
                    clsColor);
            }

            // ── Заголовок слота ──
            var slotColor = sel ? new Color(238, 222, 188) : new Color(200, 190, 170);
            _sb.DrawString(F2, $"Слот {slot}", new Vector2(rect.X + 76, rect.Y + 16),
                slotColor);

            if (info == null)
            {
                string hint = SaveMode ? "Пусто — клик сохранит сюда"
                    : "Пусто — начните новую игру";
                _sb.DrawString(FBody, hint, new Vector2(rect.X + 76, rect.Y + 54),
                    new Color(98, 90, 74));
            }
            else
            {
                var cls = ClassData.Get(info.ClassKey) ?? ClassData.Get("default");
                var clsColor = cls?.Color ?? Color.White;
                string className = info.Class;
                string modeName = info.WorldMode == "overworld"
                    ? "Открытый мир" : $"Подземелье {info.Depth}";
                var nameSize = FBody.MeasureString(className);
                _sb.DrawString(FBody, className, new Vector2(rect.X + 76, rect.Y + 54),
                    clsColor);
                _sb.DrawString(FBody,
                    $"   Ур.{info.Level}   ·   {modeName}   ·   ${info.Gold}",
                    new Vector2(rect.X + 76 + nameSize.X, rect.Y + 54),
                    new Color(222, 214, 198));

                // Дата — справа от мусорки.
                var delRect = SlotDeleteRect(slot - 1);
                string date = info.Timestamp.ToString("dd.MM.yyyy HH:mm");
                var dateSize = FSmall.MeasureString(date);
                _sb.DrawString(FSmall, date,
                    new Vector2(delRect.X - 10 - dateSize.X, rect.Y + 18),
                    new Color(98, 90, 74));

                if (SaveMode)
                {
                    var warn = "клик перезапишет";
                    var ws = FTiny.MeasureString(warn);
                    _sb.DrawString(FTiny, warn,
                        new Vector2(rect.X + rect.Width - 20 - ws.X, rect.Bottom - 24),
                        new Color(208, 158, 82));
                }

                // ── Мусорка — только у заполненного слота ──
                DrawTrashBin(delRect);
            }
        }
    }

    /// <summary>Рамка/заливка простого прямоугольника (без скругления).</summary>
    private void DrawBox(Rectangle r, Color fill, Color border)
    {
        _sb.Draw(Blank, r, fill);
        _sb.Draw(Blank, new Rectangle(r.X, r.Y, r.Width, 2), border);
        _sb.Draw(Blank, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), border);
        _sb.Draw(Blank, new Rectangle(r.X, r.Y, 2, r.Height), border);
        _sb.Draw(Blank, new Rectangle(r.Right - 2, r.Y, 2, r.Height), border);
    }

    /// <summary>Красная корзина (menu_renderer._draw_trash_bin).</summary>
    private void DrawTrashBin(Rectangle r)
    {
        DrawBox(r, new Color(60, 30, 30), new Color(180, 80, 80));
        int cx = r.X + r.Width / 2;
        float k = Math.Min(r.Width, r.Height) / 32f;
        var red = new Color(240, 120, 120);
        var darkRed = new Color(180, 80, 80);
        int lidY = r.Y + (int)(7 * k);
        _sb.Draw(Blank, new Rectangle(cx - (int)(8 * k), lidY,
            (int)(16 * k), Math.Max(1, (int)(2 * k))), red);
        // Крышка-дуга approximated as a small horizontal bar.
        _sb.Draw(Blank, new Rectangle(cx - (int)(4 * k), lidY - (int)(4 * k),
            (int)(8 * k), Math.Max(1, (int)(2 * k))), red);
        int topY = lidY + (int)(3 * k);
        int botY = r.Y + r.Height - (int)(6 * k);
        for (int y = topY; y < botY; y++)
        {
            float t = (botY - topY) <= 0 ? 0 : (float)(y - topY) / (botY - topY);
            int hw = (int)((7 - 2 * t) * k);
            _sb.Draw(Blank, new Rectangle(cx - hw, y, hw * 2, 1), red);
        }
        for (int dx = -3; dx <= 3; dx += 3)
        {
            int x1 = cx + (int)(dx * k);
            int x2 = cx + (int)(dx * k * 0.85);
            for (int y = topY + (int)(3 * k); y < botY - (int)(3 * k); y++)
                _sb.Draw(Blank, new Rectangle(x1, y, 1, 1), darkRed);
        }
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

            // Подпись — H2, слева, по центру строки.
            _sb.DrawString(F2, rows[i][0],
                new Vector2(rect.X + 28, rect.Y + rect.Height / 2 - 11),
                sel ? new Color(238, 222, 188) : new Color(200, 190, 170));

            // Значение — H2, по центру зоны x+w-100.
            int vc = rect.X + rect.Width - 100;
            var val = rows[i][1];
            var vs = F2.MeasureString(val);
            var valColor = sel ? new Color(206, 166, 82) : new Color(150, 140, 120);
            _sb.DrawString(F2, val,
                new Vector2(vc - vs.X / 2, rect.Y + rect.Height / 2 - vs.Y / 2),
                valColor);

            if (sel && i != 5)
            {
                int ay = rect.Y + rect.Height / 2;
                int valLeft = vc - (int)vs.X / 2;
                int valRight = vc + (int)vs.X / 2;
                DrawArrowLeft(valLeft - 16, ay);
                DrawArrowRight(valRight + 16, ay);
            }
        }
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
                ("Tab", "Статистика забега"),
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
                ("PgUp/PgDn · [ ]", "Прокрутка журнала событий"),
                ("Home / End", "Журнал в начало / в конец"),
                ("Магазин", "1–9 — купить · S — продать"),
                ("", "R — обновить ассортимент"),
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
                _sb.DrawString(F2, cat, new Vector2(colXs[c], y),
                    new Color(214, 178, 98));
                y += 32;
                foreach (var (keyStr, desc) in rows)
                {
                    if (keyStr.Length > 0)
                        _sb.DrawString(FBody, keyStr, new Vector2(colXs[c] + 20, y),
                            new Color(196, 176, 138));
                    _sb.DrawString(FBody, desc, new Vector2(colXs[c] + 200, y),
                        new Color(222, 214, 198));
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
        DrawSubtitle("Игра разработана командой EVA", 128, FBody,
            new Color(150, 140, 120));

        (string, string[])[] sections =
        {
            ("Создатели", new[] { "Dodik", "Don't talk to people", "And don’t write" }),
            ("Художник", new[] { "Лизабет еБатори" }),
        };
        int cx = AethelgardGame.ScreenW / 2;
        int y = 190;
        foreach (var (cat, names) in sections)
        {
            var catSize = F2.MeasureString(cat);
            _sb.DrawString(F2, cat, new Vector2(cx - catSize.X / 2, y),
                new Color(214, 178, 98));
            y += 40;
            foreach (var name in names)
            {
                var size = FBody.MeasureString(name);
                _sb.DrawString(FBody, name, new Vector2(cx - size.X / 2, y),
                    new Color(222, 214, 198));
                y += 32;
            }
            y += 16;
        }
        const string rights = "Все права на изображения принадлежат ей";
        _sb.DrawString(FSmall, rights,
            new Vector2(cx - FSmall.MeasureString(rights).X / 2, y + 8),
            new Color(98, 90, 74));
        var verSize = FBody.MeasureString("от 11.2026");
        _sb.DrawString(FBody, "от 11.2026",
            new Vector2(cx - verSize.X / 2, AethelgardGame.ScreenH - 84),
            new Color(206, 166, 82));
        DrawHint("Esc — назад", AethelgardGame.ScreenH - 40);
    }

    private void DrawMultiplayer()
    {
        DrawTitle("МУЛЬТИПЛЕЕР");
        DrawSubtitle("Играй с другом по локальной сети", 160, FSmall,
            new Color(150, 140, 120));

        if (HasMpSave())
            DrawSubtitle("● Есть сохранение мультиплеера", 180, FTiny,
                new Color(134, 190, 118));
        else
            DrawSubtitle("Сохранения нет", 180, FTiny, new Color(98, 90, 74));

        var labels = MpLabels;
        for (int i = 0; i < labels.Length; i++)
        {
            var rect = ClassStartRect(i);
            bool sel = i == MpSelection;
            DrawButtonBg(rect, sel);
            var size = F2.MeasureString(labels[i]);
            _sb.DrawString(F2, labels[i],
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
        var pSize = FBody.MeasureString(prompt);
        _sb.DrawString(FBody, prompt, new Vector2(cx - pSize.X / 2, 200),
            new Color(222, 214, 198));

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
        var tSize = F2.MeasureString(text);
        _sb.DrawString(F2, text,
            new Vector2(cx - tSize.X / 2, field.Y + field.Height / 2 - tSize.Y / 2),
            new Color(228, 220, 200));

        DrawSubtitle("Например: localhost или 192.168.1.5", 310, FSmall,
            new Color(98, 90, 74));

        var btn = MpJoinRect();
        DrawButtonBg(btn, true);
        var bSize = F2.MeasureString("Подключиться");
        _sb.DrawString(F2, "Подключиться",
            new Vector2(btn.X + (btn.Width - bSize.X) / 2,
                        btn.Y + (btn.Height - bSize.Y) / 2),
            new Color(238, 222, 188));

        DrawHint("Enter — подключиться · Ctrl+V — вставить · Esc — назад",
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
