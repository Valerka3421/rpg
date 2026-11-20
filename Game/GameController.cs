using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Aethelgard.Core;
using Aethelgard.Net;
using Aethelgard.Audio;

namespace Aethelgard.Game;

/// <summary>
/// Game state + input handling + update loop.
/// Ported from Python game/game/ package.
///
/// Modes: "overworld" (surface), "dungeon" (underground), "inventory" (modal).
/// </summary>
public class GameController
{
    public Dungeon Overworld;
    public Dungeon Dungeon;
    public Dungeon CurrentMap => _worldMode == "dungeon" ? Dungeon : Overworld;
    public Player Player;
    public string ScreenMode = "class_select";
    // "class_select", "game", "inventory", "skills", "shop", "quests", "dialogue",
    // "mp_menu", "mp_game"
    public string WorldMode => _worldMode;
    public int Depth = 0;
    public float Fps;
    public string? LastMessage;
    public float MessageTimer;

    // Multiplayer
    public bool IsMultiplayer = false;
    public GameClient? MpClient;
    public Snapshot? MpSnapshot;
    public string ChatInput = "";
    public bool ChatActive = false;
    public List<string> ChatLog = new();

    // Dev console
    public DevConsole Console = new();

    // Audio
    public AudioSystem? Audio;

    // Skill screen state
    public int SkillTab = 0;

    // City NPCs
    public List<Entity> CityNpcs = new();
    public Entity? QuestBoard;
    public Entity? Elder;
    public string? ActiveShopNpc;  // "blacksmith", "alchemist", "innkeeper"
    public string ShopSubMode = "buy";  // "buy" or "sell"
    public int ShopPage = 0;

    // Quest log
    public QuestLog Quests = new();
    public int QuestSelected = 0;

    // Dialogue
    public string? DialogueText;
    public string? DialogueNpc;

    private string _worldMode = "overworld";
    private float _moveCooldown;
    private double _regenTimer;
    private double _statusTimer;
    private float _guardTimer;

    // Dungeon floor cache (for returning upstairs)
    private readonly Dictionary<int, Dungeon> _floorCache = new();

    public GameController()
    {
        Overworld = new Dungeon(Config.MapW, Config.MapH);
        Dungeon = Overworld;
        Player = new Player();
    }

    /// <summary>
    /// Start a new game with the selected class.
    /// </summary>
    public void NewGame(string classKey = "warrior")
    {
        Overworld = World.GenerateOverworld();
        Player = new Player(classKey);

        // Apply class starting stat bonuses
        var classDef = ClassData.Get(classKey);
        if (classDef != null)
        {
            Player.ClassName = classDef.Name;
            Player.Str += classDef.StrBonus;
            Player.Agi += classDef.AgiBonus;
            Player.Vit += classDef.VitBonus;
            Player.Luk += classDef.LukBonus;
            Player.IntStat += classDef.IntBonus;
            Player.DfStat += classDef.DfStatBonus;
            Player.ClassTier = classDef.Tier;
            Player.Recalc();
            Player.Hp = Player.MaxHp;
            Player.Mana = Player.MaxMana;

            // Give starting items
            foreach (var itemKey in classDef.StartItems)
            {
                Player.AddItem(itemKey, 1);
                // Auto-equip first weapon/armor
                var itemDef = ItemData.Get(itemKey);
                if (itemDef != null &&
                    (itemDef.Type == ItemData.ItemType.Weapon
                     || itemDef.Type == ItemData.ItemType.Armor
                     || itemDef.Type == ItemData.ItemType.Helmet))
                {
                    Player.EquipItem(itemKey);
                }
            }
        }

        var (cx, cy) = Core.Overworld.VillageCenter;
        Player.X = cx;
        Player.Y = cy;

        if (Overworld.Tiles[cy][cx] == Tile.Wall)
            Overworld.Tiles[cy][cx] = Tile.Floor;

        Dungeon = Overworld;
        _worldMode = "overworld";
        Depth = 0;
        ScreenMode = "game";
        _floorCache.Clear();

        // Initialize craft levels
        Player.CraftLevels["blacksmith"] = 1;
        Player.CraftLevels["alchemy"] = 1;
        Player.CraftLevels["magic_engineering"] = 1;

        // Spawn city NPCs
        SpawnCityNpcs();

        // Spawn quest board and elder
        var (bx, by) = CityData.QuestBoardPos;
        QuestBoard = new Entity(bx, by, 1, 1, 0, 0, "Quest Board", 'B',
            new Color(206, 170, 96), 0)
        { IsNpc = true, NpcType = "quest_board" };
        // Ensure board tile is floor
        if (Overworld.Tiles[by][bx] == Tile.Wall)
            Overworld.Tiles[by][bx] = Tile.Floor;

        var (ex2, ey2) = CityData.ElderPos;
        Elder = new Entity(ex2, ey2, 1, 1, 0, 0, "Village Elder", 'E',
            new Color(180, 160, 100), 0)
        { IsNpc = true, NpcType = "elder" };
        if (Overworld.Tiles[ey2][ex2] == Tile.Wall)
            Overworld.Tiles[ey2][ex2] = Tile.Floor;

        // Populate quest board
        Quests.PopulateBoard();

        Rules.InitActionTimers(Dungeon.Monsters);
        ShowMessage($"Welcome, {Player.ClassName}!");
    }

