using System;
using System.Collections.Generic;
using System.Linq;

namespace Aethelgard.Core;

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
    public class Node
    {
        public string Key;
        public string Name;
        public string Desc;
        public int Level;           // required player level
        public Dictionary<string, int> Effects;  // stat → bonus
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
            new Node { Key="w_0", Name="Warrior's Resolve", Desc="+5 STR, +3 VIT", Level=1, Position=0, Effects=new(){{"str",5},{"vit",3}} },
            new Node { Key="w_1", Name="Tough Skin", Desc="+10 Max HP", Level=5, Position=1, Effects=new(){{"max_hp",10}} },
            new Node { Key="w_2", Name="Power Strike", Desc="+3 ATK", Level=9, Position=2, Effects=new(){{"atk",3}} },
            new Node { Key="w_3", Name="Iron Will", Desc="+3 DEF", Level=13, Position=3, Effects=new(){{"df",3}} },
            new Node { Key="w_4", Name="Berserker Rage", Desc="+15% crit damage", Level=17, Position=4, Effects=new(){{"crit_mult",15}} },
            new Node { Key="w_5", Name="Thorns", Desc="Reflect 2 damage", Level=21, Position=5, Effects=new(){}, Perk="thorns" },
            new Node { Key="w_6", Name="Double Strike", Desc="25% second hit", Level=25, Position=6, Effects=new(){}, Perk="double_strike" },
            new Node { Key="w_7", Name="Battle Hardened", Desc="+5 VIT, +20 HP", Level=1, Position=7, Effects=new(){{"vit",5},{"max_hp",20}} },
            new Node { Key="w_8", Name="Weapon Mastery", Desc="+5 ATK", Level=5, Position=8, Effects=new(){{"atk",5}} },
            new Node { Key="w_9", Name="Fortify", Desc="+5 DEF", Level=9, Position=9, Effects=new(){{"df",5}} },
            new Node { Key="w_10", Name="Unstoppable", Desc="+30 HP, +3 STR", Level=13, Position=10, Effects=new(){{"max_hp",30},{"str",3}} },
            new Node { Key="w_11", Name="Juggernaut", Desc="+10% crit, +5 DEF", Level=17, Position=11, Effects=new(){{"crit",10},{"df",5}} },
        },
        ["mage"] = new[]
        {
            new Node { Key="m_0", Name="Arcane Mind", Desc="+5 INT, +10 MP", Level=1, Position=0, Effects=new(){{"int",5},{"max_mana",10}} },
            new Node { Key="m_1", Name="Mana Flow", Desc="+5 MP", Level=5, Position=1, Effects=new(){{"max_mana",5}} },
            new Node { Key="m_2", Name="Spell Power", Desc="+3 ATK", Level=9, Position=2, Effects=new(){{"atk",3}} },
            new Node { Key="m_3", Name="Mana Shield", Desc="+3 DEF", Level=13, Position=3, Effects=new(){{"df",3}} },
            new Node { Key="m_4", Name="Regeneration", Desc="Regen enabled", Level=17, Position=4, Effects=new(){}, Perk="regen" },
            new Node { Key="m_5", Name="Vampiric Touch", Desc="Heal on kill", Level=21, Position=5, Effects=new(){}, Perk="vampire" },
            new Node { Key="m_6", Name="Arcane Mastery", Desc="+15% XP", Level=25, Position=6, Effects=new(){{"xp_pct",15}} },
            new Node { Key="m_7", Name="Scholar", Desc="+5 INT", Level=1, Position=7, Effects=new(){{"int",5}} },
            new Node { Key="m_8", Name="Deep Pool", Desc="+20 MP", Level=5, Position=8, Effects=new(){{"max_mana",20}} },
            new Node { Key="m_9", Name="Elemental Power", Desc="+5 ATK", Level=9, Position=9, Effects=new(){{"atk",5}} },
            new Node { Key="m_10", Name="Arcane Barrier", Desc="+5 DEF, +15 MP", Level=13, Position=10, Effects=new(){{"df",5},{"max_mana",15}} },
            new Node { Key="m_11", Name="Spellweaver", Desc="+10% crit, +10 INT", Level=17, Position=11, Effects=new(){{"crit",10},{"int",10}} },
        },
        ["rogue"] = new[]
        {
            new Node { Key="r_0", Name="Shadowstep", Desc="+5 AGI, +3 LUK", Level=1, Position=0, Effects=new(){{"agi",5},{"luk",3}} },
            new Node { Key="r_1", Name="Nimble", Desc="+5% dodge", Level=5, Position=1, Effects=new(){{"dodge",5}} },
            new Node { Key="r_2", Name="Deadly Aim", Desc="+5% crit", Level=9, Position=2, Effects=new(){{"crit",5}} },
            new Node { Key="r_3", Name="Quick Reflexes", Desc="+10% speed", Level=13, Position=3, Effects=new(){{"move_speed",10}} },
            new Node { Key="r_4", Name="Backstab", Desc="+10% crit dmg", Level=17, Position=4, Effects=new(){{"crit_mult",10}} },
            new Node { Key="r_5", Name="Greedy", Desc="+50% gold", Level=21, Position=5, Effects=new(){}, Perk="greedy" },
            new Node { Key="r_6", Name="Shadow Clone", Desc="Double strike", Level=25, Position=6, Effects=new(){}, Perk="double_strike" },
            new Node { Key="r_7", Name="Swift", Desc="+5 AGI", Level=1, Position=7, Effects=new(){{"agi",5}} },
            new Node { Key="r_8", Name="Lucky", Desc="+5 LUK", Level=5, Position=8, Effects=new(){{"luk",5}} },
            new Node { Key="r_9", Name="Assassin's Eye", Desc="+10% crit", Level=9, Position=9, Effects=new(){{"crit",10}} },
            new Node { Key="r_10", Name="Evasion", Desc="+10% dodge", Level=13, Position=10, Effects=new(){{"dodge",10}} },
            new Node { Key="r_11", Name="Master Thief", Desc="+15% gold, +5 AGI", Level=17, Position=11, Effects=new(){{"agi",5}}, Perk="greedy" },
        },
        ["artisan"] = new[]
        {
            new Node { Key="a_0", Name="Craftsman's Eye", Desc="+2 all stats", Level=1, Position=0, Effects=new(){{"str",2},{"agi",2},{"vit",2},{"luk",2},{"int",2}} },
            new Node { Key="a_1", Name="Steady Hand", Desc="+10% craft quality", Level=5, Position=1, Effects=new(){{"craft_quality",10}} },
            new Node { Key="a_2", Name="Resourceful", Desc="+15% XP", Level=9, Position=2, Effects=new(){{"xp_pct",15}} },
            new Node { Key="a_3", Name="Merchant's Eye", Desc="+25% gold", Level=13, Position=3, Effects=new(){}, Perk="greedy" },
            new Node { Key="a_4", Name="Master Crafter", Desc="+20% craft XP", Level=17, Position=4, Effects=new(){{"craft_xp",20}} },
            new Node { Key="a_5", Name="Lucky Find", Desc="+10% drop chance", Level=21, Position=5, Effects=new(){{"drop_pct",10}} },
            new Node { Key="a_6", Name="Renaissance", Desc="+5 all stats", Level=25, Position=6, Effects=new(){{"str",5},{"agi",5},{"vit",5},{"luk",5},{"int",5}} },
            new Node { Key="a_7", Name="Jack of All", Desc="+3 all stats", Level=1, Position=7, Effects=new(){{"str",3},{"agi",3},{"vit",3},{"luk",3},{"int",3}} },
            new Node { Key="a_8", Name="Efficient", Desc="+15% craft XP", Level=5, Position=8, Effects=new(){{"craft_xp",15}} },
            new Node { Key="a_9", Name="Quality Control", Desc="+20% craft quality", Level=9, Position=9, Effects=new(){{"craft_quality",20}} },
            new Node { Key="a_10", Name="Treasure Hunter", Desc="+15% drop chance", Level=13, Position=10, Effects=new(){{"drop_pct",15}} },
            new Node { Key="a_11", Name="Grandmaster", Desc="+5 all, +25% gold", Level=17, Position=11, Effects=new(){{"str",5},{"agi",5},{"vit",5},{"luk",5},{"int",5}}, Perk="greedy" },
        },
        ["default"] = new[]
        {
            new Node { Key="d_0", Name="Adventurer's Spirit", Desc="+3 all stats", Level=1, Position=0, Effects=new(){{"str",3},{"agi",3},{"vit",3},{"luk",3},{"int",3}} },
            new Node { Key="d_1", Name="Survivalist", Desc="+15 HP", Level=5, Position=1, Effects=new(){{"max_hp",15}} },
            new Node { Key="d_2", Name="Quick Learner", Desc="+10% XP", Level=9, Position=2, Effects=new(){{"xp_pct",10}} },
            new Node { Key="d_3", Name="Versatile", Desc="+2 all stats", Level=13, Position=3, Effects=new(){{"str",2},{"agi",2},{"vit",2},{"luk",2},{"int",2}} },
            new Node { Key="d_4", Name="Lucky Strike", Desc="+5% crit", Level=17, Position=4, Effects=new(){{"crit",5}} },
            new Node { Key="d_5", Name="Fortune", Desc="+20% gold", Level=21, Position=5, Effects=new(){}, Perk="greedy" },
            new Node { Key="d_6", Name="Renaissance", Desc="+5 all stats", Level=25, Position=6, Effects=new(){{"str",5},{"agi",5},{"vit",5},{"luk",5},{"int",5}} },
            new Node { Key="d_7", Name="Wanderer", Desc="+5 AGI", Level=1, Position=7, Effects=new(){{"agi",5}} },
            new Node { Key="d_8", Name="Hardy", Desc="+5 VIT", Level=5, Position=8, Effects=new(){{"vit",5}} },
            new Node { Key="d_9", Name="Scholar", Desc="+5 INT", Level=9, Position=9, Effects=new(){{"int",5}} },
            new Node { Key="d_10", Name="Warrior", Desc="+5 STR", Level=13, Position=10, Effects=new(){{"str",5}} },
            new Node { Key="d_11", Name="Master", Desc="+10% crit, +10% dodge", Level=17, Position=11, Effects=new(){{"crit",10},{"dodge",10}} },
        },
    };

    // ── Craft branches ──
    public static readonly Dictionary<string, CraftBranch> CraftBranches = new()
    {
        ["blacksmith"] = new()
        {
            Key="blacksmith", Name="Blacksmithing",
            Desc="Forge weapons and armor from raw materials.",
            Level=1, Xp=0, XpNext=100,
            Recipes = new()
            {
                { 1, new[]{"dagger"} },
                { 3, new[]{"sword"} },
                { 5, new[]{"axe", "leather"} },
                { 8, new[]{"chainmail", "cap"} },
                { 12, new[]{"warhammer", "helm"} },
                { 18, new[]{"platemail"} },
            },
        },
        ["alchemy"] = new()
        {
            Key="alchemy", Name="Alchemy",
            Desc="Brew potions and elixirs from herbs and powders.",
            Level=1, Xp=0, XpNext=80,
            Recipes = new()
            {
                { 1, new[]{"potion_hp_s"} },
                { 3, new[]{"potion_mana_s"} },
                { 5, new[]{"potion_hp_m"} },
                { 8, new[]{"berry"} },
                { 12, new[]{"mushroom"} },
            },
        },
        ["magic_engineering"] = new()
        {
            Key="magic_engineering", Name="Magic Engineering",
            Desc="Enchant items with magical properties.",
            Level=1, Xp=0, XpNext=120,
            Recipes = new()
            {
                { 1, new[]{"potion_mana_s"} },
                { 5, new[]{"book_str"} },
                { 10, new[]{"book_int"} },
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
            def = ClassData.Get(def.BaseClass);
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

        // Apply effects
        foreach (var (stat, value) in node.Effects)
        {
            ApplyStatEffect(player, stat, value);
        }

        // Apply perk
        if (node.Perk != null)
        {
            ApplyPerk(player, node.Perk);
        }

        player.Recalc();
        return true;
    }

    private static void ApplyStatEffect(Player player, string stat, int value)
    {
        switch (stat)
        {
            case "str": player.Str += value; break;
            case "agi": player.Agi += value; break;
            case "vit": player.Vit += value; break;
            case "luk": player.Luk += value; break;
            case "int": player.IntStat += value; break;
            case "df": player.DfStat += value; break;
            case "atk": player.Str += value; break; // simplified
            case "max_hp": player.Vit += value / 3; break;
            case "max_mana": player.IntStat += value / 2; break;
            case "dodge": player.Agi += value; break;
            case "crit": player.Luk += value; break;
        }
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
