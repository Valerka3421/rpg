using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;
using UnityEngine;
using UnityEngine.UI;
using UColor = UnityEngine.Color;
using XnaRect = Microsoft.Xna.Framework.Rectangle;

namespace Aethelgard
{
    /// <summary>
    /// Ввод UI в координатах дизайна 1920×1080 — immediate-mode хит-тесты
    /// поверх shim-мыши (порт UiKit.Hot/Clicked/EndInputFrame).
    /// GameApp один раз за кадр вызывает UiInput.Tick() перед Refresh
    /// экранов; экраны читают Hot/Clicked по тем же прямоугольникам, что
    /// используют для uGUI-визуала.
    /// </summary>
    public static class UiInput
    {
        public static Vector2 Pos;              // позиция мыши в дизайне
        public static bool LeftDown;
        public static bool PrevLeftDown;
        public static int Wheel;

        /// <summary>Срезает edge кликов; обязателен раз в кадр.</summary>
        public static void Tick()
        {
            PrevLeftDown = LeftDown;
            var ms = Mouse.GetState();
            Pos = new Vector2(ms.X, ms.Y);
            LeftDown = ms.LeftButton == ButtonState.Pressed;
            Wheel = ms.ScrollWheelValue;
        }

        public static bool Hot(XnaRect r) =>
            r.Contains((int)Pos.x, (int)Pos.y);

        /// <summary>Клик в этом кадре (edge ЛКМ).</summary>
        public static bool Clicked(XnaRect r) =>
            Hot(r) && LeftDown && !PrevLeftDown;
    }

    /// <summary>
    /// UiKitCanvas — uGUI-порт UiKit: единая палитра, панели/секции/бары/
    /// кнопки/вкладки/слоты/тултипы. Экраны строят визуал один раз в
    /// Build() (возвращённые Image/Text обновляют в Refresh()), а ховер и
    /// клики считают через UiInput по тем же прямоугольникам.
    /// Все координаты — дизайн 1920×1080.
    /// </summary>
    public sealed class UiKitCanvas
    {
        // ── Ритм сетки (как в render/UiKit.cs) ──
        public const int Pad = 16;
        public const int Gap = 10;
        public const int RowH = 26;
        public const int Slot = 56;
        public const int TabH = 34;

        // ── Палитра (значения из UiKit.Theme) ──
        public static class Theme
        {
            public static readonly UColor Bg = Uc(16, 14, 12);
            public static readonly UColor BgSoft = Uc(24, 21, 17);
            public static readonly UColor BgSlot = Uc(30, 27, 22);
            public static readonly UColor BgSlotHot = Uc(46, 40, 30);
            public static readonly UColor BgSlotSel = Uc(58, 48, 30);
            public static readonly UColor Border = Uc(84, 70, 48);
            public static readonly UColor BorderSoft = Uc(56, 47, 34);
            public static readonly UColor Gold = Uc(222, 180, 96);
            public static readonly UColor GoldBright = Uc(244, 210, 130);
            public static readonly UColor Text = Uc(224, 216, 198);
            public static readonly UColor TextDim = Uc(158, 148, 128);
            public static readonly UColor TextMuted = Uc(108, 99, 84);
            public static readonly UColor Hp = Uc(206, 76, 62);
            public static readonly UColor Mana = Uc(96, 140, 222);
            public static readonly UColor Xp = Uc(126, 188, 108);
            public static readonly UColor Danger = Uc(214, 88, 70);
            public static readonly UColor Shadow = Uc(0, 0, 0, 150);
            public static readonly UColor Dim = Uc(0, 0, 0, 170);

            private static UColor Uc(byte r, byte g, byte b, byte a = 255)
                => new(r / 255f, g / 255f, b / 255f, a / 255f);
        }

        // Размеры шрифта: desktop size 1..5 → H1..Tiny.
        public const int FontH1 = 36, FontH2 = 28, FontBody = 20, FontSmall = 16, FontTiny = 13;

        private readonly Canvas _canvas;
        private readonly Transform _root;
        private readonly Transform _tooltipLayer;

        public UiKitCanvas(string name)
        {
            var go = new GameObject(name);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            go.AddComponent<GraphicRaycaster>();
            go.AddComponent<CanvasGroup>().blocksRaycasts = false; // клики ловит UiInput

            _root = go.transform;
            _tooltipLayer = new GameObject("Tooltips").transform;
            _tooltipLayer.SetParent(_root, false);

            BuildTooltip();
            SetActive(false);
        }

        public GameObject GameObject => _canvas.gameObject;
        public Transform Root => _root;

        public void SetActive(bool on) => _canvas.gameObject.SetActive(on);

        // ── Примитивы ──

