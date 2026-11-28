using UnityEngine;
    using UnityEngine.UI;

    namespace Aethelgard.UI
    {

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Aethelgard.BaseClass;
    using Aethelgard.Entities;
    using Aethelgard.Entities.Monsters;
    using Aethelgard.Structs;
    using Aethelgard.World;
    using Aethelgard.Systems;
    using Aethelgard.Data;
    using Aethelgard.Net;
    using Aethelgard.Audio;
    /// <summary>
    /// Анимированный фон меню — точный порт render/menu_background.py:
    /// процедурный ландшафт (сид 20241114, ячейка 48px, сглаженный шум),
    /// вода/песок/трава/лесные кластеры/извилистая дорога/руины,
    /// вертикальное затухание, 44 поднимающихся уголька (240,190,110),
    /// виньетка (5,4,3), затемнение (6,5,4,α70) поверх.
    /// </summary>
    public class MenuBackdrop : MonoBehaviour
    {
        private float _t;
        private RectTransform[] _embers = null!;
        private Vector2[] _emberVel = null!;
        private UITheme _theme = null!;

        private static Texture2D? _landscape;

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
            // ── Ландшафт (одна текстура, генерируется один раз) ──
            if (_landscape == null) _landscape = BuildLandscape();

            var land = new GameObject("Landscape", typeof(RectTransform))
                .AddComponent<Image>();
            land.transform.SetParent(parent, false);
            var lrt = (RectTransform)land.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            land.sprite = Sprite.Create(_landscape,
                new Rect(0, 0, _landscape.width, _landscape.height),
                Vector2.one * 0.5f, 100, 0, SpriteMeshType.FullRect);
            land.raycastTarget = false;

            // ── Вертикальное затухание (8,7,6) alpha 70→180 ──
            var fade = new GameObject("Fade", typeof(RectTransform)).AddComponent<Image>();
            fade.transform.SetParent(parent, false);
            var frt = (RectTransform)fade.transform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = frt.offsetMax = Vector2.zero;
            var fadeSprite = Sprite.Create(VerticalFade(),
                new Rect(0, 0, 4, 64), Vector2.one * 0.5f, 100, 0,
                SpriteMeshType.FullRect);
            fade.sprite = fadeSprite;
            fade.type = Image.Type.Simple;
            fade.raycastTarget = false;

            // ── Затемнение (6,5,4,α70) ──
            var dim = new GameObject("Dim", typeof(RectTransform)).AddComponent<Image>();
            dim.transform.SetParent(parent, false);
            var drt = (RectTransform)dim.transform;
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = drt.offsetMax = Vector2.zero;
            dim.color = UITheme.C(6, 5, 4, 70f / 255f);
            dim.raycastTarget = false;

            // ── Угольки (44 шт., (240,190,110), поднимаются) ──
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
                e.color = _theme.MenuEmber;
                e.raycastTarget = false;
                ert.anchoredPosition = new Vector2(UnityEngine.Random.Range(0, 1920), UnityEngine.Random.Range(0, 1080));
                _embers[i] = ert;
                _emberVel[i] = new Vector2(UnityEngine.Random.Range(-8f, 8f), UnityEngine.Random.Range(22f, 55f));
            }

            // ── Виньетка (5,4,3) — 70-ступенчатый градиент ≈ alpha 0.45 по краям ──
            var vg = new GameObject("vignette", typeof(RectTransform)).AddComponent<Image>();
            vg.transform.SetParent(parent, false);
            var vrt = (RectTransform)vg.transform;
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
            vrt.offsetMin = vrt.offsetMax = Vector2.zero;
            vg.sprite = UiKit.RadialGradient(256, 1.6f);
            vg.type = Image.Type.Simple;
            vg.color = new Color(5f / 255f, 4f / 255f, 3f / 255f, 0.38f);
            vg.raycastTarget = false;
        }

        // ── Процедурный ландшафт (menu_background.py, 1:1) ──

        private static Texture2D BuildLandscape()
        {
            int w = 320, h = 180;            // 48px-ячейки в разрешении 1920×1080 → 40×22.5;
            const int cell = 6;              // рисуем 320×180 и растягиваем (Point→Bilinear)
            var rnd = new System.Random(20241114);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

            // Палитра menu_background.py
            var water = UITheme.C(34, 52, 62);
            var waterDeep = UITheme.C(24, 38, 50);
            var sand = UITheme.C(96, 86, 60);
            var grassA = UITheme.C(48, 58, 38);
            var grassB = UITheme.C(62, 72, 44);
            var grassC = UITheme.C(78, 84, 50);
            var forest = UITheme.C(34, 44, 32);

            // Сглаженный шум высоты (ячейка 48px → в нашей сетке ~5px)
            int gw = w / cell + 2, gh = h / cell + 2;
            var noise = new float[gw, gh];
            for (int y = 0; y < gh; y++)
                for (int x = 0; x < gw; x++)
                    noise[x, y] = (float)rnd.NextDouble();
            float Sample(float fx, float fy)
            {
                int x0 = (int)fx, y0 = (int)fy;
                float tx = fx - x0, ty = fy - y0;
                tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty); // smoothstep
                float a = Lerp(noise[x0, y0], noise[x0 + 1, y0], tx);
                float b = Lerp(noise[x0, y0 + 1], noise[x0 + 1, y0 + 1], tx);
                return Lerp(a, b, ty);
            }

            // Река/вода слева + песок по берегу, дорога справа-налево
            var px = new Color32[w * h];
            var road = new List<(int, int)>();
            int roadX = w - 40;
            for (int y = h - 1; y >= 0; y--)
            {
                road.Add((roadX, y));
                roadX += rnd.Next(-1, 2);
                roadX = Mathf.Clamp(roadX, w / 2, w - 8);
            }

            // Лесные кластеры
            var forestSpots = new List<(float, float, float)>();
            for (int i = 0; i < 22; i++)
                forestSpots.Add((rnd.Next(0, w), rnd.Next(0, h), 8 + rnd.Next(0, 16)));

            var ruinsSpots = new List<(int, int)>();
            for (int i = 0; i < 6; i++)
                ruinsSpots.Add((rnd.Next(0, w), rnd.Next(0, h)));

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float n = Sample(x / (float)cell, y / (float)cell);
                    Color c;
                    float shoreDist = x; // вода в левой части
                    if (x < 46 + n * 22)
                        c = Color.Lerp(water, waterDeep, Mathf.Clamp01((46 + n * 22 - x) / 40f));
                    else if (x < 60 + n * 22)
                        c = sand;
                    else
                    {
                        // трава: 3 оттенка по шуму
                        c = n < 0.4f ? grassA : n < 0.72f ? grassB : grassC;
                        // лесные кластеры
                        foreach (var (fx, fy, fr) in forestSpots)
                        {
                            float d = Mathf.Sqrt((x - fx) * (x - fx) + (y - fy) * (y - fy));
                            if (d < fr)
                            {
                                float k = d / fr;
                                c = k < 0.55f ? UITheme.C(26, 34, 24)
                                    : k < 0.85f ? forest
                                    : UITheme.C(46, 58, 38);
                            }
                        }
                        // дорога
                        foreach (var (rx, ry) in road)
                        {
                            int dx = Mathf.Abs(x - rx);
                            if (dx <= 3) c = UITheme.C(94, 82, 60);
                            else if (dx <= 4) c = UITheme.C(112, 98, 70);
                        }
                        // руины
                        foreach (var (rx, ry) in ruinsSpots)
                        {
                            int dx = Mathf.Abs(x - rx), dy = Mathf.Abs(y - ry);
                            if (dx < 6 && dy < 4 && (dx + dy) % 3 != 0)
                                c = (dx + dy) % 2 == 0 ? UITheme.C(70, 66, 58) : UITheme.C(100, 94, 80);
                        }
                    }
                    px[y * w + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private static Texture2D? _vFade;

        private static Texture2D VerticalFade()
        {
            if (_vFade != null) return _vFade;
            var tex = new Texture2D(4, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
            {
                // верх прозрачно → низ непрозрачно (alpha 70→180 из 255)
                float k = 1f - y / 63f;
                byte a = (byte)Mathf.RoundToInt(35f + (120f - 35f) * k);
                for (int x = 0; x < 4; x++)
                    tex.SetPixel(x, y, new Color32(8, 7, 6, a));
            }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            _vFade = tex;
            return tex;
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private void Update()
        {
            _t += Time.deltaTime;
            if (_embers == null) return;
            for (int i = 0; i < _embers.Length; i++)
            {
                var e = _embers[i];
                var p = e.anchoredPosition + _emberVel[i] * Time.deltaTime;
                // лёгкое синусоидальное покачивание как в python
                p.x += Mathf.Sin(_t * 2f + i) * 6f * Time.deltaTime;
                if (p.y > 1120) p.y = -20;
                if (p.x < -20) p.x = 1940;
                if (p.x > 1940) p.x = -20;
                e.anchoredPosition = p;
            }
        }
    }
}
