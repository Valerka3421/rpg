using System;
using System.Collections.Generic;
using System.Linq;
using Aethelgard.Data;
using Aethelgard.World;

namespace Aethelgard.Systems;

/// <summary>
/// Combat rules: attack resolution, damage, XP, gold, loot, magic.
/// Ported from Python core/rules.py. Shared between single-player and MP.
/// </summary>
public static class Rules
{
    private static readonly Random Rng = new();

    public class AttackResult
    {
        public int Damage;
        public bool Crit;
        public bool Dodged;
        public string? StatusApplied;
        public int Healed;       // вампиризм
        public int ThornsDamage; // ответный урон шипов
    }

    /// <summary>
    /// Resolve a melee attack: attacker → defender.
    /// Уклонение — только у игрока-защитника, крит — только у игрока-атакера
    /// (как в core/rules.py:resolve_attack).
    /// </summary>
    public static AttackResult ResolveAttack(Entity attacker, Entity defender)
    {
        var result = new AttackResult();

        // Dodge check (only vs player)
        float dodge = 0f;
        if (defender is Player p) dodge = p.DodgeChance;
        if (Rng.NextDouble() < dodge)
        {
            result.Dodged = true;
            return result;
        }

        // Base damage: max(1, atk - df + randint(-1, 1))
        int baseDmg = Math.Max(1, attacker.Atk - defender.Df);
        baseDmg += Rng.Next(-Config.DmgSpread, Config.DmgSpread + 1);

        // Crit (only player attacker)
        float crit = 0f;
        if (attacker is Player ap) crit = ap.CritChance;
        if (Rng.NextDouble() < crit)
        {
            double critMult = Config.CritMult
                + (attacker is Player cp ? cp.Effect("crit_mult") : 0.0);
            baseDmg = (int)(baseDmg * critMult);
            result.Crit = true;
        }

        // Rage bonus (эффект rage при HP < 50%)
        if (attacker is Player rap) baseDmg = (int)(baseDmg * rap.RageBonus);

        result.Damage = Math.Max(1, baseDmg);
        defender.Hp -= result.Damage;

        // Apply ability status (ABILITY_TO_STATUS из statuses.json)
        if (!string.IsNullOrEmpty(attacker.Ability) && result.Damage > 0 &&
            StatusData.AbilityToStatus.TryGetValue(attacker.Ability, out var status))
        {
            defender.AddStatus(status);
            result.StatusApplied = status;
        }

        // Vampire / lifesteal: heal = max(1, damage * pct), минимум 5% у перка
        if (attacker is Player vamp && result.Damage > 0)
        {
            double pct = vamp.Lifesteal;
            if (vamp.Vampire) pct = Math.Max(pct, 0.05);
            if (pct > 0)
            {
                int heal = Math.Max(1, (int)(result.Damage * pct));
                int before = vamp.Hp;
                vamp.Hp = Math.Min(vamp.MaxHp, vamp.Hp + heal);
                result.Healed = vamp.Hp - before;
            }
        }

        // Thorns: ответный урон
        if (defender is Player dp && dp.Thorns && result.Damage > 0 && attacker.Alive)
        {
            attacker.Hp -= Config.ThornsDamage;
            result.ThornsDamage = Config.ThornsDamage;
        }

        return result;
    }

    /// <summary>
    /// Двойной удар: перк double_strike даёт второй удар с шансом 0.15.
    /// Возвращает результат второго удара или null.
    /// </summary>
    public static AttackResult? TryDoubleStrike(Player attacker, Entity defender)
    {
        if (!attacker.DoubleStrike) return null;
        if (Rng.NextDouble() >= Config.DoubleStrikeChance) return null;
        if (!defender.Alive || !attacker.Alive) return null;
        return ResolveAttack(attacker, defender);
    }

