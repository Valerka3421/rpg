using Aethelgard.Render;
using Aethelgard.Systems;
using UnityEngine;
using UnityEngine.UI;
using XnaMouse = Microsoft.Xna.Framework.Input.MouseState;
using Screens = Aethelgard.Systems.ScreenMode;

namespace Aethelgard.UI;

/// <summary>
/// Магазин (порт Render/ShopScreen.cs): сетка товаров 3 колонки,
/// переключение Покупка/Продажа, переролл, цены и золото.
/// </summary>
public class ShopUI
{
    private readonly UIManager _ui;
    private readonly UITheme _t;
    private readonly GameController _game;

    public RectTransform Root = null!;
    private GameObject? _panel;
    private string _sig = "";
    private (bool buy, int buyIdx, int sellIdx, bool toggleMode, bool reroll, bool close)? _pending;

    public ShopUI(UIManager ui)
    {
        _ui = ui;
        _t = ui.Theme;
        _game = ui.Game;
    }

    public void Build()
    {
        var go = UiKit.Node("Shop", null);
        Root = UiKit.Attach(go, _ui.Root);
        Root.gameObject.SetActive(false);
    }

    public void Draw(XnaMouse mouse, XnaMouse prevMouse)
    {
        var stock = _game.CurrentShopStock();
        string sig = $"{_game.ShopSubMode}|{_game.ShopPage}|{stock.Count}";
        if (sig != _sig)
        {
            _sig = sig;
            Rebuild(stock);
        }
        if (_pending != null)
        {
            var p = _pending.Value;
            _pending = null;
            if (p.buyIdx >= 0) _game.BuyItem(p.buyIdx);
            if (p.sellIdx >= 0) _game.SellItem(p.sellIdx);
            if (p.toggleMode) _game.ToggleShopMode();
            if (p.reroll) _game.RerollShop();
            if (p.close) { _game.ScreenMode = Screens.Game; return; }
            _sig = "";
        }
    }

    private void Rebuild(System.Collections.Generic.List<RecipeData.ShopEntry> stock)
    {
        if (_panel != null) UnityEngine.Object.Destroy(_panel);
        var p = _game.Player!;
        // Модальное окно python-final (_base.py) + заголовок T.MERCH
        UiKit.Scrim(Root, _t);
        _panel = UiKit.Panel(Root, 240, 120, 1440, 840, _t, "ShopPanel");
        var parent = _panel.transform;

        string npc = _game.ActiveShopNpc ?? "merchant";
        UiKit.Label(parent, 0, 20, 1440, 44, $"ЛАВКА · {NpcName(npc).ToUpperInvariant()}",
            _t.H1Size, _t.Merch, _t, TextAnchor.MiddleCenter, "Title");
        UiKit.CloseButton(parent, 1440 - 10, 10,
            () => _pending = (false, -1, -1, false, false, true), _t);
        UiKit.Label(parent, 24, 78, 300, 24, $"Золото: {p.Gold}",
            _t.SmallSize, _t.Gold, _t, TextAnchor.UpperLeft, "Gold");

        // Переключатель режима: покупка = золото, продажа = HP_CRIT (_shop.py)
        bool buy = _game.ShopSubMode == "buy";
        var modeBtn = UiKit.WidgetButton(parent, 700, 74, 150, 30,
            buy ? "ПРОДАТЬ" : "КУПИТЬ", _t.SmallSize, _t);
        modeBtn.image.color = buy ? _t.HpCrit : _t.Gold;
        modeBtn.onClick.AddListener(() => _pending = (false, -1, -1, true, false, false));

        var reroll = UiKit.WidgetButton(parent, 880, 74, 220, 30, "Обновить (реролл)",
            _t.SmallSize, _t);
        reroll.onClick.AddListener(() => _pending = (false, -1, -1, false, true, false));

        // Товары: сетка 3×3, ячейка 112×96 gap 10 (_shop.py)
        int cellW = 112, cellH = 96, gap = 10;
        int startX = 60, startY = 140;
        for (int i = 0; i < 9; i++)
        {
            int col = i % 3, row = i / 3;
            var slot = UiKit.Node("Goods" + i, null);
            UiKit.Place(slot, parent, startX + col * (cellW + gap),
                startY + row * (cellH + gap), cellW, cellH);
            UiKit.Img(slot, _t.SlotFill);
            UiKit.Frame((RectTransform)slot.transform, _t.PanelBorder, 1);
            int idx = _game.ShopPage * 9 + i;
            if (idx < stock.Count)
            {
                var entry = stock[idx];
                var def = ItemData.GetItemData(entry.Key);
                UiKit.Label(slot.transform, 0, 0, cellW, cellH - 24,
                    def.Name, _t.TinySize, ItemData.DisplayColor(entry.Key).U(), _t,
                    TextAnchor.MiddleCenter, "Item");
                UiKit.Label(slot.transform, 0, cellH - 26, cellW, 22,
                    buy ? $"{entry.Price} з." : $"{ItemData.SellPrice(entry.Key)} з.",
                    _t.TinySize, _t.Gold, _t, TextAnchor.MiddleCenter, "Price");
                var btn = slot.AddComponent<Button>();
                btn.onClick.AddListener(() => _pending =
                    (buy, buy ? idx : -1, buy ? -1 : idx, false, false, false));
            }
        }

        int by = startY + 3 * (cellH + gap) + 24;
        var prev = UiKit.WidgetButton(parent, 60, by, 38, 28, "‹", _t.BodySize, _t);
        prev.onClick.AddListener(() => { _game.ShopPage--; _sig = ""; });
        UiKit.Label(parent, 110, by, 120, 28,
            $"{_game.ShopPage + 1}/{_game.ShopPagesCount()}", _t.BodySize, _t.Text, _t,
            TextAnchor.MiddleCenter, "Page");
        var next = UiKit.WidgetButton(parent, 240, by, 38, 28, "›", _t.BodySize, _t);
        next.onClick.AddListener(() => { _game.ShopPage++; _sig = ""; });
    }

    private static string NpcName(string key) => key switch
    {
        "blacksmith" => "Кузнец",
        "alchemist" => "Алхимик",
        "innkeeper" => "Трактирщик",
        "merchant" => "Торговец",
        _ => "Торговец",
    };
}
