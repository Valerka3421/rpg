using Aethelgard.Runtime;
using Aethelgard.Systems;
using UnityEngine;
using UnityEngine.UI;
using XnaMouse = Microsoft.Xna.Framework.Input.MouseState;
using Screens = Aethelgard.Systems.ScreenMode;

namespace Aethelgard.UI;

/// <summary>
/// Крафт (порт Render/CraftScreen из ModalScreens): табы ремёсел,
/// список рецептов с доступностью, минигейм «молот» с 3 зонами качества.
/// </summary>
public class CraftUI
{
    private readonly UIManager _ui;
    private readonly UITheme _t;
    private readonly GameController _game;

    public RectTransform Root = null!;
    private GameObject? _panel;
    private string _sig = "";
    private (int craftIdx, bool close)? _pending;

    public CraftUI(UIManager ui)
    {
        _ui = ui;
        _t = ui.Theme;
        _game = ui.Game;
    }

    public void Build()
    {
        var go = UiKit.Node("Craft", null);
        Root = UiKit.Attach(go, _ui.Root);
        Root.gameObject.SetActive(false);
    }

    public void Draw(float dt, XnaMouse mouse, XnaMouse prevMouse)
    {
        var list = _game.CraftableList();
        string sig = $"{_game.CraftTab}|{_game.CraftPage}|{_game.CraftQty}|{list.Count}|{_game.MinigameActive}";
        if (sig != _sig)
        {
            _sig = sig;
            Rebuild(list);
        }

        // Минигейм: полоса движется сама, Space — удар
        if (_game.MinigameActive)
        {
            _game.UpdateMinigame(dt);
            UpdateMinigameBar();
            var keys = InputState.Keys;
            var prevKeys = InputState.PrevKeys;
            bool space = keys.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Space) &&
                         !prevKeys.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Space);
            bool enter = keys.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Enter) &&
                         !prevKeys.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Enter);
            if (space || enter) _game.FinishMinigame();
        }

        if (_pending != null)
        {
            var p = _pending.Value;
            _pending = null;
            if (p.craftIdx >= 0) _game.StartCraft(p.craftIdx);
            if (p.close) { _game.ScreenMode = Screens.Game; return; }
            _sig = "";
        }
    }

    private (GameObject root, Image fill, Text overlay)? _mgBar;
    private Text? _mgHint;

    private void Rebuild(System.Collections.Generic.List<(string Key, ItemData.Def Item)> list)
    {
        if (_panel != null) UnityEngine.Object.Destroy(_panel);
        UiKit.Scrim(Root, _t);
        _panel = UiKit.Panel(Root, 360, 90, 1200, 900, _t, "CraftPanel");
        var parent = _panel.transform;

        UiKit.Label(parent, 0, 20, 1200, 44, "КУЗНИЦА И МАСТЕРСКАЯ",
            _t.H1Size, _t.Title, _t, TextAnchor.MiddleCenter, "Title");
        UiKit.CloseButton(parent, 1200 - 10, 10, () => _pending = (-1, true), _t);

        // Табы ремёсел (28px, активный с золотой рамкой — _craft.py)
        var tabs = RecipeData.CraftTabs;
        int tx = 60;
        for (int i = 0; i < tabs.Length; i++)
        {
            int idx = i;
            bool active = _game.CraftTab == i;
            var tab = UiKit.Node("Tab" + i, null);
            UiKit.Place(tab, parent, tx, 84, 280, 28);
            UiKit.Img(tab, active ? UITheme.C(66, 54, 38) : _t.SlotFill);
            UiKit.Frame((RectTransform)tab.transform,
                active ? _t.Gold : _t.PanelBorder, 1);
            var tabBtn = tab.AddComponent<Button>();
            tabBtn.targetGraphic = tab.GetComponent<Image>();
            tabBtn.onClick.AddListener(() => { _game.CraftTab = idx; _game.CraftPage = 0; _sig = ""; });
            UiKit.Label(tab.transform, 0, 0, 280, 28, tabs[i].Name, _t.SmallSize,
                active ? _t.Gold : _t.TextDim, _t, TextAnchor.MiddleCenter, "TabT");
            tx += 300;
        }

        // Рецепты — 5 строк
        int y = 150;
        int start = _game.CraftPage * 5;
        for (int i = 0; i < 5 && start + i < list.Count; i++)
        {
            var (key, def) = list[start + i];
            var row = UiKit.Panel(parent, 40, y, 1120, 92, _t, "Recipe" + i);
            UiKit.Solid(row.transform, 0, 0, 6, 92, _t.Gold, "Stripe");
            UiKit.Label(row.transform, 20, 8, 500, 26, def.Name,
                _t.BodySize, _t.Text, _t, TextAnchor.UpperLeft, "Name");
            UiKit.Label(row.transform, 20, 38, 700, 44, def.Desc, _t.TinySize,
                _t.TextDim, _t, TextAnchor.UpperLeft, "Desc");
            var craftBtn = UiKit.WidgetButton(row.transform, 840, 22, 240, 48,
                "Крафт ×" + _game.CraftQty, _t.SmallSize, _t);
            int idx = start + i;
            craftBtn.onClick.AddListener(() => _pending = (idx, false));
            y += 100;
        }

        // Кнопки страниц «‹ ›»
        var prev = UiKit.WidgetButton(parent, 40, 680, 38, 28, "‹", _t.BodySize, _t);
        prev.onClick.AddListener(() => { _game.CraftPage--; _sig = ""; });
        UiKit.Label(parent, 90, 680, 120, 28,
            $"{_game.CraftPage + 1}", _t.BodySize, _t.Text, _t,
            TextAnchor.MiddleCenter, "Page");
        var next = UiKit.WidgetButton(parent, 220, 680, 38, 28, "›", _t.BodySize, _t);
        next.onClick.AddListener(() => { _game.CraftPage++; _sig = ""; });

        // ── Минигейм молота ──
        var mg = UiKit.Panel(parent, 240, 740, 720, 130, _t, "Minigame");
        UiKit.Label(mg.transform, 0, 8, 720, 24, "УДАР МОЛОТОМ — ПРОБЕЛ!",
            _t.BodySize, _t.Gold, _t, TextAnchor.MiddleCenter, "MgTitle");
        var bar = UiKit.Bar(mg.transform, 40, 44, 260, 12, _t, _t.ProgressCraft);
        _mgBar = bar;
        _mgHint = UiKit.Label(mg.transform, 0, 88, 720, 24, "", _t.TinySize,
            _t.TextDim, _t, TextAnchor.MiddleCenter, "MgHint");
        mg.gameObject.SetActive(_game.MinigameActive);
    }

    private void UpdateMinigameBar()
    {
        if (_mgBar == null) return;
        // 3 зоны качества (_craft.py): 0.40–0.60 зелёная, 0.25–0.75 жёлтая
        _mgBar.Value.fill.rectTransform.anchorMax = new Vector2(_game.MinigamePos, 1f);
        _mgBar.Value.fill.color =
            _game.MinigamePos is > 0.40f and < 0.60f ? _t.Heal :
            _game.MinigamePos is > 0.25f and < 0.75f ? _t.Crit : _t.HpCrit;
        if (_mgHint != null)
            _mgHint.text = $"Точность позиции: {_game.MinigamePos:F2} — жми ПРОБЕЛ в зелёной зоне";
    }
}
