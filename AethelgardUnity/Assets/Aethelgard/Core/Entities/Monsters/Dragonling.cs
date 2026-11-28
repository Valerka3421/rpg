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
    /// Дракончик — поджигает цель (burning), глубокие этажи. Все данные вида перенесены из data/monsters.json
    /// прямо в класс (архитектура «Пример»: данные живут в классе).
    /// </summary>
    public class Dragonling : Monster
    {
        public const string Key = "dragonling";
    
        /// <summary>Данные вида до масштабирования (статический конструктор ниже).</summary>
        private static readonly MonsterData.Def _def;
    
        static Dragonling()
        {
            _def = new MonsterData.Def
            {
                Key = "dragonling",
                Name = "Дракончик",
                Ch = "d",
                Hp = 90,
                Atk = 16,
                Df = 5,
                Xp = 130,
                Color = new Microsoft.Xna.Framework.Color(200,150,60),
                MinDepth = 10,
                Ability = "burning",
                Boss = false,
                BossDepth = 0,
                OverworldOnly = false,
                Passive = false,
                Flees = false,
                Gold = true,
            };
        }
    
        public Dragonling()
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
