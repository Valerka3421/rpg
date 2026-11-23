using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Aethelgard.Net;

/// <summary>
/// Authoritative server game state.
/// Ported from Python net/core.py:GameCore.
///
/// Uses Rules for combat — same rules as single-player.
/// Manages: players (by PID), overworld + locations + dungeon cache,
/// monsters, items, events, shared quest log, trade sessions.
/// Безопасность (parity с net/core.py + test_net_security.py):
/// валидация шага (одна ось, |d|≤1), санитизация чата (120 символов),
/// скейлинг мобов +20% HP/+10% ATK за игрока, XP 70/30.
/// </summary>
public class GameCore
{
    public Dictionary<int, Player> Players = new();
    public Dungeon Overworld;
    public Dungeon Dungeon;
    public Dungeon CurrentMap =>
        _worldMode == "dungeon" ? Dungeon
        : _currentLocation ?? Overworld;
    public string WorldMode => _worldMode;
    public int Depth = 0;
    public List<string> Events = new();
    public List<(int Pid, string Text)> ChatLog = new();

    /// <summary>Визуальные снаряды текущей карты (стрелы/файрболы монстров и навыков).</summary>
    public List<Projectile> Projectiles = new();

    /// <summary>Общий журнал квестов отряда (parity: quest_log в снапшоте).</summary>
    public QuestLog Quests = new();

    /// <summary>Активные сессии обмена (пара PID'ов → сессия).</summary>
    private readonly Dictionary<int, TradeSession> _trades = new();

    private string _worldMode = "overworld";
    private readonly Dictionary<int, Dungeon> _floorCache = new();
    private readonly Dictionary<string, Dungeon> _locationCache = new();
    private Dungeon? _currentLocation;
    private float _regenTimer;

    public GameCore()
    {
        Overworld = WorldGen.GenerateOverworld();
        Dungeon = Overworld;
        SpawnWorldNpcs();
        Rules.InitActionTimers(Overworld.Monsters);
    }

    /// <summary>Старейшина, доска заданий и городские NPC на оверворлде.</summary>
    private void SpawnWorldNpcs()
    {
        var (bx, by) = CityData.QuestBoardPos;
        Overworld.Monsters.Add(new Entity(bx, by, 1, 1, 0, 0, "Доска заданий", 'B',
            new Microsoft.Xna.Framework.Color(206, 170, 96), 0)
        { IsNpc = true, NpcType = "quest_board" });
        if (Overworld.Tiles[by][bx] == Tile.Wall) Overworld.Tiles[by][bx] = Tile.Floor;

        var (ex, ey) = CityData.ElderPos;
        Overworld.Monsters.Add(new Entity(ex, ey, 1, 1, 0, 0, "Старейшина", 'E',
            new Microsoft.Xna.Framework.Color(180, 160, 100), 0)
        { IsNpc = true, NpcType = "elder" });
        if (Overworld.Tiles[ey][ex] == Tile.Wall) Overworld.Tiles[ey][ex] = Tile.Floor;

        foreach (var def in CityData.Npcs.Values)
        {
            var npc = new Entity(def.Pos.X, def.Pos.Y, 10, 10, 0, 0, def.Name,
                def.Ch.Length > 0 ? def.Ch[0] : '?', def.Color, 0)
            { IsNpc = true, NpcType = def.Key };
            Overworld.Monsters.Add(npc);
            if (Overworld.Tiles[def.Pos.Y][def.Pos.X] == Tile.Wall)
                Overworld.Tiles[def.Pos.Y][def.Pos.X] = Tile.Floor;
        }
    }

    /// <summary>
    /// Add a new player. Скейлинг мобов: +20% HP / +10% ATK за каждого
    /// игрока сверх первого (net/player_registry.py:126-147).
    /// </summary>
    public Player AddPlayer(int pid, string classKey)
    {
        var player = CharacterFactory.Create(classKey);
        player.X = Aethelgard.World.Overworld.VillageCenter.Item1;
        player.Y = Aethelgard.World.Overworld.VillageCenter.Item2;

        Players[pid] = player;

        // Восстановление сохранённого игрока (продолжение MP-сессии)
        RestoreSavedPlayer(pid, player);

        // Скейлинг существующих мобов под размер отряда. Считаем от базовых
        // статов, а не от текущих: иначе каждый новый игрок накапливал множитель.
        int party = Math.Max(1, Players.Count);
        if (party > 1)
        {
            double hpFactor = 1 + Config.MpHpScalePerPlayer * (party - 1);
            double atkFactor = 1 + Config.MpAtkScalePerPlayer * (party - 1);
            foreach (var m in CurrentMap.Monsters)
            {
                if (m.IsNpc || !m.Alive) continue;
                int baseHp = m.BaseMaxHp > 0 ? m.BaseMaxHp : m.MaxHp;
                int baseAtk = m.BaseAtk > 0 ? m.BaseAtk : m.Atk;
                double hpRatio = m.MaxHp > 0 ? m.Hp / (double)m.MaxHp : 1.0;
                m.MaxHp = Math.Max(1, (int)(baseHp * hpFactor));
                m.Hp = Math.Max(1, Math.Min(m.MaxHp, (int)(m.MaxHp * hpRatio)));
                m.Atk = Math.Max(1, (int)(baseAtk * atkFactor));
            }
        }

        AddEvent($"Игрок {pid} вступил в отряд ({classKey})");
        return player;
    }

    public void RemovePlayer(int pid)
    {
        CancelTrade(pid, "opponent left");
        Players.Remove(pid);
        AddEvent($"Игрок {pid} покинул игру");
    }

