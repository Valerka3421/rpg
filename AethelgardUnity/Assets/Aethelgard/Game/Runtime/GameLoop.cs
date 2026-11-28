using Aethelgard.Systems;
using Aethelgard.UI;
using UnityEngine;
using XnaMouse = Microsoft.Xna.Framework.Input.MouseState;
using Screens = Aethelgard.Systems.ScreenMode;

namespace Aethelgard.Runtime;

/// <summary>
/// Главный цикл игры на Unity — замена MonoGame AethelgardGame.
/// Создаёт GameController, камеру, мир (WorldRenderer) и UI (UIManager),
/// каждый кадр: ввод → диспетчеризация по ScreenMode (порт Update/Draw
/// World/Game.cs, включая ESC-поглощение кадров).
/// Сцену собирает сам (Bootstrap) — .unity-файл не нужен.
/// </summary>
public class GameLoop : MonoBehaviour
{
    public static GameLoop Instance { get; private set; } = null!;

    public GameController Game = null!;
    public UIManager Ui = null!;
    public WorldRenderer World = null!;
    public UITheme Theme = null!;

    private Camera _camera = null!;
    private XnaMouse _prevMouse;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("AethelgardGameLoop");
        go.AddComponent<GameLoop>();
    }

    private void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (Theme == null) Theme = UITheme.Default;

        // Камера мира (спрайты)
        var camGo = new GameObject("WorldCamera");
        _camera = camGo.AddComponent<Camera>();
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = new Color(12f / 255f, 12f / 255f, 18f / 255f);
        _camera.cullingMask = ~(1 << LayerMask.NameToLayer("UI"));
        camGo.transform.position = new Vector3(0, 0, -10);

        // Игровая логика
        Game = new GameController();
        Game.ScreenMode = Screens.Menu; // старт в главном меню (как MonoGame LoadContent)
        Game.Audio = new Audio.AudioSystem();
        Game.Audio.Volume = Game.Settings.Volume / 10f;

        // UI поверх камеры
        var uiGo = new GameObject("UI", typeof(RectTransform));
        Ui = uiGo.AddComponent<UIManager>();
        Ui.Init(Game, Theme);

        World = new WorldRenderer(transform, _camera);

        // Загрузка настроек разрешения
        var res = Game.Settings.Resolution;
        if (res is { Length: 2 } && res[0] > 0 && res[1] > 0 &&
            (res[0] != Screen.width || res[1] != Screen.height))
            Screen.SetResolution(res[0], res[1], Game.Settings.Fullscreen
                ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        // ── Ввод ──
        InputState.PrevKeys = InputState.Keys;
        InputState.Keys = UnityInput.ReadKeyboard();
        float scaleFactor = Screen.height / 1080f;
        var mouse = UnityInput.ReadMouse(scaleFactor);
        var prevMouse = _prevMouse;
        _prevMouse = mouse;

        Game.Fps = 1f / Mathf.Max(0.0001f, dt);

        string mode = Game.ScreenMode;

        // ── Меню — кадр целиком UI ──
        if (Ui.Tick(mouse, prevMouse, dt))
        {
            _camera.backgroundColor = new Color(0.05f, 0.04f, 0.07f);
            return;
        }
        _camera.backgroundColor = new Color(12f / 255f, 12f / 255f, 18f / 255f);

        // ── Выбор класса: логика в UI, старт через HandleClassSelection ──
        if (mode == Screens.ClassSelect)
        {
            Ui.DrawClassSelect();
            return;
        }

        // ── Мультиплеер ──
        if (mode == Screens.MpGame)
        {
            Game.HandleInput(InputState.Keys, InputState.PrevKeys, dt);
            Game.UpdateMp(dt);
            return;
        }

        // ── Обычная игра ──
        Game.HandleInput(InputState.Keys, InputState.PrevKeys, dt);
        Game.Update(dt);

        // Мир перерисовывается, если есть куда смотреть
        if (Game.CurrentMap != null && Game.Player != null &&
            (mode == Screens.Game || mode == Screens.Inventory ||
             mode == Screens.Skills || mode == Screens.Shop ||
             mode == Screens.Craft || mode == Screens.Stats ||
             mode == Screens.Quests || mode == Screens.Dialogue))
            World.Draw(Game);
    }

    private void OnApplicationQuit()
    {
        Game?.Settings?.Save();
        (Game?.Audio as IDisposable)?.Dispose();
    }
}
