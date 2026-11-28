using UnityEngine;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace Aethelgard.Render;

/// <summary>
/// Палитра большой карты (Bigmap) — порт MonoGame MapPalette
/// (parity python bigmap_renderer.py).
/// </summary>
public static class MapPalette
{
    public static readonly Color32 Wall = new(74, 68, 59, 255);
    public static readonly Color32 Floor = new(60, 56, 49, 255);
    public static readonly Color32 Grass = new(55, 64, 42, 255);
    public static readonly Color32 Tree = new(46, 58, 40, 255);
    public static readonly Color32 Road = new(104, 90, 66, 255);
    public static readonly Color32 Village = new(90, 80, 62, 255);
    public static readonly Color32 Unexplored = new(14, 12, 10, 255);

    public static readonly Color32 Stairs = new(240, 200, 90, 255);
    public static readonly Color32 Exit = new(180, 160, 100, 255);
    public static readonly Color32 Gate = new(140, 110, 70, 255);
    public static readonly Color32 Chest = new(220, 180, 60, 255);
    public static readonly Color32 Monster = new(200, 70, 60, 255);
    public static readonly Color32 Boss = new(255, 90, 60, 255);
    public static readonly Color32 Npc = new(110, 180, 220, 255);
    public static readonly Color32 Item = new(220, 200, 110, 255);
    public static readonly Color32 Player = new(120, 230, 130, 255);

    /// <summary>Цвет тайла для большой карты (parity MapPalette.TileColor).</summary>
    public static Color32 TileColor(Dungeon d, int x, int y, bool overworld)
    {
        if (x < 0 || y < 0 || x >= Config.MapW || y >= Config.MapH)
            return Unexplored;
        var tile = d.TileAt(x, y);
        return tile switch
        {
            Tile.Wall => Wall,
            Tile.Stairs => Stairs,
            Tile.Exit => Exit,
            Tile.Gate => Gate,
            _ => overworld ? Grass : Floor,
        };
    }
}
