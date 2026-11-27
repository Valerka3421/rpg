using System;
using System.Collections.Generic;
using System.Linq;

namespace Aethelgard.Systems;

/// <summary>Quest progress state.</summary>
public class QuestState
{
    public int Progress;
    public int Count = 1;
    public bool Generated;
    public string Type = "";
    public string Target = "";
    public string Name = "";
    public string Desc = "";
    public int RewardGold;
    public int RewardXp;
}

/// <summary>
/// Quest log — full parity with Python core/quests.py.
/// </summary>
public class QuestLog
{
    public HashSet<string> Available = new();
    public Dictionary<string, QuestState> Active = new();
    public HashSet<string> Completed = new();
    public HashSet<string> TurnedIn = new();

    /// <summary>Сгенерированные квесты доски (генератор доски: до 3 активных).</summary>
    public List<QuestData.Def> GeneratedBoard = new();

    /// <summary>Полный сброс журнала для новой партии.</summary>
    public void Reset()
    {
        Available.Clear();
        Active.Clear();
        Completed.Clear();
        TurnedIn.Clear();
        GeneratedBoard.Clear();
        PopulateBoard();
    }

    public void AddAvailable(IEnumerable<string> keys)
    {
        foreach (var k in keys)
            if (!TurnedIn.Contains(k) && !Active.ContainsKey(k) && !Completed.Contains(k))
                Available.Add(k);
    }

    /// <summary>
    /// Заполнить доску заданий доступными квестами из таблицы QuestData.
    /// В оригинале вызывалось GameController'ом, но метод не существовал.
    /// </summary>
    public void PopulateBoard()
    {
        AddAvailable(QuestData.Quests.Keys);
    }

    public bool Accept(string questKey, int count, IReadOnlyDictionary<string, int>? inventory = null)
    {
        if (!Available.Contains(questKey)) return false;
        Available.Remove(questKey);
        Active[questKey] = new QuestState { Progress = 0, Count = count };
        if (inventory != null)
        {
            var q = QuestData.Get(questKey);
            if (q?.Type == "collect")
            {
                int have = inventory.GetValueOrDefault(q.Target);
                Active[questKey].Progress = Math.Min(have, count);
                if (have >= count) Complete(questKey);
            }
        }
        return true;
    }

    public void UpdateKill(char monsterCh)
    {
        foreach (var qKey in Active.Keys.ToList())
        {
            var q = QuestData.Get(qKey) ?? (Active[qKey].Generated ? GeneratedDef(Active[qKey]) : null);
            if (q == null || q.Type != "kill") continue;
            // Target квеста — это ровно один глиф монстра ('r', 'G', 'A'),
            // поэтому сравнение по первому символу здесь уже точное.
            if (q.Target.Length == 0 || q.Target[0] != monsterCh) continue;
            var state = Active[qKey];
            state.Progress++;
            if (state.Progress >= state.Count) Complete(qKey);
        }
    }

    public void UpdateCollect(string itemKey, IReadOnlyDictionary<string, int> playerInv)
    {
        foreach (var qKey in Active.Keys.ToList())
        {
            var q = QuestData.Get(qKey) ?? (Active[qKey].Generated ? GeneratedDef(Active[qKey]) : null);
            if (q == null || q.Type != "collect") continue;
            if (q.Target != itemKey) continue;
            var state = Active[qKey];
            state.Progress = Math.Min(playerInv.GetValueOrDefault(itemKey), state.Count);
            if (state.Progress >= state.Count) Complete(qKey);
        }
    }

    public void UpdateReach(int depth)
    {
        foreach (var qKey in Active.Keys.ToList())
        {
            var q = QuestData.Get(qKey) ?? (Active[qKey].Generated ? GeneratedDef(Active[qKey]) : null);
            if (q == null || q.Type != "reach") continue;
            if (int.TryParse(q.Target, out int target) && depth >= target) Complete(qKey);
        }
    }

    public void UpdateCraft(string itemKey)
    {
        foreach (var qKey in Active.Keys.ToList())
        {
            var q = QuestData.Get(qKey) ?? (Active[qKey].Generated ? GeneratedDef(Active[qKey]) : null);
            if (q == null || q.Type != "craft") continue;
            if (q.Target != itemKey) continue;
            var state = Active[qKey];
            state.Progress++;
            if (state.Progress >= state.Count) Complete(qKey);
        }
    }

    private void Complete(string qKey)
    {
        if (!Active.TryGetValue(qKey, out var state)) return;
        if (!state.Generated)
        {
            Active.Remove(qKey);
            Completed.Add(qKey);
        }
        // Generated-квест остаётся в Active с Progress==Count: награда
        // выдаётся через TurnInGenerated у доски. Раньше квест удалялся
        // из Active и его награда была принципиально недостижима.
    }

    private static QuestData.Def GeneratedDef(QuestState s) => new()
    {
        Key = "", Type = s.Type, Target = s.Target, Count = s.Count,
        Name = s.Name, Desc = s.Desc, RewardGold = s.RewardGold,
        RewardXp = s.RewardXp, Generated = true,
    };

