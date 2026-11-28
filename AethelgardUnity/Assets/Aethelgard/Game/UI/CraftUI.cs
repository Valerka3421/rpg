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
        _panel = UiKit.Panel(Root, 360, 90, 1200, 900, _t, "CraftPanel");
        var parent = _panel.transform;

        UiKit.Label(parent, 0, 26, 1200, 44, "КУЗНИЦА И МАСТЕРСКАЯ",
            _t.H2Size, _t.Gold, _t, TextAnchor.MiddleCenter, "Title");

        // Табы ремёсел
        var tabs = RecipeData.CraftTabs;
        int tx = 60;
        for (int i = 0; i < tabs.Length; i++)
        {
            int idx = i;
            var btn = UiKit.WidgetButton(parent, tx, 84, 280, 44,
                _game.CraftTab == i ? tabs[i].Name.ToUpperInvariant() : tabs[i].Name,
                _t.SmallSize, _t);
            btn.onClick.AddListener(() => { _game.CraftTab = idx; _game.CraftPage = 0; _sig = ""; });
            tx += 300;
        }

        // Рецепты — 5 строк
        int y = 150;
        int start = _game.CraftPage * 5;
        for (int i = 0; i < 5 && start + i < list.Count; i++)
        {
            var (key, def) = list[start + i];
            var row = UiKit.Panel(parent, 40, y, 1120, 92, _t, "Recipe" + i);
            UiKit.Solid(row.transform, 0, 0, 6, 92, _t.BronzeHi, "Stripe");
            UiKit.Label(row.transform, 20, 8, 500, 26, def.Name,
                _t.BodySize, _t.Ink, _t, TextAnchor.UpperLeft, "Name");
            UiKit.Label(row.transform, 20, 38, 700, 44, def.Desc, _t.TinySize,
                _t.InkDim, _t, TextAnchor.UpperLeft, "Desc");
            var craftBtn = UiKit.WidgetButton(row.transform, 840, 22, 240, 48,
                "Крафт ×" + _game.CraftQty, _t.SmallSize, _t);
            int idx = start + i;
            craftBtn.onClick.AddListener(() => _pending = (idx, false));
            y += 100;
        }

        // Кнопки страниц
        var prev = UiKit.WidgetButton(parent, 40, 680, 220, 44, "← Страница",
            _t.SmallSize, _t);
        prev.onClick.AddListener(() => { _game.CraftPage--; _sig = ""; });
        var next = UiKit.WidgetButton(parent, 280, 680, 220, 44, "Страница →",
            _t.SmallSize, _t);
        next.onClick.AddListener(() => { _game.CraftPage++; _sig = ""; });
        var close = UiKit.WidgetButton(parent, 860, 680, 260, 44, "Закрыть (Esc)",
            _t.SmallSize, _t);
        close.onClick.AddListener(() => _pending = (-1, true));

        // ── Минигейм молота ──
        var mg = UiKit.Panel(parent, 240, 740, 720, 130, _t, "Minigame");
        UiKit.Label(mg.transform, 0, 8, 720, 24, "УДАР МОЛОТОМ — ПРОБЕЛ!",
            _t.BodySize, _t.Gold, _t, TextAnchor.MiddleCenter, "MgTitle");
        var bar = UiKit.Bar(mg.transform, 40, 44, 640, 36, _t, _t.BronzeHi);
        _mgBar = bar;
        _mgHint = UiKit.Label(mg.transform, 0, 88, 720, 24, "", _t.TinySize,
            _t.InkDim, _t, TextAnchor.MiddleCenter, "MgHint");
        mg.gameObject.SetActive(_game.MinigameActive);
    }

    private void UpdateMinigameBar()
    {
        if (_mgBar == null) return;
        // 3 зоны качества: красная (промах), жёлтая (хорошо), зелёная (отлично)
        _mgBar.Value.fill.rectTransform.anchorMax = new Vector2(_game.MinigamePos, 1f);
        _mgBar.Value.fill.color =
            _game.MinigamePos is > 0.40f and < 0.60f ? _t.Success :
            _game.MinigamePos is > 0.25f and < 0.75f ? _t.GoldSoft : _t.Danger;
        if (_mgHint != null)
            _mgHint.text = $"Точность позиции: {_game.MinigamePos:F2} — жми ПРОБЕЛ в зелёной зоне";
    }
}
