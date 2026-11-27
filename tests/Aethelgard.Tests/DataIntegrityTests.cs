using System;
using System.Collections.Generic;
using System.Linq;
using Aethelgard.Data;
using Xunit;

namespace Aethelgard.Tests;

/// <summary>
/// Валидатор ссылочной целостности данных. Раньше 7 типов оружия
/// (knife/rapier/katana/spear/halberd/crossbow/fist) не имели ни одного
/// предмета, а pickpocket/tracker стартовали с «оружием-призраком» —
/// всё это проходило компиляцию и падало только в рантайме у игрока.
/// Эти тесты не дают данным разъехаться снова.
/// </summary>
public class DataIntegrityTests
{
    private static Dictionary<string, ItemData.Def> Items => ItemData.All;

    private static bool KnownKey(string key)
    {
        if (Items.ContainsKey(key)) return true;
        var (baseKey, _) = ItemData.ParseItemKey(key);
        return baseKey != null && baseKey != key && Items.ContainsKey(baseKey);
    }

    [Fact]
    public void EveryClassWeaponType_HasAtLeastOneItem()
    {
        var itemWTypes = Items.Values
            .Where(d => !string.IsNullOrEmpty(d.WType))
            .Select(d => d.WType!)
            .ToHashSet();

        foreach (var cls in ClassDefs.All.Values)
        {
            foreach (var wt in cls.Weapons)
                Assert.True(itemWTypes.Contains(wt),
                    $"класс '{cls.Key}' допускает оружие '{wt}', но предметов такого WType нет в ItemData");
        }
    }

    [Fact]
    public void EveryClassStartWeapon_Exists()
    {
        foreach (var cls in ClassDefs.All.Values)
        {
            if (string.IsNullOrEmpty(cls.StartWeapon)) continue;
            Assert.True(Items.ContainsKey(cls.StartWeapon),
                $"класс '{cls.Key}' стартует с несуществующего оружия '{cls.StartWeapon}'");
        }
    }

    [Fact]
    public void EveryClassStartArmor_AndItems_Exist()
    {
        foreach (var cls in ClassDefs.All.Values)
        {
            if (!string.IsNullOrEmpty(cls.StartArmor))
                Assert.True(Items.ContainsKey(cls.StartArmor),
                    $"класс '{cls.Key}': стартовая броня '{cls.StartArmor}' не существует");
            foreach (var key in cls.StartItems.Keys)
                Assert.True(Items.ContainsKey(key),
                    $"класс '{cls.Key}': стартовый предмет '{key}' не существует");
        }
    }

    [Fact]
    public void EveryRecipeMaterial_Exists()
    {
        foreach (var def in Items.Values)
        {
            if (def.Recipe != null)
                foreach (var mat in def.Recipe.Keys)
                    Assert.True(Items.ContainsKey(mat),
                        $"рецепт '{def.Key}' ссылается на несуществующий материал '{mat}'");
            if (def.Recipes != null)
                foreach (var v in def.Recipes)
                    foreach (var mat in v.Materials.Keys)
                        Assert.True(Items.ContainsKey(mat),
                            $"вариант рецепта '{def.Key}' ссылается на несуществующий материал '{mat}'");
        }
    }

    [Fact]
    public void EveryBlueprintLink_IsConsistent()
    {
        foreach (var def in Items.Values)
        {
            if (!string.IsNullOrEmpty(def.BlueprintRequired))
                Assert.True(Items.ContainsKey(def.BlueprintRequired),
                    $"предмет '{def.Key}' требует несуществующий чертёж '{def.BlueprintRequired}'");
            if (!string.IsNullOrEmpty(def.Teaches))
                Assert.True(Items.ContainsKey(def.Teaches),
                    $"чертёж '{def.Key}' учит крафту несуществующего предмета '{def.Teaches}'");
        }
    }

    [Fact]
    public void EveryQuestRewardItem_Exists()
    {
        foreach (var q in QuestDefs.Quests.Values)
        {
            foreach (var key in q.RewardItems.Keys)
                Assert.True(Items.ContainsKey(key),
                    $"квест '{q.Key}' награждает несуществующим предметом '{key}'");
            if (q.Type == "craft" && !string.IsNullOrEmpty(q.Target))
                Assert.True(Items.ContainsKey(q.Target),
                    $"квест '{q.Key}' (craft) ссылается на несуществующий предмет '{q.Target}'");
            // collect-цель — предмет, kill-цель — глиф монстра, reach — глубина
            if (q.Type == "collect" && !string.IsNullOrEmpty(q.Target))
                Assert.True(Items.ContainsKey(q.Target),
                    $"квест '{q.Key}' (collect) ссылается на несуществующий предмет '{q.Target}'");
        }
    }

    [Fact]
    public void EveryKillQuestTarget_IsValidMonsterGlyph()
    {
        var glyphs = MonsterData.All.Values.Select(m => m.Ch).ToHashSet();
        foreach (var q in QuestDefs.Quests.Values)
        {
            if (q.Type != "kill" || string.IsNullOrEmpty(q.Target)) continue;
            Assert.True(glyphs.Contains(q.Target),
                $"квест '{q.Key}' (kill) использует глиф '{q.Target}', не принадлежащий ни одному монстру");
        }
    }

    [Fact]
    public void EveryQuestRewardItemTest_PotionRewardKeysExist()
    {
        // Регрессия прошлых фантомов (potion_hp_s и пр.)
        var keys = QuestDefs.Quests.Values.SelectMany(q => q.RewardItems.Keys).ToHashSet();
        foreach (var k in keys)
            Assert.True(KnownKey(k), $"награда квеста '{k}' не существует");
    }
}
