// uGUI-порт MP-веток render/MainMenuScreen.cs (Mode.Multiplayer и Mode.MpJoin):
// экран menu_multiplayer — «Хост» / «Подключиться» / «Назад», экран
// menu_mp_join — поле адреса «host:port» и пароля. Действия — те же вызовы,
// что desktop Desktop/Game.cs ожидает от меню:
//   «Продолжить/Новая игра (хост)» → GameController.StartMpHost(fresh, "warrior")
//     (desktop OnMpHost; хост-класс «warrior», desktop тоже не выбирает класс здесь);
//   «Подключиться» → GameController.StartMpJoin("host:port") (desktop OnMpJoin).
// Отличие от desktop: ветка «Создать игру» (PendingMpHost → class_select →
// StartMpHost(fresh, выбранный класс)) здесь не показана — Unity MenuUi не
// потребляет PendingMpHost; если понадобится, включить по флагу PendingMpHost.
// Focus в InputField: свойство InputFocused — GameApp пропускает HandleInput.
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UColor = UnityEngine.Color;
using URect = UnityEngine.Rect;
using XnaRect = Microsoft.Xna.Framework.Rectangle;
using Screens = Aethelgard.Systems.ScreenMode;

namespace Aethelgard
{
    public class MpUi : MonoBehaviour
    {
        private GameController _game = null!;
        private UiKitCanvas _kitMenu = null!;   // ScreenMode.MenuMultiplayer
        private UiKitCanvas _kitJoin = null!;   // ScreenMode.MenuMpJoin

        // ── Экран menu_multiplayer (кнопки как MainMenuScreen.MpItems) ──
        private static readonly string[] MenuBtns =
        {
            "Продолжить игру (хост)",   // HasMpSave() — desktop «continue»
            "Новая игра (хост)",        // desktop «new»
            "Подключиться к игре",      // desktop «join» → menu_mp_join
            "Назад",                    // desktop MenuBack → главное меню
        };
        private static readonly XnaRect MenuPanel = new(680, 230, 560, 470);
        private static XnaRect MenuBtnRect(int i) => new(760, 330 + i * 78, 400, 56);

        // ── Экран menu_mp_join ──
        private static readonly XnaRect JoinPanel = new(680, 230, 560, 470);
        private static readonly XnaRect AddrRect = new(720, 356, 480, 44);
        private static readonly XnaRect PassRect = new(720, 448, 480, 44);
        private static readonly XnaRect JoinConnectRect = new(760, 524, 400, 56);
        private static readonly XnaRect JoinBackRect = new(760, 594, 400, 56);

        private readonly Image[] _menuBg = new Image[MenuBtns.Length];
        private readonly Text[] _menuLbl = new Text[MenuBtns.Length];
        private GameObject _menuContinueBg = null!, _menuContinueLbl = null!;

        private InputField _addr = null!, _pass = null!;
        private Image _addrBg = null!, _passBg = null!;
        private readonly Image[] _joinBg = new Image[2];
        private readonly Text[] _joinLbl = new Text[2];
        private Text _status = null!;

        /// <summary>GameApp подписывается: после успешного StartMpHost/
        /// StartMpJoin нужно запустить мир (_worldStarted = true), иначе
        /// ранний return в Update не даст работать MP-игре.</summary>
        public System.Action? OnMpStarted;

        public static MpUi Create(GameController game)
        {
            var go = new GameObject("MpUi");
            var ui = go.AddComponent<MpUi>();
            ui._game = game;
            ui._kitMenu = new UiKitCanvas("MpMenuCanvas");
            ui._kitJoin = new UiKitCanvas("MpJoinCanvas");
            ui.BuildMenu();
            ui.BuildJoin();
            return ui;
        }

        /// <summary>InputField адреса/пароля в фокусе — GameApp в этом кадре
        /// пропускает игровой HandleInput (печатаем в поле, а не в мир).</summary>
        public bool InputFocused =>
            (_addr != null && _addr.gameObject.activeInHierarchy && _addr.isFocused)
            || (_pass != null && _pass.gameObject.activeInHierarchy && _pass.isFocused);

