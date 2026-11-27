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
public partial class GameController
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

    /// <summary>Посещённые локации (для быстрого перехода на большой карте).</summary>
    public HashSet<string> VisitedLocations { get; } = new() { "village" };

    public void MarkVisited(string key) => VisitedLocations.Add(key);

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
        // Полный сброс состояния забега: новая партия не продолжается
        // поверх старой (квесты, статистика, кулдауны, эффекты).
        Quests.Reset();
        KillsTotal = 0; BossKills = 0; PotionsUsed = 0;
        GoldEarned = 0; ItemsCrafted = 0; QuestsDone = 0;
        KillsByCh.Clear();
        Victory = false;
        Projectiles.Clear();
        _moveCooldown = 0;
        DungeonMerchant = null;
        DungeonShopMode = false;
        MinigameActive = false;
        MinigameItem = null;
        CraftPage = 0; CraftTab = 0; CraftQty = 1;
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

        // ── Журнал с вкладками: ввод сообщения (Y, работает и в одиночной игре) ──
        if (ChatInputOpen)
        {
            HandleChatInput(keys, prevKeys);
            return;
        }

        // Клавиши журнала: Y — написать, PgUp/PgDn/Home/End — прокрутка
        if (keys.IsKeyDown(Keys.Y) && !prevKeys.IsKeyDown(Keys.Y))
        {
            ChatInputOpen = true;
            ChatLogInput = "";
            return;
        }
        if (keys.IsKeyDown(Keys.PageUp) && !prevKeys.IsKeyDown(Keys.PageUp))
            ChatScroll += 3;
        if (keys.IsKeyDown(Keys.PageDown) && !prevKeys.IsKeyDown(Keys.PageDown))
            ChatScroll = Math.Max(0, ChatScroll - 3);
        if (keys.IsKeyDown(Keys.Home) && !prevKeys.IsKeyDown(Keys.Home))
            // не int.MaxValue: PageUp += 3 вызывал переполнение в отрицательное
            ChatScroll = int.MaxValue / 2;
        if (keys.IsKeyDown(Keys.End) && !prevKeys.IsKeyDown(Keys.End))
            ChatScroll = 0;
        // Клик по вкладкам журнала (мышь в координатах дизайна 1920×1080)
        var chatMouse = ScaledMouse();
        if (chatMouse.LeftButton == ButtonState.Pressed)
        {
            for (int i = 0; i < ChatPanel.TabNames.Length; i++)
            {
                if (ChatPanel.TabRect(i).Contains(chatMouse.X, chatMouse.Y))
                {
                    ChatViewTab = i;
                    ChatScroll = 0;
                }
            }
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
                // Результат раньше игнорировался: «Загружено» показывалось
                // даже при отказе ApplyLoad на битых данных.
                bool ok = SaveSystem.ApplyLoad(this, data);
                ShowMessage(ok
                    ? $"Загружено из слота {CurrentSlot}"
                    : $"Ошибка загрузки: {SaveSystem.LastError}");
                if (ok) Audio?.Play("pickup");
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
                    GameChat.Instance.Item($"Использовано: {ItemData.DisplayName(itemKey)}");
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
    private void TryLearnNode(int index)
    {
        if (ProgressionSystem.LearnNode(Player, index, out string message))
        {
            ShowMessage(message);
            GameChat.Instance.Xp(message);   // изучение навыка — вкладка «Опыт»
        }
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
            {
                ShowMessage($"Новый класс: {ClassData.Get(newKey)?.Name}!");
                GameChat.Instance.Level($"Класс сменён: {ClassData.Get(newKey)?.Name}!");
            }
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
        {
            ShowMessage(message);
            GameChat.Instance.Level(message);   // смена класса — вкладка «Опыт»
        }
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
                {
                    ShowMessage($"Применено: {def.Name}");
                    GameChat.Instance.Item($"Применено: {def.Name}");
                }
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
                    GameChat.Instance.Item($"Использовано: {def.Name}");
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
        MarkVisited(to);
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
            // Если спускались из локации «Подземелье» — вернуться туда,
            // иначе — на поверхность (оверворлд).
            if (_currentLocation?.LocationKey == "dungeon")
            {
                _worldMode = "overworld";
                Dungeon = _currentLocation;
                Depth = 0;
                if (Dungeon.StairsPos.HasValue)
                {
                    Player.X = Dungeon.StairsPos.Value.Item1;
                    Player.Y = Dungeon.StairsPos.Value.Item2;
                }
                ShowMessage("Вы поднялись в зал подземелья.");
            }
            else
            {
                ExitToOverworld();
            }
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
        MarkVisited(key);
        var (entryX, entryY) = (1, map.H / 2);
        if (CurrentMap.Tiles[entryY][entryX] == Tile.Wall)
            CurrentMap.Tiles[entryY][entryX] = Tile.Floor;
        Player.X = entryX;
        Player.Y = entryY;
        CurrentMap.RevealAround(entryX, entryY, Config.RevealRadius);
        ShowMessage($"Вы вошли в: {Locations.All[key].Name}.");
        return true;
    }

    /// <summary>
    /// Быстрый переход с большой карты — только в посещённые локации.
    /// </summary>
    public bool FastTravel(string key)
    {
        if (!Locations.All.ContainsKey(key) || !VisitedLocations.Contains(key)) return false;
        if (key == "village")
        {
            Projectiles.Clear();
            _worldMode = "overworld";
            _currentLocation = null;
            Player.X = Aethelgard.World.Overworld.VillageCenter.Item1;
            Player.Y = Aethelgard.World.Overworld.VillageCenter.Item2;
            Overworld.RevealAround(Player.X, Player.Y, Config.RevealRadius);
            ShowMessage("Вы вернулись в Город Этельгард.");
            return true;
        }
        return GotoLocation(key);
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
        // Оружие может быть заточено ("sword+2") — ищем деф по базовому ключу,
        // иначе Get вернёт null и дальнобойная атака молча пропадёт.
        var (wBase, _) = ItemData.ParseItemKey(weapon ?? "");
        var wdef = ItemData.Get(wBase) ?? ItemData.Get(weapon);
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
            _statusTimer -= Config.StatusTickInterval;   // -= вместо = 0: часть интервала не теряется
            Player.TickStatuses();
            foreach (var m in CurrentMap.Monsters)
                m.TickStatuses();
        }

        // Regen tick
        _regenTimer += dt;
        if (_regenTimer >= Config.RegenTickInterval)
        {
            _regenTimer -= Config.RegenTickInterval;
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
        MonsterAI.Update(CurrentMap, PlayerTargets, dt,
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

    // Кэш-массив цели снарядов и MonsterAI: раньше new[]{Player} аллоцировался
    // на каждый снаряд (и в MonsterAI.Update) каждый кадр.
    private Player[]? _playerTargetCache;
    private IReadOnlyList<Player> PlayerTargets =>
        _playerTargetCache != null && ReferenceEquals(_playerTargetCache[0], Player)
            ? _playerTargetCache
            : _playerTargetCache = new[] { Player };

    private void UpdateProjectiles(float dt)
    {
        for (int i = Projectiles.Count - 1; i >= 0; i--)
        {
            var p = Projectiles[i];
            // Снаряды монстров целятся в игрока, остальные — в монстров.
            IReadOnlyList<Entity> targets = p.SourcePid == -1
                ? PlayerTargets
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
    /// Ввод сообщения журнала во вкладку «Общение» (одиночная игра).
    /// Enter — отправить, Esc — отмена, Backspace — стереть.
    /// </summary>
    private void HandleChatInput(KeyboardState keys, KeyboardState prevKeys)
    {
        bool shift = keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift);
        bool submit, cancel;
        if (EditChatLine(keys, prevKeys, ref ChatLogInput, shift, out submit, out cancel))
        {
            if (submit && ChatLogInput.Trim().Length > 0)
                Chat.Social($"[{Player.ClassName}] {ChatLogInput.Trim()}");
            ChatInputOpen = false;
            ChatLogInput = "";
        }
    }

    /// <summary>
    /// Общая правка однострочного ввода чата. Раньше SP и MP дублировали
    /// ~50 строк этой логики — правка в одном месте не попадала во второй.
    /// Возвращает true, если клавиша обработана (в т.ч. «ничего не делать»);
    /// submit — Enter, cancel — Esc. Символы с учётом раскладки
    /// (Window.TextInput): русская клавиатура печатает русские буквы.
    /// TextKeyMap — запасной путь для сред без TextInput-событий.
    /// </summary>
    private bool EditChatLine(KeyboardState keys, KeyboardState prevKeys,
        ref string line, bool shift, out bool submit, out bool cancel)
    {
        submit = false; cancel = false;
        if (keys.IsKeyDown(Keys.Enter) && !prevKeys.IsKeyDown(Keys.Enter))
        {
            submit = true;
            return true;
        }
        if (keys.IsKeyDown(Keys.Escape) && !prevKeys.IsKeyDown(Keys.Escape))
        {
            cancel = true;
            return true;
        }
        if (keys.IsKeyDown(Keys.Back) && !prevKeys.IsKeyDown(Keys.Back))
        {
            if (line.Length > 0) line = line[..^1];
            return true;
        }
        if (line.Length >= 120) return true;
        bool gotText = false;
        while (TextInput.TryDequeue(out char ch))
        {
            if (char.IsControl(ch)) continue;
            if (line.Length < 120)
            {
                line += ch;
                gotText = true;
            }
        }
        if (gotText) return true;
        foreach (var (key, ch) in TextKeyMap)
        {
            if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key))
            {
                char c = ch;
                if (shift && char.IsLetter(c)) c = char.ToUpper(c);
                line += c;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Play sound for an event.
    /// </summary>
    public void PlaySound(string name)
    {
        Audio?.Play(name);
    }

    /// <summary>
    /// Мышь в координатах дизайна (1920×1080) из сырого Mouse.GetState.
    /// Общий хелпер: раньше SP и MP дублировали эту конструкцию.
    /// </summary>
    protected static MouseState ScaledMouse()
    {
        var cm = Mouse.GetState();
        return new MouseState(
            (int)(cm.X * AethelgardGame.MouseScaleX),
            (int)(cm.Y * AethelgardGame.MouseScaleY),
            cm.ScrollWheelValue, cm.LeftButton, cm.MiddleButton,
            cm.RightButton, cm.XButton1, cm.XButton2);
    }

    // ── Multiplayer ──

    /// <summary>
    /// Старт MP-игры как клиент (подключение к чужому серверу).
    /// </summary>
}
