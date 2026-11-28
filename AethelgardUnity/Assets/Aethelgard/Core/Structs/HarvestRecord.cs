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
    /// Сборный ресурс (дерево/руда/куст) на карте. Перенесён из HarvestSystem.
    /// </summary>
    public class HarvestRecord
    {
        public string Kind = "";
        public int Hp;
        public int MaxHp;
        public bool Has;
        public double ReadyAt;
        public double SpawnedAt;
    }
}
