using System;
using System.Collections.Generic;
using System.Linq;

namespace Aethelgard.Data;

/// <summary>
/// Quests and the quest board — full parity with
/// Python core/data/quests.py. Данные перенесены из data/quests.json
/// в QuestDefs.cs.
/// </summary>
public static partial class QuestData
{
    public static readonly Dictionary<string, Def> Quests = QuestDefs.Quests;

    public class Def
    {
        public string Key = "";
        public string Name = "";
        public string Desc = "";
        public string Type = "";       // kill / collect / reach / craft
        public string Target = "";
        public int Count = 1;
        public int RewardGold;
        public int RewardXp;
        public Dictionary<string, int> RewardItems = new();
        public string? Giver;
        public int DepthMin;
        public int DepthMax;
        public bool Generated;
    }

    public static readonly Dictionary<int, string> BoardBossFloors = new()
    {
        [5] = "G", [10] = "L", [15] = "V", [20] = "A",
    };

    public static Def? Get(string? key) =>
        string.IsNullOrEmpty(key) ? null : Quests.GetValueOrDefault(key);

    public static List<string> QuestsByGiver(string giver) =>
        Quests.Where(kv => kv.Value.Giver == giver).Select(kv => kv.Key).ToList();

    private static List<string> BoardMonstersFor(int dmin, int dmax)
    {
        var res = MonsterData.All
            .Where(kv => kv.Value.MinDepth >= dmin && kv.Value.MinDepth <= dmax && !kv.Value.OverworldOnly)
            .Select(kv => kv.Key).ToList();
        if (res.Count == 0 && dmax > 0)
            res = MonsterData.All
                .Where(kv => kv.Value.MinDepth <= dmax && !kv.Value.OverworldOnly)
                .Select(kv => kv.Key).ToList();
        return res;
    }

    private static List<string> BoardCollectTargets(int dmin, int dmax)
    {
        var chars = new HashSet<string>();
        foreach (var k in BoardMonstersFor(dmin, dmax))
            if (MonsterData.All.TryGetValue(k, out var m)) chars.Add(m.Ch);
        var targets = new HashSet<string>();
        foreach (var ch in chars)
        {
            if (!MonsterData.MonsterDrops.TryGetValue(ch, out var drops)) continue;
            foreach (var (itemKey, _) in drops)
            {
                var (baseKey, _) = ItemData.ParseItemKey(itemKey);
                if (ItemData.Get(baseKey)?.Type == "material" && baseKey != null)
                    targets.Add(baseKey);
            }
        }
        return targets.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    private static Def MakeBoardQuest(int dmin, int dmax, int seed)
    {
        var r = new Random(seed);

        foreach (var (bd, bch) in BoardBossFloors)
        {
            if (dmin <= bd && bd <= dmax && r.NextDouble() < 0.30)
            {
                var boss = MonsterData.All.Values.FirstOrDefault(m => m.Ch == bch);
                string bossName = boss?.Name ?? "Босс";
                return new Def
                {
                    Key = $"gen_boss_{bch}_{bd}_{seed}", Type = "kill", Target = bch, Count = 1,
                    DepthMin = dmin, DepthMax = dmax,
                    Name = $"Охота: {bossName}",
                    Desc = $"Победить {bossName} (глубина {bd}).",
                    RewardGold = 150 + bd * 12, RewardXp = 100 + bd * 10, Generated = true,
                };
            }
        }

        var monsters = BoardMonstersFor(dmin, dmax);
        double roll = r.NextDouble();

        if (roll < 0.55 && monsters.Count > 0)
        {
            string tgt = monsters[r.Next(monsters.Count)];
            var m = MonsterData.All[tgt];
            int count = r.Next(3, 8);
            return new Def
            {
                Key = $"gen_kill_{tgt}_{dmin}_{seed}", Type = "kill", Target = m.Ch, Count = count,
                DepthMin = dmin, DepthMax = dmax,
                Name = $"Уничтожить: {m.Name}",
                Desc = $"Убить {count} × {m.Name}.",
                RewardGold = 25 + count * 6 + dmin * 3,
                RewardXp = 15 + count * 4 + dmin * 2, Generated = true,
            };
        }

        if (roll < 0.80)
        {
            var targets = BoardCollectTargets(dmin, dmax);
            if (targets.Count > 0)
            {
                string tgt = targets[r.Next(targets.Count)];
                var it = ItemData.Get(tgt);
                string itemName = it?.Name ?? tgt;
                int count = r.Next(2, 6);
                return new Def
                {
                    Key = $"gen_collect_{tgt}_{dmin}_{seed}", Type = "collect", Target = tgt, Count = count,
                    DepthMin = dmin, DepthMax = dmax,
                    Name = $"Принести: {itemName}",
                    Desc = $"Собрать {count} × {itemName}.",
                    RewardGold = 30 + count * 8 + dmin * 3,
                    RewardXp = 20 + count * 5 + dmin * 2, Generated = true,
                };
            }
        }

        int targetDepth = Math.Max(dmax, dmin + 1);
        return new Def
        {
            Key = $"gen_reach_{targetDepth}_{dmin}_{seed}", Type = "reach",
            Target = targetDepth.ToString(), Count = 1,
            DepthMin = dmin, DepthMax = dmax,
            Name = "Спуститься в глубину",
            Desc = $"Достичь глубины {targetDepth}.",
            RewardGold = 40 + targetDepth * 6,
            RewardXp = 30 + targetDepth * 5, Generated = true,
        };
    }

    public static List<Def> GenerateBoardQuests(int dmin, int dmax, int count = 3, int baseSeed = 0)
    {
        dmin = Math.Max(0, dmin);
        dmax = Math.Min(20, dmax);
        var output = new List<Def>();
        var seen = new HashSet<string>();
        for (int i = 0; i < count * 4; i++)
        {
            if (output.Count >= count) break;
            var q = MakeBoardQuest(dmin, dmax, baseSeed + i * 7919);
            if (!seen.Add(q.Key)) continue;
            output.Add(q);
        }
        return output;
    }
}
