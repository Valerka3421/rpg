using Aethelgard.Runtime;
using Aethelgard.Render;
using System.Collections.Generic;
using Aethelgard.Systems;
using UnityEngine;
using UnityEngine.UI;
using XnaMouse = Microsoft.Xna.Framework.Input.MouseState;
using Screens = Aethelgard.Systems.ScreenMode;

namespace Aethelgard.UI;

/// <summary>
/// Инвентарь (порт Render/InventoryScreen.cs): сетка предметов с страницами,
/// панель экипировки, кнопки «Использовать»/«Снять», золото.
/// </summary>
public class InventoryUI
{
    private readonly UIManager _ui;
    private readonly UITheme _t;
    private readonly GameController _game;

    public RectTransform Root = null!;
    private GameObject? _panel;
    private string _sig = "";
    private int _useSel = -1;

    public InventoryUI(UIManager ui)
    {
        _ui = ui;
        _t = ui.Theme;
        _game = ui.Game;
    }

    public void Build()
    {
        var go = UiKit.Node("Inventory", null);
        Root = UiKit.Attach(go, _ui.Root);
        Root.gameObject.SetActive(false);
    }

    public void Draw(XnaMouse mouse, XnaMouse prevMouse)
    {
        var p = _game.Player;
        if (p == null) return;

        string sig = $"{_game.CurrentPage}|{p.Inventory.Count}";
        if (sig != _sig)
        {
            _sig = sig;
            Rebuild(p);
        }
        _useSel = -1;
        // Обработанные клики записаны в _pendingAction
        if (_pendingAction != null)
        {
            var a = _pendingAction.Value;
            _pendingAction = null;
            if (a.use != null) _game.UseInventoryItem(a.use);
            if (a.unequip != null) _game.UnequipInventorySlot(a.unequip);
            if (a.pageDelta != 0) _game.ChangeInventoryPage(a.pageDelta);
            if (a.close) { _game.ScreenMode = Screens.Game; return; }
            _sig = ""; // перерисовать
        }
    }

    private (string? use, string? unequip, int pageDelta, bool close)? _pendingAction;

