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
    /// Общие боевые действия SP и MP: поиск цели, серия ударов и награда за килл.
    ///
    /// <see cref="GameController"/> и <see cref="GameCore"/> раньше имели
    /// собственные копии выбора ближайшего монстра, обработки двойного удара
    /// и выдачи награды. Правила урона остаются в <see cref="Rules"/>, а здесь —
    /// сценарии, которые одинаковы для одиночной и сетевой игры.
    /// </summary>
    public static class CombatSystem
    {
        public sealed class AttackOutcome
        {
            public Entity? Target;
            /// <summary>Первый удар (null, если цели не было).</summary>
            public Rules.AttackResult? First;
            /// <summary>Второй удар от перка double_strike, если сработал.</summary>
            public Rules.AttackResult? Second;
            public bool Killed;
            public bool Dodged => First?.Dodged == true;
        }
    
        /// <summary>Ближайший живой монстр в радиусе <paramref name="range"/>.</summary>
        public static Entity? FindNearestMonster(Player player, Dungeon map, int range = 1)
        {
            Entity? target = null;
            int bestDist = int.MaxValue;
            foreach (var m in map.Monsters)
            {
                if (!m.Alive || m.IsNpc) continue;
                int dist = Math.Max(Math.Abs(m.X - player.X), Math.Abs(m.Y - player.Y));
                if (dist <= range && dist < bestDist)
                {
                    bestDist = dist;
                    target = m;
                }
            }
            return target;
        }
    
        /// <summary>Монстр в линии взгляда игрока (для дальних атак и навыков).</summary>
        public static Entity? FindInFacing(Player player, Dungeon map, int range)
        {
            var (fdx, fdy) = player.Facing;
            if (fdx == 0 && fdy == 0) fdy = 1;
            for (int step = 1; step <= range; step++)
            {
                int x = player.X + fdx * step, y = player.Y + fdy * step;
                if (x < 0 || x >= map.W || y < 0 || y >= map.H) break;
                if (map.Tiles[y][x] == Tile.Wall) break;
                var m = map.MonsterAt(x, y);
                if (m != null && m.Alive && !m.IsNpc) return m;
            }
            return null;
        }
    
        /// <summary>
        /// Один ход ближнего боя: первый удар + возможный двойной удар.
        /// Сообщения, звук и награда остаются за вызывающим кодом.
        /// </summary>
        public static AttackOutcome Attack(Player attacker, Entity target)
        {
            var outcome = new AttackOutcome { Target = target };
            var first = Rules.ResolveAttack(attacker, target);
            outcome.First = first;
    
            if (!first.Dodged && target.Alive && attacker.Alive)
                outcome.Second = Rules.TryDoubleStrike(attacker, target);
    
            outcome.Killed = !target.Alive;
            return outcome;
        }
    
        /// <summary>Атаковать ближайшего монстра в радиусе; null — цели нет.</summary>
        public static AttackOutcome? AttackNearest(Player attacker, Dungeon map, int range = 1)
        {
            var target = FindNearestMonster(attacker, map, range);
            return target == null ? null : Attack(attacker, target);
        }
    
        /// <summary>
        /// Начислить награду за убитого монстра и убрать его с карты.
        /// В MP передаётся весь отряд — XP делится 70/30 (см. <see cref="Rules"/>).
        /// </summary>
        public static void AwardKill(Player killer, Dungeon map, Entity target, int depth,
            IReadOnlyCollection<Player>? party = null)
        {
            Rules.AwardKill(killer, target, Math.Max(1, depth), party?.ToList());
            map.Monsters.Remove(target);
        }
    
        /// <summary>Вид снаряда по способности монстра.</summary>
        public static string ProjectileKindFor(Entity monster) => monster.Ability switch
        {
            "fireball" or "burning" => "fireball",
            "frost" => "frost",
            "lightning" => "lightning",
            _ => "arrow",
        };
    
        /// <summary>Вид снаряда по активному навыку игрока.</summary>
        public static string ProjectileKindFor(ClassData.ActiveSkill skill) => skill.Key switch
        {
            "fireball" => "fireball",
            "frost_bolt" => "frost",
            _ => "arrow",
        };
    }
}
