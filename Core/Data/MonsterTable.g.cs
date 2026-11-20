using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Aethelgard.Core;

public static partial class MonsterData
{
    public static readonly Dictionary<string, Def> All = new()
    {
        ["rat"] = new() { Key = "rat", Name = "Крыса", Ch = "r", Hp = 3, Atk = 1, Df = 0, Xp = 3, Color = new Color(170, 150, 130), MinDepth = 1, Ability = "poison", Gold = false },
        ["goblin"] = new() { Key = "goblin", Name = "Гоблин", Ch = "g", Hp = 8, Atk = 3, Df = 1, Xp = 6, Color = new Color(120, 190, 100), MinDepth = 1 },
        ["archer"] = new() { Key = "archer", Name = "Гоблин-лучник", Ch = "a", Hp = 6, Atk = 2, Df = 0, Xp = 7, Color = new Color(150, 200, 100), MinDepth = 2, Ability = "ranged" },
        ["skeleton"] = new() { Key = "skeleton", Name = "Скелет", Ch = "s", Hp = 12, Atk = 4, Df = 1, Xp = 12, Color = new Color(220, 220, 220), MinDepth = 2 },
        ["shaman"] = new() { Key = "shaman", Name = "Гоблин-шаман", Ch = "S", Hp = 10, Atk = 2, Df = 1, Xp = 14, Color = new Color(180, 140, 220), MinDepth = 3, Ability = "healer" },
        ["orc"] = new() { Key = "orc", Name = "Орк", Ch = "O", Hp = 20, Atk = 6, Df = 2, Xp = 22, Color = new Color(150, 95, 60), MinDepth = 3 },
        ["troll"] = new() { Key = "troll", Name = "Тролль", Ch = "T", Hp = 35, Atk = 8, Df = 3, Xp = 40, Color = new Color(85, 140, 85), MinDepth = 4 },
        ["demon"] = new() { Key = "demon", Name = "Демон", Ch = "D", Hp = 50, Atk = 12, Df = 4, Xp = 70, Color = new Color(200, 70, 70), MinDepth = 6, Ability = "burning" },
        ["vampire"] = new() { Key = "vampire", Name = "Вампир", Ch = "v", Hp = 70, Atk = 14, Df = 5, Xp = 100, Color = new Color(180, 50, 80), MinDepth = 8, Ability = "poison" },
        ["dragonling"] = new() { Key = "dragonling", Name = "Дракончик", Ch = "d", Hp = 90, Atk = 16, Df = 5, Xp = 130, Color = new Color(200, 150, 60), MinDepth = 10, Ability = "burning" },
        ["golem"] = new() { Key = "golem", Name = "Голем", Ch = "Y", Hp = 120, Atk = 14, Df = 8, Xp = 150, Color = new Color(130, 130, 140), MinDepth = 12 },
        ["wraith"] = new() { Key = "wraith", Name = "Призрак", Ch = "W", Hp = 80, Atk = 18, Df = 4, Xp = 140, Color = new Color(180, 180, 220), MinDepth = 14, Ability = "bleeding" },
        ["deer"] = new() { Key = "deer", Name = "Олень", Ch = "e", Hp = 8, Atk = 2, Df = 0, Xp = 8, Color = new Color(170, 125, 80), MinDepth = 1, OverworldOnly = true, Passive = true, Flees = true, Gold = false },
        ["wolf"] = new() { Key = "wolf", Name = "Волк", Ch = "w", Hp = 14, Atk = 4, Df = 1, Xp = 12, Color = new Color(130, 135, 145), MinDepth = 1, Ability = "bleeding", OverworldOnly = true, Gold = false },
        ["bear"] = new() { Key = "bear", Name = "Медведь", Ch = "b", Hp = 30, Atk = 7, Df = 2, Xp = 25, Color = new Color(105, 72, 45), MinDepth = 1, OverworldOnly = true, Gold = false },
        ["slime"] = new() { Key = "slime", Name = "Слайм", Ch = "j", Hp = 10, Atk = 2, Df = 1, Xp = 6, Color = new Color(100, 190, 90), MinDepth = 1, Ability = "poison", OverworldOnly = true, Gold = false },
        ["boar"] = new() { Key = "boar", Name = "Кабан", Ch = "p", Hp = 18, Atk = 5, Df = 1, Xp = 15, Color = new Color(120, 100, 80), MinDepth = 1, OverworldOnly = true, Gold = false },
        ["crab"] = new() { Key = "crab", Name = "Краб", Ch = "c", Hp = 6, Atk = 2, Df = 2, Xp = 7, Color = new Color(220, 120, 90), MinDepth = 1, OverworldOnly = true, Gold = false },
        ["murloc"] = new() { Key = "murloc", Name = "Мурлок", Ch = "m", Hp = 11, Atk = 3, Df = 1, Xp = 10, Color = new Color(90, 180, 170), MinDepth = 1, Ability = "poison", OverworldOnly = true, Gold = false },
        ["turtle"] = new() { Key = "turtle", Name = "Черепаха", Ch = "u", Hp = 16, Atk = 3, Df = 3, Xp = 12, Color = new Color(120, 160, 110), MinDepth = 1, OverworldOnly = true, Gold = false },
        ["boss_g_5"] = new() { Key = "boss_g_5", Name = "Король гоблинов", Ch = "G", Hp = 80, Atk = 9, Df = 2, Xp = 150, Color = new Color(255, 100, 100), MinDepth = 5, Boss = true, BossDepth = 5 },
        ["boss_l_10"] = new() { Key = "boss_l_10", Name = "Лич", Ch = "L", Hp = 150, Atk = 12, Df = 4, Xp = 500, Color = new Color(200, 100, 255), MinDepth = 10, Boss = true, BossDepth = 10 },
        ["boss_v_15"] = new() { Key = "boss_v_15", Name = "Владыка вампиров", Ch = "V", Hp = 250, Atk = 18, Df = 6, Xp = 700, Color = new Color(200, 40, 80), MinDepth = 15, Boss = true, BossDepth = 15 },
        ["boss_a_20"] = new() { Key = "boss_a_20", Name = "Древний дракон", Ch = "A", Hp = 400, Atk = 22, Df = 8, Xp = 1200, Color = new Color(240, 130, 40), MinDepth = 20, Boss = true, BossDepth = 20 },
    };