    private void Rebuild(Player p)
    {
        if (_panel != null) UnityEngine.Object.Destroy(_panel);
        _panel = UiKit.Panel(Root, 310, 100, 1300, 880, _t, "InvPanel");
        var parent = _panel.transform;

        UiKit.Label(parent, 0, 26, 1300, 44, "ИНВЕНТАРЬ", _t.H2Size, _t.Gold, _t,
            TextAnchor.MiddleCenter, "Title");
        UiKit.Label(parent, 24, 80, 400, 24,
            $"Золото: {_game.Player!.Gold}    Стр. {_game.CurrentPage + 1}/{_game.InventoryPagesCount()}",
            _t.SmallSize, _t.GoldSoft, _t, TextAnchor.UpperLeft, "Gold");

        // Сетка 8×4 слота
        var slots = new List<string>();
        foreach (var kv in p.Inventory)
            for (int i = 0; i < kv.Value; i++) slots.Add(kv.Key);
        int slotSize = 92, gap = 8;
        int startX = 24, startY = 120;
        for (int i = 0; i < 32; i++)
        {
            string? key = i < slots.Count ? slots[i] : null;
            int col = i % 8, row = i / 8;
            var slot = UiKit.Slot(parent, startX + col * (slotSize + gap),
                startY + row * (slotSize + gap), slotSize, _t, "Slot" + i);
            if (key != null)
            {
                var def = ItemData.GetItemData(key);
                var name = UiKit.Label(slot.transform, 0, 0, slotSize, slotSize,
                    def.Name, _t.TinySize, ItemData.DisplayColor(key).U(), _t,
                    TextAnchor.MiddleCenter, "Item");
                int captured = i;
                var btn = slot.AddComponent<Button>();
                btn.onClick.AddListener(() => ShowActions(key, captured));
                AddHover(slot, def, _t);
            }
        }

        // Экипировка
        int ex = 820;
        UiKit.Label(parent, ex, 120, 400, 24, "ЭКИПИРОВКА", _t.SmallSize, _t.GoldSoft, _t,
            TextAnchor.UpperLeft, "EqTitle");
        string[] eqNames = { "Оружие", "Броня", "Шлем", "Ноги", "Обувь" };
        string?[] eqKeys = { p.Weapon, p.Armor, p.Head, p.Legs, p.Boots };
        int ey = 150;
        for (int i = 0; i < eqKeys.Length; i++)
        {
            string? k = eqKeys[i];
            UiKit.Label(parent, ex, ey + i * 46, 110, 22, eqNames[i], _t.SmallSize,
                _t.InkDim, _t, TextAnchor.UpperLeft, "EqN" + i);
            var val = UiKit.Label(parent, ex + 120, ey + i * 46, 240, 22,
                k != null ? ItemData.DisplayName(k) : "—", _t.SmallSize,
                k != null ? ItemData.DisplayColor(k).U() : _t.InkFaint, _t,
                TextAnchor.UpperLeft, "EqV" + i);
            if (k != null)
            {
                string captured = k;
                var btn = val.gameObject.AddComponent<Button>();
                btn.onClick.AddListener(() =>
                    _pendingAction = (null, captured, 0, false));
            }
        }

        // Кнопки страниц и закрытия
        int by = 120 + 4 * (slotSize + gap) + 20;
        var prev = UiKit.WidgetButton(parent, 24, by, 200, 44, "← Страница",
            _t.SmallSize, _t);
        prev.onClick.AddListener(() => _pendingAction = (null, null, -1, false));
        var next = UiKit.WidgetButton(parent, 236, by, 200, 44, "Страница →",
            _t.SmallSize, _t);
        next.onClick.AddListener(() => _pendingAction = (null, null, 1, false));
        var close = UiKit.WidgetButton(parent, 980, by, 260, 44, "Закрыть (Esc)",
            _t.SmallSize, _t);
        close.onClick.AddListener(() => _pendingAction = (null, null, 0, true));

        // Панель действий по выбору
        if (_useSel >= 0 && _selKey != null)
        {
            var def = ItemData.GetItemData(_selKey);
            var actions = UiKit.Panel(parent, 400, 700, 500, 130, _t, "Actions");
            UiKit.Label(actions.transform, 0, 12, 500, 26, def.Name, _t.BodySize,
                _t.Gold, _t, TextAnchor.MiddleCenter, "SelName");
            if (ItemData.IsStackable(def) || ItemData.IsEquipment(def))
            {
                var use = UiKit.WidgetButton(actions.transform, 40, 50, 200, 44,
                    ItemData.IsEquipment(def) ? "Надеть" : "Использовать",
                    _t.SmallSize, _t);
                use.onClick.AddListener(() =>
                {
                    _pendingAction = (_selKey, null, 0, false);
                    _useSel = -1;
                });
            }
            var drop = UiKit.WidgetButton(actions.transform, 260, 50, 200, 44,
                "Отмена", _t.SmallSize, _t);
            drop.onClick.AddListener(() => { _useSel = -1; _sig = ""; });
        }
    }

    private string? _selKey;

    private void ShowActions(string key, int idx)
    {
        _selKey = key;
        _useSel = idx;
        _sig = ""; // перерисовать
    }

    private static void AddHover(GameObject slot, ItemData.Def def, UITheme t)
    {
        var desc = UiKit.Label(slot.transform, -140, 90, 300, 80,
            $"{def.Name}\nЦена: {def.Price}\n{def.Desc}", t.TinySize,
            t.Ink, t, TextAnchor.UpperLeft, "HoverDesc");
        desc.gameObject.SetActive(false);
        var trigger = slot.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        var entry = new UnityEngine.EventSystems.EventTrigger.Entry
        { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
        entry.callback.AddListener(_ => desc.gameObject.SetActive(true));
        trigger.triggers.Add(entry);
        var exit = new UnityEngine.EventSystems.EventTrigger.Entry
        { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => desc.gameObject.SetActive(false));
        trigger.triggers.Add(exit);
    }
}
