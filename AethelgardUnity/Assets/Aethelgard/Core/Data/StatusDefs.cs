// ═══════════════════════════════════════════════════════════════════
// АВТОГЕНЕРАЦИЯ: scripts/embed_tables.py (перенос data/statuses.json в код).
// Данные живут в классах; правьте здесь, а не в JSON.
// ═══════════════════════════════════════════════════════════════════

using Microsoft.Xna.Framework;

namespace Aethelgard.Data;

/// <summary>Данные статусов (перенесены из data/statuses.json).</summary>
internal static class StatusDefs
{
    public static readonly Dictionary<string, StatusData.Def> All = new()
    {
        ["poison"] = new StatusData.Def { Key = "poison", Name = "Отравление", Short = "PSN", Color = new Color(120,200,100), Damage = 1, Heal = 0, Duration = 3 },
        ["burning"] = new StatusData.Def { Key = "burning", Name = "Горение", Short = "BRN", Color = new Color(255,100,50), Damage = 3, Heal = 0, Duration = 3 },
        ["bleeding"] = new StatusData.Def { Key = "bleeding", Name = "Кровотечение", Short = "BLD", Color = new Color(200,30,30), Damage = 2, Heal = 0, Duration = 4 },
        ["slow"] = new StatusData.Def { Key = "slow", Name = "Замедление", Short = "SLW", Color = new Color(100,150,255), Damage = 0, Heal = 0, Duration = 2 },
        ["regen"] = new StatusData.Def { Key = "regen", Name = "Регенерация", Short = "RGN", Color = new Color(120,240,120), Damage = 0, Heal = 2, Duration = 4 },
        ["strength"] = new StatusData.Def { Key = "strength", Name = "Сила", Short = "STR", Color = new Color(240,120,90), Damage = 0, Heal = 0, Duration = 20 },
        ["agility"] = new StatusData.Def { Key = "agility", Name = "Ловкость", Short = "AGI", Color = new Color(140,230,120), Damage = 0, Heal = 0, Duration = 20 },
        ["stone_skin"] = new StatusData.Def { Key = "stone_skin", Name = "Каменная кожа", Short = "STN", Color = new Color(170,170,160), Damage = 0, Heal = 0, Duration = 20 },
        ["speed"] = new StatusData.Def { Key = "speed", Name = "Скорость", Short = "SPD", Color = new Color(150,220,250), Damage = 0, Heal = 0, Duration = 20 },
        ["dexterity"] = new StatusData.Def { Key = "dexterity", Name = "Проворность", Short = "DEX", Color = new Color(250,210,120), Damage = 0, Heal = 0, Duration = 20 },
    };

    public static readonly Dictionary<string, Dictionary<string, double>> Mods = new()
    {
        ["strength"] = new Dictionary<string, double> { ["atk"] = 3 },
        ["agility"] = new Dictionary<string, double> { ["dodge"] = 0.08 },
        ["stone_skin"] = new Dictionary<string, double> { ["df"] = 3 },
        ["speed"] = new Dictionary<string, double> { ["move_speed"] = 0.25 },
        ["dexterity"] = new Dictionary<string, double> { ["attack_speed"] = 0.2 },
    };

    public static readonly Dictionary<string, string> AbilityToStatus = new()
    {
        ["poison"] = "poison",
        ["burning"] = "burning",
        ["bleeding"] = "bleeding",
        ["slow"] = "slow",
    };
}
