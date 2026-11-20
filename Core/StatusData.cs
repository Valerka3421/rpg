using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Aethelgard.Core;

/// <summary>
/// ALL 10 status effects — 100% parity with Python core/data/statuses.py.
/// </summary>
public static class StatusData
{
    public class Def
    {
        public string Key, Name;
        public int Duration;       // turns
        public int DamagePerTurn;  // for poison/burning/bleeding
        public float SpeedMod;     // for slow/speed
        public int StatBonus;      // for buff statuses
        public string? StatName;   // which stat is buffed
        public Color Color;
    }

    public static readonly Dictionary<string, Def> All = new()
    {
        ["poison"]      = new() { Key="poison", Name="Poisoned", Duration=4, DamagePerTurn=2, Color=new(120,200,90) },
        ["burning"]     = new() { Key="burning", Name="Burning", Duration=3, DamagePerTurn=3, Color=new(255,100,50) },
        ["bleeding"]    = new() { Key="bleeding", Name="Bleeding", Duration=5, DamagePerTurn=1, Color=new(200,40,40) },
        ["slow"]        = new() { Key="slow", Name="Slowed", Duration=4, SpeedMod=0.5f, Color=new(130,190,255) },
        ["regen"]       = new() { Key="regen", Name="Regenerating", Duration=5, DamagePerTurn=-2, Color=new(120,240,120) },
        ["strength"]    = new() { Key="strength", Name="Strength", Duration=20, StatBonus=3, StatName="atk", Color=new(240,120,90) },
        ["agility"]     = new() { Key="agility", Name="Agility", Duration=20, StatBonus=8, StatName="dodge", Color=new(140,230,120) },
        ["stone_skin"]  = new() { Key="stone_skin", Name="Stone Skin", Duration=20, StatBonus=3, StatName="df", Color=new(170,170,160) },
        ["speed"]       = new() { Key="speed", Name="Speed", Duration=20, SpeedMod=1.25f, Color=new(150,220,250) },
        ["dexterity"]   = new() { Key="dexterity", Name="Dexterity", Duration=20, SpeedMod=1.20f, Color=new(250,210,120) },
    };

    /// <summary>
    /// Apply status tick: damage/heal, decrement duration.
    /// </summary>
    public static void Tick(Entity entity)
    {
        var expired = new List<string>();
        foreach (var (key, turns) in entity.Statuses)
        {
            if (!All.TryGetValue(key, out var def)) continue;

            // Apply effect
            if (def.DamagePerTurn > 0)
                entity.Hp -= def.DamagePerTurn;
            else if (def.DamagePerTurn < 0)
                entity.Hp = Math.Min(entity.MaxHp, entity.Hp - def.DamagePerTurn);

            // Decrement
            if (turns <= 1)
                expired.Add(key);
            else
                entity.Statuses[key] = turns - 1;
        }

        foreach (var s in expired)
            entity.Statuses.Remove(s);
    }

    /// <summary>
    /// Get speed modifier from all active statuses.
    /// </summary>
    public static float GetSpeedMod(Entity entity)
    {
        float mod = 1f;
        foreach (var (key, _) in entity.Statuses)
        {
            if (All.TryGetValue(key, out var def))
                mod *= def.SpeedMod;
        }
        return mod;
    }
}
