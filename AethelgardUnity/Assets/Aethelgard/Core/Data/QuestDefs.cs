// ═══════════════════════════════════════════════════════════════════
// АВТОГЕНЕРАЦИЯ: scripts/embed_tables.py (перенос data/quests.json в код).
// Данные живут в классах; правьте здесь, а не в JSON.
// ═══════════════════════════════════════════════════════════════════

using System.Collections.Generic;

namespace Aethelgard.Data;

/// <summary>Данные квестов (перенесены из data/quests.json).</summary>
internal static class QuestDefs
{
    public static readonly Dictionary<string, QuestData.Def> Quests = new()
    {
        ["q_rats"] = new QuestData.Def { Key = "q_rats", Name = "Нашествие крыс", Desc = "Убей 5 крыс в открытом мире или подземелье.", Type = "kill", Target = "r", Count = 5, RewardGold = 20, RewardXp = 15, RewardItems = new(), Giver = "elder", DepthMin = 0, DepthMax = 0, Generated = false },
        ["q_bones"] = new QuestData.Def { Key = "q_bones", Name = "Кости для старейшины", Desc = "Принеси 3 кости скелета.", Type = "collect", Target = "bone", Count = 3, RewardGold = 30, RewardXp = 25, RewardItems = new Dictionary<string, int> { ["potion_m"] = 1 }, Giver = "elder", DepthMin = 0, DepthMax = 0, Generated = false },
        ["q_first_step"] = new QuestData.Def { Key = "q_first_step", Name = "Первый шаг", Desc = "Спустись на глубину 5.", Type = "reach", Target = "5", Count = 1, RewardGold = 50, RewardXp = 50, RewardItems = new Dictionary<string, int> { ["sword"] = 1 }, Giver = "elder", DepthMin = 0, DepthMax = 0, Generated = false },
        ["q_goblin_king"] = new QuestData.Def { Key = "q_goblin_king", Name = "Голова короля", Desc = "Убей Короля гоблинов на глубине 5.", Type = "kill", Target = "G", Count = 1, RewardGold = 100, RewardXp = 100, RewardItems = new Dictionary<string, int> { ["book_str"] = 1 }, Giver = "merchant", DepthMin = 0, DepthMax = 0, Generated = false },
        ["q_craft_bone"] = new QuestData.Def { Key = "q_craft_bone", Name = "Кузнец из тебя", Desc = "Создай костяной меч (2 кости + 1 дерево).", Type = "craft", Target = "bone_sword", Count = 1, RewardGold = 40, RewardXp = 30, RewardItems = new(), Giver = "merchant", DepthMin = 0, DepthMax = 0, Generated = false },
        ["q_dragon"] = new QuestData.Def { Key = "q_dragon", Name = "Убить дракона", Desc = "Победи Древнего дракона на глубине 20.", Type = "kill", Target = "A", Count = 1, RewardGold = 500, RewardXp = 1000, RewardItems = new Dictionary<string, int> { ["book_double"] = 1 }, Giver = "merchant", DepthMin = 0, DepthMax = 0, Generated = false },
    };
}
