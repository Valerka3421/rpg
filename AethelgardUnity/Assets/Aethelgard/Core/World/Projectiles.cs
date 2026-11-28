using System;
    using System.Collections.Generic;
    using Microsoft.Xna.Framework;
    
    namespace Aethelgard.World
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
    /// Projectile system — arrows, fireballs, frost, lightning, heal.
    /// 100% parity with Python core/rules.py ranged attack system.
    /// </summary>
    public class Projectile
    {
        public float X, Y;       // current position (tile-space)
        public float Tx, Ty;     // target position
        public string Kind;      // arrow, fireball, frost, lightning, heal
        public int Damage;
        public Color Color;
        public int SourcePid;    // who fired it (-1 = monster)
        public string? Status;   // status to apply on hit
        public float Speed;      // tiles per second
        public float Life;       // seconds until expiry
        public Entity? Attacker; // для расчёта урона через Rules (dodge/crit)
        public bool Visual;      // только визуальный снаряд (урон уже нанесён)
    }
    
    public static class ProjectileSystem
    {
        public static readonly Dictionary<string, (Color Color, float Speed, string? Status)> Kinds = new()
        {
            ["arrow"]     = (new(220,200,170), 12f, null),
            ["fireball"]  = (new(255,120,50),  8f, "burning"),
            ["frost"]     = (new(130,190,255), 9f, "slow"),
            ["lightning"] = (new(240,230,110), 16f, "slow"),
            ["heal"]      = (new(130,245,150), 10f, null),
        };
    
        /// <summary>
        /// Create a projectile from attacker toward target point.
        /// If target is null, flies in facing direction (8 tiles ahead).
        /// Ported from Python ranged_attack — no auto-targeting.
        /// </summary>
        public static Projectile Create(Entity attacker, Entity? target, string kind,
            int damage, int sourcePid = -1, bool visual = false)
        {
            var info = Kinds.GetValueOrDefault(kind, Kinds["arrow"]);
    
            // If player: fly in facing direction (no homing)
            // If monster: fly toward target
            float tx, ty;
            if (target != null)
            {
                tx = target.X;
                ty = target.Y;
            }
            else if (attacker is Player p)
            {
                var (fx, fy) = p.Facing;
                if (fx == 0 && fy == 0) fx = 1;
                tx = attacker.X + fx * 8;
                ty = attacker.Y + fy * 8;
            }
            else
            {
                tx = attacker.X + attacker.Facing.Dx * 8;
                ty = attacker.Y + attacker.Facing.Dy * 8;
            }
    
            return new Projectile
            {
                X = attacker.X, Y = attacker.Y,
                Tx = tx, Ty = ty,
                Kind = kind,
                Damage = damage,
                Color = info.Color,
                SourcePid = sourcePid,
                Status = info.Status,
                Speed = info.Speed,
                Life = 2f,
                Attacker = attacker,
                Visual = visual,
            };
        }
    
        /// <summary>
        /// Update projectile position. Returns true if it should be removed.
        /// Player projectiles fly STRAIGHT (no homing) — ported from Python update_projectiles.
        /// Monster projectiles home on target.
        /// </summary>
        public static bool Update(Projectile p, float dt, Dungeon d,
            IReadOnlyList<Entity> targets)
        {
            p.Life -= dt;
            if (p.Life <= 0) return true;
    
            float dx = p.Tx - p.X, dy = p.Ty - p.Y;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            float step = p.Speed * dt;
    
            if (dist > 0.001f)
            {
                float move = Math.Min(step, dist);
                p.X += dx / dist * move;
                p.Y += dy / dist * move;
            }
    
            // Check collision with any valid target on path (proximity check).
            // Визуальные снаряды не наносят урон и не сталкиваются с целями.
            if (!p.Visual)
            {
                Entity? hit = null;
                float bestDist = 0.7f;
                foreach (var m in targets)
                {
                    if (!m.Alive) continue;
                    float md = MathF.Sqrt((m.X - p.X) * (m.X - p.X) + (m.Y - p.Y) * (m.Y - p.Y));
                    if (md <= bestDist)
                    {
                        hit = m;
                        bestDist = md;
                    }
                }
    
                if (hit != null)
                {
                    ApplyHit(p, hit);
                    return true;
                }
            }
    
            // Wall stops projectile
            int cx = (int)MathF.Round(p.X);
            int cy = (int)MathF.Round(p.Y);
            if (cx >= 0 && cx < d.W && cy >= 0 && cy < d.H
                && d.Tiles[cy][cx] == Tile.Wall)
                return true;
    
            // Reached target point or expired
            if (dist <= step || p.Life <= 0)
                return true;
    
            return false;
        }
    
        /// <summary>
        /// Apply projectile hit: damage + status.
        /// </summary>
        public static void ApplyHit(Projectile p, Entity target)
        {
            if (!target.Alive) return;
    
            // God mode check
            if (target is Player player && player.GodModeStat)
                return;
    
            // Если известен стрелок — используем общие правила боя (dodge,
            // crit, вампиризм, шипы), иначе — фиксированный урон снаряда.
            if (p.Attacker != null && p.Attacker.Alive)
            {
                var result = Rules.ResolveAttack(p.Attacker, target);
                if (!string.IsNullOrEmpty(p.Status) && result.Damage > 0)
                    target.AddStatus(p.Status);
            }
            else
            {
                target.Hp -= p.Damage;
                if (!string.IsNullOrEmpty(p.Status))
                    target.AddStatus(p.Status);
            }
    
            // Heal projectile
            if (p.Kind == "heal")
                target.Hp = Math.Min(target.MaxHp, target.Hp + p.Damage * 2);
        }
    }
}
