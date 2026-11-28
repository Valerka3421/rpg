using UnityEngine;
using UnityEngine.UI;

namespace Aethelgard.UI;

/// <summary>
/// Анимированный фон меню (порт Render/MenuBackground.cs): закатный градиент,
/// тёмные «хребты» с параллаксом, всплывающие угольки, виньетка.
/// </summary>
public class MenuBackdrop : MonoBehaviour
{
    private float _t;
    private Image[] _ridges = null!;
    private RectTransform[] _embers = null!;
    private Vector2[] _emberVel = null!;
    private UITheme _theme = null!;

    public static MenuBackdrop Create(RectTransform parent, UITheme theme)
    {
        var go = new GameObject("Backdrop", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var mb = go.AddComponent<MenuBackdrop>();
        mb._theme = theme;
        mb.Build(rt);
        return mb;
    }

    private void Build(RectTransform parent)
    {
        // Небо — градиент через 3 полосы (быстро и без шейдеров)
        var sky = new GameObject("Sky", typeof(RectTransform));
        var srt = (RectTransform)sky.transform;
        srt.SetParent(parent, false);
        srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
        srt.offsetMin = srt.offsetMax = Vector2.zero;
        var top = new GameObject("top", typeof(RectTransform)).AddComponent<Image>();
        top.transform.SetParent(srt, false);
        var trt = (RectTransform)top.transform;
        trt.anchorMin = new Vector2(0, 0.55f); trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        top.color = _theme.MenuSkyTop;
        var bot = new GameObject("bot", typeof(RectTransform)).AddComponent<Image>();
        bot.transform.SetParent(srt, false);
        var brt = (RectTransform)bot.transform;
        brt.anchorMin = Vector2.zero; brt.anchorMax = new Vector2(1, 0.55f);
        brt.offsetMin = brt.offsetMax = Vector2.zero;
        bot.color = _theme.MenuSkyBottom;

        // Хребты — 3 слоя полупрозрачных тёмных Image с лёгким дрейфом
        _ridges = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            var ridge = new GameObject("ridge" + i, typeof(RectTransform)).AddComponent<Image>();
            ridge.transform.SetParent(parent, false);
            var rrt = (RectTransform)ridge.transform;
            rrt.anchorMin = Vector2.zero;
            rrt.anchorMax = new Vector2(1.3f, 0.35f + i * 0.12f);
            rrt.offsetMin = rrt.offsetMax = Vector2.zero;
            float a = 0.55f + i * 0.15f;
            ridge.color = new Color(0.05f, 0.04f, 0.03f, a);
            _ridges[i] = ridge;
        }

        // Угольки
        int count = Mathf.Clamp(_theme.MenuEmbers, 0, 120);
        _embers = new RectTransform[count];
        _emberVel = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            var e = new GameObject("ember" + i, typeof(RectTransform)).AddComponent<Image>();
            e.transform.SetParent(parent, false);
            var ert = (RectTransform)e.transform;
            ert.anchorMin = ert.anchorMax = Vector2.zero;
            ert.sizeDelta = new Vector2(3, 3);
            e.color = new Color(1f, 0.75f, 0.45f, UnityEngine.Random.Range(0.35f, 0.85f));
            ert.anchoredPosition = new Vector2(UnityEngine.Random.Range(0, 1920), UnityEngine.Random.Range(0, 1080));
            _embers[i] = ert;
            _emberVel[i] = new Vector2(UnityEngine.Random.Range(-8f, 8f), UnityEngine.Random.Range(22f, 55f));
        }

        // Виньетка
        var vg = new GameObject("vignette", typeof(RectTransform)).AddComponent<Image>();
        vg.transform.SetParent(parent, false);
        var vrt = (RectTransform)vg.transform;
        vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
        vrt.offsetMin = vrt.offsetMax = Vector2.zero;
        vg.color = new Color(0, 0, 0, 0.22f);
        vg.raycastTarget = false;
    }

    private void Update()
    {
        _t += Time.deltaTime;
        if (_ridges == null) return;
        for (int i = 0; i < _ridges.Length; i++)
        {
            var rt = _ridges[i].rectTransform;
            float x = Mathf.Sin(_t * (0.06f + i * 0.03f)) * 40f;
            rt.anchoredPosition = new Vector2(x, 0);
        }
        if (_embers == null) return;
        for (int i = 0; i < _embers.Length; i++)
        {
            var e = _embers[i];
            var p = e.anchoredPosition + _emberVel[i] * Time.deltaTime;
            if (p.y > 1120) p.y = -20;
            if (p.x < -20) p.x = 1940;
            if (p.x > 1940) p.x = -20;
            e.anchoredPosition = p;
        }
    }
}
