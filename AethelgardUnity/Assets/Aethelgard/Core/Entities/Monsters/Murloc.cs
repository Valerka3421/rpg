namespace Aethelgard.Entities.Monsters
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
    /// Мурлок — ядовитое чудовище пляжа (poison). Все данные вида перенесены из data/monsters.json
    /// прямо в класс (архитектура «Пример»: данные живут в классе).
    /// </summary>
    public class Murloc : Monster
    {
        public const string Key = "murloc";
    
        /// <summary>Данные вида до масштабирования (статический конструктор ниже).</summary>
        private static readonly MonsterData.Def _def;
    
        static Murloc()
        {
            _def = new MonsterData.Def
            {
                Key = "murloc",
                Name = "Мурлок",
                Ch = "m",
                Hp = 11,
                Atk = 3,
                Df = 1,
                Xp = 10,
                Color = new Microsoft.Xna.Framework.Color(90,180,170),
                MinDepth = 1,
                Ability = "poison",
                Boss = false,
                BossDepth = 0,
                OverworldOnly = true,
                Passive = false,
                Flees = false,
                Gold = false,
            };
        }
    
        public Murloc()
        {
        }
    
        /// <summary>Данные вида (перенесены из таблицы монстров в класс).</summary>
        public override MonsterData.Def Def => _def;
    
        public override void Spawn(int depth, List<Entity> list)
        {
            InitFromDef(_def, depth);
            list.Add(this);
        }
    }
}