    /// <summary>
    /// Process input from a client. Валидация шага: ровно один шаг по одной
    /// оси (net/core.py:_is_valid_step) — «телепорты» отклоняются.
    /// </summary>
    public void ProcessInput(int pid, ClientMsg msg)
    {
        if (!Players.TryGetValue(pid, out var player)) return;

        // wait — пропуск хода: сброс таймеров действий (net/core.py:wait)
        if (msg.Wait == true)
        {
            player.ActionTimer = 0f;
            player.MoveTimer = 0f;
        }

        // duel_request — вызов на дуэль: событие в общий лог (net/core.py:duel_request)
        if (msg.DuelTarget.HasValue && Players.TryGetValue(msg.DuelTarget.Value, out var duelTarget)
            && msg.DuelTarget.Value != pid)
        {
            AddEvent($"{player.ClassName} вызывает {duelTarget.ClassName} на дуэль!");
        }

        // quest_talk — разговор с NPC/доской заданий (net/core.py:quest_talk)
        if (!string.IsNullOrEmpty(msg.QuestTalk))
        {
            switch (msg.QuestTalk)
            {
                case "quest_board":
                    if (NearElder(player))
                    {
                        Quests.PopulateBoard();
                        AddEvent("Отряд изучает доску заданий.");
                    }
                    else AddEvent("Доска заданий — в деревне.");
                    break;
                case "elder":
                    if (NearElder(player))
                        AddEvent("Старейшина: задания берут и сдают здесь.");
                    else AddEvent("Старейшина — в деревне.");
                    break;
                case "merchant":
                    AddEvent("Торговец путешествует по этажам 3, 6, 9, 12, 15, 18.");
                    break;
                default:
                    AddEvent($"Вы говорите с: {msg.QuestTalk}.");
                    break;
            }
        }

        if (msg.Dx != null && msg.Dy != null)
        {
            int dx = msg.Dx.Value, dy = msg.Dy.Value;
            // int (не bool), abs ≤ 1, одна ось
            if (!Config.IsValidStep(dx, dy)) return;
            TryMovePlayer(player, dx, dy);
        }

        if (msg.Attack == true) PlayerAttack(player);
        if (msg.UseStairs == true) UseStairs(player);
        if (msg.UsePotion == true)
        {
            // Цепочка зелий как на H в сингле: только существующие зелья
            string? potion = ItemSystem.UseBestPotion(player, CurrentMap);
            if (potion != null)
                AddEvent($"Игрок {pid} выпил: {potion}");
        }
        if (msg.UseItemKey != null)
        {
            var key = msg.UseItemKey;
            if (player.ItemCount(key) > 0 &&
                player.Inventory.ContainsKey(key))
            {
                player.UseItem(key, CurrentMap);
            }
        }
        if (!string.IsNullOrEmpty(msg.UnequipSlot))
            player.UnequipSlot(msg.UnequipSlot);
        if (msg.LearnNodeIdx.HasValue)
        {
            // Изучение узла дерева навыков (клавиши 1–7 на панели N — как в SP)
            if (ProgressionSystem.LearnNode(player, msg.LearnNodeIdx.Value, out string msgText))
                AddEvent(msgText);
        }
        if (msg.Promote == true)
        {
            // Повышение класса (P): первое доступное, как в SP TryPromoteClass
            if (ProgressionSystem.Promote(player, out string promoteText))
                AddEvent(promoteText);
        }
        if (msg.QuickbarSlot.HasValue)
        {
            // Назначение хотбара (Shift+1–8 в инвентаре — как в SP)
            player.AssignQuickbar(msg.QuickbarSlot.Value, msg.QuickbarKey);
        }
        if (!string.IsNullOrEmpty(msg.AllocateStat))
            StatSystem.Allocate(player, msg.AllocateStat);
        if (!string.IsNullOrEmpty(msg.CraftKey) && msg.CraftAccuracy.HasValue)
        {
            var res = Rules.CraftItem(player, msg.CraftKey, msg.CraftQty ?? 1,
                Math.Clamp(msg.CraftAccuracy.Value, 0.0, 1.0));
            AddEvent(res.Message);
        }
        if (msg.BuyIdx.HasValue) MpBuy(player, msg.BuyIdx.Value);
        if (msg.SellIdx.HasValue) MpSell(player, msg.SellIdx.Value);
        if (msg.Reroll == true)
        {
            var stock = CurrentMap.MerchantStock;
            if (stock != null && _worldMode == "dungeon")
            {
                // Реролл возможен только рядом с торговцем (как в SP: R у торговца)
                if (!NearMerchant(player))
                    AddEvent("Торговец не рядом.");
                else
                {
                    int price = Config.RerollPrice(Math.Max(1, Depth));
                    if (player.Gold >= price)
                    {
                        player.Gold -= price;
                        CurrentMap.MerchantStock = RecipeData.MakeShopStock(Math.Max(1, Depth));
                    }
                }
            }
        }
        if (!string.IsNullOrEmpty(msg.QuestKey))
        {
            // Принять/сдать квест можно только рядом со старейшиной
            if (NearElder(player))
            {
                if (msg.QuestAccept == true)
                {
                    if (Quests.Available.Contains(msg.QuestKey))
                    {
                        var def = QuestData.Get(msg.QuestKey);
                        Quests.Accept(msg.QuestKey, def?.Count ?? 1, player.Inventory);
                        AddEvent($"Игрок {pid} принял задание");
                    }
                    else
                    {
                        var gen = Quests.GeneratedBoard.FirstOrDefault(q => q.Key == msg.QuestKey);
                        if (gen != null && Quests.AcceptGenerated(gen))
                            Quests.GeneratedBoard.Remove(gen);
                    }
                }
                else if (msg.QuestTurnIn == true)
                {
                    if (Quests.Completed.Contains(msg.QuestKey))
                        Quests.TurnIn(msg.QuestKey, player);
                    else
                        Quests.TurnInGenerated(msg.QuestKey, player);
                }
            }
            else
                AddEvent("Задания выдаёт Старейшина в деревне.");
        }
        if (msg.HarvestX.HasValue && msg.HarvestY.HasValue)
        {
            int hx = msg.HarvestX.Value, hy = msg.HarvestY.Value;
            // Цель обязана быть смежной с игроком (net/core.py:616-651)
            if (Math.Abs(hx - player.X) <= 1 && Math.Abs(hy - player.Y) <= 1
                && CurrentMap.Harvestables.TryGetValue((hx, hy), out var rec) && rec.Has)
            {
                var hit = HarvestSystem.Hit(CurrentMap, player, hx, hy, player.Atk);
                if (hit != null && hit.Done)
                    AddEvent($"Игрок {pid} собрал: {hit.Name}");
            }
        }
        if (msg.SkillIdx.HasValue)
        {
            // Активный навык класса: урон по ближайшему / лечение
            var skill = SkillSystem.Get(player, msg.SkillIdx.Value);
            if (skill != null && SkillSystem.Validate(player, skill) == null)
            {
                if (SkillSystem.IsHeal(skill))
                {
                    if (player.Hp < player.MaxHp)
                    {
                        int healed = SkillSystem.Heal(player, skill);
                        SkillSystem.Pay(player, skill);
                        AddEvent($"Игрок {pid}: {skill.Name} (+{healed} HP)");
                    }
                }
                else
                {
                    var target = CombatSystem.FindNearestMonster(player, CurrentMap, 8);
                    if (target != null)
                    {
                        Projectiles.Add(ProjectileSystem.Create(player, target,
                            CombatSystem.ProjectileKindFor(skill), 0, visual: true));
                        int damage = SkillSystem.Damage(player, skill, target);
                        SkillSystem.Pay(player, skill);
                        AddEvent($"Игрок {pid}: {skill.Name} — {damage} урона ({target.Name})");
                        if (!target.Alive)
                            KillMonster(player, target);
                    }
                }
            }
        }

        // ── Обмен (trade) ──
        if (msg.TradeTarget.HasValue && !string.IsNullOrEmpty(msg.TradeAction))
            HandleTrade(pid, player, msg);

        if (msg.ChatText != null)
        {
            // Санитизация чата: сжатие пробелов + обрезка 120 символов
            string clean = Config.SanitizeChat(msg.ChatText);
            if (clean.Length > 0)
            {
                ChatLog.Add((pid, clean));
                if (ChatLog.Count > 20) ChatLog.RemoveAt(0);
            }
        }
    }

