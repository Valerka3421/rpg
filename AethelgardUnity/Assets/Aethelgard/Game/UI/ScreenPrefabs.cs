using UnityEngine;
using UnityEngine.UI;

namespace Aethelgard.UI;

/// <summary>
/// Слой редактируемости UI: раскладка каждого экрана живёт в префабе
/// «Assets/Aethelgard/Resources/Screens/<Имя>.prefab» (генерируется меню
/// Aethelgard → Generate Screen Prefabs, правится в Unity Editor).
///
/// После того как экран построил свою разметку кодом, вызов
/// ScreenPrefabs.Apply(builtRoot, "ИмяЭкрана") накладывает префаб поверх:
///   - позиции/размеры/якоря всех совпадающих по имени узлов берутся из префаба;
///   - если узел в префабе переименован с префиксом «!» (например
///     «!Btn_NovaIgra»), из префаба берётся ещё и текст, размер шрифта,
///     цвет и выравнивание — так правится содержание кнопок и подписей;
///   - совпадающие по имени узлы: иерархия должна совпадать с построенной
///     кодом (Panel → Slot0, Panel → Btn_NewGame …).
///
/// Динамические значения (HP, золото, содержимое ячеек) код перезаписывает
/// каждый кадр — их в префабе редактировать бессмысленно; для статических
/// подписей используйте «!».
/// </summary>
public static class ScreenPrefabs
{
    /// <summary>Наложить префаб экрана на построенное дерево.</summary>
    public static void Apply(GameObject? built, string screenName)
    {
        if (built == null) return;
        var prefab = Load(screenName);
        if (prefab == null) return;

        // ВАЖНО: прямоугольник корня НЕ переносим — он зависит от канваса,
        // в котором префаб генерировался (edit-mode ≠ Play). Позицию/размер
        // самой панели задаёт код; из префаба берутся только дети.
        ApplyChildren(built.transform, prefab.transform);
    }

    private static GameObject? Load(string screenName) =>
        Resources.Load<GameObject>($"Screens/{screenName}");

    private static readonly Dictionary<string, bool> _overrideCache = new();

    /// <summary>
    /// Переопределён ли текст узла в префабе (узел переименован с «!»).
    /// Экраны с покадровой перезаписью текста используют это, чтобы не
    /// затирать правки из редактора.
    /// </summary>
    public static bool OverridesText(string screen, string nodeName)
    {
        string key = screen + "/" + nodeName;
        if (_overrideCache.TryGetValue(key, out var cached)) return cached;
        bool result = false;
        var prefab = Load(screen);
        if (prefab != null)
            foreach (Transform c in prefab.transform)
                if (c.name == "!" + nodeName) { result = true; break; }
        _overrideCache[key] = result;
        return result;
    }

    private static void ApplyChildren(Transform built, Transform prefab)
    {
        foreach (Transform pf in prefab)
        {
            string pname = pf.name;
            bool overrideContent = pname.StartsWith("!");
            string clean = overrideContent ? pname[1..] : pname;

            Transform b = built.Find(clean);
            if (b == null) continue;

            CopyRect((RectTransform)pf, (RectTransform)b);
            if (overrideContent) CopyContent(pf, b);
            ApplyChildren(b, pf);
        }
    }

    private static void CopyRect(RectTransform from, RectTransform to)
    {
        to.anchorMin = from.anchorMin;
        to.anchorMax = from.anchorMax;
        to.pivot = from.pivot;
        to.anchoredPosition = from.anchoredPosition;
        to.sizeDelta = from.sizeDelta;
        to.localScale = from.localScale;
        to.localRotation = from.localRotation;
    }

    private static void CopyContent(Transform from, Transform to)
    {
        var fromText = from.GetComponent<Text>();
        var toText = to.GetComponent<Text>();
        if (fromText != null && toText != null)
        {
            toText.text = fromText.text;
            toText.fontSize = fromText.fontSize;
            toText.color = fromText.color;
            toText.alignment = fromText.alignment;
            toText.fontStyle = fromText.fontStyle;
        }
        var fromImg = from.GetComponent<Image>();
        var toImg = to.GetComponent<Image>();
        if (fromImg != null && toImg != null)
            toImg.color = fromImg.color;

        // Текст кнопки лежит в дочернем узле «Text» — рекурсивно копируем
        var fromChild = from.Find("Text");
        var toChild = to.Find("Text");
        if (fromChild != null && toChild != null)
            CopyContent(fromChild, toChild);
    }
}
