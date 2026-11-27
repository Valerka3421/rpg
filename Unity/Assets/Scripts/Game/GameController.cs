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
public partial class GameController : ISaveableGame
{
    // Явная реализация ISaveableGame: поля Player/Depth/Overworld/Quests/
    // RunStart не удовлетворяют свойствам интерфейса неявно.
    Player ISaveableGame.Player { get => Player; }
    int ISaveableGame.Depth { get => Depth; }
    Dungeon ISaveableGame.Overworld { get => Overworld; set => Overworld = value; }
    QuestLog ISaveableGame.Quests { get => Quests; }
    DateTime ISaveableGame.RunStart { get => RunStart; set => RunStart = value; }

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
        Modals.Clear();
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
            if (ScreenMode == Screens.Inventory) CloseModal(); else OpenModal(Screens.Inventory);
            return;
        }

        // Toggle skills screen
        if (keys.IsKeyDown(Keys.N) && !prevKeys.IsKeyDown(Keys.N))
        {
            if (ScreenMode == Screens.Skills) CloseModal(); else OpenModal(Screens.Skills);
            return;
        }

        // Craft screen (K)
        if (keys.IsKeyDown(Keys.K) && !prevKeys.IsKeyDown(Keys.K))
        {
            if (ScreenMode == Screens.Craft) CloseModal(); else OpenModal(Screens.Craft);
            CraftPage = 0;
            return;
        }

        // Stats screen (C)
        if (keys.IsKeyDown(Keys.C) && !prevKeys.IsKeyDown(Keys.C))
        {
            if (ScreenMode == Screens.Stats) CloseModal(); else OpenModal(Screens.Stats);
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
                if (ScreenMode == Screens.Quests) CloseModal(); else OpenModal(Screens.Quests);
            }
            else
                ShowMessage("Квесты доступны у доски поручений, старейшины или торговца");
            return;
        }

        // Pause menu (Esc)
        if (keys.IsKeyDown(Keys.Escape) && !prevKeys.IsKeyDown(Keys.Escape))
        {
            if (ScreenMode == Screens.Pause) CloseModal(); else OpenModal(Screens.Pause);
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
                CloseModal();
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
            OpenModal(Screens.StatsRun);
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
            if (ScreenMode == Screens.Bigmap) CloseModal(); else OpenModal(Screens.Bigmap);
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
            CloseModal();
    }

    private void HandlePauseInput(KeyboardState keys, KeyboardState prevKeys)
    {
        if (keys.IsKeyDown(Keys.Escape) && !prevKeys.IsKeyDown(Keys.Escape))
            CloseModal();
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
