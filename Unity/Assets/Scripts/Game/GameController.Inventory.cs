using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Screens = Aethelgard.Systems.ScreenMode;
namespace Aethelgard.Systems;

/// <summary>
/// Инвентарь и использование предметов: ввод панели, хотбар, снятие
/// экипировки. Выделено из GameController (частичный класс).
/// </summary>
public partial class GameController
{
    private void HandleInventoryInput(KeyboardState keys, KeyboardState prevKeys)
    {
        // Page navigation (Q/E), страницы по 100 (сетка 10×10)
        if (keys.IsKeyDown(Keys.Q) && !prevKeys.IsKeyDown(Keys.Q))
            CurrentPage = Math.Max(0, CurrentPage - 1);
        if (keys.IsKeyDown(Keys.E) && !prevKeys.IsKeyDown(Keys.E))
            CurrentPage++;

        // U/V/H/B/N — снять weapon/armor/head/legs/boots
        // (game/_input.py, экран инвентаря)
        var unequips = new (Keys Key, string Slot, string Label)[]
        {
            (Keys.U, "weapon", "Оружие"),
            (Keys.V, "armor", "Броня"),
            (Keys.H, "head", "Шлем"),
            (Keys.B, "legs", "Поножи"),
            (Keys.N, "boots", "Обувь"),
        };
        foreach (var (key, slot, label) in unequips)
        {
            if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key))
            {
                if (Player.UnequipSlot(slot)) ShowMessage($"{label} снято.");
                return;
            }
        }

        // Shift+1–8: назначить предмет в хотбар
        bool shift = keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift);
        if (shift)
        {
            for (int i = 0; i < 8; i++)
            {
                var key = Keys.D1 + i;
                if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key))
                    AssignQuickbarFromInventory(i);
            }
            return;
        }

        // Number keys 1-8: use/equip item in slot
        for (int i = 0; i < 8; i++)
        {
            var key = Keys.D1 + i;
            if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key))
            {
                UseItemByIndex(i);
            }
        }
    }

    private void AssignQuickbarFromInventory(int slot)
    {
        int pageStart = CurrentPage * Config.InventoryPageSize;
        var items = Player.InventoryStacks();
        int idx = pageStart + slot;
        if (idx >= items.Count) return;
        Player.AssignQuickbar(slot, items[idx].Key);
        ShowMessage($"Слот {slot + 1}: {ItemData.DisplayName(items[idx].Key)}");
    }

    public int CurrentPage = 0;

    /// <summary>
    /// Handle input in skills screen mode.
    /// </summary>
    private void UseItemByIndex(int index)
    {
        int pageStart = CurrentPage * Config.InventoryPageSize;
        var items = Player.InventoryStacks();
        int idx = pageStart + index;
        if (idx >= items.Count) return;
        UseItemByKey(items[idx].Key);
    }

    /// <summary>Клик по предмету в инвентаре (game/_input.py: use_item(key)).</summary>
    public void UseInventoryItem(string key)
    {
        UseItemByKey(key);
        CurrentPage = Math.Min(CurrentPage, InventoryPagesCount() - 1);
    }

    /// <summary>Клик по ячейке экипировки — снять предмет.</summary>
    public void UnequipInventorySlot(string slot)
    {
        if (Player.UnequipSlot(slot))
            ShowMessage("Предмет снят.");
    }

    public void ChangeInventoryPage(int delta)
    {
        if (delta < 0) CurrentPage = Math.Max(0, CurrentPage - 1);
        else if (delta > 0)
            CurrentPage = Math.Min(InventoryPagesCount() - 1, CurrentPage + 1);
    }

    public int InventoryPagesCount()
    {
        int n = Player.InventoryStacks().Count;
        return Math.Max(1, (n + Config.InventoryPageSize - 1) / Config.InventoryPageSize);
    }

    private void UseItemByKey(string key)
    {
        var def = ItemData.Get(ItemData.ParseItemKey(key).Base);
        if (def == null) return;

        switch (def.Kind)
        {
            case ItemData.ItemType.Weapon:
            case ItemData.ItemType.Armor:
            case ItemData.ItemType.Helmet:
            case ItemData.ItemType.Leggings:
            case ItemData.ItemType.Boots:
                if (Player.EquipItem(key))
                    ShowMessage($"Надето: {ItemData.DisplayName(key)}");
                break;
            case ItemData.ItemType.Scroll:
            case ItemData.ItemType.Rune:
                if (Player.UseItem(key, CurrentMap))
                {
                    ShowMessage($"Применено: {def.Name}");
                    GameChat.Instance.Item($"Применено: {def.Name}");
                }
                else
                    ShowMessage($"Недостаточно маны ({def.ManaCost} MP).");
                break;
            case ItemData.ItemType.Blueprint:
                if (Player.UseItem(key))
                    ShowMessage($"Изучен чертёж: {def.Name}");
                else
                    ShowMessage("Уже известно.");
                break;
            default:
                if (Player.UseItem(key, CurrentMap))
                {
                    ShowMessage($"Использовано: {def.Name}");
                    GameChat.Instance.Item($"Использовано: {def.Name}");
                    if (key.StartsWith("potion")) PotionsUsed++;
                }
                break;
        }
        // collect-квесты проверяем после любого использования
        Quests.UpdateCollect(key, Player.Inventory);
    }

    /// <summary>
    /// Try to move player by (dx, dy).
    /// Stairs NO LONGER auto-trigger — press F to use them.
    /// Boss arena: player locked inside while boss is alive.
    /// </summary>
}