        public void SetVisible(bool on)
        {
            bool join = on && _game.ScreenMode == Screens.MenuMpJoin;
            bool menu = on && _game.ScreenMode == Screens.MenuMultiplayer;
            _kitJoin.SetActive(join);
            _kitMenu.SetActive(menu);
            if (!(join || menu))
            {
                if (_addr != null && _addr.isFocused) _addr.DeactivateInputField();
                if (_pass != null && _pass.isFocused) _pass.DeactivateInputField();
            }
            else if (join && _addr.text.Length == 0)
            {
                _addr.text = "localhost";   // desktop: MpIp = "localhost"
            }
        }

        // ── Построение экранов ──

        private void BuildMenu()
        {
            Background(_kitMenu);
            _kitMenu.Panel(UR(MenuPanel), dimWorldBehind: false);
            _kitMenu.Centered(
                UR(new XnaRect(MenuPanel.X, MenuPanel.Y + 24, MenuPanel.Width, 44)),
                "Мультиплеер", 2, UiKitCanvas.Theme.Gold);

            for (int i = 0; i < MenuBtns.Length; i++)
            {
                var r = MenuBtnRect(i);
                _menuBg[i] = _kitMenu.Button(UR(r), "", 3);
                _menuLbl[i] = _kitMenu.Centered(UR(r), MenuBtns[i], 3,
                    UiKitCanvas.Theme.TextDim);
            }
            // «Продолжить» — только при наличии MP-сейва (desktop HasMpSave)
            _menuContinueBg = _menuBg[0].gameObject;
            _menuContinueLbl = _menuLbl[0].gameObject;

            _kitMenu.Centered(
                UR(new XnaRect(MenuPanel.X, MenuPanel.Y + MenuPanel.Height - 34,
                    MenuPanel.Width, 24)),
                "Esc — назад", 4, UiKitCanvas.Theme.TextMuted);
        }

        private void BuildJoin()
        {
            Background(_kitJoin);
            _kitJoin.Panel(UR(JoinPanel), dimWorldBehind: false);
            _kitJoin.Centered(
                UR(new XnaRect(JoinPanel.X, JoinPanel.Y + 24, JoinPanel.Width, 44)),
                "Подключение к серверу", 2, UiKitCanvas.Theme.Gold);

            _kitJoin.Label(
                UR(new XnaRect(AddrRect.X, AddrRect.Y - 28, AddrRect.Width, 24)),
                "Адрес (host:port):", 4, UiKitCanvas.Theme.Text, TextAnchor.LowerLeft,
                wrap: false);
            (_addrBg, _addr) = MakeInput(_kitJoin, AddrRect, "localhost");

            // Пароль: поле сохранено для паритета (desktop передаёт null в
            // GameClient.Connect; GameController.StartMpJoin пока шлёт null).
            _kitJoin.Label(
                UR(new XnaRect(PassRect.X, PassRect.Y - 28, PassRect.Width, 24)),
                "Пароль (если задан):", 4, UiKitCanvas.Theme.Text, TextAnchor.LowerLeft,
                wrap: false);
            (_passBg, _pass) = MakeInput(_kitJoin, PassRect, "пароль…");

            var connectR = JoinConnectRect;
            _joinBg[0] = _kitJoin.Button(UR(connectR), "", 3);
            _joinLbl[0] = _kitJoin.Centered(UR(connectR), "Подключиться", 3,
                UiKitCanvas.Theme.TextDim);
            var backR = JoinBackRect;
            _joinBg[1] = _kitJoin.Button(UR(backR), "", 3);
            _joinLbl[1] = _kitJoin.Centered(UR(backR), "Назад", 3,
                UiKitCanvas.Theme.TextDim);

            _kitJoin.Centered(
                UR(new XnaRect(JoinPanel.X, JoinPanel.Y + JoinPanel.Height - 34,
                    JoinPanel.Width, 24)),
                "Enter — подключиться · Esc — назад", 4, UiKitCanvas.Theme.TextMuted);

            // Статус/ошибка (game.LastMessage, как Hud-сообщение на desktop)
            _status = _kitJoin.Centered(
                UR(new XnaRect(JoinPanel.X, JoinPanel.Y + JoinPanel.Height + 12,
                    JoinPanel.Width, 24)),
                "", 4, UiKitCanvas.Theme.Danger);
        }

