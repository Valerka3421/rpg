using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Screens = Aethelgard.Systems.ScreenMode;
namespace Aethelgard.Systems;

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
    public Dungeon CurrentMap =>
        _worldMode == "dungeon" ? Dungeon
        : _currentLocation != null ? _currentLocation
        : Overworld;
    public Player Player;
    public string ScreenMode = Screens.ClassSelect;
    // Константы экранов — Systems/Modes.cs (ScreenMode).
    public string WorldMode => _worldMode;
    public int Depth = 0;
    public float Fps;
    /// <summary>Показывать FPS (настройка show_fps, командой fps в консоли).</summary>
    /// <summary>Настройки игры (settings.json) — единый объект.</summary>
    public Settings Settings = Settings.Load();
    public bool ShowFps = Settings.Load().ShowFps;
    public string? LastMessage;
    public float MessageTimer;

    // Multiplayer
    public bool IsMultiplayer = false;
    public GameClient? MpClient;
    public GameServer? MpServer;
    public Snapshot? MpSnapshot;
    public string ChatInput = "";
    public bool ChatActive = false;
    public List<string> ChatLog = new();

    // Dev console
    public DevConsole Console = new();

    // Главное меню: ожидание старта MP-хоста после выбора класса
    public bool PendingMpHost = false;
    public bool PendingMpFresh = false;

    // Текущий слот сохранения (F5/F9, меню сейвов)
    public int CurrentSlot = 1;

    // Audio
    public AudioSystemCore? Audio;

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

    // Craft state (вкладки, страницы, партия, мини-игра точности)
    public int CraftTab = 0;
    public int CraftPage = 0;
    public int CraftQty = 1;
    public bool MinigameActive;
    public float MinigamePos = 0.5f;
    public float MinigameDir = 1f;
    public float MinigameSpeed = 1.0f;
    public string? MinigameItem;
    public int MinigameRecipeIdx;

    // Stats screen state
    public int StatSelected = 0;

    // Minimap
    public bool MinimapVisible = true;
    private int _lastRevealX = -1, _lastRevealY = -1; // последняя открытая туманом позиция

    // Run statistics (Tab) — parity python run_summary (game.run_summary)
    public int KillsTotal, GoldEarned, ItemsCrafted, QuestsDone;
    public int BossKills, PotionsUsed;
    public Dictionary<string, int> KillsByCh = new();
    public DateTime RunStart = DateTime.Now;
    public bool Victory;

    // Active dungeon shop (торговец на этаже)
    public Entity? DungeonMerchant;
    public bool DungeonShopMode;

    /// <summary>Летящие снаряды (стрелы/файрболы) на текущей карте.</summary>
    public List<Projectile> Projectiles = new();

    // Locations cache (ворота → локации)
    private readonly Dictionary<string, Dungeon> _locationCache = new();
    private Dungeon? _currentLocation;
    public string CurrentLocation => _currentLocation?.LocationKey ?? "village";

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
        Overworld = WorldGen.GenerateOverworld();
        Player = CharacterFactory.Create(classKey);

        var (cx, cy) = Aethelgard.World.Overworld.VillageCenter;
        Player.X = cx;
        Player.Y = cy;

        if (Overworld.Tiles[cy][cx] == Tile.Wall)
            Overworld.Tiles[cy][cx] = Tile.Floor;

        // Открыть туман войны вокруг точки спавна
        Overworld.RevealAround(cx, cy, Config.RevealRadius);

        Dungeon = Overworld;
        _worldMode = "overworld";
        Depth = 0;
        ScreenMode = Screens.Game;
        RunStart = DateTime.Now;
        KillsTotal = 0; BossKills = 0; PotionsUsed = 0;
        GoldEarned = 0; ItemsCrafted = 0; QuestsDone = 0;
        KillsByCh.Clear();
        _floorCache.Clear();

        // Initialize craft levels
        Player.CraftLevels["blacksmith"] = 1;
        Player.CraftLevels["alchemy"] = 1;
        Player.CraftLevels["magic_engineering"] = 1;

        // Spawn city NPCs
        SpawnCityNpcs();

        // Spawn quest board and elder
        var (bx, by) = CityData.QuestBoardPos;
        QuestBoard = new Entity(bx, by, 1, 1, 0, 0, "Доска заданий", 'B',
            new Color(206, 170, 96), 0)
        { IsNpc = true, NpcType = "quest_board" };
        // Ensure board tile is floor
        if (Overworld.Tiles[by][bx] == Tile.Wall)
            Overworld.Tiles[by][bx] = Tile.Floor;

        var (ex2, ey2) = CityData.ElderPos;
        Elder = new Entity(ex2, ey2, 1, 1, 0, 0, "Старейшина", 'E',
            new Color(180, 160, 100), 0)
        { IsNpc = true, NpcType = "elder" };
        if (Overworld.Tiles[ey2][ex2] == Tile.Wall)
            Overworld.Tiles[ey2][ex2] = Tile.Floor;

        // Populate quest board
        Quests.PopulateBoard();

        Rules.InitActionTimers(Dungeon.Monsters);
        ShowMessage($"Добро пожаловать, {Player.ClassName}!");
    }

    /// <summary>
    /// Handle keyboard input.
    /// </summary>
    public void HandleInput(KeyboardState keys, KeyboardState prevKeys, float dt)
    {
        // Class select mode — handled by ClassSelectScreen via AethelgardGame
        if (ScreenMode == Screens.ClassSelect) return;

        // Multiplayer game mode — input goes to client
        if (ScreenMode == Screens.MpGame)
        {
            HandleMpInput(keys, prevKeys);
            return;
        }

        // Dev console (F12 or `) — запрещена в мультиплеере
        if (!IsMultiplayer && keys.IsKeyDown(Keys.F12) && !prevKeys.IsKeyDown(Keys.F12))
        {
            Console.TryOpen();
            return;
        }
        if (!IsMultiplayer && keys.IsKeyDown(Keys.OemTilde) && !prevKeys.IsKeyDown(Keys.OemTilde))
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

        // F5 = быстрое сохранение, F9 = быстрая загрузка (текущий слот)
        if (keys.IsKeyDown(Keys.F5) && !prevKeys.IsKeyDown(Keys.F5))
        {
            bool ok = SaveSystem.Save(this, CurrentSlot);
            ShowMessage(ok ? $"Сохранено в слот {CurrentSlot}" : "Ошибка сохранения!");
            Audio?.Play("menu_move");
            return;
        }
        if (keys.IsKeyDown(Keys.F9) && !prevKeys.IsKeyDown(Keys.F9))
        {
            var data = SaveSystem.Load(CurrentSlot);
            if (data != null)
            {
                SaveSystem.ApplyLoad(this, data);
                ShowMessage($"Загружено из слота {CurrentSlot}");
                Audio?.Play("pickup");
            }
            else
                ShowMessage($"Слот {CurrentSlot} пуст");
            return;
        }

        // Toggle inventory
        if (keys.IsKeyDown(Keys.I) && !prevKeys.IsKeyDown(Keys.I))
        {
            ScreenMode = ScreenMode == Screens.Inventory ? Screens.Game : Screens.Inventory;
            return;
        }

        // Toggle skills screen
        if (keys.IsKeyDown(Keys.N) && !prevKeys.IsKeyDown(Keys.N))
        {
            ScreenMode = ScreenMode == Screens.Skills ? Screens.Game : Screens.Skills;
            return;
        }

        // Craft screen (K)
        if (keys.IsKeyDown(Keys.K) && !prevKeys.IsKeyDown(Keys.K))
        {
            ScreenMode = ScreenMode == Screens.Craft ? Screens.Game : Screens.Craft;
            CraftPage = 0;
            return;
        }

        // Stats screen (C)
        if (keys.IsKeyDown(Keys.C) && !prevKeys.IsKeyDown(Keys.C))
        {
            ScreenMode = ScreenMode == Screens.Stats ? Screens.Game : Screens.Stats;
            return;
        }

        // Quests screen (L) — только рядом с доской поручений, старейшиной
        // или торговцем (раньше открывалась «стоя на воздухе» где угодно)
        if (keys.IsKeyDown(Keys.L) && !prevKeys.IsKeyDown(Keys.L))
        {
            var questNpc = NearbyNpc();
            bool nearQuestGiver = questNpc?.NpcType == "quest_board"
                || questNpc?.NpcType == "elder" || questNpc?.NpcType == "merchant";
            if (nearQuestGiver)
            {
                ScreenMode = ScreenMode == Screens.Quests ? Screens.Game : Screens.Quests;
            }
            else
                ShowMessage("Квесты доступны у доски поручений, старейшины или торговца");
            return;
        }

        // Pause menu (Esc)
        if (keys.IsKeyDown(Keys.Escape) && !prevKeys.IsKeyDown(Keys.Escape))
        {
            ScreenMode = ScreenMode == Screens.Pause ? Screens.Game : Screens.Pause;
            return;
        }

        // Talk to NPC (T)
        if (keys.IsKeyDown(Keys.T) && !prevKeys.IsKeyDown(Keys.T))
        {
            TalkToNpc();
            return;
        }

        if (ScreenMode == Screens.Inventory)
        {
            HandleInventoryInput(keys, prevKeys);
            return;
        }

        if (ScreenMode == Screens.Skills)
        {
            HandleSkillsInput(keys, prevKeys);
            return;
        }

        if (ScreenMode == Screens.Shop)
        {
            HandleShopInput(keys, prevKeys);
            return;
        }

        if (ScreenMode == Screens.Quests)
        {
            HandleQuestInput(keys, prevKeys);
            return;
        }

        if (ScreenMode == Screens.Dialogue)
        {
            bool anyPressed = keys.IsKeyDown(Keys.Enter) || keys.IsKeyDown(Keys.T)
                || keys.IsKeyDown(Keys.Escape);
            bool wasPressed = prevKeys.IsKeyDown(Keys.Enter) || prevKeys.IsKeyDown(Keys.T)
                || prevKeys.IsKeyDown(Keys.Escape);
            if (anyPressed && !wasPressed)
                ScreenMode = Screens.Game;
            return;
        }

        if (ScreenMode == Screens.Craft)
        {
            HandleCraftInput(keys, prevKeys);
            return;
        }

        if (ScreenMode == Screens.Stats)
        {
            HandleStatsInput(keys, prevKeys);
            return;
        }

        if (ScreenMode == Screens.Pause)
        {
            HandlePauseInput(keys, prevKeys);
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

        // Статистика забега (Tab — python stats_run.open)
        if (keys.IsKeyDown(Keys.Tab) && !prevKeys.IsKeyDown(Keys.Tab)
            && ScreenMode == Screens.Game)
        {
            ScreenMode = Screens.StatsRun;
            return;
        }

        // F — use stairs/exit/gate (no longer auto-trigger)
        if (keys.IsKeyDown(Keys.F) && !prevKeys.IsKeyDown(Keys.F))
            UseStairs();

        // E — harvest / interact with resources
        if (keys.IsKeyDown(Keys.E) && !prevKeys.IsKeyDown(Keys.E))
            TryHarvest();

        // B / M — большая карта (как в python: M открывает карту).
        // Миникарта включается в настройках, а не хоткеем.
        if ((keys.IsKeyDown(Keys.B) && !prevKeys.IsKeyDown(Keys.B))
            || (keys.IsKeyDown(Keys.M) && !prevKeys.IsKeyDown(Keys.M)))
        {
            ScreenMode = ScreenMode == Screens.Bigmap ? Screens.Game : Screens.Bigmap;
            return;
        }

        // U/V — unequip helmet/leggings
        if (keys.IsKeyDown(Keys.U) && !prevKeys.IsKeyDown(Keys.U))
        {
            if (Player.UnequipSlot("head")) ShowMessage("Шлем снят.");
        }
        if (keys.IsKeyDown(Keys.V) && !prevKeys.IsKeyDown(Keys.V))
        {
            if (Player.UnequipSlot("legs")) ShowMessage("Поножи сняты.");
        }

        // Active skills: F1–F4, F6–F8, F10
        TryCastSkillKey(keys, prevKeys);

        // Hotbar 1–8: use assigned item
        for (int i = 0; i < 8; i++)
        {
            var key = Keys.D1 + i;
            if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key)
                && ScreenMode == Screens.Game)
            {
                var itemKey = Player.Quickbar[i];
                if (itemKey != null && Player.UseItem(itemKey, CurrentMap))
                {
                    ShowMessage($"Использовано: {ItemData.DisplayName(itemKey)}");
                    if (itemKey.StartsWith("potion")) PotionsUsed++;
                }
            }
        }

        // Use quick potion (H) — только существующие зелья: s → m → l
        if (keys.IsKeyDown(Keys.H) && !prevKeys.IsKeyDown(Keys.H))
        {
            if (ItemSystem.UseBestPotion(Player, CurrentMap) != null)
            {
                ShowMessage("Выпито зелье лечения");
                PotionsUsed++;
            }
            else
                ShowMessage("Нет зелий лечения!");
        }
    }

    // ── Craft screen input ──

    private void HandleCraftInput(KeyboardState keys, KeyboardState prevKeys)
    {
        if (MinigameActive)
        {
            // Space — остановить индикатор точности
            if (keys.IsKeyDown(Keys.Space) && !prevKeys.IsKeyDown(Keys.Space))
                FinishMinigame();
            if (keys.IsKeyDown(Keys.Escape) && !prevKeys.IsKeyDown(Keys.Escape))
                MinigameActive = false;
            return;
        }

        // Tabs: Q/A — назад, E/D — вперёд (4 вкладки: all + 3 ветки)
        if ((keys.IsKeyDown(Keys.Q) && !prevKeys.IsKeyDown(Keys.Q))
            || (keys.IsKeyDown(Keys.A) && !prevKeys.IsKeyDown(Keys.A)))
            CraftTab = (CraftTab + 3) % 4;
        if ((keys.IsKeyDown(Keys.E) && !prevKeys.IsKeyDown(Keys.E))
            || (keys.IsKeyDown(Keys.D) && !prevKeys.IsKeyDown(Keys.D)))
            CraftTab = (CraftTab + 1) % 4;

        // Pages: Left/Right
        if (keys.IsKeyDown(Keys.Left) && !prevKeys.IsKeyDown(Keys.Left))
            CraftPage = Math.Max(0, CraftPage - 1);
        if (keys.IsKeyDown(Keys.Right) && !prevKeys.IsKeyDown(Keys.Right))
            CraftPage++;

        // Batch size [ / ]
        if (keys.IsKeyDown(Keys.OemOpenBrackets) && !prevKeys.IsKeyDown(Keys.OemOpenBrackets))
            CraftQty = Math.Max(1, CraftQty - 1);
        if (keys.IsKeyDown(Keys.OemCloseBrackets) && !prevKeys.IsKeyDown(Keys.OemCloseBrackets))
            CraftQty = Math.Min(9, CraftQty + 1);

        // 1–8: начать крафт (мини-игра)
        for (int i = 0; i < 8; i++)
        {
            var key = Keys.D1 + i;
            if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key))
                StartCraft(i);
        }

        if (keys.IsKeyDown(Keys.K) && !prevKeys.IsKeyDown(Keys.K)
            || keys.IsKeyDown(Keys.Escape) && !prevKeys.IsKeyDown(Keys.Escape))
            ScreenMode = Screens.Game;
    }

    private void HandleStatsInput(KeyboardState keys, KeyboardState prevKeys)
    {
        // Up/Down — выбор характеристики
        if (keys.IsKeyDown(Keys.Up) && !prevKeys.IsKeyDown(Keys.Up))
            StatSelected = Math.Max(0, StatSelected - 1);
        if (keys.IsKeyDown(Keys.Down) && !prevKeys.IsKeyDown(Keys.Down))
            StatSelected = Math.Min(5, StatSelected + 1);

        // Enter / 1–6 — вложить очко
        bool enter = keys.IsKeyDown(Keys.Enter) && !prevKeys.IsKeyDown(Keys.Enter);
        for (int i = 0; i < 6; i++)
        {
            var key = Keys.D1 + i;
            if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key))
                AllocateStat(i);
        }
        if (enter) AllocateStat(StatSelected);

        if (keys.IsKeyDown(Keys.C) && !prevKeys.IsKeyDown(Keys.C)
            || keys.IsKeyDown(Keys.Escape) && !prevKeys.IsKeyDown(Keys.Escape))
            ScreenMode = Screens.Game;
    }

    private void HandlePauseInput(KeyboardState keys, KeyboardState prevKeys)
    {
        if (keys.IsKeyDown(Keys.Escape) && !prevKeys.IsKeyDown(Keys.Escape))
            ScreenMode = Screens.Game;
    }

    /// <summary>Вложить свободное очко в характеристику (0–5).</summary>
    public bool AllocateStat(int index)
    {
        if (Player.FreePoints <= 0)
        {
            ShowMessage("Нет свободных очков!");
            return false;
        }
        if (StatSystem.Allocate(Player, index))
        {
            ShowMessage("Характеристика повышена!");
            return true;
        }
        return false;
    }

    // ── Крафт: список, мини-игра точности, исполнение ──

    /// <summary>
    /// Список крафтабельных предметов для вкладки (0 = all, 1–3 — ветки).
    /// Гейт: уровень ветки ≥ skill_req.
    /// </summary>
    public List<(string Key, ItemData.Def Item)> CraftableList()
    {
        string? branch = CraftTab == 0 ? null : RecipeData.SkillOrder[CraftTab - 1];
        var result = new List<(string, ItemData.Def)>();
        foreach (var (key, item) in ItemData.All)
        {
            if (!item.HasRecipe) continue;
            if (branch != null && ItemData.CategoryOf(key) != branch) continue;
            result.Add((key, item));
        }
        return result.OrderBy(kv => kv.Item2.SkillReq).ThenBy(kv => kv.Item1).ToList();
    }

    /// <summary>Начать крафт предмета по индексу на странице (мини-игра).</summary>
    public void StartCraft(int index)
    {
        var list = CraftableList();
        int idx = CraftPage * Config.CraftPageSize + index;
        if (index < 0 || idx >= list.Count) return;
        var (key, def) = list[idx];

        var branch = ItemData.CategoryOf(key);
        int branchLevel = Player.CraftLevels.GetValueOrDefault(branch, 1);
        if (Player.Level < def.LevelReq)
        {
            ShowMessage($"Требуется уровень персонажа: {def.LevelReq}.");
            return;
        }
        if (def.SkillReq > branchLevel)
        {
            ShowMessage($"Требуется уровень ветки «{branch}»: {def.SkillReq}.");
            return;
        }
        if (!string.IsNullOrEmpty(def.BlueprintRequired)
            && !Player.Blueprints.Contains(def.BlueprintRequired))
        {
            ShowMessage("Требуется чертёж.");
            return;
        }

        // Скорость мини-игры: 0.85 + skill_req*0.02 + (qty-1)*0.04, max 2.4
        MinigameSpeed = Math.Min(2.4f,
            0.85f + def.SkillReq * 0.02f + (CraftQty - 1) * 0.04f);
        MinigamePos = 0.5f;
        MinigameDir = 1f;
        MinigameItem = key;
        MinigameRecipeIdx = 0;
        MinigameActive = true;
    }

    /// <summary>Тик мини-игры — двигает индикатор туда-обратно.</summary>
    public void UpdateMinigame(float dt)
    {
        if (!MinigameActive) return;
        MinigamePos += MinigameDir * MinigameSpeed * dt;
        if (MinigamePos >= 1f) { MinigamePos = 1f; MinigameDir = -1f; }
        if (MinigamePos <= 0f) { MinigamePos = 0f; MinigameDir = 1f; }
    }

    /// <summary>Остановить мини-игру и выполнить крафт с вычисленной точностью.</summary>
    public void FinishMinigame()
    {
        if (!MinigameActive || MinigameItem == null) return;
        MinigameActive = false;

        var def = ItemData.Get(MinigameItem);
        int skillReq = def?.SkillReq ?? 1;
        double accuracy = 1.0 - Math.Abs(MinigamePos - 0.5) * 2.0
            - skillReq * 0.008
            - Math.Min(0.30, (CraftQty - 1) * 0.03);
        accuracy = Math.Max(0.0, accuracy);

        var result = Rules.CraftItem(Player, MinigameItem, CraftQty, accuracy,
            MinigameRecipeIdx);
        ShowMessage(result.Message);
        if (result.Ok)
        {
            ItemsCrafted += 1;
            Quests.UpdateCraft(MinigameItem);
            Quests.UpdateCraft(ItemData.MakeLootKey(MinigameItem, 0));
            Audio?.Play("pickup");
        }
    }

    // ── Сбор ресурсов (E) ──

    /// <summary>Добыть ресурс рядом с игроком или под курсором (E).</summary>
    public void TryHarvest()
    {
        // Ресурс на клетке игрока или в соседней клетке
        foreach (var (dx, dy) in new[] { (0, 0), (0, -1), (0, 1), (-1, 0), (1, 0) })
        {
            int x = Player.X + dx, y = Player.Y + dy;
            if (!CurrentMap.Harvestables.TryGetValue((x, y), out var rec)) continue;
            if (!rec.Has) continue;
            var hit = HarvestSystem.Hit(CurrentMap, Player, x, y, Player.Atk);
            if (hit == null) continue;
            string gained = string.Join(", ", hit.Items.Select(i => $"{ItemData.DisplayName(i.Key)} ×{i.Count}"));
            ShowMessage(hit.Done ? $"Собрано: {gained}" : $"{hit.Name}: {hit.Hp}/{hit.MaxHp}");
            Audio?.Play("pickup");
            // quest collect — ресурсы попадают в инвентарь внутри Hit
            foreach (var (key, _) in hit.Items)
                Quests.UpdateCollect(key, Player.Inventory);
            return;
        }
        ShowMessage("Здесь нечего собирать.");
    }

    // ── Активные навыки (F1–F4/F6–F8/F10) ──

    private static readonly Keys[] SkillKeys =
        { Keys.F1, Keys.F2, Keys.F3, Keys.F4, Keys.F6, Keys.F7, Keys.F8, Keys.F10 };

    private void TryCastSkillKey(KeyboardState keys, KeyboardState prevKeys)
    {
        for (int i = 0; i < SkillKeys.Length; i++)
        {
            if (keys.IsKeyDown(SkillKeys[i]) && !prevKeys.IsKeyDown(SkillKeys[i]))
            {
                CastActiveSkill(i);
                return;
            }
        }
    }

    /// <summary>
    /// Применить активный навык класса (firebolt/frost_bolt/heal_self):
    /// мана + кулдаун + эффект. Возвращает true при успехе.
    /// </summary>
    public bool CastActiveSkill(int index)
    {
        var skill = SkillSystem.Get(Player, index);
        if (skill == null) return false;

        string? error = SkillSystem.Validate(Player, skill);
        if (error != null)
        {
            ShowMessage(error);
            return false;
        }

        bool cast = false;
        switch (skill.Kind)
        {
            case "fireball":
            case "frost_bolt":
            case "damage":
            {
                // Урон ближайшему монстру по взгляду (в радиусе 8)
                var target = CombatSystem.FindInFacing(Player, CurrentMap, 8);
                if (target == null)
                {
                    ShowMessage("Нет цели.");
                    return false;
                }
                // Визуальный снаряд: урон уже нанесён ниже, снаряд только
                // показывает направление каста (ProjectileSystem.Visual).
                Projectiles.Add(ProjectileSystem.Create(Player, target,
                    CombatSystem.ProjectileKindFor(skill), 0, visual: true));

                int damage = SkillSystem.Damage(Player, skill, target);
                ShowMessage($"{skill.Name}: {damage} урона — {target.Name}");
                if (!target.Alive)
                {
                    CombatSystem.AwardKill(Player, CurrentMap, target, Depth);
                    Quests.UpdateKill(target.Ch);
                    Audio?.Play(target.Boss ? "boss" : "hit");
                }
                cast = true;
                break;
            }
            case "heal":
            case "heal_self":
            {
                if (Player.Hp >= Player.MaxHp)
                {
                    ShowMessage("Здоровье уже полное.");
                    return false;
                }
                int healed = SkillSystem.Heal(Player, skill);
                ShowMessage($"{skill.Name}: +{healed} HP");
                cast = true;
                break;
            }
        }

        if (cast)
        {
            SkillSystem.Pay(Player, skill);
            Audio?.Play(skill.Key == "fireball" ? "fireball" : "hit");
        }
        return cast;
    }

    /// <summary>Тик кулдаунов активных навыков.</summary>
    public void TickSkillCooldowns(float dt)
    {
        var keys = Player.ActiveSkillCd.Keys.ToList();
        foreach (var k in keys)
        {
            float v = Player.ActiveSkillCd[k] - dt;
            if (v <= 0) Player.ActiveSkillCd.Remove(k);
            else Player.ActiveSkillCd[k] = v;
        }
    }

    /// <summary>
    /// Handle input in inventory mode.
    /// </summary>
    private void HandleInventoryInput(KeyboardState keys, KeyboardState prevKeys)
    {
        // Page navigation (Q/E), страницы по 100 (сетка 10×10)
        if (keys.IsKeyDown(Keys.Q) && !prevKeys.IsKeyDown(Keys.Q))
            CurrentPage = Math.Max(0, CurrentPage - 1);
        if (keys.IsKeyDown(Keys.E) && !prevKeys.IsKeyDown(Keys.E))
            CurrentPage++;

        // U/V/H/B/N — снять weapon/armor/head/legs/boots
        // (game/_input.py, экран инвентаря)
        var unequips = new (Keys Key, string Slot, string Label)[]
        {
            (Keys.U, "weapon", "Оружие"),
            (Keys.V, "armor", "Броня"),
            (Keys.H, "head", "Шлем"),
            (Keys.B, "legs", "Поножи"),
            (Keys.N, "boots", "Обувь"),
        };
        foreach (var (key, slot, label) in unequips)
        {
            if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key))
            {
                if (Player.UnequipSlot(slot)) ShowMessage($"{label} снято.");
                return;
            }
        }

        // Shift+1–8: назначить предмет в хотбар
        bool shift = keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift);
        if (shift)
        {
            for (int i = 0; i < 8; i++)
            {
                var key = Keys.D1 + i;
                if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key))
                    AssignQuickbarFromInventory(i);
            }
            return;
        }

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

    private void AssignQuickbarFromInventory(int slot)
    {
        int pageStart = CurrentPage * Config.InventoryPageSize;
        var items = Player.InventoryStacks();
        int idx = pageStart + slot;
        if (idx >= items.Count) return;
        Player.AssignQuickbar(slot, items[idx].Key);
        ShowMessage($"Слот {slot + 1}: {ItemData.DisplayName(items[idx].Key)}");
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
            ShopPage = Math.Min(ShopPagesCount() - 1, ShopPage + 1);

        // R: reroll — только у торговца подземелья (городские лавки фиксированы)
        if (keys.IsKeyDown(Keys.R) && !prevKeys.IsKeyDown(Keys.R))
            RerollShop();

        // 1-9: buy or sell item (SHOP_PAGE_SIZE = 9)
        for (int i = 0; i < 9; i++)
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

    /// <summary>Перегенерация стока торговца подземелья за золото (R).</summary>
    public void RerollShop()
    {
        if (!DungeonShopMode || DungeonMerchant == null) return;
        var map = CurrentMap;
        if (map.MerchantStock == null) return;
        int price = Config.RerollPrice(Math.Max(1, Depth));
        if (Player.Gold < price)
        {
            ShowMessage($"Обновление стоит {price} золота!");
            return;
        }
        Player.Gold -= price;
        map.MerchantStock = RecipeData.MakeShopStock(Math.Max(1, Depth));
        ShopPage = 0;
        ShowMessage($"Ассортимент обновлён (-{price} золота)");
    }

    /// <summary>Переключение покупка/продажа (кнопка режима в магазине).</summary>
    public void ToggleShopMode()
    {
        ShopSubMode = ShopSubMode == "buy" ? "sell" : "buy";
        ShopPage = 0;
    }

    /// <summary>Число страниц магазина — parity ShopController.pages_count.</summary>
    public int ShopPagesCount()
    {
        int n = ShopSubMode == "buy"
            ? CurrentShopStock().Count
            : Player.InventoryStacks().Count;
        return Math.Max(1, (n + Config.ShopPageSize - 1) / Config.ShopPageSize);
    }

    /// <summary>Сток текущего магазина (городской NPC или торговец подземелья).</summary>
    public List<RecipeData.ShopEntry> CurrentShopStock()
    {
        if (DungeonShopMode)
        {
            var map = CurrentMap;
            if (map.MerchantStock == null)
                map.MerchantStock = RecipeData.MakeShopStock(Math.Max(1, Depth));
            return map.MerchantStock;
        }
        var list = new List<RecipeData.ShopEntry>();
        if (ActiveShopNpc != null)
            list = ShopSystem.CityStock(ActiveShopNpc);
        return list;
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
        if (ProgressionSystem.LearnNode(Player, index, out string message))
            ShowMessage(message);
        else if (message.Length > 0)
            ShowMessage(message);
    }

    /// <summary>Клик мышью по узлу древа навыков (из SkillScreen.Draw).
    /// SkillScreen возвращает команду только на «свежий» клик (один раз
    /// на нажатие), здесь остаётся только исполнить её.</summary>
    public void HandleSkillClick(string? command)
    {
        if (string.IsNullOrEmpty(command)) return;

        // Кнопка эволюции класса
        if (command.StartsWith("promote:"))
        {
            string newKey = command["promote:".Length..];
            if (ClassData.Promote(Player, newKey))
                ShowMessage($"Новый класс: {ClassData.Get(newKey)?.Name}!");
            else
                ShowMessage($"Смена класса доступна с уровня " +
                    $"{ClassData.ClassChangeLevels[Math.Min(Player.ClassChanges, ClassData.ClassChangeLevels.Length - 1)]}.");
            return;
        }

        // Изучение узла
        if (command.StartsWith("learn:"))
        {
            string nodeKey = command["learn:".Length..];
            var nodes = SkillTree.GetNodes(Player);
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].Key == nodeKey)
                {
                    TryLearnNode(i);
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Try to promote to the first available class.
    /// </summary>
    private void TryPromoteClass()
    {
        if (ProgressionSystem.Promote(Player, out string message))
            ShowMessage(message);
        else if (message.Length > 0)
            ShowMessage(message);
    }

    /// <summary>
    /// Use or equip item at given index on current inventory page (сетка 10×10).
    /// </summary>
    private void UseItemByIndex(int index)
    {
        int pageStart = CurrentPage * Config.InventoryPageSize;
        var items = Player.InventoryStacks();
        int idx = pageStart + index;
        if (idx >= items.Count) return;
        UseItemByKey(items[idx].Key);
    }

    /// <summary>Клик по предмету в инвентаре (game/_input.py: use_item(key)).</summary>
    public void UseInventoryItem(string key)
    {
        UseItemByKey(key);
        CurrentPage = Math.Min(CurrentPage, InventoryPagesCount() - 1);
    }

    /// <summary>Клик по ячейке экипировки — снять предмет.</summary>
    public void UnequipInventorySlot(string slot)
    {
        if (Player.UnequipSlot(slot))
            ShowMessage("Предмет снят.");
    }

    public void ChangeInventoryPage(int delta)
    {
        if (delta < 0) CurrentPage = Math.Max(0, CurrentPage - 1);
        else if (delta > 0)
            CurrentPage = Math.Min(InventoryPagesCount() - 1, CurrentPage + 1);
    }

    public int InventoryPagesCount()
    {
        int n = Player.InventoryStacks().Count;
        return Math.Max(1, (n + Config.InventoryPageSize - 1) / Config.InventoryPageSize);
    }

    private void UseItemByKey(string key)
    {
        var def = ItemData.Get(ItemData.ParseItemKey(key).Base);
        if (def == null) return;

        switch (def.Kind)
        {
            case ItemData.ItemType.Weapon:
            case ItemData.ItemType.Armor:
            case ItemData.ItemType.Helmet:
            case ItemData.ItemType.Leggings:
            case ItemData.ItemType.Boots:
                if (Player.EquipItem(key))
                    ShowMessage($"Надето: {ItemData.DisplayName(key)}");
                break;
            case ItemData.ItemType.Scroll:
            case ItemData.ItemType.Rune:
                if (Player.UseItem(key, CurrentMap))
                    ShowMessage($"Применено: {def.Name}");
                else
                    ShowMessage($"Недостаточно маны ({def.ManaCost} MP).");
                break;
            case ItemData.ItemType.Blueprint:
                if (Player.UseItem(key))
                    ShowMessage($"Изучен чертёж: {def.Name}");
                else
                    ShowMessage("Уже известно.");
                break;
            default:
                if (Player.UseItem(key, CurrentMap))
                {
                    ShowMessage($"Использовано: {def.Name}");
                    if (key.StartsWith("potion")) PotionsUsed++;
                }
                break;
        }
        // collect-квесты проверяем после любого использования
        Quests.UpdateCollect(key, Player.Inventory);
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
                ShowMessage("Дверь открыта ключом босса!");
            else
                ShowMessage("Заперто. Нужен ключ босса.");
            return;
        }

        // Chest — open on walk
        if (tile == Tile.Chest)
        {
            var loot = Rules.OpenChest(CurrentMap, Player, nx, ny);
            if (loot != null)
                ShowMessage($"Найдено: {ItemData.DisplayName(loot)}!");
            return;
        }

        // Wall blocks
        if (tile == Tile.Wall)
            return;

        // Boss arena check: player can't leave arena while boss is alive
        if (IsInBossArena(Player.X, Player.Y) && !IsInBossArena(nx, ny))
        {
            ShowMessage("Босс не выпускает вас из арены!");
            Audio?.Play("error");
            return;
        }

        // Move
        Player.X = nx;
        Player.Y = ny;
        Player.StepCounter++;

        // Открыть туман войны вокруг новой позиции
        CurrentMap.RevealAround(nx, ny, Config.RevealRadius);

        // Gate — переход между локациями по F (UseStairs)
        if (tile == Tile.Gate)
        {
            ShowMessage("A gate to another land... (press F to travel)");
        }

        // Auto-pickup items
        if (CurrentMap.Items.TryGetValue((nx, ny), out var itemKey))
        {
            Player.AddItem(itemKey, 1);
            CurrentMap.Items.Remove((nx, ny));
            ShowMessage($"Поднято: {ItemData.DisplayName(itemKey)}");
            Audio?.Play("pickup");
            Quests.UpdateCollect(itemKey, Player.Inventory);
        }
    }

    /// <summary>
    /// Перейти через ворота (GATE) в другую локацию. Врата есть и на
    /// оверворлде (края карты → биомы), и внутри локаций (назад в деревню).
    /// </summary>
    public void TravelThroughGate()
    {
        Projectiles.Clear();
        var pos = (Player.X, Player.Y);
        if (!CurrentMap.Transitions.TryGetValue(pos, out var transition))
        {
            ShowMessage("Врата закрыты.");
            return;
        }
        var (to, entry) = transition;

        if (to == "village" || to == "overworld")
        {
            // Возврат в деревню (оверворлд)
            _currentLocation = null;
            _worldMode = "overworld";
            Player.X = Aethelgard.World.Overworld.VillageCenter.Item1;
            Player.Y = Aethelgard.World.Overworld.VillageCenter.Item2;
            Overworld.RevealAround(Player.X, Player.Y, Config.RevealRadius);
            ShowMessage("Вы вернулись в Город Этельгард.");
            return;
        }

        if (!Locations.All.ContainsKey(to))
        {
            ShowMessage("Врата закрыты.");
            return;
        }

        _worldMode = "overworld"; // локация — наземная карта
        if (!_locationCache.TryGetValue(to, out var map))
        {
            map = Locations.GenerateLocation(to);
            _locationCache[to] = map;
        }
        _currentLocation = map;
        // Вход — у врат локации (врата в первый столбец, вход справа от них)
        var (entryX, entryY) = (1, map.H / 2);
        if (CurrentMap.Tiles[entryY][entryX] == Tile.Wall)
            CurrentMap.Tiles[entryY][entryX] = Tile.Floor;
        Player.X = entryX;
        Player.Y = entryY;
        CurrentMap.RevealAround(entryX, entryY, Config.RevealRadius);
        ShowMessage($"Вы вошли в: {Locations.All[to].Name}.");
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
        Projectiles.Clear();
        var tile = CurrentMap.Tiles[Player.Y][Player.X];

        // Ворота — переход между локациями
        if (tile == Tile.Gate)
        {
            TravelThroughGate();
            return;
        }

        if (_worldMode == "overworld")
        {
            if (tile != Tile.Stairs)
            {
                ShowMessage("Здесь нет лестницы.");
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
                ShowMessage("Нельзя спуститься глубже.");
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
            ShowMessage("Здесь нет лестницы.");
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
        ShowMessage($"Вы вошли в подземелье, этаж {Depth}");
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
            ShowMessage("Это самый глубокий этаж.");
            return;
        }

        Dungeon = GetOrGenerateFloor(Depth);
        PlacePlayerAtEntry();
        Quests.UpdateReach(Depth);
        ShowMessage($"Спуск на этаж {Depth}");
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
        ShowMessage($"Подъём на этаж {Depth}");
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
        Player.X = Aethelgard.World.Overworld.DungeonPos.Item1;
        Player.Y = Aethelgard.World.Overworld.DungeonPos.Item2;
        ShowMessage("Вы поднялись на поверхность.");
    }

    /// <summary>
    /// Exit dungeon back to overworld (публичный вход для дев-консоли).
    /// </summary>
    public void GotoSurface()
    {
        if (_worldMode == "overworld" && _currentLocation == null) return;
        ExitToOverworld();
        _currentLocation = null;
    }

    /// <summary>
    /// Телепорт в подземелье на этаж N (дев-консоль: depth <N>).
    /// </summary>
    public void GotoDungeonFloor(int depth)
    {
        depth = Math.Clamp(depth, 1, Config.MaxDepth);
        _worldMode = "dungeon";
        _currentLocation = null;
        Depth = depth;
        Dungeon = GetOrGenerateFloor(depth);
        PlacePlayerAtEntry();
        Quests.UpdateReach(Depth);
        ShowMessage($"Телепорт: этаж {Depth}");
    }

    /// <summary>
    /// Телепорт в локацию по ключу (дев-консоль: goto <loc>).
    /// </summary>
    public bool GotoLocation(string key)
    {
        if (!Locations.All.ContainsKey(key)) return false;
        Projectiles.Clear();
        _worldMode = "overworld";
        if (!_locationCache.TryGetValue(key, out var map))
        {
            map = Locations.GenerateLocation(key);
            _locationCache[key] = map;
        }
        _currentLocation = map;
        var (entryX, entryY) = (1, map.H / 2);
        if (CurrentMap.Tiles[entryY][entryX] == Tile.Wall)
            CurrentMap.Tiles[entryY][entryX] = Tile.Floor;
        Player.X = entryX;
        Player.Y = entryY;
        ShowMessage($"Вы вошли в: {Locations.All[key].Name}.");
        return true;
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

    // ── Save/load hooks ──

    /// <summary>Доступ к кэшу этажей для SaveSystem.</summary>
    public Dictionary<int, Dungeon> GetFloorCache() => _floorCache;

    /// <summary>Доступ к кэшу локаций для SaveSystem.</summary>
    public Dictionary<string, Dungeon> GetLocationCache() => _locationCache;

    /// <summary>
    /// Восстановить карту в кэш (SaveSystem.ApplyLoad). Карты с
    /// LocationKey попадают в кэш локаций, остальные — в кэш этажей.
    /// </summary>
    public void SetFloorCache(int depth, Dungeon floor, string? locationKey)
    {
        if (!string.IsNullOrEmpty(locationKey))
        {
            floor.LocationKey = locationKey;
            _locationCache[locationKey] = floor;
        }
        else
        {
            _floorCache[depth] = floor;
        }
    }

    /// <summary>Восстановить режим мира и текущую локацию (SaveSystem.ApplyLoad).</summary>
    public void SetWorldMode(string mode, int depth, string? locationKey = null)
    {
        _worldMode = mode;
        Depth = depth;
        if (mode == "dungeon" && depth >= 1)
        {
            _currentLocation = null;
            Dungeon = GetOrGenerateFloor(depth);
            PlacePlayerAtEntry();
        }
        else
        {
            _currentLocation = string.IsNullOrEmpty(locationKey)
                ? null
                : GetOrGenerateLocation(locationKey);
            Dungeon = Overworld;
        }
    }

    private Dungeon GetOrGenerateLocation(string key)
    {
        if (_locationCache.TryGetValue(key, out var map))
            return map;
        map = Locations.GenerateLocation(key);
        _locationCache[key] = map;
        return map;
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
    /// Attack nearest monster (или дальнобойным оружием — по взгляду).
    /// </summary>
    private void Attack()
    {
        // Дальнобойное оружие (bow/staff/book wtype) — выстрел по взгляду до 8 клеток
        string? weapon = Player.Weapon;
        var wdef = ItemData.Get(weapon);
        if (wdef?.WType is "bow" or "staff" or "book")
        {
            var ranged = CombatSystem.FindInFacing(Player, CurrentMap, 8);
            if (ranged != null)
            {
                AttackTarget(ranged);
                return;
            }
        }

        var target = CombatSystem.FindNearestMonster(Player, CurrentMap);
        if (target != null)
            AttackTarget(target);
    }

    private void AttackTarget(Entity target)
    {
        var outcome = CombatSystem.Attack(Player, target);
        var result = outcome.First;
        if (result == null) return;

        if (result.Dodged)
        {
            ShowMessage($"{target.Name} dodged!");
            Audio?.Play("miss");
            return;
        }

        if (result.Crit)
            Audio?.Play("crit");

        if (outcome.Killed)
        {
            FinishKill(target);
            return;
        }

        Audio?.Play("attack");
        if (result.Healed > 0)
            ShowMessage($"Вампиризм: +{result.Healed} HP");
        else if (outcome.Second != null)
            ShowMessage("Двойной удар!");
    }

    private void FinishKill(Entity target)
    {
        string msg = $"{target.Name} slain!";
        if (target.Boss)
            msg = $"Boss {target.Name} defeated!";
        CombatSystem.AwardKill(Player, CurrentMap, target, Depth);
        Audio?.Play(target.Boss ? "boss" : "death_monster");
        KillsTotal++;
        if (target.Boss) BossKills++;
        KillsByCh[target.Name] = KillsByCh.GetValueOrDefault(target.Name) + 1;

        // Quest kill tracking
        Quests.UpdateKill(target.Ch);

        // Победа: Древний дракон (глубина 20) повержен
        if (target.Boss && target.MonsterKey == "boss_a_20")
        {
            Victory = true;
            msg += " VICTORY! You have conquered the dungeon!";
        }

        ShowMessage(msg);
    }

    /// <summary>
    /// Update game logic: monsters, status effects, regen, harvest.
    /// Реген: +1 MP каждые 5 с всегда, +1 HP — только с перком regen
    /// (core/config.py REGEN_TICK_INTERVAL / _base.py:517-528).
    /// </summary>
    public void Update(float dt)
    {
        if (ScreenMode != Screens.Game) return;

        // Туман войны: открываем клетки вокруг игрока (в т.ч. после телепортов,
        // загрузки, переходов между локациями — покрывает все способы перемещения)
        if (Player.X != _lastRevealX || Player.Y != _lastRevealY || CurrentMap._revealDirty)
        {
            _lastRevealX = Player.X;
            _lastRevealY = Player.Y;
            CurrentMap._revealDirty = false;
            CurrentMap.RevealAround(Player.X, Player.Y, Config.RevealRadius);
        }

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
            if (Player.Alive && Player.Hp < Player.MaxHp && Player.Regen)
                Player.Hp = Math.Min(Player.MaxHp, Player.Hp + Config.RegenHpPerTick);
            if (Player.Mana < Player.MaxMana)
                Player.Mana = Math.Min(Player.MaxMana, Player.Mana + Config.RegenManaPerTick);
        }

        // Skill cooldowns + craft minigame
        TickSkillCooldowns(dt);
        UpdateMinigame(dt);

        // Harvest regrow
        HarvestSystem.Tick(CurrentMap);

        // Monster AI + projectiles
        UpdateMonsters(dt);
        UpdateProjectiles(dt);

        // Message timer
        if (MessageTimer > 0)
            MessageTimer -= dt;
    }

    /// <summary>Смерть игрока — респавн с полным HP в деревне.</summary>
    private void HandlePlayerDeath()
    {
        if (Player.Alive) return;
        Projectiles.Clear();
        Player.Hp = Player.MaxHp;
        Player.Mana = Player.MaxMana;
        Player.Statuses.Clear();
        _worldMode = "overworld";
        _currentLocation = null;
        Depth = 0;
        Dungeon = Overworld;
        Player.X = Aethelgard.World.Overworld.VillageCenter.Item1;
        Player.Y = Aethelgard.World.Overworld.VillageCenter.Item2;
        ShowMessage("Вы погибли... и очнулись в деревне.");
    }

    /// <summary>
    /// Монстр-ИИ по способностям (parity с _combat.py:_monster_act):
    /// ranged — стреляет с 2–4 клеток, healer — лечит союзника +8 HP в
    /// радиусе 5, passive — убегает, slow — пропуск хода, босс — преследует
    /// до 15 и «дыхание» раз в 4.5 с на дистанции 2–5.
    /// </summary>
    private void UpdateMonsters(float dt)
    {
        MonsterAI.Update(CurrentMap, new[] { Player }, dt,
            onMelee: (_, _, result) =>
            {
                if (!result.Dodged && result.Damage > 0)
                    Audio?.Play("hit");
            },
            onRanged: (m, p) =>
            {
                // Дальняя атака летит снарядом; урон считается при попадании
                // через Rules (dodge/crit/статусы), как в одиночной игре.
                Projectiles.Add(ProjectileSystem.Create(
                    m, p, CombatSystem.ProjectileKindFor(m), Math.Max(1, m.Atk - p.Df)));
            },
            onBreath: (m, _, damage) =>
            {
                ShowMessage($"{m.Name} breathes fire! (-{damage} HP)");
                Audio?.Play("fireball");
            });

        HandlePlayerDeath();
    }

    private void UpdateProjectiles(float dt)
    {
        for (int i = Projectiles.Count - 1; i >= 0; i--)
        {
            var p = Projectiles[i];
            // Снаряды монстров целятся в игрока, остальные — в монстров.
            IReadOnlyList<Entity> targets = p.SourcePid == -1
                ? new Entity[] { Player }
                : CurrentMap.Monsters;

            if (ProjectileSystem.Update(p, dt, CurrentMap, targets))
                Projectiles.RemoveAt(i);
        }
    }

    public void ShowMessage(string msg)
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

        // Text input — обработка печатных символов (общая карта TextKeyMap)
        foreach (var (key, ch) in TextKeyMap)
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
    /// Карта клавиш для текстового ввода (консоль и MP-чат): буквы, цифры,
    /// пробел и базовая пунктуация. Shift даёт заглавные буквы.
    /// </summary>
    private static readonly Dictionary<Keys, char> TextKeyMap = new()
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

    /// <summary>
    /// Play sound for an event.
    /// </summary>
    public void PlaySound(string name)
    {
        Audio?.Play(name);
    }

    // ── Multiplayer ──

    /// <summary>
    /// Старт MP-игры как клиент (подключение к чужому серверу).
    /// </summary>
    public void StartMultiplayer(GameClient client, string classKey)
    {
        MpClient = client;
        IsMultiplayer = true;
        ScreenMode = Screens.MpGame;
        Player = new Player(string.IsNullOrEmpty(classKey) ? "warrior" : classKey);
    }

    /// <summary>
    /// Стать хостом: поднять сервер, при продолжении — загрузить мир из
    /// сейва (menu_multiplayer: «Продолжить игру»), иначе — новая игра.
    /// Класс выбирается на class_select (python: хост всегда выбирает класс).
    /// </summary>
    public bool StartMpHost(bool freshStart, string classKey)
    {
        try
        {
            var server = new GameServer();
            server.Start(GameServer.DefaultPort);
            if (!freshStart)
                server.Core.LoadWorld();   // продолжить из сохранения

            var client = new GameClient();
            bool ok = client.Connect("localhost", server.Port, classKey, null);
            if (!ok)
            {
                server.Stop();
                return false;
            }
            MpServer = server;
            StartMultiplayer(client, classKey);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Подключиться к хосту по адресу "host:port" (menu_mp_join).
    /// </summary>
    public bool StartMpJoin(string hostPort)
    {
        try
        {
            var parts = hostPort.Split(':');
            string host = parts[0];
            int port = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : 9000;
            var client = new GameClient();
            if (!client.Connect(host, port, "warrior", null))
                return false;
            StartMultiplayer(client, "warrior");
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Отключиться от MP-сессии и остановить сервер хоста.</summary>
    public void StopMultiplayer()
    {
        MpClient?.Disconnect();
        MpClient = null;
        MpServer?.Stop();
        MpServer = null;
        IsMultiplayer = false;
        MpSnapshot = null;
        MpPanel = "";
        ChatLog.Clear();
        ChatInput = "";
        ChatActive = false;
    }

    /// <summary>Синхронизирует видимые настройки (FPS, мини-карта, звук).</summary>
    public void ApplySettings()
    {
        ShowFps = Settings.ShowFps;
        MinimapVisible = Settings.ShowMinimap;
        if (Audio != null)
            Audio.Volume = Settings.Volume / 10f;
    }


    /// <summary>
    /// Открытая панель MP-клиента: "" | "inventory" | "stats" | "skills" |
    /// "quests" | "shop" — те же экраны, что в синглплеере (I/C/N/L/B).
    /// </summary>
    public string MpPanel = "";
    public int MpPage = 0;          // инвентарь: страница по 8 слотов
    public int MpShopPage = 0;      // магазин: страница по 9
    public bool MpShopSell = false; // магазин: режим продажи
    public bool MpSkillCast = false;// навыки: false — учить (1–7), true — кастовать (1–8)
    public int MpTradeSel = 0;      // обмен: выбранный предмет моего предложения

    /// <summary>
    /// Handle input in multiplayer mode — send to server.
    /// Клавиши повторяют синглплеер: WASD/Space/F/E/T/H/M/P, панели I/C/N/L/B,
    /// хотбар 1–8, дуэль Z, ожидание X, чат Y/Enter, сохранение мира F5.
    /// </summary>
    private void HandleMpInput(KeyboardState keys, KeyboardState prevKeys)
    {
        if (MpClient == null) return;
        var snap = MpSnapshot;
        var my = snap?.Players.FirstOrDefault(p => p.Pid == snap.MyPid);
        bool shift = keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift);

        // ── Ввод чата ──
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
            if (keys.IsKeyDown(Keys.Back) && !prevKeys.IsKeyDown(Keys.Back))
            {
                if (ChatInput.Length > 0) ChatInput = ChatInput[..^1];
                return;
            }
            // Печатные символы — через общую карту TextKeyMap (фикс: раньше
            // ввод текста был заглушкой и чат нельзя было набрать)
            foreach (var (key, ch) in TextKeyMap)
            {
                if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key))
                {
                    char c = ch;
                    if (shift && char.IsLetter(c)) c = char.ToUpper(c);
                    ChatInput += c;
                    return;
                }
            }
            return;
        }

        // Чат: Y или Enter
        if ((keys.IsKeyDown(Keys.Y) && !prevKeys.IsKeyDown(Keys.Y))
            || (keys.IsKeyDown(Keys.Enter) && !prevKeys.IsKeyDown(Keys.Enter)))
        {
            ChatActive = true;
            ChatInput = "";
            return;
        }

        if (snap == null || my == null) return;

        // Окно обмена открывается и закрывается снапшотом
        // (net/view.py:_apply_trade).
        if (snap.Trade != null && snap.Trade.Players.Contains(snap.MyPid))
        {
            if (MpPanel == "") { MpPanel = Screens.Trade; MpTradeSel = 0; }
        }
        else if (MpPanel == Screens.Trade)
        {
            MpPanel = "";
        }

        // ── Панели (экраны MP дублируют SP-экраны) ──
        if (MpPanel != "")
        {
            HandleMpPanelInput(keys, prevKeys, snap, my, shift);
            return;
        }

        // Открытие панелей — те же клавиши, что в сингле
        if (keys.IsKeyDown(Keys.I) && !prevKeys.IsKeyDown(Keys.I))
        { MpPanel = Screens.Inventory; MpPage = 0; return; }
        if (keys.IsKeyDown(Keys.C) && !prevKeys.IsKeyDown(Keys.C))
        { MpPanel = Screens.Stats; return; }
        if (keys.IsKeyDown(Keys.N) && !prevKeys.IsKeyDown(Keys.N))
        { MpPanel = Screens.Skills; MpSkillCast = false; return; }
        if (keys.IsKeyDown(Keys.L) && !prevKeys.IsKeyDown(Keys.L))
        { MpPanel = Screens.Quests; return; }
        if (keys.IsKeyDown(Keys.B) && !prevKeys.IsKeyDown(Keys.B))
        { MpPanel = Screens.Shop; MpShopPage = 0; MpShopSell = false; return; }

        // Движение
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

        // Атака (Space)
        if (keys.IsKeyDown(Keys.Space) && !prevKeys.IsKeyDown(Keys.Space))
            MpClient.SendAttack();

        // Лестницы/выход/ворота (F — как в сингле)
        if (keys.IsKeyDown(Keys.F) && !prevKeys.IsKeyDown(Keys.F))
            MpClient.SendUseStairs();

        // Добыча ресурса (E — как в сингле): смежный живой узел из снапшота
        if (keys.IsKeyDown(Keys.E) && !prevKeys.IsKeyDown(Keys.E))
        {
            foreach (var node in snap.Harvests)
            {
                if (Math.Max(Math.Abs(node.X - my.X), Math.Abs(node.Y - my.Y)) <= 1)
                {
                    MpClient.SendHarvest(node.X, node.Y);
                    break;
                }
            }
        }

        // Поговорить с NPC (T — как в сингле): смежный NPC по NpcType
        if (keys.IsKeyDown(Keys.T) && !prevKeys.IsKeyDown(Keys.T))
        {
            foreach (var npc in snap.Npcs)
            {
                if (!string.IsNullOrEmpty(npc.NpcType)
                    && Math.Max(Math.Abs(npc.X - my.X), Math.Abs(npc.Y - my.Y)) <= 1)
                {
                    MpClient.SendQuestTalk(npc.NpcType);
                    break;
                }
            }
        }

        // Быстрое зелье (H — как в сингле)
        if (keys.IsKeyDown(Keys.H) && !prevKeys.IsKeyDown(Keys.H))
            MpClient.SendUsePotion();

        // Ожидание хода (X — net/core.py:wait)
        if (keys.IsKeyDown(Keys.X) && !prevKeys.IsKeyDown(Keys.X))
            MpClient.SendWait();

        // Дуэль (Z — net/core.py:duel_request): ближайший другой игрок
        if (keys.IsKeyDown(Keys.Z) && !prevKeys.IsKeyDown(Keys.Z))
        {
            int bestPid = -1, bestDist = int.MaxValue;
            foreach (var p in snap.Players)
            {
                if (p.Pid == snap.MyPid) continue;
                int d = Math.Max(Math.Abs(p.X - my.X), Math.Abs(p.Y - my.Y));
                if (d < bestDist) { bestDist = d; bestPid = p.Pid; }
            }
            if (bestPid >= 0) MpClient.SendDuelRequest(bestPid);
        }

        // Обмен (G — соседняя с Z клавиша; в python обмен вызывается кликом
        // по игроку): предложение обмена ближайшему игроку в клетке рядом.
        if (keys.IsKeyDown(Keys.G) && !prevKeys.IsKeyDown(Keys.G))
        {
            int bestPid = -1, bestDist = int.MaxValue;
            foreach (var p in snap.Players)
            {
                if (p.Pid == snap.MyPid) continue;
                int d = Math.Max(Math.Abs(p.X - my.X), Math.Abs(p.Y - my.Y));
                if (d < bestDist) { bestDist = d; bestPid = p.Pid; }
            }
            if (bestPid >= 0 && bestDist <= 1) MpClient.SendTrade(bestPid, "request");
        }

        // Повышение класса (P — как в сингле)
        if (keys.IsKeyDown(Keys.P) && !prevKeys.IsKeyDown(Keys.P))
            MpClient.SendPromote();

        // Хотбар 1–8 (как в сингле): использовать назначенный предмет
        for (int i = 0; i < 8 && i < my.Quickbar.Length; i++)
        {
            var key = Keys.D1 + i;
            if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key)
                && !shift && my.Quickbar[i] != null)
            {
                MpClient.SendUseItem(my.Quickbar[i]!);
            }
        }

        // Сохранение мира хостом (F5 — аналог быстрого сохранения)
        if (keys.IsKeyDown(Keys.F5) && !prevKeys.IsKeyDown(Keys.F5))
            MpClient.SendSaveWorld();
    }

    /// <summary>
    /// Ввод на открытой MP-панели: раскладка клавиш повторяет
    /// HandleInventoryInput / HandleStatsInput / HandleSkillsInput /
    /// HandleQuestInput / HandleShopInput синглплеера.
    /// </summary>
    private void HandleMpPanelInput(KeyboardState keys, KeyboardState prevKeys,
        Snapshot snap, Snapshot.PlayerState my, bool shift)
    {
        // Esc / повторная клавиша панели — закрыть
        if (keys.IsKeyDown(Keys.Escape) && !prevKeys.IsKeyDown(Keys.Escape))
        { MpPanel = ""; return; }

        switch (MpPanel)
        {
            case Screens.Inventory:
            {
                if (keys.IsKeyDown(Keys.I) && !prevKeys.IsKeyDown(Keys.I))
                { MpPanel = ""; return; }

                // Q/E — страницы (как в SP)
                if (keys.IsKeyDown(Keys.Q) && !prevKeys.IsKeyDown(Keys.Q))
                    MpPage = Math.Max(0, MpPage - 1);
                if (keys.IsKeyDown(Keys.E) && !prevKeys.IsKeyDown(Keys.E))
                    MpPage++;

                // U/V/H/B/N — снять weapon/armor/head/legs/boots (как в SP)
                var unequips = new (Keys Key, string Slot)[]
                {
                    (Keys.U, "weapon"), (Keys.V, "armor"), (Keys.H, "head"),
                    (Keys.B, "legs"), (Keys.N, "boots"),
                };
                foreach (var (key, slot) in unequips)
                {
                    if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key))
                    {
                        MpClient!.SendUnequip(slot);
                        return;
                    }
                }

                var items = my.Inventory.Keys.ToList();
                // Shift+1–8 — назначить предмет в хотбар (как в SP)
                if (shift)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        var key = Keys.D1 + i;
                        int idx = MpPage * 8 + i;
                        if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key)
                            && idx < items.Count)
                            MpClient!.SendQuickbar(i, items[idx]);
                    }
                    return;
                }

                // 1–8 — использовать/экипировать предмет (как в SP)
                for (int i = 0; i < 8; i++)
                {
                    var key = Keys.D1 + i;
                    int idx = MpPage * 8 + i;
                    if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key)
                        && idx < items.Count)
                        MpClient!.SendUseItem(items[idx]);
                }
                break;
            }

            case Screens.Stats:
            {
                if (keys.IsKeyDown(Keys.C) && !prevKeys.IsKeyDown(Keys.C))
                { MpPanel = ""; return; }
                // 1–6 — вложить очко (str/agi/vit/luk/df/int, как в SP)
                string[] stats = { "str", "agi", "vit", "luk", "df_stat", "int" };
                for (int i = 0; i < 6; i++)
                {
                    var key = Keys.D1 + i;
                    if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key))
                        MpClient!.SendAllocateStat(stats[i]);
                }
                break;
            }

            case Screens.Skills:
            {
                if (keys.IsKeyDown(Keys.N) && !prevKeys.IsKeyDown(Keys.N))
                { MpPanel = ""; return; }

                // Q/E — переключение «учить / кастовать» (как табы в SP)
                if ((keys.IsKeyDown(Keys.Q) && !prevKeys.IsKeyDown(Keys.Q))
                    || (keys.IsKeyDown(Keys.E) && !prevKeys.IsKeyDown(Keys.E)))
                    MpSkillCast = !MpSkillCast;

                // P — повышение класса (как в SP)
                if (keys.IsKeyDown(Keys.P) && !prevKeys.IsKeyDown(Keys.P))
                {
                    MpClient!.SendPromote();
                    return;
                }

                if (!MpSkillCast)
                {
                    // 1–7 — изучить узел (как HandleSkillsInput в SP)
                    for (int i = 0; i < 7; i++)
                    {
                        var key = Keys.D1 + i;
                        if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key))
                            MpClient!.SendLearnNode(i);
                    }
                }
                else
                {
                    // 1–8 — применить активный навык (как F1–F8 в SP)
                    for (int i = 0; i < 8; i++)
                    {
                        var key = Keys.D1 + i;
                        if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key))
                            MpClient!.SendSkill(i);
                    }
                }
                break;
            }

            case Screens.Quests:
            {
                if (keys.IsKeyDown(Keys.L) && !prevKeys.IsKeyDown(Keys.L))
                { MpPanel = ""; return; }
                // 1–9: принять доступный / сдать выполненный (как в SP)
                var available = snap.QuestBoard.Where(q => q.State == "available").ToList();
                var completed = snap.QuestBoard.Where(q => q.State == "completed").ToList();
                for (int i = 0; i < 9; i++)
                {
                    var key = Keys.D1 + i;
                    if (!keys.IsKeyDown(key) || prevKeys.IsKeyDown(key)) continue;
                    if (i < available.Count)
                        MpClient!.SendQuest(available[i].Key, true);
                    else
                    {
                        int cIdx = i - available.Count;
                        if (cIdx < completed.Count)
                            MpClient!.SendQuest(completed[cIdx].Key, false);
                    }
                }
                break;
            }

            case Screens.Shop:
            {
                if (keys.IsKeyDown(Keys.B) && !prevKeys.IsKeyDown(Keys.B))
                { MpPanel = ""; return; }

                // S — купить/продать (как в SP)
                if (keys.IsKeyDown(Keys.S) && !prevKeys.IsKeyDown(Keys.S))
                { MpShopSell = !MpShopSell; MpShopPage = 0; }

                // Left/Right — страницы (как в SP)
                if (keys.IsKeyDown(Keys.Left) && !prevKeys.IsKeyDown(Keys.Left))
                    MpShopPage = Math.Max(0, MpShopPage - 1);
                if (keys.IsKeyDown(Keys.Right) && !prevKeys.IsKeyDown(Keys.Right))
                    MpShopPage++;

                // R — реролл стока торговца (как в SP)
                if (keys.IsKeyDown(Keys.R) && !prevKeys.IsKeyDown(Keys.R))
                    MpClient!.SendReroll();

                // 1–9 — купить/продать
                for (int i = 0; i < 9; i++)
                {
                    var key = Keys.D1 + i;
                    if (!keys.IsKeyDown(key) || prevKeys.IsKeyDown(key)) continue;
                    int idx = MpShopPage * Config.ShopPageSize + i;
                    if (MpShopSell) MpClient!.SendSell(idx);
                    else MpClient!.SendBuy(idx);
                }
                break;
            }

            case Screens.Trade:
            {
                var trade = snap.Trade;
                if (trade == null) { MpPanel = ""; return; }
                int myPid = snap.MyPid;
                int other = trade.Players.FirstOrDefault(p => p != myPid);
                if (other <= 0) { MpPanel = ""; return; }
                bool accepted = trade.Accepted.Contains(myPid);

                // Esc — отмена (decline до подтверждения, cancel после)
                if (keys.IsKeyDown(Keys.Escape) && !prevKeys.IsKeyDown(Keys.Escape))
                {
                    MpClient!.SendTrade(other, accepted ? "cancel" : "decline");
                    MpPanel = "";
                    return;
                }

                // Y — принять заявку
                if (!accepted && keys.IsKeyDown(Keys.Y) && !prevKeys.IsKeyDown(Keys.Y))
                { MpClient!.SendTrade(other, "accept"); return; }

                if (!accepted) return;

                var myOffer = trade.Offers.GetValueOrDefault(myPid.ToString())
                    ?? new Dictionary<string, int>();
                var myKeys = myOffer.Keys.OrderBy(k => k).ToList();
                if (MpTradeSel >= myKeys.Count) MpTradeSel = Math.Max(0, myKeys.Count - 1);

                // R — подтвердить (net/core.py:trade_ready)
                if (keys.IsKeyDown(Keys.R) && !prevKeys.IsKeyDown(Keys.R))
                { MpClient!.SendTrade(other, "ready"); return; }

                // Q/E — выбор предмета моего предложения
                if (keys.IsKeyDown(Keys.Q) && !prevKeys.IsKeyDown(Keys.Q))
                    MpTradeSel = Math.Max(0, MpTradeSel - 1);
                if (keys.IsKeyDown(Keys.E) && !prevKeys.IsKeyDown(Keys.E))
                    MpTradeSel = Math.Min(myKeys.Count - 1, MpTradeSel + 1);

                // X — убрать выбранный предмет из предложения
                if (keys.IsKeyDown(Keys.X) && !prevKeys.IsKeyDown(Keys.X)
                    && myKeys.Count > 0 && MpTradeSel < myKeys.Count)
                { MpClient!.SendTrade(other, "remove", myKeys[MpTradeSel]); return; }

                // Золото: C +10 · V −10 · B +100 (в python — кнопки в окне)
                int myGold = trade.Gold.GetValueOrDefault(myPid.ToString());
                if (keys.IsKeyDown(Keys.C) && !prevKeys.IsKeyDown(Keys.C)
                    && myGold + 10 <= my.Gold)
                    MpClient!.SendTrade(other, "gold", null, 1, myGold + 10);
                if (keys.IsKeyDown(Keys.V) && !prevKeys.IsKeyDown(Keys.V)
                    && myGold - 10 >= 0)
                    MpClient!.SendTrade(other, "gold", null, 1, myGold - 10);
                if (keys.IsKeyDown(Keys.B) && !prevKeys.IsKeyDown(Keys.B)
                    && myGold + 100 <= my.Gold)
                    MpClient!.SendTrade(other, "gold", null, 1, myGold + 100);

                // 1–9 — добавить предмет из инвентаря (×1)
                var inv = my.Inventory.Keys.OrderBy(k => k).ToList();
                for (int i = 0; i < 9 && i < inv.Count; i++)
                {
                    var key = Keys.D1 + i;
                    if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key))
                    { MpClient!.SendTrade(other, "add", inv[i]); return; }
                }
                break;
            }
        }
    }

    /// <summary>
    /// Update multiplayer state — fetch latest snapshot.
    /// </summary>
    public void UpdateMp(float dt)
    {
        if (MpClient == null) return;

        MpSnapshot = MpClient.GetSnapshot();

        if (MpSnapshot != null)
        {
            // Update chat log from snapshot
            foreach (var chat in MpSnapshot.Chat)
            {
                string entry = $"[P{chat.Pid}] {chat.Text}";
                if (!ChatLog.Contains(entry))
                    ChatLog.Add(entry);
            }
            if (ChatLog.Count > 20)
                ChatLog.RemoveRange(0, ChatLog.Count - 20);
        }

        // Message timer
        if (MessageTimer > 0)
            MessageTimer -= dt;

        // Move cooldown
        _moveCooldown -= dt;
    }

    // ── City NPCs ──

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
        ScreenMode = Screens.Shop;
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
            QuestsDone++;
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
            ScreenMode = Screens.Dialogue;
            return;
        }

        if (npc.NpcType == "merchant")
        {
            // Торговец подземелья
            DungeonMerchant = npc;
            DungeonShopMode = true;
            ShopSubMode = "buy";
            ShopPage = 0;
            ScreenMode = Screens.Shop;
            return;
        }

        // Shop NPCs
        if (npc.NpcType != null && CityData.Npcs.ContainsKey(npc.NpcType))
        {
            if (npc.NpcType == "guard")
            {
                DialogueNpc = "Стражник";
                DialogueText = "Я сторожу врата. Монстры редко подходят к городским стенам, но в диких землях будьте начеку.";
                ScreenMode = Screens.Dialogue;
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
