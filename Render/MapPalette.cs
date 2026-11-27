using Aethelgard.Systems;
using Aethelgard.World;
using Microsoft.Xna.Framework;

namespace Aethelgard.Render;

/// <summary>
/// Единая палитра тайлов для мини-карты и большой карты.
/// Гарантирует, что обе карты выглядят одинаково и соответствуют миру.
/// Помимо цвета каждый объект имеет буквенную метку (глиф), которая
/// рисуется поверх клетки на большой карте — обозначения читаются
/// даже при плохом восприятии цвета.
/// </summary>
public static class MapPalette
{
    public static readonly Color Unexplored = new(16, 14, 12);
    public static readonly Color Floor = new(96, 88, 74);
    public static readonly Color Wall = new(52, 46, 38);
    public static readonly Color Tree = new(46, 74, 40);
    public static readonly Color Rock = new(98, 92, 78);
    public static readonly Color Stairs = new(120, 190, 235);
    public static readonly Color Exit = new(110, 210, 110);
    public static readonly Color UpStairs = new(220, 185, 85);
    public static readonly Color Gate = new(210, 165, 70);
    public static readonly Color Chest = new(240, 200, 70);
    public static readonly Color LockedDoor = new(220, 80, 60);

    public static readonly Color Player = new(255, 225, 60);
    public static readonly Color Monster = new(225, 75, 75);
    public static readonly Color Boss = new(255, 40, 40);
    public static readonly Color Npc = new(250, 210, 90);
    public static readonly Color Item = new(200, 170, 80);

    // ── Глифы поверх цветных клеток (большая карта, клетка ≥ 6 px) ──
    public const char PlayerGlyph = 'P';
    public const char NpcGlyph = 'N';
    public const char MonsterGlyph = 'M';
    public const char BossGlyph = 'B';
    public const char ItemGlyph = '$';
    public const char StairsGlyph = '>';
    public const char UpStairsGlyph = '<';
    public const char GateGlyph = 'G';
    public const char ChestGlyph = '$'; // сундук различим цветом золота

    /// <summary>
    /// Цвет клетки под глифом большой карты: раньше глифы рисовались только
    /// чёрным слоем и были невидимы на тёмных клетках; теперь клетка получает
    /// подложку соответствующего цвета, а глиф — белый.
    /// </summary>
    public static Color GlyphColor(char glyph) => glyph switch
    {
        StairsGlyph => Stairs,
        UpStairsGlyph => UpStairs,
        GateGlyph => Gate,
        ChestGlyph => Chest,
        '+' => LockedDoor,
        _ => Floor,
    };

    /// <summary>Глиф тайла (x, y), либо null — глиф не нужен.</summary>
    public static char? TileGlyph(Dungeon d, int x, int y)
    {
        switch (d.Tiles[y][x])
        {
            case Tile.Stairs: return StairsGlyph;
            case Tile.Exit: return StairsGlyph;
            case Tile.UpStairs: return UpStairsGlyph;
            case Tile.Gate: return GateGlyph;
            case Tile.Chest: return ChestGlyph;
            case Tile.LockedDoor: return '+';
            default: return null;
        }
    }

    /// <summary>Цвет тайла (x, y) с учётом биома (деревья/камни на оверворлде).</summary>
    public static Color TileColor(Dungeon d, int x, int y, bool overworld)
    {
        var t = d.Tiles[y][x];
        switch (t)
        {
            case Tile.Wall:
                if (overworld)
                {
                    string kind = Overworld.WallKind(x, y);
                    if (kind.StartsWith("tree")) return Tree;
                    if (kind == "rock") return Rock;
                }
                if (d.Harvestables.ContainsKey((x, y))) return Tree;
                return Wall;
            case Tile.Stairs: return Stairs;
            case Tile.Exit: return Exit;
            case Tile.UpStairs: return UpStairs;
            case Tile.Gate: return Gate;
            case Tile.Chest: return Chest;
            case Tile.LockedDoor: return LockedDoor;
            default: return Floor;
        }
    }
}
