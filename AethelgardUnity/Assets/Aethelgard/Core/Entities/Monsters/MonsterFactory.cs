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
    /// Фабрика монстров: по ключу создаёт экземпляр нужного вида.
    /// Вызывается из MonsterData.CreateEntity; реестр AllKeys используется
    /// для построения таблицы MonsterData.All прямо из классов монстров.
    /// </summary>
    public static class MonsterFactory
    {
        /// <summary>Все виды монстров (ключ таблицы → класс).</summary>
        public static readonly string[] AllKeys =
        {
            "rat", "goblin", "archer", "skeleton", "shaman", "orc", "troll",
            "demon", "vampire", "dragonling", "golem", "wraith",
            "deer", "wolf", "bear", "slime", "boar", "crab", "murloc", "turtle",
            "boss_g_5", "boss_l_10", "boss_v_15", "boss_a_20",
        };
    
        /// <summary>Создать монстра вида key. Неизвестный ключ → null.</summary>
        public static Monster? Create(string key) => key switch
        {
            "rat" => new Rat(),
            "goblin" => new Goblin(),
            "archer" => new Archer(),
            "skeleton" => new Skeleton(),
            "shaman" => new Shaman(),
            "orc" => new Orc(),
            "troll" => new Troll(),
            "demon" => new Demon(),
            "vampire" => new Vampire(),
            "dragonling" => new Dragonling(),
            "golem" => new Golem(),
            "wraith" => new Wraith(),
            "deer" => new Deer(),
            "wolf" => new Wolf(),
            "bear" => new Bear(),
            "slime" => new Slime(),
            "boar" => new Boar(),
            "crab" => new Crab(),
            "murloc" => new Murloc(),
            "turtle" => new Turtle(),
            "boss_g_5" => new GoblinKing(),
            "boss_l_10" => new Lich(),
            "boss_v_15" => new VampireLord(),
            "boss_a_20" => new AncientDragon(),
            _ => null,
        };
    }
}
