using System;
using System.Collections.Generic;
using System.Linq;

namespace Aethelgard.World;

/// <summary>
/// ALL 13 locations — 100% parity with Python core/locations.py.
/// Each location is a separate map with its own generator.
///
/// Генерация переписана «с нуля»: вместо однотипного шума прямоугольных
/// стен каждая локация строится ПО СМЫСЛУ своего типа — ограда по
/// периметру с единственными воротами назад в деревню, дорога от ворот
/// к центральной площади и тематические зоны (штольни в шахте, поля,
/// палаточный лагерь, руинные кварталы и т.д.). Вся генерация идёт от
/// Random(StableSeed(key)) — одна и та же локация всегда даёт одну и
/// ту же карту (тест Locations_AreReproducibleForSameSeed).
/// </summary>
public static class Locations
{
    public class LocDef
    {
        public string Key, Name;
        public int W, H;
        public string Biome;  // for rendering
        public bool IsDungeon;
    }

    public static readonly Dictionary<string, LocDef> All = new()
    {
        ["village"]       = new() { Key="village", Name="Город Этельгард", W=100, H=70, Biome="village" },
        ["dungeon"]       = new() { Key="dungeon", Name="Подземелье", W=60, H=40, Biome="dungeon", IsDungeon=true },
        ["goblin_village"]= new() { Key="goblin_village", Name="Деревня гоблинов", W=120, H=70, Biome="goblin" },
        ["mine"]          = new() { Key="mine", Name="Заброшенная шахта", W=120, H=70, Biome="mine", IsDungeon=true },
        ["pine"]          = new() { Key="pine", Name="Сосновый бор", W=120, H=70, Biome="pine" },
        ["birch"]         = new() { Key="birch", Name="Берёзовая роща", W=120, H=70, Biome="birch" },
        ["rock"]          = new() { Key="rock", Name="Каменный перевал", W=120, H=70, Biome="rock" },
        ["autumn"]        = new() { Key="autumn", Name="Осенний лес", W=120, H=70, Biome="autumn" },
        ["swamp"]         = new() { Key="swamp", Name="Гиблое болото", W=120, H=70, Biome="swamp" },
        ["meadow"]        = new() { Key="meadow", Name="Цветущая поляна", W=120, H=70, Biome="meadow" },
        ["dead"]          = new() { Key="dead", Name="Мёртвый лес", W=120, H=70, Biome="dead" },
        ["beach"]         = new() { Key="beach", Name="Морской пляж", W=120, H=70, Biome="beach" },
        ["graveyard"]     = new() { Key="graveyard", Name="Старое кладбище", W=120, H=70, Biome="dead" },
        ["ruins"]         = new() { Key="ruins", Name="Руины города", W=120, H=70, Biome="ruins" },
    };

    /// <summary>
    /// Generate a location map by key.
    /// </summary>
    /// <param name="seed">Зерно. null — стабильное зерно от ключа локации,
    /// поэтому одна и та же локация всегда генерируется одинаково.</param>
    public static Dungeon GenerateLocation(string key, int? seed = null)
    {
        int actualSeed = seed ?? StableSeed(key);

        if (!All.TryGetValue(key, out var def))
            return WorldGen.GenerateOverworld(actualSeed);

        // Village uses the overworld generator (embedded village)
        if (key == "village")
            return WorldGen.GenerateOverworld(actualSeed);

        // Dungeon — отдельная локация-вход: каменные залы с лестницей вниз
        // (механика этажей Depth остаётся прежней) и вратами назад в деревню.
        if (key == "dungeon")
            return GenerateDungeonEntrance(def, actualSeed);

        // --- Наземные локации: общий каркас + тематические зоны ---
        var d = new Dungeon(def.W, def.H);
        d.LocationKey = key;
        d.Rooms = new List<Room>(); // для карт локаций комнаты не отслеживаются
        var rng = new Random(actualSeed);

        // 1) Основание карты: пол + ограда по периметру.
        //    Ограда задаёт «границу мира»: наружу не выйти, есть ровно
        //    одни ворота назад в деревню (левый край, середина).
        FillFloor(d);
        Fence(d);

        // 2) Дорога от ворот к центральной площади. Кроется ДО зон и
        //    ПОСЛЕ них — чтобы ни одна зона не перекрыла путь.
        CarveRoadAndPlaza(d);

        // 3) Тематические зоны по типу локации.
        switch (key)
        {
            case "pine":   case "birch":  case "autumn": case "dead":
                BuildForest(d, rng, def, key); break;
            case "rock":    BuildRockyPass(d, rng, def);   break;
            case "swamp":   BuildSwamp(d, rng, def);       break;
            case "meadow":  BuildMeadow(d, rng, def);      break;
            case "beach":   BuildBeach(d, rng, def);       break;
            case "mine":    BuildMine(d, rng, def);        break;
            case "goblin_village": BuildGoblinCamp(d, rng, def); break;
            case "ruins":   BuildRuins(d, rng, def);       break;
            case "graveyard": BuildGraveyard(d, rng, def); break;
            default: ScatterThicket(d, rng, 0.30f);        break;
        }

        // 4) Гарантия проходимости: ворота и ограда восстанавливаются
        //    заново (шахта заливает карту породой целиком и затирает
        //    ворота), дорога/площадь перекраиваются — точка входа у
        //    ворот всегда проходима.
        Fence(d);
        CarveRoadAndPlaza(d);
        int gy = def.H / 2;
        if (d.Tiles[gy][1] == Tile.Wall) d.Tiles[gy][1] = Tile.Floor;

        // 5) Достижимая зона (BFS от входа) — спавн только на ней,
        //    чтобы игрок никогда не упирался в «недостижимые» монстры.
        var reach = Reachable(d, 1, gy);

        // 6) Монстры по биому
        SpawnMonsters(d, rng, def.Biome, def.Biome == "goblin" ? 15 : 10, reach);
        Rules.InitActionTimers(d.Monsters);

        // 7) Предметы по биому (не на ресурсах — иначе их не подобрать)
        ScatterItems(d, rng, def.Biome, 15, reach);

        return d;
    }

