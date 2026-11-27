using System;
using System.Collections.Generic;
using System.Linq;

namespace Aethelgard.World;

/// <summary>
/// ALL 13 locations — 100% parity with Python core/locations.py.
/// Each location is a separate map with its own generator.
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

        // Generate biome map
        var d = new Dungeon(def.W, def.H);
        d.LocationKey = key;

        // Fill with floor
        for (int y = 0; y < def.H; y++)
            for (int x = 0; x < def.W; x++)
                d.Tiles[y][x] = Tile.Floor;

        // Plant forest based on biome
        var rng = new Random(actualSeed);
        float density = def.Biome switch
        {
            "pine" => 0.55f, "birch" => 0.42f, "rock" => 0.30f,
            "autumn" => 0.46f, "swamp" => 0.36f, "dead" => 0.40f,
            "meadow" => 0.20f, "beach" => 0.08f,
            "goblin" => 0.30f, "mine" => 0.15f, "ruins" => 0.25f,
            _ => 0.35f,
        };

        for (int y = 1; y < def.H - 1; y++)
        {
            for (int x = 1; x < def.W - 1; x++)
            {
                if (rng.NextDouble() < density)
                    d.Tiles[y][x] = Tile.Wall;
            }
        }

        // Mine: add ore veins
        if (def.Biome == "mine")
        {
            for (int i = 0; i < 20; i++)
            {
                int x = rng.Next(2, def.W - 2);
                int y = rng.Next(2, def.H - 2);
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if (rng.NextDouble() < 0.5)
                            d.Tiles[y + dy][x + dx] = Tile.Wall;
            }
        }

        // Ruins: add broken walls
        if (def.Biome == "ruins")
        {
            for (int i = 0; i < 30; i++)
            {
                int x = rng.Next(5, def.W - 5);
                int y = rng.Next(5, def.H - 5);
                int len = rng.Next(3, 8);
                bool horizontal = rng.Next(2) == 0;
                for (int j = 0; j < len; j++)
                {
                    int wx = horizontal ? x + j : x;
                    int wy = horizontal ? y : y + j;
                    if (wx < def.W && wy < def.H)
                        d.Tiles[wy][wx] = Tile.Wall;
                }
            }
        }

        // Place exit gate back to village
        d.Tiles[def.H / 2][0] = Tile.Gate;
        d.Transitions[(0, def.H / 2)] = ("village", (Aethelgard.World.Overworld.VillageCenter.Item1, Aethelgard.World.Overworld.VillageCenter.Item2));

        // Spawn monsters based on biome
        var pool = MonstersOf(def.Biome);
        int monsterCount = def.Biome == "goblin" ? 15 : 10;
        int spawned = 0;
        for (int attempt = 0; attempt < 100 && spawned < monsterCount; attempt++)
        {
            int x = rng.Next(1, def.W - 1);
            int y = rng.Next(1, def.H - 1);
            if (d.Tiles[y][x] != Tile.Floor) continue;
            if (d.MonsterAt(x, y) != null) continue;
            string key2 = pool[rng.Next(pool.Length)];
            if (!MonsterData.All.ContainsKey(key2)) continue;
            var m = MonsterData.CreateEntity(key2, 1);
            m.X = x; m.Y = y;
            d.Monsters.Add(m);
            spawned++;
        }
        Rules.InitActionTimers(d.Monsters);

        // Scatter items
        var itemPool = ItemsOf(def.Biome);
        for (int i = 0; i < 15; i++)
        {
            int x = rng.Next(1, def.W - 1);
            int y = rng.Next(1, def.H - 1);
            if (d.Tiles[y][x] == Tile.Floor && !d.Items.ContainsKey((x, y)))
                d.Items[(x, y)] = itemPool[rng.Next(itemPool.Length)];
        }

        // Place harvestables (trees, bushes, ore)
        for (int i = 0; i < 20; i++)
        {
            int x = rng.Next(1, def.W - 1);
            int y = rng.Next(1, def.H - 1);
            if (d.Tiles[y][x] == Tile.Wall && rng.NextDouble() < 0.3)
            {
                d.Harvestables[(x, y)] = HarvestSystem.MakeHarvestable("tree");
            }
        }
        for (int i = 0; i < 15; i++)
        {
            int x = rng.Next(1, def.W - 1);
            int y = rng.Next(1, def.H - 1);
            if (d.Tiles[y][x] == Tile.Floor && !d.Items.ContainsKey((x, y)) && rng.NextDouble() < 0.3)
            {
                d.Harvestables[(x, y)] = HarvestSystem.MakeHarvestable("bush");
            }
        }

        // Rooms (for dungeon gen)
        d.Rooms = new List<Room>(); // для карт локаций комнаты не отслеживаются

        return d;
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
