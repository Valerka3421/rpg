using Aethelgard.Runtime;
using Aethelgard.Render;
using System.Linq;
using Aethelgard.Systems;
using UnityEngine;
using UnityEngine.UI;
using XnaKeys = Microsoft.Xna.Framework.Input.Keys;
using XnaMouse = Microsoft.Xna.Framework.Input.MouseState;

namespace Aethelgard.UI;

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

    public void Draw(XnaMouse mouse, XnaMouse prevMouse)
    {
        HandleKeys(InputState.Keys, InputState.PrevKeys);
        Redraw();
    }

    private void Rebuild()
    {
        if (_panel != null) UnityEngine.Object.Destroy(_panel);
        string key = StarterClasses[Selected];
        var cls = ClassData.Get(key);
        if (cls == null) return;
        var color = cls.Color.U();

        _panel = UiKit.Panel(Root, 560, 120, 800, 860, _t, "ClassPanel");
        var parent = _panel.transform;

        UiKit.Label(parent, 0, 30, 800, 52, cls.Name, _t.H1Size, color, _t,
            TextAnchor.MiddleCenter, "ClassName");
        UiKit.Label(parent, 0, 92, 800, 24, $"{cls.Tier}-я ступень · {cls.Base}",
            _t.SmallSize, _t.InkDim, _t, TextAnchor.MiddleCenter, "Tier");
        UiKit.Separator(parent, 80, 130, 640);
        UiKit.Label(parent, 60, 150, 680, 90, cls.Desc, _t.BodySize, _t.Ink, _t,
            TextAnchor.UpperLeft, "Desc");

        // Портрет — процедурный спрайт класса
        var portrait = UiKit.Slot(parent, 320, 250, 160, _t, "Portrait");
        UiKit.Frame((RectTransform)portrait.transform, color, 2);
        var imgGo = UiKit.Node("Sprite", null);
        UiKit.Place(imgGo, portrait.transform, 8, 8, 144, 144);
        var img = imgGo.AddComponent<Image>();
        img.sprite = Aethelgard.Render.SpriteStore.Get(
            "player_" + key, cls.Color);
        img.preserveAspect = true;

        // Производные статы
        var stats = Aethelgard.Entities.Player.ComputeStats(
            cls.Stats.GetValueOrDefault("str", 5), cls.Stats.GetValueOrDefault("agi", 5),
            cls.Stats.GetValueOrDefault("vit", 5), cls.Stats.GetValueOrDefault("luk", 5),
            cls.Stats.GetValueOrDefault("df_stat", 5), cls.Stats.GetValueOrDefault("int", 5),
            1, cls.StartWeapon, cls.StartArmor, null, null, null,
            cls.Effects ?? new System.Collections.Generic.Dictionary<string, double>());
        (string, string, Color)[] chips =
        {
            ("ATK", stats.Atk.ToString(), new Color(0.87f, 0.59f, 0.43f)),
            ("DEF", stats.Df.ToString(), new Color(0.59f, 0.67f, 0.78f)),
            ("HP", stats.MaxHp.ToString(), new Color(0.86f, 0.33f, 0.25f)),
            ("MP", stats.MaxMana.ToString(), new Color(0.47f, 0.63f, 1f)),
            ("Уклон", $"{Mathf.Round(stats.Dodge * 100)}%", new Color(0.59f, 0.65f, 0.7f)),
            ("Крит", $"{Mathf.Round(stats.Crit * 100)}%", new Color(0.91f, 0.7f, 0.29f)),
        };
        int cx0 = 100, cy0 = 430;
        for (int i = 0; i < chips.Length; i++)
        {
            int col = i % 3, row = i / 3;
            var chip = UiKit.Panel(parent, cx0 + col * 210, cy0 + row * 62, 196, 52, _t,
                "Chip" + i);
            UiKit.Label(chip.transform, 10, 6, 100, 20, chips[i].Item1, _t.TinySize,
                _t.InkFaint, _t, TextAnchor.UpperLeft, "CL");
            UiKit.Label(chip.transform, 10, 24, 176, 24, chips[i].Item2, _t.H2Size,
                chips[i].Item3, _t, TextAnchor.LowerLeft, "CV");
        }

        // Атрибуты полосками
        const int maxVal = 12;
        (string, int, Color)[] attrs =
        {
            ("STR", cls.Stats.GetValueOrDefault("str", 5), new Color(0.86f, 0.47f, 0.39f)),
            ("AGI", cls.Stats.GetValueOrDefault("agi", 5), new Color(0.55f, 0.78f, 0.51f)),
            ("VIT", cls.Stats.GetValueOrDefault("vit", 5), new Color(0.86f, 0.47f, 0.43f)),
            ("LUK", cls.Stats.GetValueOrDefault("luk", 5), new Color(0.86f, 0.78f, 0.43f)),
            ("DEF", cls.Stats.GetValueOrDefault("df_stat", 5), new Color(0.59f, 0.67f, 0.82f)),
            ("INT", cls.Stats.GetValueOrDefault("int", 5), new Color(0.51f, 0.67f, 0.94f)),
        };
        int ay = 570;
        foreach (var (label, val, barColor) in attrs)
        {
            UiKit.Label(parent, 100, ay, 60, 22, label, _t.SmallSize, _t.Ink, _t,
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
            _t.GoldSoft, _t, TextAnchor.UpperLeft, "GearTitle");
        int gy = ay + 42;
        var gear = StartGear(cls);
        foreach (var line in gear.Take(4))
        {
            UiKit.Label(parent, 110, gy, 640, 20, "◆ " + line, _t.SmallSize,
                _t.InkDim, _t, TextAnchor.UpperLeft, "G" + gy);
            gy += 22;
        }

        // Особенность
        UiKit.Label(parent, 100, gy + 10, 600, 22, "ОСОБЕННОСТЬ", _t.SmallSize,
            _t.GoldSoft, _t, TextAnchor.UpperLeft, "PerkTitle");
        UiKit.Label(parent, 100, gy + 38, 640, 60,
            string.IsNullOrEmpty(cls.Perk) ? cls.Desc : cls.Perk, _t.SmallSize,
            _t.Ink, _t, TextAnchor.UpperLeft, "Perk");

        // Кнопка старт + назад
        var start = UiKit.WidgetButton(parent, 200, 780, 400, 52,
            $"ИГРАТЬ ЗА {cls.Name.ToUpperInvariant()}", _t.BodySize, _t);
        start.onClick.AddListener(() => _startClicked = true);
        var back = UiKit.WidgetButton(Root, 40, 30, 120, 60, "←", _t.H2Size, _t);
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
