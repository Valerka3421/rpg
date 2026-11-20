using System;
using System.Collections.Generic;

namespace Aethelgard.Core;

/// <summary>
/// Combat rules: attack resolution, damage, XP, loot.
/// Ported from Python core/rules.py. Shared between single-player and MP.
/// </summary>
public static class Rules
{
    public class AttackResult
    {
        public int Damage;
        public bool Crit;
        public bool Dodged;
        public string? StatusApplied;
    }

    /// <summary>
    /// Resolve a melee attack: attacker → defender.
    /// Returns damage dealt and flags.
    /// </summary>
    public static AttackResult ResolveAttack(Entity attacker, Entity defender)
    {
        var result = new AttackResult();

        // Dodge check
        float dodge = 0f;
        if (defender is Player p)
            dodge = p.DodgeChance;

        if (Rng.NextDouble() < dodge)
        {
            result.Dodged = true;
            return result;
        }

        // Base damage
        int baseDmg = attacker.Atk - defender.Df;
        baseDmg = Math.Max(1, baseDmg);

        // Damage spread ±1
        baseDmg += Rng.Next(-Config.DmgSpread, Config.DmgSpread + 1);

        // Crit check
        float crit = 0f;
        if (attacker is Player ap)
            crit = ap.CritChance;

        if (Rng.NextDouble() < crit)
        {
            baseDmg = (int)(baseDmg * Config.CritMult);
            result.Crit = true;
        }

        result.Damage = Math.Max(1, baseDmg);
        defender.Hp -= result.Damage;

        // Apply ability status
        if (!string.IsNullOrEmpty(attacker.Ability) && result.Damage > 0)
        {
            var statusMap = new Dictionary<string, string>
            {
                { "burn", "burning" },
                { "frost", "slow" },
                { "poison", "poisoned" },
            };
            if (statusMap.TryGetValue(attacker.Ability, out var status))
            {
                defender.AddStatus(status);
                result.StatusApplied = status;
            }
        }

        // Thorns
        if (defender is Player dp && dp.Thorns && result.Damage > 0)
        {
            attacker.Hp -= Config.ThornsDamage;
        }

        return result;
    }

    /// <summary>
    /// Award kill rewards: XP, gold, drops.
    /// Drops are added directly to the killer's inventory.
    /// </summary>
    public static void AwardKill(Player killer, Entity monster)
    {
        killer.GainXp(monster.XpValue);
        if (killer.Greedy)
            killer.Gold += monster.XpValue / 2;

        // Roll and collect drops
        string monsterKey = monster.Name switch
        {
            "Rat" => "rat",
            "Slime" => "slime",
            "Wolf" => "wolf",
            "Goblin" => "goblin",
            "Skeleton" => "skeleton",
            "Boar" => "boar",
            "Orc" => "orc",
            "Troll" or "Troll Warlord" => "troll",
            "Wraith" => "wraith",
            "Demon" or "Demon Lord" => "demon",
            _ => "",
        };

        var drops = DungeonGen.RollDrops(monsterKey, 1);
        foreach (var item in drops)
            killer.AddItem(item, 1);

        // Boss gold reward
        if (monster.Boss)
            killer.Gold += 50;
    }

    /// <summary>
    /// Open a chest at (x,y) — items go to player inventory.
    /// Returns the item key that was found, or null if chest was empty.
    /// </summary>
    public static string? OpenChest(Dungeon dungeon, Player player, int x, int y)
    {
        if (dungeon.TileAt(x, y) != Tile.Chest)
            return null;

        // Get pre-generated loot
        if (dungeon.Items.TryGetValue((x, y), out var lootKey))
        {
            player.AddItem(lootKey, 1);
            dungeon.Items.Remove((x, y));
        }

        // Convert chest to floor
        dungeon.Tiles[y][x] = Tile.Floor;
        return lootKey;
    }

    /// <summary>
    /// Try to unlock a locked door — requires boss_key in inventory.
    /// </summary>
    public static bool TryUnlockDoor(Player player, Dungeon dungeon, int x, int y)
    {
        if (dungeon.TileAt(x, y) != Tile.LockedDoor)
            return false;

        if (!player.Inventory.ContainsKey("boss_key"))
            return false;

        player.RemoveItem("boss_key", 1);
        dungeon.Tiles[y][x] = Tile.Floor;
        return true;
    }

    private static readonly Random Rng = new();

    /// <summary>
    /// Initialize action timers for all monsters (jittered).
    /// </summary>
    public static void InitActionTimers(List<Entity> monsters)
    {
        foreach (var m in monsters)
        {
            m.ActionTimer = Config.MonsterBaseCooldown
                * (1f + (float)(Rng.NextDouble() - 0.5) * 0.4f);
        }
    }
}
