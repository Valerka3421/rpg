using System;
    using System.Collections.Generic;
    using System.Linq;
    
    namespace Aethelgard.Systems
    {
    
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Aethelgard.BaseClass;
    using Aethelgard.Entities;
    using Aethelgard.Entities.Monsters;
    using Aethelgard.Structs;
    using Aethelgard.World;
    using Aethelgard.Systems;
    using Aethelgard.Data;
    using Aethelgard.Net;
    using Aethelgard.Audio;
    /// <summary>
    /// Skill tree: class nodes (bought with skill points) + craft branches (leveled by XP).
    /// Ported from Python core/skills.py.
    ///
    /// Structure:
    /// - Class tab: hexagonal tree with 8 nodes per class branch
    ///   Nodes unlocked at levels 1, 5, 9, 13, 17, 21, 25
    ///   Each node gives stat bonuses or perks
    /// - Craft tab: blacksmith / alchemy / magic_engineering
    ///   Leveled 1-25 by crafting XP, unlocks recipes
    /// </summary>
    public static class SkillTree
    {
        /// <summary>
        /// Сумма пассивных эффектов выученных узлов древа. В оригинале
        /// Player.Effects() вызывал SkillTree.Aggregate, но метод не был объявлен.
        /// </summary>
        public static Dictionary<string, double> Aggregate(Player player)
        {
            // Масштаб эффектов по тиру класса (core/skills.py:_TIER_SCALE):
            // T1 ×1.0, T2 ×1.4, T3 ×1.8 — ветка специализации сильнее базовой.
            double factor = player.ClassTier >= 3 ? 1.8 : player.ClassTier == 2 ? 1.4 : 1.0;
            var totals = new Dictionary<string, double>();
            foreach (var node in GetNodes(player))
            {
                if (!player.LearnedNodes.Contains(node.Key)) continue;
                if (node.Effects == null) continue;
                foreach (var (key, value) in node.Effects)
                    totals[key] = totals.GetValueOrDefault(key) + value * factor;
            }
            return totals;
        }
    
        public class Node
        {
            public string Key;
            public string Name;
            public string Desc;
            public int Level;           // required player level
            public Dictionary<string, double> Effects;  // stat → bonus (доли для процентов)
            public int Position;        // 0=center, 1-7 = surrounding hex
            public string? Perk;        // special perk flag
        }
    
        public class CraftBranch
        {
            public string Key;
            public string Name;
            public string Desc;
            public int Level;           // current level (1-25)
            public int Xp;              // current XP
            public int XpNext;          // XP needed for next level
            public Dictionary<int, string[]> Recipes; // level → unlocked recipe keys
        }
    
        // ── Skill nodes per class branch — 12 nodes each (48 total) ──
        public static readonly Dictionary<string, Node[]> ClassNodes = new()
        {
            ["warrior"] = new[]
            {
                new Node { Key="w_0", Name="Стойка воина", Desc="+5 STR, +3 VIT", Level=1, Position=0, Effects=new(){{"str",5},{"vit",3}} },
                new Node { Key="w_1", Name="Закалка", Desc="+10 макс. HP", Level=3, Position=1, Effects=new(){{"hp",10}} },
                new Node { Key="w_2", Name="Натиск", Desc="+3 ATK", Level=5, Position=2, Effects=new(){{"atk",3}} },
                new Node { Key="w_3", Name="Железная кожа", Desc="+3 DEF", Level=7, Position=3, Effects=new(){{"df",3}} },
                new Node { Key="w_4", Name="Печать крита", Desc="+15% к крит. урону", Level=9, Position=4, Effects=new(){{"crit_mult",0.15}} },
                new Node { Key="w_5", Name="Отпор", Desc="Отражает 2 урона", Level=11, Position=5, Effects=new(){}, Perk="thorns" },
                new Node { Key="w_6", Name="Боевой транс", Desc="25% второй удар", Level=13, Position=6, Effects=new(){}, Perk="double_strike" },
                new Node { Key="w_7", Name="Боевой навык", Desc="+5 VIT, +20 HP", Level=15, Position=7, Effects=new(){{"vit",5},{"hp",20}} },
                new Node { Key="w_8", Name="Сокрушение", Desc="+5 ATK", Level=17, Position=8, Effects=new(){{"atk",5}} },
                new Node { Key="w_9", Name="Крепкий хват", Desc="+5 DEF", Level=19, Position=9, Effects=new(){{"df",5}} },
                new Node { Key="w_10", Name="Несокрушимость", Desc="+30 HP, +3 STR", Level=21, Position=10, Effects=new(){{"hp",30},{"str",3}} },
                new Node { Key="w_11", Name="Жажда боя", Desc="+10% крит, +5 DEF", Level=25, Position=11, Effects=new(){{"crit",0.10},{"df",5}} },
            },
            ["mage"] = new[]
            {
                new Node { Key="m_0", Name="Медитация", Desc="+5 INT, +10 MP", Level=1, Position=0, Effects=new(){{"int",5},{"mana",10}} },
                new Node { Key="m_1", Name="Поток силы", Desc="+5 MP", Level=3, Position=1, Effects=new(){{"mana",5}} },
                new Node { Key="m_2", Name="Разряд", Desc="+3 ATK", Level=5, Position=2, Effects=new(){{"atk",3}} },
                new Node { Key="m_3", Name="Эфирный щит", Desc="+3 DEF", Level=7, Position=3, Effects=new(){{"df",3}} },
                new Node { Key="m_4", Name="Регенерация", Desc="Включает регенерацию", Level=9, Position=4, Effects=new(){}, Perk="regen" },
                new Node { Key="m_5", Name="Вампиризм", Desc="Лечение при убийстве", Level=11, Position=5, Effects=new(){}, Perk="vampire" },
                new Node { Key="m_6", Name="Искатель", Desc="+15% опыта", Level=13, Position=6, Effects=new(){{"xp_pct",0.15}} },
                new Node { Key="m_7", Name="Мудрец", Desc="+5 INT", Level=15, Position=7, Effects=new(){{"int",5}} },
                new Node { Key="m_8", Name="Глубокий колодец", Desc="+20 MP", Level=17, Position=8, Effects=new(){{"mana",20}} },
                new Node { Key="m_9", Name="Цепная молния", Desc="+5 ATK", Level=19, Position=9, Effects=new(){{"atk",5}} },
                new Node { Key="m_10", Name="Источник", Desc="+5 DEF, +15 MP", Level=21, Position=10, Effects=new(){{"df",5},{"mana",15}} },
                new Node { Key="m_11", Name="Архимаг", Desc="+10% крит, +10 INT", Level=25, Position=11, Effects=new(){{"crit",0.10},{"int",10}} },
            },
            ["rogue"] = new[]
            {
                new Node { Key="r_0", Name="Ловкость", Desc="+5 AGI, +3 LUK", Level=1, Position=0, Effects=new(){{"agi",5},{"luk",3}} },
                new Node { Key="r_1", Name="Тень", Desc="+5% уклонения", Level=3, Position=1, Effects=new(){{"dodge",0.05}} },
                new Node { Key="r_2", Name="Хладнокровие", Desc="+5% крита", Level=5, Position=2, Effects=new(){{"crit",0.05}} },
                new Node { Key="r_3", Name="Тихий шаг", Desc="+10% скорости", Level=7, Position=3, Effects=new(){{"move_speed",0.10}} },
                new Node { Key="r_4", Name="Слабое место", Desc="+10% крит. урона", Level=9, Position=4, Effects=new(){{"crit_mult",0.10}} },
                new Node { Key="r_5", Name="Мародёр", Desc="+50% золота", Level=11, Position=5, Effects=new(){}, Perk="greedy" },
                new Node { Key="r_6", Name="Коварство", Desc="Двойной удар", Level=13, Position=6, Effects=new(){}, Perk="double_strike" },
                new Node { Key="r_7", Name="Сноровка", Desc="+5 AGI", Level=15, Position=7, Effects=new(){{"agi",5}} },
                new Node { Key="r_8", Name="Делец", Desc="+5 LUK", Level=17, Position=8, Effects=new(){{"luk",5}} },
                new Node { Key="r_9", Name="Смертельный глаз", Desc="+10% крита", Level=19, Position=9, Effects=new(){{"crit",0.10}} },
                new Node { Key="r_10", Name="Неуловимость", Desc="+10% уклонения", Level=21, Position=10, Effects=new(){{"dodge",0.10}} },
                new Node { Key="r_11", Name="Ловчий", Desc="+15% золота, +5 AGI", Level=25, Position=11, Effects=new(){{"agi",5}}, Perk="greedy" },
            },
            ["artisan"] = new[]
            {
                new Node { Key="a_0", Name="Ученик", Desc="+2 ко всем статам", Level=1, Position=0, Effects=new(){{"str",2},{"agi",2},{"vit",2},{"luk",2},{"int",2}} },
                new Node { Key="a_1", Name="Верный глаз", Desc="+10% качества крафта", Level=3, Position=1, Effects=new(){{"craft_quality",0.10}} },
                new Node { Key="a_2", Name="Искатель", Desc="+15% опыта", Level=5, Position=2, Effects=new(){{"xp_pct",0.15}} },
                new Node { Key="a_3", Name="Добытчик", Desc="+25% золота", Level=7, Position=3, Effects=new(){}, Perk="greedy" },
                new Node { Key="a_4", Name="Мастерская", Desc="+20% опыта крафта", Level=9, Position=4, Effects=new(){{"craft_xp",0.20}} },
                new Node { Key="a_5", Name="Смекалка", Desc="+10% шанса дропа", Level=11, Position=5, Effects=new(){{"drop_pct",0.10}} },
                new Node { Key="a_6", Name="Ремесленный глаз", Desc="+5 ко всем статам", Level=13, Position=6, Effects=new(){{"str",5},{"agi",5},{"vit",5},{"luk",5},{"int",5}} },
                new Node { Key="a_7", Name="Изобретательность", Desc="+3 ко всем статам", Level=15, Position=7, Effects=new(){{"str",3},{"agi",3},{"vit",3},{"luk",3},{"int",3}} },
                new Node { Key="a_8", Name="Подмастерье", Desc="+15% опыта крафта", Level=17, Position=8, Effects=new(){{"craft_xp",0.15}} },
                new Node { Key="a_9", Name="Тщательность", Desc="+20% качества крафта", Level=19, Position=9, Effects=new(){{"craft_quality",0.20}} },
                new Node { Key="a_10", Name="Золотая жила", Desc="+15% шанса дропа", Level=21, Position=10, Effects=new(){{"drop_pct",0.15}} },
                new Node { Key="a_11", Name="Дело жизни", Desc="+5 ко всем статам, +25% золота", Level=25, Position=11, Effects=new(){{"str",5},{"agi",5},{"vit",5},{"luk",5},{"int",5}}, Perk="greedy" },
            },
            ["default"] = new[]
            {
                new Node { Key="d_0", Name="Дух приключений", Desc="+3 ко всем статам", Level=1, Position=0, Effects=new(){{"str",3},{"agi",3},{"vit",3},{"luk",3},{"int",3}} },
                new Node { Key="d_1", Name="Выносливость", Desc="+15 HP", Level=3, Position=1, Effects=new(){{"hp",15}} },
                new Node { Key="d_2", Name="Быстрая обучаемость", Desc="+10% опыта", Level=5, Position=2, Effects=new(){{"xp_pct",0.10}} },
                new Node { Key="d_3", Name="Универсал", Desc="+2 ко всем статам", Level=7, Position=3, Effects=new(){{"str",2},{"agi",2},{"vit",2},{"luk",2},{"int",2}} },
                new Node { Key="d_4", Name="Печать крита", Desc="+5% крита", Level=9, Position=4, Effects=new(){{"crit",0.05}} },
                new Node { Key="d_5", Name="Делец", Desc="+20% золота", Level=11, Position=5, Effects=new(){}, Perk="greedy" },
                new Node { Key="d_6", Name="Эрудиция", Desc="+5 ко всем статам", Level=13, Position=6, Effects=new(){{"str",5},{"agi",5},{"vit",5},{"luk",5},{"int",5}} },
                new Node { Key="d_7", Name="Следопыт", Desc="+5 AGI", Level=15, Position=7, Effects=new(){{"agi",5}} },
                new Node { Key="d_8", Name="Живучесть", Desc="+5 VIT", Level=17, Position=8, Effects=new(){{"vit",5}} },
                new Node { Key="d_9", Name="Мудрец", Desc="+5 INT", Level=19, Position=9, Effects=new(){{"int",5}} },
                new Node { Key="d_10", Name="Мощь", Desc="+5 STR", Level=21, Position=10, Effects=new(){{"str",5}} },
                new Node { Key="d_11", Name="Мастер", Desc="+10% крита, +10% уклонения", Level=25, Position=11, Effects=new(){{"crit",0.10},{"dodge",0.10}} },
            },
        };
    
        // ── Craft branches ──
        public static readonly Dictionary<string, CraftBranch> CraftBranches = new()
        {
            ["blacksmith"] = new()
            {
                Key="blacksmith", Name="Кузнечное дело",
                Desc="Оружие, броня, шлемы и поножи. С уровнем открываются рецепты.",
                Level=1, Xp=0, XpNext=100,
                // Только реально существующие крафт-предметы (SkillReq ≈ уровень ветки)
                Recipes = new()
                {
                    { 1, new[]{"wooden_club", "stone_hatchet", "mage_robe", "leather_cap"} },
                    { 3, new[]{"bone_sword", "wooden_shield"} },
                    { 6, new[]{"iron_sword", "hunting_bow", "mage_hood", "wolf_hood"} },
                    { 8, new[]{"leather_armor", "iron_helm"} },
                    { 12, new[]{"steel_sword", "battle_hammer"} },
                    { 18, new[]{"demon_blade"} },
                    { 22, new[]{"golem_hammer", "lich_robe"} },
                    { 25, new[]{"dragon_blade", "dragon_armor"} },
                },
            },
            ["alchemy"] = new()
            {
                Key="alchemy", Name="Алхимия",
                Desc="Зелья лечения, маны, баффы и яды.",
                Level=1, Xp=0, XpNext=80,
                Recipes = new()
                {
                    { 1, new[]{"potion_s", "mana_s"} },
                    { 3, new[]{"potion_m"} },
                    { 5, new[]{"mana_m"} },
                    { 6, new[]{"potion_l"} },
                    { 12, new[]{"potion_strength"} },
                    { 14, new[]{"potion_agility"} },
                    { 16, new[]{"potion_stone_skin"} },
                    { 18, new[]{"potion_speed"} },
                    { 20, new[]{"potion_dexterity"} },
                    { 25, new[]{"elixir_life"} },
                },
            },
            ["magic_engineering"] = new()
            {
                Key="magic_engineering", Name="Магическая инженерия",
                Desc="Свитки и руны. Уровень открывает новые формулы.",
                Level=1, Xp=0, XpNext=120,
                Recipes = new()
                {
                    { 1, new[]{"scroll_heal", "rune_heal"} },
                    { 5, new[]{"scroll_fireball", "rune_fire"} },
                    { 8, new[]{"scroll_freeze", "rune_frost"} },
                    { 12, new[]{"scroll_lightning", "rune_ward"} },
                    { 18, new[]{"scroll_inferno", "rune_flame"} },
                    { 22, new[]{"rune_guardian", "orb_arcane"} },
                },
            },
        };
    
        /// <summary>
        /// Get skill nodes for a player's class branch.
        /// Returns the nodes for the base class (warrior/mage/rogue/artisan).
        /// </summary>
        public static Node[] GetNodes(Player player)
        {
            // Find the base class (tier 1)
            var def = ClassData.Get(player.ClassKey);
            while (def != null && def.Tier > 1)
            {
                def = ClassData.Get(def.Base);
            }
    
            if (def == null || !ClassNodes.ContainsKey(def.Key))
                return Array.Empty<Node>();
    
            return ClassNodes[def.Key];
        }
    
        /// <summary>
        /// Check if a player has learned a specific skill node.
        /// </summary>
        public static bool HasNode(Player player, string nodeKey)
        {
            return player.LearnedNodes.Contains(nodeKey);
        }
    
        /// <summary>
        /// Try to learn a skill node. Returns true if successful.
        /// </summary>
        public static bool LearnNode(Player player, string nodeKey)
        {
            var nodes = GetNodes(player);
            var node = nodes.FirstOrDefault(n => n.Key == nodeKey);
            if (node == null) return false;
            if (HasNode(player, nodeKey)) return false;
            if (player.Level < node.Level) return false;
            if (player.SkillPoints < 1) return false;
    
            player.SkillPoints--;
            player.LearnedNodes.Add(nodeKey);
    
            // Перк — единственное, что применяется как флаг. Числовые бонусы
            // живут в Effects() и пересчитываются, а не мутируют статы навсегда.
            if (node.Perk != null)
            {
                ApplyPerk(player, node.Perk);
            }
    
            player.Recalc();
            return true;
        }
    
        private static void ApplyPerk(Player player, string perk)
        {
            switch (perk)
            {
                case "double_strike": player.DoubleStrike = true; break;
                case "regen": player.Regen = true; break;
                case "vampire": player.Vampire = true; break;
                case "thorns": player.Thorns = true; break;
                case "greedy": player.Greedy = true; break;
            }
        }
    
        /// <summary>
        /// Get all recipes available to the player across all craft branches.
        /// </summary>
        public static List<(string Branch, string Recipe)> GetAvailableRecipes(Player player)
        {
            var recipes = new List<(string, string)>();
            foreach (var (key, branch) in CraftBranches)
            {
                foreach (var (level, recipeList) in branch.Recipes)
                {
                    if (player.CraftLevels.GetValueOrDefault(key, 1) >= level)
                    {
                        foreach (var r in recipeList)
                            recipes.Add((key, r));
                    }
                }
            }
            return recipes;
        }
    
        /// <summary>
        /// Add crafting XP to a branch and level up if enough.
        /// </summary>
        public static void AddCraftXp(Player player, string branchKey, int xp)
        {
            int currentLevel = player.CraftLevels.GetValueOrDefault(branchKey, 1);
            int currentXp = player.CraftXp.GetValueOrDefault(branchKey, 0);
            currentXp += xp;
    
            var branch = CraftBranches.GetValueOrDefault(branchKey);
            if (branch == null) return;
    
            while (currentLevel < 25)
            {
                int need = branch.XpNext * currentLevel;
                if (currentXp >= need)
                {
                    currentXp -= need;
                    currentLevel++;
                }
                else break;
            }
    
            player.CraftLevels[branchKey] = currentLevel;
            player.CraftXp[branchKey] = currentXp;
        }
    }
}