    /// <summary>Turn in a completed static quest.</summary>
    public bool TurnIn(string qKey, Player player, Action<int>? onLevelUp = null)
    {
        if (!Completed.Contains(qKey)) return false;
        var q = QuestData.Get(qKey);
        if (q == null) return false;
        if (q.Type == "collect")
        {
            if (player.ItemCount(q.Target) < q.Count) return false;
            player.RemoveItem(q.Target, q.Count);
        }
        int gold = (int)Math.Round(q.RewardGold * (1.0 + player.QuestGoldPct));
        player.Gold += gold;
        foreach (var (item, cnt) in q.RewardItems) player.AddItem(item, cnt);
        int before = player.Level;
        player.GainXp((int)Math.Round(q.RewardXp * (1.0 + player.QuestXpPct)));
        for (int lvl = before + 1; lvl <= player.Level; lvl++) onLevelUp?.Invoke(lvl);
        Completed.Remove(qKey);
        TurnedIn.Add(qKey);
        return true;
    }

    public bool AcceptGenerated(QuestData.Def quest)
    {
        if (Active.ContainsKey(quest.Key) || TurnedIn.Contains(quest.Key)) return false;
        Active[quest.Key] = new QuestState
        {
            Progress = 0, Count = quest.Count, Generated = true,
            Type = quest.Type, Target = quest.Target,
            Name = quest.Name, Desc = quest.Desc,
            RewardGold = quest.RewardGold, RewardXp = quest.RewardXp,
        };
        return true;
    }

    public bool TurnInGenerated(string key, Player player, Action<int>? onLevelUp = null)
    {
        if (!Active.TryGetValue(key, out var data) || !data.Generated) return false;
        if (data.Progress < data.Count) return false;
        if (data.Type == "collect")
        {
            if (player.ItemCount(data.Target) < data.Count) return false;
            player.RemoveItem(data.Target, data.Count);
        }
        player.Gold += data.RewardGold;
        int before = player.Level;
        player.GainXp(data.RewardXp);
        for (int lvl = before + 1; lvl <= player.Level; lvl++) onLevelUp?.Invoke(lvl);
        Active.Remove(key);
        TurnedIn.Add(key);
        return true;
    }

    public Dictionary<string, object?> ToDict() => new()
    {
        ["available"] = Available.OrderBy(x => x, StringComparer.Ordinal).ToList(),
        ["active"] = Active.ToDictionary(kv => kv.Key, kv => (object?)new Dictionary<string, object?>
        {
            ["progress"] = kv.Value.Progress,
            ["count"] = kv.Value.Count,
            ["generated"] = kv.Value.Generated,
            ["type"] = kv.Value.Type,
            ["target"] = kv.Value.Target,
            ["name"] = kv.Value.Name,
            ["desc"] = kv.Value.Desc,
            ["reward_gold"] = kv.Value.RewardGold,
            ["reward_xp"] = kv.Value.RewardXp,
        }),
        ["completed"] = Completed.OrderBy(x => x, StringComparer.Ordinal).ToList(),
        ["turned_in"] = TurnedIn.OrderBy(x => x, StringComparer.Ordinal).ToList(),
    };

    public static QuestLog FromDict(System.Text.Json.JsonElement d)
    {
        var ql = new QuestLog();
        if (d.TryGetProperty("available", out var av) && av.ValueKind == System.Text.Json.JsonValueKind.Array)
            foreach (var v in av.EnumerateArray()) ql.Available.Add(v.GetString() ?? "");
        if (d.TryGetProperty("active", out var act) && act.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            foreach (var p in act.EnumerateObject())
            {
                var s = new QuestState();
                if (p.Value.TryGetProperty("progress", out var pr)) s.Progress = pr.GetInt32();
                if (p.Value.TryGetProperty("count", out var c)) s.Count = c.GetInt32();
                if (p.Value.TryGetProperty("generated", out var g)) s.Generated = g.GetBoolean();
                if (p.Value.TryGetProperty("type", out var t)) s.Type = t.GetString() ?? "";
                if (p.Value.TryGetProperty("target", out var tg)) s.Target = tg.GetString() ?? "";
                if (p.Value.TryGetProperty("name", out var n)) s.Name = n.GetString() ?? "";
                if (p.Value.TryGetProperty("desc", out var ds)) s.Desc = ds.GetString() ?? "";
                if (p.Value.TryGetProperty("reward_gold", out var rg)) s.RewardGold = rg.GetInt32();
                if (p.Value.TryGetProperty("reward_xp", out var rx)) s.RewardXp = rx.GetInt32();
                ql.Active[p.Name] = s;
            }
        }
        if (d.TryGetProperty("completed", out var co) && co.ValueKind == System.Text.Json.JsonValueKind.Array)
            foreach (var v in co.EnumerateArray()) ql.Completed.Add(v.GetString() ?? "");
        if (d.TryGetProperty("turned_in", out var ti) && ti.ValueKind == System.Text.Json.JsonValueKind.Array)
            foreach (var v in ti.EnumerateArray()) ql.TurnedIn.Add(v.GetString() ?? "");
        return ql;
    }
}
