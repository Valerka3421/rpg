using game.BaseClass;
using game.Enemy.Mobs;
using game.Structs;
using System.Numerics;
using System.Xml.Serialization;
using static game.Structs.ItemStruct;

namespace game.enemy
{
    public class Player : Specific
    {
        private ClassPlayer _playerClass;
        public ClassPlayer PlayerClass => _playerClass;
        private int _xp;
        public int Xp => _xp;
        private int _gold;
        public int Gold
        {
            get
            {
                return _gold;
            }
            set
            {
                _gold -= value;
                if (_gold < 0)
                {
                    _gold = 0;
                }

            }
        }
        public enum ClassPlayer
        {
            warrior,
            mage,
            archer,
            rogue
        }

        private List<ItemStruct.Item> _items = new List<ItemStruct.Item>();
        public List<ItemStruct.Item> InventoryPlayer => _items;
        private Dictionary<int, int> _lvlXp = new Dictionary<int, int>();
        public Player(string name, int id_class)
        {
            _name = name;
            _playerClass = (ClassPlayer)id_class;
            _xp = 0;
            _level = 1;
            Class();
            _gold = 100;
            _missChance = 10;
            LevelGenerate();
        }
        public void Class()
        {
            if (_playerClass == ClassPlayer.warrior)
            {
                _health = 10;
                _damage = 6;
                _speed = 2;
                _protect = 2;
                _protectQuality = 0.5;
            }
            else if (_playerClass == ClassPlayer.mage)
            {
                _health = 5;
                _damage = 8;
                _speed = 1;
                _protect = 0.5;
                _protectQuality = 0.1;
            }
            else if (_playerClass == ClassPlayer.archer)
            {
                _health = 6;
                _damage = 5;
                _speed = 2;
                _protect = 0.5;
                _protectQuality = 0.3;
            }
            else if (_playerClass == ClassPlayer.rogue)
            {
                _health = 8;
                _damage = 4;
                _speed = 3;
                _protect = 1;
                _protectQuality = 0.2;
            }
        }
        public void Inventory()
        {
            if (_items.Count == 0)
            {
                Console.WriteLine("--------- Инвентарь персонажа пуст ----------");
            }
            else
            {
                Console.WriteLine("--------- Инвентарь персонажа ----------");
                int j = 1;
                for (int i = 0; i < _items.Count; i++) {
                    Console.WriteLine("---- Предмет " + j + " ----");
                    Console.WriteLine("Название предмета: " + _items[i].Name);
                    Console.WriteLine("Дополнительное здоровье: " + _items[i].Health);
                    Console.WriteLine("Дополнительная скорость: " + _items[i].Speed);
                    Console.WriteLine("Тип предмета: " + _items[i].Type);
                    Console.WriteLine("Размер предмета: " + _items[i].Size);
                    Console.WriteLine("Урон предмета: " + _items[i].Damage);
                    Console.WriteLine("Защита предмета: " + _items[i].Protect);
                    Console.WriteLine("Качество защиты предмета: " + _items[i].ProtectQuality);
                    Console.WriteLine("------------");
                    j++;
                }
                CheckInventory();
            }
        }
        public void CheckInventory()
        {
            int m = 0;
            int l = 0;
            int s = 0;
            foreach (var item in _items)
            {
                if (item.Size == SizeItem.Large)
                {
                    l++;
                }
                else if (item.Size == SizeItem.Small)
                {
                    s++;
                }
                else if (item.Size == SizeItem.Medium)
                {
                    m++;
                }
            }
            if (m >= 3)
            {
                Console.WriteLine("Достигнуто максимальное количество предметов размера Medium");
            }
            else
            {
                Console.WriteLine("Занято " + m + " мест размера Medium из 3");
            }

            if (s >= 3)
            {
                Console.WriteLine("Достигнуто максимальное количество предметов размера Small");
            }
            else
            {
                Console.WriteLine("Занято " + s + " мест размера Small из 3");
            }

            if (l >= 3)
            {
                Console.WriteLine("Достигнуто максимальное количество предметов размера Large");
            }
            else
            {
                Console.WriteLine("Занято " + l + " мест размера Large из 3");
            }
        }
        public void AddInventory(ItemStruct.Item item)
        {
            _items.Add(item);
            SpecificItems();
        }
        public void Trade()
        {
            if (_items.Count == 0)
            {
                Console.WriteLine("--------- Инвентарь персонажа пуст ----------");
            }
            else
            {
                Console.WriteLine("--------- Инвентарь персонажа ----------");
                int j = 1;
                for (int i = 0; i < _items.Count; i++)
                {
                    int price_item = _items[i].Price - (_items[i].Price / 2);
                    Console.WriteLine("---- Предмет " + j + " ----");
                    Console.WriteLine("Название предмета: " + _items[i].Name);
                    Console.WriteLine("Дополнительное здоровье: " + _items[i].Health);
                    Console.WriteLine("Дополнительная скорость: " + _items[i].Speed);
                    Console.WriteLine("Тип предмета: " + _items[i].Type);
                    Console.WriteLine("Размер предмета: " + _items[i].Size);
                    Console.WriteLine("Урон предмета: " + _items[i].Damage);
                    Console.WriteLine("Защита предмета: " + _items[i].Protect);
                    Console.WriteLine("Качество защиты предмета: " + _items[i].ProtectQuality);
                    Console.WriteLine("Стоимость при продаже: " + price_item);
                    Console.WriteLine("------------------------");
                    j++;
                }
                while(true)
                {
                    Console.WriteLine("Какой предмет ходите продать? (введите 0 если хотите отменить продажу)");
                    bool item = int.TryParse(Console.ReadLine(), out int h);
                    if (item)
                    {
                        if (h == 0)
                        {
                            Console.Clear();
                            Console.WriteLine("Продажа отменена");
                            break;
                        }
                        else
                        {
                            int id = h - 1;
                            int price = _items[id].Price - (_items[id].Price / 2);
                            _gold += price;
                            _items.Remove(_items[id]);
                            Console.WriteLine($"Предмет {h} продан за {price} золота");
                            break;
                        }
                    }
                    else
                    {
                        Console.WriteLine(" ");
                        Console.WriteLine("Не корректное значение");
                        continue;
                    }
                }
                
            }
            
        }
        public void SpecificItems()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                _health += _items[i].Health;
                _damage += _items[i].Damage;
                _protect += _items[i].Protect;
                _speed += _items[i].Speed;
                _protectQuality += _items[i].ProtectQuality;
            }
        }
        public void Specific()
        {
            int id = Level + 1;
            Console.WriteLine("Характеристики персонажа " + Name);
            if(id > 50)
            {
                Console.WriteLine("Достигнут максимальный уровень");
            }
            else
            {
                Console.WriteLine("Опыт: " + _xp + " из " + _lvlXp[id]);
            }
            
            Console.WriteLine("Ваш уровень: " + Level);
            Console.WriteLine("Ваш класс: " + PlayerClass);
            Console.WriteLine("Ваше здоровье: " + Health);
            Console.WriteLine("Ваш урон: " + Damage);
            Console.WriteLine("Ваша скорость: " + Speed);
            Console.WriteLine("Ваша защита: " + Protect);
            Console.WriteLine("Урон по вашей броне: " + ProtectQuality + "%");
            Console.WriteLine("Шанс промаха: " + MissChance + "%");
            Console.WriteLine("Ваше золото: " + Gold);
        }
        private void LvlUp()
        {
            Console.WriteLine("Ваш уровень повышен");
            if (_playerClass == ClassPlayer.warrior)
            {
                _health = 2.5 + 2.5 * Level;
                _damage = 3 + 3 * Level;
                _speed = 1 + 1 * Level;
                _protect = 1 + 1 * Level;
                _protectQuality = 0.4 + 0.1 * Level;
            }
            else if (_playerClass == ClassPlayer.mage)
            {
                _health = 5 + 5 * Level;
                _damage = 4 + 4 * Level;
                _speed = 0.5 + 0.5 * Level;
                _protect = 0.25 + 0.25 * Level;
                _protectQuality = 0.05 + 0.05 * Level;
            }
            else if (_playerClass == ClassPlayer.archer)
            {
                _health = 3 + 3 * Level;
                _damage = 2.5 + 2.5 * Level;
                _speed = 1 + 1 * Level;
                _protect = 0.25 + 0.25 * Level;
                _protectQuality = 0.15 + 0.15 * Level;
            }
            else if (_playerClass == ClassPlayer.rogue)
            {
                _health = 4 + 4 * Level;
                _damage = 2 + 2 * Level;
                _speed = 1.5 + 1.5 * Level;
                _protect = 0.5 + 0.5 * Level;
                _protectQuality = 0.1 + 0.1 * Level;
            }

            if(_level == 10)
            {
                Console.WriteLine("Достижение 'Это только начало' получено");
                _gold += 100;
            }

            _gold += 100;

            if (_protectQuality > 1)
            {
                _protectQuality = 1;
            }

            _health = Math.Round(_health, 2);
            _damage = Math.Round(_damage, 2);
            _speed = Math.Round(_speed, 2);
            _protect = Math.Round(_protect, 2);
            _protectQuality = Math.Round(_protectQuality, 3);
            SpecificItems();
        }
        private void CheckXp()
        {
            if (_level == 50)
            {
                Console.WriteLine("Достигнут максимальный уровень");
            }
            else
            {
                int old_lvl = Level;
                foreach (var xp in _lvlXp)
                {
                    if (_xp >= xp.Value)
                    {
                        _level = xp.Key;

                    }
                }
                if (_level > old_lvl)
                {
                    LvlUp();
                }
            }
        }
        public void LevelGenerate()
        {
            int lvl = 1;

            for (int o = 0; o < 50; o++)
            {
                _lvlXp[lvl] = 20 * (2 * o);
                lvl++;
            }
        }
        public void Fight(int id, Main mobs)
        {
            string name_mob = mobs.MobsList[id].Name;
            double hp_mob = mobs.MobsList[id].Health;
            double damage_mob = mobs.MobsList[id].Damage;
            double protect_mob = mobs.MobsList[id].Protect;
            double quality_mob = mobs.MobsList[id].ProtectQuality;
            double missChance_mob = mobs.MobsList[id].MissChance;
            double attack_mob = 0;

            double hp_player = Health;
            double damage_player = Damage;
            double protect_player = Protect;
            double quality_player = ProtectQuality;
            double attack_player = 0;

            int miss;

            int step = 1;
            Random rnd = new Random();

            Console.WriteLine("");
            Console.WriteLine("Здоровье " + Name + " " + hp_player + "/" + Health);
            Console.WriteLine("Здоровье " + name_mob + " " + hp_mob + "/" + mobs.MobsList[id].Health);
            Console.WriteLine("");

            while (true)
            {
                if (hp_player > 0)
                {
                    miss = rnd.Next(1, 100);
                    Console.WriteLine("Ход " + step);
                    Thread.Sleep(1000);
                    if (miss > MissChance)
                    {
                        attack_player = rnd.Next((int)damage_player - 1, (int)damage_player + 1);
                        hp_mob -= attack_player - protect_mob * quality_mob;
                        hp_mob = Math.Round(hp_mob, 3);
                        if (hp_mob < 0)
                        {
                            hp_mob = 0;
                        }
                        Console.WriteLine("Персонаж " + Name + " атаковал " + name_mob + " на " + attack_player);
                        Console.WriteLine("Броня " + name_mob + " заблокировала " + protect_mob * quality_mob + " урона");
                        Console.WriteLine("Здоровье " + name_mob + ": " + hp_mob + "/" + mobs.MobsList[id].Health);
                    }
                    else
                    {
                        Console.WriteLine("Персонаж " + Name + " промахнулся");
                        Console.WriteLine("Здоровье " + name_mob + ": " + hp_mob + "/" + mobs.MobsList[id].Health);
                    }
                    step++;
                    Thread.Sleep(2500);
                    if (hp_mob > 0)
                    {
                        miss = rnd.Next(1, 100);
                        Console.WriteLine("Ход " + step);
                        Thread.Sleep(1000);
                        if (miss > missChance_mob)
                        {
                            attack_mob = rnd.Next((int)damage_mob - 1, (int)damage_mob + 1);
                            if (attack_mob - protect_player * quality_player > 0)
                            {
                                hp_player -= attack_mob - protect_player * quality_player;
                            }
                            hp_player = Math.Round(hp_player, 3);
                            if (hp_player < 0)
                            {
                                hp_player = 0;
                            }
                            Console.WriteLine("Противник " + name_mob + " атаковал " + Name + " на " + attack_mob);
                            Console.WriteLine("Броня " + Name + " заблокировала " + protect_player * quality_player + " урона");
                            Console.WriteLine("Здоровье " + Name + ": " + hp_player + "/" + Health);
                        }
                        else
                        {
                            Console.WriteLine("Противник " + name_mob + " промахнулся");
                            Console.WriteLine("Здоровье " + Name + ": " + hp_player + "/" + Health);
                        }

                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }

                if (hp_mob <= 0 || hp_player <= 0)
                {
                    break;
                }

                step++;
                Thread.Sleep(2500);
            }
            Thread.Sleep(5000);
            if (hp_mob >= 0 & hp_player <= 0)
            {
                Console.WriteLine("Вы проигрыли");
                Console.WriteLine("Нажмите Enter чтобы продолжить");
                Console.ReadLine();
                Console.Clear();
            }
            else if (hp_mob <= 0 & hp_player >= 0)
            {
                _xp += mobs.MobsList[id].XPDrop;
                _gold += mobs.MobsList[id].GoldDrop;

                CheckXp();

                Console.WriteLine("Вы победили");
                Console.WriteLine("Вы получили " + mobs.MobsList[id].XPDrop + " опыта");
                Console.WriteLine("Вы получили " + mobs.MobsList[id].GoldDrop + " золота");
                Console.WriteLine("Нажмите Enter чтобы продолжить");
                Console.ReadLine();
                Console.Clear();
            }
        }
        public void Achievements()
        {
            Console.WriteLine("------------ Достяжения ------------");
            Console.WriteLine("------ Это свет в конце тоннеля! А, нет ... ------");
            Console.WriteLine("Условия получения: Достигнуть 3 уровня");
            Console.WriteLine("Награда: Открытие магазина");
            Console.WriteLine("");
            Console.WriteLine("------ Это только начало ------");
            Console.WriteLine("Условия получения: Достигнуть 10 уровня");
            Console.WriteLine("Награда: 100 золота");
            Console.WriteLine("");
            Console.WriteLine("------ Как то не чувсвую ------");
            Console.WriteLine("Условия получения: Победить первого босса");
            Console.WriteLine("Награда: +15 к урону");
            Console.WriteLine("");
        }
    }
}