        private void Background(UiKitCanvas kit)
        {
            // Фон за меню — мир ещё не создан (как MenuUi: тёмная заливка)
            kit.RectImage(UR(new XnaRect(0, 0, AethelgardGame.ScreenW, AethelgardGame.ScreenH)),
                new UColor(0.07f, 0.06f, 0.05f));
        }

        private static (Image bg, InputField field) MakeInput(
            UiKitCanvas kit, XnaRect r, string placeholder)
        {
            var go = new GameObject("mp_input");
            go.transform.SetParent(kit.Root, false);
            var img = go.AddComponent<Image>();
            img.color = UiKitCanvas.Theme.BgSlot;
            Place(img.rectTransform, r);

            var field = go.AddComponent<InputField>();
            field.textComponent = FieldText(go.transform, UiKitCanvas.Theme.Text);
            var ph = FieldText(go.transform, UiKitCanvas.Theme.TextMuted);
            ph.fontStyle = FontStyle.Italic;
            ph.text = placeholder;
            field.placeholder = ph;
            field.text = "";
            return (img, field);
        }

        private static Text FieldText(Transform parent, UColor color)
        {
            var t = new GameObject("text").AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = UiKitCanvas.FontSmall;
            t.color = color;
            t.alignment = TextAnchor.MiddleLeft;
            t.raycastTarget = false;
            var rt = t.rectTransform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(8f, 2f);
            rt.offsetMax = new Vector2(-8f, -2f);
            return t;
        }

        // XnaRect → RectTransform в design-координатах (как UiKitCanvas.Place;
        // нижнюю границу считаем явно: Y + Height).
        private static void Place(RectTransform rt, XnaRect r)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(r.X, AethelgardGame.ScreenH - (r.Y + r.Height));
            rt.sizeDelta = new Vector2(r.Width, r.Height);
        }

        private static URect UR(XnaRect r) => new(r.X, r.Y, r.Width, r.Height);

        // ── Кадровое обновление ──

        public void Refresh()
        {
            bool join = _game.ScreenMode == Screens.MenuMpJoin;
            if (join) RefreshJoin(); else RefreshMenu();
            HandleKeys(join);
        }

        private void RefreshMenu()
        {
            bool hasSave = HasMpSave();
            _menuContinueBg.SetActive(hasSave);
            _menuContinueLbl.SetActive(hasSave);

            for (int i = 0; i < MenuBtns.Length; i++)
            {
                bool hot = UiInput.Hot(MenuBtnRect(i));
                // Ховер — смена bg.color (Theme.BgSlot / BgSlotHot)
                _menuBg[i].color = hot ? UiKitCanvas.Theme.BgSlotHot
                    : UiKitCanvas.Theme.BgSlot;
                _menuLbl[i].color = hot ? UiKitCanvas.Theme.GoldBright
                    : UiKitCanvas.Theme.TextDim;
            }

            if (UiInput.Clicked(MenuBtnRect(0)))
            {
                // «Продолжить» из MP-сейва (fresh=false) / «Новая» (fresh=true),
                // как desktop ActivateMp → OnMpHost(freshStart)
                HostGame(!hasSave);
                return;
            }
            if (UiInput.Clicked(MenuBtnRect(1))) { HostGame(true); return; }
            if (UiInput.Clicked(MenuBtnRect(2)))
            { _game.ScreenMode = Screens.MenuMpJoin; return; }   // desktop «join»
            if (UiInput.Clicked(MenuBtnRect(3)))
                _game.ScreenMode = Screens.Menu;                 // desktop MenuBack
        }

