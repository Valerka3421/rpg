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
    /// Слайм — ядовитая слизь поверхности (poison). Все данные вида перенесены из data/monsters.json
    /// прямо в класс (архитектура «Пример»: данные живут в классе).
    /// </summary>
    public class Slime : Monster
    {
        public const string Key = "slime";
    
        /// <summary>Данные вида до масштабирования (статический конструктор ниже).</summary>
        private static readonly MonsterData.Def _def;
    
        static Slime()
        {
            _def = new MonsterData.Def
            {
                Key = "slime",
                Name = "Слайм",
                Ch = "j",
                Hp = 10,
                Atk = 2,
                Df = 1,
                Xp = 6,
                Color = new Microsoft.Xna.Framework.Color(100,190,90),
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
    
        public Slime()
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