    public static readonly Dictionary<int, string> BossByDepth = new()
    {
        [5] = "boss_g_5",
        [10] = "boss_l_10",
        [15] = "boss_v_15",
        [20] = "boss_a_20",
    };

    public static readonly Dictionary<string, string[]> BossDrops = new()
    {
        ["G"] = new[] { "goblin_ear", "goblin_ear", "bone" },
        ["L"] = new[] { "bone", "bone", "wraith_essence", "wraith_essence" },
        ["V"] = new[] { "vampire_fang", "vampire_fang", "vampire_fang" },
        ["A"] = new[] { "dragon_scale", "dragon_scale", "dragon_scale", "dragon_scale", "dragon_scale" },
    };

    public static readonly Dictionary<string, (string Item, double Chance)[]> MonsterDrops = new()
    {
        ["r"] = new[] { ("rat_tail", 0.5), ("mushroom", 0.1), ("hide", 0.3) },
        ["g"] = new[] { ("goblin_ear", 0.5), ("wood", 0.15), ("hide", 0.3) },
        ["a"] = new[] { ("goblin_ear", 0.45), ("wood", 0.2), ("hide", 0.25) },
        ["s"] = new[] { ("bone", 0.7), ("stone", 0.15) },
        ["S"] = new[] { ("goblin_ear", 0.45), ("mushroom", 0.25) },
        ["O"] = new[] { ("orc_tusk", 0.5), ("ore", 0.15) },
        ["T"] = new[] { ("ogre_leather", 0.5), ("wood", 0.3), ("stone", 0.2), ("hide", 0.3) },
        ["D"] = new[] { ("demon_scale", 0.5), ("ore", 0.2) },
        ["v"] = new[] { ("vampire_fang", 0.5), ("bone", 0.1) },
        ["d"] = new[] { ("dragon_scale", 0.45), ("ore", 0.2) },
        ["Y"] = new[] { ("golem_core", 0.35), ("stone", 0.7), ("ore", 0.2) },
        ["W"] = new[] { ("wraith_essence", 0.5), ("bone", 0.15) },
        ["e"] = new[] { ("venison", 0.7), ("bone", 0.2) },
        ["w"] = new[] { ("wolf_pelt", 0.6), ("venison", 0.2), ("bone", 0.15), ("hide", 0.25) },
        ["b"] = new[] { ("bear_pelt", 0.6), ("bear_claw", 0.5), ("venison", 0.25), ("hide", 0.3) },
        ["j"] = new[] { ("slime_gel", 0.8), ("mushroom", 0.15) },
        ["p"] = new[] { ("boar_tusk", 0.55), ("venison", 0.55), ("bone", 0.1), ("hide", 0.2) },
        ["c"] = new[] { ("venison", 0.45), ("hide", 0.3), ("bone", 0.15) },
        ["m"] = new[] { ("slime_gel", 0.5), ("hide", 0.3), ("mushroom", 0.1) },
        ["u"] = new[] { ("bone", 0.55), ("hide", 0.4), ("stone", 0.1) },
    };

    public static readonly Dictionary<string, string> ManaCrystalBySize = new() { ["small"] = "mana_crystal_small", ["normal"] = "mana_crystal", ["large"] = "mana_crystal_large" };
    public static readonly Dictionary<string, int> ManaPowderByCrystal = new() { ["mana_crystal_small"] = 1, ["mana_crystal"] = 2, ["mana_crystal_large"] = 4 };
}
