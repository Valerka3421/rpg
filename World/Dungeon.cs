using Microsoft.Xna.Framework;
using Aethelgard.Data;

namespace Aethelgard.World;

/// <summary>
/// Карта уровня: сетка тайлов + сущности + предметы. Перенесена из
/// Entities.cs в отдельный файл слоя World. Комнаты — единый тип
/// Structs.Room (в оригинале Room объявлялся дважды).
/// </summary>
public class Dungeon
{
    public int W, H;
    public Tile[][] Tiles;       // [y][x]
    public List<Room> Rooms = new();
    public List<Entity> Monsters = new();
    public Dictionary<(int, int), string> Items = new();
    public (int, int)? StairsPos;
    public Dictionary<(int, int), HarvestRecord> Harvestables = new();
    public Dictionary<(int, int), (string To, (int, int) Entry)> Transitions = new();
    public HashSet<(int, int)> BarrierTiles = new();
    public (int, int)? TreasuryPos;
    public HashSet<(int, int)> BossDoorCells = new();
    public Dictionary<(int, int), Dictionary<string, int>> ChestLoot = new();
    public HashSet<(int, int)> Explored = new();
    public int Depth = 1;

    /// <summary>Сток торговца на этаже (MERCHANT_FLOORS), генерируется по MakeShopStock.</summary>
    public List<RecipeData.ShopEntry>? MerchantStock;

    /// <summary>Имя локации для HUD (village/pine/...), null = подземелье/оверворлд.</summary>
    public string? LocationKey;

    public Dungeon(int w, int h)
    {
        W = w; H = h;
        Tiles = new Tile[h][];
        for (int y = 0; y < h; y++)
        {
            Tiles[y] = new Tile[w];
            for (int x = 0; x < w; x++)
                Tiles[y][x] = Tile.Wall;
        }
    }

    public Tile TileAt(int x, int y)
    {
        if (x < 0 || x >= W || y < 0 || y >= H) return Tile.Wall;
        return Tiles[y][x];
    }

    public void SetTile(int x, int y, Tile t)
    {
        if (x < 0 || x >= W || y < 0 || y >= H) return;
        Tiles[y][x] = t;
    }

    public bool IsWalkable(int x, int y)
    {
        var t = TileAt(x, y);
        if (t is Tile.Wall or Tile.LockedDoor or Tile.Chest) return false;
        if (Harvestables.ContainsKey((x, y))) return false;
        return true;
    }

    public Entity? MonsterAt(int x, int y) =>
        Monsters.Find(m => m.X == x && m.Y == y && m.Alive);

    public Entity? NpcAt(int x, int y) =>
        Monsters.Find(m => m.X == x && m.Y == y && m.IsNpc && m.Alive);
}
