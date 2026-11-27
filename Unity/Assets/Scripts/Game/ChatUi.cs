// uGUI-порт render/ChatPanel.cs — панель журнала с вкладками «Всё · Бой ·
// Предметы · Общение · Опыт» поверх мира в мультиплеере. Вкладки рисуются
// строго в прямоугольниках Aethelgard.Render.ChatPanel.TabRect(i) — GameController
// обрабатывает клики по вкладкам в этих же координатах (design 1920×1080).
// Список строк — последние N строк выбранной вкладки (Chat.AllSafe/TabSafe),
// прокрутка — game.ChatScroll как на desktop. Полоса ввода при открытом
// вводе чата: uGUI InputField (LegacyRuntime.ttf); Enter отправляет через
// MpClient.SendChat (MP) / Chat.Social (SP-журнал) — те же вызовы, что в
// GameController.HandleMpInput / HandleChatInput. GameApp при InputFocused
// пропускает игровой HandleInput, поэтому двойного набора нет.
using UnityEngine;
using UnityEngine.UI;
using UColor = UnityEngine.Color;
using URect = UnityEngine.Rect;
using XnaRect = Microsoft.Xna.Framework.Rectangle;
using ChatPanel = Aethelgard.Render.ChatPanel;
using Screens = Aethelgard.Systems.ScreenMode;

namespace Aethelgard
{
    public class ChatUi : MonoBehaviour
    {
        private GameController _game = null!;
        private UiKitCanvas _kit = null!;

        // ── Геометрия (design 1920×1080): вкладки — ChatPanel.TabRect(i),
        // список строк над вкладками, полоса ввода под ними. ──
        private static XnaRect PanelRect() => new(8, 782, 1090, 292);
        private static XnaRect ListRect() => new(16, 782, 1074, 200);
        private static XnaRect InputRect() => new(16, 1028, 1074, 46);
        private const int LineH = 18;
        private const int VisibleLines = 11;

        private readonly Image[] _tabBg = new Image[5];
        private readonly Text[] _tabLabel = new Text[5];
        private readonly Image[] _tabUnder = new Image[5];
        private readonly XnaRect[] _tabRects = new XnaRect[5];

        private readonly Text[] _lines = new Text[VisibleLines];
        private Text _players = null!, _hint = null!;
        private GameObject _inputGo = null!;
        private InputField _input = null!;

        // Автофокус при открытии ввода; глушение клавиш сразу после потери
        // фокуса (см. InputFocused) — против «залипшего» Enter/Y.
        private bool _wasOpen;
        private bool _wasFocused;
        private float _suppressUntil;

        public static ChatUi Create(GameController game)
        {
            var go = new GameObject("ChatUi");
            var ui = go.AddComponent<ChatUi>();
            ui._game = game;
            ui._kit = new UiKitCanvas("ChatCanvas");
            ui.Build();
            return ui;
        }

        /// <summary>InputField полосы ввода в фокусе (или окно «глушения»
        /// сразу после отправки) — GameApp в этом кадре пропускает
        /// HandleInput, чтобы символы не попадали в игровое управление.</summary>
        public bool InputFocused =>
            RawFocused || Time.unscaledTime < _suppressUntil;

        private bool RawFocused =>
            _input != null && _input.gameObject.activeInHierarchy && _input.isFocused;

        /// <summary>Панель показывается только в MP: MpClient подключён или
        /// экран MpGame. GameApp передаёт то же условие — дублируем здесь,
        /// рендер только отображает состояние GameController.</summary>
        public void SetVisible(bool on)
        {
            bool mp = _game.MpClient != null || _game.ScreenMode == Screens.MpGame;
            _kit.SetActive(on && mp);
            if (!(on && mp) && RawFocused)
                _input.DeactivateInputField();
        }

