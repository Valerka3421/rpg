using UnityEngine;
using UnityEngine.UI;

namespace Aethelgard.UI;

/// <summary>
/// Конструктор uGUI-виджетов по теме. Все панели/кнопки/бары игры
/// создаются здесь — единый источник стиля, ВСЕ цвета из UITheme
/// (точная копия палитры python-final). Виджеты — обычные GameObject'ы
/// с Image/Text: их можно править в инспекторе Unity в Play Mode
/// и генерировать в префабы (Editor → Aethelgard → Generate UI Prefabs).
/// </summary>
public static class UiKit
{
    private static Font? _font;

    public static Font GetFont(UITheme t)
    {
        if (_font == null)
            _font = UnityEngine.Font.CreateDynamicFontFromOSFont(t.FontName, 20);
        return _font;
    }

    // ── Процедурные спрайты (кэш) ──

    private static readonly Dictionary<string, Sprite> _sprites = new();

    /// <summary>
    /// Вертикальный градиент для кнопок (base_renderer.py): белый верх,
    /// затемнение к низу — итоговые цвета задаются Image.color (верхним
    /// цветом градиента), низ автоматически темнее (24/44 ≈ 0.545).
    /// </summary>
    public static Sprite BtnGradient()
    {
        if (_sprites.TryGetValue("btnGrad", out var s)) return s;
        var tex = new Texture2D(4, 64, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < 64; y++)
        {
            float k = 1f - y / 63f; // верх = 1
            byte v = (byte)Mathf.RoundToInt(255f * (0.545f + 0.455f * k));
            for (int x = 0; x < 4; x++)
                tex.SetPixel(x, y, new Color32(v, v, v, 255));
        }
        tex.Apply();
        s = Sprite.Create(tex, new Rect(0, 0, 4, 64), Vector2.one * 0.5f, 100,
            0, SpriteMeshType.FullRect, Vector4.zero);
        _sprites["btnGrad"] = s;
        return s;
    }

