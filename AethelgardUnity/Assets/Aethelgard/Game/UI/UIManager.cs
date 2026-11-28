using Aethelgard.Runtime;
using Aethelgard.Render;
using Aethelgard.Systems;
using UnityEngine;
using UnityEngine.UI;
using XnaKeys = Microsoft.Xna.Framework.Input.Keys;
using XnaMouse = Microsoft.Xna.Framework.Input.MouseState;
using Screens = Aethelgard.Systems.ScreenMode;

namespace Aethelgard.UI;

/// <summary>
/// Корень интерфейса: один Canvas 1920×1080, контейнеры всех экранов,
/// диспетчеризация по ScreenMode (порт Update/Draw World/Game.cs) и HUD.
/// Экраны собираются кодом по UITheme; тему и виджеты можно править в
/// инспекторе Unity. ESC-навигация повторяет MonoGame-версию.
/// </summary>
public class UIManager : MonoBehaviour
{
    public GameController Game = null!;
    public UITheme Theme = null!;
    public string? PendingClassSelection;   // результат ClassSelect
    public bool ClassBackRequested;

    private RectTransform _root = null!;
    private MenuUI _menu = null!;
    private ClassSelectUI _classSelect = null!;
    private HudUI _hud = null!;
    private InventoryUI _inventory = null!;
    private ShopUI _shop = null!;
    private SkillUI _skills = null!;
    private CraftUI _craft = null!;
    private StatsUI _stats = null!;
    private QuestsUI _quests = null!;
    private DialogueUI _dialogue = null!;
    private BigmapUI _bigmap = null!;
    private ConsoleUI _console = null!;
    private MpUI _mp = null!;
    private DeathUI _death = null!;
    private MessageUI _message = null!;

    private static readonly string[] MenuModes =
    {
        Screens.Menu, Screens.MenuSaves, Screens.MenuSettings,
        Screens.MenuControls, Screens.MenuCredits, Screens.MenuMultiplayer,
        Screens.MenuMpJoin,
    };

    public void Init(GameController game, UITheme theme)
    {
        Game = game;
        Theme = theme;

        var canvasGo = gameObject;
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // EventSystem — без него кнопки не реагируют на мышь
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        _root = (RectTransform)transform;
        _root.anchorMin = Vector2.zero;
        _root.anchorMax = Vector2.one;
        _root.offsetMin = _root.offsetMax = Vector2.zero;

        _menu = new MenuUI(this);
        _classSelect = new ClassSelectUI(this);
        _hud = new HudUI(this);
        _inventory = new InventoryUI(this);
        _shop = new ShopUI(this);
        _skills = new SkillUI(this);
        _craft = new CraftUI(this);
        _stats = new StatsUI(this);
        _quests = new QuestsUI(this);
        _dialogue = new DialogueUI(this);
        _bigmap = new BigmapUI(this);
        _console = new ConsoleUI(this);
        _mp = new MpUI(this);
        _death = new DeathUI(this);
        _message = new MessageUI(this);

        _menu.Build();
        _classSelect.Build();
        _hud.Build();
        _inventory.Build();
        _shop.Build();
        _skills.Build();
        _craft.Build();
        _stats.Build();
        _quests.Build();
        _dialogue.Build();
        _bigmap.Build();
        _console.Build();
        _mp.Build();
        _death.Build();
        _message.Build();
    }

    public RectTransform Root => _root;

    /// <summary>Кадр экрана выбора класса (клик по кнопке «ИГРАТЬ» уже в UI).</summary>
    public void DrawClassSelect()
    {
        var keys = Runtime.InputState.Keys;
        var prevKeys = Runtime.InputState.PrevKeys;
        _classSelect.HandleKeys(keys, prevKeys);
        _classSelect.Redraw();
    }

