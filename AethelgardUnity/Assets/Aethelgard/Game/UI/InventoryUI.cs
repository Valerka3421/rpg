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
        // Модальное окно python-final (_base.py): scrim + панель + крестик
        UiKit.Scrim(Root, _t);
        _panel = UiKit.Panel(Root, 240, 100, 1440, 880, _t, "InvPanel");
        var parent = _panel.transform;

        UiKit.Label(parent, 0, 20, 1440, 44, "ИНВЕНТАРЬ", _t.H1Size, _t.Title, _t,
            TextAnchor.MiddleCenter, "Title");
        UiKit.CloseButton(parent, 1440 - 10, 10, () => _pendingAction = (null, null, 0, true), _t);
        UiKit.Label(parent, 24, 74, 400, 24,
            $"Золото: {_game.Player!.Gold}    Стр. {_game.CurrentPage + 1}/{_game.InventoryPagesCount()}",
            _t.SmallSize, _t.Gold, _t, TextAnchor.UpperLeft, "Gold");

        // Сетка 10×10 (python-final): ячейка 64, gap 4
        var slots = new List<string>();
        foreach (var kv in p.Inventory)
            for (int i = 0; i < kv.Value; i++) slots.Add(kv.Key);
        int slotSize = 64, gap = 4, cols = 10;
        int startX = 24, startY = 110;
        for (int i = 0; i < 100; i++)
        {
            string? key = i < slots.Count ? slots[i] : null;
            int col = i % cols, row = i / cols;
            var slot = UiKit.Slot(parent, startX + col * (slotSize + gap),
                startY + row * (slotSize + gap), slotSize, _t, "Slot" + i);
            UiKit.Frame((RectTransform)slot.transform,
                key != null ? _t.PanelBorder : _t.SlotBorderEmpty, 1);
            if (key != null)
            {
                var def = ItemData.GetItemData(key);
                var name = UiKit.Label(slot.transform, 0, 0, slotSize, slotSize,
                    def.Name, 11, ItemData.DisplayColor(key).U(), _t,
                    TextAnchor.MiddleCenter, "Item");
                int captured = i;
                var btn = slot.AddComponent<Button>();
                btn.onClick.AddListener(() => ShowActions(key, captured));
                AddHover(slot, def, _t);
            }
        }

        // Экипировка — левая колонка (36% ширины в python)
        int ex = startX + cols * (slotSize + gap) + 40;
        UiKit.Label(parent, ex, 110, 380, 24, "ЭКИПИРОВКА", _t.SmallSize, _t.Section, _t,
            TextAnchor.UpperLeft, "EqTitle");
        string[] eqNames = { "Оружие", "Броня", "Шлем", "Ноги", "Обувь" };
        string?[] eqKeys = { p.Weapon, p.Armor, p.Head, p.Legs, p.Boots };
        int ey = 146;
        for (int i = 0; i < eqKeys.Length; i++)
        {
            string? k = eqKeys[i];
            UiKit.Label(parent, ex, ey + i * 46, 110, 22, eqNames[i], _t.SmallSize,
                _t.TextDim, _t, TextAnchor.UpperLeft, "EqN" + i);
            var val = UiKit.Label(parent, ex + 120, ey + i * 46, 260, 22,
                k != null ? ItemData.DisplayName(k) : "—", _t.SmallSize,
                k != null ? ItemData.DisplayColor(k).U() : _t.TextMuted, _t,
                TextAnchor.UpperLeft, "EqV" + i);
            if (k != null)
            {
                string captured = k;
                var btn = val.gameObject.AddComponent<Button>();
                btn.onClick.AddListener(() =>
                    _pendingAction = (null, captured, 0, false));
            }
        }

        // Кнопки страниц «‹ ›» 38×28 (python) + закрытие
        int by = startY + 10 * (slotSize + gap) + 16;
        var prev = UiKit.WidgetButton(parent, startX, by, 38, 28, "‹",
            _t.BodySize, _t);
        prev.onClick.AddListener(() => _pendingAction = (null, null, -1, false));
        var next = UiKit.WidgetButton(parent, startX + 50, by, 38, 28, "›",
            _t.BodySize, _t);
        next.onClick.AddListener(() => _pendingAction = (null, null, 1, false));

        // Панель действий по выбору
        if (_useSel >= 0 && _selKey != null)
        {
            var def = ItemData.GetItemData(_selKey);
            var actions = UiKit.Panel(parent, ex - 20, 420, 460, 130, _t, "Actions");
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