    /// <summary>
    /// Круглый радиальный градиент (тьма с «дырой», ореолы факелов):
    /// альфа = a·(r/half)^pow от края к центру.
    /// </summary>
    public static Sprite RadialGradient(int size, float pow)
    {
        string key = $"radial{size}_{pow}";
        if (_sprites.TryGetValue(key, out var s)) return s;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float half = size / 2f;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(Mathf.Pow(Mathf.Clamp01(r), pow));
                px[y * size + x] = new Color32(255, 255, 255,
                    (byte)Mathf.RoundToInt(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        s = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100,
            0, SpriteMeshType.FullRect, Vector4.zero);
        _sprites[key] = s;
        return s;
    }

    /// <summary>Гексагон (pointy-top) с рамкой — узел дерева навыков.</summary>
    public static Sprite Hex(float radius, int border, Color fill, Color borderColor,
        Color? outer = null)
    {
        string key = $"hex{radius}_{border}_{(int)(fill.r*255)}{(int)(fill.g*255)}" +
            $"{(int)(fill.b*255)}_{(int)(borderColor.r*255)}{(int)(borderColor.g*255)}" +
            $"{(int)(borderColor.b*255)}_{outer != null}";
        if (_sprites.TryGetValue(key, out var s)) return s;
        int pad = 6;
        int size = Mathf.CeilToInt(radius * 2) + pad * 2;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float cx = size / 2f, cy = size / 2f;
        var px = new Color32[size * size];
        var corners = new (float, float)[6];
        for (int i = 0; i < 6; i++)
        {
            float ang = Mathf.PI / 180f * (60f * i + 90f);
            corners[i] = (Mathf.Cos(ang), Mathf.Sin(ang));
        }
        float Inside(float x, float y, float rr)
        {
            // полуплоскости по 6 рёбрам
            float min = 1f;
            for (int i = 0; i < 6; i++)
            {
                var (x1, y1) = corners[i];
                var (x2, y2) = corners[(i + 1) % 6];
                float ex = x2 - x1, ey = y2 - y1;
                float len = Mathf.Sqrt(ex * ex + ey * ey);
                float d = ((x - cx - x1 * rr) * ey - (y - cy - y1 * rr) * ex) / len;
                min = Mathf.Min(min, d / 2f + 0.5f); // 0.5 на ребре, внутри >0.5
            }
            return min;
        }
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f - cx, fy = y + 0.5f - cy;
                float dist = Mathf.Sqrt(fx * fx + fy * fy);
                Color32 c;
                if (outer != null && dist <= radius + pad - 2 &&
                    Inside(x, y, radius + pad - 2) > 0.5f)
                    c = outer.Value;
                else if (dist <= radius + border && Inside(x, y, radius + border) > 0.5f)
                    c = borderColor;
                else if (dist <= radius && Inside(x, y, radius) > 0.5f)
                    c = fill;
                else
                    c = new Color32(0, 0, 0, 0);
                px[y * size + x] = c;
            }
        tex.SetPixels32(px);
        tex.Apply();
        s = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100,
            0, SpriteMeshType.FullRect, Vector4.zero);
        _sprites[key] = s;
        return s;
    }

    // ── Базовые элементы ──

    public static GameObject Node(string name, Transform parent) =>
        new(name, typeof(RectTransform));

    public static RectTransform Attach(GameObject go, Transform parent)
    {
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        // По умолчанию контейнер растягивается на весь родитель:
        // дефолтный rect 100×100 ломал центрирование панелей.
        // (Place() переопределяет якоря для обычных детей.)
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    public static RectTransform Rect(GameObject go, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var rt = Attach(go, parent);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return rt;
    }

    /// <summary>
    /// Позиционировать по прямоугольнику в координатах РОДИТЕЛЯ
    /// (x,y — левый верхний угол, отсчёт сверху). Для корня канваса
    /// это дизайн-координаты 1920×1080.
    /// </summary>
    public static RectTransform Place(GameObject go, Transform parent,
        int x, int y, int w, int h)
    {
        var rt = Attach(go, parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        var (pw, ph) = ParentSize(parent);
        rt.anchoredPosition = new Vector2(
            x + w / 2f - pw / 2f, ph / 2f - (y + h / 2f));
        rt.sizeDelta = new Vector2(w, h);
        _sizes[rt] = new Vector2(w, h);
        return rt;
    }

    // Размеры, заданные через Place (rect может быть ещё не посчитан
    // на момент построения UI в Awake, поэтому храним сами).
    private static readonly Dictionary<Transform, Vector2> _sizes = new();

    /// <summary>
    /// Явно задать размер контейнера для расчёта Place() (нужно в
    /// edit-mode: канвас там может быть 0×0, а не 1920×1080).
    /// </summary>
    public static void RegisterSize(Transform t, Vector2 size) => _sizes[t] = size;

    private static (float, float) ParentSize(Transform p)
    {
        // 1) размер, заданный через Place/RegisterSize;
        // 2) ближайший зарегистрированный предок (edit-mode канвас);
        // 3) фактический rect; 4) дизайн 1920×1080.
        for (var t = p; t != null; t = t.parent)
            if (_sizes.TryGetValue(t, out var s)) return (s.x, s.y);
        if (p is RectTransform pr && pr.rect.width > 0)
            return (pr.rect.width, pr.rect.height);
        return (1920, 1080); // корень канваса (дизайн-разрешение)
    }

    public static Image Img(GameObject go, Color c)
    {
        var img = go.AddComponent<Image>();
        img.color = c;
        img.type = Image.Type.Sliced;
        return img;
    }

    public static Image Solid(Transform parent, int x, int y, int w, int h, Color c, string name = "solid")
    {
        var go = Node(name, null);
        Place(go, parent, x, y, w, h);
        return Img(go, c);
    }

    // ── Панель (render/theme.py Theme.panel: PANEL + 1px PANEL_BORDER +
    //    drop shadow (5,4,3) со смещением 3) ──

    public static GameObject Panel(Transform parent, int x, int y, int w, int h,
        UITheme t, string name = "Panel")
    {
        var go = Node(name, null);
        var rt = Place(go, parent, x, y, w, h);
        if (t.PanelShadow)
        {
            var shadow = Node("Shadow", rt);
            var srt = (RectTransform)shadow.transform;
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = new Vector2(-3, -3);
            srt.offsetMax = new Vector2(3, 3);
            var si = Img(shadow, t.BtnShadow);
            si.raycastTarget = false;
            shadow.transform.SetAsFirstSibling();
        }
        Img(go, t.Panel);
        Frame(rt, t.PanelBorder, 1);
        return go;
    }

    /// <summary>Бронзовая рамка (4 полосы) вокруг RectTransform.</summary>
    public static GameObject Frame(RectTransform owner, Color c, int thickness,
        int inset = 0)
    {
        var frame = Node("Frame", owner);
        var frt = (RectTransform)frame.transform;
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.offsetMin = new Vector2(inset, inset);
        frt.offsetMax = new Vector2(-inset, -inset);
        foreach (var (nm, amin, amax, omin, omax) in new[]
        {
            ("top", Vector2.zero, new Vector2(1, 0), new Vector2(0, -thickness), new Vector2(0, 0)),
            ("bottom", Vector2.zero, new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, thickness)),
            ("left", Vector2.zero, new Vector2(0, 1), new Vector2(0, thickness), new Vector2(thickness, -thickness)),
            ("right", new Vector2(1, 0), new Vector2(1, 1), new Vector2(-thickness, thickness), Vector2.zero),
        })
        {
            var bar = Node(nm, frame.transform);
            var brt = (RectTransform)bar.transform;
            brt.anchorMin = amin; brt.anchorMax = amax;
            brt.offsetMin = omin; brt.offsetMax = omax;
            Img(bar, c);
        }
        return frame;
    }

    // ── Текст ──

    public static Text Label(Transform parent, int x, int y, int w, int h,
        string s, int size, Color c, UITheme t,
        TextAnchor align = TextAnchor.UpperLeft, string name = "Text")
    {
        var go = Node(name, null);
        Place(go, parent, x, y, w, h);
        var txt = go.AddComponent<Text>();
        txt.font = GetFont(t);
        txt.fontSize = size;
        txt.color = c;
        txt.alignment = align;
        txt.text = s;
        txt.supportRichText = true;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        return txt;
    }

    /// <summary>Заголовок-секция с линиями по бокам (Theme.section).</summary>
    public static void Section(Transform parent, int x, int y, int w, string s,
        int size, UITheme t)
    {
        var txt = Label(parent, x, y, w, size + 8, s, size, t.Section, t,
            TextAnchor.MiddleCenter, "Section");
        txt.gameObject.AddComponent<Shadow>();
        int lineW = w / 2 - s.Length * size / 2 - 16;
        if (lineW > 10)
        {
            Solid(parent, x, y + (size + 8) / 2, lineW, 1, t.PanelBorder, "SecLineL");
            Solid(parent, x + w - lineW, y + (size + 8) / 2, lineW, 1, t.PanelBorder, "SecLineR");
        }
    }

    /// <summary>Клавиша-кейкап (Theme.keycap): KEY_BG / KEY_BORDER / KEY_TEXT.</summary>
    public static GameObject Keycap(Transform parent, int x, int y, int w, int h,
        string s, int size, UITheme t)
    {
        var go = Node("Key_" + s, null);
        Place(go, parent, x, y, w, h);
        Img(go, t.KeyBg);
        Frame((RectTransform)go.transform, t.KeyBorder, 1);
        Label(go.transform, 0, 0, w, h, s, size, t.KeyText, t, TextAnchor.MiddleCenter);
        return go;
    }

    /// <summary>Бейдж (Theme.badge): тёмно-золотой фон, золотой текст.</summary>
    public static GameObject Badge(Transform parent, int x, int y, int w, int h,
        string s, int size, UITheme t)
    {
        var go = Node("Badge_" + s, null);
        Place(go, parent, x, y, w, h);
        Img(go, UITheme.Shade(t.Gold, 0.24f));
        Label(go.transform, 0, 0, w, h, s, size, t.Gold, t, TextAnchor.MiddleCenter);
        return go;
    }

    /// <summary>Разделитель (Theme.divider) — тёмная + светлая линии.</summary>
    public static void Separator(Transform parent, int x, int y, int w, UITheme t)
    {
        Solid(parent, x, y, w, 1, t.PanelBorder, "SepDark");
        Solid(parent, x, y + 1, w, 1, UITheme.Shade(t.Gold, 0.55f), "SepHi");
    }

    // ── Кнопка (base_renderer.py, 1:1) ──

    /// <summary>
    /// Кнопка-градиент python-final: вертикальный градиент (44,39,31)→(24,21,17),
    /// рамка (72,62,46); выделенная — градиент (96,76,44)→(56,44,25), золотая
    /// рамка (206,166,82), верхняя светлая линия (236,206,142) и стрелка слева.
    /// </summary>
    public static Button WidgetButton(Transform parent, int x, int y, int w, int h,
        string label, int fontSize, UITheme t, bool menuArrow = false)
    {
        var go = Node("Button_" + label, null);
        Place(go, parent, x, y, w, h);
        var grad = go.AddComponent<Image>();
        grad.sprite = BtnGradient();
        grad.type = Image.Type.Simple;
        grad.color = t.BtnNormalTop; // низ градиента автоматически темнее

        var frame = Frame((RectTransform)go.transform, t.BtnNormalBorder, 2);
        var hi = Solid(go.transform, 2, 2, w - 4, 1, new Color(0, 0, 0, 0), "HiLine");
        hi.raycastTarget = false;
        // Стрелка-треугольник слева (как в base_renderer.py), скрыта по умолчанию
        var arrowTxt = Label(go.transform, -22, 0, 16, h, "▶", fontSize,
            t.BtnSelBorder, t, TextAnchor.MiddleCenter, "Arrow");
        arrowTxt.gameObject.SetActive(false);
        arrowTxt.raycastTarget = false;
        var arrow = arrowTxt.rectTransform;

        var txt = Label(go.transform, 0, 0, w, h, label, fontSize,
            t.BtnNormalText, t, TextAnchor.MiddleCenter);
        txt.gameObject.AddComponent<Shadow>();

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.05f;
        btn.targetGraphic = grad;
        btn.image = grad;

        // Скин кнопки: переключение градиент/рамка/стрелка при выборе
        var skin = go.AddComponent<MenuButtonSkin>();
        skin.Init(grad, frame.transform, hi.rectTransform, arrow,
            txt, t, menuArrow, null);
        return btn;
    }

    /// <summary>Визуально подсветить кнопку как выбранную клавиатурой.</summary>
    public static void SetKeyboardSelected(Button? btn, bool selected)
    {
        if (btn == null) return;
        var skin = btn.GetComponent<MenuButtonSkin>();
        if (skin != null) skin.SetSelected(selected);
    }

    // ── Кнопка закрытия модального окна (_base.py) ──

    public static Button CloseButton(Transform parent, int right, int top,
        System.Action onClick, UITheme t)
    {
        var go = Node("CloseButton", null);
        Place(go, parent, right - 34, top, 34, 34);
        Img(go, t.CloseBtnFill);
        Frame((RectTransform)go.transform, t.CloseBtnBorder, 1);
        var txt = Label(go.transform, 0, 0, 34, 34, "✕", t.BodySize, t.CloseBtnX, t,
            TextAnchor.MiddleCenter);
        txt.fontStyle = FontStyle.Bold;
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(new UnityEngine.Events.UnityAction(onClick));
        btn.targetGraphic = go.GetComponent<Image>();
        btn.image = go.GetComponent<Image>();
        return btn;
    }

    // ── Бар состояния (Theme.bar) ──

    public static (GameObject root, Image fill, Text overlay) Bar(
        Transform parent, int x, int y, int w, int h, UITheme t, Color fillColor)
    {
        var go = Node("Bar", null);
        Place(go, parent, x, y, w, h);
        Img(go, t.PanelDark);
        Frame((RectTransform)go.transform, t.PanelBorder, 1);

        var fillGo = Node("Fill", go.transform);
        var frt = (RectTransform)fillGo.transform;
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = new Vector2(0f, 1f);
        frt.offsetMin = new Vector2(1, 1);
        frt.offsetMax = new Vector2(1, -1);
        var fill = fillGo.AddComponent<Image>();
        fill.color = fillColor;

        var overlay = Label(go.transform, 0, 0, w, h, "", t.SmallSize, t.Text, t,
            TextAnchor.MiddleCenter, "Overlay");
        return (go, fill, overlay);
    }

    // ── Слот предмета (_base.py: fill (32,28,22), border (78,66,48)) ──

    public static GameObject Slot(Transform parent, int x, int y, int size,
        UITheme t, string name = "Slot")
    {
        var go = Node(name, null);
        Place(go, parent, x, y, size, size);
        Img(go, t.SlotFill);
        Frame((RectTransform)go.transform, t.PanelBorder, 1);
        return go;
    }

    // ── Затухание фона под модальными окнами (_base.py: (6,5,4,165)) ──

    public static Image Scrim(Transform parent, UITheme t, string name = "Scrim")
    {
        var go = Node(name, null);
        var rt = Attach(go, parent);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = Img(go, t.ModalDim);
        return img;
    }
}

