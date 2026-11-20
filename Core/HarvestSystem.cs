using System;
using System.Collections.Generic;

namespace Aethelgard.Core;

/// <summary>Harvestable resource record (tree, ore, berry bush).</summary>
public class HarvestRecord
{
    public string Kind = "bush";
    public int Hp = 1;
    public int MaxHp = 1;
    public bool Has = true;
    public double ReadyAt;
    public double SpawnedAt;
}

/// <summary>
/// Harvestable world resources — full parity with Python core/harvest.py.
/// </summary>
public static class HarvestSystem
{
    public const double BerryRegrow = 180.0;
    public const double TreeRegrow = 300.0;
    public const double OreRegrow = 480.0;

    public const int TreeHp = 3;
    public const int OreHp = 6;
    public const int BushHp = 1;

    public static readonly Dictionary<string, (string Item, int Min, int Max)> Rewards = new()
    {
        ["tree"] = ("wood", 2, 4),
        ["ore"] = ("ore", 2, 4),
        ["bush"] = ("berry", 1, 2),
    };

    public static readonly Dictionary<string, string> Names = new()
    {
        ["tree"] = "Дерево",
        ["ore"] = "Рудный камень",
        ["bush"] = "Куст ягод",
    };

    private static readonly Random Rng = new();

    public static double Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

    public static HarvestRecord MakeHarvestable(string kind, double? now = null)
    {
        int hp = kind == "bush" ? BushHp : kind == "tree" ? TreeHp : OreHp;
        return new HarvestRecord
        {
            Kind = kind,
            Hp = hp,
            MaxHp = hp,
            Has = true,
            ReadyAt = 0,
            SpawnedAt = now ?? Now,
        };
    }

    public static double RegrowTime(string kind) => kind switch
    {
        "tree" => TreeRegrow,
        "ore" => OreRegrow,
        "bush" => BerryRegrow,
        _ => 60.0,
    };

    public static bool IsBlocking(HarvestRecord? record) => record != null;
    public static bool CanHarvest(HarvestRecord? record) => record is { Has: true };

    public class HitResult
    {
        public string Kind = "";
        public string Name = "";
        public bool Done;
        public List<(string Key, int Count)> Items = new();
        public int Hp;
        public int MaxHp;
    }

    /// <summary>Hit a resource. Returns an event or null.</summary>
    public static HitResult? Hit(Dungeon dungeon, Player player, int x, int y, int damage, double? now = null)
    {
        double t = now ?? Now;
        if (!dungeon.Harvestables.TryGetValue((x, y), out var record) || !record.Has)
            return null;
        var result = new HitResult
        {
            Kind = record.Kind,
            Name = Names.GetValueOrDefault(record.Kind, "Ресурс"),
            Hp = record.Hp,
            MaxHp = record.MaxHp,
        };

        if (record.Kind == "bush")
        {
            record.Has = false;
            record.ReadyAt = t + BerryRegrow;
            var (key, lo, hi) = Rewards["bush"];
            int count = Rng.Next(lo, hi + 1);
            player.AddItem(key, count);
            result.Done = true;
            result.Items.Add((key, count));
            result.Hp = 0;
            return result;
        }

        record.Hp = Math.Max(0, record.Hp - Math.Max(1, damage));
        result.Hp = record.Hp;
        if (record.Hp > 0) return result;

        record.Has = false;
        record.ReadyAt = t + RegrowTime(record.Kind);
        var (rk, rlo, rhi) = Rewards[record.Kind];
        int rc = Rng.Next(rlo, rhi + 1);
        player.AddItem(rk, rc);
        result.Items.Add((rk, rc));
        if (record.Kind == "ore" && Rng.NextDouble() < 0.5)
        {
            player.AddItem("stone", 1);
            result.Items.Add(("stone", 1));
        }
        result.Done = true;
        return result;
    }

    /// <summary>Regrow resources; returns positions that became ready.</summary>
    public static List<(int X, int Y)> Tick(Dungeon dungeon, double? now = null)
    {
        double t = now ?? Now;
        var ready = new List<(int, int)>();
        foreach (var (pos, record) in dungeon.Harvestables)
        {
            if (!record.Has && t >= record.ReadyAt)
            {
                record.Has = true;
                record.Hp = record.MaxHp;
                ready.Add(pos);
            }
        }
        return ready;
    }

    public static List<Dictionary<string, object>> Serialize(Dungeon dungeon)
    {
        var output = new List<Dictionary<string, object>>();
        foreach (var ((x, y), rec) in dungeon.Harvestables)
        {
            output.Add(new Dictionary<string, object>
            {
                ["x"] = x, ["y"] = y, ["kind"] = rec.Kind, ["hp"] = rec.Hp,
                ["max_hp"] = rec.MaxHp, ["has"] = rec.Has,
                ["ready_at"] = rec.ReadyAt, ["spawned_at"] = rec.SpawnedAt,
            });
        }
        return output;
    }

    public static Dictionary<(int, int), HarvestRecord> Deserialize(IEnumerable<Dictionary<string, object>>? rows)
    {
        var result = new Dictionary<(int, int), HarvestRecord>();
        if (rows == null) return result;
        foreach (var row in rows)
        {
            try
            {
                int x = Convert.ToInt32(row["x"]);
                int y = Convert.ToInt32(row["y"]);
                result[(x, y)] = new HarvestRecord
                {
                    Kind = row.TryGetValue("kind", out var k) ? k?.ToString() ?? "bush" : "bush",
                    Hp = row.TryGetValue("hp", out var hp) ? Convert.ToInt32(hp) : 1,
                    MaxHp = row.TryGetValue("max_hp", out var mh) ? Convert.ToInt32(mh) : 1,
                    Has = !row.TryGetValue("has", out var h) || Convert.ToBoolean(h),
                    ReadyAt = row.TryGetValue("ready_at", out var ra) ? Convert.ToDouble(ra) : 0,
                    SpawnedAt = row.TryGetValue("spawned_at", out var sa) ? Convert.ToDouble(sa) : 0,
                };
            }
            catch { /* skip malformed */ }
        }
        return result;
    }
}