        private void Build()
        {
            _kit.Panel(UR(PanelRect()), dimWorldBehind: false);

            // Вкладки — строго в ChatPanel.TabRect(i): GameController читает
            // клики по этим же прямоугольникам (HandleMpInput/HandleInput).
            for (int i = 0; i < ChatPanel.TabNames.Length; i++)
            {
                var r = ChatPanel.TabRect(i);
                _tabRects[i] = r;
                _tabBg[i] = _kit.Tab(UR(r), "", false);
                _tabLabel[i] = _kit.Centered(UR(r), ChatPanel.TabNames[i], 4,
                    UiKitCanvas.Theme.TextDim);
                // золотое подчёркивание выбранной вкладки (desktop ChatPanel)
                _tabUnder[i] = _kit.RectImage(
                    UR(new XnaRect(r.X, r.Y + r.Height - 3, r.Width, 3)),
                    UiKitCanvas.Theme.BorderSoft);
            }

            // Строки журнала (пул, 11 строк)
            var list = ListRect();
            for (int i = 0; i < VisibleLines; i++)
                _lines[i] = _kit.Label(
                    UR(new XnaRect(list.X + 4, list.Y + i * LineH, list.Width - 8, LineH)),
                    "", 4, UiKitCanvas.Theme.Text, TextAnchor.UpperLeft, wrap: false);

            // Игроки онлайн (из снапшота MpClient) — правый верх списка
            var panel = PanelRect();
            _players = _kit.Label(
                UR(new XnaRect(panel.X + panel.Width - 350, list.Y + 4, 340, 20)),
                "", 4, UiKitCanvas.Theme.TextMuted, TextAnchor.UpperRight, wrap: false);

            // Подсказка (видна, когда полоса ввода закрыта)
            _hint = _kit.Label(UR(InputRect()),
                "Y — написать · PgUp/PgDn — прокрутка",
                4, UiKitCanvas.Theme.TextMuted, TextAnchor.MiddleLeft, wrap: false);

            BuildInput();
        }

        // Полоса ввода: Image + InputField в координатах InputRect().
        // CanvasGroup канвы блокирует raycast'ы, поэтому фокус берём явно:
        // клик по полосе (UiInput.Clicked) → ActivateInputField.
        private void BuildInput()
        {
            var r = InputRect();
            _inputGo = new GameObject("chat_input");
            _inputGo.transform.SetParent(_kit.Root, false);
            var img = _inputGo.AddComponent<Image>();
            img.color = UiKitCanvas.Theme.BgSoft;
            Place(img.rectTransform, r);

            _input = _inputGo.AddComponent<InputField>();
            _input.textComponent = FieldText(_inputGo.transform, UiKitCanvas.Theme.Text);
            var ph = FieldText(_inputGo.transform, UiKitCanvas.Theme.TextMuted);
            ph.fontStyle = FontStyle.Italic;
            ph.text = "Сообщение…  (Enter — отправить · Esc — отмена)";
            _input.placeholder = ph;
            _input.text = "";
            _input.onSubmit.AddListener(_ => SubmitChat());
            _inputGo.SetActive(false);
        }

        private static Text FieldText(Transform parent, UColor color)
        {
            var t = new GameObject("text").AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = UiKitCanvas.FontSmall;
            t.color = color;
            t.alignment = TextAnchor.MiddleLeft;
            t.supportRichText = false;
            t.raycastTarget = false;
            var rt = t.rectTransform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(8f, 2f);
            rt.offsetMax = new Vector2(-8f, -2f);
            return t;
        }

        // XnaRect → RectTransform в design-координатах (низ экрана — опора,
        // как UiKitCanvas.Place; y-границы считаем явно: X+Width / Y+Height).
        private static void Place(RectTransform rt, XnaRect r)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(r.X, AethelgardGame.ScreenH - (r.Y + r.Height));
            rt.sizeDelta = new Vector2(r.Width, r.Height);
        }

        private static URect UR(XnaRect r) => new(r.X, r.Y, r.Width, r.Height);

        public void Refresh()
        {
            RefreshTabs();
            RefreshLines();
            RefreshPlayers();
            RefreshInput();

            // Глушение клавиш после потери фокуса (Enter/символы, набранные
            // в момент деактивации поля, не должны дойти до GameController).
            bool focused = RawFocused;
            if (_wasFocused && !focused)
                _suppressUntil = Time.unscaledTime + 0.25f;
            _wasFocused = focused;
        }

