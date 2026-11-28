using System;
    using System.Collections.Generic;

    namespace Aethelgard.World
    {

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Aethelgard.BaseClass;
    using Aethelgard.Entities;
    using Aethelgard.Entities.Monsters;
    using Aethelgard.Structs;
    using Aethelgard.World;
    using Aethelgard.Systems;
    using Aethelgard.Data;
    using Aethelgard.Net;
    using Aethelgard.Audio;
    /// <summary>
    /// Система локаций — порт core/locations.py python-final.
    /// Каждая локация — своя карта: деревня 100×70 (исторический размер),
    /// остальные — 120×70. Между ними каменные врата (Tile.Gate) со
    /// сторонами света (SIDE_POS/SIDE_ENTRY). Карты кэшируются в
    /// GameController._locationCache.
    /// </summary>
    public static class Locations
    {
        public class LocDef
        {
            public string Key = "", Name = "";
            public int W, H;
            public string Biome = "";
            public bool IsDungeon;
            public string Kind = "biome"; // city | biome | goblin | mine | ruins
        }

        // ── Размеры (python: VILLAGE_W/H, LOCATION_W/H) ──
        public const int VillageW = 100;
        public const int VillageH = 70;
        public const int LocationW = 120;
        public const int LocationH = 70;

        // ── Сетка позиций врат локаций ──
        public static readonly Dictionary<string, (int X, int Y)> GridPos = new()
        {
            ["pine"] = (0, 0), ["birch"] = (1, 0), ["rock"] = (2, 0),
            ["autumn"] = (0, 1), ["village"] = (1, 1), ["swamp"] = (2, 1),
            ["goblin_village"] = (3, 1),
            ["beach"] = (0, 2), ["meadow"] = (1, 2), ["dead"] = (2, 2),
            ["graveyard"] = (3, 2),
            ["ruins"] = (3, 3),
        };

        // Врата на краях карты локации и точка появления в 2 клетках внутрь
        public static readonly Dictionary<string, (int X, int Y)> SidePos = new()
        {
            ["n"] = (LocationW / 2, 1),
            ["s"] = (LocationW / 2, LocationH - 4),
            ["e"] = (LocationW - 2, LocationH / 2),
            ["w"] = (1, LocationH / 2),
        };
        public static readonly Dictionary<string, (int X, int Y)> SideEntry = new()
        {
            ["n"] = (LocationW / 2, 3),
            ["s"] = (LocationW / 2, LocationH - 6),
            ["e"] = (LocationW - 4, LocationH / 2),
            ["w"] = (3, LocationH / 2),
        };

        // Врата деревни и точки появления при возврате из биома
        // (python VILLAGE_GATES / VILLAGE_GATE_ENTRY)
        public static readonly Dictionary<string, (int X, int Y)> VillageGates = new()
        {
            ["birch"] = (50, 1),     // север
            ["swamp"] = (98, 35),    // восток
            ["meadow"] = (50, 68),   // юг
            ["autumn"] = (1, 35),    // запад
        };
        public static readonly Dictionary<string, (int X, int Y)> VillageGateEntry =
            new()
            {
                ["autumn"] = SideEntry["e"],
                ["swamp"] = SideEntry["w"],
                ["meadow"] = SideEntry["n"],
                ["birch"] = SideEntry["s"],
            };

        // Точка появления в биоме при выходе из деревни (у его врат)
        public static (int, int) VillageGateSide(string key) =>
            key switch
            {
                "birch" => VillageGateEntry["birch"],
                "swamp" => VillageGateEntry["swamp"],
                "meadow" => VillageGateEntry["meadow"],
                "autumn" => VillageGateEntry["autumn"],
                _ => VillageGateEntry["birch"],
            };

        /// <summary>Врата каждой локации: позиция → (куда, точка появления).</summary>
        public static readonly Dictionary<string,
            Dictionary<(int X, int Y), (string To, (int X, int Y) Entry)>> BiomeGates =
            new()
        {
            ["pine"] = new()
            {
                [SidePos["e"]] = ("birch", SideEntry["w"]),
                [(40, LocationH - 4)] = ("autumn", SideEntry["n"]),
            },
            ["birch"] = new()
            {
                [SidePos["w"]] = ("pine", SideEntry["e"]),
                [SidePos["e"]] = ("rock", SideEntry["w"]),
                [SidePos["s"]] = ("village", VillageGateEntry["birch"]),
            },
            ["rock"] = new()
            {
                [SidePos["w"]] = ("birch", SideEntry["e"]),
                [(80, LocationH - 4)] = ("swamp", SideEntry["n"]),
            },
            ["autumn"] = new()
            {
                [SidePos["n"]] = ("pine", (40, LocationH - 6)),
                [SidePos["s"]] = ("beach", SideEntry["n"]),
                [SidePos["e"]] = ("village", VillageGateEntry["autumn"]),
            },
            ["swamp"] = new()
            {
                [SidePos["n"]] = ("rock", (80, LocationH - 6)),
                [SidePos["s"]] = ("dead", SideEntry["n"]),
                [SidePos["w"]] = ("village", VillageGateEntry["swamp"]),
                [SidePos["e"]] = ("goblin_village", (LocationW / 2, 4)),
            },
            ["beach"] = new()
            {
                [SidePos["n"]] = ("autumn", SideEntry["s"]),
                [SidePos["e"]] = ("meadow", SideEntry["w"]),
            },
            ["meadow"] = new()
            {
                [SidePos["n"]] = ("village", VillageGateEntry["meadow"]),
                [SidePos["w"]] = ("beach", SideEntry["e"]),
                [SidePos["e"]] = ("dead", SideEntry["w"]),
            },
            ["dead"] = new()
            {
                [SidePos["n"]] = ("swamp", SideEntry["s"]),
                [SidePos["w"]] = ("meadow", SideEntry["e"]),
                [SidePos["e"]] = ("graveyard", SideEntry["w"]),
            },
            ["graveyard"] = new()
            {
                [SidePos["w"]] = ("dead", SideEntry["e"]),
                [SidePos["s"]] = ("ruins", SideEntry["n"]),
            },
            ["ruins"] = new()
            {
                [SidePos["n"]] = ("graveyard", SideEntry["s"]),
            },
        };

        public static readonly Dictionary<string, string[]> BiomePools = new()
        {
            ["pine"] = new[] { "deer", "wolf", "rat" },
            ["birch"] = new[] { "deer", "slime", "rat" },
            ["rock"] = new[] { "wolf", "bear" },
            ["autumn"] = new[] { "deer", "boar", "wolf" },
            ["swamp"] = new[] { "slime", "rat", "boar" },
            ["beach"] = new[] { "crab", "murloc", "turtle" },
            ["meadow"] = new[] { "deer", "boar", "rat" },
            ["dead"] = new[] { "wolf", "bear", "rat" },
            ["graveyard"] = new[] { "skeleton", "wolf", "rat" },
            ["ruins"] = new[] { "skeleton", "rat", "slime" },
        };

        public static readonly Dictionary<string, float> BiomeDensity = new()
        {
            ["pine"] = 0.46f, ["birch"] = 0.36f, ["rock"] = 0.30f,
            ["autumn"] = 0.40f, ["swamp"] = 0.30f, ["beach"] = 0.35f,
            ["meadow"] = 0.17f, ["dead"] = 0.36f,
            ["graveyard"] = 0.12f, ["ruins"] = 0.28f,
        };

        public static readonly Dictionary<string, int> BiomeBushes = new()
        {
            ["rock"] = 0, ["graveyard"] = 0, ["beach"] = 0, ["ruins"] = 8,
        };

        public static readonly Dictionary<string, LocDef> All = new()
        {
            ["village"]        = new() { Key = "village", Name = "Город Этельгард", W = VillageW, H = VillageH, Biome = "village", Kind = "city" },
            ["goblin_village"] = new() { Key = "goblin_village", Name = "Деревня гоблинов", W = LocationW, H = LocationH, Biome = "goblin", Kind = "goblin" },
            ["mine"]           = new() { Key = "mine", Name = "Заброшенная шахта", W = LocationW, H = LocationH, Biome = "mine", Kind = "mine" },
            ["pine"]           = new() { Key = "pine", Name = "Сосновый бор", W = LocationW, H = LocationH, Biome = "pine" },
            ["birch"]          = new() { Key = "birch", Name = "Берёзовая роща", W = LocationW, H = LocationH, Biome = "birch" },
            ["rock"]           = new() { Key = "rock", Name = "Каменный перевал", W = LocationW, H = LocationH, Biome = "rock" },
            ["autumn"]         = new() { Key = "autumn", Name = "Осенний лес", W = LocationW, H = LocationH, Biome = "autumn" },
            ["swamp"]          = new() { Key = "swamp", Name = "Гиблое болото", W = LocationW, H = LocationH, Biome = "swamp" },
            ["meadow"]         = new() { Key = "meadow", Name = "Цветущая поляна", W = LocationW, H = LocationH, Biome = "meadow" },
            ["dead"]           = new() { Key = "dead", Name = "Мёртвый лес", W = LocationW, H = LocationH, Biome = "dead" },
            ["beach"]          = new() { Key = "beach", Name = "Пляж моря", W = LocationW, H = LocationH, Biome = "beach" },
            ["graveyard"]      = new() { Key = "graveyard", Name = "Старое кладбище", W = LocationW, H = LocationH, Biome = "graveyard" },
            ["ruins"]          = new() { Key = "ruins", Name = "Руины города", W = LocationW, H = LocationH, Biome = "ruins", Kind = "ruins" },
        };

        // ── Дороги: Г-образный путь от центра локации к вратам ──
        // (python _l_path + road_cells)
        private static HashSet<(int X, int Y)> LPath((int X, int Y) a, (int X, int Y) b)
        {
            var cells = new HashSet<(int, int)>();
            for (int x = Math.Min(a.X, b.X); x <= Math.Max(a.X, b.X); x++)
                for (int dy = -1; dy <= 1; dy++)
                    cells.Add((x, a.Y + dy));
            for (int y = Math.Min(a.Y, b.Y); y <= Math.Max(a.Y, b.Y); y++)
                for (int dx = -1; dx <= 1; dx++)
                    cells.Add((b.X + dx, y));
            return cells;
        }

        private static readonly Dictionary<string, HashSet<(int, int)>> _roadCache = new();

        public static HashSet<(int X, int Y)> RoadCells(string key)
        {
            if (_roadCache.TryGetValue(key, out var cached)) return cached;
            HashSet<(int, int)> roads;
            if (key == "village")
            {
                roads = new HashSet<(int, int)>();
                foreach (var gate in VillageGates.Values)
                    roads.UnionWith(LPath(Overworld.VillageCenter, (gate.Item1, gate.Item2)));
            }
            else if (key == "mine")
            {
                roads = LPath((60, 60), (60, LocationH - 4));
            }
            else
            {
                roads = new HashSet<(int, int)>();
                var center = (X: LocationW / 2, Y: LocationH / 2);
                if (BiomeGates.TryGetValue(key, out var gates))
                    foreach (var pos in gates.Keys)
                        roads.UnionWith(LPath(center, pos));
            }
            _roadCache[key] = roads;
            return roads;
        }

        // ── Определение биома деревни для леса по краям (старая сетка) ──
        private static string? LegacyBiomeAt(int x, int y)
        {
            int rx = -1, ry = -1;
            for (int i = 0; i < 3; i++)
            {
                if (x >= Overworld.RegionX[i] && x < Overworld.RegionX[i + 1]) rx = i;
                if (y >= Overworld.RegionY[i] && y < Overworld.RegionY[i + 1]) ry = i;
            }
            if (rx < 0 || ry < 0) return null;
            if (rx == 1 && ry == 1) return null;
            return Overworld.BiomeNames.GetValueOrDefault((rx, ry));
        }

        /// <summary>
        /// Generate a location map by key (лениво, кэшируется GameController).
        /// </summary>
        public static Dungeon GenerateLocation(string key, int? seed = null)
        {
            int actualSeed = seed ?? StableSeed(key);
            var rng = new Random(actualSeed);

            return key switch
            {
                "village" => GenerateVillage(rng),
                "goblin_village" => GenerateGoblinVillage(rng),
                "mine" => GenerateMine(rng),
                "ruins" => GenerateRuins(rng),
                _ when All.ContainsKey(key) && All[key].Kind == "biome"
                    => GenerateBiome(key, rng),
                _ => GenerateVillage(rng),
            };
        }

        // ── Общие помощники ──

        private static void FillFloor(Dungeon d)
        {
            for (int y = 0; y < d.H; y++)
                for (int x = 0; x < d.W; x++)
                    d.Tiles[y][x] = Tile.Floor;
        }

        private static void Clear(Dungeon d, (int X, int Y) center, int radius)
        {
            for (int y = Math.Max(0, center.Y - radius);
                 y <= Math.Min(d.H - 1, center.Y + radius); y++)
                for (int x = Math.Max(0, center.X - radius);
                     x <= Math.Min(d.W - 1, center.X + radius); x++)
                    if ((x - center.X) * (x - center.X) +
                        (y - center.Y) * (y - center.Y) <= radius * radius)
                        d.Tiles[y][x] = Tile.Floor;
        }

        /// <summary>Прорубить дорогу (полутолщина 1) между двумя точками.</summary>
        private static void Carve(Dungeon d, (int X, int Y) a, (int X, int Y) b)
        {
            foreach (var (x, y) in LPath(a, b))
                if (x >= 0 && x < d.W && y >= 0 && y < d.H)
                    d.Tiles[y][x] = Tile.Floor;
        }

        /// <summary>Финальная очистка дорог от стен, кустов, предметов и мобов.</summary>
        private static void FinalizeRoads(Dungeon d, string key)
        {
            var roads = RoadCells(key);
            foreach (var (x, y) in roads)
            {
                if (x < 0 || x >= d.W || y < 0 || y >= d.H) continue;
                if (d.Tiles[y][x] == Tile.Wall) d.Tiles[y][x] = Tile.Floor;
                d.Harvestables.Remove((x, y));
                d.Items.Remove((x, y));
            }
            d.Monsters.RemoveAll(m => roads.Contains((m.X, m.Y)));
        }

        private static void ScatterItems(Dungeon d, Random rng, int count,
            (int X, int Y) center, HashSet<(int, int)> avoid, string[] pool)
        {
            for (int i = 0; i < count * 4 && i < count * 40; i++)
            {
                if (d.Items.Count >= count + d.Items.Count - count && i >= count * 4) break;
            }
            int placed = 0;
            for (int attempt = 0; attempt < count * 20 && placed < count; attempt++)
            {
                int x = rng.Next(2, d.W - 2);
                int y = rng.Next(2, d.H - 2);
                if (d.Tiles[y][x] != Tile.Floor) continue;
                if ((x, y) == center || avoid.Contains((x, y))) continue;
                if ((x - center.X) * (x - center.X) + (y - center.Y) * (y - center.Y) < 25)
                    continue;
                if (d.Items.ContainsKey((x, y))) continue;
                d.Items[(x, y)] = pool[rng.Next(pool.Length)];
                placed++;
            }
        }

        private static void ScatterBushes(Dungeon d, Random rng, int count,
            HashSet<(int, int)> avoid)
        {
            int placed = 0;
            for (int attempt = 0; attempt < count * 20 && placed < count; attempt++)
            {
                int x = rng.Next(2, d.W - 2);
                int y = rng.Next(2, d.H - 2);
                if (d.Tiles[y][x] != Tile.Floor) continue;
                if (avoid.Contains((x, y))) continue;
                if (d.Items.ContainsKey((x, y))) continue;
                d.Harvestables[(x, y)] = HarvestSystem.MakeHarvestable("bush");
                placed++;
            }
        }

        private static void SpawnMonsters(Dungeon d, Random rng, string[] pool,
            int count, HashSet<(int, int)> avoid, (int X, int Y)? minDist = null,
            Func<int, int, bool>? safeZone = null)
        {
            int spawned = 0;
            for (int attempt = 0; attempt < count * 30 && spawned < count; attempt++)
            {
                int x = rng.Next(1, d.W - 1);
                int y = rng.Next(1, d.H - 1);
                if (d.Tiles[y][x] != Tile.Floor) continue;
                if (d.MonsterAt(x, y) != null) continue;
                if (avoid.Contains((x, y))) continue;
                if (minDist != null)
                {
                    var (mx, my) = minDist.Value;
                    if ((x - mx) * (x - mx) + (y - my) * (y - my) < 144) continue;
                }
                if (safeZone != null && safeZone(x, y)) continue;
                string key = pool[rng.Next(pool.Length)];
                if (!MonsterData.All.ContainsKey(key)) continue;
                var m = MonsterData.CreateEntity(key, 1);
                m.X = x; m.Y = y;
                d.Monsters.Add(m);
                spawned++;
            }
            Rules.InitActionTimers(d.Monsters);
        }

        // ── ДЕРЕВНЯ (100×70 — исторический размер, python _generate_village) ──

        private static Dungeon GenerateVillage(Random rng)
        {
            var d = new Dungeon(VillageW, VillageH)
            {
                LocationKey = "village",
            };
            FillFloor(d);
            var center = Overworld.VillageCenter;

            // Лес по краям: биом по старой сетке, чтобы переходы вели в
            // визуально знакомые края
            for (int y = 1; y < d.H - 1; y++)
            {
                for (int x = 1; x < d.W - 1; x++)
                {
                    if (Overworld.InVillage(x, y)) continue;
                    if (Overworld.IsRoad(x, y)) continue;
                    string? biome = LegacyBiomeAt(x, y);
                    if (biome == null) continue;
                    if (rng.NextDouble() < BiomeDensity.GetValueOrDefault(biome, 0.4f))
                        d.Tiles[y][x] = Tile.Wall;
                }
            }

            Clear(d, center, 7);

            // Дома и площадь — координаты исторического города (VILLAGE_HOUSES)
            foreach (var (hx, hy, hw, hh) in Overworld.VillageHouses)
                for (int yy = hy; yy < hy + hh; yy++)
                    for (int xx = hx; xx < hx + hw; xx++)
                        d.Tiles[yy][xx] = Tile.Wall;
            var (wellX, wellY) = Overworld.WellPos;
            d.Tiles[wellY][wellX] = Tile.Wall;
            foreach (var (sx, sy) in Overworld.VillageStalls) d.Tiles[sy][sx] = Tile.Wall;
            foreach (var (sx, sy) in Overworld.VillageStatues) d.Tiles[sy][sx] = Tile.Wall;
            foreach (var (tx, ty) in Overworld.VillageTorches) d.Tiles[ty][tx] = Tile.Wall;

            // Городская стена — сплошное кольцо, дороги пробивают ворота
            foreach (var (wx, wy) in Overworld.CityWallCells())
                d.Tiles[wy][wx] = Tile.Wall;

            // Дороги к вратам и сами врата (переходы в биомы)
            var avoid = new HashSet<(int, int)> { center, Overworld.ElderPos,
                Overworld.QuestBoardVillagePos };
            foreach (var (_, gate) in VillageGates)
            {
                Carve(d, center, gate);
                Clear(d, gate, 2);
            }
            foreach (var (key, gate) in VillageGates)
            {
                d.Tiles[gate.Item2][gate.Item1] = Tile.Gate;
                d.Transitions[(gate.Item1, gate.Item2)] = (key, VillageGateEntry[key]);
                avoid.Add((gate.Item1, gate.Item2));
            }

            // Доска поручений, старейшина и горожане должны стоять на полу
            d.Tiles[Overworld.QuestBoardVillagePos.Y][Overworld.QuestBoardVillagePos.X] = Tile.Floor;
            d.Tiles[Overworld.ElderPos.Y][Overworld.ElderPos.X] = Tile.Floor;
            foreach (var (_, pos) in Overworld.CityNpcPos)
                d.Tiles[pos.Y][pos.X] = Tile.Floor;

            foreach (var (_, pos) in Overworld.CityNpcPos) avoid.Add(pos);

            ScatterItems(d, rng, 26, center, avoid, new[] { "mushroom", "wood", "stone" });
            ScatterBushes(d, rng, 24, avoid);
            SpawnMonsters(d, rng, new[] { "deer", "wolf", "boar", "rat", "slime" }, 16,
                avoid, center,
                (x, y) => Overworld.InCitySafe(x, y, 6));
            d.Rooms = new List<Room> { new(center.Item1 - 1, center.Item2 - 1, 3, 3) };

            // Дороги внутри деревни + старые отрезки — очистить
            foreach (var (x, y) in RoadCells("village"))
                if (x >= 0 && x < d.W && y >= 0 && y < d.H)
                {
                    if (d.Tiles[y][x] == Tile.Wall) d.Tiles[y][x] = Tile.Floor;
                    d.Harvestables.Remove((x, y));
                    d.Items.Remove((x, y));
                }
            foreach (var (x0, y0, x1, y1) in Overworld.RoadSegments)
            {
                if (y0 == y1)
                    for (int x = Math.Min(x0, x1); x <= Math.Max(x0, x1); x++)
                        for (int yy = y0 - 1; yy <= y0 + 1; yy++)
                            CleanCell(d, x, yy);
                else if (x0 == x1)
                    for (int yy = Math.Min(y0, y1); yy <= Math.Max(y0, y1); yy++)
                        for (int x = x0 - 1; x <= x0 + 1; x++)
                            CleanCell(d, x, yy);
            }

            return d;
        }

        private static void CleanCell(Dungeon d, int x, int y)
        {
            if (x < 0 || x >= d.W || y < 0 || y >= d.H) return;
            if (d.Tiles[y][x] == Tile.Wall) d.Tiles[y][x] = Tile.Floor;
            d.Harvestables.Remove((x, y));
            d.Items.Remove((x, y));
        }

        // ── БИОМЫ (120×70, python _generate_biome) ──

        private static Dungeon GenerateBiome(string key, Random rng)
        {
            var def = All[key];
            string biome = def.Biome;
            var d = new Dungeon(LocationW, LocationH) { LocationKey = key };
            FillFloor(d);
            var center = (X: LocationW / 2, Y: LocationH / 2);
            Clear(d, center, 10);

            if (biome == "beach")
            {
                GenerateBeach(d, rng, center);
            }
            else if (biome == "ruins")
            {
                ScatterRuins(d, rng, center);
            }
            else
            {
                // 120 лесных кластеров-кружков (как в python: range(120))
                float density = BiomeDensity.GetValueOrDefault(biome, 0.4f);
                for (int i = 0; i < 120; i++)
                {
                    int cx = rng.Next(2, d.W - 2);
                    int cy = rng.Next(2, d.H - 2);
                    int radius = rng.Next(1, 4);
                    for (int yy = cy - radius; yy <= cy + radius; yy++)
                        for (int xx = cx - radius; xx <= cx + radius; xx++)
                        {
                            if (xx < 0 || xx >= d.W || yy < 0 || yy >= d.H) continue;
                            if ((xx - cx) * (xx - cx) + (yy - cy) * (yy - cy) > radius * radius)
                                continue;
                            if ((xx - center.X) * (xx - center.X) +
                                (yy - center.Y) * (yy - center.Y) < 110)
                                continue;
                            if (rng.NextDouble() < density)
                                d.Tiles[yy][xx] = Tile.Wall;
                        }
                }
            }

            // Врата по таблице BIOME_GATES + дороги к ним
            var avoid = new HashSet<(int, int)> { center };
            foreach (var (pos, (to, entry)) in BiomeGates[key])
            {
                Carve(d, center, pos);
                Clear(d, pos, 2);
                d.Tiles[pos.Y][pos.X] = Tile.Gate;
                d.Transitions[pos] = (to, entry);
                avoid.Add(pos);
            }

            if (biome == "rock")
            {
                // Вход в шахту — на склоне горы (север карты)
                var minePos = (X: 86, Y: 12);
                Clear(d, minePos, 5);
                Carve(d, center, minePos);
                d.Tiles[minePos.Y][minePos.X] = Tile.Gate;
                d.Transitions[minePos] = ("mine", (60, LocationH - 6));
                avoid.Add(minePos);
                AddOre(d, rng, 40);
            }

            if (biome == "graveyard")
            {
                // Вход в главное подземелье — в центре кладбища
                var dungeonPos = (X: 60, Y: 33);
                Clear(d, dungeonPos, 4);
                d.Tiles[dungeonPos.Y][dungeonPos.X] = Tile.Stairs;
                d.StairsPos = dungeonPos;
                avoid.Add(dungeonPos);
            }

            string[] itemPool = biome == "beach" ? new[] { "stone", "bone", "hide" }
                : biome == "rock" ? new[] { "stone", "ore", "mushroom" }
                : new[] { "mushroom", "wood", "stone" };
            var roads = RoadCells(key);
            ScatterItems(d, rng, 22, center,
                new HashSet<(int, int)>(avoid.Union(roads)), itemPool);
            int bushes = BiomeBushes.GetValueOrDefault(biome, 26);
            if (bushes > 0)
                ScatterBushes(d, rng, bushes,
                    new HashSet<(int, int)>(avoid.Union(roads)));
            SpawnMonsters(d, rng, BiomePools.GetValueOrDefault(biome, new[] { "deer", "wolf" }),
                14, new HashSet<(int, int)>(avoid.Union(roads)), center);
            FinalizeRoads(d, key);
            d.Rooms = new List<Room> { new(center.X - 2, center.Y - 2, 5, 5) };
            return d;
        }

        // ── ПЛЯЖ: волнистая береговая линия + пальмы (python _generate_beach) ──

        private static bool BeachSea(int x, int y)
        {
            double coastY = LocationH * 0.66 + 5.0 * Math.Sin(x / 11.0)
                + 2.0 * Math.Sin(x / 4.3);
            double coastX = LocationW * 0.14 + 4.0 * Math.Sin(y / 9.0)
                + 2.0 * Math.Sin(y / 3.3);
            return y > coastY || x < coastX;
        }

        private static void GenerateBeach(Dungeon d, Random rng, (int X, int Y) center)
        {
            // Море — WALL, суша — песчаный пол (рендер по LocationKey=beach)
            for (int y = 0; y < d.H; y++)
                for (int x = 0; x < d.W; x++)
                    d.Tiles[y][x] = BeachSea(x, y) ? Tile.Wall : Tile.Floor;
            Clear(d, center, 9);

            var roads = RoadCells("beach");
            var blocked = new HashSet<(int, int)>(roads) { center };
            // Пальмы: одиночные WALL на песке, ближе к линии прибоя
            var spots = new List<(int X, int Y)>();
            for (int y = 2; y < d.H - 2; y++)
                for (int x = 2; x < d.W - 2; x++)
                {
                    if (d.Tiles[y][x] != Tile.Floor || blocked.Contains((x, y))) continue;
                    if ((x - center.X) * (x - center.X) +
                        (y - center.Y) * (y - center.Y) < 64) continue;
                    if (BeachSea(x, y + 1) || BeachSea(x - 1, y))
                        spots.Add((x, y));
                }
            // Fisher-Yates перемешивание (детерминированное по rng)
            for (int i = spots.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (spots[i], spots[j]) = (spots[j], spots[i]);
            }
            for (int i = 0; i < spots.Count && i < 34; i++)
                d.Tiles[spots[i].Y][spots[i].X] = Tile.Wall;
        }

        private static void ScatterRuins(Dungeon d, Random rng, (int X, int Y) center)
        {
            float density = BiomeDensity.GetValueOrDefault("ruins", 0.28f);
            for (int i = 0; i < 30; i++)
            {
                int x = rng.Next(5, d.W - 5);
                int y = rng.Next(5, d.H - 5);
                int len = rng.Next(3, 8);
                bool horizontal = rng.Next(2) == 0;
                for (int j = 0; j < len; j++)
                {
                    int wx = horizontal ? x + j : x;
                    int wy = horizontal ? y : y + j;
                    if (wx < d.W && wy < d.H &&
                        (wx - center.X) * (wx - center.X) +
                        (wy - center.Y) * (wy - center.Y) >= 60)
                        d.Tiles[wy][wx] = Tile.Wall;
                }
            }
        }

        // ── ДЕРЕВНЯ ГОБЛИНОВ (python _generate_goblin_village) ──

        private static readonly (int X, int Y) GoblinCenter = (LocationW / 2, LocationH / 2);
        private static readonly (int X, int Y, int W, int H) GoblinRect =
            (GoblinCenter.X - 16, GoblinCenter.Y - 13, 33, 27);

        private static bool InLocalGoblinVillage(int x, int y) =>
            x >= GoblinRect.X && x < GoblinRect.X + GoblinRect.W &&
            y >= GoblinRect.Y && y < GoblinRect.Y + GoblinRect.H;

        private static Dungeon GenerateGoblinVillage(Random rng)
        {
            var d = new Dungeon(LocationW, LocationH) { LocationKey = "goblin_village" };
            FillFloor(d);
            var (cx, cy) = GoblinCenter;
            Clear(d, GoblinCenter, 15);

            // Лес вокруг деревни (кластеры), кроме самой деревни и центральной
            // дороги по x = cx
            for (int i = 0; i < 100; i++)
            {
                int x = rng.Next(2, d.W - 2);
                int y = rng.Next(2, d.H - 2);
                int radius = rng.Next(1, 4);
                for (int yy = y - radius; yy <= y + radius; yy++)
                    for (int xx = x - radius; xx <= x + radius; xx++)
                    {
                        if (xx < 0 || xx >= d.W || yy < 0 || yy >= d.H) continue;
                        if ((xx - x) * (xx - x) + (yy - y) * (yy - y) > radius * radius)
                            continue;
                        if (InLocalGoblinVillage(xx, yy)) continue;
                        if (xx == cx) continue;
                        if (rng.NextDouble() < 0.5)
                            d.Tiles[yy][xx] = Tile.Wall;
                    }
            }

            // Хижины, тотемы, костёр (локальные координаты относительно центра)
            var houses = new (int, int, int, int)[]
            {
                (GoblinRect.X + 2, GoblinRect.Y + 2, 5, 4),
                (GoblinRect.X + 13, GoblinRect.Y + 2, 6, 4),
                (GoblinRect.X + 25, GoblinRect.Y + 2, 5, 4),
                (GoblinRect.X + 2, GoblinRect.Y + 21, 5, 4),
                (GoblinRect.X + 13, GoblinRect.Y + 21, 6, 4),
                (GoblinRect.X + 25, GoblinRect.Y + 21, 5, 4),
            };
            foreach (var (hx, hy, hw, hh) in houses)
                for (int yy = hy; yy < hy + hh; yy++)
                    for (int xx = hx; xx < hx + hw; xx++)
                        d.Tiles[yy][xx] = Tile.Wall;
            var campfire = (X: cx, Y: cy - 4);
            d.Tiles[campfire.Y][campfire.X] = Tile.Wall;
            foreach (var (tx, ty) in new[] { (cx - 9, cy - 3), (cx + 9, cy - 3) })
                d.Tiles[ty][tx] = Tile.Wall;

            // Северные врата ведут к болоту
            var gatePos = SidePos["n"];
            Carve(d, gatePos, GoblinCenter);
            Clear(d, (cx, 5), 2);
            d.Tiles[gatePos.Y][gatePos.X] = Tile.Gate;
            d.Transitions[gatePos] = ("swamp", SideEntry["e"]);

            var avoid = new HashSet<(int, int)> { gatePos, campfire };
            ScatterItems(d, rng, 18, GoblinCenter, avoid,
                new[] { "mushroom", "wood", "stone" });
            ScatterBushes(d, rng, 18, avoid);
            SpawnMonsters(d, rng, new[] { "goblin", "archer", "shaman", "boar", "wolf" },
                20, avoid, GoblinCenter);
            // Гарантия всех трёх видов гоблинов на площади
            for (int i = 0; i < 3; i++)
            {
                string key = new[] { "goblin", "archer", "shaman" }[i];
                int mx = cx - 6 + i * 6;
                int my = cy - 8;
                if (d.Tiles[my][mx] == Tile.Floor && d.MonsterAt(mx, my) == null)
                {
                    var m = MonsterData.CreateEntity(key, 1);
                    m.X = mx; m.Y = my;
                    d.Monsters.Add(m);
                }
            }
            Rules.InitActionTimers(d.Monsters);
            FinalizeRoads(d, "goblin_village");
            d.Rooms = new List<Room> { new(cx - 2, cy - 2, 5, 5) };
            return d;
        }

        // ── ШАХТА (python _generate_mine): комнаты + коридоры + руда ──

        private static Dungeon GenerateMine(Random rng)
        {
            var d = new Dungeon(LocationW, LocationH) { LocationKey = "mine" };
            for (int y = 0; y < d.H; y++)
                for (int x = 0; x < d.W; x++)
                    d.Tiles[y][x] = Tile.Wall;

            var rooms = new List<Room>();
            for (int i = 0; i < 120 && rooms.Count < 10; i++)
            {
                int w = rng.Next(6, 13);
                int h = rng.Next(4, 9);
                int x = rng.Next(4, d.W - w - 4);
                int y = rng.Next(4, 59 - h);
                var room = new Room(x, y, w, h);
                if (rooms.Any(r => room.Intersects(r))) continue;
                if (room.X < 64 && room.X + room.W > 56 && room.Y + room.H > 54) continue;
                rooms.Add(room);
                for (int yy = y; yy < y + h; yy++)
                    for (int xx = x; xx < x + w; xx++)
                        d.Tiles[yy][xx] = Tile.Floor;
            }
            if (rooms.Count == 0)
            {
                rooms.Add(new Room(54, 54, 9, 6));
                rooms.Add(new Room(20, 20, 9, 6));
                foreach (var room in rooms)
                    for (int yy = room.Y; yy < room.Y + room.H; yy++)
                        for (int xx = room.X; xx < room.X + room.W; xx++)
                            d.Tiles[yy][xx] = Tile.Floor;
            }

            // Коридоры: комнаты от нижних к верхним + путь к выходу (60,60)
            rooms.Sort((a, b) => b.Center.Y.CompareTo(a.Center.Y));
            var prev = rooms[0].Center;
            Carve(d, prev, (60, 60));
            foreach (var room in rooms.Skip(1))
            {
                var rc = room.Center;
                Carve(d, prev, rc);
                prev = rc;
            }

            var gatePos = (X: 60, Y: LocationH - 4);
            Clear(d, (60, 60), 3);
            Carve(d, (60, 60), gatePos);
            d.Tiles[gatePos.Y][gatePos.X] = Tile.Gate;
            d.Transitions[gatePos] = ("rock", (86, 14));

            var roads = RoadCells("mine");
            AddOre(d, rng, 70);
            var avoid = new HashSet<(int, int)> { gatePos };
            avoid.UnionWith(roads);
            ScatterItems(d, rng, 14, (60, 60), avoid,
                new[] { "stone", "ore", "mushroom" });
            SpawnMonsters(d, rng, new[] { "rat", "goblin" }, 12, avoid);
            Rules.InitActionTimers(d.Monsters);
            d.Rooms = rooms;
            return d;
        }

        private static void AddOre(Dungeon d, Random rng, int count)
        {
            int placed = 0;
            for (int attempt = 0; attempt < count * 20 && placed < count; attempt++)
            {
                int x = rng.Next(2, d.W - 2);
                int y = rng.Next(2, d.H - 2);
                if (d.Tiles[y][x] != Tile.Wall) continue;
                if (d.Harvestables.ContainsKey((x, y))) continue;
                d.Harvestables[(x, y)] = HarvestSystem.MakeHarvestable("ore");
                placed++;
            }
        }

        // ── РУИНЫ ГОРОДА (python _generate_ruins) ──

        private static Dungeon GenerateRuins(Random rng)
        {
            var d = new Dungeon(LocationW, LocationH) { LocationKey = "ruins" };
            FillFloor(d);
            var center = (X: LocationW / 2, Y: LocationH / 2);
            Clear(d, center, 12);

            var roads = RoadCells("ruins");
            var avoid = new HashSet<(int, int)>(roads) { center };
            foreach (var (pos, (to, entry)) in BiomeGates["ruins"])
            {
                Carve(d, center, pos);
                Clear(d, pos, 2);
                d.Tiles[pos.Y][pos.X] = Tile.Gate;
                d.Transitions[pos] = (to, entry);
                avoid.Add(pos);
            }

            ScatterRuins(d, rng, center);

            // 25% шанс: сундук с базовыми ресурсами
            if (rng.NextDouble() < 0.25)
            {
                var pool = new[] { "wood", "stone", "ore", "hide", "bone", "berry",
                    "mushroom", "magic_powder" };
                for (int attempt = 0; attempt < 200; attempt++)
                {
                    int cx = rng.Next(6, d.W - 6);
                    int cy = rng.Next(6, d.H - 6);
                    if (avoid.Contains((cx, cy)) || d.Transitions.ContainsKey((cx, cy)))
                        continue;
                    if ((cx - center.X) * (cx - center.X) +
                        (cy - center.Y) * (cy - center.Y) < 60) continue;
                    if (d.Tiles[cy][cx] != Tile.Floor) continue;
                    d.Tiles[cy][cx] = Tile.Chest;
                    var loot = new Dictionary<string, int>();
                    int kinds = rng.Next(2, 5);
                    for (int i = 0; i < kinds; i++)
                    {
                        string k = pool[rng.Next(pool.Length)];
                        loot[k] = loot.GetValueOrDefault(k) + rng.Next(5, 21);
                    }
                    d.ChestLoot[(cx, cy)] = loot;
                    avoid.Add((cx, cy));
                    break;
                }
            }

            ScatterItems(d, rng, 14, center, avoid, new[] { "stone", "ore", "bone" });
            SpawnMonsters(d, rng, BiomePools["ruins"], 12, avoid, center);
            ScatterBushes(d, rng, 8, avoid);
            FinalizeRoads(d, "ruins");
            d.Rooms = new List<Room> { new(center.X - 2, center.Y - 2, 5, 5) };
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
}
