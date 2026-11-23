namespace Aethelgard.Structs;

/// <summary>
/// Типы тайлов карты — вынесены из Config в отдельную структуру слоя Structs.
/// </summary>
public enum Tile
{
    Wall = 0,
    Floor = 1,
    Stairs = 2,
    Exit = 3,
    UpStairs = 4,
    Gate = 5,
    LockedDoor = 6,
    Chest = 7,
}
