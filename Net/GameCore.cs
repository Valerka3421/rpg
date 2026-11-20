using System;
using System.Collections.Generic;
using System.Linq;
using Aethelgard.Core;

namespace Aethelgard.Net;

/// <summary>
/// Authoritative server game state.
/// Ported from Python net/core.py:GameCore.
///
/// Uses Core.Rules for combat — same rules as single-player.
/// Manages: players (by PID), dungeon, monsters, items, events.
/// </summary>
public class GameCore
{
    public Dictionary<int, Player> Players = new();
    public Dungeon Overworld;  // Note: shadows Core.Overworld static class
    public Dungeon Dungeon;
    public Dungeon CurrentMap => _worldMode == "dungeon" ? Dungeon : Overworld;
    public string WorldMode => _worldMode;
    public int Depth = 0;
    public List<string> Events = new();
    public List<(int Pid, string Text)> ChatLog = new();

    private string _worldMode = "overworld";
    private readonly Dictionary<int, Dungeon> _floorCache = new();
    private float _snapshotTimer;
    private float _guardTimer;

    public GameCore()
    {
        Overworld = World.GenerateOverworld();
        Dungeon = Overworld;
        // Spawn monsters
        Rules.InitActionTimers(Overworld.Monsters);
    }

    /// <summary>
    /// Add a new player to the game.
    /// </summary>
    public Player AddPlayer(int pid, string classKey)
    {
        var player = new Player(classKey);
        player.X = Core.Overworld.VillageCenter.Item1;
        player.Y = Core.Overworld.VillageCenter.Item2;

        // Give starting items
        var classDef = ClassData.Get(classKey);
        if (classDef != null)
        {
            player.ClassName = classDef.Name;
            player.Str += classDef.StrBonus;
            player.Agi += classDef.AgiBonus;
            player.Vit += classDef.VitBonus;
            player.Luk += classDef.LukBonus;
            player.IntStat += classDef.IntBonus;
            player.Recalc();
            player.Hp = player.MaxHp;
            player.Mana = player.MaxMana;
            foreach (var itemKey in classDef.StartItems)
            {
                player.AddItem(itemKey, 1);
                var itemDef = ItemData.Get(itemKey);
                if (itemDef != null &&
                    (itemDef.Type == ItemData.ItemType.Weapon
                     || itemDef.Type == ItemData.ItemType.Armor))
                    player.EquipItem(itemKey);
            }
        }

        Players[pid] = player;
        AddEvent($"Player {pid} joined as {classKey}");
        return player;
    }

    /// <summary>
    /// Remove a player.
    /// </summary>
    public void RemovePlayer(int pid)
    {
        Players.Remove(pid);
        AddEvent($"Player {pid} left");
    }

    /// <summary>
    /// Process input from a client.
    /// </summary>
    public void ProcessInput(int pid, ClientMsg msg)
    {
        if (!Players.TryGetValue(pid, out var player)) return;

        if (msg.Dx != null && msg.Dy != null)
        {
            TryMovePlayer(player, msg.Dx.Value, msg.Dy.Value);
        }

        if (msg.Attack == true)
        {
            PlayerAttack(player);
        }

        if (msg.UseStairs == true)
        {
            UseStairs(player);
        }

        if (msg.UsePotion == true)
        {
            if (player.UseItem("potion_hp_s"))
                AddEvent($"Player {pid} healed");
        }

        if (msg.ChatText != null)
        {
            ChatLog.Add((pid, msg.ChatText));
            if (ChatLog.Count > 20)
                ChatLog.RemoveAt(0);
        }
    }