    /// <summary>
    /// Award kill rewards: XP (в MP — 70/30), gold, drops, mana crystals.
    /// Золото: randint(2, 5+depth), босс +30+depth*10, greedy ×1.5.
    /// (core/rules.py:kill_reward, net/core.py — делёж XP 70/30)
    /// </summary>
    public static void AwardKill(Player killer, Entity monster, int depth = 1,
        List<Player>? allPlayers = null)
    {
        // XP: в MP убийце 70%, остальным — делёж 30%
        if (allPlayers != null && allPlayers.Count > 1)
            AwardKillXp(killer, monster, allPlayers);
        else
            killer.GainXp(monster.XpValue);

        // Gold
        var def = MonsterData.Get(!string.IsNullOrEmpty(monster.MonsterKey)
            ? monster.MonsterKey : null)
            ?? MonsterData.ByCh(monster.Ch.ToString());
        if (def == null || def.Gold)
        {
            int gold = Rng.Next(2, 6 + Math.Max(0, depth));
            if (monster.Boss) gold += 30 + depth * 10;
            if (killer.Greedy) gold = (int)(gold * 1.5);
            if (killer.GoldPct > 0) gold += Math.Max(1, (int)(gold * killer.GoldPct));
            killer.Gold += gold;
        }

        // Drops (таблица MONSTER_DROPS по глифу + BOSS_DROPS).
        // drop_pct из узлов/класса повышает шанс каждой строки таблицы.
        string monsterKey = !string.IsNullOrEmpty(monster.MonsterKey)
            ? monster.MonsterKey : def?.Key ?? "";
        foreach (var drop in DungeonGen.RollDrops(
                     monsterKey, monster.Ch, depth, killer.Effect("drop_pct")))
            killer.AddItem(drop, 1);

        // Mana crystal: шанс 8% с любого моба (размер по max_hp)
        var crystal = MonsterData.RollManaCrystal(monster);
        if (crystal != null) killer.AddItem(crystal, 1);
    }

    /// <summary>
    /// MP-награда за килл: 70% XP убийце, 30% делятся остальные
    /// (net/core.py:1039-1049).
    /// </summary>
    public static void AwardKillXp(Player killer, Entity monster,
        IEnumerable<Player> allPlayers)
    {
        int total = monster.XpValue;
        killer.GainXp((int)Math.Round(total * 0.70));
        var others = allPlayers.Where(pl => pl != killer && pl.Alive).ToList();
        if (others.Count == 0) return;
        int share = Math.Max(1, (int)Math.Round(total * 0.30) / others.Count);
        foreach (var pl in others) pl.GainXp(share);
    }

    // ── Магия: свитки и руны (core/rules.py:_use_scroll + items.py) ──

    public class CastResult
    {
        public bool Ok;
        public string Message = "";
        public List<(int X, int Y, int Damage)> Hits = new();
        public int Healed;
        public string? StatusApplied;
    }

    /// <summary>Радиус AOE огненного шара 3×3 (радиус 1).</summary>
    public const int FireballRadius = 1;
    /// <summary>Радиус заморозки — 5 клеток.</summary>
    public const int FreezeRadius = 5;
    /// <summary>Радиус молнии/инферно — 3.</summary>
    public const int BoltRadius = 3;

    /// <summary>
    /// Применить свиток или руну. Списывает ману только если эффект сработал.
    /// scroll_fireball — урон 3×3 вокруг целевой клетки; scroll_freeze — slow
    /// монстрам в радиусе 5; молния/инферно — урон в радиусе 3; лечащие — HP.
    /// </summary>
    public static CastResult CastMagic(Player player, Dungeon map, string baseKey)
    {
        var def = ItemData.Get(baseKey);
        var result = new CastResult();
        if (def == null)
        {
            result.Message = "Неизвестное заклинание.";
            return result;
        }

        int cost = def.ManaCost;
        if (player.Mana < cost)
        {
            result.Message = $"Not enough mana ({cost} MP required).";
            return result;
        }

        bool cast = false;

        // Лечение
        int heal = def.Heal > 0 ? def.Heal : 0;
        if (heal > 0)
        {
            int before = player.Hp;
            player.Hp = Math.Min(player.MaxHp, player.Hp + heal);
            result.Healed = player.Hp - before;
            cast = cast || result.Healed > 0 || player.Hp == player.MaxHp && before < player.MaxHp
                   || before == player.MaxHp; // руну лечения применяем всегда
            cast = true;
        }

        // Урон по области — по ключу эффекта
        string key = baseKey;
        int dmg = def.IntValue;
        if (key.Contains("fireball") && dmg > 0)
        {
            HitArea(map, player.X, player.Y, FireballRadius, dmg + player.TotalInt, result);
            cast = true;
        }
        else if (key.Contains("lightning") && dmg > 0)
        {
            HitArea(map, player.X, player.Y, BoltRadius, dmg + player.TotalInt, result);
            cast = true;
        }
        else if (key.Contains("inferno") && dmg > 0)
        {
            HitArea(map, player.X, player.Y, BoltRadius, dmg + player.TotalInt, result);
            cast = true;
        }
        else if ((key.Contains("freeze") || key.Contains("frost")) && !key.Contains("fire"))
        {
            // slow монстрам в радиусе
            int radius = key.Contains("freeze") ? FreezeRadius : 2;
            foreach (var m in map.Monsters.Where(m => m.Alive))
            {
                int dist = Math.Max(Math.Abs(m.X - player.X), Math.Abs(m.Y - player.Y));
                if (dist <= radius)
                {
                    m.AddStatus("slow");
                    result.StatusApplied = "slow";
                    cast = true;
                }
            }
        }
        else if (key.Contains("fire") && dmg > 0 && heal <= 0 && result.Hits.Count == 0)
        {
            // руна огня — урон ближайшему
            HitArea(map, player.X, player.Y, 1, dmg + player.TotalInt, result);
            cast = true;
        }

        // Статусы (ward → stone_skin и т.п.)
        if (!string.IsNullOrEmpty(def.Status))
        {
            player.AddStatus(def.Status);
            result.StatusApplied = def.Status;
            cast = true;
        }
        if (!string.IsNullOrEmpty(def.Effect) && def.Effect != def.Status)
        {
            player.AddStatus(def.Effect);
            result.StatusApplied = def.Effect;
            cast = true;
        }

        if (!cast)
        {
            result.Message = "Свиток не сработал — нет подходящей цели.";
            return result;
        }

        player.Mana -= cost;
        result.Ok = true;
        result.Message = $"Cast {def.Name} (-{cost} MP).";
        return result;
    }

