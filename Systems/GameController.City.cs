using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Screens = Aethelgard.Systems.ScreenMode;
namespace Aethelgard.Systems;

/// <summary>
/// Город: NPC, стражники (ИИ), лавки, доска заданий, диалоги.
/// Выделено из GameController (частичный класс).
/// </summary>
public partial class GameController
{
    private void SpawnCityNpcs()
    {
        CityNpcs.Clear();
        // Стражники: 4 поста у ворот (север, восток, запад, юг) — parity с
        // _monsters.py: посты (50,24), (64,35), (34,35), (50,44).
        var guardPosts = new (int X, int Y)[]
        {
            (50, 24), (64, 35), (34, 35), (50, 44),
        };
        foreach (var def in CityData.Npcs.Values)
        {
            if (def.IsGuard)
            {
                foreach (var (px, py) in guardPosts)
                {
                    var guard = new Entity(px, py, 9999, 9999, 3, 0,
                        def.Name, 'G', def.Color, 0)
                    {
                        IsNpc = true,
                        NpcType = "guard",
                        Immortal = true,
                        HomeX = px,
                        HomeY = py,
                    };
                    CityNpcs.Add(guard);
                    if (Overworld.Tiles[py][px] == Tile.Wall)
                        Overworld.Tiles[py][px] = Tile.Floor;
                }
                continue;
            }

            var npc = new Entity(def.Pos.X, def.Pos.Y, 10, 10, 0, 0,
                def.Name, def.Ch.Length > 0 ? def.Ch[0] : '?', def.Color, 0)
            {
                IsNpc = true,
                NpcType = def.Key,
                HomeX = def.Pos.X,
                HomeY = def.Pos.Y,
            };
            CityNpcs.Add(npc);
            // Ensure NPC tile is floor
            if (Overworld.Tiles[def.Pos.Y][def.Pos.X] == Tile.Wall)
                Overworld.Tiles[def.Pos.Y][def.Pos.X] = Tile.Floor;
        }
    }

    /// <summary>
    /// Get all NPCs on the current map (городские + NPC текущей карты:
    /// торговец и доска заданий в подземелье).
    /// </summary>
    public List<Entity> AllNpcs()
    {
        var npcs = new List<Entity>();
        if (QuestBoard != null) npcs.Add(QuestBoard);
        if (Elder != null) npcs.Add(Elder);
        if (_worldMode == "overworld")
            npcs.AddRange(CityNpcs);
        // NPC на текущей карте (торговец/доска в подземелье, локации)
        foreach (var m in CurrentMap.Monsters)
            if (m.IsNpc && m.Alive)
                npcs.Add(m);
        return npcs;
    }

    /// <summary>
    /// NPC at position, or null.
    /// </summary>
    public Entity? NpcAt(int x, int y)
    {
        foreach (var npc in AllNpcs())
        {
            if (npc.X == x && npc.Y == y)
                return npc;
        }
        return null;
    }

    /// <summary>
    /// NPC adjacent to player (within 1 tile).
    /// </summary>
    public Entity? NearbyNpc()
    {
        foreach (var npc in AllNpcs())
        {
            if (Math.Max(Math.Abs(npc.X - Player.X), Math.Abs(npc.Y - Player.Y)) <= 1)
                return npc;
        }
        return null;
    }

    // ── Shop ──

    /// <summary>
    /// Open shop with a city NPC.
    /// </summary>
    public void OpenCityShop(string npcType)
    {
        ActiveShopNpc = npcType;
        ShopSubMode = "buy";
        ShopPage = 0;
        OpenModal(Screens.Shop);
    }

    /// <summary>
    /// Buy item from shop by index (городская лавка или торговец подземелья).
    /// </summary>
    public void BuyItem(int index)
    {
        var stock = CurrentShopStock();
        int pageStart = ShopPage * Config.ShopPageSize;
        int idx = pageStart + index;
        if (index < 0 || idx >= stock.Count) return;

        if (ShopSystem.Buy(Player, stock, idx, out string message))
            ShowMessage(message);
        else if (message.Length > 0)
            ShowMessage(message);
    }

    /// <summary>
    /// Sell item from inventory by index (цена продажи sell + rarity*10).
    /// </summary>
    public void SellItem(int index)
    {
        int pageStart = ShopPage * Config.ShopPageSize;
        int idx = pageStart + index;
        int before = Player.Gold;
        if (ShopSystem.Sell(Player, idx, out string message))
        {
            GoldEarned += Player.Gold - before;
            ShowMessage(message);
        }
        else if (message.Length > 0)
            ShowMessage(message);
    }

    // ── Quests ──

    /// <summary>
    /// Accept quest from the board by index (статические + сгенерированные).
    /// </summary>
    public void AcceptQuest(int index)
    {
        var available = Quests.Available.ToList();
        // Сначала статические, затем сгенерированные
        if (index < available.Count)
        {
            string key = available[index];
            var def = QuestData.Get(key);
            if (Quests.Accept(key, def?.Count ?? 1, Player.Inventory))
                ShowMessage($"Принято задание: {def?.Name}");
            return;
        }

        int genIdx = index - available.Count;
        var generated = Quests.GeneratedBoard;
        if (genIdx < 0 || genIdx >= generated.Count) return;

        var q = generated[genIdx];
        if (Quests.AcceptGenerated(q))
        {
            generated.RemoveAt(genIdx);
            // QuestsDone — счётчик выполненных, а не принятых: раньше
            // статистика забега росла при каждом принятии
            ShowMessage($"Принято задание: {q.Name}");
        }
    }

