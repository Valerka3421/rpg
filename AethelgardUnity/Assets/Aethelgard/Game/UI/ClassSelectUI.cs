using Aethelgard.Runtime;
    using Aethelgard.Render;
    using System.Linq;
    using Aethelgard.Systems;
    using UnityEngine;
    using UnityEngine.UI;
    using XnaKeys = Microsoft.Xna.Framework.Input.Keys;
    using XnaMouse = Microsoft.Xna.Framework.Input.MouseState;

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
    /// Выбор класса (порт Render/ClassSelectScreen.cs): карточки классов слева,
    /// предпросмотр справа — производные статы, полоски атрибутов, стартовый
    /// набор, особенность, кнопка «ИГРАТЬ ЗА …».
    /// </summary>
    public class ClassSelectUI
    {
        private readonly UIManager _ui;
        private readonly UITheme _t;
        private readonly GameController _game;
        public RectTransform Root = null!;
        public int Selected;

        private GameObject? _panel;
    private GameObject? _cards;
        private string _sig = "";
        private bool _startClicked;

        private static readonly string[] StarterClasses = { "warrior", "mage", "rogue", "artisan" };

        public ClassSelectUI(UIManager ui)
        {
            _ui = ui; _t = ui.Theme; _game = ui.Game;
        }

        public void Build()
        {
            var go = UiKit.Node("ClassSelect", null);
            Root = UiKit.Attach(go, _ui.Root);
            Root.gameObject.SetActive(false);
            MenuBackdrop.Create(Root, _t);
        }

        public void HandleKeys(Microsoft.Xna.Framework.Input.KeyboardState keys, Microsoft.Xna.Framework.Input.KeyboardState prevKeys)
        {
            bool up = (keys.IsKeyDown(XnaKeys.Up) && !prevKeys.IsKeyDown(XnaKeys.Up)) ||
                      (keys.IsKeyDown(XnaKeys.W) && !prevKeys.IsKeyDown(XnaKeys.W));
            bool down = (keys.IsKeyDown(XnaKeys.Down) && !prevKeys.IsKeyDown(XnaKeys.Down)) ||
                        (keys.IsKeyDown(XnaKeys.S) && !prevKeys.IsKeyDown(XnaKeys.S));
            bool enter = keys.IsKeyDown(XnaKeys.Enter) && !prevKeys.IsKeyDown(XnaKeys.Enter);
            if (up) { Selected = (Selected + StarterClasses.Length - 1) % StarterClasses.Length; _sig = ""; }
            if (down) { Selected = (Selected + 1) % StarterClasses.Length; _sig = ""; }
            if (enter) _startClicked = true;
        }

        /// <summary>Отрисовка экрана выбора класса (без клавиатуры).</summary>
        public void Redraw()
        {
            string sig = $"cls{Selected}";
            if (sig != _sig)
            {
                _sig = sig;
                Rebuild();
            }

            if (_startClicked)
            {
                _startClicked = false;
                _ui.HandleClassSelection(StarterClasses[Selected]);
                _sig = "";
            }
        }

        private void Rebuild()
        {
            // Уничтожаем ВСЁ содержимое экрана (карточки и «←» тоже — иначе
            // они накапливались при каждой смене выбора)
            if (_panel != null) UnityEngine.Object.Destroy(_panel);
            if (_cards != null) UnityEngine.Object.Destroy(_cards);
            string key = StarterClasses[Selected];
            var cls = ClassData.Get(key);
            if (cls == null) return;
            var color = cls.Color.U();

            // Контейнер карточек (пересоздаётся вместе с превью)
            _cards = UiKit.Node("Cards", null);
            UiKit.Attach(_cards, Root);
            var cardsT = _cards.transform;

            // python-final: слева карточки 380×96 (x=70, y=118, шаг 110),
            // справа превью-панель с рамкой цвета класса
            for (int i = 0; i < StarterClasses.Length; i++)
            {
                var cardCls = ClassData.Get(StarterClasses[i]);
                if (cardCls == null) continue;
                var cColor = cardCls.Color.U();
                var card = UiKit.Node("ClassCard" + i, null);
                UiKit.Place(card, cardsT, 70, 118 + i * 110, 380, 96);
                UiKit.Img(card, i == Selected ? UITheme.Shade(cColor, 0.25f) : _t.Panel);
                UiKit.Frame((RectTransform)card.transform,
                    i == Selected ? _t.Gold : _t.PanelBorder, 2);
                var cardBtn = card.AddComponent<Button>();
                cardBtn.targetGraphic = card.GetComponent<Image>();
                int captured = i;
                cardBtn.onClick.AddListener(() => { Selected = captured; _sig = ""; });
                // Портрет-миниатюра в карточке
                var thumb = UiKit.Node("Thumb", null);
                UiKit.Place(thumb, card.transform, 8, 8, 80, 80);
                var thumbImg = thumb.AddComponent<Image>();
                thumbImg.sprite = Aethelgard.Render.SpriteStore.Get(
                    "player_" + StarterClasses[i], cardCls.Color);
                thumbImg.preserveAspect = true;
                UiKit.Label(card.transform, 100, 12, 270, 28, cardCls.Name, _t.H2Size,
                    cColor, _t, TextAnchor.UpperLeft, "CardName");
                UiKit.Label(card.transform, 100, 44, 270, 44, $"{cardCls.Tier}-я ступень · {cardCls.Base}",
                    _t.TinySize, _t.TextDim, _t, TextAnchor.UpperLeft, "CardTier");
            }

            // Правая панель превью (26,22,18,α236) + рамка цвета класса (α90)
            _panel = UiKit.Node("ClassPanel", null);
            UiKit.Place(_panel, Root, 520, 60, 1340, 960);
            var pImg = UiKit.Img(_panel, _t.CardPreviewFill);
            UiKit.Frame((RectTransform)_panel.transform, UITheme.Shade(color, 0.9f), 2);
            var parent = _panel.transform;

            UiKit.Label(parent, 0, 30, 1340, 52, cls.Name, _t.H1Size, color, _t,
                TextAnchor.MiddleCenter, "ClassName");
            UiKit.Label(parent, 0, 92, 1340, 24, $"{cls.Tier}-я ступень · {cls.Base}",
                _t.SmallSize, _t.TextDim, _t, TextAnchor.MiddleCenter, "Tier");
            UiKit.Separator(parent, 80, 130, 640, _t);
            UiKit.Label(parent, 60, 150, 1200, 90, cls.Desc, _t.BodySize, _t.Text, _t,
                TextAnchor.UpperLeft, "Desc");

            // Портрет 180 + рамка цвета класса (α120)
            var portrait = UiKit.Node("Portrait", null);
            UiKit.Place(portrait, parent, 320, 250, 180, 180);
            UiKit.Img(portrait, _t.PortraitFill);
            UiKit.Frame((RectTransform)portrait.transform, UITheme.Shade(color, 0.47f), 2);
            var imgGo = UiKit.Node("Sprite", null);
            UiKit.Place(imgGo, portrait.transform, 10, 10, 160, 160);
            var img = imgGo.AddComponent<Image>();
            img.sprite = Aethelgard.Render.SpriteStore.Get(
                "player_" + key, cls.Color);
            img.preserveAspect = true;

            // Производные статы — чипы (34,29,23)/(70,60,46), цвета python
            var stats = Aethelgard.Entities.Player.ComputeStats(
                cls.Stats.GetValueOrDefault("str", 5), cls.Stats.GetValueOrDefault("agi", 5),
                cls.Stats.GetValueOrDefault("vit", 5), cls.Stats.GetValueOrDefault("luk", 5),
                cls.Stats.GetValueOrDefault("df_stat", 5), cls.Stats.GetValueOrDefault("int", 5),
                1, cls.StartWeapon, cls.StartArmor, null, null, null,
                cls.Effects ?? new System.Collections.Generic.Dictionary<string, double>());
            (string, string, Color)[] chips =
            {
                ("ATK", stats.Atk.ToString(), _t.AttrAtk),
                ("DEF", stats.Df.ToString(), _t.AttrDef),
                ("HP", stats.MaxHp.ToString(), _t.AttrHp),
                ("MP", stats.MaxMana.ToString(), _t.AttrMp),
                ("Уклон", $"{Mathf.Round(stats.Dodge * 100)}%", _t.AttrDodge),
                ("Крит", $"{Mathf.Round(stats.Crit * 100)}%", _t.AttrCrit),
            };
            int cx0 = 100, cy0 = 430;
            for (int i = 0; i < chips.Length; i++)
            {
                int col = i % 3, row = i / 3;
                var chip = UiKit.Node("Chip" + i, null);
                UiKit.Place(chip, parent, cx0 + col * 210, cy0 + row * 62, 196, 48);
                UiKit.Img(chip, _t.ChipFill);
                UiKit.Frame((RectTransform)chip.transform, _t.ChipBorder, 1);
                UiKit.Label(chip.transform, 10, 6, 100, 20, chips[i].Item1, _t.TinySize,
                    _t.TextMuted, _t, TextAnchor.UpperLeft, "CL");
                UiKit.Label(chip.transform, 10, 22, 176, 24, chips[i].Item2, _t.H2Size,
                    chips[i].Item3, _t, TextAnchor.LowerLeft, "CV");
            }

            // Атрибуты полосками (цвета python, фон (20,18,15), максимум 12)
            const int maxVal = 12;
            (string, int, Color)[] attrs =
            {
                ("STR", cls.Stats.GetValueOrDefault("str", 5), _t.BarStr),
                ("AGI", cls.Stats.GetValueOrDefault("agi", 5), _t.BarAgi),
                ("VIT", cls.Stats.GetValueOrDefault("vit", 5), _t.BarVit),
                ("LUK", cls.Stats.GetValueOrDefault("luk", 5), _t.BarLuk),
                ("DEF", cls.Stats.GetValueOrDefault("df_stat", 5), _t.BarDef),
                ("INT", cls.Stats.GetValueOrDefault("int", 5), _t.BarInt),
            };
            int ay = 570;
            foreach (var (label, val, barColor) in attrs)
            {
                UiKit.Label(parent, 100, ay, 60, 22, label, _t.SmallSize, _t.Text, _t,
                    TextAnchor.UpperLeft, "A" + label);
                var bar = UiKit.Bar(parent, 180, ay + 3, 380, 13, _t, barColor);
                bar.fill.rectTransform.anchorMax = new Vector2(
                    Mathf.Clamp01(val / (float)maxVal), 1f);
                UiKit.Label(parent, 570, ay, 60, 22, val.ToString(), _t.SmallSize,
                    barColor, _t, TextAnchor.UpperRight, "Av" + label);
                ay += 28;
            }

            // Стартовый набор
            UiKit.Label(parent, 100, ay + 14, 600, 22, "СТАРТОВЫЙ НАБОР", _t.SmallSize,
                _t.Section, _t, TextAnchor.UpperLeft, "GearTitle");
            int gy = ay + 42;
            var gear = StartGear(cls);
            foreach (var line in gear.Take(4))
            {
                UiKit.Label(parent, 110, gy, 640, 20, "◆ " + line, _t.SmallSize,
                    _t.TextDim, _t, TextAnchor.UpperLeft, "G" + gy);
                gy += 22;
            }

            // Особенность
            UiKit.Label(parent, 100, gy + 10, 600, 22, "ОСОБЕННОСТЬ", _t.SmallSize,
                _t.Section, _t, TextAnchor.UpperLeft, "PerkTitle");
            UiKit.Label(parent, 100, gy + 38, 640, 60,
                string.IsNullOrEmpty(cls.Perk) ? cls.Desc : cls.Perk, _t.SmallSize,
                _t.Text, _t, TextAnchor.UpperLeft, "Perk");

            // Кнопка «Начать» в цвете класса (фон ×0.92, рамка ×1.4 — python)
            var start = UiKit.Node("StartButton", null);
            UiKit.Place(start, parent, 200, 880, 400, 56);
            UiKit.Img(start, UITheme.Shade(color, 0.92f));
            UiKit.Frame((RectTransform)start.transform, UITheme.Shade(color, 1.4f), 2);
            // Тёмный или светлый текст по яркости цвета класса
            float lum = 0.299f * color.r + 0.587f * color.g + 0.114f * color.b;
            var stTxt = UiKit.Label(start.transform, 0, 0, 400, 56,
                $"ИГРАТЬ ЗА {cls.Name.ToUpperInvariant()}", _t.BodySize,
                lum > 0.5f ? UITheme.C(20, 16, 12) : UITheme.C(250, 246, 238), _t,
                TextAnchor.MiddleCenter, "StartText");
            var startBtn = start.AddComponent<Button>();
            startBtn.targetGraphic = start.GetComponent<Image>();
            startBtn.onClick.AddListener(() => _startClicked = true);

            var back = UiKit.WidgetButton(cardsT, 34, 28, 52, 48, "←", _t.H2Size, _t);
            back.onClick.AddListener(() => _ui.HandleClassSelection(null));
        }

        private static System.Collections.Generic.List<string> StartGear(ClassData.Def cls)
        {
            var gear = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(cls.StartWeapon) && ItemData.Get(cls.StartWeapon) is { } w)
                gear.Add(w.Name);
            if (!string.IsNullOrEmpty(cls.StartArmor) && ItemData.Get(cls.StartArmor) is { } a)
                gear.Add(a.Name);
            if (cls.StartItems != null)
                foreach (var (key, count) in cls.StartItems)
                    if (ItemData.Get(key) is { } it)
                        gear.Add($"{count}× {it.Name}");
            if (gear.Count == 0) gear.Add("Пустые руки — всё впереди");
            return gear;
        }
    }
}
