using System.Collections.Generic;
using Aethelgard.Render;
using Aethelgard.Systems;
using Microsoft.Xna.Framework.Input;
using UnityEngine;
using UColor = UnityEngine.Color;
using UColor32 = UnityEngine.Color32;
using UObject = UnityEngine.Object;
using Screens = Aethelgard.Systems.ScreenMode;

namespace Aethelgard
{
    /// <summary>
    /// Драйвер Unity: bootstrap игры, ввод, рендер мира, HUD.
    ///
    /// Логика целиком живёт в GameController (дословно перенесён из
    /// MonoGame-версии в этот asmdef) и в ядре Aethelgard.Core. Этот
    /// MonoBehaviour только переводит Unity-ввод в shim-типы
    /// (KeyboardState/MouseState) и рисует состояние игры спрайтами.
    /// </summary>
    public class GameApp : MonoBehaviour
    {
        private GameController _game = null!;
        private WorldView _world = null!;
        private Hud _hud = null!;
        private MenuUi _menu = null!;
        private Camera _camera = null!;
        private bool _worldStarted;

        private float _diag;
        private KeyboardState _prevKeys;
        private KeyboardState _keys;
        private static readonly Dictionary<KeyCode, Keys> KeyMap = BuildKeyMap();

        private void Awake()
        {
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;

            // 1 мировая единица = 1 пиксель дизайна. Камера видит
            // Screen.height пикселей по вертикали; письмо в WorldView.
            _camera = Camera.main != null ? Camera.main : new GameObject("Camera").AddComponent<Camera>();
            _camera.transform.position = new Vector3(0, 0, -10);
            _camera.backgroundColor = new UColor(0.06f, 0.05f, 0.05f);
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.orthographic = true;

            _game = new GameController
            {
                Audio = new AudioSystem(),
            };
            // MVP: сразу главное меню (MonoGame-флоу через MainMenuScreen
            // подключается по мере порта экранов).
            _game.ScreenMode = Screens.Menu;

            var go = new GameObject("WorldView");
            _world = go.AddComponent<WorldView>();
            _world.Init(_game);

            _hud = Hud.Create(_game);
            _menu = MenuUi.Create(_game);
            _menu.OnClassPicked = classKey =>
            {
                _game.NewGame(classKey);
                _worldStarted = true;
                Debug.Log($"[Aethelgard] NewGame({classKey}) ok: map={_game.CurrentMap.W}x{_game.CurrentMap.H}, player=({_game.Player.X},{_game.Player.Y})");
            };
            Debug.Log("[Aethelgard] boot ok, menu shown");

            Mouse.Provider = () =>
            {
                var mp = Input.mousePosition;
                float s = 1080f / Screen.height;   // экран -> дизайн 1920×1080
                bool left = Input.GetMouseButton(0);
                return new MouseState(
                    (int)(mp.x * s), (int)(mp.y * s), 0,
                    left ? ButtonState.Pressed : ButtonState.Released,
                    ButtonState.Released, ButtonState.Released,
                    ButtonState.Released, ButtonState.Released);
            };
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);

            // Экранный флоу: меню управляется uGUI-кнопками, игровой цикл —
            // только когда класс выбран (ScreenMode == Game).
            if (!_worldStarted)
            {
                if (_game.ScreenMode == Screens.ClassSelect) _menu.ShowClassSelect();
                else if (_game.ScreenMode == Screens.Menu) _menu.ShowMain();
                // Esc в выборе класса — назад в меню; Enter — подтвердить
                if (_game.ScreenMode == Screens.ClassSelect && Input.GetKeyDown(KeyCode.Escape))
                    _game.ScreenMode = Screens.Menu;
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    _menu.Confirm();
                return;
            }

            // Единый масштаб (дизайн 1080 в высоту): мышь и мышиные клики
            // GameController считает в этих координатах.
            AethelgardGame.MouseScaleX = 1080f / Screen.height;
            AethelgardGame.MouseScaleY = 1080f / Screen.height;

            _keys = ReadKeyboard();
            _game.HandleInput(_keys, _prevKeys, dt);
            _game.Update(dt);
            _prevKeys = _keys;

