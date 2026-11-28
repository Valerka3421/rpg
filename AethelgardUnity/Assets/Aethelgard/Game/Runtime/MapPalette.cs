using UnityEngine;
    using XnaColor = Microsoft.Xna.Framework.Color;

    namespace Aethelgard.Render
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
    /// Палитра большой карты — точный порт render/bigmap_renderer.py:
    /// BIOME_COLORS, GROUND_COLORS, WALL_COLORS, подземелье, маркеры.
    /// </summary>
    public static class MapPalette
    {
        // ── BIOME_COLORS ──
        private static readonly System.Collections.Generic.Dictionary<string, Color32> BiomeColors =
            new()
            {
                ["pine"] = new(52, 74, 46, 255),
                ["birch"] = new(92, 112, 64, 255),
                ["rock"] = new(96, 92, 84, 255),
                ["autumn"] = new(126, 88, 48, 255),
                ["swamp"] = new(64, 84, 54, 255),
                ["dark"] = new(34, 44, 34, 255),
                ["meadow"] = new(108, 148, 76, 255),
                ["dead"] = new(74, 70, 60, 255),
                ["beach"] = new(222, 204, 152, 255),
            };

        // ── GROUND_COLORS ──
        private static readonly System.Collections.Generic.Dictionary<string, Color32> GroundColors =
            new()
            {
                ["road"] = new(132, 112, 78, 255),
                ["beach_sand"] = new(224, 206, 154, 255),
                ["mine_ground"] = new(74, 68, 60, 255),
                ["city_ruins_floor"] = new(122, 112, 98, 255),
                ["dungeon_ruins"] = new(104, 96, 84, 255),
                ["grass_dead"] = new(86, 92, 64, 255),
                ["goblin_plaza"] = new(118, 94, 60, 255),
                ["goblin_path"] = new(116, 88, 54, 255),
                ["goblin_grass"] = new(66, 88, 54, 255),
            };

        // ── WALL_COLORS ──
        private static readonly System.Collections.Generic.Dictionary<string, Color32> WallColors =
            new()
            {
                ["water"] = new(38, 74, 116, 255),
                ["beach_tree"] = new(38, 78, 44, 255),
                ["mine_wall"] = new(48, 44, 42, 255),
                ["city_ruins_wall"] = new(104, 94, 82, 255),
                ["city_ruins_statue"] = new(150, 142, 124, 255),
                ["tombstone"] = new(104, 104, 108, 255),
            };

        private static readonly Color32 DungeonFloor = new(96, 86, 70, 255);
        private static readonly Color32 DungeonWall = new(46, 42, 38, 255);
        private static readonly Color32 Unexplored = new(14, 12, 10, 255);

        // Маркеры
        public static readonly Color32 Stairs = new(255, 200, 90, 255);
        public static readonly Color32 Exit = new(180, 160, 100, 255);
        public static readonly Color32 Gate = new(140, 110, 70, 255);
        public static readonly Color32 Chest = new(244, 186, 64, 255);
        public static readonly Color32 Monster = new(200, 70, 60, 255);
        public static readonly Color32 Boss = new(255, 90, 60, 255);
        public static readonly Color32 Npc = new(110, 180, 220, 255);
        public static readonly Color32 Item = new(220, 200, 110, 255);
        public static readonly Color32 Player = new(120, 230, 130, 255);

        // Для рамки карты
        public static readonly Color32 FrameBorder = new(180, 160, 100, 255);
        public static readonly Color32 FramePad = new(12, 10, 8, 255);

        /// <summary>Цвет тайла для большой карты (parity bigmap_renderer).</summary>
        public static Color32 TileColor(Dungeon d, int x, int y, bool overworld)
        {
            if (x < 0 || y < 0 || x >= Config.MapW || y >= Config.MapH)
                return Unexplored;
            var tile = d.TileAt(x, y);
            if (overworld)
            {
                string kind = TileRenderer.TileKindAt(d, x, y);
                if (WallColors.TryGetValue(kind, out var wc)) return wc;
                if (GroundColors.TryGetValue(kind, out var gc)) return gc;
                string biome = kind.StartsWith("grass_", System.StringComparison.Ordinal)
                    ? kind["grass_".Length..] : "";
                if (biome.Length > 0 && BiomeColors.TryGetValue(biome, out var bc)) return bc;
                if (kind == "village_floor") return new Color32(66, 78, 46, 255);
                return new Color32(55, 64, 42, 255);
            }
            return tile switch
            {
                Tile.Wall => DungeonWall,
                Tile.Stairs => Stairs,
                Tile.Exit => Exit,
                Tile.Gate => Gate,
                _ => DungeonFloor,
            };
        }
    }
}
