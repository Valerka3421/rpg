using System.Linq;
    using Aethelgard.Systems;
    using UnityEngine;
    using UnityEngine.UI;
    using XnaMouse = Microsoft.Xna.Framework.Input.MouseState;
    using Screens = Aethelgard.Systems.ScreenMode;

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
    /// Характеристики (порт Render/StatsScreen): 6 статов с «+»,
    /// производные статы, перки, навыки ремёсел.
    /// </summary>
    public class StatsUI
    {
        private readonly UIManager _ui;
        private readonly UITheme _t;
        private readonly GameController _game;

        public RectTransform Root = null!;
        private GameObject? _panel;
        private string _sig = "";
        private int? _allocate;

        public StatsUI(UIManager ui)
        {
            _ui = ui;
            _t = ui.Theme;
            _game = ui.Game;
        }

        public void Build()
        {
            var go = UiKit.Node("Stats", null);
            Root = UiKit.Attach(go, _ui.Root);
            Root.gameObject.SetActive(false);
        }

        public void Draw(XnaMouse mouse, XnaMouse prevMouse)
        {
            var p = _game.Player;
            if (p == null) return;
            string sig = $"{p.FreePoints}|{p.Level}";
            if (sig != _sig)
            {
                _sig = sig;
                Rebuild(p);
            }
            if (_allocate != null)
            {
                _game.AllocateStat(_allocate.Value);
                _allocate = null;
                _sig = "";
            }
        }

        private void Rebuild(Player p)
        {
            if (_panel != null) UnityEngine.Object.Destroy(_panel);
            UiKit.Scrim(Root, _t);
            _panel = UiKit.Panel(Root, 310, 90, 1300, 900, _t, "StatsPanel");
            var parent = _panel.transform;

            UiKit.Label(parent, 0, 20, 1300, 44,
                $"{p.ClassName} · УР. {p.Level} · ОЧКОВ: {p.FreePoints}",
                _t.H1Size, _t.Title, _t, TextAnchor.MiddleCenter, "Title");
            UiKit.CloseButton(parent, 1300 - 10, 10,
                () => _game.ScreenMode = Screens.Game, _t);

            var stats = Player.ComputeStats(p.Str, p.Agi, p.Vit, p.Luk,
                p.DfStat, p.IntStat, p.Level, p.Weapon, p.Armor, p.Head,
                p.Legs, p.Boots, ClassData.Data(p.ClassKey).Effects);

            // Цвета атрибутов — python-final (attr bars)
            (string Label, int Value, int Idx, Color C)[] attrs =
            {
                ("Сила (STR)", p.Str, 0, _t.BarStr),
                ("Ловкость (AGI)", p.Agi, 1, _t.BarAgi),
                ("Стойкость (VIT)", p.Vit, 2, _t.BarVit),
                ("Удача (LUK)", p.Luk, 3, _t.BarLuk),
                ("Защита (DEF)", p.DfStat, 4, _t.BarDef),
                ("Интеллект (INT)", p.IntStat, 5, _t.BarInt),
            };
            int y = 100;
            foreach (var (label, value, idx, color) in attrs)
            {
                var row = UiKit.Panel(parent, 60, y, 560, 52, _t, "Attr_" + idx);
                UiKit.Label(row.transform, 16, 14, 300, 26, label, _t.BodySize,
                    _t.Text, _t, TextAnchor.UpperLeft, "L");
                UiKit.Label(row.transform, 320, 14, 60, 26, value.ToString(),
                    _t.BodySize, color, _t, TextAnchor.UpperRight, "V");
                if (p.FreePoints > 0)
                {
                    var plus = UiKit.WidgetButton(row.transform, 470, 6, 60, 40, "+",
                        _t.H2Size, _t);
                    int captured = idx;
                    plus.onClick.AddListener(() => _allocate = captured);
                }
                y += 62;
            }

            // Производные статы
            int dx = 700;
            UiKit.Label(parent, dx, 96, 520, 24, "ПРОИЗВОДНЫЕ", _t.SmallSize,
                _t.Section, _t, TextAnchor.UpperLeft, "DerTitle");
            (string, string)[] derived =
            {
                ("Здоровье", $"{p.Hp}/{stats.MaxHp}"),
                ("Мана", $"{p.Mana}/{stats.MaxMana}"),
                ("Атака", stats.Atk.ToString()),
                ("Защита", stats.Df.ToString()),
                ("Уклонение", $"{Mathf.RoundToInt(stats.Dodge * 100)}%"),
                ("Крит. удар", $"{Mathf.RoundToInt(stats.Crit * 100)}%"),
                ("Золото", p.Gold.ToString()),
                ("Опыт", $"{p.Xp}/{p.XpNext}"),
            };
            int dy = 128;
            foreach (var (name, val) in derived)
            {
                UiKit.Label(parent, dx, dy, 300, 22, name, _t.BodySize, _t.InkDim, _t,
                    TextAnchor.UpperLeft, "D" + dy);
                UiKit.Label(parent, dx + 300, dy, 200, 22, val, _t.BodySize, _t.Ink, _t,
                    TextAnchor.UpperRight, "Dv" + dy);
                dy += 30;
            }

            // Перки
            UiKit.Label(parent, dx, dy + 16, 520, 24, "ОСОБЕННОСТИ КЛАССА",
                _t.SmallSize, _t.Section, _t, TextAnchor.UpperLeft, "PerkTitle");
            string perk = ClassData.Get(p.ClassKey)?.Perk ?? "";
            UiKit.Label(parent, dx, dy + 44, 520, 90, perk, _t.SmallSize, _t.Ink, _t,
                TextAnchor.UpperLeft, "Perk");

            // Навыки ремёсел
            UiKit.Label(parent, 60, y + 12, 560, 24, "РЕМЁСЛА", _t.SmallSize,
                _t.Section, _t, TextAnchor.UpperLeft, "CraftTitle");
            int cy = y + 42;
            foreach (var branch in RecipeData.CraftTabs.Take(3))
            {
                int lvl = p.CraftLevels.GetValueOrDefault(branch.Key, 1);
                int xp = p.CraftXp.GetValueOrDefault(branch.Key, 0);
                UiKit.Label(parent, 60, cy, 300, 22, $"{branch} — ур. {lvl}",
                    _t.SmallSize, _t.Ink, _t, TextAnchor.UpperLeft, "C" + cy);
                UiKit.Label(parent, 380, cy, 240, 22, $"{xp} XP", _t.SmallSize,
                    _t.InkDim, _t, TextAnchor.UpperRight, "Cx" + cy);
                cy += 28;
            }

        }
    }
}
