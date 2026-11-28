using System;
    using System.Collections.Generic;
    using System.Linq;
    using Aethelgard.Data;
    using Aethelgard.Entities;
    using Aethelgard.Net;
    using Aethelgard.Systems;
    using Aethelgard.World;
    using Microsoft.Xna.Framework;
    using NUnit.Framework;

    namespace Aethelgard.Tests
    {

    public class SaveSystemTests
    {
        [Test]
        public void SaveRoundTrip_PreservesPropertiesAndNestedPoints()
        {
            var data = new SaveSystem.SaveData
            {
                Timestamp = new DateTime(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc),
                World = new SaveSystem.WorldData
                {
                    Mode = "dungeon",
                    Depth = 7,
                    Location = "mine",
                },
                Player = new SaveSystem.PlayerData
                {
                    X = 12,
                    Y = 34,
                    Hp = 55,
                    MaxHp = 80,
                    Mana = 9,
                    MaxMana = 20,
                    Level = 6,
                    Gold = 321,
                    ClassKey = "mage",
                    Inventory = new Dictionary<string, int> { ["wood"] = 3, ["potion_s"] = 2 },
                    LearnedNodes = new List<string> { "m_0", "m_1" },
                },
            };
            data.Floors.Add(new SaveSystem.FloorData
            {
                Depth = 7,
                W = 140,
                H = 70,
                StairsPos = new SaveSystem.PointSave { X = 10, Y = 11 },
                TreasuryPos = new SaveSystem.PointSave { X = 20, Y = 21 },
                BossDoorCells = { new SaveSystem.PointSave { X = 30, Y = 31 } },
            });

            string json = SaveSystem.Serialize(data);

            // Свойства должны реально попадать в JSON (раньше public-поля
            // игнорировались System.Text.Json и сохранялся пустой объект).
            StringAssert.Contains("\"Inventory\"", json);
            StringAssert.Contains("\"StairsPos\"", json);

            var back = SaveSystem.Deserialize(json);
            Assert.IsNotNull(back);
            Assert.AreEqual(7, back!.World.Depth);
            Assert.AreEqual("mine", back.World.Location);
            Assert.AreEqual(3, back.Player.Inventory["wood"]);
            Assert.AreEqual(2, back.Player.Inventory["potion_s"]);
            Assert.AreEqual(new[] { "m_0", "m_1" }, back.Player.LearnedNodes);
            Assert.AreEqual(10, back.Floors[0].StairsPos!.X);
            Assert.AreEqual(31, back.Floors[0].BossDoorCells[0].Y);
        }

        [Test]
        public void Deserialize_RejectsFutureSaveVersion()
        {
            var data = SaveSystem.Deserialize("{\"Version\":999}");
            Assert.IsNull(data);
        }

        [Test]
        public void PlayerData_CompatibilityAliasesMapToNewSlots()
        {
            var player = new SaveSystem.PlayerData
            {
                Helmet = "iron_helm",
                Leggings = "chain_legs",
            };

            Assert.AreEqual("iron_helm", player.Head);
            Assert.AreEqual("chain_legs", player.Legs);
        }
    }

    public class ProtocolTests
    {
        [Test]
        public void Snapshot_RoundTrip_KeepsColorsAndNestedEntries()
        {
            var snap = new Snapshot
            {
                Depth = 4,
                MapW = 140,
                MapH = 70,
                WorldMode = "dungeon",
                MyPid = 2,
                StairsPos = new PointState { X = 5, Y = 6 },
            };
            snap.Players.Add(new Snapshot.PlayerState
            {
                Pid = 2,
                X = 7,
                Y = 8,
                Hp = 10,
                MaxHp = 12,
                ClassKey = "rogue",
                Color = new Color(10, 20, 30, 255),
                Inventory = new Dictionary<string, int> { ["dagger"] = 1 },
            });
            snap.Items.Add(new Snapshot.ItemState { X = 1, Y = 2, Key = "wood" });
            snap.Chat.Add(new Snapshot.ChatEntry { Pid = 2, Text = "привет" });

            string json = Protocol.Serialize(snap);
            var back = Protocol.Deserialize<Snapshot>(json);

            Assert.IsNotNull(back);
            Assert.AreEqual("dungeon", back!.WorldMode);
            Assert.AreEqual(5, back.StairsPos!.X);
            Assert.AreEqual(new Color(10, 20, 30, 255), back.Players[0].Color);
            Assert.AreEqual("wood", back.Items[0].Key);
            Assert.AreEqual("привет", back.Chat[0].Text);
        }

        [Test]
        public void HmacSignature_VerifiesAndDetectsTampering()
        {
            byte[] key = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
            var msg = new ClientMsg
            {
                Type = "input",
                Dx = 1,
                Dy = -1,
                ChatText = "тест",
            };

            string payload = Protocol.SignedPayload(msg);
            string signature = Protocol.Sign(payload, key);

            Assert.IsTrue(Protocol.Verify(payload, signature, key));

            // Другая полезная нагрузка → подпись недействительна.
            msg.Dx = 0;
            string tampered = Protocol.SignedPayload(msg);
            Assert.IsFalse(Protocol.Verify(tampered, signature, key));
        }

        [Test]
        public void ClientMessage_CarriesProtocolVersion()
        {
            var msg = new ClientMsg { Type = "hello" };
            Assert.AreEqual(Protocol.Version, msg.ProtocolVersion);
            Assert.AreEqual(Protocol.Version,
                Protocol.Deserialize<ClientMsg>(Protocol.Serialize(msg))!.ProtocolVersion);
        }
    }

    public class EffectTests
    {
        [Test]
        public void ClassEffects_AreIncludedInAggregate()
        {
            var player = new Player("artisan");
            var effects = player.Effects();

            Assert.IsTrue(effects.GetValueOrDefault("craft_xp") > 0);
        }

        [Test]
        public void LearningNode_DoesNotPermanentlyMutateBaseStats()
        {
            var player = new Player("artisan");
            int baseStr = player.Str;
            int baseVit = player.Vit;

            player.LearnedNodes.Add("a_0");
            player.Recalc();

            // База не меняется, итоговые значения — вычисляемые.
            Assert.AreEqual(baseStr, player.Str);
            Assert.AreEqual(baseVit, player.Vit);
            Assert.AreEqual(baseStr + 2, player.TotalStr);
            Assert.AreEqual(baseVit + 2, player.TotalVit);
        }

        [Test]
        public void ComputeStats_AppliesFlatAndFractionalEffects()
        {
            var effects = new Dictionary<string, double>
            {
                ["hp"] = 20,
                ["atk"] = 3,
                ["df"] = 2,
                ["mana"] = 10,
                ["dodge"] = 0.10,
                ["crit"] = 0.05,
            };

            var stats = Player.ComputeStats(
                5, 5, 5, 5, 5, 5, 1,
                null, null, null, null, null, effects);

            Assert.AreEqual(15 + 5 * 2 + 20, stats.MaxHp);
            Assert.AreEqual(2 + 5 / 2 + 3, stats.Atk);
            Assert.AreEqual(5 / 2 + 2, stats.Df);
            Assert.AreEqual(10 + 1 * 2 + 5 * 2 + 10, stats.MaxMana);
            Assert.IsTrue(stats.Dodge >= 0.10f);
            Assert.IsTrue(stats.Crit >= 0.05f);
        }
    }

    public class FloorPersistenceTests
    {
        [Test]
        public void FloorData_RoundTrip_PreservesTransitionsBarriersAndExplored()
        {
            var d = DungeonGen.Generate(3, 4242);
            d.LocationKey = "pine";
            d.Transitions[(5, 6)] = ("pine", (7, 8));
            d.BarrierTiles.Add((9, 10));
            d.Explored.Add((11, 12));
            d.ChestLoot[(1, 2)] = new Dictionary<string, int> { ["wood"] = 3 };

            var fd = SaveSystem.FloorToData(3, d);
            var back = SaveSystem.FloorFromData(fd);

            Assert.IsNotNull(back);
            Assert.AreEqual(d.W, back!.W);
            Assert.AreEqual("pine", back.LocationKey);
            Assert.AreEqual(("pine", (7, 8)), back.Transitions[(5, 6)]);
            Assert.IsTrue(back.BarrierTiles.Contains((9, 10)));
            Assert.IsTrue(back.Explored.Contains((11, 12)));
            Assert.AreEqual(3, back.ChestLoot[(1, 2)]["wood"]);
            Assert.AreEqual(d.Tiles[0][0], back.Tiles[0][0]);
        }
    }

    public class PasswordHasherTests
    {
        [Test]
        public void Hash_ThenVerify_AcceptsCorrectPassword()
        {
            string encoded = PasswordHasher.Hash("s3cret-пароль");

            StringAssert.StartsWith("pbkdf2_sha256$", encoded);
            Assert.IsTrue(PasswordHasher.Verify("s3cret-пароль", encoded));
            Assert.IsFalse(PasswordHasher.Verify("s3cret-пароль ", encoded));
            Assert.IsFalse(PasswordHasher.Verify("S3cret-пароль", encoded));
        }

        [Test]
        public void Hash_UsesUniqueSaltForEveryCall()
        {
            string a = PasswordHasher.Hash("same");
            string b = PasswordHasher.Hash("same");

            Assert.AreNotEqual(a, b);
            Assert.IsTrue(PasswordHasher.Verify("same", a));
            Assert.IsTrue(PasswordHasher.Verify("same", b));
        }

        [Test]
        public void Verify_RejectsMalformedOrLegacyValues()
        {
            Assert.IsFalse(PasswordHasher.Verify("secret", null));
            Assert.IsFalse(PasswordHasher.Verify("secret", ""));
            Assert.IsFalse(PasswordHasher.Verify("secret", "secret"));  // plaintext
            Assert.IsFalse(PasswordHasher.Verify("secret", "pbkdf2_sha256$bad"));
        }

        [Test]
        public void ServerPassword_StoresOnlyHash()
        {
            var server = new GameServer { ServerPassword = "host-pass" };

            Assert.IsNotNull(server.ServerPassword);
            Assert.AreNotEqual("host-pass", server.ServerPassword);
            StringAssert.StartsWith("pbkdf2_sha256$", server.ServerPassword);
            Assert.IsTrue(PasswordHasher.Verify("host-pass", server.ServerPassword));
        }
    }

    public class WorldGenerationTests
    {    private static string Tiles(Dungeon d) =>
            string.Join("|", d.Tiles.Select(row =>
                new string(row.Select(t => (char)('0' + (int)t)).ToArray())));

        [Test]
        public void Overworld_IsReproducibleForSameSeed()
        {
            var a = WorldGen.GenerateOverworld(12345);
            var b = WorldGen.GenerateOverworld(12345);
            var c = WorldGen.GenerateOverworld(54321);

            Assert.AreEqual(Tiles(a), Tiles(b));
            Assert.AreNotEqual(Tiles(a), Tiles(c));
            Assert.AreEqual(a.Monsters.Count, b.Monsters.Count);
        }

        [Test]
        public void Dungeon_IsReproducibleForSameSeed()
        {
            var a = DungeonGen.Generate(4, 777);
            var b = DungeonGen.Generate(4, 777);

            Assert.AreEqual(Tiles(a), Tiles(b));
            Assert.AreEqual(a.Rooms.Count, b.Rooms.Count);
            Assert.AreEqual(a.StairsPos, b.StairsPos);
        }

        [Test]
        public void Locations_AreReproducibleForSameSeed()
        {
            var a = Locations.GenerateLocation("pine", 99);
            var b = Locations.GenerateLocation("pine", 99);

            Assert.AreEqual(Tiles(a), Tiles(b));
            Assert.AreEqual(a.Monsters.Count, b.Monsters.Count);
        }
    }

    public class SharedSimulationTests
    {
        [Test]
        public void CharacterFactory_AppliesStartItemsOnce()
        {
            var player = CharacterFactory.Create("mage");
            var def = ClassData.Data("mage");

            foreach (var (key, count) in def.StartItems)
                Assert.AreEqual(count, player.ItemCount(key));
        }

        [Test]
        public void StatSystem_AllocatesPointAndRecalculates()
        {
            var player = new Player("warrior") { FreePoints = 1 };
            int before = player.Str;

            Assert.IsTrue(StatSystem.Allocate(player, 0));
            Assert.AreEqual(before + 1, player.Str);
            Assert.AreEqual(0, player.FreePoints);
            Assert.IsFalse(StatSystem.Allocate(player, 0));
        }

        [Test]
        public void ShopSystem_BuysAndSellsThroughSameRules()
        {
            var player = new Player("warrior") { Gold = 100 };
            var stock = new List<RecipeData.ShopEntry>
            {
                new() { Key = "potion_s", Price = 10, Infinite = true },
            };

            Assert.IsTrue(ShopSystem.Buy(player, stock, 0, out _));
            Assert.AreEqual(90, player.Gold);
            Assert.AreEqual(1, player.ItemCount("potion_s"));

            int idx = player.Inventory.Keys.ToList().IndexOf("potion_s");
            Assert.IsTrue(ShopSystem.Sell(player, idx, out _));
            Assert.AreEqual(0, player.ItemCount("potion_s"));
            Assert.IsTrue(player.Gold > 90);
        }

        [Test]
        public void CombatSystem_FindsAndDamagesNearestMonster()
        {
            var map = new Dungeon(20, 20);
            var player = new Player("warrior") { X = 5, Y = 5 };
            var monster = new Entity(6, 5, 50, 50, 0, 0, "Rat", 'r', Color.White, 5);
            map.Monsters.Add(monster);

            var outcome = CombatSystem.AttackNearest(player, map);

            Assert.IsNotNull(outcome);
            Assert.AreSame(monster, outcome!.Target);
            Assert.IsTrue(monster.Hp < 50);
        }

        [Test]
        public void SkillSystem_AppliesDamageAndPaysMana()
        {
            var player = new Player("mage") { Mana = 100 };
            var skill = SkillSystem.Get(player, 0);
            Assert.IsNotNull(skill);

            var target = new Entity(6, 5, 50, 50, 0, 0, "Rat", 'r', Color.White, 5);
            int damage = SkillSystem.Damage(player, skill!, target);
            SkillSystem.Pay(player, skill!);

            Assert.AreEqual(skill!.Damage + player.TotalInt, damage);
            Assert.AreEqual(50 - damage, target.Hp);
            Assert.AreEqual(100 - skill.Mana, player.Mana);
            Assert.IsTrue(player.ActiveSkillCd.ContainsKey(skill.Key));
        }
    }
}
