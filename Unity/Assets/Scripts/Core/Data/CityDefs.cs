// ═══════════════════════════════════════════════════════════════════
// АВТОГЕНЕРАЦИЯ: scripts/embed_tables.py (перенос data/city.json в код).
// Данные живут в классах; правьте здесь, а не в JSON.
// ═══════════════════════════════════════════════════════════════════

using Microsoft.Xna.Framework;

namespace Aethelgard.Data;

/// <summary>Данные города (перенесены из data/city.json).</summary>
internal static class CityDefs
{
    public static readonly Dictionary<string, CityData.NpcDef> Npcs = new()
    {
        ["guard"] = new CityData.NpcDef { Key = "guard", Name = "Стражник", Ch = "G", Color = new Color(196,200,220), IsGuard = true, IsShop = false },
        ["blacksmith"] = new CityData.NpcDef { Key = "blacksmith", Name = "Кузнец", Ch = "K", Color = new Color(206,152,88), IsGuard = false, IsShop = true },
        ["alchemist"] = new CityData.NpcDef { Key = "alchemist", Name = "Алхимик", Ch = "A", Color = new Color(156,126,224), IsGuard = false, IsShop = true },
        ["innkeeper"] = new CityData.NpcDef { Key = "innkeeper", Name = "Трактирщик", Ch = "I", Color = new Color(214,172,112), IsGuard = false, IsShop = true },
    };

    public static readonly Dictionary<string, (int X, int Y)> NpcPositions = new()
    {
        ["guard"] = (50, 44),
        ["blacksmith"] = (40, 32),
        ["alchemist"] = (60, 32),
        ["innkeeper"] = (48, 31),
    };

    public static readonly Dictionary<string, (string Key, int Price)[]> CityShopStock = new()
    {
        ["blacksmith"] = new[] { ("iron_ingot", 12), ("steel_ingot", 22), ("mithril_ingot", 40), ("ore", 10), ("wood", 4), ("stone", 3) },
        ["alchemist"] = new[] { ("potion_s", 8), ("potion_m", 16), ("potion_l", 30), ("mana_s", 10), ("mana_m", 22), ("magic_powder", 18) },
        ["innkeeper"] = new[] { ("fried_mushrooms", 6), ("berry_compote", 8), ("roast_venison", 12), ("roast_boar", 16), ("roast_bear", 22) },
    };

    public static readonly Dictionary<string, string> NpcNames = new()
    {
        ["guard"] = "Стражник",
        ["blacksmith"] = "Кузнец",
        ["alchemist"] = "Алхимик",
        ["innkeeper"] = "Трактирщик",
    };

    public static readonly Dictionary<string, (string Ch, Color Color)> NpcGlyphs = new()
    {
        ["guard"] = ("G", new Color(196,200,220)),
        ["blacksmith"] = ("K", new Color(206,152,88)),
        ["alchemist"] = ("A", new Color(156,126,224)),
        ["innkeeper"] = ("I", new Color(214,172,112)),
    };

    public static readonly Dictionary<string, string> NpcShops = new()
    {
        ["blacksmith"] = "Кузнец",
        ["alchemist"] = "Алхимик",
        ["innkeeper"] = "Трактирщик",
    };

    public static readonly (int X, int Y) QuestBoardPos = (48, 37);
    public static readonly (int X, int Y) ElderPos = (52, 35);
}
