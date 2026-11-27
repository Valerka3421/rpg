using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Screens = Aethelgard.Systems.ScreenMode;
namespace Aethelgard.Systems;

/// <summary>
/// Магазин и доска квестов: ввод панелей, покупка/продажа, реролл.
/// Выделено из GameController (частичный класс).
/// </summary>
public partial class GameController
{
    private void HandleShopInput(KeyboardState keys, KeyboardState prevKeys)
    {
        // S: toggle buy/sell
        if (keys.IsKeyDown(Keys.S) && !prevKeys.IsKeyDown(Keys.S))
        {
            ShopSubMode = ShopSubMode == "buy" ? "sell" : "buy";
            ShopPage = 0;
        }

        // Q/E: pages
        if (keys.IsKeyDown(Keys.Q) && !prevKeys.IsKeyDown(Keys.Q))
            ShopPage = Math.Max(0, ShopPage - 1);
        if (keys.IsKeyDown(Keys.E) && !prevKeys.IsKeyDown(Keys.E))
            ShopPage = Math.Min(ShopPagesCount() - 1, ShopPage + 1);

        // R: reroll — только у торговца подземелья (городские лавки фиксированы)
        if (keys.IsKeyDown(Keys.R) && !prevKeys.IsKeyDown(Keys.R))
            RerollShop();

        // 1-9: buy or sell item (SHOP_PAGE_SIZE = 9)
        for (int i = 0; i < 9; i++)
        {
            var key = Keys.D1 + i;
            if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key))
            {
                if (ShopSubMode == "buy")
                    BuyItem(i);
                else
                    SellItem(i);
            }
        }
    }

    /// <summary>Перегенерация стока торговца подземелья за золото (R).</summary>
    public void RerollShop()
    {
        if (!DungeonShopMode || DungeonMerchant == null) return;
        var map = CurrentMap;
        if (map.MerchantStock == null) return;
        int price = Config.RerollPrice(Math.Max(1, Depth));
        if (Player.Gold < price)
        {
            ShowMessage($"Обновление стоит {price} золота!");
            return;
        }
        Player.Gold -= price;
        map.MerchantStock = RecipeData.MakeShopStock(Math.Max(1, Depth));
        ShopPage = 0;
        ShowMessage($"Ассортимент обновлён (-{price} золота)");
    }

    /// <summary>Переключение покупка/продажа (кнопка режима в магазине).</summary>
    public void ToggleShopMode()
    {
        ShopSubMode = ShopSubMode == "buy" ? "sell" : "buy";
        ShopPage = 0;
    }

    /// <summary>Число страниц магазина — parity ShopController.pages_count.</summary>
    public int ShopPagesCount()
    {
        int n = ShopSubMode == "buy"
            ? CurrentShopStock().Count
            : Player.InventoryStacks().Count;
        return Math.Max(1, (n + Config.ShopPageSize - 1) / Config.ShopPageSize);
    }

    /// <summary>Сток текущего магазина (городской NPC или торговец подземелья).</summary>
    public List<RecipeData.ShopEntry> CurrentShopStock()
    {
        if (DungeonShopMode)
        {
            var map = CurrentMap;
            if (map.MerchantStock == null)
                map.MerchantStock = RecipeData.MakeShopStock(Math.Max(1, Depth));
            return map.MerchantStock;
        }
        var list = new List<RecipeData.ShopEntry>();
        if (ActiveShopNpc != null)
            list = ShopSystem.CityStock(ActiveShopNpc);
        return list;
    }

    /// <summary>
    /// Handle input in quests mode.
    /// </summary>
    private void HandleQuestInput(KeyboardState keys, KeyboardState prevKeys)
    {
        // Up/Down: select quest
        if (keys.IsKeyDown(Keys.Up) && !prevKeys.IsKeyDown(Keys.Up))
            QuestSelected = Math.Max(0, QuestSelected - 1);
        if (keys.IsKeyDown(Keys.Down) && !prevKeys.IsKeyDown(Keys.Down))
            QuestSelected++;

        // Enter: accept or turn in
        if (keys.IsKeyDown(Keys.Enter) && !prevKeys.IsKeyDown(Keys.Enter))
        {
            var available = Quests.Available.ToList();
            var completed = Quests.Completed.ToList();

            if (QuestSelected < available.Count)
                AcceptQuest(QuestSelected);
            else
            {
                int completedIdx = QuestSelected - available.Count;
                if (completedIdx >= 0 && completedIdx < completed.Count)
                    TurnInQuest(completedIdx);
            }
        }
    }

    /// <summary>
    /// Try to learn a skill node by index (0-6).
    /// </summary>
}
