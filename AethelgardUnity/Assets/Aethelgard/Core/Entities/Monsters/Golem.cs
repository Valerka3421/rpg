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
    /// Голем — живая броня: много HP и высокая защита. Все данные вида перенесены из data/monsters.json
    /// прямо в класс (архитектура «Пример»: данные живут в классе).
    /// </summary>
    public class Golem : Monster
    {
        public const string Key = "golem";
    
        /// <summary>Данные вида до масштабирования (статический конструктор ниже).</summary>
        private static readonly MonsterData.Def _def;
    
        static Golem()
        {
            _def = new MonsterData.Def
            {
                Key = "golem",
                Name = "Голем",
                Ch = "Y",
                Hp = 120,
                Atk = 14,
                Df = 8,
                Xp = 150,
                Color = new Microsoft.Xna.Framework.Color(130,130,140),
                MinDepth = 12,
                Ability = null,
                Boss = false,
                BossDepth = 0,
                OverworldOnly = false,
                Passive = false,
                Flees = false,
                Gold = true,
            };
        }
    
        public Golem()
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