    /// <summary>Показ/скрытие контейнеров по ScreenMode.</summary>
    public void Show(string mode)
    {
        bool menu = System.Array.IndexOf(MenuModes, mode) >= 0;
        SetActive(_menu.Root, menu);
        SetActive(_classSelect.Root, mode == Screens.ClassSelect);
        SetActive(_hud.Root, mode == Screens.Game || mode == Screens.MpGame ||
            mode == Screens.Dialogue || mode == Screens.StatsRun ||
            mode == Screens.Bigmap);
        SetActive(_inventory.Root, mode == Screens.Inventory);
        SetActive(_shop.Root, mode == Screens.Shop);
        SetActive(_skills.Root, mode == Screens.Skills);
        SetActive(_craft.Root, mode == Screens.Craft);
        SetActive(_stats.Root, mode == Screens.Stats);
        SetActive(_quests.Root, mode == Screens.Quests || mode == Screens.StatsRun);
        SetActive(_dialogue.Root, mode == Screens.Dialogue);
        SetActive(_bigmap.Root, mode == Screens.Bigmap);
        SetActive(_mp.Root, mode == Screens.MpGame);
        SetActive(_death.Root, ShouldShowDeath(mode));
    }

    private bool ShouldShowDeath(string mode) =>
        Game.Victory && mode == Screens.Game && Game.Player?.Alive == true;

    private static void SetActive(RectTransform? rt, bool active)
    {
        if (rt != null) rt.gameObject.SetActive(active);
    }

    /// <summary>
    /// Кадр UI. Возвращает true, если кадр поглощён (меню/пауза) —
    /// GameController не должен обрабатывать этот кадр.
    /// </summary>
    public bool Tick(XnaMouse mouse, XnaMouse prevMouse, float dt)
    {
        var keys = InputState.Keys;
        var prevKeys = InputState.PrevKeys;
        string mode = Game.ScreenMode;
        Show(mode); // видимость экранов обновляется каждый кадр

        // ── Меню и подменю ──
        if (System.Array.IndexOf(MenuModes, mode) >= 0)
        {
            _menu.Handle(keys, prevKeys, mouse, prevMouse);
            _menu.Draw(mode);
            return true;
        }

        // ── ESC-навигация (порт World/Game.cs) ──
        bool escPressed = keys.IsKeyDown(XnaKeys.Escape) &&
                          !prevKeys.IsKeyDown(XnaKeys.Escape);
        if (escPressed)
        {
            switch (mode)
            {
                case Screens.MpGame:
                    Game.StopMultiplayer();
                    Game.ScreenMode = Screens.Menu;
                    _menu.OpenMain();
                    return true;
                case Screens.ClassSelect:
                    Game.CurrentSlot = 1;
                    Game.PendingMpHost = false;
                    Game.PendingMpFresh = false;
                    Game.ScreenMode = Screens.Menu;
                    _menu.OpenMain();
                    return true;
                case Screens.Inventory: case Screens.Skills: case Screens.Shop:
                case Screens.Quests: case Screens.Dialogue: case Screens.Craft:
                case Screens.Stats: case Screens.Bigmap: case Screens.StatsRun:
                case Screens.Trade:
                    Game.ScreenMode = Screens.Game;
                    return true;
            }
        }

        // ── Обновление активного экрана ──
        switch (mode)
        {
            case Screens.MpGame:
                _hud.Draw();
                _mp.Draw(dt);
                break;
            case Screens.Game:
                _hud.Draw();
                _death.Draw();
                break;
            case Screens.Inventory:
                _inventory.Draw(mouse, prevMouse);
                break;
            case Screens.Skills:
                _skills.Draw(mouse, prevMouse);
                break;
            case Screens.Shop:
                _shop.Draw(mouse, prevMouse);
                break;
            case Screens.Craft:
                _craft.Draw(dt, mouse, prevMouse);
                break;
            case Screens.Stats:
                _stats.Draw(mouse, prevMouse);
                break;
            case Screens.Quests:
                _quests.Draw();
                break;
            case Screens.StatsRun:
                _quests.DrawRunStats();
                break;
            case Screens.Dialogue:
                _hud.Draw();
                _dialogue.Draw(mouse);
                break;
            case Screens.Bigmap:
                _bigmap.Draw();
                break;
            case Screens.Pause:
                _menu.DrawPause(dt);
                break;
        }

        // Дев-консоль поверх всего
        if (Game.Console.Open)
            _console.Draw();

        // Всплывающее сообщение
        _message.Draw();

        return false;
    }