    /// <summary>
    /// Handle keyboard input.
    /// </summary>
    public void HandleInput(KeyboardState keys, KeyboardState prevKeys, float dt)
    {
        // Class select mode — handled by ClassSelectScreen via Game1
        if (ScreenMode == "class_select") return;

        // Multiplayer game mode — input goes to client
        if (ScreenMode == "mp_game")
        {
            HandleMpInput(keys, prevKeys);
            return;
        }

        // Dev console (F12 or `)
        if (keys.IsKeyDown(Keys.F12) && !prevKeys.IsKeyDown(Keys.F12))
        {
            Console.TryOpen();
            return;
        }
        if (keys.IsKeyDown(Keys.OemTilde) && !prevKeys.IsKeyDown(Keys.OemTilde))
        {
            Console.TryOpen();
            return;
        }

        // If console is open, handle console input
        if (Console.Open)
        {
            HandleConsoleInput(keys, prevKeys);
            return;
        }

        // F5 = quick save, F9 = quick load
        if (keys.IsKeyDown(Keys.F5) && !prevKeys.IsKeyDown(Keys.F5))
        {
            bool ok = SaveSystem.Save(this, 1);
            ShowMessage(ok ? "Saved to slot 1" : "Save failed!");
            Audio?.Play("menu_move");
            return;
        }
        if (keys.IsKeyDown(Keys.F9) && !prevKeys.IsKeyDown(Keys.F9))
        {
            var data = SaveSystem.Load(1);
            if (data != null)
            {
                SaveSystem.ApplyLoad(this, data);
                ShowMessage("Loaded from slot 1");
                Audio?.Play("pickup");
            }
            else
                ShowMessage("No save in slot 1");
            return;
        }

        // Toggle inventory
        if (keys.IsKeyDown(Keys.I) && !prevKeys.IsKeyDown(Keys.I))
        {
            ScreenMode = ScreenMode == "inventory" ? "game" : "inventory";
            return;
        }

        // Toggle skills screen
        if (keys.IsKeyDown(Keys.N) && !prevKeys.IsKeyDown(Keys.N))
        {
            ScreenMode = ScreenMode == "skills" ? "game" : "skills";
            return;
        }

        // Talk to NPC (T)
        if (keys.IsKeyDown(Keys.T) && !prevKeys.IsKeyDown(Keys.T))
        {
            TalkToNpc();
            return;
        }

        if (ScreenMode == "inventory")
        {
            HandleInventoryInput(keys, prevKeys);
            return;
        }

        if (ScreenMode == "skills")
        {
            HandleSkillsInput(keys, prevKeys);
            return;
        }

        if (ScreenMode == "shop")
        {
            HandleShopInput(keys, prevKeys);
            return;
        }

        if (ScreenMode == "quests")
        {
            HandleQuestInput(keys, prevKeys);
            return;
        }

        if (ScreenMode == "dialogue")
        {
            bool anyPressed = keys.IsKeyDown(Keys.Enter) || keys.IsKeyDown(Keys.T)
                || keys.IsKeyDown(Keys.Escape);
            bool wasPressed = prevKeys.IsKeyDown(Keys.Enter) || prevKeys.IsKeyDown(Keys.T)
                || prevKeys.IsKeyDown(Keys.Escape);
            if (anyPressed && !wasPressed)
                ScreenMode = "game";
            return;
        }

        // Game mode input
        _moveCooldown -= dt;

        int dx = 0, dy = 0;
        if (keys.IsKeyDown(Keys.W) || keys.IsKeyDown(Keys.Up)) dy = -1;
        else if (keys.IsKeyDown(Keys.S) || keys.IsKeyDown(Keys.Down)) dy = 1;
        else if (keys.IsKeyDown(Keys.A) || keys.IsKeyDown(Keys.Left)) dx = -1;
        else if (keys.IsKeyDown(Keys.D) || keys.IsKeyDown(Keys.Right)) dx = 1;

        if ((dx != 0 || dy != 0) && _moveCooldown <= 0)
        {
            TryMove(dx, dy);
            _moveCooldown = Config.PlayerMoveCooldown;
        }

        // Attack (Space)
        if (keys.IsKeyDown(Keys.Space) && !prevKeys.IsKeyDown(Keys.Space))
            Attack();

        // F — use stairs/exit (no longer auto-trigger)
        if (keys.IsKeyDown(Keys.F) && !prevKeys.IsKeyDown(Keys.F))
            UseStairs();

        // B — bigmap
        if (keys.IsKeyDown(Keys.B) && !prevKeys.IsKeyDown(Keys.B))
        {
            ScreenMode = ScreenMode == "bigmap" ? "game" : "bigmap";
            return;
        }

        // Use quick potion (H)
        if (keys.IsKeyDown(Keys.H) && !prevKeys.IsKeyDown(Keys.H))
        {
            if (Player.UseItem("potion_hp_s"))
                ShowMessage("Drank healing potion (+10 HP)");
            else if (Player.UseItem("potion_hp_m"))
                ShowMessage("Drank healing potion (+25 HP)");
            else
                ShowMessage("No healing potions!");
        }
    }