            _world.Refresh();
            _hud.Refresh();
            UpdateCamera();
            _diag += dt;
            if (_diag >= 3f) { _diag = 0f; Debug.Log($"[Aethelgard] cam={_camera.transform.position} ortho={_camera.orthographicSize} player=({_game.Player.X},{_game.Player.Y}) tiles={_world.TileCount} ents={_world.EntityCount}"); }
        }

        private void UpdateCamera()
        {
            // Камера следует за игроком; по вертикали видно высоту экрана
            // в пикселях дизайна (весь HUD накладывается поверх).
            _camera.orthographicSize = Screen.height / 2f;
            var p = _game.Player;
            var map = _game.CurrentMap;
            float x = p.X * Config.TileSize + Config.TileSize / 2f;
            float y = (map.H - 1 - p.Y) * Config.TileSize + Config.TileSize / 2f;
            _camera.transform.position = new Vector3(x, y, -10);
        }

        private static KeyboardState ReadKeyboard()
        {
            var pressed = new List<Keys>();
            foreach (var kv in KeyMap)
                if (Input.GetKey(kv.Key))
                    pressed.Add(kv.Value);
            return new KeyboardState(pressed);
        }

        private static Dictionary<KeyCode, Keys> BuildKeyMap()
        {
            var map = new Dictionary<KeyCode, Keys>
            {
                [KeyCode.A] = Keys.A, [KeyCode.B] = Keys.B, [KeyCode.C] = Keys.C,
                [KeyCode.D] = Keys.D, [KeyCode.E] = Keys.E, [KeyCode.F] = Keys.F,
                [KeyCode.G] = Keys.G, [KeyCode.H] = Keys.H, [KeyCode.I] = Keys.I,
                [KeyCode.J] = Keys.J, [KeyCode.K] = Keys.K, [KeyCode.L] = Keys.L,
                [KeyCode.M] = Keys.M, [KeyCode.N] = Keys.N, [KeyCode.O] = Keys.O,
                [KeyCode.P] = Keys.P, [KeyCode.Q] = Keys.Q, [KeyCode.R] = Keys.R,
                [KeyCode.S] = Keys.S, [KeyCode.T] = Keys.T, [KeyCode.U] = Keys.U,
                [KeyCode.V] = Keys.V, [KeyCode.W] = Keys.W, [KeyCode.X] = Keys.X,
                [KeyCode.Y] = Keys.Y, [KeyCode.Z] = Keys.Z,
                [KeyCode.Alpha0] = Keys.D0, [KeyCode.Alpha1] = Keys.D1,
                [KeyCode.Alpha2] = Keys.D2, [KeyCode.Alpha3] = Keys.D3,
                [KeyCode.Alpha4] = Keys.D4, [KeyCode.Alpha5] = Keys.D5,
                [KeyCode.Alpha6] = Keys.D6, [KeyCode.Alpha7] = Keys.D7,
                [KeyCode.Alpha8] = Keys.D8, [KeyCode.Alpha9] = Keys.D9,
                [KeyCode.UpArrow] = Keys.Up, [KeyCode.DownArrow] = Keys.Down,
                [KeyCode.LeftArrow] = Keys.Left, [KeyCode.RightArrow] = Keys.Right,
                [KeyCode.Space] = Keys.Space, [KeyCode.Return] = Keys.Enter,
                [KeyCode.KeypadEnter] = Keys.Enter, [KeyCode.Escape] = Keys.Escape,
                [KeyCode.Backspace] = Keys.Back, [KeyCode.Tab] = Keys.Tab,
                [KeyCode.LeftShift] = Keys.LeftShift, [KeyCode.RightShift] = Keys.RightShift,
                [KeyCode.Home] = Keys.Home, [KeyCode.End] = Keys.End,
                [KeyCode.PageUp] = Keys.PageUp, [KeyCode.PageDown] = Keys.PageDown,
                [KeyCode.Minus] = Keys.OemMinus, [KeyCode.Period] = Keys.OemPeriod,
                [KeyCode.Comma] = Keys.OemComma, [KeyCode.LeftBracket] = Keys.OemOpenBrackets,
                [KeyCode.RightBracket] = Keys.OemCloseBrackets, [KeyCode.BackQuote] = Keys.OemTilde,
                [KeyCode.F1] = Keys.F1, [KeyCode.F2] = Keys.F2, [KeyCode.F3] = Keys.F3,
                [KeyCode.F4] = Keys.F4, [KeyCode.F5] = Keys.F5, [KeyCode.F6] = Keys.F6,
                [KeyCode.F7] = Keys.F7, [KeyCode.F8] = Keys.F8, [KeyCode.F9] = Keys.F9,
                [KeyCode.F10] = Keys.F10, [KeyCode.F11] = Keys.F11, [KeyCode.F12] = Keys.F12,
            };
            return map;
        }
    }

    /// <summary>Рендер карты и сущностей спрайтами.</summary>
    public class WorldView : MonoBehaviour
    {
        private GameController _game = null!;
        private readonly Dictionary<string, Texture2D> _tileTex = new();
        private readonly List<SpriteRenderer> _entityPool = new();
        private Texture2D _glyphAtlas = null!;
        private int _glyphCols, _glyphRows;
        private Dictionary<string, Vector2Int>? _glyphIndex;
        private Transform _tilesRoot = null!, _entitiesRoot = null!;

        private string _mapKey = "";
        private readonly Dictionary<(int, int), GameObject> _tileMap = new();

        public void Init(GameController game)
        {
            _game = game;
            _tilesRoot = new GameObject("Tiles").transform;
            _tilesRoot.SetParent(transform, false);
            _entitiesRoot = new GameObject("Entities").transform;
            _entitiesRoot.SetParent(transform, false);

            _glyphAtlas = Resources.Load<Texture2D>("GlyphAtlas/atlas")!;
            var manifest = Resources.Load<TextAsset>("GlyphAtlas/glyph_manifest")!;
            Debug.Log($"[Aethelgard] glyphAtlas={_glyphAtlas != null} manifest={manifest != null}");
            ParseManifest(manifest.text);
            Debug.Log($"[Aethelgard] glyphs parsed={_glyphIndex!.Count} cols={_glyphCols} rows={_glyphRows}");
        }

        private void ParseManifest(string json)
        {
            // Манифест {"cell":64,"cols":16,"rows":7,"glyphs":{"!":{"col":0,"row":0},...}}.
            // Парсим regex'ом — JSON может быть с отступами.
            _glyphIndex = new Dictionary<string, Vector2Int>();
            _glyphCols = RegexInt(json, "\"cols\"");
            _glyphRows = RegexInt(json, "\"rows\"");
            var re = new System.Text.RegularExpressions.Regex(
                "\"(?<k>(?:[^\"\\\\]|\\\\.)*)\":\\s*\\{\\s*\"col\":\\s*(?<c>\\d+),\\s*\"row\":\\s*(?<r>\\d+)");
            foreach (System.Text.RegularExpressions.Match m in re.Matches(json))
                _glyphIndex[Unescape(m.Groups["k"].Value)] =
                    new Vector2Int(int.Parse(m.Groups["c"].Value), int.Parse(m.Groups["r"].Value));
        }

        private static int RegexInt(string s, string key)
        {
            var m = System.Text.RegularExpressions.Regex.Match(s, key + @"\s*:\s*(\d+)");
            return m.Success ? int.Parse(m.Groups[1].Value) : 0;
        }

        private static string Unescape(string s) =>
            s == "\"\\\"" ? "\"" : s == "\\\\" ? "\\" : s;

        private SpriteRenderer GlyphSprite(char ch, UColor color, Transform parent)
        {
            var go = new GameObject($"glyph_{ch}");
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = "Default";
            if (_glyphIndex != null && _glyphIndex.TryGetValue(ch.ToString(), out var cell))
            {
                float w = 1f / _glyphCols;
                float h = 1f / _glyphRows;
                var sprite = Sprite.Create(_glyphAtlas,
                    new Rect(cell.x * w, 1f - (cell.y + 1) * h, w, h),
                    new Vector2(0.5f, 0.5f), 1f);
                sr.sprite = sprite;
            }
            sr.color = color;
            return sr;
        }

        private Texture2D TileTexture(string kind, int variant, float light)
        {
            int band = (int)Mathf.Round(Mathf.Clamp(light, 0.3f, 1f) * 20);
            string key = $"{kind}_{variant}_{band}";
            if (_tileTex.TryGetValue(key, out var tex)) return tex;
            tex = ToUnityTexture(TileArt.GenerateTile(kind, variant, band / 20f));
            _tileTex[key] = tex;
            return tex;
        }

        /// <summary>PixelCanvas хранит premultiplied alpha; Unity ждёт прямой
        /// альфа-канал — разворачиваем при загрузке.</summary>
        private static Texture2D ToUnityTexture(PixelCanvas canvas)
        {
            var tex = new Texture2D(canvas.Width, canvas.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
            };
            var px = new UColor32[canvas.Pixels.Length];
            for (int i = 0; i < px.Length; i++)
            {
                var c = canvas.Pixels[i];
                float a = c.A / 255f;
                byte r, g, b;
                if (a > 0.0001f && a < 1f)
                {
                    r = (byte)Mathf.Min(255, c.R / a + 0.5f);
                    g = (byte)Mathf.Min(255, c.G / a + 0.5f);
                    b = (byte)Mathf.Min(255, c.B / a + 0.5f);
                }
                else { r = (byte)c.R; g = (byte)c.G; b = (byte)c.B; }
                px[i] = new UColor32(r, g, b, (byte)c.A);
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>Полная перестройка тайлов текущей карты (при смене карты
        /// или росте исследованной области).</summary>
        public void Refresh()
        {
            var map = _game.CurrentMap;
            string mapKey = map.LocationKey ?? $"map_{map.Depth}_{map.W}x{map.H}";
            if (mapKey != _mapKey)
            {
                _mapKey = mapKey;
                RebuildTiles(map);
            }
            else
            {
                // инкрементально добавляем только что исследованные тайлы
                foreach (var cell in map.Explored)
                    if (!_tileMap.ContainsKey(cell))
                        AddTile(map, cell.Item1, cell.Item2);
            }
            RefreshEntities(map);
        }

        private void RebuildTiles(Dungeon map)
        {
            foreach (var go in _tileMap.Values) Destroy(go);
            _tileMap.Clear();
            foreach (var cell in map.Explored)
                AddTile(map, cell.Item1, cell.Item2);
        }

        private void AddTile(Dungeon map, int x, int y)
        {
            var cell = (x, y);
            if (_tileMap.ContainsKey(cell) || x < 0 || y < 0 || x >= map.W || y >= map.H) return;
            string kind = TileArt.TileKindAt(map, x, y);
            int variant = (x * 31 + y * 17) % 4;
            var tex = TileTexture(kind, variant, 1f);
            var go = new GameObject($"tile_{x}_{y}");
            go.transform.SetParent(_tilesRoot, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 1f);
            go.transform.position = TilePos(map, x, y);
            _tileMap[cell] = go;
        }

        private static Vector3 TilePos(Dungeon map, int x, int y)
            => new(x * Config.TileSize + Config.TileSize / 2f,
                   (map.H - 1 - y) * Config.TileSize + Config.TileSize / 2f, 0f);

        public int TileCount => _tileMap.Count;
        public int EntityCount => _entityPool.Count;

        private void RefreshEntities(Dungeon map)
        {
            int needed = 0;
            void Draw(char ch, UColor color, Vector3 pos, int layer)
            {
                var sr = needed < _entityPool.Count ? _entityPool[needed] : AddEntity();
                sr.gameObject.name = $"ent_{ch}_{layer}";
                var existing = sr.sprite;
                SetGlyph(sr, ch);
                sr.color = color;
                sr.transform.position = pos;
                sr.sortingOrder = layer;
                needed++;
            }

            foreach (var m in map.Monsters)
                Draw(m.Ch, m.Color, TilePos(map, m.X, m.Y), 10);
            foreach (var kv in map.Items)
                Draw('?', new UColor(0.94f, 0.78f, 0.27f), TilePos(map, kv.Key.Item1, kv.Key.Item2), 9);
            foreach (var kv in map.Harvestables)
                Draw('§', new UColor(0.55f, 0.75f, 0.4f), TilePos(map, kv.Key.Item1, kv.Key.Item2), 8);

            var p = _game.Player;
            Draw(p.Ch, p.Color, TilePos(map, p.X, p.Y), 20);

            for (int i = _entityPool.Count - 1; i >= needed; i--)
            {
                Destroy(_entityPool[i].gameObject);
                _entityPool.RemoveAt(i);
            }
        }

        private void SetGlyph(SpriteRenderer sr, char ch)
        {
            if (_glyphIndex != null && _glyphIndex.TryGetValue(ch.ToString(), out var cell))
            {
                // Rect — в ПИКСЕЛЯХ текстуры (не UV), PPU=1 → глиф 64 юнита
                const int cellPx = 64;
                sr.sprite = Sprite.Create(_glyphAtlas,
                    new Rect(cell.x * cellPx, (_glyphRows - 1 - cell.y) * cellPx, cellPx, cellPx),
                    new Vector2(0.5f, 0.5f), 1f);
            }
        }

        private SpriteRenderer AddEntity()
        {
            var go = new GameObject("ent");
            go.transform.SetParent(_entitiesRoot, false);
            var sr = go.AddComponent<SpriteRenderer>();
            _entityPool.Add(sr);
            return sr;
        }
    }

    /// <summary>HUD на uGUI: HP/мана/опыт, золото, уровень, сообщение.</summary>
    public class Hud : MonoBehaviour
    {
        private GameController _game = null!;
        private RectTransform _hpFill = null!, _mpFill = null!, _xpFill = null!;
        private UnityEngine.UI.Text _info = null!;
        private UnityEngine.UI.Text _message = null!;
        private Canvas _canvas = null!;

        public static Hud Create(GameController game)
        {
            var go = new GameObject("Hud");
            var hud = go.AddComponent<Hud>();
            hud._game = game;
            hud.Build();
            return hud;
        }

        private void Build()
        {
            _canvas = new GameObject("Canvas").AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>().uiScaleMode =
                UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            var scaler = _canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.referenceResolution = new Vector2(1920, 1080);
            _canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            _canvas.transform.SetParent(transform, false);

            _hpFill = Bar(_canvas.transform, new Vector2(30, 70), new Vector2(420, 26), new UColor(0.78f, 0.18f, 0.16f));
            _mpFill = Bar(_canvas.transform, new Vector2(30, 110), new Vector2(420, 26), new UColor(0.22f, 0.45f, 0.85f));
            _xpFill = Bar(_canvas.transform, new Vector2(30, 150), new Vector2(420, 18), new UColor(0.85f, 0.72f, 0.2f));
            _info = Label(_canvas.transform, new Vector2(30, 180), 22, UColor.white);
            _message = Label(_canvas.transform, new Vector2(30, 20), 26, new UColor(1f, 0.95f, 0.75f));
        }

        private RectTransform Bar(Transform parent, Vector2 pos, Vector2 size, UColor color)
        {
            var bgGo = new GameObject("bar");
            bgGo.transform.SetParent(parent, false);
            var bg = bgGo.AddComponent<UnityEngine.UI.Image>();
            bg.color = new UColor(0f, 0f, 0f, 0.55f);
            var rt = bg.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var fillGo = new GameObject("fill");
            fillGo.transform.SetParent(bgGo.transform, false);
            var fill = fillGo.AddComponent<UnityEngine.UI.Image>();
            fill.color = color;
            var frt = fill.rectTransform;
            frt.anchorMin = new Vector2(0f, 0f);
            frt.anchorMax = new Vector2(0f, 1f);
            frt.pivot = new Vector2(0f, 0.5f);
            frt.anchoredPosition = Vector2.zero;
            frt.sizeDelta = new Vector2(size.x, 0f);
            return frt;
        }

        private UnityEngine.UI.Text Label(Transform parent, Vector2 pos, int size, UColor color)
        {
            var go = new GameObject("label");
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<UnityEngine.UI.Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(900, size + 10);
            return text;
        }

        public void Refresh()
        {
            var p = _game.Player;
            SetBar(_hpFill, p.Hp, p.MaxHp);
            SetBar(_mpFill, p.Mana, p.MaxMana);
            SetBar(_xpFill, p.Xp, p.XpNext);
            _info.text = $"{p.Name} · ур. {p.Level} · {p.ClassName} · золото {p.Gold}";
            if (_game.LastMessage != null && _game.MessageTimer > 0f)
                _message.text = _game.LastMessage;
            else
                _message.text = "";
        }

        private static void SetBar(RectTransform fill, int value, int max)
        {
            max = max <= 0 ? 1 : max;
            float parentW = fill.parent.GetComponent<RectTransform>().sizeDelta.x;
            fill.sizeDelta = new Vector2(parentW * Mathf.Clamp01(value / (float)max), 0f);
        }
    }
}
