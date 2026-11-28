using XnaKeys = Microsoft.Xna.Framework.Input.Keys;
using Aethelgard.Runtime;
using Aethelgard.Render;
using Aethelgard.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace Aethelgard.UI;

/// <summary>
/// HUD (порт HUD GameRenderer): плашка персонажа, полосы HP/MP/XP,
/// золото, экипировка справа, хотбар снизу, тултип сообщения.
/// </summary>
public class HudUI
{
    private readonly UIManager _ui;
    private readonly UITheme _t;
    private readonly GameController _game;

    public RectTransform Root = null!;

    private Text _plate = null!;
    private (GameObject root, Image fill, Text overlay) _hp;
    private (GameObject root, Image fill, Text overlay) _mp;
    private (GameObject root, Image fill, Text overlay) _xp;
    private Text _gold = null!;
    private Text _equip = null!;
    private readonly Text[] _hotbar = new Text[8];
    private readonly Button[] _toolbar = new Button[6];

    public HudUI(UIManager ui)
    {
        _ui = ui;
        _t = ui.Theme;
        _game = ui.Game;
    }

    public void Build()
    {
        var go = UiKit.Node("Hud", null);
        Root = UiKit.Attach(go, _ui.Root);
        Root.gameObject.SetActive(false);

        // Верхняя тёмная полоса HUD (высота 170)
        UiKit.Solid(Root, 0, 0, 1920, 170, new Color(0.07f, 0.055f, 0.04f, 0.95f), "HudBg");
        UiKit.Solid(Root, 0, 168, 1920, 2, _t.Bronze, "HudEdge");

        // Плашка персонажа
        _plate = UiKit.Label(Root, 20, 10, 360, 24, "", _t.SmallSize, _t.Gold, _t,
            TextAnchor.UpperLeft, "Plate");

        // Полосы
        _hp = UiKit.Bar(Root, 20, 40, 200, 20, _t, _t.Life);
        _mp = UiKit.Bar(Root, 20, 64, 200, 20, _t, _t.Mana);
        _xp = UiKit.Bar(Root, 20, 88, 200, 16, _t, _t.Experience);

        // Золото
        _gold = UiKit.Label(Root, 20, 112, 200, 22, "", _t.BodySize, _t.Gold, _t,
            TextAnchor.UpperLeft, "Gold");

        // Экипировка справа
        UiKit.Solid(Root, 1560, 8, 350, 154, _t.Inset, "EquipPanel");
        UiKit.Frame((RectTransform)UiKit.Solid(Root, 1560, 8, 350, 154, new Color(0, 0, 0, 0)).transform, _t.Bronze, 1);
        _equip = UiKit.Label(Root, 1570, 12, 336, 146, "", _t.TinySize, _t.Ink, _t,
            TextAnchor.UpperLeft, "Equip");

        // Хотбар по центру снизу
        for (int i = 0; i < 8; i++)
        {
            int idx = i;
            var slot = UiKit.Slot(Root, 700 + i * 66, 176, 60, _t, "Hotbar" + i);
            var txt = UiKit.Label(slot.transform, 0, 0, 60, 60, "", _t.TinySize,
                _t.Ink, _t, TextAnchor.MiddleCenter, "Item");
            _hotbar[i] = txt;
        }

        // Кнопки интерфейса справа сверху экрана
        string[] names = { "И (I)", "Нав (K)", "Стат (C)", "Квесты (Q)", "Карта (M)", "Пауза (Esc)" };
        var keys = new XnaKeys?[]
        {
            Microsoft.Xna.Framework.Input.Keys.I,
            Microsoft.Xna.Framework.Input.Keys.K,
            Microsoft.Xna.Framework.Input.Keys.C,
            Microsoft.Xna.Framework.Input.Keys.Q,
            Microsoft.Xna.Framework.Input.Keys.M,
            Microsoft.Xna.Framework.Input.Keys.Escape,
        };
        for (int i = 0; i < names.Length; i++)
        {
            int idx = i;
            var btn = UiKit.WidgetButton(Root, 1180 + (i % 2) * 190, 180 + (i / 2) * 64,
                180, 56, names[i], _t.SmallSize, _t);
            btn.onClick.AddListener(() =>
            {
                if (keys[idx] is { } k)
                    _game.HandleInput(Pressed(k), InputState.PrevKeys, Time.deltaTime);
            });
            _toolbar[i] = btn;
        }
    }

    private static Microsoft.Xna.Framework.Input.KeyboardState Pressed(XnaKeys k) =>
        new(new[] { k });

    public void Draw()
    {
        var p = _game.Player;
        if (p == null) return;
        string loc = _game.WorldMode == "dungeon" ? $"Подземелье d{_game.Depth}" : "Поверхность";
        _plate.text = $"{p.ClassName} · Ур. {p.Level} · {loc}";

        float hpFrac = p.MaxHp > 0 ? (float)p.Hp / p.MaxHp : 0f;
        _hp.fill.rectTransform.anchorMax = new Vector2(hpFrac, 1f);
        _hp.overlay.text = $"{p.Hp}/{p.MaxHp}";
        float mpFrac = p.MaxMana > 0 ? (float)p.Mana / p.MaxMana : 0f;
        _mp.fill.rectTransform.anchorMax = new Vector2(mpFrac, 1f);
        _mp.overlay.text = $"{p.Mana}/{p.MaxMana}";
        float xpFrac = p.XpNext > 0 ? (float)p.Xp / p.XpNext : 0f;
        _xp.fill.rectTransform.anchorMax = new Vector2(xpFrac, 1f);
        _xp.overlay.text = $"XP {p.Xp}/{p.XpNext}";

        _gold.text = $"◆ {p.Gold}";

        _equip.text =
            $"Оружие: {ItemName(p.Weapon)}\n" +
            $"Броня: {ItemName(p.Armor)}\n" +
            $"Шлем: {ItemName(p.Head)}\n" +
            $"Ноги: {ItemName(p.Legs)}\n" +
            $"Обувь: {ItemName(p.Boots)}\n" +
            $"Очки: {p.FreePoints} · Навыки: {p.SkillPoints}";

        for (int i = 0; i < 8; i++)
        {
            var key = p.Quickbar[i];
            _hotbar[i].text = key != null && ItemData.Get(key) is { } it
                ? it.Name : $"{i + 1}";
        }
    }

    private static string ItemName(string? key) =>
        key != null && ItemData.Get(key) is { } it ? it.Name : "—";
}
