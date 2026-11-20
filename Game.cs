using System;
using Aethelgard.Core;
using Aethelgard.Game;
using Aethelgard.Net;
using Aethelgard.Render;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Aethelgard;

/// <summary>
/// MonoGame game loop. Delegates to GameController (state/input)
/// and renderers. Mirrors Python main.py architecture.
///
/// Modes: class_select → game (single-player) OR mp_menu → mp_game (multiplayer)
/// </summary>
public class Game : Microsoft.Xna.Framework.Game
{
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
    private MultiplayerScreen _mpScreen;
    private MpRenderer _mpRenderer;
    private DevConsoleScreen _devConsole;
    private SaveSlotScreen _saveSlots;
    private BigmapScreen _bigmap;
    private KeyboardState _prevKeys;
    private float _fps;
    private float _fpsTimer;

    public const int ScreenW = 1280;
    public const int ScreenH = 720;

    public Game()
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
        _mpScreen = new MultiplayerScreen(GraphicsDevice, _spriteBatch);
        _mpRenderer = new MpRenderer(GraphicsDevice, _spriteBatch);
        _devConsole = new DevConsoleScreen(GraphicsDevice, _spriteBatch);
        _saveSlots = new SaveSlotScreen(GraphicsDevice, _spriteBatch);
        _bigmap = new BigmapScreen(GraphicsDevice, _spriteBatch);
        // Generate font at runtime (no MGCB content pipeline needed)
        var font = RuntimeFont.Create(GraphicsDevice);
        if (font != null)
        {
            _renderer.LoadFont(font);
            _inventory.LoadFont(font);
            _skills.LoadFont(font);
            _classSelect.LoadFont(font);
            _shop.LoadFont(font);
            _quests.LoadFont(font);
            _dialogue.LoadFont(font);
            _mpScreen.LoadFont(font);
            _mpRenderer.LoadFont(font);
            _devConsole.LoadFont(font);
            _saveSlots.LoadFont(font);
            _bigmap.LoadFont(font);
        }

        // Initialize audio
        _game.Audio = new Aethelgard.Audio.AudioSystem();
        _game.Audio.Start();
    }

    protected override void Update(GameTime gameTime)
    {
        var keys = Keyboard.GetState();
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // FPS
        _fpsTimer += dt;
        if (_fpsTimer >= 0.5f)
        {
            _fps = 1f / dt;
            _fpsTimer = 0;
        }
        _game.Fps = _fps;

        // ESC handling
        if (keys.IsKeyDown(Keys.Escape))
        {
            if (_game.ScreenMode == "mp_menu")
            {
                _mpScreen.Reset();
                _game.ScreenMode = "class_select";
            }
            else if (_game.ScreenMode == "mp_game")
            {
                // Disconnect and return to class select
                _game.MpClient?.Disconnect();
                _game.ScreenMode = "class_select";
            }
            else if (_game.ScreenMode == "inventory" || _game.ScreenMode == "skills"
                     || _game.ScreenMode == "shop" || _game.ScreenMode == "quests"
                     || _game.ScreenMode == "dialogue")
                _game.ScreenMode = "game";
            else if (_game.ScreenMode == "class_select")
                Exit();
            else
                Exit();
        }

        // M key — open multiplayer menu from class select
        if (_game.ScreenMode == "class_select"
            && keys.IsKeyDown(Keys.M) && !_prevKeys.IsKeyDown(Keys.M))
        {
            _game.ScreenMode = "mp_menu";
            _mpScreen.Reset();
        }

        // MP menu update
        if (_game.ScreenMode == "mp_menu")
        {
            var mouse = Mouse.GetState();
            _spriteBatch.Begin();
            bool startMp = _mpScreen.Draw(keys, _prevKeys, mouse);
            _spriteBatch.End();

            if (startMp && _mpScreen.Client != null)
            {
                _game.StartMultiplayer(_mpScreen.Client);
            }

            // Enter to start as host
            if (_mpScreen.CurrentMode == MultiplayerScreen.Mode.HostWaiting
                && keys.IsKeyDown(Keys.Enter) && !_prevKeys.IsKeyDown(Keys.Enter))
            {
                if (_mpScreen.Client != null)
                    _game.StartMultiplayer(_mpScreen.Client);
            }
        }
        else if (_game.ScreenMode == "mp_game")
        {
            _game.HandleInput(keys, _prevKeys, dt);
            _game.UpdateMp();
        }
        else if (_game.ScreenMode != "class_select")
        {
            _inventory.CurrentPage = _game.CurrentPage;
            _skills.CurrentTab = _game.SkillTab;
            _game.HandleInput(keys, _prevKeys, dt);
            _game.Update(dt);
        }

        _prevKeys = keys;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(12, 12, 18));

        var keys = Keyboard.GetState();
        var mouse = Mouse.GetState();

        if (_game.ScreenMode == "class_select")
        {
            _spriteBatch.Begin();
            var selected = _classSelect.Draw(keys, _prevKeys, mouse);
            _spriteBatch.End();
            if (selected != null)
                _game.NewGame(selected);

            // MP hint
            if (_fontHint != null)
            {
                _spriteBatch.Begin();
                _spriteBatch.DrawString(_fontHint, "Press M for Multiplayer",
                    new Vector2(Game.ScreenW / 2 - 80, Game.ScreenH - 60),
                    new Color(150, 140, 100));
                _spriteBatch.End();
            }
        }
        else if (_game.ScreenMode == "mp_menu")
        {
            // Already drawn in Update
        }
        else if (_game.ScreenMode == "mp_game")
        {
            _mpRenderer.Draw(_game);
        }
        else
        {
            _renderer.Draw(_game);

            _spriteBatch.Begin();
            switch (_game.ScreenMode)
            {
                case "inventory": _inventory.Draw(_game.Player, mouse); break;
                case "skills": _skills.Draw(_game.Player, mouse); break;
                case "shop": _shop.Draw(_game); break;
                case "quests": _quests.Draw(_game); break;
                case "dialogue": _dialogue.Draw(_game); break;
                case "bigmap": _bigmap.Draw(_game); break;
            }
            // Dev console overlay (always on top)
            if (_game.Console.Open)
                _devConsole.Draw(_game.Console);
            _spriteBatch.End();
        }

        base.Draw(gameTime);
    }

    private SpriteFont? _fontHint;
}