        /// <summary>Прямоугольник-картинка в абсолютных координатах дизайна
        /// (низ экрана = y, как на desktop).</summary>
        public Image RectImage(Rect r, UColor color)
        {
            var img = new GameObject("rect").AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.SetParent(_root, false);
            Place(rt, r);
            return img;
        }

        /// <summary>Текст (fontSize из уровня 1..5).</summary>
        public Text Label(Rect r, string s, int size = 3, UColor? color = null,
            TextAnchor align = TextAnchor.UpperLeft, bool wrap = true)
        {
            var t = new GameObject("text").AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = FontSize(size);
            t.color = color ?? Theme.Text;
            t.alignment = align;
            t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = s;
            var rt = t.rectTransform;
            rt.SetParent(_root, false);
            Place(rt, r);
            return t;
        }

        public Text Centered(Rect r, string s, int size = 3, UColor? color = null)
            => Label(r, s, size, color, TextAnchor.MiddleCenter, wrap: false);

        private static int FontSize(int level) => level switch
        {
            1 => FontH1, 2 => FontH2, 3 => FontBody, 4 => FontSmall, _ => FontTiny,
        };

        private static void Place(RectTransform rt, Rect r)
        {
            // Дизайн-координаты: desktop рисует от верхнего левого угла.
            // uGUI: опорная точка — низ экрана; конвертируем y.
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(r.x, AethelgardGame.ScreenH - r.yMax);
            rt.sizeDelta = new Vector2(r.width, r.height);
        }

        /// <summary>Панель: затемнение мира + фон + рамка. Возвращает фон.</summary>
        public Image Panel(Rect r, bool dimWorldBehind = true)
        {
            if (dimWorldBehind)
                RectImage(new Rect(0, 0, AethelgardGame.ScreenW, AethelgardGame.ScreenH), Theme.Dim);
            RectImage(Offset(r, 4, 4), Theme.Shadow);
            var bg = RectImage(r, Theme.Bg);
            Frame(r, Theme.Border, thick: true);
            RectImage(new Rect(r.x + 2, r.y + 2, r.width - 4, 1), Alpha(Theme.Gold, 90));
            return bg;
        }

        /// <summary>Секция: подложка + тонкая рамка + заголовок.</summary>
        public Image Section(Rect r, string? title = null, int titleSize = 4)
        {
            var bg = RectImage(r, Theme.BgSoft);
            Frame(r, Theme.BorderSoft);
            if (title != null)
                Label(new Rect(r.x + 8, r.y + 5, r.width - 16, 20), title, titleSize, Theme.Gold);
            return bg;
        }

        /// <summary>Полоса прогресса; обновление — через SetBar.</summary>
        public (Image bg, Image fill) Bar(Rect r, UColor color)
        {
            var bg = RectImage(r, Alpha(new UColor(0, 0, 0), 140));
            var inner = new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2);
            var innerBg = RectImage(inner, Uc(30, 26, 20));
            var fillGo = new GameObject("fill").AddComponent<Image>();
            fillGo.color = color;
            fillGo.raycastTarget = false;
            var frt = fillGo.rectTransform;
            frt.SetParent(innerBg.transform, false);
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = new Vector2(0f, 1f);
            frt.pivot = new Vector2(0f, 0.5f);
            frt.anchoredPosition = Vector2.zero;
            frt.sizeDelta = Vector2.zero;
            RectImage(r, Alpha(new UColor(1f, 1f, 1f), 26)).rectTransform
                .SetSiblingIndex(0);
            return (bg, fillGo);
        }

        /// <summary>Обновить заполнение бара (доля 0..1).</summary>
        public static void SetBar(Image fill, double frac)
        {
            frac = System.Math.Clamp(frac, 0, 1);
            float w = ((RectTransform)fill.transform.parent).sizeDelta.x;
            fill.rectTransform.sizeDelta = new Vector2(w * (float)frac, 0f);
        }

        /// <summary>Кнопка: bg-картинка для анимации ховера; клик — UiInput.Clicked(rect).</summary>
        public Image Button(Rect r, string label, int size = 3)
        {
            RectImage(Offset(r, 2, 2), Theme.ShadowCol);
            var bg = RectImage(r, Theme.BgSlot);
            Frame(r, Theme.BorderSoft);
            Centered(r, label, size, Theme.TextDim);
            return bg;
        }

        /// <summary>Слот предмета: возвращает фон для анимации ховера/выбора.</summary>
        public Image Slot(Rect r)
        {
            RectImage(Offset(r, 2, 2), Theme.Shadow);
            var bg = RectImage(r, Theme.BgSlot);
            Frame(r, Theme.BorderSoft);
            return bg;
        }

