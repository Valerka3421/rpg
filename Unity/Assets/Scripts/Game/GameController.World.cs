using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Screens = Aethelgard.Systems.ScreenMode;
namespace Aethelgard.Systems;

/// <summary>
/// Мир: перемещение, лестницы/ворота, переходы между этажами и
/// локациями, быстрое перемещение, кэши карт. Выделено из GameController
/// (частичный класс).
/// </summary>
public partial class GameController
{
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
        EnterLocationAt(map, entry);
        ShowMessage($"Вы вошли в: {Locations.All[to].Name}.");
    }

    /// <summary>
    /// Разместить игрока у точки входа локации. Раньше entry из перехода
    /// выбрасывался и вход хардкодился в (1, H/2), хотя TransitionSave
    /// честно сериализовал и восстанавливал EntryX/EntryY.
    /// </summary>
    private void EnterLocationAt(Dungeon map, (int X, int Y)? entry)
    {
        // Вход — у врат локации (врата в первый столбец, вход справа от них)
        var (entryX, entryY) = entry ?? (1, map.H / 2);
        entryX = Math.Clamp(entryX, 1, map.W - 2);
        entryY = Math.Clamp(entryY, 1, map.H - 2);
        if (CurrentMap.Tiles[entryY][entryX] == Tile.Wall)
            CurrentMap.Tiles[entryY][entryX] = Tile.Floor;
        Player.X = entryX;
        Player.Y = entryY;
        CurrentMap.RevealAround(entryX, entryY, Config.RevealRadius);
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
        EnterLocationAt(map, null);
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
        Modals.Clear();
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
}