    // ==================================================================
    //                         ОБЩИЙ КАРКАС
    // ==================================================================

    private static void FillFloor(Dungeon d)
    {
        for (int y = 0; y < d.H; y++)
            for (int x = 0; x < d.W; x++)
                d.Tiles[y][x] = Tile.Floor;
    }

    /// <summary>
    /// Ограда по периметру карты + единственные ворота назад в деревню
    /// на левом краю. Переход ведёт в VillageCenter оверворлда.
    /// </summary>
    private static void Fence(Dungeon d)
    {
        for (int x = 0; x < d.W; x++) { d.Tiles[0][x] = Tile.Wall; d.Tiles[d.H - 1][x] = Tile.Wall; }
        for (int y = 0; y < d.H; y++) { d.Tiles[y][0] = Tile.Wall; d.Tiles[y][d.W - 1] = Tile.Wall; }

        int gy = d.H / 2;
        d.Tiles[gy][0] = Tile.Gate;
        d.Transitions[(0, gy)] = ("village",
            (Aethelgard.World.Overworld.VillageCenter.Item1,
             Aethelgard.World.Overworld.VillageCenter.Item2));
    }

    /// <summary>
    /// Дорога от ворот к центру карты + центральная площадь (расчищенный
    /// прямоугольник). Дорога — «хребет» локации: от неё расходятся зоны,
    /// вдоль неё стоят кусты ягод (смысловая расстановка Harvestables).
    /// </summary>
    private static void CarveRoadAndPlaza(Dungeon d)
    {
        int cy = d.H / 2, cx = d.W / 2;
        for (int x = 1; x <= cx; x++)
            d.Tiles[cy][x] = Tile.Floor;
        // Площадь: расчистка вокруг центра (деревья/здания туда не сажаем)
        for (int y = cy - 3; y <= cy + 3; y++)
            for (int x = cx - 5; x <= cx + 5; x++)
                if (x > 0 && x < d.W - 1 && y > 0 && y < d.H - 1)
                    d.Tiles[y][x] = Tile.Floor;
    }

