using System;
using System.Collections.Generic;
using System.Linq;
using Aethelgard.Data;
using Aethelgard.Entities;
using Aethelgard.Net;
using Aethelgard.Systems;
using Aethelgard.World;
using Microsoft.Xna.Framework;
using Xunit;

namespace Aethelgard.Tests;

public class SaveSystemTests
{
    [Fact]
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
        Assert.Contains("\"Inventory\"", json);
        Assert.Contains("\"StairsPos\"", json);

        var back = SaveSystem.Deserialize(json);
        Assert.NotNull(back);
        Assert.Equal(7, back!.World.Depth);
        Assert.Equal("mine", back.World.Location);
        Assert.Equal(3, back.Player.Inventory["wood"]);
        Assert.Equal(2, back.Player.Inventory["potion_s"]);
        Assert.Equal(new[] { "m_0", "m_1" }, back.Player.LearnedNodes);
        Assert.Equal(10, back.Floors[0].StairsPos!.X);
        Assert.Equal(31, back.Floors[0].BossDoorCells[0].Y);
    }

    [Fact]
    public void Deserialize_RejectsFutureSaveVersion()
    {
        var data = SaveSystem.Deserialize("{\"Version\":999}");
        Assert.Null(data);
    }

    [Fact]
    public void PlayerData_CompatibilityAliasesMapToNewSlots()
    {
        var player = new SaveSystem.PlayerData
        {
            Helmet = "iron_helm",
            Leggings = "chain_legs",
        };

        Assert.Equal("iron_helm", player.Head);
        Assert.Equal("chain_legs", player.Legs);
    }
}

public class ProtocolTests
{
    [Fact]
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

        Assert.NotNull(back);
        Assert.Equal("dungeon", back!.WorldMode);
        Assert.Equal(5, back.StairsPos!.X);
        Assert.Equal(new Color(10, 20, 30, 255), back.Players[0].Color);
        Assert.Equal("wood", back.Items[0].Key);
        Assert.Equal("привет", back.Chat[0].Text);
    }

    [Fact]
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

        Assert.True(Protocol.Verify(payload, signature, key));

        // Другая полезная нагрузка → подпись недействительна.
        msg.Dx = 0;
        string tampered = Protocol.SignedPayload(msg);
        Assert.False(Protocol.Verify(tampered, signature, key));
    }

    [Fact]
    public void ClientMessage_CarriesProtocolVersion()
    {
        var msg = new ClientMsg { Type = "hello" };
        Assert.Equal(Protocol.Version, msg.ProtocolVersion);
        Assert.Equal(Protocol.Version,
            Protocol.Deserialize<ClientMsg>(Protocol.Serialize(msg))!.ProtocolVersion);
    }

    [Fact]
    public void Ecdh_BothSidesDeriveSameSessionKey()
    {
        // v4: сессионный ключ HMAC выводится из ECDH P-256, plaintext-ключ
        // в welcome больше не передаётся (защита от пассивного прослушивания)
        using var server = System.Security.Cryptography.ECDiffieHellman.Create(
            System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        using var client = System.Security.Cryptography.ECDiffieHellman.Create(
            System.Security.Cryptography.ECCurve.NamedCurves.nistP256);

        var serverPub = Convert.ToBase64String(
            server.PublicKey.ExportSubjectPublicKeyInfo());
        var clientPub = Convert.ToBase64String(
            client.PublicKey.ExportSubjectPublicKeyInfo());

        using var importedServer = System.Security.Cryptography.ECDiffieHellman.Create();
        importedServer.ImportSubjectPublicKeyInfo(
            Convert.FromBase64String(serverPub), out _);
        using var importedClient = System.Security.Cryptography.ECDiffieHellman.Create();
        importedClient.ImportSubjectPublicKeyInfo(
            Convert.FromBase64String(clientPub), out _);

        var serverKey = server.DeriveKeyFromHash(importedClient.PublicKey,
            System.Security.Cryptography.HashAlgorithmName.SHA256);
        var clientKey = client.DeriveKeyFromHash(importedServer.PublicKey,
            System.Security.Cryptography.HashAlgorithmName.SHA256);

        Assert.Equal(32, serverKey.Length);
        Assert.Equal(serverKey, clientKey);
    }
}

public class EffectTests
{
    [Fact]
    public void ClassEffects_AreIncludedInAggregate()
    {
        var player = new Player("artisan");
        var effects = player.Effects();

        Assert.True(effects.GetValueOrDefault("craft_xp") > 0);
    }

    [Fact]
    public void LearningNode_DoesNotPermanentlyMutateBaseStats()
    {
        var player = new Player("artisan");
        int baseStr = player.Str;
        int baseVit = player.Vit;

        player.LearnedNodes.Add("a_0");
        player.Recalc();

        // База не меняется, итоговые значения — вычисляемые.
        Assert.Equal(baseStr, player.Str);
        Assert.Equal(baseVit, player.Vit);
        Assert.Equal(baseStr + 2, player.TotalStr);
        Assert.Equal(baseVit + 2, player.TotalVit);
    }

