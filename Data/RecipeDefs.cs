// ═══════════════════════════════════════════════════════════════════
// АВТОГЕНЕРАЦИЯ: scripts/embed_tables.py (перенос data/recipes.json в код).
// Данные живут в классах; правьте здесь, а не в JSON.
// ═══════════════════════════════════════════════════════════════════

namespace Aethelgard.Data;

/// <summary>Данные ремёсел (перенесены из data/recipes.json).</summary>
internal static class RecipeDefs
{
    public static readonly Dictionary<string, RecipeData.SkillDef> Skills = new()
    {
        ["blacksmith"] = new RecipeData.SkillDef { Key = "blacksmith", Name = "Кузнечное дело", Desc = "Слитки, оружие и броня." },
        ["alchemy"] = new RecipeData.SkillDef { Key = "alchemy", Name = "Алхимия", Desc = "Зелья и магический порошок." },
        ["magic_engineering"] = new RecipeData.SkillDef { Key = "magic_engineering", Name = "Магическая инженерия", Desc = "Свитки и руны." },
    };

    public static readonly string[] SkillOrder = new[] { "blacksmith", "alchemy", "magic_engineering" };

    public static readonly (string Key, string Name)[] CraftTabs = new[]
    {
        ("all", "Все"),
        ("blacksmith", "Кузнечное"),
        ("alchemy", "Алхимия"),
        ("magic_engineering", "Инженерия"),
    };
}
