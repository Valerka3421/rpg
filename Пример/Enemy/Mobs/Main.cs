using game.Prd;
using static game.Structs.ItemStruct;
using static game.Enemy.Mobs.Mob;
using static System.Net.Mime.MediaTypeNames;
using System;

namespace game.Enemy.Mobs
{
    public class Main
    {
        public List<Mob> MobsList = new List<Mob>();
        private static bool _boss = false;

        public void ListMobs(int lvl_player)
        {
            Console.WriteLine("Список врагов:");
            Random rnd = new Random();

            if (lvl_player == 10 || lvl_player == 20 || lvl_player == 30 || lvl_player == 40 || lvl_player == 49)
            {
                _boss = true;
            }

            if (_boss)
            {
                FactoryMob(null, lvl_player, null);
                Console.WriteLine("Противник " + MobsList[0].Name);
                Console.WriteLine("Характеристики");
                Console.WriteLine("Уровень: " + MobsList[0].Level);
                Console.WriteLine("Здоровье: " + MobsList[0].Health);
                Console.WriteLine("Урон: " + MobsList[0].Damage);
                Console.WriteLine("Скорость: " + MobsList[0].Speed);
                Console.WriteLine("Защита: " + MobsList[0].Protect);
                Console.WriteLine("Урон по защите: " + MobsList[0].ProtectQuality);
                Console.WriteLine("Золото при победе: " + MobsList[0].GoldDrop);
                Console.WriteLine("Опыт за победу: " + MobsList[0].XPDrop);
                Console.WriteLine(" ");
                Console.WriteLine("Выберите противника");
                Console.WriteLine("1 - Атаковать");
                Console.WriteLine("0 - Убежать");
            }
            else
            {
                for (int id = 0; id < 3; id++)
                {
                    int id_mob = rnd.Next(0, 3);
                    RaceMob typeMob = (RaceMob)id_mob;
                    string type = typeMob.ToString();
                    FactoryMob(type, lvl_player, id);
                    if (id > 0)
                    {
                        Console.WriteLine("");
                    }
                    Console.WriteLine("Противник " + MobsList[id].Name);
                    Console.WriteLine("Характеристики");
                    Console.WriteLine("Уровень: " + MobsList[id].Level);
                    Console.WriteLine("Здоровье: " + MobsList[id].Health);
                    Console.WriteLine("Урон: " + MobsList[id].Damage);
                    Console.WriteLine("Скорость: " + MobsList[id].Speed);
                    Console.WriteLine("Защита: " + MobsList[id].Protect);
                    Console.WriteLine("Урон по защите: " + MobsList[id].ProtectQuality);
                    Console.WriteLine("Золото при победе: " + MobsList[id].GoldDrop);
                    Console.WriteLine("Опыт за победу: " + MobsList[id].XPDrop);
                }

                Console.WriteLine(" ");
                Console.WriteLine("Выберите противника");
                Console.WriteLine("1 - первый противник");
                Console.WriteLine("2 - второй противник");
                Console.WriteLine("3 - третий противник");
                Console.WriteLine("0 - Выход");
            }
            _boss = false;
            
        }
        public void FactoryMob(string? type, int lvl, int? id)
        {
            if (_boss)
            {
                Boss boss = new Boss();
                boss.Spawn(lvl, MobsList);
            }
            else
            {
                if (type == "zombie")
                {
                    Zombie zombie = new Zombie();
                    zombie.Spawn(lvl, MobsList);
                }
                else if (type == "skelet")
                {
                    Skelet skelet = new Skelet();
                    skelet.Spawn(lvl, MobsList);
                }
                else if (type == "wolf")
                {
                    Wolf wolf = new Wolf();
                    wolf.Spawn(lvl, MobsList);
                }
                else if (type == "slime")
                {
                    Slime slime = new Slime();
                    slime.Spawn(lvl, MobsList);
                }
                else
                {
                    Console.WriteLine("Возникла ошибка");
                }
            }
            
        }
    }



}
