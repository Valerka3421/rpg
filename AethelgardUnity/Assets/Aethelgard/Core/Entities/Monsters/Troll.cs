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
    /// Тролль — тяжёлый монстр средних этажей. Все данные вида перенесены из data/monsters.json
    /// прямо в класс (архитектура «Пример»: данные живут в классе).
    /// </summary>
    public class Troll : Monster
    {
        public const string Key = "troll";
    
        /// <summary>Данные вида до масштабирования (статический конструктор ниже).</summary>
        private static readonly MonsterData.Def _def;
    
        static Troll()
        {
            _def = new MonsterData.Def
            {
                Key = "troll",
                Name = "Тролль",
                Ch = "T",
                Hp = 35,
                Atk = 8,
                Df = 3,
                Xp = 40,
                Color = new Microsoft.Xna.Framework.Color(85,140,85),
                MinDepth = 4,
                Ability = null,
                Boss = false,
                BossDepth = 0,
                OverworldOnly = false,
                Passive = false,
                Flees = false,
                Gold = true,
            };
        }
    
        public Troll()
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