        private void RefreshTabs()
        {
            for (int i = 0; i < _tabRects.Length; i++)
            {
                bool active = i == _game.ChatViewTab;
                bool hot = UiInput.Hot(_tabRects[i]);
                _tabBg[i].color = active ? UiKitCanvas.Theme.BgSlotSel
                    : hot ? UiKitCanvas.Theme.BgSlotHot : UiKitCanvas.Theme.BgSoft;
                _tabLabel[i].color = active ? UiKitCanvas.Theme.GoldBright
                    : UiKitCanvas.Theme.TextDim;
                _tabUnder[i].color = active ? UiKitCanvas.Theme.Gold
                    : UiKitCanvas.Theme.BorderSoft;
                if (UiInput.Clicked(_tabRects[i]))
                {
                    // как desktop: выбор вкладки + сброс прокрутки
                    _game.ChatViewTab = i;
                    _game.ChatScroll = 0;
                }
            }
        }

        private void RefreshLines()
        {
            var list = _game.ChatViewTab switch
            {
                1 => GameChat.Instance.TabSafe(GameChat.Tab.Combat),
                2 => GameChat.Instance.TabSafe(GameChat.Tab.Items),
                3 => GameChat.Instance.TabSafe(GameChat.Tab.Social),
                4 => GameChat.Instance.TabSafe(GameChat.Tab.Xp),
                _ => GameChat.Instance.AllSafe,
            };
            int maxScroll = Math.Max(0, list.Count - VisibleLines);
            int scroll = Math.Clamp(_game.ChatScroll, 0, maxScroll);
            int start = list.Count - VisibleLines - scroll;
            for (int i = 0; i < VisibleLines; i++)
            {
                int idx = start + i;
                if (idx < 0 || idx >= list.Count)
                {
                    _lines[i].text = "";
                    continue;
                }
                var e = list[idx];
                // Обрезка по ширине панели (desktop режет по MeasureString)
                string text = e.Text.Length > 95 ? e.Text[..95] : e.Text;
                _lines[i].text = text;
                _lines[i].color = e.Color;   // implicit XNA Color → UColor
            }
        }

        private void RefreshPlayers()
        {
            var snap = _game.MpSnapshot;
            if (snap == null)
            {
                _players.text = "";
                return;
            }
            string names = string.Join(", ", snap.Players.Select(p =>
                p.Pid == snap.MyPid ? p.ClassName + " [вы]" : $"{p.ClassName} (P{p.Pid})"));
            if (names.Length > 60) names = names[..57] + "…";
            _players.text = $"Игроков: {snap.Players.Count}"
                + (names.Length > 0 ? " · " + names : "");
        }

        private void RefreshInput()
        {
            // Открытый ввод: MP — ChatActive/ChatInput, SP-журнал —
            // ChatInputOpen/ChatLogInput (та же панель в обеих ветках).
            bool open = _game.ChatActive || _game.ChatInputOpen;
            if (open && !_wasOpen && !RawFocused)
                _input.ActivateInputField();   // автофокус при открытии
            _wasOpen = open;

            if (open)
            {
                _inputGo.SetActive(true);
                string buf = _game.ChatActive ? _game.ChatInput : _game.ChatLogInput;
                // Пока поле в фокусе, печатает uGUI; десктопный клавиатурный
                // путь (EditChatLine) пишет прямо в буфер GameController.
                if (!RawFocused && _input.text != buf)
                    _input.text = buf;
                if (!RawFocused && UiInput.Clicked(InputRect()))
                    _input.ActivateInputField();
            }
            else
            {
                _inputGo.SetActive(false);
                if (RawFocused)
                    _input.DeactivateInputField();
                _hint.text = "Y — написать · PgUp/PgDn — прокрутка";
            }
        }

        /// <summary>Enter в поле: та же отправка, что HandleMpInput (MP:
        /// SendChat + локальный «[me] …») и HandleChatInput (SP: Chat.Social)
        /// на desktop; Esc/закрытие — сброс буфера.</summary>
        private void SubmitChat()
        {
            string text = _input.text.Trim();
            if (_game.ChatActive)
            {
                if (text.Length > 0 && _game.MpClient != null)
                {
                    _game.MpClient.SendChat(text);
                    _game.ChatLog.Add("[me] " + text);
                }
                _game.ChatActive = false;
                _game.ChatInput = "";
            }
            if (_game.ChatInputOpen)
            {
                if (text.Length > 0)
                    GameChat.Instance.Social($"[{_game.Player.ClassName}] {text}");
                _game.ChatInputOpen = false;
                _game.ChatLogInput = "";
            }
            _input.text = "";
            _input.DeactivateInputField();
        }
    }
}
