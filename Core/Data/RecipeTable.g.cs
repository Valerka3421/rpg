using System;
using System.Collections.Generic;

namespace Aethelgard.Core;

public static partial class RecipeData
{
    public static readonly Dictionary<string, SkillDef> Skills = new()
    {
        ["blacksmith"] = new() { Key = "blacksmith", Name = "Кузнечное дело", Desc = "Слитки, оружие и броня." },
        ["alchemy"] = new() { Key = "alchemy", Name = "Алхимия", Desc = "Зелья и магический порошок." },
        ["magic_engineering"] = new() { Key = "magic_engineering", Name = "Магическая инженерия", Desc = "Свитки и руны." },
    };

    public static readonly string[] SkillOrder = { "blacksmith", "alchemy", "magic_engineering" };
    public static readonly (string Key, string Name)[] CraftTabs = { ("all", "Все крафты"), ("blacksmith", "Кузнечное дело"), ("alchemy", "Алхимия"), ("magic_engineering", "Магическая инженерия") };
}