        private void RefreshJoin()
        {
            // Поля: ховер/фокус — смена цвета; фокус по клику (raycast'ы
            // канвы блокирует CanvasGroup → EventSystem сам не получит клик).
            FieldVisual(_addrBg, AddrRect);
            FieldVisual(_passBg, PassRect);
            if (UiInput.Clicked(AddrRect)) _addr.ActivateInputField();
            if (UiInput.Clicked(PassRect)) _pass.ActivateInputField();

            for (int i = 0; i < 2; i++)
            {
                var r = i == 0 ? JoinConnectRect : JoinBackRect;
                bool hot = UiInput.Hot(r);
                _joinBg[i].color = hot ? UiKitCanvas.Theme.BgSlotHot
                    : UiKitCanvas.Theme.BgSlot;
                _joinLbl[i].color = hot ? UiKitCanvas.Theme.GoldBright
                    : UiKitCanvas.Theme.TextDim;
            }
            if (UiInput.Clicked(JoinConnectRect)) { Connect(); return; }
            if (UiInput.Clicked(JoinBackRect))
                _game.ScreenMode = Screens.MenuMultiplayer;      // desktop MenuBack

            // Сообщение об ошибке подключения (ShowMessage, таймер 3 c)
            _status.text = _game.MessageTimer > 0f ? _game.LastMessage ?? "" : "";
        }

        private void FieldVisual(Image bg, XnaRect r)
        {
            bg.color = UiInput.Hot(r)
                ? UiKitCanvas.Theme.BgSlotHot : UiKitCanvas.Theme.BgSlot;
        }

        private void HandleKeys(bool join)
        {
            // Esc/Enter в меню — как desktop HandleMenuInput / MenuBack.
            // Пока поле в фокусе, клавиши съедает InputField (GameApp и так
            // пропускает HandleInput при InputFocused).
            if (InputFocused) return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                _game.ScreenMode = join ? Screens.MenuMultiplayer : Screens.Menu;
            }
            else if (join && (Input.GetKeyDown(KeyCode.Return)
                || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                Connect();
            }
        }

        // ── Действия (desktop Desktop/Game.cs WireMenu) ──

        private void HostGame(bool fresh)
        {
            // desktop OnMpHost: StartMpHost(fresh, "warrior"); при неудаче —
            // сообщение и остаёмся в меню.
            if (_game.StartMpHost(fresh, "warrior"))
                OnMpStarted?.Invoke();
            else
                _game.ShowMessage("Не удалось запустить сервер мультиплеера");
        }

        /// <summary>Подключиться по адресу «host:port» (desktop ConnectMp:
        /// порт по умолчанию 9000 или ETHELGARD_PORT, «host:port» в поле
        /// переопределяет порт).</summary>
        private void Connect()
        {
            string host = _addr.text.Trim();
            if (host.Length == 0) host = "localhost";
            int port = int.TryParse(Environment.GetEnvironmentVariable("ETHELGARD_PORT"),
                out var p) ? p : 9000;
            if (port <= 0 || port >= 65536) port = 9000;
            var parts = host.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[1], out var hp)
                && hp > 0 && hp < 65536)
            {
                host = parts[0];
                port = hp;
            }
            // Пароль из _pass.text пока не передаётся: GameController.StartMpJoin
            // жёстко шлёт null в GameClient.Connect (паритет desktop OnMpJoin).
            if (_game.StartMpJoin($"{host}:{port}"))
                OnMpStarted?.Invoke();
            else
                _game.ShowMessage("Не удалось подключиться к серверу");
        }

        /// <summary>Наличие MP-сейва — desktop MainMenuScreen.HasMpSave.</summary>
        private static bool HasMpSave()
        {
            return File.Exists(Path.Combine(Config.DataDir, "savegame_mp_world.json"))
                || File.Exists(Path.Combine(Config.DataDir, "savegame_mp_players.json"));
        }
    }
}