    private void TryMovePlayer(Player player, int dx, int dy)
    {
        int nx = player.X + dx;
        int ny = player.Y + dy;

        if (nx < 0 || nx >= CurrentMap.W || ny < 0 || ny >= CurrentMap.H)
            return;

        // Check for monster
        var monster = CurrentMap.MonsterAt(nx, ny);
        if (monster != null)
        {
            var result = Rules.ResolveAttack(player, monster);
            if (!monster.Alive)
            {
                Rules.AwardKill(player, monster);
                CurrentMap.Monsters.Remove(monster);
            }
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

        player.X = nx;
        player.Y = ny;

        // Auto-pickup
        if (CurrentMap.Items.TryGetValue((nx, ny), out var itemKey))
        {
            player.AddItem(itemKey, 1);
            CurrentMap.Items.Remove((nx, ny));
        }
    }

    private void PlayerAttack(Player player)
    {
        Entity? target = null;
        int bestDist = int.MaxValue;
        foreach (var m in CurrentMap.Monsters)
        {
            if (!m.Alive) continue;
            int dist = Math.Max(Math.Abs(m.X - player.X), Math.Abs(m.Y - player.Y));
            if (dist <= 1 && dist < bestDist)
            {
                bestDist = dist;
                target = m;
            }
        }

        if (target != null)
        {
            var result = Rules.ResolveAttack(player, target);
            if (!target.Alive)
            {
                Rules.AwardKill(player, target);
                CurrentMap.Monsters.Remove(target);
            }
        }
    }

    private void UseStairs(Player player)
    {
        var tile = CurrentMap.Tiles[player.Y][player.X];
        if (tile == Tile.Stairs)
        {
            if (_worldMode == "overworld")
            {
                _worldMode = "dungeon";
                Depth = 1;
                Dungeon = GetOrGenerateFloor(1);
                AddEvent($"Player entered dungeon floor {Depth}");
            }
            else
            {
                Depth++;
                if (Depth > Config.MaxDepth) { Depth = Config.MaxDepth; return; }
                Dungeon = GetOrGenerateFloor(Depth);
                AddEvent($"Player descended to floor {Depth}");
            }
            PlacePlayerAtEntry(player);
        }
        else if (tile == Tile.Exit && _worldMode == "dungeon")
        {
            _worldMode = "overworld";
            Dungeon = Overworld;
            Depth = 0;
            player.X = Core.Overworld.DungeonPos.Item1;
            player.Y = Core.Overworld.DungeonPos.Item2;
            AddEvent("Player returned to surface");
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
    /// Update game logic: monster AI, regen, guard.
    /// </summary>
    public void Update(float dt)
    {
        // Monster AI
        foreach (var m in CurrentMap.Monsters)
        {
            if (!m.Alive) continue;
            m.ActionTimer -= dt;
            if (m.ActionTimer > 0) continue;
            m.ActionTimer = Config.MonsterBaseCooldown;

            // Find nearest player
            Player? nearest = null;
            int bestDist = int.MaxValue;
            foreach (var p in Players.Values)
            {
                if (!p.Alive) continue;
                int dist = Math.Max(Math.Abs(m.X - p.X), Math.Abs(m.Y - p.Y));
                if (dist < bestDist) { bestDist = dist; nearest = p; }
            }

            if (nearest == null) continue;

            if (bestDist <= 1)
            {
                Rules.ResolveAttack(m, nearest);
            }
            else if (bestDist <= 8)
            {
                int dx = Math.Sign(nearest.X - m.X);
                int dy = Math.Sign(nearest.Y - m.Y);
                int nx = m.X + dx, ny = m.Y + dy;
                if (CurrentMap.IsWalkable(nx, ny)
                    && CurrentMap.MonsterAt(nx, ny) == null
                    && !Players.Values.Any(p => p.X == nx && p.Y == ny))
                {
                    m.X = nx; m.Y = ny;
                }
            }
        }

        CurrentMap.Monsters.RemoveAll(m => !m.Alive);

        // Player regen
        foreach (var p in Players.Values)
        {
            if (p.Hp <= 0) continue;
            if (p.Hp < p.MaxHp)
                p.Hp = Math.Min(p.MaxHp, p.Hp + 1);
        }

        // Clear old events
        if (Events.Count > 10)
            Events.RemoveRange(0, Events.Count - 10);
    }

    private void AddEvent(string msg)
    {
        Events.Add(msg);
    }

    /// <summary>
    /// Build a snapshot for a specific client (their view).
    /// </summary>
    public Snapshot BuildSnapshot(int forPid)
    {
        var snap = new Snapshot
        {
            Depth = Depth,
            MapW = CurrentMap.W,
            MapH = CurrentMap.H,
            WorldMode = _worldMode,
            StairsPos = CurrentMap.StairsPos,
            MyPid = forPid,
        };

        // Players
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
            snap.Items.Add((x, y, key));

        // Events
        snap.Events.AddRange(Events);

        // Chat
        snap.Chat.AddRange(ChatLog);

        return snap;
    }
}
