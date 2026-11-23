using System;
using System.Collections.Generic;

namespace Aethelgard.Systems;

/// <summary>
/// Общий ИИ монстров для одиночной игры и авторитетного MP-сервера.
///
/// Раньше <c>GameController.UpdateMonsters</c> и <c>GameCore.Update</c>
/// содержали почти одинаковую копию логики (ranged/healer/flee/boss breath).
/// Теперь правила живут в одном месте, а вызывающий код передаёт колбэки
/// для звука, сообщений и снарядов.
/// </summary>
public static class MonsterAI
{
    /// <summary>
    /// Обновляет ИИ всех монстров на карте. NPC пропускаются.
    /// </summary>
    /// <param name="map">Текущая карта.</param>
    /// <param name="players">Живые цели (1 — сингл, до 4 — MP).</param>
    /// <param name="dt">Дельта времени.</param>
    /// <param name="onMelee">Удар в упор (звук/лог).</param>
    /// <param name="onRanged">Дальняя атака: вызывающий создаёт снаряд.</param>
    /// <param name="onBreath">Дыхание босса (звук/лог).</param>
    public static void Update(
        Dungeon map,
        IReadOnlyList<Player> players,
        float dt,
        Action<Entity, Player, Rules.AttackResult>? onMelee = null,
        Action<Entity, Player>? onRanged = null,
        Action<Entity, Player, int>? onBreath = null)
    {
        foreach (var m in map.Monsters)
        {
            if (!m.Alive || m.IsNpc) continue;

            m.ActionTimer -= dt;
            if (m.ActionTimer > 0) continue;

            float interval = m.Boss ? Config.BossActionInterval : Config.MonsterActionInterval;
            m.ActionTimer = interval
                * (1f + (float)(Random.Shared.NextDouble() - 0.5) * 2f * Config.MonsterActionJitter);

            // slow — пропуск хода
            if (m.Statuses.ContainsKey("slow")) continue;

            Player? nearest = null;
            int bestDist = int.MaxValue;
            foreach (var p in players)
            {
                if (!p.Alive) continue;
                int dist = Math.Max(Math.Abs(m.X - p.X), Math.Abs(m.Y - p.Y));
                if (dist < bestDist)
                {
                    bestDist = dist;
                    nearest = p;
                }
            }
            if (nearest == null) continue;

            // Пассивные (олень) убегают при приближении
            if (m.Flees && bestDist <= 3)
            {
                StepAway(map, m, nearest, players);
                continue;
            }

            // Healer: лечит раненого союзника в радиусе 5 (+8 HP)
            if (m.Healer && TryHeal(map, m))
                continue;

            // Ranged: атакует с 2–4 клеток
            if (m.Ranged && bestDist >= 2 && bestDist <= 4)
            {
                if (onRanged != null)
                    onRanged(m, nearest);
                else
                    Rules.ResolveAttack(m, nearest);
                continue;
            }

            // Boss breath: раз в BossBreathInterval на дистанции 2–5
            if (m.Boss)
            {
                m.BreathTimer += interval;
                if (m.BreathTimer >= Config.BossBreathInterval
                    && bestDist >= Config.BossBreathRangeMin
                    && bestDist <= Config.BossBreathRangeMax)
                {
                    m.BreathTimer = 0;
                    int damage = Math.Max(1,
                        (int)(m.Atk * Config.BossBreathPower) - nearest.Df);
                    if (!nearest.GodModeStat)
                        nearest.Hp -= damage;
                    onBreath?.Invoke(m, nearest, damage);
                    continue;
                }
            }

            if (bestDist <= 1)
            {
                var result = Rules.ResolveAttack(m, nearest);
                onMelee?.Invoke(m, nearest, result);
            }
            else if (bestDist <= (m.Boss ? 15 : 8))
            {
                StepToward(map, m, nearest, players);
            }
        }

        map.Monsters.RemoveAll(m => !m.Alive);
    }

    private static bool TryHeal(Dungeon map, Entity healer)
    {
        Entity? wounded = null;
        int woundHp = int.MaxValue;
        foreach (var ally in map.Monsters)
        {
            if (ally == healer || !ally.Alive || ally.Hp >= ally.MaxHp) continue;
            int d = Math.Max(Math.Abs(ally.X - healer.X), Math.Abs(ally.Y - healer.Y));
            if (d <= 5 && ally.Hp < woundHp)
            {
                woundHp = ally.Hp;
                wounded = ally;
            }
        }
        if (wounded == null) return false;

        wounded.Hp = Math.Min(wounded.MaxHp, wounded.Hp + 8);
        return true;
    }

    private static void StepToward(Dungeon map, Entity m, Player target, IReadOnlyList<Player> players)
    {
        int dx = Math.Sign(target.X - m.X);
        int dy = Math.Sign(target.Y - m.Y);

        // Сначала диагональ, затем кардинальные направления.
        if (TryMove(map, m, dx, dy, players)) return;
        if (dx != 0 && TryMove(map, m, dx, 0, players)) return;
        if (dy != 0) TryMove(map, m, 0, dy, players);
    }

    private static void StepAway(Dungeon map, Entity m, Player target, IReadOnlyList<Player> players)
    {
        int dx = -Math.Sign(target.X - m.X);
        int dy = -Math.Sign(target.Y - m.Y);

        if (TryMove(map, m, dx, dy, players)) return;
        if (dx != 0 && TryMove(map, m, dx, 0, players)) return;
        if (dy != 0) TryMove(map, m, 0, dy, players);
    }

    private static bool TryMove(Dungeon map, Entity m, int dx, int dy,
        IReadOnlyList<Player> players)
    {
        if (dx == 0 && dy == 0) return false;

        int nx = m.X + dx, ny = m.Y + dy;
        if (!map.IsWalkable(nx, ny)) return false;
        if (map.MonsterAt(nx, ny) != null) return false;
        foreach (var p in players)
            if (p.Alive && p.X == nx && p.Y == ny) return false;

        m.X = nx;
        m.Y = ny;
        return true;
    }
}
