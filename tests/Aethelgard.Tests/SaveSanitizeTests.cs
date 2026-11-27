using System.Collections.Generic;
using Aethelgard.Entities;
using Aethelgard.Systems;
using Aethelgard.World;
using Xunit;

namespace Aethelgard.Tests;

/// <summary>
/// Битые и подделанные сейвы не должны крашить игру: мусорный JSON не
/// десериализуется, ApplyLoad клампит значения и фильтрует неизвестные
/// предметы (SanitizePlayerData).
/// </summary>
public class SaveSanitizeTests
{
    [Fact]
    public void Deserialize_BrokenJson_Throws()
    {
        Assert.ThrowsAny<Exception>(() => SaveSystem.Deserialize("{not json at all"));
        Assert.ThrowsAny<Exception>(() => SaveSystem.Deserialize(""));
    }

    [Fact]
    public void ApplyLoad_ClampsBrokenValues_AndFiltersUnknownItems()
    {
        var game = new GameController();
        game.NewGame("warrior");

        var data = new SaveSystem.SaveData
        {
            Player = new SaveSystem.PlayerData
            {
                Level = 500,             // → клампится к 200
                Gold = -50,              // → 0
                Hp = 99999, MaxHp = 30,  // → 30
                Mana = -5,               // → 0
                X = -100, Y = -100,      // → внутрь карты
                Inventory = new Dictionary<string, int>
                {
                    ["totally_unknown_item"] = 5,   // → отфильтрован
                    ["potion_s"] = 3,               // → остаётся
                },
                // Hotbar/CraftLevels/LearnedNodes = null → пустые коллекции
            },
            World = new SaveSystem.WorldData { Mode = "overworld" },
            Quests = new SaveSystem.QuestDataSave(),
        };

        var json = SaveSystem.Serialize(data);
        var loaded = SaveSystem.Deserialize(json);
        Assert.NotNull(loaded);
        Assert.True(SaveSystem.ApplyLoad(game, loaded!));

        var p = game.Player;
        Assert.Equal(200, p.Level);
        Assert.Equal(0, p.Gold);
        // Recalc после загрузки восстанавливает HP/MP до максимумов —
        // важно лишь отсутствие отрицательных значений
        Assert.True(p.Hp >= 0 && p.Hp <= p.MaxHp);
        Assert.True(p.Mana >= 0 && p.Mana <= p.MaxMana);
        Assert.InRange(p.X, 1, game.CurrentMap.W - 2);
        Assert.InRange(p.Y, 1, game.CurrentMap.H - 2);
        Assert.False(p.Inventory.ContainsKey("totally_unknown_item"));
        Assert.Equal(3, p.Inventory.GetValueOrDefault("potion_s"));
    }
}