    /// <summary>
    /// BFS от входа: все клетки, достижимые по полу без блокирующих
    /// ресурсов. Итерация по очереди строго детерминирована, поэтому
    /// порядок списка (и выбор из него rng) воспроизводим.
    /// </summary>
    private static List<(int, int)> Reachable(Dungeon d, int sx, int sy)
    {
        var seen = new HashSet<(int, int)> { (sx, sy) };
        var queue = new Queue<(int, int)>();
        queue.Enqueue((sx, sy));
        var order = new List<(int, int)>();
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            order.Add((x, y));
            foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
            {
                if (nx < 0 || ny < 0 || nx >= d.W || ny >= d.H) continue;
                if (seen.Contains((nx, ny))) continue;
                if (d.Tiles[ny][nx] != Tile.Floor) continue;
                if (d.Harvestables.ContainsKey((nx, ny))) continue; // ресурс блокирует клетку
                seen.Add((nx, ny));
                queue.Enqueue((nx, ny));
            }
        }
        return order;
    }

    private static void SpawnMonsters(Dungeon d, Random rng, string biome, int count, List<(int, int)> reach)
    {
        var pool = MonstersOf(biome);
        int spawned = 0;
        for (int attempt = 0; attempt < 300 && spawned < count; attempt++)
        {
            var (x, y) = reach[rng.Next(reach.Count)];
            if (d.MonsterAt(x, y) != null || d.Items.ContainsKey((x, y))) continue;
            string key2 = pool[rng.Next(pool.Length)];
            if (!MonsterData.All.ContainsKey(key2)) continue;
            var m = MonsterData.CreateEntity(key2, 1);
            m.X = x; m.Y = y;
            d.Monsters.Add(m);
            spawned++;
        }
    }

    private static void ScatterItems(Dungeon d, Random rng, string biome, int count, List<(int, int)> reach)
    {
        var itemPool = ItemsOf(biome);
        for (int i = 0; i < count; i++)
        {
            var (x, y) = reach[rng.Next(reach.Count)];
            if (d.Items.ContainsKey((x, y)) || d.Harvestables.ContainsKey((x, y))) continue;
            d.Items[(x, y)] = itemPool[rng.Next(itemPool.Length)];
        }
    }

    // ==================================================================
    //                     ГЕОМЕТРИЧЕСКИЕ ХЕЛПЕРЫ
    // ==================================================================

    /// <summary>
    /// Круглое пятно стен («пятно деревьев», «валун», «заросли»).
    /// RNG передаётся явно — вся генерация идёт от одного зерна.
    /// </summary>
    private static void Blob(Dungeon d, Random rng, int cx, int cy, int r, double density)
    {
        int r2 = r * r;
        for (int y = cy - r; y <= cy + r; y++)
        {
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (x < 1 || y < 1 || x >= d.W - 1 || y >= d.H - 1) continue;
                int dx = x - cx, dy = y - cy;
                if (dx * dx + dy * dy > r2) continue;
                // Край пятна рваный — выглядит естественно, а не как штамп
                double edge = 1.0 - 0.55 * ((dx * dx + dy * dy) / (double)r2);
                if (rng.NextDouble() < density * edge)
                    d.Tiles[y][x] = Tile.Wall;
            }
        }
    }

    /// <summary>Контур прямоугольной стены (постройка) с проёмом-дверью.</summary>
    private static void Building(Dungeon d, int x, int y, int w, int h, int doorSide, Random rng)
    {
        for (int xx = x; xx < x + w; xx++) { d.Tiles[y][xx] = Tile.Wall; d.Tiles[y + h - 1][xx] = Tile.Wall; }
        for (int yy = y; yy < y + h; yy++) { d.Tiles[yy][x] = Tile.Wall; d.Tiles[yy][x + w - 1] = Tile.Wall; }
        // Дверь: 0=сверху, 1=снизу, 2=слева, 3=справа — рваная (1-2 клетки),
        // чтобы постройки не выглядели одинаковыми штампами
        int doorLen = rng.Next(1, 3);
        if (doorSide == 0 || doorSide == 1)
        {
            int dy = doorSide == 0 ? y : y + h - 1;
            int dx = x + w / 2;
            for (int i = 0; i < doorLen && dx + i < x + w - 1; i++) d.Tiles[dy][dx + i] = Tile.Floor;
        }
        else
        {
            int dx = doorSide == 2 ? x : x + w - 1;
            int dy = y + h / 2;
            for (int i = 0; i < doorLen && dy + i < y + h - 1; i++) d.Tiles[dy + i][dx] = Tile.Floor;
        }
    }

    // ==================================================================
    //                    ТЕМАТИЧЕСКИЕ ГЕНЕРАТОРЫ ЗОН
    // ==================================================================

    /// <summary>
    /// Леса (pine/birch/autumn/dead): кластеры деревьев вокруг случайных
    /// центров, поляна-площадь в центре свободна. Мёртвый лес реже и с
    /// «просеками» — там погибший древостой, а не чаща. Деревья как
    /// Harvestables ставятся на часть стеновых клеток кластеров — рубить
    /// можно то, что визуально дерево.
    /// </summary>
    private static void BuildForest(Dungeon d, Random rng, LocDef def, string key)
    {
        bool deadForest = key == "dead";
        int clusters = deadForest ? 9 : 14;
        int treeChance = deadForest ? 15 : 35; // % стеновых клеток кластера с рубимым деревом

        for (int i = 0; i < clusters; i++)
        {
            int cx = rng.Next(8, d.W - 8);
            int cy = rng.Next(6, d.H - 6);
            // Кластеры не должны накрывать центральную площадь и дорогу:
            // дорога — единственный «хребет», оставляем коридор вокруг неё
            if (Math.Abs(cy - d.H / 2) < 5 && cx > d.W / 2 - 10 && cx < d.W / 2 + 10) continue;
            int r = rng.Next(3, deadForest ? 5 : 7);
            Blob(d, rng, cx, cy, r, deadForest ? 0.55 : 0.8);
        }
        // Плотная «стена леса» по углам карты — лес окружает поляну
        foreach (var (cx, cy) in new[] { (10, 10), (d.W - 11, 10), (10, d.H - 11), (d.W - 11, d.H - 11) })
            Blob(d, rng, cx, cy, 7, 0.85);

        // Осень/берёза: пара прогалина-полян с кустами ягод — ягоды ищут
        // по краям леса, у дорог, а не в чаще
        for (int i = 0; i < 3; i++)
        {
            int bx = rng.Next(10, d.W - 10), by = rng.Next(5, d.H - 5);
            if (Math.Abs(by - d.H / 2) < 4) continue;
            Blob(d, rng, bx, by, 2, 0.3);
        }

        // Рубимые деревья на стеновых клетках (кластеры уже стоят)
        PlantHarvestableOnWalls(d, rng, "tree", treeChance);
        // Кусты вдоль дороги — ягоды «у дорог» по смыслу
        BushesAlongRoad(d, rng, deadForest ? 5 : 9);
    }

    /// <summary>
    /// Каменный перевал: скальные гряды поперёк карты с проходами —
    /// перевал именно «переваливается» через хребты. Валуны содержат
    /// руду (Harvestable «ore» на камне — смысловая добыча).
    /// </summary>
    private static void BuildRockyPass(Dungeon d, Random rng, LocDef def)
    {
        // 3 скальные гряды поперёк карты, в каждой — 2 прохода
        for (int i = 0; i < 3; i++)
        {
            int ry = 10 + i * (d.H - 20) / 3 + rng.Next(-2, 3);
            int gap1 = rng.Next(8, d.W / 2 - 6);
            int gap2 = rng.Next(d.W / 2 + 6, d.W - 8);
            for (int x = 1; x < d.W - 1; x++)
            {
                if (Math.Abs(x - gap1) <= 2 || Math.Abs(x - gap2) <= 2) continue;
                // Толщина гряды рваная — не прямая линия
                int thick = 2 + rng.Next(3);
                for (int dy = 0; dy < thick && ry + dy < d.H - 1; dy++)
                    d.Tiles[ry + dy][x] = Tile.Wall;
            }
        }
        // Отдельные валуны по склонам
        for (int i = 0; i < 25; i++)
        {
            int x = rng.Next(3, d.W - 3), y = rng.Next(3, d.H - 3);
            if (Math.Abs(y - d.H / 2) < 4 && x < d.W / 2 + 6) continue; // не на дороге/площади
            Blob(d, rng, x, y, rng.Next(1, 3), 0.85);
        }

        // Руда на валунах у проходов: шахтёрский смысл «перевала»
        PlaceOreNearGaps(d, rng);
        PlantHarvestableOnWalls(d, rng, "ore", 18);
        PlantHarvestableOnWalls(d, rng, "tree", 8); // редкие одинокие сосны
        BushesAlongRoad(d, rng, 4);
    }

    /// <summary>
    /// Гиблое болото: густые заросли-«плавни» (стены читаются как трясина
    /// и камыши — вода отдельным тайлом не моделируется), тропа от ворот
    /// петляет к площади. Ягод почти нет — болото не ягодное.
    /// </summary>
    private static void BuildSwamp(Dungeon d, Random rng, LocDef def)
    {
        // Много мелких пятен трясины, гуще к краям
        for (int i = 0; i < 40; i++)
        {
            bool nearEdge = i % 3 == 0;
            int x = nearEdge ? rng.Next(2, 14) : rng.Next(14, d.W - 14);
            int y = rng.Next(2, d.H - 2);
            if (Math.Abs(y - d.H / 2) < 4 && x > 5 && x < d.W / 2 + 10) continue;
            Blob(d, rng, x, y, rng.Next(2, 5), 0.75);
        }
        // «Топь»-запад: широкое непроходимое пятно в дальнем углу
        Blob(d, rng, d.W - 18, d.H - 14, 10, 0.9);

        PlantHarvestableOnWalls(d, rng, "tree", 20); // кривые болотные деревья
        BushesAlongRoad(d, rng, 4);
    }

    /// <summary>
    /// Цветущая поляна: открытая карта, редкие рощи-островки и живые
    /// изгороди (кусты как Harvestables — их много и они смыслово
    /// «ягодные кусты по краю поля»).
    /// </summary>
    private static void BuildMeadow(Dungeon d, Random rng, LocDef def)
    {
        // Рощи-островки
        for (int i = 0; i < 7; i++)
        {
            int cx = rng.Next(10, d.W - 10), cy = rng.Next(6, d.H - 6);
            if (Math.Abs(cy - d.H / 2) < 5 && Math.Abs(cx - d.W / 2) < 10) continue;
            Blob(d, rng, cx, cy, rng.Next(2, 4), 0.6);
        }
        PlantHarvestableOnWalls(d, rng, "tree", 40);
        // Изгороди: короткие линии кустов на открытых клетках
        for (int i = 0; i < 6; i++)
        {
            int x = rng.Next(10, d.W - 20), y = rng.Next(4, d.H - 4);
            if (Math.Abs(y - d.H / 2) < 5) continue;
            int len = rng.Next(4, 9);
            bool horiz = rng.Next(2) == 0;
            for (int j = 0; j < len; j++)
            {
                int hx = horiz ? x + j : x, hy = horiz ? y : y + j;
                if (hx < 1 || hy < 1 || hx >= d.W - 1 || hy >= d.H - 1) continue;
                if (d.Tiles[hy][hx] != Tile.Floor) continue;
                d.Harvestables[(hx, hy)] = HarvestSystem.MakeHarvestable("bush");
            }
        }
        BushesAlongRoad(d, rng, 10);
    }

    /// <summary>
    /// Морской пляж: дюны вдоль верхнего края, скалы у дальнего края
    /// (море — за пределами карты), редкие деревья у дюн. Открытая
    /// полоса вдоль дороги — сам пляж.
    /// </summary>
    private static void BuildBeach(Dungeon d, Random rng, LocDef def)
    {
        // Дюны: волнистая полоса стен вдоль верхнего края
        int ridge = 4;
        for (int x = 1; x < d.W - 1; x++)
        {
            ridge += rng.Next(-1, 2);
            ridge = Math.Clamp(ridge, 2, 8);
            for (int y = 1; y <= ridge; y++)
                d.Tiles[y][x] = Tile.Wall;
        }
        // Скалы в дальнем правом углу — мыс
        Blob(d, rng, d.W - 16, 10, 8, 0.85);
        Blob(d, rng, d.W - 24, 14, 5, 0.8);
        // Редкие коряги-деревья на дюнах
        PlantHarvestableOnWalls(d, rng, "tree", 12);
        PlantHarvestableOnWalls(d, rng, "ore", 10); // у мыса можно добыть камень
        BushesAlongRoad(d, rng, 3);
    }

    /// <summary>
    /// Заброшенная шахта: карта — сплошной камень, из которого вырезаны
    /// штольни: вход-коридор от ворот, центральный зал (площадь), четыре
    /// выработки по квадрантам, соединённые откаточными коридорами, и
    /// тупиковые забои. Руда (Harvestable «ore») стоит у стен выработок —
    /// её добывают там, где «жила вышла к стенке».
    /// </summary>
    private static void BuildMine(Dungeon d, Random rng, LocDef def)
    {
        // Карта целиком — порода
        for (int y = 0; y < d.H; y++)
            for (int x = 0; x < d.W; x++)
                d.Tiles[y][x] = Tile.Wall;

        var chambers = new List<(int X, int Y, int W, int H)>();

        // Центральный зал — вокруг площади (совпадает с CarveRoadAndPlaza)
        int cx = d.W / 2, cy = d.H / 2;
        chambers.Add((cx - 6, cy - 4, 13, 9));

        // Четыре выработки по квадрантам (координаты фиксированные, размеры
        // слегка варьируются зерном — карта различима, но связна всегда)
        chambers.Add((12, 8 + rng.Next(0, 3), 14 + rng.Next(0, 4), 8));
        chambers.Add((d.W - 34, 10 + rng.Next(0, 3), 15 + rng.Next(0, 4), 8));
        chambers.Add((16, d.H - 18 - rng.Next(0, 3), 16, 8));
        chambers.Add((d.W - 36, d.H - 20 - rng.Next(0, 3), 17, 8));

        foreach (var (rx, ry, rw, rh) in chambers)
            for (int y = ry; y < ry + rh; y++)
                for (int x = rx; x < rx + rw; x++)
                    if (x > 0 && x < d.W - 1 && y > 0 && y < d.H - 1)
                        d.Tiles[y][x] = Tile.Floor;

        // Вход-коридор от ворот к центральному залу
        for (int x = 1; x <= cx - 6; x++) d.Tiles[cy][x] = Tile.Floor;

        // Откаточные коридоры: зал ↔ каждая выработка (L-образно)
        foreach (var (rx, ry, rw, rh) in chambers.Skip(1))
        {
            int tx = rx + rw / 2, ty = ry + rh / 2;
            int x = cx, y = cy;
            for (; x != tx; x += Math.Sign(tx - x)) d.Tiles[y][x] = Tile.Floor;
            for (; y != ty; y += Math.Sign(ty - y)) d.Tiles[y][x] = Tile.Floor;
        }

        // Тупиковые забои: от каждой выработки 1-2 коротких туннеля
        foreach (var (rx, ry, rw, rh) in chambers.Skip(1))
        {
            int digs = rng.Next(1, 3);
            for (int i = 0; i < digs; i++)
            {
                int sx = rx + rng.Next(1, rw - 1), sy = ry + rh / 2;
                bool up = rng.Next(2) == 0;
                int len = rng.Next(3, 7);
                for (int j = 1; j <= len; j++)
                {
                    int ty2 = up ? sy - j : sy + j;
                    if (ty2 < 1 || ty2 >= d.H - 1) break;
                    d.Tiles[ty2][sx] = Tile.Floor;
                    // Наращиваем забой вширь — штольня, а не прокол
                    if (sx + 1 < d.W - 1) d.Tiles[ty2][sx + 1] = Tile.Floor;
                }
            }
        }

        // Деревянные крепи: одиночные опоры внутри выработок
        foreach (var (rx, ry, rw, rh) in chambers.Skip(1))
        {
            int props = rng.Next(2, 5);
            for (int i = 0; i < props; i++)
            {
                int px = rx + rng.Next(2, rw - 2), py = ry + rng.Next(2, rh - 2);
                d.Tiles[py][px] = Tile.Wall;
            }
        }

        // Рудные жилы: Harvestable «ore» на клетках пола, у которых ≥2
        // соседа — стенка (жила «выходит» к породе). Только в выработках,
        // не в коридорах — чтобы не блокировать проход.
        var veinCells = new List<(int, int)>();
        foreach (var (rx, ry, rw, rh) in chambers.Skip(1))
            for (int y = ry + 1; y < ry + rh - 1; y++)
                for (int x = rx + 1; x < rx + rw - 1; x++)
                {
                    int solid = 0;
                    if (d.Tiles[y - 1][x] == Tile.Wall) solid++;
                    if (d.Tiles[y + 1][x] == Tile.Wall) solid++;
                    if (d.Tiles[y][x - 1] == Tile.Wall) solid++;
                    if (d.Tiles[y][x + 1] == Tile.Wall) solid++;
                    if (solid >= 2 && d.Tiles[y][x] == Tile.Floor) veinCells.Add((x, y));
                }
        // Перемешиваем детерминированно (Fisher-Yates от того же rng)
        for (int i = veinCells.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (veinCells[i], veinCells[j]) = (veinCells[j], veinCells[i]);
        }
        int ores = Math.Min(14, veinCells.Count);
        for (int i = 0; i < ores; i++)
        {
            var (ox, oy) = veinCells[i];
            d.Harvestables[(ox, oy)] = HarvestSystem.MakeHarvestable("ore");
        }
    }

    /// <summary>
    /// Деревня гоблинов: частокол по периметру (ворота — единственный
    /// проём), центральное кострище-площадь, кольцо палаток (небольшие
    /// прямоугольные «шалаши») вокруг площади и караулки по углам.
    /// </summary>
    private static void BuildGoblinCamp(Dungeon d, Random rng, LocDef def)
    {
        // Второй внутренний частокол — деревня огорожена дважды, вход
        // через широкий проём напротив ворот
        int innerL = 6;
        for (int y = innerL; y < d.H - innerL; y++)
        {
            d.Tiles[y][innerL] = Tile.Wall;
            d.Tiles[y][d.W - 1 - innerL] = Tile.Wall;
        }
        for (int x = innerL; x < d.W - innerL; x++)
        {
            d.Tiles[innerL][x] = Tile.Wall;
            d.Tiles[d.H - 1 - innerL][x] = Tile.Wall;
        }
        int gy = d.H / 2;
        d.Tiles[gy][innerL] = Tile.Floor; d.Tiles[gy][innerL + 1] = Tile.Floor;

        // Кострище: кольцо камней вокруг центра площади (клетка центра
        // остаётся полом — там «огонь», к нему подходят по кругу)
        int cx = d.W / 2, cy = d.H / 2;
        foreach (var (dx, dy) in new[] { (-1, -1), (0, -1), (1, -1), (-1, 1), (0, 1), (1, 1), (-1, 0), (1, 0) })
            if (Math.Abs(dx) + Math.Abs(dy) == 2) // только диагонали — проходы с 4 сторон
                d.Tiles[cy + dy][cx + dx] = Tile.Wall;

        // Кольцо палаток: 8-10 шалшей 3x2 вокруг площади на радиусе 12-14
        for (int i = 0; i < 10; i++)
        {
            double ang = Math.PI * 2 * i / 10 + rng.NextDouble() * 0.3;
            int tx = cx + (int)(Math.Cos(ang) * 14);
            int ty = cy + (int)(Math.Sin(ang) * 9);
            tx = Math.Clamp(tx, 9, d.W - 12);
            ty = Math.Clamp(ty, 9, d.H - 12);
            Building(d, tx, ty, 3 + rng.Next(2), 2 + rng.Next(2), rng.Next(4), rng);
        }

        // Караулки у четырёх углов внутреннего частокола
        foreach (var (bx, by) in new[] { (9, 9), (d.W - 14, 9), (9, d.H - 13), (d.W - 14, d.H - 13) })
            Building(d, bx, by, 5, 4, rng.Next(4), rng);

        // Трофейные кусты и пары деревьев — гоблины окружили себя хворостом
        PlantHarvestableOnWalls(d, rng, "tree", 10);
        BushesAlongRoad(d, rng, 6);
    }

    /// <summary>
    /// Руины города: кварталы — прямоугольные контуры стен со «вывалами»
    /// (случайные бреши), улицы-просветы между ними, центральная площадь
    /// с колодцем (2×2 камня) и склеп в дальнем углу с единственной
    /// дверью. Валуны-обломки разбросаны по улицам.
    /// </summary>
    private static void BuildRuins(Dungeon d, Random rng, LocDef def)
    {
        // Кварталы: сетка 4×3 контуров зданий со случайными брешами
        for (int qy = 0; qy < 3; qy++)
        {
            for (int qx = 0; qx < 4; qx++)
            {
                int bx = 10 + qx * (d.W - 24) / 4 + rng.Next(-1, 2);
                int by = 8 + qy * (d.H - 20) / 3 + rng.Next(-1, 2);
                int bw = rng.Next(9, 14), bh = rng.Next(6, 9);
                if (by + bh >= d.H - 3 || bx + bw >= d.W - 3) continue;
                Building(d, bx, by, bw, bh, rng.Next(4), rng);
                // Вывалы: несколько случайных кусков контура «обвалились»
                int gaps = rng.Next(2, 5);
                for (int g = 0; g < gaps; g++)
                {
                    int gx = bx + rng.Next(1, bw - 1), gy2 = by + rng.Next(1, bh);
                    // ломаем только контур (проверяем, что это внешняя стена)
                    bool edge = gx == bx || gx == bx + bw - 1 || gy2 == by || gy2 == by + bh - 1;
                    if (edge && d.Tiles[gy2][gx] == Tile.Wall)
                        d.Tiles[gy2][gx] = Tile.Floor;
                }
            }
        }

        // Колодец на площади (юго-восточный угол площади — не на дороге)
        int cx = d.W / 2, cy = d.H / 2;
        d.Tiles[cy + 2][cx + 4] = Tile.Wall;
        d.Tiles[cy + 2][cx + 5] = Tile.Wall;
        d.Tiles[cy + 3][cx + 4] = Tile.Wall;
        d.Tiles[cy + 3][cx + 5] = Tile.Wall;

        // Склеп: замкнутая постройка в дальнем правом углу, дверь сверху
        int vaultW = 10, vaultH = 7;
        int vx = d.W - 16, vy = d.H - 12;
        Building(d, vx, vy, vaultW, vaultH, 0, rng);
        // Внутри склепа — сундук с добычей (смысловая «награда за руины»)
        int chestX = vx + vaultW / 2, chestY = vy + vaultH / 2;
        if (d.Tiles[chestY][chestX] == Tile.Floor)
        {
            d.Tiles[chestY][chestX] = Tile.Chest;
            var resources = new[] { "ore", "stone", "iron_ingot", "wood" };
            var loot = new Dictionary<string, int>();
            for (int j = 0; j < 2; j++)
            {
                string lk = resources[rng.Next(resources.Length)];
                loot[lk] = loot.GetValueOrDefault(lk) + rng.Next(2, 6);
            }
            d.ChestLoot[(chestX, chestY)] = loot;
        }

        // Обломки и одичавшие деревья на улицах
        for (int i = 0; i < 18; i++)
        {
            int x = rng.Next(3, d.W - 3), y = rng.Next(3, d.H - 3);
            if (d.Tiles[y][x] == Tile.Floor && Math.Abs(y - cy) + Math.Abs(x - cx) > 12)
                d.Tiles[y][x] = Tile.Wall;
        }
        PlantHarvestableOnWalls(d, rng, "tree", 12);
        BushesAlongRoad(d, rng, 5);
    }

    /// <summary>
    /// Старое кладбище: часовня в верхней части (постройка с дверью),
    /// строгие ряды надгробий в нижнем поле (одиночные камни через 3×2
    /// клетки с «пропусками» — часть могил потеряна), мёртвые деревья
    /// вдоль ограды, ограда-ограда уже есть общим каркасом.
    /// </summary>
    private static void BuildGraveyard(Dungeon d, Random rng, LocDef def)
    {
        // Часовня по центру выше дороги
        int cx = d.W / 2, cy = d.H / 2;
        Building(d, cx - 7, cy - 14, 14, 9, 1, rng);

        // Ряды надгробий: нижние 2/3 карты, шахматка 3×2 с пропусками
        for (int y = cy + 4; y < d.H - 3; y += 2)
        {
            for (int x = 6; x < d.W - 6; x += 3)
            {
                if (rng.NextDouble() < 0.18) continue;          // потерянная могила
                if (Math.Abs(y - cy) < 3 && x > cx - 8 && x < cx + 8) continue; // не на площади
                d.Tiles[y][x] = Tile.Wall;
            }
        }

        // Мёртвые деревья вдоль ограды (внутри, полоса 2-3 клетки)
        for (int i = 0; i < 14; i++)
        {
            bool top = i % 2 == 0;
            int x = rng.Next(4, d.W - 4);
            int y = top ? rng.Next(3, 7) : d.H - rng.Next(4, 7);
            if (d.Tiles[y][x] == Tile.Floor) Blob(d, rng, x, y, 1, 0.9);
        }
        PlantHarvestableOnWalls(d, rng, "tree", 10);
        BushesAlongRoad(d, rng, 3);
    }

    /// <summary>Запасной генератор для неизвестных биомов: редкие заросли.</summary>
    private static void ScatterThicket(Dungeon d, Random rng, float density)
    {
        for (int i = 0; i < 20; i++)
            Blob(d, rng, rng.Next(6, d.W - 6), rng.Next(4, d.H - 4), rng.Next(2, 5), density);
        PlantHarvestableOnWalls(d, rng, "tree", 25);
        BushesAlongRoad(d, rng, 6);
    }

    // ==================================================================
    //                  СМЫСЛОВАЯ РАССТАНОВКА РЕСУРСОВ
    // ==================================================================

    /// <summary>
    /// Поставить Harvestable заданного вида на часть стеновых клеток
    /// (рубить/добывать можно то, что визуально — дерево/камень с рудой).
    /// Клетка остаётся стеной: ресурс не перекрывает проходы, а стена и
    /// так непроходима.
    /// </summary>
    private static void PlantHarvestableOnWalls(Dungeon d, Random rng, string kind, int percent)
    {
        for (int y = 1; y < d.H - 1; y++)
        {
            for (int x = 1; x < d.W - 1; x++)
            {
                if (d.Tiles[y][x] != Tile.Wall) continue;
                if (d.Harvestables.ContainsKey((x, y))) continue;
                if (rng.Next(100) >= percent) continue;
                d.Harvestables[(x, y)] = HarvestSystem.MakeHarvestable(kind);
            }
        }
    }

    /// <summary>
    /// Кусты ягод вдоль дороги: «ягоды у кустов у дорог» — периодически
    /// над/под дорогой, с проходами (не сплошная стена из кустов).
    /// </summary>
    private static void BushesAlongRoad(Dungeon d, Random rng, int count)
    {
        int cy = d.H / 2;
        for (int i = 0; i < count; i++)
        {
            int x = rng.Next(4, d.W / 2 - 4);
            int side = rng.Next(2) == 0 ? -1 : 1;
            int y = cy + side * rng.Next(1, 3);
            if (y < 1 || y >= d.H - 1) continue;
            if (d.Tiles[y][x] != Tile.Floor) continue;
            if (d.Harvestables.ContainsKey((x, y)) || d.Items.ContainsKey((x, y))) continue;
            d.Harvestables[(x, y)] = HarvestSystem.MakeHarvestable("bush");
        }
    }

    /// <summary>
    /// Руда у проходов скальных гряд перевала — то, что ищут, идя через
    /// перевал: жилы прямо у тропы. Вызывается из BuildRockyPass ДО
    /// PlantHarvestableOnWalls, чтобы руда у проходов гарантированно была.
    /// </summary>
    private static void PlaceOreNearGaps(Dungeon d, Random rng)
    {
        int placed = 0;
        for (int attempt = 0; attempt < 60 && placed < 6; attempt++)
        {
            int x = rng.Next(4, d.W - 4), y = rng.Next(4, d.H - 4);
            if (d.Tiles[y][x] != Tile.Wall || d.Harvestables.ContainsKey((x, y))) continue;
            // Клетка рядом с полом — «у тропы»
            bool nearFloor =
                d.Tiles[y - 1][x] == Tile.Floor || d.Tiles[y + 1][x] == Tile.Floor ||
                d.Tiles[y][x - 1] == Tile.Floor || d.Tiles[y][x + 1] == Tile.Floor;
            if (!nearFloor) continue;
            d.Harvestables[(x, y)] = HarvestSystem.MakeHarvestable("ore");
            placed++;
        }
    }

    /// <summary>Пул монстров биома (для генерации и сводки на большой карте).</summary>
    public static string[] MonstersOf(string biome) => biome switch
    {
        "pine"   => new[] { "deer", "wolf", "rat" },
        "birch"  => new[] { "deer", "slime", "rat" },
        "rock"   => new[] { "wolf", "bear" },
        "autumn" => new[] { "deer", "boar", "wolf" },
        "swamp"  => new[] { "slime", "rat", "boar" },
        "dead"   => new[] { "wolf", "bear", "rat" },
        "meadow" => new[] { "deer", "boar", "rat" },
        "beach"  => new[] { "rat", "slime", "boar" },
        "goblin" => new[] { "goblin", "archer", "shaman", "boar", "wolf" },
        "mine"   => new[] { "skeleton", "orc", "troll" },
        "ruins"  => new[] { "skeleton", "wraith", "troll" },
        "dungeon"=> new[] { "skeleton", "orc", "troll", "wraith" },
        _        => new[] { "rat", "wolf" },
    };

    /// <summary>Пул предметов биома (для генерации и сводки на большой карте).</summary>
    public static string[] ItemsOf(string biome) => biome switch
    {
        "pine"   => new[] { "wood", "mushroom", "berry" },
        "rock"   => new[] { "stone", "ore" },
        "swamp"  => new[] { "mushroom", "berry" },
        "mine"   => new[] { "ore", "stone", "iron_ingot" },
        "dungeon"=> new[] { "ore", "iron_ingot" },
        _        => new[] { "mushroom", "wood", "stone", "berry" },
    };

    /// <summary>
    /// Отдельная локация «Подземелье»: каменные залы, соединённые
    /// коридорами; в центральной комнате лестница вниз (этажи Depth),
    /// у выхода врата назад в деревню.
    /// </summary>
    private static Dungeon GenerateDungeonEntrance(LocDef def, int seed)
    {
        var d = new Dungeon(def.W, def.H);
        d.LocationKey = "dungeon";
        var rng = new Random(seed);

        // Комнаты 5×4 … 10×7, до 8 штук, без пересечений (с запасом 1 клетка)
        var rects = new List<(int X, int Y, int W, int H)>();
        for (int attempt = 0; attempt < 200 && rects.Count < 8; attempt++)
        {
            int w = rng.Next(5, 11), h = rng.Next(4, 8);
            int x = rng.Next(2, def.W - w - 2), y = rng.Next(2, def.H - h - 2);
            bool overlaps = false;
            foreach (var (rx, ry, rw, rh) in rects)
                if (x - 1 < rx + rw + 1 && x + w + 1 > rx - 1
                    && y - 1 < ry + rh + 1 && y + h + 1 > ry - 1)
                    { overlaps = true; break; }
            if (overlaps) continue;

            for (int yy = y; yy < y + h; yy++)
                for (int xx = x; xx < x + w; xx++)
                    d.Tiles[yy][xx] = Tile.Floor;
            rects.Add((x, y, w, h));
        }

        // L-коридоры между центрами комнат по порядку
        int CenterX(int i) => rects[i].X + rects[i].W / 2;
        int CenterY(int i) => rects[i].Y + rects[i].H / 2;
        for (int i = 1; i < rects.Count; i++)
        {
            int x = CenterX(i - 1), y = CenterY(i - 1);
            int tx = CenterX(i), ty = CenterY(i);
            for (; x != tx; x += Math.Sign(tx - x)) d.Tiles[y][x] = Tile.Floor;
            for (; y != ty; y += Math.Sign(ty - y)) d.Tiles[y][x] = Tile.Floor;
        }

        // Лестница вниз — центр первой комнаты
        var (sx, sy) = (CenterX(0), CenterY(0));
        d.Tiles[sy][sx] = Tile.Stairs;
        d.StairsPos = (sx, sy);

        // Врата назад в деревню — слева посередине, с проходом до карты
        int gy = def.H / 2;
        d.Tiles[gy][0] = Tile.Gate;
        d.Transitions[(0, gy)] = ("village", (Overworld.VillageCenter.Item1, Overworld.VillageCenter.Item2));
        for (int x = 1; x < def.W / 2 && d.Tiles[gy][x] == Tile.Wall; x++)
            d.Tiles[gy][x] = Tile.Floor;

        // Монстры из пула подземелья
        var pool = MonstersOf("dungeon");
        int spawned = 0;
        for (int attempt = 0; attempt < 100 && spawned < 8; attempt++)
        {
            var (rx, ry, rw, rh) = rects[rng.Next(rects.Count)];
            int x = rng.Next(rx, rx + rw), y = rng.Next(ry, ry + rh);
            if (d.Tiles[y][x] != Tile.Floor || d.MonsterAt(x, y) != null) continue;
            if ((x, y) == (sx, sy)) continue;
            string key = pool[rng.Next(pool.Length)];
            if (!MonsterData.All.ContainsKey(key)) continue;
            var m = MonsterData.CreateEntity(key, 1);
            m.X = x; m.Y = y;
            d.Monsters.Add(m);
            spawned++;
        }
        Rules.InitActionTimers(d.Monsters);

        // Предметы и пара сундуков в комнатах
        var itemPool = ItemsOf("dungeon");
        for (int i = 0; i < 8; i++)
        {
            var (rx, ry, rw, rh) = rects[rng.Next(rects.Count)];
            int x = rng.Next(rx, rx + rw), y = rng.Next(ry, ry + rh);
            if (d.Tiles[y][x] == Tile.Floor && !d.Items.ContainsKey((x, y)) && (x, y) != (sx, sy))
                d.Items[(x, y)] = itemPool[rng.Next(itemPool.Length)];
        }
        for (int i = 0; i < 2 && i < rects.Count; i++)
        {
            var (rx, ry, rw, rh) = rects[i];
            int x = rx + rw / 2, y = ry + 1;
            if (d.Tiles[y][x] == Tile.Floor && (x, y) != (sx, sy))
            {
                d.Tiles[y][x] = Tile.Chest;
                var resources = new[] { "ore", "stone", "iron_ingot" };
                var loot = new Dictionary<string, int>();
                for (int j = 0; j < 2; j++)
                {
                    string key = resources[rng.Next(resources.Length)];
                    loot[key] = loot.GetValueOrDefault(key) + rng.Next(2, 6);
                }
                d.ChestLoot[(x, y)] = loot;
            }
        }

        d.Rooms = rects.Select(r => new Room(r.X, r.Y, r.W, r.H)).ToList();
        return d;
    }

    /// <summary>
    /// Стабильный (не зависящий от процесса) хеш ключа локации.
    /// string.GetHashCode() рандомизирован между запусками и для
    /// детерминированной генерации не подходит.
    /// </summary>
    private static int StableSeed(string key)
    {
        unchecked
        {
            int hash = 17;
            foreach (char c in key)
                hash = hash * 31 + c;
            return Config.WorldSeed ^ hash;
        }
    }
}
