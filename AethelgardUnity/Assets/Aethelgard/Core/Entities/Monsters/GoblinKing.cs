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
    /// Король гоблинов — БОСС глубины 5 (этаж 5 подземелья). Все данные вида перенесены из data/monsters.json
    /// прямо в класс (архитектура «Пример»: данные живут в классе).
    /// </summary>
    public class GoblinKing : Monster
    {
        public const string Key = "boss_g_5";
    
        /// <summary>Данные вида до масштабирования (статический конструктор ниже).</summary>
        private static readonly MonsterData.Def _def;
    
        static GoblinKing()
        {
            _def = new MonsterData.Def
            {
                Key = "boss_g_5",
                Name = "Король гоблинов",
                Ch = "G",
                Hp = 80,
                Atk = 9,
                Df = 2,
                Xp = 150,
                Color = new Microsoft.Xna.Framework.Color(255,100,100),
                MinDepth = 5,
                Ability = null,
                Boss = true,
                BossDepth = 5,
                OverworldOnly = false,
                Passive = false,
                Flees = false,
                Gold = true,
            };
        }
    
        public GoblinKing()
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
