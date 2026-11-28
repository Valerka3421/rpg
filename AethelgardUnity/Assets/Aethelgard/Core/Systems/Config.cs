using System;
    using System.Collections.Generic;
    using Microsoft.Xna.Framework;
    
    namespace Aethelgard.Systems
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
    /// Central configuration: constants, colors, balance.
    /// Ported from Python core/config.py.
    /// </summary>
    public static class Config
    {
        // ── Paths ──
        public static readonly string BaseDir = AppContext.BaseDirectory;
        public static readonly string DataDir = Environment.GetEnvironmentVariable("ETHELGARD_DATA_DIR")
            ?? AppContext.BaseDirectory;
    
        // ── Генерация мира ──
        /// <summary>Базовое зерно мира. Одинаковое зерно → одинаковая карта.</summary>
        public const int WorldSeed = 20261121;
    
        // ── Map dimensions ──
        public const int TileSize = 64;
        public const int HudHeight = 170;
        public const int MapW = 140;
        public const int MapH = 70;
        public const int LegacyMapW = 100;
        public const int LegacyMapH = 70;
    
        // ── Resolution ──
        // Дизайн-разрешение рендер-таргета. 1920×1080 — 1:1 с типичным окном,
        // интерфейс и мир не растягиваются → картинка резкая.
        public const int ScreenW = 1920;
        public const int ScreenH = 1080;
    
        public static int ViewW => ScreenW / TileSize;            // 20
        public static int ViewH => (ScreenH - HudHeight) / TileSize; // 8
    
        public static int HudHeightActual => ScreenH - ViewH * TileSize;
        public static (int X, int Y) ViewOrigin => ((ScreenW - ViewW * TileSize) / 2, 0);
    
        // ── Game parameters ──
        public const int FovRadius = 9;
        public const bool FogOfWarEnabled = false;
        public const int MaxDepth = 20;
        public static readonly int[] MerchantFloors = { 3, 6, 9, 12, 15, 18 };
        public static readonly string[] OverworldMonsters = { "rat", "wolf", "boar" };
        public const int InventoryCols = 10;
        public const int InventoryRows = 10;
        public const int InventoryPageSize = InventoryCols * InventoryRows;
        public const int MaxStack = 99;
        public const int ShopPageSize = 9;
        public const int CraftPageSize = 8;
        public const int MinimapScale = 3;
        public const int MinimapMargin = 8;
        public const int RevealRadius = int.MaxValue / 4; // туман войны отключён — карта открыта целиком
        public const int SaveVersion = 8;
    
        // ── Player cooldowns (seconds) ──
        public const float PlayerMoveCooldown = 0.12f;
        public const float PlayerAttackCooldown = 0.40f;
        public const float PlayerRangedCooldown = 0.55f;
        public const float MonsterBaseCooldown = 0.35f;
        public const float StatusTickInterval = 0.50f;
        public const float RegenTickInterval = 5.00f;
        public const int RegenHpPerTick = 1;
        public const int RegenManaPerTick = 1;
        public const float HarvestRespawnTime = 120.0f;
    
        // ── Combat constants ──
        public const int DmgSpread = 1;
        public const float CritMult = 1.5f;
        public const int XpLevelGuard = 200;
        public const float DodgeCap = 0.30f;
        public const float DodgeTotalCap = 0.40f;
        public const float CritBaseCap = 0.40f;
        public const float CritTotalCap = 0.50f;
        public const int ThornsDamage = 2;
        public const float DoubleStrikeChance = 0.15f;
        public const int SkillCap = 25;
        public const int CitySafeMargin = 8;
        public const double ManaCrystalChance = 0.08;
        public const int ChatMaxLength = 120;
        public const int MaxMsgSize = 65536;
        public const int LogMaxMessages = 50;
        public const int DevConsoleMaxTries = 3;
        public const int DevConsoleLockSeconds = 30;
    
        // ── XP curve: xp_next(level) = XP_BASE * level^XP_EXPONENT ──
        public const int XpBase = 10;
        public const float XpExponent = 1.35f;
    
        // ── MP scaling: +20% HP / +10% ATK за каждого игрока сверх первого ──
        public const float MpHpScalePerPlayer = 0.20f;
        public const float MpAtkScalePerPlayer = 0.10f;
    
        // ── XP split in MP: 70% killer / 30% teammates ──
        public const float MpKillerXpShare = 0.70f;
    
        // ── Monster timing ──
        public const float MonsterActionInterval = 0.35f;
        public const float MonsterActionJitter = 0.20f;
        public const float BossActionInterval = 0.55f;
        public const float BossBreathInterval = 4.5f;
        public const int BossBreathRangeMin = 2;
        public const int BossBreathRangeMax = 5;
        public const float BossBreathPower = 0.6f;
    
        // ── Animation ──
        public const float AnimIdleSpeed = 4.0f;
        public const float AnimIdleAmp = 2.5f;
        public const float AnimAttackDur = 0.25f;
        public const int AnimAttackAmp = 12;
    
        // ── Projectiles ──
        public const float ProjectileSpeedArrow = 12.0f;
        public const float ProjectileSpeedFireball = 8.0f;
        public const float ProjectileSpeedFrost = 9.0f;
        public const float ProjectileSpeedLightning = 16.0f;
        public const float ProjectileSpeedHeal = 10.0f;
    
        // ── UI ──
        public const int ModalMarginX = 40;
        public const int ModalMarginY = 20;
        public const int ModalContentPad = 20;
        public const int ModalTopPad = 56;
        public const int ModalCloseSize = 34;
        public const int ModalClosePad = 10;
        public const int ToolbarBtn = 56;
        public const int ToolbarGap = 8;
        public const int ToolbarMargin = 12;
        public const int PlusSize = 26;
        public const int HammerSize = 32;
        public const int QuickbarSlots = 10;
        public const int QuickbarSlotSize = 48;
        public const int QuickbarGap = 4;
        public const int QuickbarYOffset = 8;
        public const int BigmapMargin = 80;
        public const int BigmapTitleH = 40;
    
        // ── Colors (Stoneshard palette) ──
        public static readonly Color ColBg = new(12, 12, 18);
        public static readonly Color ColWall = new(70, 60, 85);
        public static readonly Color ColWallD = new(30, 26, 38);
        public static readonly Color ColFloor = new(38, 33, 48);
        public static readonly Color ColFloorD = new(20, 18, 26);
        public static readonly Color ColText = new(225, 225, 235);
        public static readonly Color ColDim = new(110, 110, 130);
        public static readonly Color ColPlayer = new(120, 230, 130);
        public static readonly Color ColStairs = new(240, 200, 90);
        public static readonly Color ColHp = new(200, 60, 60);
        public static readonly Color ColHpBg = new(50, 20, 20);
        public static readonly Color ColXp = new(80, 160, 240);
        public static readonly Color ColGold = new(240, 210, 90);
        public static readonly Color ColDmg = new(255, 100, 100);
        public static readonly Color ColCrit = new(255, 200, 50);
        public static readonly Color ColHeal = new(120, 240, 120);
        public static readonly Color ColDodge = new(180, 180, 255);
        public static readonly Color ColWin = new(255, 220, 80);
        public static readonly Color ColMerch = new(240, 200, 100);
        public static readonly Color BigmapPadColor = new(12, 10, 8);
        public static readonly Color BigmapBorder = new(180, 160, 100);
    
        // ── Tile colors for procedural rendering ──
        public static readonly Color WallBase = new(74, 68, 59);
        public static readonly Color WallMortar = new(24, 22, 19);
        public static readonly Color FloorBase = new(60, 56, 49);
        public static readonly Color FloorGrout = new(31, 29, 25);
        public static readonly Color GrassBase = new(55, 64, 42);
        public static readonly Color GrassDark = new(39, 46, 32);
        public static readonly Color TreeBase = new(46, 58, 40);
        public static readonly Color TreeDark = new(27, 35, 24);
        public static readonly Color Trunk = new(66, 52, 38);
        public static readonly Color Stone = new(86, 82, 74);
        public static readonly Color StoneDark = new(48, 46, 41);
        public static readonly Color TorchGlow = new(255, 170, 94);
        public static readonly Color RoadColor = new(104, 90, 66);
    
        /// <summary>XP needed to advance from `level` to `level+1`.</summary>
        public static int XpToNext(int level) =>
            Math.Max(1, (int)(XpBase * Math.Pow(Math.Max(1, level), XpExponent)));
    
        /// <summary>Deterministic hash for variant selection.</summary>
        public static int HashInt(int x, int y, int seed, int modulo)
        {
            unchecked
            {
                int h = seed;
                h = h * 31 + x;
                h = h * 31 + y;
                h = h ^ (h >> 13);
                h = h * 0x45d9f3b;
                h = h ^ (h >> 16);
                return ((h % modulo) + modulo) % modulo;
            }
        }
    
        public static float Hash01(int x, int y, int seed) =>
            HashInt(x, y, seed, 100000) / 100000f;
    
        /// <summary>XP за уровень навыка ремесла: 45·lvl^1.35 (min 10).</summary>
        public static int CraftSkillXpToNext(int level) =>
            Math.Max(10, (int)(45 * Math.Pow(Math.Max(1, level), 1.35)));
    
        /// <summary>Цена реролла подземного магазина: 15 + depth*3.</summary>
        public static int RerollPrice(int depth) => 15 + depth * 3;
    
        /// <summary>Мана: восстановление 1 MP каждые 5 с всегда; HP — только с перком regen.</summary>
        public static bool CanRegenHp(bool hasRegenPerk) => hasRegenPerk;
    
        /// <summary>Валидация шага в MP: ровно один шаг по одной оси (net/core.py:_is_valid_step).</summary>
        public static bool IsValidStep(int dx, int dy)
        {
            if (dx == 0 && dy == 0) return false;
            if (Math.Abs(dx) > 1 || Math.Abs(dy) > 1) return false;
            return dx == 0 || dy == 0; // только по одной оси
        }
    
        /// <summary>Санитизация чата: сжатие пробелов + обрезка до 120 символов.</summary>
        public static string SanitizeChat(string text)
        {
            var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string cleaned = string.Join(" ", parts);
            return cleaned.Length <= ChatMaxLength ? cleaned : cleaned[..ChatMaxLength];
        }
    }
}
