using Microsoft.Xna.Framework;
    
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
    /// Абстрактный монстр. Конкретные виды (Rat, Goblin, …, AncientDragon)
    /// объявлены отдельными классами в папке Monsters — как в проекте «Пример»
    /// (Zombie.cs, Skelet.cs, Wolf.cs, Boss.cs): каждый класс несёт СВОИ
    /// характеристики (перенесены из data/monsters.json) и реализует Spawn().
    /// </summary>
    public abstract class Monster : Entity
    {
        protected Monster() { }
    
        /// <summary>Данные вида до масштабирования (живут в классе монстра).</summary>
        public abstract MonsterData.Def Def { get; }
    
        /// <summary>Заспавнить монстра с масштабированием по глубине и добавить в список.</summary>
        public abstract void Spawn(int depth, List<Entity> list);
    
        /// <summary>Общая инициализация по данным таблицы монстров.</summary>
        protected void InitFromDef(MonsterData.Def def, int depth)
        {
            int hp, atk, df;
            if (def.Boss)
                (hp, atk, df) = MonsterData.ScaleBossStats(def.Hp, def.Atk, def.Df, depth);
            else
                (hp, atk, df) = MonsterData.ScaleStats(def.Hp, def.Atk, def.Df, depth);
    
            Name = def.Name;
            Ch = def.Ch.Length > 0 ? def.Ch[0] : '?';
            Color = def.Color;
            MaxHp = hp;
            Hp = hp;
            Atk = atk;
            Df = df;
            XpValue = def.Xp;
            Boss = def.Boss;
            Ability = def.Ability;
            MonsterKey = def.Key;
            Passive = def.Passive;
            Flees = def.Flees;
            Ranged = def.Ability == "ranged";
            Healer = def.Ability == "healer";
            Rules.InitActionTimers(new List<Entity> { this });
        }
    }
}