        /// <summary>Вкладка; клик — UiInput.Clicked(rect).</summary>
        public Image Tab(Rect r, string label, bool selected)
        {
            var bg = RectImage(r, selected ? Uc(64, 52, 28) : Theme.BgSoft);
            Frame(r, selected ? Theme.Gold : Theme.BorderSoft);
            if (selected) RectImage(new Rect(r.x, r.yMax - 3, r.width, 3), Theme.Gold);
            Centered(r, label, 4, selected ? Theme.GoldBright : Theme.TextDim);
            return bg;
        }

        // ── Тултип (единственный, поверх всего; обновляется ShowTooltip) ──

        private GameObject _tooltipGo = null!;
        private Text _tooltipTitle = null!, _tooltipBody = null!;

        private void BuildTooltip()
        {
            _tooltipGo = new GameObject("tooltip");
            _tooltipGo.transform.SetParent(_tooltipLayer, false);
            var rt = _tooltipGo.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(260, 120);

            var bgGo = new GameObject("bg").AddComponent<Image>();
            bgGo.color = Theme.Bg;
            bgGo.raycastTarget = false;
            bgGo.rectTransform.SetParent(_tooltipGo.transform, false);
            bgGo.rectTransform.anchorMin = Vector2.zero;
            bgGo.rectTransform.anchorMax = Vector2.one;
            bgGo.rectTransform.sizeDelta = Vector2.zero;
            bgGo.rectTransform.anchoredPosition = Vector2.zero;

            _tooltipTitle = new GameObject("title").AddComponent<Text>();
            _tooltipTitle.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _tooltipTitle.fontSize = FontSmall;
            _tooltipTitle.color = Theme.GoldBright;
            _tooltipTitle.alignment = TextAnchor.UpperLeft;
            _tooltipTitle.raycastTarget = false;
            var trt = _tooltipTitle.rectTransform;
            trt.SetParent(_tooltipGo.transform, false);
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -6f);
            trt.sizeDelta = new Vector2(-20f, 24f);

            _tooltipBody = new GameObject("body").AddComponent<Text>();
            _tooltipBody.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _tooltipBody.fontSize = FontSmall;
            _tooltipBody.color = Theme.Text;
            _tooltipBody.alignment = TextAnchor.UpperLeft;
            _tooltipBody.raycastTarget = false;
            var brt = _tooltipBody.rectTransform;
            brt.SetParent(_tooltipGo.transform, false);
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(10f, 8f);
            brt.offsetMax = new Vector2(-10f, -32f);

            _tooltipGo.SetActive(false);
        }

        /// <summary>Показать тултип у якоря (правее, с фолбэком влево/вверх).</summary>
        public void ShowTooltip(Rect anchor, string title, IEnumerable<string> lines)
        {
            var body = new System.Text.StringBuilder();
            foreach (var l in lines) body.AppendLine(l);
            _tooltipTitle.text = title;
            _tooltipBody.text = body.ToString().TrimEnd();

            // Оценка высоты по числу переносимых строк (примерно 2 строки
            // на 22 символа при FontSmall).
            int estLines = 0;
            foreach (var l in lines)
            {
                int chars = string.IsNullOrEmpty(l) ? 1 : l.Length;
                estLines += System.Math.Max(1, Mathf.CeilToInt(chars / 26f));
            }
            float h = Mathf.Max(70f, 40f + estLines * 20f);
            float w = 300f;

            float x = anchor.xMax + 8f;
            float y = anchor.y;
            if (x + w > AethelgardGame.ScreenW - 8) x = anchor.x - w - 8;
            if (x < 8) x = 8;
            if (y + h > AethelgardGame.ScreenH - 8) y = AethelgardGame.ScreenH - h - 8;

            var rt = _tooltipGo.GetComponent<RectTransform>();
            Place(rt, new Rect(x, y, w, h));
            _tooltipGo.transform.SetAsLastSibling();
            _tooltipGo.SetActive(true);
        }

        public void HideTooltip() => _tooltipGo.SetActive(false);

        // ── Хелперы ──

        private static readonly UColor ShadowCol = new(0f, 0f, 0f, 150f / 255f);
        private static readonly UColor Shadow = new(0f, 0f, 0f, 150f / 255f);

        private static Rect Offset(Rect r, int dx, int dy) => new(r.x + dx, r.y + dy, r.width, r.height);

        private static UColor Uc(byte r, byte g, byte b, byte a = 255)
            => new(r / 255f, g / 255f, b / 255f, a / 255f);

        public static UColor Alpha(UColor c, byte a) => new(c.r, c.g, c.b, a / 255f);

        private void Frame(Rect r, UColor c, bool thick = false)
        {
            int t = thick ? 2 : 1;
            RectImage(new Rect(r.x, r.y, r.width, t), c);
            RectImage(new Rect(r.x, r.yMax - t, r.width, t), c);
            RectImage(new Rect(r.x, r.y, t, r.height), c);
            RectImage(new Rect(r.xMax - t, r.y, t, r.height), c);
        }
    }
}
