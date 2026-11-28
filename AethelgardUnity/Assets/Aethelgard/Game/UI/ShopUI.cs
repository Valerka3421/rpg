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
        _panel = UiKit.Panel(Root, 360, 100, 1200, 880, _t, "ShopPanel");
        var parent = _panel.transform;

        string npc = _game.ActiveShopNpc ?? "merchant";
        UiKit.Label(parent, 0, 26, 1200, 44, $"ЛАВКА · {NpcName(npc).ToUpperInvariant()}",
            _t.H2Size, _t.Gold, _t, TextAnchor.MiddleCenter, "Title");
        UiKit.Label(parent, 24, 84, 300, 24, $"Золото: {p.Gold}",
            _t.SmallSize, _t.GoldSoft, _t, TextAnchor.UpperLeft, "Gold");

        bool buy = _game.ShopSubMode == "buy";
        var modeBtn = UiKit.WidgetButton(parent, 700, 78, 200, 44,
            buy ? "ПРОДАТЬ" : "КУПИТЬ", _t.SmallSize, _t);
        modeBtn.onClick.AddListener(() => _pending = (false, -1, -1, true, false, false));

        var reroll = UiKit.WidgetButton(parent, 920, 78, 220, 44, "Обновить (реролл)",
            _t.SmallSize, _t);
        reroll.onClick.AddListener(() => _pending = (false, -1, -1, false, true, false));

        // Товары: сетка 3×3
        int slotSize = 150, gap = 14;
        int startX = 60, startY = 150;
        for (int i = 0; i < 9; i++)
        {
            int col = i % 3, row = i / 3;
            var slot = UiKit.Slot(parent, startX + col * (slotSize + gap),
                startY + row * (slotSize + gap), slotSize, _t, "Goods" + i);
            int idx = _game.ShopPage * 9 + i;
            if (idx < stock.Count)
            {
                var entry = stock[idx];
                var def = ItemData.GetItemData(entry.Key);
                UiKit.Label(slot.transform, 0, 0, slotSize, slotSize,
                    def.Name, _t.TinySize, ItemData.DisplayColor(entry.Key).U(), _t,
                    TextAnchor.MiddleCenter, "Item");
                UiKit.Label(slot.transform, 0, slotSize - 26, slotSize, 22,
                    buy ? $"{entry.Price} з." : $"{ItemData.SellPrice(entry.Key)} з.",
                    _t.TinySize, _t.GoldSoft, _t, TextAnchor.MiddleCenter, "Price");
                var btn = slot.AddComponent<Button>();
                btn.onClick.AddListener(() => _pending =
                    (buy, buy ? idx : -1, buy ? -1 : idx, false, false, false));
            }
        }

        int by = 150 + 3 * (slotSize + gap) + 24;
        var prev = UiKit.WidgetButton(parent, 60, by, 220, 44, "← Страница",
            _t.SmallSize, _t);
        prev.onClick.AddListener(() => { _game.ShopPage--; _sig = ""; });
        UiKit.Label(parent, 300, by, 200, 44,
            $"{_game.ShopPage + 1}/{_game.ShopPagesCount()}", _t.BodySize, _t.Ink, _t,
            TextAnchor.MiddleCenter, "Page");
        var next = UiKit.WidgetButton(parent, 500, by, 220, 44, "Страница →",
            _t.SmallSize, _t);
        next.onClick.AddListener(() => { _game.ShopPage++; _sig = ""; });
        var close = UiKit.WidgetButton(parent, 860, by, 260, 44, "Закрыть (Esc)",
            _t.SmallSize, _t);
        close.onClick.AddListener(() => _pending = (false, -1, -1, false, false, true));
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
