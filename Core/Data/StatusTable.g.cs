using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Aethelgard.Core;

public static partial class StatusData
{
    public static readonly Dictionary<string, Def> All = new()
    {
        ["poison"] = new() { Key = "poison", Name = "Отравление", Short = "PSN", Color = new Color(120, 200, 100), Damage = 1, Heal = 0, Duration = 3 },
        ["burning"] = new() { Key = "burning", Name = "Горение", Short = "BRN", Color = new Color(255, 100, 50), Damage = 3, Heal = 0, Duration = 3 },
        ["bleeding"] = new() { Key = "bleeding", Name = "Кровотечение", Short = "BLD", Color = new Color(200, 30, 30), Damage = 2, Heal = 0, Duration = 4 },
        ["slow"] = new() { Key = "slow", Name = "Замедление", Short = "SLW", Color = new Color(100, 150, 255), Damage = 0, Heal = 0, Duration = 2 },
        ["regen"] = new() { Key = "regen", Name = "Регенерация", Short = "RGN", Color = new Color(120, 240, 120), Damage = 0, Heal = 2, Duration = 4 },
        ["strength"] = new() { Key = "strength", Name = "Сила", Short = "STR", Color = new Color(240, 120, 90), Damage = 0, Heal = 0, Duration = 20 },
        ["agility"] = new() { Key = "agility", Name = "Ловкость", Short = "AGI", Color = new Color(140, 230, 120), Damage = 0, Heal = 0, Duration = 20 },
        ["stone_skin"] = new() { Key = "stone_skin", Name = "Каменная кожа", Short = "STN", Color = new Color(170, 170, 160), Damage = 0, Heal = 0, Duration = 20 },
        ["speed"] = new() { Key = "speed", Name = "Скорость", Short = "SPD", Color = new Color(150, 220, 250), Damage = 0, Heal = 0, Duration = 20 },
        ["dexterity"] = new() { Key = "dexterity", Name = "Проворность", Short = "DEX", Color = new Color(250, 210, 120), Damage = 0, Heal = 0, Duration = 20 },
    };

    public static readonly Dictionary<string, Dictionary<string, double>> Mods = new()
    {
        ["strength"] = new() { ["atk"] = 3 },
        ["agility"] = new() { ["dodge"] = 0.08 },
        ["stone_skin"] = new() { ["df"] = 3 },
        ["speed"] = new() { ["move_speed"] = 0.25 },
        ["dexterity"] = new() { ["attack_speed"] = 0.2 },
    };

    public static readonly Dictionary<string, string> AbilityToStatus = new()
    {
        ["poison"] = "poison",
        ["burning"] = "burning",
        ["bleeding"] = "bleeding",
        ["slow"] = "slow",
    };
}
