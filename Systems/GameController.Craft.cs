using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Screens = Aethelgard.Systems.ScreenMode;
namespace Aethelgard.Systems;

/// <summary>
/// Крафт: список рецептов, мини-игра точности, партии [ ].
/// Выделено из GameController (частичный класс).
/// </summary>
public partial class GameController
{
    private void HandleCraftInput(KeyboardState keys, KeyboardState prevKeys)
    {
        if (MinigameActive)
        {
            // Space — остановить индикатор точности
            if (keys.IsKeyDown(Keys.Space) && !prevKeys.IsKeyDown(Keys.Space))
                FinishMinigame();
            if (keys.IsKeyDown(Keys.Escape) && !prevKeys.IsKeyDown(Keys.Escape))
                MinigameActive = false;
            // [ / ] во время мини-игры — вариант рецепта (мульти-рецепты,
            // напр. magic_powder из кристаллов разного размера)
            int variants = ItemData.RecipesOf(MinigameItem).Count;
            if (variants > 1)
            {
                if (keys.IsKeyDown(Keys.OemOpenBrackets) && !prevKeys.IsKeyDown(Keys.OemOpenBrackets))
                    MinigameRecipeIdx = (MinigameRecipeIdx + variants - 1) % variants;
                if (keys.IsKeyDown(Keys.OemCloseBrackets) && !prevKeys.IsKeyDown(Keys.OemCloseBrackets))
                    MinigameRecipeIdx = (MinigameRecipeIdx + 1) % variants;
            }
            return;
        }

        // Tabs: Q/A — назад, E/D — вперёд (4 вкладки: all + 3 ветки)
        if ((keys.IsKeyDown(Keys.Q) && !prevKeys.IsKeyDown(Keys.Q))
            || (keys.IsKeyDown(Keys.A) && !prevKeys.IsKeyDown(Keys.A)))
            CraftTab = (CraftTab + 3) % 4;
        if ((keys.IsKeyDown(Keys.E) && !prevKeys.IsKeyDown(Keys.E))
            || (keys.IsKeyDown(Keys.D) && !prevKeys.IsKeyDown(Keys.D)))
            CraftTab = (CraftTab + 1) % 4;

        // Pages: Left/Right
        if (keys.IsKeyDown(Keys.Left) && !prevKeys.IsKeyDown(Keys.Left))
            CraftPage = Math.Max(0, CraftPage - 1);
        if (keys.IsKeyDown(Keys.Right) && !prevKeys.IsKeyDown(Keys.Right))
            CraftPage++;

        // Batch size [ / ]
        if (keys.IsKeyDown(Keys.OemOpenBrackets) && !prevKeys.IsKeyDown(Keys.OemOpenBrackets))
            CraftQty = Math.Max(1, CraftQty - 1);
        if (keys.IsKeyDown(Keys.OemCloseBrackets) && !prevKeys.IsKeyDown(Keys.OemCloseBrackets))
            CraftQty = Math.Min(9, CraftQty + 1);

        // 1–8: начать крафт (мини-игра)
        for (int i = 0; i < 8; i++)
        {
            var key = Keys.D1 + i;
            if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key))
                StartCraft(i);
        }

        if (keys.IsKeyDown(Keys.K) && !prevKeys.IsKeyDown(Keys.K)
            || keys.IsKeyDown(Keys.Escape) && !prevKeys.IsKeyDown(Keys.Escape))
            ScreenMode = Screens.Game;
    }

    public void StartCraft(int index)
    {
        var list = CraftableList();
        int idx = CraftPage * Config.CraftPageSize + index;
        if (index < 0 || idx >= list.Count) return;
        var (key, def) = list[idx];

        var branch = ItemData.CategoryOf(key);
        int branchLevel = Player.CraftLevels.GetValueOrDefault(branch, 1);
        if (Player.Level < def.LevelReq)
        {
            ShowMessage($"Требуется уровень персонажа: {def.LevelReq}.");
            return;
        }
        if (def.SkillReq > branchLevel)
        {
            ShowMessage($"Требуется уровень ветки «{branch}»: {def.SkillReq}.");
            return;
        }
        if (!string.IsNullOrEmpty(def.BlueprintRequired)
            && !Player.Blueprints.Contains(def.BlueprintRequired))
        {
            ShowMessage("Требуется чертёж.");
            return;
        }

        // Скорость мини-игры: 0.85 + skill_req*0.02 + (qty-1)*0.04, max 2.4
        MinigameSpeed = Math.Min(2.4f,
            0.85f + def.SkillReq * 0.02f + (CraftQty - 1) * 0.04f);
        MinigamePos = 0.5f;
        MinigameDir = 1f;
        MinigameItem = key;
        // Автовыбор первого ДОСТУПНОГО варианта рецепта (у magic_powder их 3 —
        // из кристаллов разного размера). Раньше хардкод 0 ломал крафт, если
        // материалов первого варианта нет, и не давал выбрать альтернативу
        // ([ / ] во время мини-игры переключают вариант).
        var (_, _, recipeIdx) = ItemData.SelectRecipe(key, Player.Inventory, null);
        MinigameRecipeIdx = Math.Max(0, recipeIdx);
        MinigameActive = true;
    }

    /// <summary>Тик мини-игры — двигает индикатор туда-обратно.</summary>
    public void UpdateMinigame(float dt)
    {
        if (!MinigameActive) return;
        MinigamePos += MinigameDir * MinigameSpeed * dt;
        if (MinigamePos >= 1f) { MinigamePos = 1f; MinigameDir = -1f; }
        if (MinigamePos <= 0f) { MinigamePos = 0f; MinigameDir = 1f; }
    }

    /// <summary>Остановить мини-игру и выполнить крафт с вычисленной точностью.</summary>
    public void FinishMinigame()
    {
        if (!MinigameActive || MinigameItem == null) return;
        MinigameActive = false;

        var def = ItemData.Get(MinigameItem);
        int skillReq = def?.SkillReq ?? 1;
        double accuracy = 1.0 - Math.Abs(MinigamePos - 0.5) * 2.0
            - skillReq * 0.008
            - Math.Min(0.30, (CraftQty - 1) * 0.03);
        accuracy = Math.Max(0.0, accuracy);

        var result = Rules.CraftItem(Player, MinigameItem, CraftQty, accuracy,
            MinigameRecipeIdx);
        ShowMessage(result.Message);
        if (result.Ok)
        {
            ItemsCrafted += 1;
            GameChat.Instance.Item(
                $"Создано: {ItemData.DisplayName(MinigameItem)} ×{CraftQty} (качество +{result.Quality})");
            Quests.UpdateCraft(MinigameItem);
            Quests.UpdateCraft(ItemData.MakeLootKey(MinigameItem, 0));
            Audio?.Play("pickup");
        }
    }

    // ── Сбор ресурсов (E) ──

    /// <summary>Добыть ресурс рядом с игроком или под курсором (E).</summary>
}