    // ── Trade sessions ──

    private class TradeSession
    {
        public int PidA, PidB;
        public Dictionary<string, int> OfferA = new(), OfferB = new();
        public int GoldA, GoldB;
        public bool ReadyA, ReadyB;
        public bool Done;
    }

    private void HandleTrade(int pid, Player player, ClientMsg msg)
    {
        int target = msg.TradeTarget ?? -1;
        if (target < 0 || target == pid) return; // обмен с самим собой запрещён
        if (msg.TradeAction == "accept")
        {
            // accept — подтверждение заявки вторым игроком (Y в net/view)
            if (_trades.TryGetValue(pid, out var acc))
                AddEvent($"Игрок {pid} принял обмен (обоим: R — готов).");
            return;
        }
        if (Players.TryGetValue(target, out var partner))
        {
            int dist = Math.Max(Math.Abs(partner.X - player.X), Math.Abs(partner.Y - player.Y));
            if (dist > 1)
            {
                AddEvent("Слишком далеко для обмена — подойдите ближе.");
                return;
            }
        }
        else
        {
            AddEvent("Такого игрока нет.");
            return;
        }

        switch (msg.TradeAction)
        {
            case "request":
                if (_trades.ContainsKey(pid) || _trades.ContainsKey(target)) return;
                var s = new TradeSession { PidA = pid, PidB = target };
                _trades[pid] = s; _trades[target] = s;
                AddEvent($"Игрок {pid} предлагает обмен игроку {target} (Y — принять)");
                break;
            case "decline": case "cancel":
                CancelTrade(pid, "trade cancelled");
                break;
            case "ready":
                if (_trades.TryGetValue(pid, out var st2)) { st2.ReadyA = st2.ReadyB = true; ExecuteIfReady(st2); }
                break;
            case "add":
                if (_trades.TryGetValue(pid, out var st3) && !string.IsNullOrEmpty(msg.TradeItemKey))
                {
                    int count = Math.Max(1, msg.TradeCount ?? 1);
                    var offer = pid == st3.PidA ? st3.OfferA : st3.OfferB;
                    if (player.ItemCount(msg.TradeItemKey) >= count)
                        offer[msg.TradeItemKey] = offer.GetValueOrDefault(msg.TradeItemKey) + count;
                }
                break;
            case "remove":
                if (_trades.TryGetValue(pid, out var st4) && !string.IsNullOrEmpty(msg.TradeItemKey))
                {
                    var offer2 = pid == st4.PidA ? st4.OfferA : st4.OfferB;
                    offer2.Remove(msg.TradeItemKey);
                }
                break;
            case "gold":
                if (_trades.TryGetValue(pid, out var st5))
                {
                    int gold = Math.Max(0, msg.TradeGold ?? 0);
                    if (player.Gold >= gold)
                    {
                        if (pid == st5.PidA) st5.GoldA = gold; else st5.GoldB = gold;
                    }
                }
                break;
        }
    }

    private void ExecuteIfReady(TradeSession s)
    {
        if (!s.ReadyA || !s.ReadyB || s.Done) return;
        if (!Players.TryGetValue(s.PidA, out var a) || !Players.TryGetValue(s.PidB, out var b))
            return;
        // Двойная проверка наличия перед исполнением (net/core.py:_trade_execute)
        foreach (var (key, count) in s.OfferA)
            if (a.ItemCount(key) < count) { AddEvent("Обмен не удался: не хватает предметов."); return; }
        foreach (var (key, count) in s.OfferB)
            if (b.ItemCount(key) < count) { AddEvent("Обмен не удался: не хватает предметов."); return; }
        if (a.Gold < s.GoldA || b.Gold < s.GoldB) { AddEvent("Обмен не удался: не хватает золота."); return; }

        foreach (var (key, count) in s.OfferA) { a.RemoveItem(key, count); b.AddItem(key, count); }
        foreach (var (key, count) in s.OfferB) { b.RemoveItem(key, count); a.AddItem(key, count); }
        a.Gold -= s.GoldA; b.Gold += s.GoldA;
        b.Gold -= s.GoldB; a.Gold += s.GoldB;

        AddEvent("Обмен завершён.");
        s.Done = true;
        _trades.Remove(s.PidA);
        _trades.Remove(s.PidB);
    }

    private void CancelTrade(int pid, string reason)
    {
        if (!_trades.TryGetValue(pid, out var s)) return;
        _trades.Remove(s.PidA);
        _trades.Remove(s.PidB);
        AddEvent($"Обмен отменён ({reason}).");
    }

    // ── Shop (adjacency required) ──

    private void MpBuy(Player player, int idx)
    {
        List<RecipeData.ShopEntry> stock;
        if (_worldMode == "dungeon")
        {
            // Покупка возможна только рядом с торговцем (как в SP: B у торговца)
            if (!NearMerchant(player))
            {
                AddEvent("Торговец не рядом.");
                return;
            }
            stock = CurrentMap.MerchantStock ??= RecipeData.MakeShopStock(Math.Max(1, Depth));
        }
        else
        {
            // Городская лавка: нужно стоять рядом с NPC-лавочником
            Entity? npc = NearShopNpc(player);
            if (npc == null || npc.NpcType == null
                || !CityData.Npcs.ContainsKey(npc.NpcType))
                return;
            stock = ShopSystem.CityStock(npc.NpcType);
        }
        if (ShopSystem.Buy(player, stock, idx, out string message) && message.Length > 0)
            AddEvent(message);
    }

    private void MpSell(Player player, int idx)
    {
        // Продажа возможна только рядом с торговцем/лавочником (как в SP)
        if (_worldMode == "dungeon" && !NearMerchant(player))
        {
            AddEvent("Торговец не рядом.");
            return;
        }
        if (_worldMode != "dungeon" && NearShopNpc(player) == null) return;
        if (ShopSystem.Sell(player, idx, out string message) && message.Length > 0)
            AddEvent(message);
    }