    private static void HitArea(Dungeon map, int cx, int cy, int radius, int dmg,
        CastResult result)
    {
        foreach (var m in map.Monsters.Where(m => m.Alive).ToList())
        {
            int dx = Math.Abs(m.X - cx), dy = Math.Abs(m.Y - cy);
            if (dx <= radius && dy <= radius)
            {
                m.Hp -= dmg;
                result.Hits.Add((m.X, m.Y, dmg));
            }
        }
    }

    // ── Крафт (core/rules.py:craft_item) ──

    public class CraftResult
    {
        public bool Ok;
        public string Message = "";
        public string? ItemKey;
        public int Quality;      // +N качество
        public double Accuracy;  // точность мини-игры
        public int CraftXp;
    }

    /// <summary>Качество из точности: ≥0.93→+3, ≥0.72→+2, ≥0.42→+1, иначе 0.</summary>
    public static int QualityFromAccuracy(double accuracy) =>
        accuracy >= 0.93 ? 3 : accuracy >= 0.72 ? 2 : accuracy >= 0.42 ? 1 : 0;

    /// <summary>
    /// Множитель опыта крафта. Оставлен для обратной совместимости:
    /// теперь множители приходят из ClassData.Def.Effects.
    /// </summary>
    public static double CraftClassMultiplier(string classKey) => classKey switch
    {
        "blacksmith" or "magic_engineer" or "alchemist" or "herbalist" => 2.5,
        "potionmaker" => 1.5,
        "apprentice" => 1.25,
        "artisan" => 1.10,
        _ => 1.0,
    };

    /// <summary>
    /// Скрафтить предмет. Требует материалы (qty партий), уровень ветки
    /// ≥ skill_req, чертёж — для особых рецептов, класс ветки artisan —
    /// для craft_classes. Качество +N — только для снаряжения.
    /// </summary>
    public static CraftResult CraftItem(Player player, string itemKey, int qty,
        double accuracy, int recipeIndex = 0)
    {
        var result = new CraftResult();
        var def = ItemData.Get(itemKey);
        if (def == null || !def.HasRecipe)
        {
            result.Message = "Неизвестный рецепт.";
            return result;
        }

        qty = Math.Max(1, Math.Min(9, qty));

        // Гейт ветки ремесла
        string branch = ItemData.CategoryOf(itemKey);
        int branchLevel = player.CraftLevels.GetValueOrDefault(branch, 1);
        if (def.SkillReq > branchLevel)
        {
            result.Message = $"Requires {branch} level {def.SkillReq}.";
            return result;
        }

        // Особые рецепты: чертёж + класс ветки artisan
        if (!string.IsNullOrEmpty(def.BlueprintRequired) &&
            !player.Blueprints.Contains(def.BlueprintRequired))
        {
            result.Message = "Требуется чертёж.";
            return result;
        }
        if (def.CraftClasses.Length > 0 &&
            !def.CraftClasses.Contains(player.ClassKey) && player.ClassKey != "artisan")
        {
            result.Message = "Этот предмет могут создавать только ремесленники.";
            return result;
        }

        // Материалы: qty партий
        var (mats, count, idx) = ItemData.SelectRecipe(itemKey, player.Inventory, recipeIndex);
        if (mats == null)
        {
            result.Message = "Рецепта нет.";
            return result;
        }
        int batches = qty / Math.Max(1, count);
        if (batches < 1)
        {
            result.Message = "Недостаточно материалов даже на одну партию.";
            return result;
        }
        foreach (var (mat, need) in mats)
        {
            if (player.ItemCount(mat) < need * batches)
            {
                result.Message = $"Not enough {ItemData.DisplayName(mat)}.";
                return result;
            }
        }
        foreach (var (mat, need) in mats)
            player.RemoveItem(mat, need * batches);

        // Качество — только снаряжение (EQUIP_TYPES). craft_quality:
        // значение <1 — шанс +1, значение >=1 — гарантированный +N.
        int quality = ItemData.IsEquipment(def) ? QualityFromAccuracy(accuracy) : 0;
        if (ItemData.IsEquipment(def))
        {
            double qBonus = player.Effect("craft_quality");
            if (qBonus >= 1)
                quality = Math.Min(3, quality + (int)qBonus);
            else if (qBonus > 0 && Rng.NextDouble() < qBonus)
                quality = Math.Min(3, quality + 1);
        }
        string outKey = ItemData.MakeLootKey(itemKey, quality);

        for (int i = 0; i < batches; i++)
            player.AddItem(outKey, 1);

        // Опыт ветки: (6 + tier*4 + level_req*2) * count * множители.
        // Множители берутся из эффектов класса (craft_xp + профильная ветка).
        int xpPer = (6 + def.Tier * 4 + def.SkillReq * 2) * Math.Max(1, count);
        string branchXpKey = branch switch
        {
            "blacksmith" => "smithing_xp",
            "alchemy" => "alchemy_xp",
            "magic_engineering" => "engineering_xp",
            _ => "craft_xp",
        };
        double mult = 1.0 + player.Effect("craft_xp") + player.Effect(branchXpKey);
        int xp = Math.Max(1, (int)Math.Round(xpPer * batches * mult));
        AddCraftXp(player, branch, xp);

        result.Ok = true;
        result.ItemKey = outKey;
        result.Quality = quality;
        result.Accuracy = accuracy;
        result.CraftXp = xp;
        result.Message = $"Создано: {ItemData.DisplayName(outKey)}" +
            (quality > 0 ? $" (+{quality})" : "") + $" (+{xp} XP)";
        return result;
    }

