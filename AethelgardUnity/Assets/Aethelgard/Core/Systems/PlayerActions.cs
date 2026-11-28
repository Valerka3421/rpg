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
    /// Активные навыки класса: проверка ресурсов, лечение, урон и оплата.
    /// Общая часть <see cref="GameController.CastActiveSkill"/> и MP-обработчика
    /// навыков в <see cref="GameCore"/>.
    /// </summary>
    public static class SkillSystem
    {
        public static ClassData.ActiveSkill? Get(Player player, int index)
        {
            var skills = player.ActiveSkills();
            return index < 0 || index >= skills.Count ? null : skills[index];
        }
    
        public static bool IsHeal(ClassData.ActiveSkill skill) =>
            skill.Kind.Contains("heal", StringComparison.Ordinal);
    
        /// <summary>Причина, по которой навык нельзя применить, или null.</summary>
        public static string? Validate(Player player, ClassData.ActiveSkill skill)
        {
            if (player.ActiveSkillCd.TryGetValue(skill.Key, out float cd) && cd > 0)
                return $"{skill.Name}: перезарядка {cd:F1} с.";
            if (player.Mana < skill.Mana)
                return "Недостаточно маны!";
            return null;
        }
    
        /// <summary>Лечение собой; возвращает фактически восстановленное HP.</summary>
        public static int Heal(Player player, ClassData.ActiveSkill skill)
        {
            int before = player.Hp;
            player.Hp = Math.Min(player.MaxHp, player.Hp + skill.Damage);
            return player.Hp - before;
        }
    
        /// <summary>Урон по цели (с бонусом интеллекта) и статус заморозки.</summary>
        public static int Damage(Player player, ClassData.ActiveSkill skill, Entity target)
        {
            int damage = skill.Damage + player.TotalInt;
            target.Hp -= damage;
            if (skill.Key == "frost_bolt" || skill.Kind.Contains("frost", StringComparison.Ordinal))
                target.AddStatus("slow");
            return damage;
        }
    
        /// <summary>Списать ману и запустить перезарядку после успешного каста.</summary>
        public static void Pay(Player player, ClassData.ActiveSkill skill)
        {
            player.Mana -= skill.Mana;
            player.ActiveSkillCd[skill.Key] = (float)skill.Cooldown;
        }
    }
    
    /// <summary>
    /// Магазин: городской сток, покупка и продажа. Общий для SP-экрана и MP.
    /// Проверка близости к NPC остаётся в вызывающем коде (у SP и MP разные
    /// источники карты), а экономика — здесь.
    /// </summary>
    public static class ShopSystem
    {
        public static List<RecipeData.ShopEntry> CityStock(string npcType)
        {
            var list = new List<RecipeData.ShopEntry>();
            if (CityData.Npcs.TryGetValue(npcType, out var npcDef))
            {
                foreach (var e in npcDef.ShopStock)
                    list.Add(new RecipeData.ShopEntry
                    {
                        Key = e.Key,
                        Price = e.Price,
                        Infinite = true,
                    });
            }
            return list;
        }
    
        public static bool Buy(Player player, IReadOnlyList<RecipeData.ShopEntry> stock,
            int index, out string message)
        {
            message = "";
            if (index < 0 || index >= stock.Count) return false;
            var entry = stock[index];
            if (entry.Sold && !entry.Infinite)
            {
                message = "Уже продано.";
                return false;
            }
            var itemDef = ItemData.Get(ItemData.ParseItemKey(entry.Key).Base);
            if (itemDef == null) return false;
            if (ItemData.ParseItemKey(entry.Key).Base == "book_double"
                && player.DoubleStrike)
            {
                message = "Двойной удар уже изучен.";
                return false;
            }
            if (player.Gold < entry.Price)
            {
                message = "Недостаточно золота!";
                return false;
            }
    
            player.Gold -= entry.Price;
            player.AddItem(entry.Key, 1);
            if (!entry.Infinite) entry.Sold = true;
            message = $"Куплено: {itemDef.Name} за {entry.Price} золота";
            return true;
        }
    
        public static bool Sell(Player player, int index, out string message)
        {
            message = "";
            // parity rules.inventory_stacks: снаряжение по 1, стаки по MAX_STACK
            var items = player.InventoryStacks();
            if (index < 0 || index >= items.Count) return false;
    
            var (key, _) = items[index];
            int price = ItemData.SellPrice(key);
            if (price <= 0)
            {
                message = "Этот предмет нельзя продать.";
                return false;
            }
    
            player.Gold += price;
            player.RemoveItem(key, 1);
            message = $"Продано: {ItemData.DisplayName(key)} за {price} золота";
            return true;
        }
    }
    
    /// <summary>Распределение свободных очков характеристик (SP и MP).</summary>
    public static class StatSystem
    {
        public static bool Allocate(Player player, int index)
        {
            if (player.FreePoints <= 0) return false;
    
            bool ok = index switch
            {
                0 => Alloc(ref player.Str),
                1 => Alloc(ref player.Agi),
                2 => Alloc(ref player.Vit),
                3 => Alloc(ref player.Luk),
                4 => Alloc(ref player.DfStat),
                5 => Alloc(ref player.IntStat),
                _ => false,
            };
            if (ok)
            {
                player.FreePoints--;
                player.Recalc();
            }
            return ok;
    
            static bool Alloc(ref int stat) { stat++; return true; }
        }
    
        /// <summary>Вариант для сетевого протокола: "str"/"agi"/…/"df_stat"/"int".</summary>
        public static bool Allocate(Player player, string stat) => stat switch
        {
            "str" => Allocate(player, 0),
            "agi" => Allocate(player, 1),
            "vit" => Allocate(player, 2),
            "luk" => Allocate(player, 3),
            "df_stat" => Allocate(player, 4),
            "int" => Allocate(player, 5),
            _ => false,
        };
    }
    
    /// <summary>Дерево навыков и повышение класса — общие сценарии SP/MP.</summary>
    public static class ProgressionSystem
    {
        public static bool LearnNode(Player player, int index, out string message)
        {
            message = "";
            var nodes = SkillTree.GetNodes(player);
            if (index < 0 || index >= nodes.Length) return false;
    
            var node = nodes[index];
            if (SkillTree.LearnNode(player, node.Key))
            {
                message = $"Изучено: {node.Name}";
                return true;
            }
    
            if (player.LearnedNodes.Contains(node.Key))
                message = "Уже изучено.";
            else if (player.Level < node.Level)
                message = $"Требуется уровень {node.Level}.";
            else
                message = "Нет очков навыков!";
            return false;
        }
    
        public static bool Promote(Player player, out string message)
        {
            message = "";
            var promotions = ClassData.AvailablePromotions(player);
            if (promotions.Length == 0)
            {
                message = "Нет доступного повышения класса.";
                return false;
            }
    
            string promoKey = promotions[0];
            if (!ClassData.Promote(player, promoKey)) return false;
            message = $"Новый класс: {ClassData.Get(promoKey)?.Name}!";
            return true;
        }
    }
    
    /// <summary>Мелкие общие операции с предметами.</summary>
    public static class ItemSystem
    {
        /// <summary>Выпить лучшее доступное зелье лечения (s → m → l).</summary>
        public static string? UseBestPotion(Player player, Dungeon? map)
        {
            foreach (var key in new[] { "potion_s", "potion_m", "potion_l" })
            {
                if (player.ItemCount(key) > 0 && player.UseItem(key, map))
                    return key;
            }
            return null;
        }
    }
}