    /// <summary>
    /// Turn in a completed quest by index (статические + сгенерированные).
    /// </summary>
    public void TurnInQuest(int index)
    {
        var completed = Quests.Completed.ToList();
        if (index < completed.Count)
        {
            string key = completed[index];
            if (Quests.TurnIn(key, Player))
            {
                var def = QuestData.Get(key);
                QuestsDone++;
                ShowMessage($"Задание выполнено: {def?.Name}! (+{def?.RewardGold} золота, +{def?.RewardXp} опыта)");
            }
            return;
        }

        int genIdx = index - completed.Count;
        if (genIdx >= 0)
        {
            // Сдать завершённый сгенерированный квест (по ключу активного)
            var genActive = Quests.Active.Where(kv => kv.Value.Generated).ToList();
            var done = genActive.Where(kv => kv.Value.Progress >= kv.Value.Count).ToList();
            if (genIdx < done.Count)
            {
                var kv = done[genIdx];
                if (Quests.TurnInGenerated(kv.Key, Player))
                {
                    QuestsDone++;
                    ShowMessage($"Задание выполнено: {kv.Value.Name}! (+{kv.Value.RewardGold} золота, +{kv.Value.RewardXp} опыта)");
                }
            }
        }
    }

    // ── Dialogue ──

    /// <summary>
    /// Talk to nearby NPC (T). Диалоги: старейшина (лор), стражник,
    /// торговец подземелья, доска заданий, городские лавки.
    /// </summary>
    public void TalkToNpc()
    {
        var npc = NearbyNpc();
        if (npc == null) return;

        if (npc.NpcType == "quest_board")
        {
            // Доска: фиксированные квесты + сгенерированные (до 3 на доске)
            Quests.PopulateBoard();
            Quests.GeneratedBoard = QuestData.GenerateBoardQuests(
                Math.Max(0, Depth), Math.Min(Config.MaxDepth, Depth + 4), 3,
                Environment.TickCount);
            ScreenMode = Screens.Quests;
            return;
        }

        if (npc.NpcType == "elder")
        {
            DialogueNpc = "Старейшина";
            DialogueText = "Приветствую, искатель приключений. Подземелье на северо-востоке таит большие опасности... и большие сокровища. Загляни на доску заданий, если ищешь работу.";
            OpenModal(Screens.Dialogue);
            return;
        }

        if (npc.NpcType == "merchant")
        {
            // Торговец подземелья
            DungeonMerchant = npc;
            DungeonShopMode = true;
            ShopSubMode = "buy";
            ShopPage = 0;
            OpenModal(Screens.Shop);
            return;
        }

        // Shop NPCs
        if (npc.NpcType != null && CityData.Npcs.ContainsKey(npc.NpcType))
        {
            if (npc.NpcType == "guard")
            {
                DialogueNpc = "Стражник";
                DialogueText = "Я сторожу врата. Монстры редко подходят к городским стенам, но в диких землях будьте начеку.";
                OpenModal(Screens.Dialogue);
            }
            else
            {
                DungeonShopMode = false;
                OpenCityShop(npc.NpcType);
            }
            return;
        }
    }

    // ── Guard AI ──

    /// <summary>
    /// Update city guard: attacks nearby monsters, returns to post.
    /// </summary>
    private void UpdateGuard(float dt)
    {
        _guardTimer += dt;
        if (_guardTimer < 0.5f) return;
        _guardTimer -= 0.5f;   // -= вместо = 0: без дрейфа таймера

        if (_worldMode != "overworld") return;

        foreach (var guard in CityNpcs.Where(n => n.NpcType == "guard"))
        {
            // Find nearest monster within 5 tiles
            Entity? target = null;
            int bestDist = 6;
            foreach (var m in Overworld.Monsters)
            {
                if (!m.Alive) continue;
                int dist = Math.Max(Math.Abs(m.X - guard.X), Math.Abs(m.Y - guard.Y));
                if (dist <= 5 && dist < bestDist)
                {
                    bestDist = dist;
                    target = m;
                }
            }

            if (target != null)
            {
                if (bestDist <= 1)
                {
                    // Attack
                    target.Hp -= guard.Atk;
                    if (target.Hp <= 0)
                        Overworld.Monsters.Remove(target);
                }
                else
                {
                    // Move toward target
                    int dx = Math.Sign(target.X - guard.X);
                    int dy = Math.Sign(target.Y - guard.Y);
                    int nx = guard.X + dx, ny = guard.Y + dy;
                    if (Overworld.IsWalkable(nx, ny) && Overworld.MonsterAt(nx, ny) == null
                        && !(nx == Player.X && ny == Player.Y))
                    {
                        guard.X = nx;
                        guard.Y = ny;
                    }
                }
            }
            else
            {
                // Return to post
                int dx = Math.Sign(guard.HomeX - guard.X);
                int dy = Math.Sign(guard.HomeY - guard.Y);
                if (dx != 0 || dy != 0)
                {
                    int nx = guard.X + dx, ny = guard.Y + dy;
                    if (Overworld.IsWalkable(nx, ny) && Overworld.MonsterAt(nx, ny) == null
                        && !(nx == Player.X && ny == Player.Y))
                    {
                        guard.X = nx;
                        guard.Y = ny;
                    }
                }
            }
        }
    }
}
