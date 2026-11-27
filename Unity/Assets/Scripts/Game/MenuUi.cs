using Aethelgard.Systems;
using UnityEngine;
using UnityEngine.UI;
using UColor = UnityEngine.Color;
using Screens = Aethelgard.Systems.ScreenMode;

namespace Aethelgard
{
    /// <summary>
    /// Главное меню и выбор класса на uGUI (MVP: минимальный набор пунктов —
    /// как MainMenuScreen в MonoGame, но без сейвов/настроек/MP; они
    /// подключаются в следующих раундах).
    /// </summary>
    public class MenuUi : MonoBehaviour
    {
        private readonly string[] _classes = { "warrior", "mage", "rogue", "artisan" };
        private static readonly string[] ClassNames =
            { "Воин", "Маг", "Разбойник", "Ремесленник" };

        private GameController _game = null!;
        private Canvas _canvas = null!;
        private GameObject _main = null!, _classSel = null!;
        private Text _hint = null!;

        /// <summary>Собственно подтверждение выбора класса.</summary>
        public System.Action<string>? OnClassPicked;

        public static MenuUi Create(GameController game)
        {
            var go = new GameObject("MenuUi");
            var ui = go.AddComponent<MenuUi>();
            ui._game = game;
            ui.Build();
            return ui;
        }

        private void Build()
        {
            _canvas = new GameObject("MenuCanvas").AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = _canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            _canvas.gameObject.AddComponent<GraphicRaycaster>();

            // затемнение фоном — мир за меню ещё не создан
            var bg = NewImage(_canvas.transform, "bg", new UColor(0.07f, 0.06f, 0.05f));
            Stretch(bg);

            _main = Panel("Этельгард", new[] { "Новая игра", "Загрузить (скоро)", "Мультиплеер (скоро)", "Выход" });
            _classSel = Panel("Выбор класса", ClassNames);

            _hint = NewText(_canvas.transform, "hint", "", 20, new UColor(1f, 0.9f, 0.7f));
            var hrt = _hint.rectTransform;
            hrt.anchorMin = hrt.anchorMax = hrt.pivot = new Vector2(0.5f, 0f);
            hrt.anchoredPosition = new Vector2(0f, 30f);
            hrt.sizeDelta = new Vector2(1200, 30);

            Hide();
        }

        private GameObject Panel(string title, string[] items)
        {
            var panel = new GameObject("panel_" + title);
            panel.transform.SetParent(_canvas.transform, false);
            var rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(560, 130 + items.Length * 92);

            NewImage(panel.transform, "bg", new UColor(0f, 0f, 0f, 0.7f)).Stretch();

            var t = NewText(panel.transform, "title", title, 42, new UColor(1f, 0.85f, 0.4f));
            var trt = t.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -30f);
            trt.sizeDelta = new Vector2(0f, 60);

            for (int i = 0; i < items.Length; i++)
            {
                int idx = i;
                var brt = NewTextButton(panel.transform, "btn" + i, items[i], 30, () => OnItem(title, idx));
                brt.anchorMin = new Vector2(0f, 1f);
                brt.anchorMax = new Vector2(1f, 1f);
                brt.pivot = new Vector2(0.5f, 1f);
                brt.anchoredPosition = new Vector2(0f, -110f - i * 92f);
                brt.sizeDelta = new Vector2(-80f, 72f);
            }
            return panel;
        }

        private void OnItem(string title, int idx)
        {
            if (title == "Этельгард")
            {
                switch (idx)
                {
                    case 0: // Новая игра
                        _classSel.SetActive(true);
                        _main.SetActive(false);
                        break;
                    case 3: Application.Quit(); break;
                }
            }
            else // выбор класса
            {
                if (idx >= 0 && idx < _classes.Length)
                {
                    _classSel.SetActive(false);
                    _main.SetActive(false);
                    OnClassPicked?.Invoke(_classes[idx]);
                }
            }
        }

        /// <summary>Enter: подтвердить текущий экран (0-й пункт).</summary>
        public void Confirm()
        {
            if (_main.activeSelf) OnItem("Этельгард", 0);
            else if (_classSel.activeSelf) OnItem("Выбор класса", 0);
        }

        public void ShowMain()
        {
            _canvas.gameObject.SetActive(true);
            _main.SetActive(true);
            _classSel.SetActive(false);
            _hint.text = "Стрелки/мышь — выбор · Enter/клик — подтвердить";
        }

        public void ShowClassSelect()
        {
            _canvas.gameObject.SetActive(true);
            _main.SetActive(false);
            _classSel.SetActive(true);
            _hint.text = "Клик по классу — начать новую игру · Esc — назад";
        }

        public void Hide()
        {
            _canvas.gameObject.SetActive(false);
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        private RectTransform NewImage(Transform parent, string name, UColor color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img.rectTransform;
        }

        private Text NewText(Transform parent, string name, string text, int size, UColor color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.text = text;
            return t;
        }

        private RectTransform NewTextButton(Transform parent, string name, string text, int size, System.Action onClick)
        {
            var rt = NewImage(parent, name, new UColor(0.12f, 0.1f, 0.08f));
            var t = NewText(rt, "label", text, size, new UColor(0.95f, 0.9f, 0.8f));
            t.rectTransform.Stretch();
            var btn = rt.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new UColor(0.35f, 0.3f, 0.2f);
            colors.pressedColor = new UColor(0.45f, 0.38f, 0.22f);
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick());
            return rt;
        }
    }

    internal static class RectTransformExt
    {
        public static void Stretch(this RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }
    }
}
