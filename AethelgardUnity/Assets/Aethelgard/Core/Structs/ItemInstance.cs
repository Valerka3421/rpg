using Microsoft.Xna.Framework;
    
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
    /// Типизированный ключ предмета с редкостью. Перенесён из QuestData.cs,
    /// где он был объявлен «случайно» в конце файла с данными квестов.
    /// </summary>
    public sealed class ItemInstance
    {
        public string Base { get; }
        public int Rarity { get; }
    
        public ItemInstance(string baseKey, int rarity = 0)
        {
            Base = baseKey;
            Rarity = rarity;
        }
    
        public string Key() => ItemData.MakeLootKey(Base, Rarity);
        public override string ToString() => Key();
        public ItemData.Def Data() => ItemData.Get(Base) ?? new ItemData.Def { Key = Base };
        public string DisplayName() => ItemData.DisplayName(Key());
        public Color DisplayColor() => ItemData.DisplayColor(Key());
        public int SellPrice() => ItemData.SellPrice(Key());
    
        public static ItemInstance FromKey(string fullKey)
        {
            var (baseKey, rarity) = ItemData.ParseItemKey(fullKey);
            return new ItemInstance(baseKey ?? "", rarity);
        }
    }
}
