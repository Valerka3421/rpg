namespace Aethelgard.Structs;

/// <summary>
/// Прямоугольная комната подземелья. Единый тип для DungeonGen и Dungeon
/// (в оригинале Room объявлялся дважды — top-level и вложенный в DungeonGen).
/// </summary>
public class Room
{
    public int X, Y, W, H;

    public Room(int x, int y, int w, int h) { X = x; Y = y; W = w; H = h; }

    public int CX => X + W / 2;
    public int CY => Y + H / 2;
    public (int X, int Y) Center => (X + W / 2, Y + H / 2);

    public bool Contains(int x, int y) =>
        x >= X && x < X + W && y >= Y && y < Y + H;

    public bool Intersects(Room other, int pad = 1) =>
        !(X + W + pad < other.X || other.X + other.W + pad < X ||
          Y + H + pad < other.Y || other.Y + other.H + pad < Y);
}
