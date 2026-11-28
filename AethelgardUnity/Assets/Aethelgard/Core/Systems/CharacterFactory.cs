namespace Aethelgard.Systems
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
    /// Единая инициализация игрока для синглплеера и MP-сервера.
    ///
    /// Раньше <see cref="GameController.NewGame"/> и <see cref="GameCore.AddPlayer"/>
    /// почти построчно повторяли друг друга: бонусы класса, пересчёт статов,
    /// стартовые предметы и автоэкипировка. Теперь это живёт здесь.
    /// </summary>
    public static class CharacterFactory
    {
        /// <summary>Создать персонажа выбранного класса со стартовым набором.</summary>
        public static Player Create(string classKey) =>
            ApplyClass(new Player(classKey), classKey);
    
        /// <summary>
        /// Применить к уже созданному персонажу бонусы класса и автоэкипировку.
        /// Стартовые предметы добавляет конструктор <see cref="Player"/>, поэтому
        /// здесь они не начисляются повторно (раньше SP и MP удваивали набор).
        /// </summary>
        public static Player ApplyClass(Player player, string classKey)
        {
            var def = ClassData.Get(classKey);
            if (def == null) return player;
    
            player.ClassName = def.Name;
            player.Str += def.StrBonus;
            player.Agi += def.AgiBonus;
            player.Vit += def.VitBonus;
            player.Luk += def.LukBonus;
            player.IntStat += def.IntBonus;
            player.DfStat += def.DfStatBonus;
            player.ClassTier = def.Tier;
            player.Recalc();
            player.Hp = player.MaxHp;
            player.Mana = player.MaxMana;
    
            // Автоэкипировка первого оружия/брони/шлема из стартового набора.
            foreach (var (itemKey, _) in def.StartItems)
            {
                var itemDef = ItemData.Get(itemKey);
                if (itemDef != null &&
                    (itemDef.Kind == ItemData.ItemType.Weapon
                     || itemDef.Kind == ItemData.ItemType.Armor
                     || itemDef.Kind == ItemData.ItemType.Helmet))
                {
                    player.EquipItem(itemKey);
                }
            }
    
            return player;
        }
    }
}
