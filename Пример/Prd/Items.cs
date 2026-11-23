using game.Structs;
using static game.enemy.Player;
using static game.Structs.ItemStruct;

namespace game.Prd
{
    public class Items
    {
        public List<string> NamesWeapons = new List<string> { "Ржавый меч", "Меч всадника", "Меч Артура" };
        public List<string> NamesArmor = new List<string> { "Кольчуга", "Латы", "Бронежилет" };
        public List<string> NamesAccessories = new List<string> { "Кольцо силы", "Кожанная одежда", "Обвес", "Серьга ловкости", "Ожерелье мудрости" };
        public List<ItemStruct.Item> items = new List<ItemStruct.Item>();

        public void ListItems(int level)
        {
            int MinDamage = 2 + 2 * level;
            int MaxDamage = 20 + 10 * level;

            int MinProtect = 2 + 4 * level;
            int MaxProtect = 20 + 5 * level;

            int MinPrice = 10 + 10 * level;
            int MaxPrice = 50 + 10 * level;

            int count = 0;

            if (level < 10)
            {
                count = 3;
            }
            else
            {
                count = 5;
            }

            Random rnd = new Random();

            int types = Enum.GetNames(typeof(TypeItem)).Length;
            int sizes = Enum.GetNames(typeof(SizeItem)).Length;
            int clases = Enum.GetNames(typeof(ClassPlayer)).Length;

            string name = "";
            double damage = 0;
            double protect = 0;

            for (int i = 0; i < count; i++)
            {
                int idType = rnd.Next(0, types);
                TypeItem type = (TypeItem)Enum.GetValues(typeof(TypeItem)).GetValue(idType);
                if (type == TypeItem.Weapons)
                {
                    protect = 0;
                    name = NamesWeapons[rnd.Next(0, NamesWeapons.Count)];
                    damage = rnd.Next(MinDamage, MaxDamage);
                }
                else if (type == TypeItem.Armor)
                {
                    damage = 0;
                    name = NamesArmor[rnd.Next(0, NamesArmor.Count)];
                    protect = rnd.Next(MinProtect, MaxProtect);
                }
                else if (type == TypeItem.Accessories)
                {
                    damage = 0;
                    name = NamesAccessories[rnd.Next(0, NamesAccessories.Count)];
                    protect = rnd.Next(MinProtect, MaxProtect);
                }

                int idSize = rnd.Next(0, sizes);
                SizeItem size = (SizeItem)Enum.GetValues(typeof(SizeItem)).GetValue(idSize);

                int idClass = rnd.Next(0, clases);
                ClassPlayer class_enemy = (ClassPlayer)Enum.GetValues(typeof(ClassPlayer)).GetValue(idClass);

                int price = rnd.Next(MinPrice, MaxPrice);

                ItemStruct.Item item = new ItemStruct.Item
                {
                    Name = name,
                    Health = 0,
                    Damage = damage,
                    Speed = 0,
                    Protect = protect,
                    ProtectQuality = 0.01,
                    Size = size,
                    Type = type,
                    Class = class_enemy,
                    Price = price
                };
                items.Add(item);

            }
        }

    }
}