    /// <summary>Начислить опыт ветки ремесла с уровнем по кривой.</summary>
    public static void AddCraftXp(Player player, string branch, int xp)
    {
        int cur = player.CraftXp.GetValueOrDefault(branch);
        int level = player.CraftLevels.GetValueOrDefault(branch, 1);
        cur += xp;
        while (level < Config.SkillCap && cur >= RecipeData.SkillXpToNext(level))
        {
            cur -= RecipeData.SkillXpToNext(level);
            level++;
        }
        player.CraftXp[branch] = cur;
        player.CraftLevels[branch] = level;
    }

    // ── Сундук и запертая дверь ──

    /// <summary>
    /// Open a chest at (x,y) — items go to player inventory.
    /// </summary>
    public static string? OpenChest(Dungeon dungeon, Player player, int x, int y)
    {
        if (dungeon.TileAt(x, y) != Tile.Chest) return null;

        // Pre-generated loot (по стекам из ChestLoot или единичный Items-ключ)
        string? lootKey = null;
        if (dungeon.ChestLoot.TryGetValue((x, y), out var lootStack))
        {
            foreach (var (key, count) in lootStack)
            {
                if (count > 0) player.AddItem(key, count);
                lootKey = key;
            }
            dungeon.ChestLoot.Remove((x, y));
        }
        else if (dungeon.Items.TryGetValue((x, y), out var key2))
        {
            player.AddItem(key2, 1);
            lootKey = key2;
        }

        dungeon.Tiles[y][x] = Tile.Floor;
        dungeon.Items.Remove((x, y));
        return lootKey;
    }

    /// <summary>
    /// Try to unlock a locked door — requires boss_key in inventory.
    /// Открывает все клетки запертой двери (BossDoorCells).
    /// </summary>
    public static bool TryUnlockDoor(Player player, Dungeon dungeon, int x, int y)
    {
        if (dungeon.TileAt(x, y) != Tile.LockedDoor) return false;
        if (!player.Inventory.ContainsKey("boss_key")) return false;

        player.RemoveItem("boss_key", 1);
        dungeon.Tiles[y][x] = Tile.Floor;
        foreach (var (dx, dy) in dungeon.BossDoorCells)
            if (dungeon.TileAt(dx, dy) == Tile.LockedDoor)
                dungeon.Tiles[dy][dx] = Tile.Floor;
        dungeon.BossDoorCells.Clear();
        return true;
    }

    /// <summary>
    /// Initialize action timers for all monsters (jittered ±20%,
    /// боссы — 0.55 c, обычные — 0.35 c: MONSTER_ACTION_INTERVAL).
    /// </summary>
    public static void InitActionTimers(List<Entity> monsters)
    {
        foreach (var m in monsters)
        {
            float interval = m.Boss ? Config.BossActionInterval : Config.MonsterActionInterval;
            m.ActionTimer = interval * (1f + (float)(Rng.NextDouble() - 0.5) * 2f * Config.MonsterActionJitter);
        }
    }
}