    private bool NearElder(Player player)
    {
        foreach (var m in Overworld.Monsters)
            if (m.IsNpc && m.NpcType == "elder" && m.Alive
                && Math.Max(Math.Abs(m.X - player.X), Math.Abs(m.Y - player.Y)) <= 1)
                return true;
        return false;
    }

    /// <summary>Торговец подземелья рядом с игроком (для Buy/Sell/Reroll).</summary>
    private bool NearMerchant(Player player)
    {
        foreach (var m in CurrentMap.Monsters)
            if (m.IsNpc && m.Alive && m.NpcType == "merchant"
                && Math.Max(Math.Abs(m.X - player.X), Math.Abs(m.Y - player.Y)) <= 1)
                return true;
        return false;
    }

    private Entity? NearShopNpc(Player player)
    {
        foreach (var m in Overworld.Monsters)
            if (m.IsNpc && m.Alive && m.NpcType is "blacksmith" or "alchemist" or "innkeeper"
                && Math.Max(Math.Abs(m.X - player.X), Math.Abs(m.Y - player.Y)) <= 1)
                return m;
        return null;
    }

    private void TryMovePlayer(Player player, int dx, int dy)
    {
        int nx = player.X + dx;
        int ny = player.Y + dy;

        if (nx < 0 || nx >= CurrentMap.W || ny < 0 || ny >= CurrentMap.H)
            return;

        // Check for monster
        var monster = CurrentMap.MonsterAt(nx, ny);
        if (monster != null && !monster.IsNpc)
        {
            var outcome = CombatSystem.Attack(player, monster);
            if (outcome.Killed) KillMonster(player, monster);
            return;
        }

        var tile = CurrentMap.Tiles[ny][nx];
        if (tile == Tile.Wall || tile == Tile.LockedDoor || tile == Tile.Chest)
            return;

        // Check for other players
        foreach (var other in Players.Values)
        {
            if (other.X == nx && other.Y == ny && other != player)
                return;
        }

        player.Facing = (dx, dy);
        player.X = nx;
        player.Y = ny;

        // Auto-pickup
        if (CurrentMap.Items.TryGetValue((nx, ny), out var itemKey))
        {
            player.AddItem(itemKey, 1);
            CurrentMap.Items.Remove((nx, ny));
            Quests.UpdateCollect(itemKey, player.Inventory);
        }
    }

    private void KillMonster(Player killer, Entity monster)
    {
        CombatSystem.AwardKill(killer, CurrentMap, monster, Depth, Players.Values.ToList());
        Quests.UpdateKill(monster.Ch);
        AddEvent($"{killer.ClassName} убил {monster.Name}");
    }

    private void PlayerAttack(Player player)
    {
        var outcome = CombatSystem.AttackNearest(player, CurrentMap);
        if (outcome is { Killed: true, Target: not null })
            KillMonster(player, outcome.Target);
    }

    private void UseStairs(Player player)
    {
        var tile = CurrentMap.Tiles[player.Y][player.X];
        if (tile is Tile.Gate or Tile.Stairs or Tile.UpStairs or Tile.Exit)
            Projectiles.Clear();
        if (tile == Tile.Gate)
        {
            TravelGate(player);
            return;
        }
        if (tile == Tile.Stairs)
        {
            if (_worldMode == "overworld")
            {
                _worldMode = "dungeon";
                Depth = 1;
                Dungeon = GetOrGenerateFloor(1);
                AddEvent($"Отряд вошёл в подземелье, этаж {Depth}");
            }
            else
            {
                Depth++;
                if (Depth > Config.MaxDepth) { Depth = Config.MaxDepth; return; }
                Dungeon = GetOrGenerateFloor(Depth);
                AddEvent($"Отряд спустился на этаж {Depth}");
            }
            PlacePlayerAtEntry(player);
            Quests.UpdateReach(Depth);
        }
        else if (tile == Tile.UpStairs && _worldMode == "dungeon")
        {
            if (Depth <= 1)
            {
                _worldMode = "overworld";
                _currentLocation = null;
                Dungeon = Overworld;
                Depth = 0;
                player.X = Aethelgard.World.Overworld.DungeonPos.Item1;
                player.Y = Aethelgard.World.Overworld.DungeonPos.Item2;
                AddEvent("Отряд вернулся на поверхность");
            }
            else
            {
                Depth--;
                Dungeon = GetOrGenerateFloor(Depth);
                PlacePlayerAtEntry(player);
            }
        }
        else if (tile == Tile.Exit && _worldMode == "dungeon")
        {
            _worldMode = "overworld";
            _currentLocation = null;
            Dungeon = Overworld;
            Depth = 0;
            player.X = Aethelgard.World.Overworld.DungeonPos.Item1;
            player.Y = Aethelgard.World.Overworld.DungeonPos.Item2;
            AddEvent("Отряд вернулся на поверхность");
        }
    }

    /// <summary>Ворота между локациями в MP.</summary>
    private void TravelGate(Player player)
    {
        var pos = (player.X, player.Y);
        if (!CurrentMap.Transitions.TryGetValue(pos, out var transition)) return;
        var (to, _) = transition;

        if (to == "village" || to == "overworld")
        {
            _currentLocation = null;
            _worldMode = "overworld";
            RespawnPlayersNear(Aethelgard.World.Overworld.VillageCenter);
            return;
        }

        if (!Locations.All.ContainsKey(to)) return;
        if (!_locationCache.TryGetValue(to, out var map))
        {
            map = Locations.GenerateLocation(to);
            _locationCache[to] = map;
        }
        _currentLocation = map;
        _worldMode = "overworld";
        RespawnPlayersNear((1, map.H / 2));
        AddEvent($"Отряд вошёл в: {Locations.All[to].Name}");
    }

    /// <summary>Респавн игроков у входа, без общих клеток (test_net.py).</summary>
    private void RespawnPlayersNear((int X, int Y) center)
    {
        var taken = new HashSet<(int, int)>();
        foreach (var p in Players.Values)
        {
            for (int r = 0; r < 12; r++)
            {
                bool placed = false;
                for (int dy = -r; dy <= r && !placed; dy++)
                    for (int dx = -r; dx <= r && !placed; dx++)
                    {
                        int x = center.X + dx, y = center.Y + dy;
                        if (x < 0 || x >= CurrentMap.W || y < 0 || y >= CurrentMap.H) continue;
                        if (!CurrentMap.IsWalkable(x, y)) continue;
                        if (CurrentMap.MonsterAt(x, y) != null) continue;
                        if (taken.Contains((x, y))) continue;
                        p.X = x; p.Y = y;
                        taken.Add((x, y));
                        placed = true;
                    }
                if (placed) break;
            }
        }
    }

