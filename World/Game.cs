using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Aethelgard.World;

/// <summary>
/// MonoGame game loop. Delegates to GameController (state/input)
/// and renderers. Mirrors Python main.py architecture.
///
/// Режимы (GameController.ScreenMode):
///   menu / menu_saves / menu_settings / menu_controls / menu_credits /
///   menu_multiplayer / menu_mp_join  — главное меню и подменю
///   class_select → game              — одиночная игра
///   game + модальные экраны          — инвентарь/навыки/магазин/...
///   mp_game                          — мультиплеер (из меню «Мультиплеер»)
///
/// Отрисовка идёт в render target 1280×720 и масштабируется под выбранное
/// разрешение окна (настройка «Разрешение» в меню).
/// </summary>
public class AethelgardGame : Microsoft.Xna.Framework.Game
{
    /// <summary>Тестовый хук: вызывается в конце каждого кадра (для снятия скриншотов).</summary>
    public static Action<GraphicsDevice>? CaptureHook;

    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private GameController _game;
    private GameRenderer _renderer;
    private InventoryScreen _inventory;
    private SkillScreen _skills;
    private ClassSelectScreen _classSelect;
    private ShopScreen _shop;
    private QuestScreen _quests;
    private DialogueScreen _dialogue;
    private MpRenderer _mpRenderer;
    private DevConsoleScreen _devConsole;
    private BigmapScreen _bigmap;
    private CraftScreen _craft;
    private StatsScreen _stats;
    private PauseScreen _pause;
    private MainMenuScreen _menu;
    private RenderTarget2D? _target;
    private KeyboardState _prevKeys;
    private MouseState _prevMouse;
    private float _fps;
    private float _fpsTimer;

    public const int ScreenW = 1920;
    public const int ScreenH = 1080;

    private static readonly string[] MenuModes =
    {
        ScreenMode.Menu, ScreenMode.MenuSaves, ScreenMode.MenuSettings, ScreenMode.MenuControls,
        ScreenMode.MenuCredits, ScreenMode.MenuMultiplayer, ScreenMode.MenuMpJoin,
    };