    // ── Колбэки меню (порт WireMenu) ──

    public void OnStartNew(int slot)
    {
        Game.CurrentSlot = slot;
        Game.PendingMpHost = false;
        Game.PendingMpFresh = false;
        _classSelect.Selected = 0;
        Game.ScreenMode = Screens.ClassSelect;
    }

    public void OnSaveSlot(int slot)
    {
        Game.CurrentSlot = slot;
        bool ok = SaveSystem.Save(Game, slot);
        Game.ShowMessage(ok ? $"Сохранено в слот {slot}" : "Ошибка сохранения!");
        if (_menu.FromGame && ok)
        {
            Game.ScreenMode = Screens.Game;
            _menu.OpenFromGame();
        }
    }

    public void OnLoadSlot(int slot)
    {
        Game.CurrentSlot = slot;
        var data = SaveSystem.Load(slot);
        if (data != null)
        {
            SaveSystem.ApplyLoad(Game, data);
            Game.ScreenMode = Screens.Game;
        }
        else
            Game.ShowMessage($"Слот {slot} пуст");
    }

    public void OnResume() => Game.ScreenMode = Screens.Game;

    public void OnExitToMenu()
    {
        SaveSystem.Save(Game, Game.CurrentSlot);
        Game.ScreenMode = Screens.Menu;
        _menu.OpenMain();
    }

    public void OnExit()
    {
        Game.Settings.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OnMpHost(bool freshStart)
    {
        if (!Game.StartMpHost(freshStart, "warrior"))
            Game.ShowMessage("Не удалось запустить сервер мультиплеера");
    }

    public void OnMpCreate()
    {
        Game.PendingMpHost = true;
        Game.PendingMpFresh = true;
        Game.ScreenMode = Screens.ClassSelect;
    }

    public void OnMpJoin(string hostPort)
    {
        if (!Game.StartMpJoin(hostPort))
            Game.ShowMessage("Не удалось подключиться к серверу");
    }

    public void OnResolution(int w, int h)
    {
        Screen.SetResolution(w, h, FullScreenMode.Windowed);
    }

    public void OnFullscreen(bool fs)
    {
        Screen.fullScreenMode = fs ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
    }

    public void OnVolume() => Game.ApplySettings();

    public void HandleClassSelection(string? selected)
    {
        if (false) // BackRequested обрабатывается через HandleClassSelection(null)
        {
            Game.CurrentSlot = 1;
            Game.PendingMpHost = false;
            Game.PendingMpFresh = false;
            Game.ScreenMode = Screens.Menu;
            _menu.OpenMain();
        }
        else if (selected != null)
        {
            if (Game.PendingMpHost)
            {
                bool fresh = Game.PendingMpFresh;
                Game.PendingMpHost = false;
                Game.PendingMpFresh = false;
                if (!Game.StartMpHost(fresh, selected))
                {
                    Game.ScreenMode = Screens.Menu;
                    _menu.OpenMain();
                    Game.ShowMessage("Не удалось запустить сервер мультиплеера");
                }
            }
            else
                Game.NewGame(selected);
        }
    }

    // ── Пауза (порт логики Pause из World/Game.cs) ──

    public void PauseChoice(int choice)
    {
        if (choice == 0) Game.ScreenMode = Screens.Game;
        else if (choice == 1)
        {
            _menu.FromGame = true;
            _menu.OpenSaves(true);
            Game.ScreenMode = Screens.MenuSaves;
        }
        else if (choice == 2)
        {
            _menu.FromGame = true;
            _menu.OpenSaves(false);
            Game.ScreenMode = Screens.MenuSaves;
        }
        else if (choice == 3)
        {
            _menu.FromGame = true;
            _menu.OpenSettings();
            Game.ScreenMode = Screens.MenuSettings;
        }
        else if (choice == 4) OnExitToMenu();
    }
}
