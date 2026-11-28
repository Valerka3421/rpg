using UnityEngine;
using UnityEngine.UI;

namespace Aethelgard.UI;

/// <summary>
/// Конструктор uGUI-виджетов по теме. Все панели/кнопки/бары игры
/// создаются здесь — единый источник стиля. Виджеты — обычные GameObject'ы
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

    private static (float, float) ParentSize(Transform p)
    {
        if (_sizes.TryGetValue(p, out var s)) return (s.x, s.y);
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

    // ── Панель (тёмное дерево + бронзовая рамка) ──

    public static GameObject Panel(Transform parent, int x, int y, int w, int h,
        UITheme t, string name = "Panel")
    {
        var go = Node(name, null);
        var rt = Place(go, parent, x, y, w, h);
        Img(go, t.WindowBg);
        Frame(rt, t.Bronze, t.FrameThickness);
        var inner = Frame(rt, t.BronzeHi, 1, 3);
        inner.name = "InnerFrame";
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

    // ── Кнопка ──

    /// <summary>
    /// Кнопка: бронзовый фон, чёрный текст, при наведении фон светлеет.
    /// Цвета задаются темой — меняются из инспектора (UITheme).
    /// </summary>
    public static Button WidgetButton(Transform parent, int x, int y, int w, int h,
        string label, int fontSize, UITheme t)
    {
        var go = Node("Button_" + label, null);
        Place(go, parent, x, y, w, h);
        Img(go, t.Bronze);
        Frame((RectTransform)go.transform, t.BronzeHi, t.FrameThickness);

        var txt = Label(go.transform, 0, 0, w, h, label, fontSize,
            Color.black, t, TextAnchor.MiddleCenter);

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        // ColorBlock в Unity УМНОЖАЕТ цвет графического элемента:
        // нормальный = графический как есть (бронза),
        // hover — светлее бронзы, нажатие — темнее.
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.45f, 1.35f, 1.15f); // светлая бронза
        colors.pressedColor = new Color(0.72f, 0.68f, 0.62f);     // тёмная бронза
        colors.selectedColor = new Color(1.2f, 1.15f, 1.05f);     // чуть светлее
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.05f;
        btn.targetGraphic = go.GetComponent<Image>();
        btn.image = go.GetComponent<Image>();
        // Цвет текста при hover — чуть светлее чёрного (читаемо на бронзе)
        txt.gameObject.AddComponent<HoverTextColor>().Init(
            Color.black, new Color(0.08f, 0.06f, 0.04f), btn);
        return btn;
    }

    /// <summary>Визуально подсветить кнопку как выбранную клавиатурой.</summary>
    public static void SetKeyboardSelected(Button? btn, bool selected)
    {
        if (btn == null) return;
        var img = btn.image;
        if (img == null) return;
        // Перекрашиваем граф кнопки: выбранная — светлая бронза
        img.color = selected ? new Color(0.62f, 0.52f, 0.34f) : t_bronzeDefault;
    }

    private static Color t_bronzeDefault = new(0.376f, 0.306f, 0.18f);

    /// <summary>Запомнить базовый цвет кнопок для клавиатурной подсветки.</summary>
    public static void SetButtonBaseColor(Color c) => t_bronzeDefault = c;

    // ── Бар состояния ──

    public static (GameObject root, Image fill, Text overlay) Bar(
        Transform parent, int x, int y, int w, int h, UITheme t, Color fillColor)
    {
        var go = Node("Bar", null);
        Place(go, parent, x, y, w, h);
        Img(go, new Color(14, 11, 9));
        Frame((RectTransform)go.transform, t.Bronze, 1);

        var fillGo = Node("Fill", go.transform);
        var frt = (RectTransform)fillGo.transform;
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = new Vector2(0f, 1f);
        frt.offsetMin = new Vector2(1, 1);
        frt.offsetMax = new Vector2(1, -1);
        var fill = fillGo.AddComponent<Image>();
        fill.color = fillColor;

        var overlay = Label(go.transform, 0, 0, w, h, "", t.SmallSize, t.Ink, t,
            TextAnchor.MiddleCenter, "Overlay");
        return (go, fill, overlay);
    }

    // ── Разделитель ──

    public static void Separator(Transform parent, int x, int y, int w)
    {
        Solid(parent, x, y, w, 1, new Color(0.376f, 0.306f, 0.18f), "SepDark");
        Solid(parent, x, y + 1, w, 1, new Color(0.62f, 0.502f, 0.282f, 0.35f), "SepHi");
    }

    // ── Слот предмета ──

    public static GameObject Slot(Transform parent, int x, int y, int size,
        UITheme t, string name = "Slot")
    {
        var go = Node(name, null);
        Place(go, parent, x, y, size, size);
        Img(go, t.Inset);
        Frame((RectTransform)go.transform, t.Bronze, 1);
        return go;
    }

    // ── Затухание фона под модальными окнами ──

    public static Image Scrim(Transform parent, UITheme t, string name = "Scrim")
    {
        var go = Node(name, null);
        var rt = Attach(go, parent);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = Img(go, new Color(0, 0, 0, 0.63f));
        return img;
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