    private Dungeon GetOrGenerateFloor(int depth)
    {
        if (_floorCache.TryGetValue(depth, out var floor))
            return floor;
        var newFloor = DungeonGen.Generate(depth);
        _floorCache[depth] = newFloor;
        return newFloor;
    }

    private void PlacePlayerAtEntry(Player player)
    {
        for (int y = 0; y < Dungeon.H; y++)
            for (int x = 0; x < Dungeon.W; x++)
            {
                var t = Dungeon.Tiles[y][x];
                if (t == Tile.Exit || t == Tile.UpStairs)
                {
                    player.X = x;
                    player.Y = y;
                    return;
                }
            }
        player.X = Dungeon.W / 2;
        player.Y = Dungeon.H / 2;
    }

    /// <summary>
    /// Update game logic: monster AI, statuses, regen, harvest regrow.
    /// Реген: +1 MP всегда, +1 HP — только с перком regen.
    /// </summary>
    public void Update(float dt)
    {
        // Общий ИИ монстров (тот же, что в синглплеере). Дальняя атака
        // наносит урон сразу, а снаряд летит как визуальный эффект.
        MonsterAI.Update(CurrentMap, Players.Values.ToList(), dt,
            onRanged: (m, p) =>
            {
                Rules.ResolveAttack(m, p);
                Projectiles.Add(ProjectileSystem.Create(
                    m, p, CombatSystem.ProjectileKindFor(m), 0, visual: true));
            },
            onBreath: (m, _, _) => AddEvent($"{m.Name} выдыхает огонь!"));

        UpdateProjectiles(dt);

        // Status tick
        _statusAccum += dt;
        if (_statusAccum >= Config.StatusTickInterval)
        {
            _statusAccum = 0;
            foreach (var p in Players.Values) p.TickStatuses();
            foreach (var m in CurrentMap.Monsters) m.TickStatuses();
        }

        // Regen
        _regenTimer += dt;
        if (_regenTimer >= Config.RegenTickInterval)
        {
            _regenTimer = 0;
            foreach (var p in Players.Values)
            {
                if (!p.Alive) continue;
                if (p.Hp < p.MaxHp && p.Regen)
                    p.Hp = Math.Min(p.MaxHp, p.Hp + Config.RegenHpPerTick);
                if (p.Mana < p.MaxMana)
                    p.Mana = Math.Min(p.MaxMana, p.Mana + Config.RegenManaPerTick);
            }
        }

        // Respawn dead players with full HP (test_net.py: respawn)
        foreach (var p in Players.Values)
        {
            if (!p.Alive)
            {
                p.Hp = p.MaxHp;
                p.Mana = p.MaxMana;
                p.Statuses.Clear();
                AddEvent("Игрок возрождён");
            }
        }

        // Harvest regrow
        HarvestSystem.Tick(CurrentMap);

        // Clear old events
        if (Events.Count > 10)
            Events.RemoveRange(0, Events.Count - 10);
    }

    /// <summary>Движение визуальных снарядов; урон наносится в момент каста.</summary>
    private void UpdateProjectiles(float dt)
    {
        var players = Players.Values.ToList();
        for (int i = Projectiles.Count - 1; i >= 0; i--)
        {
            var p = Projectiles[i];
            IReadOnlyList<Entity> targets = p.SourcePid == -1
                ? players
                : CurrentMap.Monsters;
            if (ProjectileSystem.Update(p, dt, CurrentMap, targets))
                Projectiles.RemoveAt(i);
        }
    }

    private float _statusAccum;

    private void AddEvent(string msg)
    {
        Events.Add(msg);
    }

    /// <summary>
    /// Build a snapshot for a specific client (their view).
    /// NPC и сток торговца включены (test_mp_ui/test_new_features).
    /// </summary>
    public Snapshot BuildSnapshot(int forPid)
    {
        var snap = new Snapshot
        {
            Depth = Depth,
            MapW = CurrentMap.W,
            MapH = CurrentMap.H,
            WorldMode = _worldMode,
            StairsPos = CurrentMap.StairsPos is { } sp
                ? new PointState { X = sp.Item1, Y = sp.Item2 }
                : null,
            MyPid = forPid,
        };

        // Карта тайлов (стены/лестницы/ворота рисуются как в сингле)
        foreach (var row in CurrentMap.Tiles)
            snap.Tiles.Add(new string(row.Select(t => (char)('0' + (int)t)).ToArray()));

        // Живые узлы ресурсов (E — добыть)
        foreach (var ((hx, hy), rec) in CurrentMap.Harvestables)
            if (rec.Has) snap.Harvests.Add(new PointState { X = hx, Y = hy });

        // Players (расширенный набор полей как в snapshot_builder.py)
        foreach (var (pid, p) in Players)
        {
            snap.Players.Add(new Snapshot.PlayerState
            {
                Pid = pid,
                X = p.X, Y = p.Y,
                Hp = p.Hp, MaxHp = p.MaxHp,
                Mana = p.Mana, MaxMana = p.MaxMana,
                Level = p.Level,
                Xp = p.Xp, XpNext = p.XpNext,
                Gold = p.Gold,
                ClassKey = p.ClassKey,
                ClassName = p.ClassName,
                Ch = p.Ch,
                Color = p.Color,
                Alive = p.Alive,
                Atk = p.Atk, Df = p.Df,
                FreePoints = p.FreePoints,
                SkillPoints = p.SkillPoints,
                ClassTier = p.ClassTier,
                Statuses = new Dictionary<string, int>(p.Statuses),
                Inventory = new Dictionary<string, int>(p.Inventory),
                Str = p.TotalStr, Agi = p.TotalAgi, Vit = p.TotalVit,
                Luk = p.TotalLuk, DfStat = p.TotalDfStat, IntStat = p.TotalInt,
                Weapon = p.Weapon, Armor = p.Armor, Head = p.Head,
                Legs = p.Legs, Boots = p.Boots,
                Quickbar = p.Quickbar.ToArray(),
            });
        }

        // Monsters
        foreach (var m in CurrentMap.Monsters)
        {
            snap.Monsters.Add(new Snapshot.MonsterState
            {
                X = m.X, Y = m.Y,
                Hp = m.Hp, MaxHp = m.MaxHp,
                Ch = m.Ch,
                Color = m.Color,
                Boss = m.Boss,
                Name = m.Name,
            });
        }

        // Items
        foreach (var ((x, y), key) in CurrentMap.Items)
            snap.Items.Add(new Snapshot.ItemState { X = x, Y = y, Key = key });

        // Projectiles (визуальные снаряды — рисуются клиентом)
        foreach (var p in Projectiles)
            snap.Projectiles.Add(new Snapshot.ProjectileState
            {
                X = p.X, Y = p.Y, Kind = p.Kind, Color = p.Color,
            });

        // NPCs (городские + торговец/доска подземелья)
        foreach (var m in CurrentMap.Monsters)
        {
            if (m.IsNpc && m.Alive)
                snap.Npcs.Add(new Snapshot.NpcEntry
                {
                    X = m.X, Y = m.Y, Ch = m.Ch, Color = m.Color,
                    Name = m.Name, NpcType = m.NpcType ?? "",
                });
        }

        // Merchant stock
        if (_worldMode == "dungeon" && CurrentMap.MerchantStock != null)
        {
            foreach (var e in CurrentMap.MerchantStock)
                snap.MerchantStock.Add(new Snapshot.ShopEntry { Key = e.Key, Price = e.Price });
        }

        // Shared quest log
        foreach (var kv in Quests.Active)
            snap.Quests.Add($"{kv.Value.Name}: {kv.Value.Progress}/{kv.Value.Count}");

        // Доска квестов для панели L (available + active + completed с ключами)
        foreach (var key in Quests.Available)
        {
            var def = QuestData.Get(key);
            snap.QuestBoard.Add(new Snapshot.QuestBoardEntry
            {
                Key = key, Name = def?.Name ?? key, Progress = 0,
                Count = def?.Count ?? 1, State = "available",
            });
        }
        foreach (var gen in Quests.GeneratedBoard)
            snap.QuestBoard.Add(new Snapshot.QuestBoardEntry
            {
                Key = gen.Key, Name = gen.Name, Progress = 0,
                Count = gen.Count, State = "available",
            });
        foreach (var kv in Quests.Active)
            snap.QuestBoard.Add(new Snapshot.QuestBoardEntry
            {
                Key = kv.Key, Name = kv.Value.Name, Progress = kv.Value.Progress,
                Count = kv.Value.Count, State = "active",
            });
        foreach (var key in Quests.Completed)
        {
            var def = QuestData.Get(key);
            snap.QuestBoard.Add(new Snapshot.QuestBoardEntry
            {
                Key = key, Name = def?.Name ?? key,
                Progress = def?.Count ?? 1, Count = def?.Count ?? 1,
                State = "completed",
            });
        }

        // Дерево навыков и доступные повышения (панель N)
        if (Players.TryGetValue(forPid, out var myPlayer))
        {
            foreach (var node in SkillTree.GetNodes(myPlayer))
                snap.Skills.Add(new Snapshot.SkillEntry
                {
                    Name = node.Name, Level = node.Level,
                    Learned = myPlayer.LearnedNodes.Contains(node.Key),
                });
            foreach (var sk in myPlayer.ActiveSkills())
                snap.ActiveSkills.Add(new Snapshot.ActiveSkillEntry
                {
                    Name = sk.Name, Mana = sk.Mana,
                });
            snap.Promotions.AddRange(ClassData.AvailablePromotions(myPlayer));
        }

        // Events
        snap.Events.AddRange(Events);

        // Chat
        foreach (var c in ChatLog)
            snap.Chat.Add(new Snapshot.ChatEntry { Pid = c.Pid, Text = c.Text });

        return snap;
    }