/// <summary>
/// Скин кнопки python-final: переключение градиента, рамки, верхней линии
/// и стрелки между обычным и выделенным состоянием.
/// </summary>
public class MenuButtonSkin : MonoBehaviour
{
    private Image _grad = null!;
    private Transform _frame = null!;
    private RectTransform _hiLine = null!;
    private RectTransform _arrow = null!;
    private Text _txt = null!;
    private UITheme _t = null!;
    private bool _menuArrow;
    private System.Action<Button>? _onClick;
    private Vector2 _hiSize;

    public void Init(Image grad, Transform frame, RectTransform hiLine,
        RectTransform arrow, Text txt, UITheme t, bool menuArrow,
        System.Action<Button>? onClick)
    {
        _grad = grad; _frame = frame; _hiLine = hiLine; _arrow = arrow;
        _txt = txt; _t = t; _menuArrow = menuArrow; _onClick = onClick;
        _hiSize = hiLine != null ? hiLine.sizeDelta : Vector2.zero;
        SetSelected(false);
    }

    public void SetSelected(bool sel)
    {
        if (_grad == null) return;
        _grad.color = sel ? _t.BtnSelTop : _t.BtnNormalTop;
        bool gold = sel;
        for (int i = 0; i < _frame.childCount; i++)
        {
            var img = _frame.GetChild(i).GetComponent<Image>();
            if (img != null) img.color = gold ? _t.BtnSelBorder : _t.BtnNormalBorder;
        }
        if (_hiLine != null)
        {
            var hi = _hiLine.GetComponent<Image>();
            hi.color = sel ? _t.BtnSelHiLine : Color.clear;
            _hiLine.sizeDelta = new Vector2(
                sel && _menuArrow ? _hiSize.x - 8 : _hiSize.x, _hiSize.y);
        }
        if (_arrow != null)
        {
            var arrowText = _arrow.GetComponent<Text>();
            if (arrowText != null) arrowText.color = _t.BtnSelBorder;
            _arrow.gameObject.SetActive(sel && _menuArrow);
        }
        if (_txt != null)
            _txt.color = sel ? _t.BtnSelText : _t.BtnNormalText;
    }
}

/// <summary>Меняет цвет текста при наведении (в паре с WidgetButton).</summary>
public class HoverTextColor : MonoBehaviour
{
    private Text _txt = null!;
    private Color _normal, _hover;
    private Button _btn = null!;

    public void Init(Color normal, Color hover, Button btn)
    {
        _txt = GetComponent<Text>();
        _normal = normal;
        _hover = hover;
        _btn = btn;
    }

    private void Update()
    {
        if (_txt == null || _btn == null) return;
        _txt.color = _btn.interactable
            ? (_btn.IsInteractedWithHover() ? _hover : _normal)
            : _normal * 0.6f;
    }
}

public static class ButtonHoverExt
{
    public static bool IsInteractedWithHover(this Button b) =>
        b.gameObject.GetComponent<UnityEngine.UI.Image>() != null &&
        RectTransformUtility.RectangleContainsScreenPoint(
            b.GetComponent<RectTransform>(), Input.mousePosition,
            Camera.main);
}
