using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Aethelgard.Core;

/// <summary>
/// Status effects and their modifiers — full parity with
/// Python core/data/statuses.py. The raw table lives in StatusTable.g.cs.
/// </summary>
public static partial class StatusData
{
    public class Def
    {
        public string Key = "";
        public string Name = "";
        public string Short = "";
        public Color Color = Color.White;
        public int Damage;
        public int Heal;
        public int Duration = 3;
    }

    public static Def? Get(string? key) =>
        string.IsNullOrEmpty(key) ? null : All.GetValueOrDefault(key);

    /// <summary>Apply one tick of all statuses: damage/heal then decrement.</summary>
    public static void Tick(Entity entity, Action<string, int, Color>? onTick = null)
    {
        var expired = new List<string>();
        foreach (var (key, turns) in new List<KeyValuePair<string, int>>(entity.Statuses))
        {
            if (!All.TryGetValue(key, out var def)) continue;
            if (def.Damage > 0)
            {
                entity.Hp -= def.Damage;
                onTick?.Invoke(key, -def.Damage, def.Color);
            }
            else if (def.Heal > 0)
            {
                int before = entity.Hp;
                entity.Hp = Math.Min(entity.MaxHp, entity.Hp + def.Heal);
                if (entity.Hp > before) onTick?.Invoke(key, entity.Hp - before, def.Color);
            }
            if (turns <= 1) expired.Add(key);
            else entity.Statuses[key] = turns - 1;
        }
        foreach (var s in expired) entity.Statuses.Remove(s);
    }

    public static float GetSpeedMod(Entity entity)
    {
        float mod = 1f;
        foreach (var key in entity.Statuses.Keys)
            if (key == "slow") mod *= 0.5f;
        return mod;
    }
}
