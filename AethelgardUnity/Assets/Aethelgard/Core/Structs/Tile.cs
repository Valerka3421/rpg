namespace Aethelgard.Structs
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
}