    /// <summary>
    /// Handle input in inventory mode.
    /// </summary>
    private void HandleInventoryInput(KeyboardState keys, KeyboardState prevKeys)
    {
        // Page navigation
        if (keys.IsKeyDown(Keys.Q) && !prevKeys.IsKeyDown(Keys.Q))
            CurrentPage = Math.Max(0, CurrentPage - 1);
        if (keys.IsKeyDown(Keys.E) && !prevKeys.IsKeyDown(Keys.E))
            CurrentPage++;

        // Number keys 1-8: use/equip item in slot
        for (int i = 0; i < 8; i++)
        {
            var key = Keys.D1 + i;
            if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key))
            {
                UseItemByIndex(i);
            }
        }
    }

    public int CurrentPage = 0;

    /// <summary>
    /// Handle input in skills screen mode.
    /// </summary>
    private void HandleSkillsInput(KeyboardState keys, KeyboardState prevKeys)
    {
        // Tab switching: Q/E
        if (keys.IsKeyDown(Keys.Q) && !prevKeys.IsKeyDown(Keys.Q))
            SkillTab = Math.Max(0, SkillTab - 1);
        if (keys.IsKeyDown(Keys.E) && !prevKeys.IsKeyDown(Keys.E))
            SkillTab = Math.Min(3, SkillTab + 1);

        // Number keys 1-7: learn class skill node
        if (SkillTab == 0)
        {
            for (int i = 0; i < 7; i++)
            {
                var key = Keys.D1 + i;
                if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key))
                {
                    TryLearnNode(i);
                }
            }
        }

        // Class promotion: P key (when available)
        if (keys.IsKeyDown(Keys.P) && !prevKeys.IsKeyDown(Keys.P))
        {
            TryPromoteClass();
        }
    }

    /// <summary>
    /// Handle input in shop mode.
    /// </summary>
    private void HandleShopInput(KeyboardState keys, KeyboardState prevKeys)
    {
        // S: toggle buy/sell
        if (keys.IsKeyDown(Keys.S) && !prevKeys.IsKeyDown(Keys.S))
        {
            ShopSubMode = ShopSubMode == "buy" ? "sell" : "buy";
            ShopPage = 0;
        }

        // Q/E: pages
        if (keys.IsKeyDown(Keys.Q) && !prevKeys.IsKeyDown(Keys.Q))
            ShopPage = Math.Max(0, ShopPage - 1);
        if (keys.IsKeyDown(Keys.E) && !prevKeys.IsKeyDown(Keys.E))
            ShopPage++;

        // 1-8: buy or sell item
        for (int i = 0; i < 8; i++)
        {
            var key = Keys.D1 + i;
            if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key))
            {
                if (ShopSubMode == "buy")
                    BuyItem(i);
                else
                    SellItem(i);
            }
        }
    }

    /// <summary>
    /// Handle input in quests mode.
    /// </summary>
    private void HandleQuestInput(KeyboardState keys, KeyboardState prevKeys)
    {
        // Up/Down: select quest
        if (keys.IsKeyDown(Keys.Up) && !prevKeys.IsKeyDown(Keys.Up))
            QuestSelected = Math.Max(0, QuestSelected - 1);
        if (keys.IsKeyDown(Keys.Down) && !prevKeys.IsKeyDown(Keys.Down))
            QuestSelected++;

        // Enter: accept or turn in
        if (keys.IsKeyDown(Keys.Enter) && !prevKeys.IsKeyDown(Keys.Enter))
        {
            var available = Quests.Available.ToList();
            var completed = Quests.Completed.ToList();

            if (QuestSelected < available.Count)
                AcceptQuest(QuestSelected);
            else
            {
                int completedIdx = QuestSelected - available.Count;
                if (completedIdx >= 0 && completedIdx < completed.Count)
                    TurnInQuest(completedIdx);
            }
        }
    }

    /// <summary>
    /// Try to learn a skill node by index (0-6).
    /// </summary>
    private void TryLearnNode(int index)
    {
        var nodes = SkillTree.GetNodes(Player);
        if (index < 0 || index >= nodes.Length) return;

        var node = nodes[index];
        if (SkillTree.LearnNode(Player, node.Key))
            ShowMessage($"Learned: {node.Name}");
        else if (Player.LearnedNodes.Contains(node.Key))
            ShowMessage("Already learned.");
        else if (Player.Level < node.Level)
            ShowMessage($"Requires level {node.Level}.");
        else
            ShowMessage("No skill points!");
    }

    /// <summary>
    /// Try to promote to the first available class.
    /// </summary>
    private void TryPromoteClass()
    {
        var promotions = ClassData.AvailablePromotions(Player);
        if (promotions.Length == 0)
        {
            ShowMessage("No class promotion available.");
            return;
        }

        // Auto-promote to first available (in full port, show a selection UI)
        string promoKey = promotions[0];
        if (ClassData.Promote(Player, promoKey))
        {
            var def = ClassData.Get(promoKey);
            ShowMessage($"Promoted to {def?.Name}!");
        }
    }

    /// <summary>
    /// Use or equip item at given index on current inventory page.
    /// </summary>
    private void UseItemByIndex(int index)
    {
        int pageStart = CurrentPage * 80; // 10×8 grid
        var items = Player.Inventory.ToList();
        int idx = pageStart + index;
        if (idx >= items.Count) return;

        var (key, _) = items[idx];
        var def = ItemData.Get(key);
        if (def == null) return;

        switch (def.Type)
        {
            case ItemData.ItemType.Weapon:
            case ItemData.ItemType.Armor:
            case ItemData.ItemType.Helmet:
            case ItemData.ItemType.Leggings:
            case ItemData.ItemType.Boots:
                if (Player.EquipItem(key))
                    ShowMessage($"Equipped {def.Name}");
                break;
            case ItemData.ItemType.Potion:
            case ItemData.ItemType.Food:
                if (Player.UseItem(key))
                    ShowMessage($"Used {def.Name}");
                break;
            case ItemData.ItemType.Book:
                if (Player.UseItem(key))
                    ShowMessage($"Studied {def.Name} (+{def.Value} {def.Stat})");
                break;
        }
    }

    /// <summary>
    /// Try to move player by (dx, dy).
    /// Stairs NO LONGER auto-trigger — press F to use them.
    /// Boss arena: player locked inside while boss is alive.
    /// </summary>
    private void TryMove(int dx, int dy)
    {
        int nx = Player.X + dx;
        int ny = Player.Y + dy;

        // Set facing direction for ranged attacks
        Player.Facing = (dx, dy);

        if (nx < 0 || nx >= CurrentMap.W || ny < 0 || ny >= CurrentMap.H)
            return;

        // Check for monster — attack
        var monster = CurrentMap.MonsterAt(nx, ny);
        if (monster != null)
        {
            AttackTarget(monster);
            return;
        }

        var tile = CurrentMap.Tiles[ny][nx];

        // Locked door — try to unlock
        if (tile == Tile.LockedDoor)
        {
            if (Rules.TryUnlockDoor(Player, CurrentMap, nx, ny))
                ShowMessage("Unlocked door with Boss Key!");
            else
                ShowMessage("Locked. Need a Boss Key.");
            return;
        }

        // Chest — open on walk
        if (tile == Tile.Chest)
        {
            var loot = Rules.OpenChest(CurrentMap, Player, nx, ny);
            if (loot != null)
                ShowMessage($"Found: {ItemData.DisplayName(loot)}!");
            return;
        }

        // Wall blocks
        if (tile == Tile.Wall)
            return;

        // Boss arena check: player can't leave arena while boss is alive
        if (IsInBossArena(Player.X, Player.Y) && !IsInBossArena(nx, ny))
        {
            ShowMessage("The boss won't let you leave the arena!");
            Audio?.Play("error");
            return;
        }

        // Move
        Player.X = nx;
        Player.Y = ny;
        Player.StepCounter++;

        // Stairs do NOT auto-trigger — press F to use
        // Gate message only
        if (tile == Tile.Gate && _worldMode == "overworld")
        {
            ShowMessage("The gate leads beyond the known world... (press F to travel)");
        }

        // Auto-pickup items
        if (CurrentMap.Items.TryGetValue((nx, ny), out var itemKey))
        {
            Player.AddItem(itemKey, 1);
            CurrentMap.Items.Remove((nx, ny));
            ShowMessage($"Picked up: {ItemData.DisplayName(itemKey)}");
            Audio?.Play("pickup");
        }
    }

    /// <summary>
    /// Check if a position is inside the boss arena (last room while boss is alive).
    /// Ported from Python _boss_arena().
    /// </summary>
    private bool IsInBossArena(int x, int y)
    {
        if (_worldMode != "dungeon") return false;
        bool bossAlive = CurrentMap.Monsters.Any(m => m.Boss && m.Alive);
        if (!bossAlive) return false;
        if (CurrentMap.Rooms.Count == 0) return false;
        var arena = CurrentMap.Rooms[^1];
        return x >= arena.X && x < arena.X + arena.W
            && y >= arena.Y && y < arena.Y + arena.H;
    }

    /// <summary>
    /// Use stairs — only by pressing F key (not auto-trigger).
    /// Ported from Python use_stairs().
    /// </summary>
    public void UseStairs()
    {
        var tile = CurrentMap.Tiles[Player.Y][Player.X];

        if (_worldMode == "overworld")
        {
            if (tile != Tile.Stairs)
            {
                ShowMessage("No stairs here.");
                return;
            }
            EnterDungeon();
            return;
        }

        // Dungeon mode
        if (tile == Tile.Stairs)
        {
            if (Depth >= Config.MaxDepth)
            {
                ShowMessage("Can't go deeper.");
                return;
            }
            Audio?.Play("stairs");
            DescendFloor();
        }
        else if (tile == Tile.UpStairs)
        {
            AscendFloor();
        }
        else if (tile == Tile.Exit)
        {
            ExitToOverworld();
        }
        else
        {
            ShowMessage("No stairs here.");
        }
    }

    /// <summary>
    /// Enter dungeon from overworld stairs.
    /// </summary>
    private void EnterDungeon()
    {
        _worldMode = "dungeon";
        Depth = 1;
        Dungeon = GetOrGenerateFloor(1);
        PlacePlayerAtEntry();
        Quests.UpdateReach(Depth);
        ShowMessage($"Entered dungeon floor {Depth}");
    }

    /// <summary>
    /// Descend to next floor.
    /// </summary>
    private void DescendFloor()
    {
        // Cache current floor
        _floorCache[Depth] = Dungeon;

        Depth++;
        if (Depth > Config.MaxDepth)
        {
            Depth = Config.MaxDepth;
            ShowMessage("Deepest floor reached.");
            return;
        }

        Dungeon = GetOrGenerateFloor(Depth);
        PlacePlayerAtEntry();
        Quests.UpdateReach(Depth);
        ShowMessage($"Descended to floor {Depth}");
    }

    /// <summary>
    /// Ascend to previous floor.
    /// </summary>
    private void AscendFloor()
    {
        if (Depth <= 1)
        {
            ExitToOverworld();
            return;
        }

        // Cache current floor
        _floorCache[Depth] = Dungeon;

        Depth--;
        Dungeon = GetOrGenerateFloor(Depth);
        // Place at stairs-down position
        if (Dungeon.StairsPos.HasValue)
        {
            Player.X = Dungeon.StairsPos.Value.Item1;
            Player.Y = Dungeon.StairsPos.Value.Item2;
        }
        ShowMessage($"Ascended to floor {Depth}");
    }

    /// <summary>
    /// Exit dungeon back to overworld.
    /// </summary>
    private void ExitToOverworld()
    {
        _worldMode = "overworld";
        Dungeon = Overworld;
        Depth = 0;
        // Place at dungeon entrance
        Player.X = Core.Overworld.DungeonPos.Item1;
        Player.Y = Core.Overworld.DungeonPos.Item2;
        ShowMessage("Returned to the surface.");
    }

    /// <summary>
    /// Get floor from cache or generate new.
    /// </summary>
    private Dungeon GetOrGenerateFloor(int depth)
    {
        if (_floorCache.TryGetValue(depth, out var floor))
            return floor;
        var newFloor = DungeonGen.Generate(depth);
        _floorCache[depth] = newFloor;
        return newFloor;
    }

    /// <summary>
    /// Place player at entry point of current dungeon floor.
    /// </summary>
    private void PlacePlayerAtEntry()
    {
        // Find Exit or UpStairs tile
        for (int y = 0; y < Dungeon.H; y++)
        {
            for (int x = 0; x < Dungeon.W; x++)
            {
                var t = Dungeon.Tiles[y][x];
                if (t == Tile.Exit || t == Tile.UpStairs)
                {
                    Player.X = x;
                    Player.Y = y;
                    return;
                }
            }
        }
        // Fallback: center
        Player.X = Dungeon.W / 2;
        Player.Y = Dungeon.H / 2;
    }

    /// <summary>
    /// Attack nearest monster.
    /// </summary>
    private void Attack()
    {
        Entity? target = null;
        int bestDist = int.MaxValue;

        foreach (var m in CurrentMap.Monsters)
        {
            if (!m.Alive) continue;
            int dist = Math.Max(Math.Abs(m.X - Player.X), Math.Abs(m.Y - Player.Y));
            if (dist <= 1 && dist < bestDist)
            {
                bestDist = dist;
                target = m;
            }
        }

        if (target != null)
            AttackTarget(target);
    }

    private void AttackTarget(Entity target)
    {
        var result = Rules.ResolveAttack(Player, target);

        if (result.Dodged)
        {
            ShowMessage($"{target.Name} dodged!");
            return;
        }

        if (!target.Alive)
        {
            string msg = $"{target.Name} slain!";
            if (target.Boss)
                msg = $"Boss {target.Name} defeated!";
            Rules.AwardKill(Player, target);
            Audio?.Play(target.Boss ? "boss" : "hit");

            // Quest kill tracking
            Quests.UpdateKill(target.Ch);

            CurrentMap.Monsters.Remove(target);
            ShowMessage(msg);
        }
        else
        {
            Audio?.Play("attack");
        }
    }

    /// <summary>
    /// Update game logic: monsters, status effects, regen.
    /// </summary>
    public void Update(float dt)
    {
        if (ScreenMode != "game") return;

        // Guard AI (overworld only)
        UpdateGuard(dt);

        // Status tick
        _statusTimer += dt;
        if (_statusTimer >= Config.StatusTickInterval)
        {
            _statusTimer = 0;
            Player.TickStatuses();
            foreach (var m in CurrentMap.Monsters)
                m.TickStatuses();
        }

        // Regen tick
        _regenTimer += dt;
        if (_regenTimer >= Config.RegenTickInterval)
        {
            _regenTimer = 0;
            if (Player.Hp < Player.MaxHp && Player.Alive)
                Player.Hp = Math.Min(Player.MaxHp, Player.Hp + 1);
            if (Player.Mana < Player.MaxMana)
                Player.Mana = Math.Min(Player.MaxMana, Player.Mana + 1);
        }

        // Monster AI
        UpdateMonsters(dt);

        // Message timer
        if (MessageTimer > 0)
            MessageTimer -= dt;
    }

    private void UpdateMonsters(float dt)
    {
        foreach (var m in CurrentMap.Monsters)
        {
            if (!m.Alive) continue;
            m.ActionTimer -= dt;
            if (m.ActionTimer > 0) continue;

            m.ActionTimer = Config.MonsterBaseCooldown;

            int dist = Math.Max(Math.Abs(m.X - Player.X), Math.Abs(m.Y - Player.Y));
            if (dist <= 1)
            {
                var result = Rules.ResolveAttack(m, Player);
                if (result.Damage > 0 && !result.Dodged)
                {
                    // Player took damage
                }
            }
            else if (dist <= 8)
            {
                MoveMonsterTowardPlayer(m);
            }
        }

        CurrentMap.Monsters.RemoveAll(m => !m.Alive);
    }

    private void MoveMonsterTowardPlayer(Entity m)
    {
        int dx = Math.Sign(Player.X - m.X);
        int dy = Math.Sign(Player.Y - m.Y);

        // Try diagonal first
        int nx = m.X + dx, ny = m.Y + dy;
        if (CurrentMap.IsWalkable(nx, ny) && CurrentMap.MonsterAt(nx, ny) == null
            && !(nx == Player.X && ny == Player.Y))
        {
            m.X = nx; m.Y = ny;
            return;
        }

        // Try cardinal
        if (dx != 0 && CurrentMap.IsWalkable(m.X + dx, m.Y)
            && CurrentMap.MonsterAt(m.X + dx, m.Y) == null
            && !(m.X + dx == Player.X && m.Y == Player.Y))
        {
            m.X += dx;
            return;
        }
        if (dy != 0 && CurrentMap.IsWalkable(m.X, m.Y + dy)
            && CurrentMap.MonsterAt(m.X, m.Y + dy) == null
            && !(m.X == Player.X && m.Y + dy == Player.Y))
        {
            m.Y += dy;
        }
    }

    private void ShowMessage(string msg)
    {
        LastMessage = msg;
        MessageTimer = 3.0f;
    }

    // ── Dev Console Input ──

    /// <summary>
    /// Handle input when dev console is open.
    /// </summary>
    private void HandleConsoleInput(KeyboardState keys, KeyboardState prevKeys)
    {
        // Enter: submit
        if (keys.IsKeyDown(Keys.Enter) && !prevKeys.IsKeyDown(Keys.Enter))
        {
            string result = Console.Submit(this);
            if (result.Length > 0)
                Console.Output = result;
            Audio?.Play("menu_move");
            return;
        }

        // Escape: close
        if (keys.IsKeyDown(Keys.Escape) && !prevKeys.IsKeyDown(Keys.Escape))
        {
            Console.Open = false;
            return;
        }

        // Backspace
        if (keys.IsKeyDown(Keys.Back) && !prevKeys.IsKeyDown(Keys.Back))
        {
            if (Console.Buffer.Length > 0)
                Console.Buffer = Console.Buffer[..^1];
            return;
        }

        // History navigation
        if (keys.IsKeyDown(Keys.Up) && !prevKeys.IsKeyDown(Keys.Up))
        {
            var hist = Console.HistoryUp();
            if (hist != null) Console.Buffer = hist;
            return;
        }
        if (keys.IsKeyDown(Keys.Down) && !prevKeys.IsKeyDown(Keys.Down))
        {
            var hist = Console.HistoryDown();
            if (hist != null) Console.Buffer = hist;
            return;
        }

        // Text input — simplified (handles basic letters, numbers, space)
        // In a full implementation, use TextInput event
        var keyMap = new Dictionary<Keys, char>
        {
            { Keys.A, 'a' }, { Keys.B, 'b' }, { Keys.C, 'c' }, { Keys.D, 'd' },
            { Keys.E, 'e' }, { Keys.F, 'f' }, { Keys.G, 'g' }, { Keys.H, 'h' },
            { Keys.I, 'i' }, { Keys.J, 'j' }, { Keys.K, 'k' }, { Keys.L, 'l' },
            { Keys.M, 'm' }, { Keys.N, 'n' }, { Keys.O, 'o' }, { Keys.P, 'p' },
            { Keys.Q, 'q' }, { Keys.R, 'r' }, { Keys.S, 's' }, { Keys.T, 't' },
            { Keys.U, 'u' }, { Keys.V, 'v' }, { Keys.W, 'w' }, { Keys.X, 'x' },
            { Keys.Y, 'y' }, { Keys.Z, 'z' }, { Keys.Space, ' ' },
            { Keys.D0, '0' }, { Keys.D1, '1' }, { Keys.D2, '2' }, { Keys.D3, '3' },
            { Keys.D4, '4' }, { Keys.D5, '5' }, { Keys.D6, '6' }, { Keys.D7, '7' },
            { Keys.D8, '8' }, { Keys.D9, '9' }, { Keys.OemMinus, '-' },
            { Keys.OemComma, ',' }, { Keys.OemPeriod, '.' },
        };

        foreach (var (key, ch) in keyMap)
        {
            if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key))
            {
                char c = ch;
                // Shift = uppercase
                if ((keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift))
                    && char.IsLetter(c))
                    c = char.ToUpper(c);
                Console.Buffer += c;
                return;
            }
        }
    }

    /// <summary>
    /// Play sound for an event.
    /// </summary>
    public void PlaySound(string name)
    {
        Audio?.Play(name);
    }

    // ── Multiplayer ──

    /// <summary>
    /// Start multiplayer game as client.
    /// </summary>
    public void StartMultiplayer(GameClient client)
    {
        MpClient = client;
        IsMultiplayer = true;
        ScreenMode = "mp_game";
        Player = new Player(client.MyPid >= 0 ? "warrior" : "warrior");
    }

    /// <summary>
    /// Handle input in multiplayer mode — send to server.
    /// </summary>
    private void HandleMpInput(KeyboardState keys, KeyboardState prevKeys)
    {
        if (MpClient == null) return;

        // Chat toggle (Y)
        if (keys.IsKeyDown(Keys.Y) && !prevKeys.IsKeyDown(Keys.Y))
        {
            ChatActive = !ChatActive;
            ChatInput = "";
            return;
        }

        // If typing chat, only handle Enter/Escape
        if (ChatActive)
        {
            if (keys.IsKeyDown(Keys.Enter) && !prevKeys.IsKeyDown(Keys.Enter))
            {
                if (ChatInput.Length > 0)
                {
                    MpClient.SendChat(ChatInput);
                    ChatLog.Add($"[me] {ChatInput}");
                }
                ChatActive = false;
                ChatInput = "";
                return;
            }
            if (keys.IsKeyDown(Keys.Escape) && !prevKeys.IsKeyDown(Keys.Escape))
            {
                ChatActive = false;
                ChatInput = "";
                return;
            }
            // Basic text input — in a full implementation, use proper text input
            // For now, this is a placeholder that accepts printable chars
            return;
        }

        // Movement
        int dx = 0, dy = 0;
        if (keys.IsKeyDown(Keys.W) || keys.IsKeyDown(Keys.Up)) dy = -1;
        else if (keys.IsKeyDown(Keys.S) || keys.IsKeyDown(Keys.Down)) dy = 1;
        else if (keys.IsKeyDown(Keys.A) || keys.IsKeyDown(Keys.Left)) dx = -1;
        else if (keys.IsKeyDown(Keys.D) || keys.IsKeyDown(Keys.Right)) dx = 1;

        if ((dx != 0 || dy != 0) && _moveCooldown <= 0)
        {
            MpClient.SendInput(dx, dy);
            _moveCooldown = Config.PlayerMoveCooldown;
        }

        // Attack
        if (keys.IsKeyDown(Keys.Space) && !prevKeys.IsKeyDown(Keys.Space))
            MpClient.SendAttack();

        // Use stairs
        if (keys.IsKeyDown(Keys.E) && !prevKeys.IsKeyDown(Keys.E))
            MpClient.SendUseStairs();

        // Use potion
        if (keys.IsKeyDown(Keys.H) && !prevKeys.IsKeyDown(Keys.H))
            MpClient.SendUsePotion();
    }

    /// <summary>
    /// Update multiplayer state — fetch latest snapshot.
    /// </summary>
    public void UpdateMp()
    {
        if (MpClient == null) return;

        MpSnapshot = MpClient.GetSnapshot();

        if (MpSnapshot != null)
        {
            // Update chat log from snapshot
            foreach (var (pid, text) in MpSnapshot.Chat)
            {
                string entry = $"[P{pid}] {text}";
                if (!ChatLog.Contains(entry))
                    ChatLog.Add(entry);
            }
            if (ChatLog.Count > 20)
                ChatLog.RemoveRange(0, ChatLog.Count - 20);
        }

        // Message timer
        if (MessageTimer > 0)
            MessageTimer -= 0.016f;

        // Move cooldown
        _moveCooldown -= 0.016f;
    }

    // ── City NPCs ──

    private void SpawnCityNpcs()
    {
        CityNpcs.Clear();
        foreach (var def in CityData.Npcs.Values)
        {
            var npc = new Entity(def.Pos.X, def.Pos.Y, def.IsGuard ? 50 : 10,
                def.IsGuard ? 50 : 10, def.IsGuard ? 3 : 0, 0,
                def.Name, def.Ch, def.Color, 0)
            {
                IsNpc = true,
                NpcType = def.Key,
                Immortal = def.IsGuard,
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
    /// Get all NPCs on the current map.
    /// </summary>
    public List<Entity> AllNpcs()
    {
        var npcs = new List<Entity>();
        if (QuestBoard != null) npcs.Add(QuestBoard);
        if (Elder != null) npcs.Add(Elder);
        if (_worldMode == "overworld")
            npcs.AddRange(CityNpcs);
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
        ScreenMode = "shop";
    }

    /// <summary>
    /// Buy item from shop by index.
    /// </summary>
    public void BuyItem(int index)
    {
        if (ActiveShopNpc == null) return;
        var npcDef = CityData.Npcs.GetValueOrDefault(ActiveShopNpc);
        if (npcDef == null) return;
        if (index < 0 || index >= npcDef.ShopStock.Length) return;

        string itemKey = npcDef.ShopStock[index];
        var itemDef = ItemData.Get(itemKey);
        if (itemDef == null) return;

        if (Player.Gold < itemDef.Price)
        {
            ShowMessage("Not enough gold!");
            return;
        }

        Player.Gold -= itemDef.Price;
        Player.AddItem(itemKey, 1);
        ShowMessage($"Bought {itemDef.Name} for {itemDef.Price}g");
    }

    /// <summary>
    /// Sell item from inventory by index.
    /// </summary>
    public void SellItem(int index)
    {
        var items = Player.Inventory.ToList();
        int pageStart = ShopPage * 80;
        int idx = pageStart + index;
        if (idx >= items.Count) return;

        var (key, _) = items[idx];
        var itemDef = ItemData.Get(key);
        if (itemDef == null) return;
        if (itemDef.SellPrice <= 0)
        {
            ShowMessage("Cannot sell this item.");
            return;
        }

        Player.Gold += itemDef.SellPrice;
        Player.RemoveItem(key, 1);
        ShowMessage($"Sold {itemDef.Name} for {itemDef.SellPrice}g");
    }

    // ── Quests ──

    /// <summary>
    /// Accept quest from the board by index.
    /// </summary>
    public void AcceptQuest(int index)
    {
        var available = Quests.Available.ToList();
        if (index < 0 || index >= available.Count) return;

        if (Quests.Accept(available[index]))
        {
            var def = QuestData.Get(available[index]);
            ShowMessage($"Accepted quest: {def?.Name}");
        }
    }

    /// <summary>
    /// Turn in a completed quest by index.
    /// </summary>
    public void TurnInQuest(int index)
    {
        var completed = Quests.Completed.ToList();
        if (index < 0 || index >= completed.Count) return;

        if (Quests.TurnIn(completed[index], Player))
        {
            var def = QuestData.Get(completed[index]);
            ShowMessage($"Quest complete: {def?.Name}! (+{def?.RewardGold}g, +{def?.RewardXp} XP)");
        }
    }

    // ── Dialogue ──

    /// <summary>
    /// Talk to nearby NPC.
    /// </summary>
    public void TalkToNpc()
    {
        var npc = NearbyNpc();
        if (npc == null) return;

        if (npc.NpcType == "quest_board")
        {
            ScreenMode = "quests";
            return;
        }

        if (npc.NpcType == "elder")
        {
            DialogueNpc = "Village Elder";
            DialogueText = "Greetings, adventurer. The dungeon to the northeast holds great dangers... and great treasures. Visit the quest board if you seek work.";
            ScreenMode = "dialogue";
            return;
        }

        // Shop NPCs
        if (npc.NpcType != null && CityData.Npcs.ContainsKey(npc.NpcType))
        {
            if (npc.NpcType == "guard")
            {
                DialogueNpc = "City Guard";
                DialogueText = "I keep watch over the gates. Monsters rarely approach the city walls, but stay vigilant in the wilds.";
                ScreenMode = "dialogue";
            }
            else
            {
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
        _guardTimer = 0;

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