    [Fact]
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

        Assert.Equal(15 + 5 * 2 + 20, stats.MaxHp);
        Assert.Equal(2 + 5 / 2 + 3, stats.Atk);
        Assert.Equal(5 / 2 + 2, stats.Df);
        Assert.Equal(10 + 1 * 2 + 5 * 2 + 10, stats.MaxMana);
        Assert.True(stats.Dodge >= 0.10f);
        Assert.True(stats.Crit >= 0.05f);
    }
}

public class FloorPersistenceTests
{
    [Fact]
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

        Assert.NotNull(back);
        Assert.Equal(d.W, back!.W);
        Assert.Equal("pine", back.LocationKey);
        Assert.Equal(("pine", (7, 8)), back.Transitions[(5, 6)]);
        Assert.Contains((9, 10), back.BarrierTiles);
        Assert.Contains((11, 12), back.Explored);
        Assert.Equal(3, back.ChestLoot[(1, 2)]["wood"]);
        Assert.Equal(d.Tiles[0][0], back.Tiles[0][0]);
    }
}

public class PasswordHasherTests
{
    [Fact]
    public void Hash_ThenVerify_AcceptsCorrectPassword()
    {
        string encoded = PasswordHasher.Hash("s3cret-пароль");

        Assert.StartsWith("pbkdf2_sha256$", encoded);
        Assert.True(PasswordHasher.Verify("s3cret-пароль", encoded));
        Assert.False(PasswordHasher.Verify("s3cret-пароль ", encoded));
        Assert.False(PasswordHasher.Verify("S3cret-пароль", encoded));
    }

    [Fact]
    public void Hash_UsesUniqueSaltForEveryCall()
    {
        string a = PasswordHasher.Hash("same");
        string b = PasswordHasher.Hash("same");

        Assert.NotEqual(a, b);
        Assert.True(PasswordHasher.Verify("same", a));
        Assert.True(PasswordHasher.Verify("same", b));
    }

    [Fact]
    public void Verify_RejectsMalformedOrLegacyValues()
    {
        Assert.False(PasswordHasher.Verify("secret", null));
        Assert.False(PasswordHasher.Verify("secret", ""));
        Assert.False(PasswordHasher.Verify("secret", "secret"));  // plaintext
        Assert.False(PasswordHasher.Verify("secret", "pbkdf2_sha256$bad"));
    }

    [Fact]
    public void ServerPassword_StoresOnlyHash()
    {
        var server = new GameServer { ServerPassword = "host-pass" };

        Assert.NotNull(server.ServerPassword);
        Assert.NotEqual("host-pass", server.ServerPassword);
        Assert.StartsWith("pbkdf2_sha256$", server.ServerPassword);
        Assert.True(PasswordHasher.Verify("host-pass", server.ServerPassword));
    }
}

public class WorldGenerationTests
{    private static string Tiles(Dungeon d) =>
        string.Join("|", d.Tiles.Select(row =>
            new string(row.Select(t => (char)('0' + (int)t)).ToArray())));

    [Fact]
    public void Overworld_IsReproducibleForSameSeed()
    {
        var a = WorldGen.GenerateOverworld(12345);
        var b = WorldGen.GenerateOverworld(12345);
        var c = WorldGen.GenerateOverworld(54321);

        Assert.Equal(Tiles(a), Tiles(b));
        Assert.NotEqual(Tiles(a), Tiles(c));
        Assert.Equal(a.Monsters.Count, b.Monsters.Count);
    }

    [Fact]
    public void Dungeon_IsReproducibleForSameSeed()
    {
        var a = DungeonGen.Generate(4, 777);
        var b = DungeonGen.Generate(4, 777);

        Assert.Equal(Tiles(a), Tiles(b));
        Assert.Equal(a.Rooms.Count, b.Rooms.Count);
        Assert.Equal(a.StairsPos, b.StairsPos);
    }

    [Fact]
    public void Locations_AreReproducibleForSameSeed()
    {
        var a = Locations.GenerateLocation("pine", 99);
        var b = Locations.GenerateLocation("pine", 99);

        Assert.Equal(Tiles(a), Tiles(b));
        Assert.Equal(a.Monsters.Count, b.Monsters.Count);
    }
}

public class SharedSimulationTests
{
    [Fact]
    public void CharacterFactory_AppliesStartItemsOnce()
    {
        var player = CharacterFactory.Create("mage");
        var def = ClassData.Data("mage");

        foreach (var (key, count) in def.StartItems)
            Assert.Equal(count, player.ItemCount(key));
    }

