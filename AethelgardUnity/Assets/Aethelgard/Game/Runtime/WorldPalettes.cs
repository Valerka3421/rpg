using System.Collections.Generic;

namespace Aethelgard.Render;

/// <summary>
/// Палитры мира — точный порт render/tiles/_common.py: подземелье,
/// поверхность, 9 биомов, деревня и город.
/// </summary>
public static class WorldPalettes
{
    // ── Подземелье ──
    public static readonly (byte, byte, byte) WallBase = (74, 68, 59);
    public static readonly (byte, byte, byte) Mortar = (24, 22, 19);
    public static readonly (byte, byte, byte) FloorBase = (60, 56, 49);
    public static readonly (byte, byte, byte) FloorGrout = (31, 29, 25);
    public static readonly (byte, byte, byte) TorchGlow = (255, 170, 94);
    public static readonly (byte, byte, byte) Moss = (74, 84, 49);
    public static readonly (byte, byte, byte) MossDark = (47, 55, 32);
    public static readonly (byte, byte, byte) Rust = (104, 70, 40);
    public static readonly (byte, byte, byte) Bronze = (186, 142, 76);
    public static readonly (byte, byte, byte) Bone = (192, 180, 152);

    // ── Поверхность ──
    public static readonly (byte, byte, byte) GrassBase = (55, 64, 42);
    public static readonly (byte, byte, byte) GrassDark = (39, 46, 32);
    public static readonly (byte, byte, byte) GrassDry = (98, 92, 58);
    public static readonly (byte, byte, byte) TreeBase = (46, 58, 40);
    public static readonly (byte, byte, byte) TreeDark = (27, 35, 24);
    public static readonly (byte, byte, byte) Trunk = (66, 52, 38);
    public static readonly (byte, byte, byte) Stone = (86, 82, 74);
    public static readonly (byte, byte, byte) StoneDark = (48, 46, 41);

    public readonly record struct Biome(
        (byte, byte, byte) Grass, (byte, byte, byte) GrassDark,
        (byte, byte, byte) GrassDry, (byte, byte, byte) Tree,
        (byte, byte, byte) TreeDark, (byte, byte, byte) Trunk);

    // BIOME_PALETTES (1:1)
    public static readonly Dictionary<string, Biome> Biomes = new()
    {
        ["pine"] = new((55, 64, 42), (39, 46, 32), (80, 82, 50),
            (35, 54, 36), (18, 31, 21), (58, 46, 34)),
        ["birch"] = new((66, 76, 46), (43, 53, 32), (118, 108, 64),
            (76, 94, 52), (44, 62, 32), (156, 150, 132)),
        ["rock"] = new((70, 66, 59), (44, 42, 38), (106, 98, 82),
            (90, 86, 78), (50, 48, 44), (66, 62, 56)),
        ["autumn"] = new((76, 66, 42), (50, 42, 28), (138, 106, 52),
            (120, 80, 38), (76, 46, 22), (72, 54, 36)),
        ["swamp"] = new((43, 57, 47), (27, 38, 33), (78, 82, 56),
            (46, 62, 50), (23, 37, 31), (54, 48, 38)),
        ["dark"] = new((37, 47, 35), (21, 29, 23), (68, 66, 46),
            (29, 45, 33), (14, 25, 19), (48, 40, 31)),
        ["meadow"] = new((62, 74, 44), (38, 50, 30), (110, 104, 58),
            (56, 74, 44), (32, 48, 30), (68, 54, 38)),
        ["dead"] = new((59, 57, 47), (37, 35, 31), (96, 88, 70),
            (74, 66, 54), (42, 38, 32), (70, 64, 54)),
        ["beach"] = new((220, 200, 150), (180, 165, 120), (235, 220, 175),
            (110, 130, 100), (75, 90, 70), (130, 100, 70)),
    };

    public static readonly Biome Village =
        new((66, 78, 46), (44, 56, 32), (116, 108, 62),
            (60, 78, 46), (34, 50, 30), (70, 56, 38));

    public static readonly Biome City =
        new((62, 76, 48), (42, 54, 34), (108, 104, 64),
            (58, 76, 48), (34, 50, 32), (70, 56, 38));

    // _terrain.py: вода/песок/костёр/земли гоблинов
    public static readonly (byte, byte, byte) Water = (46, 92, 128);
    public static readonly (byte, byte, byte) WaterWave = (126, 178, 204);
    public static readonly (byte, byte, byte) Sand = (196, 178, 128);
    public static readonly (byte, byte, byte) GoblinGrass = (50, 62, 40);
    public static readonly (byte, byte, byte) FireFlame = (240, 110, 40);
    public static readonly (byte, byte, byte) FireCore = (255, 205, 90);
    public static readonly (byte, byte, byte) FireGlow = (255, 160, 60);

    /// <summary>Байты → Color32.</summary>
    public static UnityEngine.Color32 Col((byte, byte, byte) c) =>
        new(c.Item1, c.Item2, c.Item3, 255);
}
