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
    /// Призрак — вызывает кровотечение (bleeding), глубокие этажи. Все данные вида перенесены из data/monsters.json
    /// прямо в класс (архитектура «Пример»: данные живут в классе).
    /// </summary>
    public class Wraith : Monster
    {
        public const string Key = "wraith";
    
        /// <summary>Данные вида до масштабирования (статический конструктор ниже).</summary>
        private static readonly MonsterData.Def _def;
    
        static Wraith()
        {
            _def = new MonsterData.Def
            {
                Key = "wraith",
                Name = "Призрак",
                Ch = "W",
                Hp = 80,
                Atk = 18,
                Df = 4,
                Xp = 140,
                Color = new Microsoft.Xna.Framework.Color(180,180,220),
                MinDepth = 14,
                Ability = "bleeding",
                Boss = false,
                BossDepth = 0,
                OverworldOnly = false,
                Passive = false,
                Flees = false,
                Gold = true,
            };
        }
    
        public Wraith()
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