    [Fact]
    public void StatSystem_AllocatesPointAndRecalculates()
    {
        var player = new Player("warrior") { FreePoints = 1 };
        int before = player.Str;

        Assert.True(StatSystem.Allocate(player, 0));
        Assert.Equal(before + 1, player.Str);
        Assert.Equal(0, player.FreePoints);
        Assert.False(StatSystem.Allocate(player, 0));
    }

    [Fact]
    public void ShopSystem_BuysAndSellsThroughSameRules()
    {
        var player = new Player("warrior") { Gold = 100 };
        var stock = new List<RecipeData.ShopEntry>
        {
            new() { Key = "potion_s", Price = 10, Infinite = true },
        };

        Assert.True(ShopSystem.Buy(player, stock, 0, out _));
        Assert.Equal(90, player.Gold);
        Assert.Equal(1, player.ItemCount("potion_s"));

        int idx = player.Inventory.Keys.ToList().IndexOf("potion_s");
        Assert.True(ShopSystem.Sell(player, idx, out _));
        Assert.Equal(0, player.ItemCount("potion_s"));
        Assert.True(player.Gold > 90);
    }

    [Fact]
    public void CombatSystem_FindsAndDamagesNearestMonster()
    {
        var map = new Dungeon(20, 20);
        var player = new Player("warrior") { X = 5, Y = 5 };
        var monster = new Entity(6, 5, 50, 50, 0, 0, "Rat", 'r', Color.White, 5);
        map.Monsters.Add(monster);

        var outcome = CombatSystem.AttackNearest(player, map);

        Assert.NotNull(outcome);
        Assert.Same(monster, outcome!.Target);
        Assert.True(monster.Hp < 50);
    }

    [Fact]
    public void SkillSystem_AppliesDamageAndPaysMana()
    {
        var player = new Player("mage") { Mana = 100 };
        var skill = SkillSystem.Get(player, 0);
        Assert.NotNull(skill);

        var target = new Entity(6, 5, 50, 50, 0, 0, "Rat", 'r', Color.White, 5);
        int damage = SkillSystem.Damage(player, skill!, target);
        SkillSystem.Pay(player, skill!);

        Assert.Equal(skill!.Damage + player.TotalInt, damage);
        Assert.Equal(50 - damage, target.Hp);
        Assert.Equal(100 - skill.Mana, player.Mana);
        Assert.True(player.ActiveSkillCd.ContainsKey(skill.Key));
    }

    [Fact]
    public void VampireMonster_LifestealsInsteadOfPoisoning()
    {
        // Раньше вампир имел Ability "poison" и травил вместо вампиризма
        var vamp = MonsterData.CreateEntity("vampire", 10);
        Assert.Equal("vampire", vamp.Ability);

        var player = CharacterFactory.Create("warrior");
        vamp.Atk = Math.Max(vamp.Atk, 20);       // гарантированный урон

        // Уклонение — редкая случайность: повторяем до не-уклонения,
        // чтобы тест был детерминированным
        Rules.AttackResult result;
        int hpBefore;
        do
        {
            vamp.Hp = Math.Max(1, vamp.MaxHp / 2);   // раненый вампир должен лечиться
            hpBefore = vamp.Hp;
            result = Rules.ResolveAttack(vamp, player);
        } while (result.Dodged);

        Assert.True(result.Damage > 0);
        Assert.True(result.Healed > 0, "вампир должен восстанавливать HP при атаке");
        Assert.True(vamp.Hp > hpBefore);
        Assert.Null(result.StatusApplied);       // яд больше не применяется
    }

    [Fact]
    public void Snapshot_DeltaSkipsTilesWhenClientRevisionCurrent()
    {
        // Дельта-снапшоты: клиент с актуальной ревизией не получает карту
        // (раньше каждый снапшот 20 Гц таскал всю карту JSON-строками)
        var core = new GameCore();
        core.AddPlayer(1, "warrior");

        var full = core.BuildSnapshot(1, clientMapRev: -1);
        Assert.NotNull(full.Tiles);
        Assert.True(full.Tiles!.Count > 0);
        Assert.Equal(full.MapRev, core.BuildSnapshot(1).MapRev);

        var delta = core.BuildSnapshot(1, clientMapRev: full.MapRev);
        Assert.Null(delta.Tiles);                       // карта не передаётся
        Assert.Equal(full.MapRev, delta.MapRev);

        // Смена карты меняет ревизию — клиент снова получает тайлы
        var p = core.Players[1];
        var (sx, sy) = Aethelgard.World.Overworld.DungeonPos;
        p.X = sx; p.Y = sy;
        core.ProcessInput(1, new ClientMsg { Type = "input", UseStairs = true });
        var afterMove = core.BuildSnapshot(1, clientMapRev: full.MapRev);
        Assert.NotNull(afterMove.Tiles);
        Assert.NotEqual(full.MapRev, afterMove.MapRev);
    }
}