    public AethelgardGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = ScreenW,
            PreferredBackBufferHeight = ScreenH,
            SynchronizeWithVerticalRetrace = true,
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        TargetElapsedTime = TimeSpan.FromMilliseconds(1000.0 / 60.0);
    }

    protected override void Initialize()
    {
        _game = new GameController();
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _renderer = new GameRenderer(GraphicsDevice, _spriteBatch);
        _inventory = new InventoryScreen(GraphicsDevice, _spriteBatch);
        _skills = new SkillScreen(GraphicsDevice, _spriteBatch);
        _classSelect = new ClassSelectScreen(GraphicsDevice, _spriteBatch);
        _shop = new ShopScreen(GraphicsDevice, _spriteBatch);
        _quests = new QuestScreen(GraphicsDevice, _spriteBatch);
        _dialogue = new DialogueScreen(GraphicsDevice, _spriteBatch);
        _mpRenderer = new MpRenderer(GraphicsDevice, _spriteBatch);
        _devConsole = new DevConsoleScreen(GraphicsDevice, _spriteBatch);
        _bigmap = new BigmapScreen(GraphicsDevice, _spriteBatch);
        _craft = new CraftScreen(GraphicsDevice, _spriteBatch);
        _stats = new StatsScreen(GraphicsDevice, _spriteBatch);
        _pause = new PauseScreen(GraphicsDevice, _spriteBatch);
        _menu = new MainMenuScreen(GraphicsDevice, _spriteBatch);

        // Generate fonts at runtime (no MGCB content pipeline needed).
        // Обычный — для интерфейса, крупный — для заголовков и кнопок меню.
        var font = RuntimeFont.Create(GraphicsDevice, 18);
        var fontBig = RuntimeFont.Create(GraphicsDevice, 28);
        if (font != null)
        {
            _renderer.LoadFont(font);
            _inventory.LoadFont(font);
            _skills.LoadFont(font);
            _classSelect.LoadFont(fontBig ?? font);
            _shop.LoadFont(font);
            _quests.LoadFont(font);
            _dialogue.LoadFont(font);
            _mpRenderer.LoadFont(font);
            _devConsole.LoadFont(font);
            _bigmap.LoadFont(font);
            _craft.LoadFont(font);
            _stats.LoadFont(font);
            _pause.LoadFont(font);
        }
        if (fontBig != null && font != null)
            _menu.LoadFont(font, fontBig);
        else if (font != null)
            _menu.LoadFont(font, font);

        // Render target фиксированного «дизайн-разрешения»
        _target = new RenderTarget2D(GraphicsDevice, ScreenW, ScreenH);

        // Initialize audio
        _game.Audio = new Aethelgard.Audio.AudioSystem();
        _game.Audio.Start();
        _game.ApplySettings();

        // Применяем настройки окна (разрешение / полный экран)
        var s = _game.Settings;
        _graphics.PreferredBackBufferWidth = s.Resolution[0];
        _graphics.PreferredBackBufferHeight = s.Resolution[1];
        _graphics.IsFullScreen = s.Fullscreen;
        _graphics.ApplyChanges();

        WireMenu();
        _game.ScreenMode = ScreenMode.Menu;
        _menu.OpenMain();
    }

    /// <summary>Связывает колбэки главного меню с игровым состоянием.</summary>
    private void WireMenu()
    {
        _menu.GetSettings = () => _game.Settings;

        _menu.OnStartNew = () =>
        {
            _game.CurrentSlot = 1;
            _game.PendingMpHost = false;
            _game.PendingMpFresh = false;
            _game.ScreenMode = ScreenMode.ClassSelect;
        };
        _menu.OnSaveSlot = slot =>
        {
            _game.CurrentSlot = slot;
            bool ok = SaveSystem.Save(_game, slot);
            _game.ShowMessage(ok ? $"Сохранено в слот {slot}" : "Ошибка сохранения!");
            if (_menu.FromGame && ok)
            {
                _game.ScreenMode = ScreenMode.Game;
                _menu.OpenFromGame();
            }
        };
        _menu.OnLoadSlot = slot =>
        {
            _game.CurrentSlot = slot;
            var data = SaveSystem.Load(slot);
            if (data != null)
            {
                SaveSystem.ApplyLoad(_game, data);
                _game.ScreenMode = ScreenMode.Game;
            }
            else
                _game.ShowMessage($"Слот {slot} пуст");
        };
        _menu.OnResume = () => _game.ScreenMode = ScreenMode.Game;
        _menu.OnExitToMenu = () =>
        {
            SaveSystem.Save(_game, _game.CurrentSlot);  // тихое сохранение (python exit_to_main_menu)
            _game.ScreenMode = ScreenMode.Menu;
            _menu.OpenMain();
        };
        _menu.OnExit = () =>
        {
            _game.Settings.Save();
            Exit();
        };
        _menu.OnMpHost = freshStart =>
        {
            // «Продолжить игру»: мир восстанавливается из сейва, класс тоже
            // (python: skip_class=True — класс восстанавливается из сейва)
            if (!_game.StartMpHost(freshStart, "warrior"))
                _game.ShowMessage("Не удалось запустить сервер мультиплеера");
        };
        _menu.OnMpCreate = () =>
        {
            _game.PendingMpHost = true;
            _game.PendingMpFresh = true;
            _game.ScreenMode = ScreenMode.ClassSelect;
        };
        _menu.OnMpJoin = hostPort =>
        {
            if (!_game.StartMpJoin(hostPort))
                _game.ShowMessage("Не удалось подключиться к серверу");
        };
        _menu.OnResolution = res =>
        {
            _graphics.PreferredBackBufferWidth = res[0];
            _graphics.PreferredBackBufferHeight = res[1];
            _graphics.IsFullScreen = false;
            _graphics.ApplyChanges();
        };
        _menu.OnFullscreen = fs =>
        {
            _graphics.IsFullScreen = fs;
            _graphics.ApplyChanges();
        };
        _menu.OnVolume = _ => _game.ApplySettings();
    }

    /// <summary>
    /// Мышь приходит в координатах клиентской области окна (логических,
    /// как их отдаёт SDL/MonoGame), а весь UI размечен в дизайн-координатах
    /// 1280×720 (render target растягивается на всё окно). Пересчитываем.
    /// Важно: используем Window.ClientBounds, а не GraphicsDevice.Viewport —
    /// при масштабировании Windows (125%/150%) viewport измеряется в
    /// физических пикселях и мышь «не точь в точь» попадает по UI.
    /// </summary>
    private MouseState ScaleMouse(MouseState m)
    {
        int vw, vh;
        try { vw = Window.ClientBounds.Width; vh = Window.ClientBounds.Height; }
        catch { return m; }
        if (vw <= 0 || vh <= 0 || (vw == ScreenW && vh == ScreenH)) return m;
        return new MouseState(
            (int)MathF.Round(m.X * (float)ScreenW / vw),
            (int)MathF.Round(m.Y * (float)ScreenH / vh),
            m.ScrollWheelValue,
            m.LeftButton, m.MiddleButton, m.RightButton, m.XButton1, m.XButton2);
    }

    protected override void Update(GameTime gameTime)
    {
        var keys = Keyboard.GetState();
        var mouse = ScaleMouse(Mouse.GetState());
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // FPS
        _fpsTimer += dt;
        if (_fpsTimer >= 0.5f)
        {
            _fps = 1f / dt;
            _fpsTimer = 0;
        }
        _game.Fps = _fps;

        string mode = _game.ScreenMode;

        // ── Главное меню и подменю ──
        if (Array.IndexOf(MenuModes, mode) >= 0)
        {
            _menu.HandleInput(keys, _prevKeys);
            _menu.HandleMouse(mouse, _prevMouse);
            _prevKeys = keys;
            _prevMouse = mouse;
            base.Update(gameTime);
            return;
        }

        // ── ESC-навигация вне меню ──
        // Важно: если ESC закрыл модальный экран, кадр «поглощается» —
        // иначе тот же ESC в этом же кадре увидит GameController и сразу
        // откроет паузу (баг: Esc из магазина открывал паузу).
        bool escPressed = keys.IsKeyDown(Keys.Escape) && !_prevKeys.IsKeyDown(Keys.Escape);
        bool escConsumed = false;
        if (escPressed)
        {
            switch (mode)
            {
                case ScreenMode.MpGame:
                    // Отключиться и вернуться в главное меню
                    _game.StopMultiplayer();
                    _game.ScreenMode = ScreenMode.Menu;
                    _menu.OpenMain();
                    escConsumed = true;
                    break;
                case ScreenMode.ClassSelect:
                    // Назад в главное меню (python: Esc → меню)
                    _game.PendingMpHost = false;
                    _game.ScreenMode = ScreenMode.Menu;
                    _menu.OpenMain();
                    escConsumed = true;
                    break;
                case ScreenMode.Inventory: case ScreenMode.Skills: case ScreenMode.Shop: case ScreenMode.Quests:
                case ScreenMode.Dialogue: case ScreenMode.Craft: case ScreenMode.Stats: case ScreenMode.Bigmap:
                    _game.ScreenMode = ScreenMode.Game;
                    escConsumed = true;
                    break;
                // ScreenMode.Pause: ESC обрабатывается GameController (resume)
                // ScreenMode.Game: ESC → пауза (тоже GameController)
            }
            if (escConsumed)
            {
                _prevKeys = keys;
                _prevMouse = mouse;
                base.Update(gameTime);
                return;
            }
        }

        if (mode == ScreenMode.MpGame)
        {
            _game.HandleInput(keys, _prevKeys, dt);
            _game.UpdateMp(dt);
        }
        else if (mode == ScreenMode.ClassSelect)
        {
            // Выбор класса рисуется в Draw (он сам обрабатывает ввод)
        }
        else if (mode == ScreenMode.Pause)
        {
            // Меню паузы: 5 пунктов (python MENU_PAUSE)
            int? choice = _pause.HandleInput(keys, _prevKeys);
            int? mouseChoice = _pause.HandleMouse(mouse, _prevMouse);
            if (mouseChoice.HasValue) choice = mouseChoice;
            if (escPressed) _game.ScreenMode = ScreenMode.Game;      // Esc — вернуться
            else if (choice == 0) _game.ScreenMode = ScreenMode.Game;
            else if (choice == 1)
            {
                _menu.FromGame = true;
                _menu.OpenSaves(true);
                _game.ScreenMode = ScreenMode.MenuSaves;
            }
            else if (choice == 2)
            {
                _menu.FromGame = true;
                _menu.OpenSaves(false);
                _game.ScreenMode = ScreenMode.MenuSaves;
            }
            else if (choice == 3)
            {
                _menu.FromGame = true;
                _menu.OpenSettings();
                _game.ScreenMode = ScreenMode.MenuSettings;
            }
            else if (choice == 4) _menu.OnExitToMenu?.Invoke();
        }
        else
        {
            _inventory.CurrentPage = _game.CurrentPage;
            _skills.CurrentTab = _game.SkillTab;
            // Клик по кнопкам интерфейса (приоритет над игровым вводом)
            if (_renderer.HandleButtonClick(mouse, _prevMouse, _game))
            {
                _prevKeys = keys;
                _prevMouse = mouse;
                base.Update(gameTime);
                return;
            }
            _game.HandleInput(keys, _prevKeys, dt);
            _game.Update(dt);
        }

        _prevKeys = keys;
        _prevMouse = mouse;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (_target == null) return;

        // Вся отрисовка — в render target 1280×720
        GraphicsDevice.SetRenderTarget(_target);
        GraphicsDevice.Clear(new Color(12, 12, 18));

        var keys = Keyboard.GetState();
        var mouse = ScaleMouse(Mouse.GetState());
        string mode = _game.ScreenMode;

        if (Array.IndexOf(MenuModes, mode) >= 0)
        {
            _spriteBatch.Begin();
            _menu.Draw(_game);
            _spriteBatch.End();
        }
        else if (mode == ScreenMode.ClassSelect)
        {
            _spriteBatch.Begin();
            var selected = _classSelect.Draw(keys, _prevKeys, mouse);
            _spriteBatch.End();
            if (selected != null)
            {
                if (_game.PendingMpHost)
                {
                    bool fresh = _game.PendingMpFresh;
                    _game.PendingMpHost = false;
                    _game.PendingMpFresh = false;
                    if (!_game.StartMpHost(fresh, selected))
                    {
                        _game.ScreenMode = ScreenMode.Menu;
                        _menu.OpenMain();
                        _game.ShowMessage("Не удалось запустить сервер мультиплеера");
                    }
                }
                else
                    _game.NewGame(selected);
            }
        }
        else if (mode == ScreenMode.MpGame)
        {
            _mpRenderer.Draw(_game);
        }
        else
        {
            // Мир рисуется всегда (GameRenderer сам делает Begin/End),
            // в паузе фон перекрывается панелью паузы.
            _renderer.Draw(_game, mouse);

            _spriteBatch.Begin();
            switch (mode)
            {
                case ScreenMode.Inventory: _inventory.Draw(_game.Player, mouse); break;
                case ScreenMode.Skills:
                {
                    var cmd = _skills.Draw(_game.Player, mouse);
                    _game.HandleSkillClick(cmd);
                    break;
                }
                case ScreenMode.Shop: _shop.Draw(_game); break;
                case ScreenMode.Quests: _quests.Draw(_game); break;
                case ScreenMode.Dialogue: _dialogue.Draw(_game); break;
                case ScreenMode.Bigmap: _bigmap.Draw(_game); break;
                case ScreenMode.Craft: _craft.Draw(_game, mouse); break;
                case ScreenMode.Stats: _stats.Draw(_game, mouse); break;
                case ScreenMode.Pause: _pause.Draw(_game); break;
            }
            // Dev console overlay (always on top)
            if (_game.Console.Open)
                _devConsole.Draw(_game.Console);
            _spriteBatch.End();
        }

        // Масштабируем render target под выбранное разрешение окна
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(new Color(6, 5, 4));
        _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque,
            SamplerState.LinearClamp, null, null);
        _spriteBatch.Draw(_target, new Rectangle(0, 0,
            GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height),
            Color.White);
        _spriteBatch.End();

        base.Draw(gameTime);
        CaptureHook?.Invoke(GraphicsDevice);
    }
}