    /// <summary>
    /// Сохранение мира MP-сессии хостом (net/core.py:save_world):
    /// savegame_mp_world.json (мета мира) + savegame_mp_players.json (игроки).
    /// </summary>
    public int SaveWorld()
    {
        try
        {
            var dir = Config.DataDir;
            var worldPath = Path.Combine(dir, "savegame_mp_world.json");
            var world = new Dictionary<string, object?>
            {
                ["version"] = 8,
                ["world_mode"] = _worldMode,
                ["depth"] = Depth,
                ["map_w"] = CurrentMap.W,
                ["map_h"] = CurrentMap.H,
                ["tiles"] = CurrentMap.Tiles.Select(r => new string(r.Select(t => (char)('0' + (int)t)).ToArray())).ToList(),
                ["monsters"] = CurrentMap.Monsters.Select(m => new Dictionary<string, object?>
                {
                    ["key"] = m.MonsterKey, ["name"] = m.Name, ["ch"] = m.Ch,
                    ["x"] = m.X, ["y"] = m.Y, ["hp"] = m.Hp, ["max_hp"] = m.MaxHp,
                }).ToList(),
                ["items"] = CurrentMap.Items.Select(kv => new Dictionary<string, object?>
                {
                    ["x"] = kv.Key.Item1, ["y"] = kv.Key.Item2, ["key"] = kv.Value,
                }).ToList(),
                ["harvestables"] = HarvestSystem.Serialize(CurrentMap),
                ["quests"] = Quests.Active.Select(kv => new Dictionary<string, object?>
                {
                    ["key"] = kv.Key, ["progress"] = kv.Value.Progress, ["count"] = kv.Value.Count,
                }).ToList(),
                // Полное состояние всех посещённых этажей и локаций: без этого
                // после загрузки терялись открытые сундуки, лут, ресурсы и
                // ассортимент торговцев на других картах.
                ["floors"] = _floorCache.OrderBy(kv => kv.Key).Select(kv => new Dictionary<string, object?>
                {
                    ["depth"] = kv.Key,
                    ["floor"] = SaveSystem.FloorToData(kv.Key, kv.Value),
                }).ToList(),
                ["locations"] = _locationCache.OrderBy(kv => kv.Key).Select(kv => new Dictionary<string, object?>
                {
                    ["key"] = kv.Key,
                    ["floor"] = SaveSystem.FloorToData(0, kv.Value),
                }).ToList(),
                ["current_location"] = _currentLocation?.LocationKey,
                ["saved_at"] = DateTime.UtcNow.ToString("o"),
            };
            File.WriteAllText(worldPath, JsonSerializer.Serialize(world, new JsonSerializerOptions { WriteIndented = true }));

            var playersPath = Path.Combine(dir, "savegame_mp_players.json");
            var players = new Dictionary<string, object?>();
            foreach (var (pid, p) in Players)
            {
                players[pid.ToString()] = new Dictionary<string, object?>
                {
                    ["class_key"] = p.ClassKey, ["class_name"] = p.ClassName, ["class_tier"] = p.ClassTier,
                    ["level"] = p.Level, ["xp"] = p.Xp, ["gold"] = p.Gold,
                    ["hp"] = p.Hp, ["mana"] = p.Mana,
                    ["str"] = p.Str, ["agi"] = p.Agi, ["vit"] = p.Vit,
                    ["luk"] = p.Luk, ["df_stat"] = p.DfStat, ["int"] = p.IntStat,
                    ["inventory"] = p.Inventory,
                    ["equipment"] = new Dictionary<string, object?>
                    {
                        ["weapon"] = p.Weapon, ["armor"] = p.Armor, ["head"] = p.Head,
                        ["legs"] = p.Legs, ["boots"] = p.Boots,
                    },
                    ["skill_nodes"] = p.LearnedNodes.ToList(),
                    ["skill_points"] = p.SkillPoints,
                    ["blueprints"] = p.Blueprints.ToList(),
                };
            }
            File.WriteAllText(playersPath, JsonSerializer.Serialize(players, new JsonSerializerOptions { WriteIndented = true }));
            AddEvent("Мир сохранён.");
            return Players.Count;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>Сохранённые игроки (восстанавливаются при «Продолжить игру»).</summary>
    public Dictionary<int, Dictionary<string, object?>> SavedPlayers = new();

    /// <summary>
    /// Загрузка мира MP-сессии из сохранения (net/core.py:load_world):
    /// savegame_mp_world.json + savegame_mp_players.json.
    /// Восстанавливает тайлы/существ/предметы/ресурсы/квесты текущей карты,
    /// игроков — по PID при AddPlayer.
    /// </summary>
    public bool LoadWorld()
    {
        try
        {
            var worldPath = Path.Combine(Config.DataDir, "savegame_mp_world.json");
            if (!File.Exists(worldPath)) return false;

            using var doc = JsonDocument.Parse(File.ReadAllText(worldPath));
            var root = doc.RootElement;

            if (root.TryGetProperty("world_mode", out var wm))
                _worldMode = wm.GetString() ?? "overworld";
            if (root.TryGetProperty("depth", out var d))
                Depth = d.GetInt32();

            string? currentLocation = root.TryGetProperty("current_location", out var cl)
                ? cl.GetString()
                : null;

            // Все посещённые этажи подземелья.
            _floorCache.Clear();
            if (root.TryGetProperty("floors", out var floors) && floors.ValueKind == JsonValueKind.Array)
            {
                foreach (var fe in floors.EnumerateArray())
                {
                    try
                    {
                        int fdepth = fe.TryGetProperty("depth", out var dep) ? dep.GetInt32() : 0;
                        if (!fe.TryGetProperty("floor", out var fl)) continue;
                        var fd = JsonSerializer.Deserialize<SaveSystem.FloorData>(fl.GetRawText());
                        var floor = fd == null ? null : SaveSystem.FloorFromData(fd);
                        if (floor != null) _floorCache[fdepth] = floor;
                    }
                    catch { /* повреждённая запись — пропустить */ }
                }
            }

            // Все посещённые локации.
            _locationCache.Clear();
            if (root.TryGetProperty("locations", out var locations) && locations.ValueKind == JsonValueKind.Array)
            {
                foreach (var le in locations.EnumerateArray())
                {
                    try
                    {
                        string key = le.TryGetProperty("key", out var kk) ? kk.GetString() ?? "" : "";
                        if (key.Length == 0 || !le.TryGetProperty("floor", out var fl)) continue;
                        var fd = JsonSerializer.Deserialize<SaveSystem.FloorData>(fl.GetRawText());
                        var loc = fd == null ? null : SaveSystem.FloorFromData(fd);
                        if (loc != null)
                        {
                            loc.LocationKey = key;
                            _locationCache[key] = loc;
                        }
                    }
                    catch { /* повреждённая запись — пропустить */ }
                }
            }

            if (_worldMode == "dungeon")
            {
                if (_floorCache.TryGetValue(Depth, out var savedFloor))
                    Dungeon = savedFloor;
                else if (Depth > 0)
                    Dungeon = GetOrGenerateFloor(Depth);
            }

            _currentLocation = currentLocation != null
                && _locationCache.TryGetValue(currentLocation, out var savedLocation)
                    ? savedLocation
                    : null;

            var map = CurrentMap;
            if (root.TryGetProperty("map_w", out var mw) && root.TryGetProperty("map_h", out var mh)
                && (mw.GetInt32() != map.W || mh.GetInt32() != map.H))
            {
                map = new Dungeon(mw.GetInt32(), mh.GetInt32()) { LocationKey = currentLocation };
                if (_worldMode == "dungeon")
                    Dungeon = map;
                else if (_currentLocation != null && currentLocation != null)
                {
                    _locationCache[currentLocation] = map;
                    _currentLocation = map;
                }
                else
                    Overworld = map;
            }

            // Тайлы строками цифр
            if (root.TryGetProperty("tiles", out var tiles) && tiles.ValueKind == JsonValueKind.Array)
            {
                int y = 0;
                foreach (var row in tiles.EnumerateArray())
                {
                    string line = row.GetString() ?? "";
                    if (y >= map.H) break;
                    for (int x = 0; x < map.W && x < line.Length; x++)
                        if (Enum.TryParse<Tile>(line[x].ToString(), out var t))
                            map.Tiles[y][x] = t;
                    y++;
                }
            }

            // Монстры: HP/позиции по ключу вида
            if (root.TryGetProperty("monsters", out var monsters) && monsters.ValueKind == JsonValueKind.Array)
            {
                var byKey = map.Monsters
                    .Where(m => !m.IsNpc)
                    .GroupBy(m => m.MonsterKey)
                    .ToDictionary(g => g.Key, g => g.ToList());
                var used = new HashSet<Entity>();
                foreach (var me in monsters.EnumerateArray())
                {
                    string key = me.TryGetProperty("key", out var k) ? k.GetString() ?? "" : "";
                    if (!byKey.TryGetValue(key, out var candidates)) continue;
                    Entity? e = candidates.FirstOrDefault(c => !used.Contains(c));
                    if (e == null) continue;
                    used.Add(e);
                    if (me.TryGetProperty("x", out var mx)) e.X = mx.GetInt32();
                    if (me.TryGetProperty("y", out var my)) e.Y = my.GetInt32();
                    if (me.TryGetProperty("hp", out var mhp)) e.Hp = mhp.GetInt32();
                    if (me.TryGetProperty("max_hp", out var mmhp)) e.MaxHp = mmhp.GetInt32();
                    if (e.Hp <= 0) e.Hp = 0;   // смерть фиксирует общий Update (hp <= 0)
                }
            }

            // Предметы на земле
            map.Items.Clear();
            if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var ie in items.EnumerateArray())
                {
                    int x = ie.TryGetProperty("x", out var ix) ? ix.GetInt32() : 0;
                    int yy = ie.TryGetProperty("y", out var iy) ? iy.GetInt32() : 0;
                    string key = ie.TryGetProperty("key", out var ik) ? ik.GetString() ?? "" : "";
                    if (key.Length > 0) map.Items[(x, yy)] = key;
                }
            }

            // Ресурсы для добычи
            if (root.TryGetProperty("harvestables", out var harvest) && harvest.ValueKind == JsonValueKind.Array)
            {
                map.Harvestables.Clear();
                foreach (var he in harvest.EnumerateArray())
                {
                    try
                    {
                        int hx = he.TryGetProperty("x", out var hxv) ? hxv.GetInt32() : 0;
                        int hy = he.TryGetProperty("y", out var hyv) ? hyv.GetInt32() : 0;
                        map.Harvestables[(hx, hy)] = new HarvestRecord
                        {
                            Kind = he.TryGetProperty("kind", out var kv) ? kv.GetString() ?? "bush" : "bush",
                            Hp = he.TryGetProperty("hp", out var hv) ? hv.GetInt32() : 1,
                            MaxHp = he.TryGetProperty("max_hp", out var mv) ? mv.GetInt32() : 1,
                            Has = !he.TryGetProperty("has", out var hov) || hov.GetBoolean(),
                            ReadyAt = he.TryGetProperty("ready_at", out var rv) ? rv.GetDouble() : 0,
                            SpawnedAt = he.TryGetProperty("spawned_at", out var sv) ? sv.GetDouble() : 0,
                        };
                    }
                    catch { /* повреждённая запись — пропустить */ }
                }
            }

            // Квесты отряда
            if (root.TryGetProperty("quests", out var quests) && quests.ValueKind == JsonValueKind.Array)
            {
                Quests.Active.Clear();
                foreach (var qe in quests.EnumerateArray())
                {
                    string key = qe.TryGetProperty("key", out var qk) ? qk.GetString() ?? "" : "";
                    if (key.Length == 0) continue;
                    var def = QuestData.Get(key);
                    Quests.Active[key] = new QuestState
                    {
                        Progress = qe.TryGetProperty("progress", out var pr) ? pr.GetInt32() : 0,
                        Count = qe.TryGetProperty("count", out var cn) ? cn.GetInt32() : def?.Count ?? 1,
                        Generated = def?.Generated ?? false,
                        Type = def?.Type ?? "",
                        Target = def?.Target ?? "",
                        Name = def?.Name ?? "",
                        Desc = def?.Desc ?? "",
                        RewardGold = def?.RewardGold ?? 0,
                        RewardXp = def?.RewardXp ?? 0,
                    };
                }
            }

            // Сохранённые игроки (для восстановления при повторном входе)
            SavedPlayers.Clear();
            var playersPath = Path.Combine(Config.DataDir, "savegame_mp_players.json");
            if (File.Exists(playersPath))
            {
                using var pdoc = JsonDocument.Parse(File.ReadAllText(playersPath));
                foreach (var pe in pdoc.RootElement.EnumerateObject())
                    SavedPlayers[int.Parse(pe.Name)] = JsonSerializer
                        .Deserialize<Dictionary<string, object?>>(pe.Value.GetRawText())
                        ?? new Dictionary<string, object?>();
            }

            Rules.InitActionTimers(CurrentMap.Monsters);
            AddEvent("Мир загружен из сохранения.");
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Восстановить сохранённого игрока по PID (вызывается из AddPlayer).
    /// </summary>
    public void RestoreSavedPlayer(int pid, Player player)
    {
        if (!SavedPlayers.TryGetValue(pid, out var s)) return;
        try
        {
            player.ClassKey = s.TryGetValue("class_key", out var ck) ? ck?.ToString() ?? player.ClassKey : player.ClassKey;
            player.ClassName = s.TryGetValue("class_name", out var cn) ? cn?.ToString() ?? player.ClassName : player.ClassName;
            player.ClassTier = s.TryGetValue("class_tier", out var ct) ? int.Parse(ct?.ToString() ?? "1") : player.ClassTier;
            player.Level = s.TryGetValue("level", out var lv) ? int.Parse(lv?.ToString() ?? "1") : player.Level;
            player.Xp = s.TryGetValue("xp", out var xp) ? int.Parse(xp?.ToString() ?? "0") : player.Xp;
            player.Gold = s.TryGetValue("gold", out var gd) ? int.Parse(gd?.ToString() ?? "0") : player.Gold;
            player.Hp = s.TryGetValue("hp", out var hp) ? int.Parse(hp?.ToString() ?? "1") : player.Hp;
            player.Mana = s.TryGetValue("mana", out var mn) ? int.Parse(mn?.ToString() ?? "0") : player.Mana;
            player.Str = s.TryGetValue("str", out var st) ? int.Parse(st?.ToString() ?? "5") : player.Str;
            player.Agi = s.TryGetValue("agi", out var ag) ? int.Parse(ag?.ToString() ?? "5") : player.Agi;
            player.Vit = s.TryGetValue("vit", out var vt) ? int.Parse(vt?.ToString() ?? "5") : player.Vit;
            player.Luk = s.TryGetValue("luk", out var lk) ? int.Parse(lk?.ToString() ?? "5") : player.Luk;
            player.DfStat = s.TryGetValue("df_stat", out var df) ? int.Parse(df?.ToString() ?? "5") : player.DfStat;
            player.IntStat = s.TryGetValue("int", out var it) ? int.Parse(it?.ToString() ?? "5") : player.IntStat;
            if (s.TryGetValue("inventory", out var inv))
            {
                var items = JsonSerializer.Deserialize<Dictionary<string, int>>(((JsonElement?)inv)?.GetRawText() ?? "{}");
                if (items != null) player.Inventory = items;
            }
            if (s.TryGetValue("equipment", out var eq) && eq is JsonElement eqEl)
            {
                var eqDict = JsonSerializer.Deserialize<Dictionary<string, string?>>(eqEl.GetRawText());
                if (eqDict != null)
                {
                    player.Weapon = eqDict.GetValueOrDefault("weapon");
                    player.Armor = eqDict.GetValueOrDefault("armor");
                    player.Head = eqDict.GetValueOrDefault("head");
                    player.Legs = eqDict.GetValueOrDefault("legs");
                    player.Boots = eqDict.GetValueOrDefault("boots");
                }
            }
            if (s.TryGetValue("skill_nodes", out var sn) && sn is JsonElement snEl)
            {
                var nodes = JsonSerializer.Deserialize<List<string>>(snEl.GetRawText());
                if (nodes != null) player.LearnedNodes = new HashSet<string>(nodes);
            }
            if (s.TryGetValue("skill_points", out var sp))
                player.SkillPoints = int.Parse(sp?.ToString() ?? "0");
            if (s.TryGetValue("blueprints", out var bp) && bp is JsonElement bpEl)
            {
                var bps = JsonSerializer.Deserialize<List<string>>(bpEl.GetRawText());
                if (bps != null) player.Blueprints = new HashSet<string>(bps);
            }
            player.Recalc();
        }
        catch
        {
            // повреждённая запись игрока — оставляем нового
        }
    }
}
