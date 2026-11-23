using game.enemy;
using game.Enemy.Mobs;

namespace game.World
{
    internal class Game
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Добро пожаловать в игру!");
            Console.WriteLine("Нажмите Enter чтобы продолжить");
            Console.ReadLine();
            Console.Clear();

            Console.WriteLine("Напишите имя персонажа");
            string? name_player = Console.ReadLine();
            if (name_player == null || name_player == "")
            {
                name_player = "Player";
            }
            Console.Clear();
            Console.WriteLine("Выбирете класс персонажа");
            Console.WriteLine("1 - Воин");
            Console.WriteLine("2 - Маг");
            Console.WriteLine("3 - Лучник");
            Console.WriteLine("4 - Разбойник");
            int class_id;
            while (true)
            {
                bool Class = int.TryParse(Console.ReadLine(), out class_id);
                if (Class)
                {
                    if (class_id > 0 && class_id < 5)
                    {
                        break;
                    }
                    else
                    {
                        Console.WriteLine(" ");
                        Console.WriteLine("Не корректное значение");
                        continue;
                    }
                }
                else
                {
                    Console.WriteLine(" ");
                    Console.WriteLine("Не корректное значение");
                    continue;
                }
            }
            Console.Clear();
            Console.WriteLine("Персонаж создан");
            Player player = new Player(name_player, class_id - 1);

            while (true)
            {

                Console.WriteLine("Выберите действие");
                Console.WriteLine("1 - Характеристики");
                Console.WriteLine("2 - Инветарь");
                Console.WriteLine("3 - Достяжения");
                Console.WriteLine("4 - Битва");
                Console.WriteLine("5 - Магазин");
                Console.WriteLine("6 - Продать предметы");
                Console.WriteLine("0 - Выход");

                string? action = Console.ReadLine();

                if (action == "1")
                {

                    Console.Clear();
                    player.Specific();
                    continue;
                }
                else if (action == "2")
                {

                    Console.Clear();
                    player.Inventory();
                    continue;
                }
                else if (action == "3")
                {

                    Console.Clear();
                    player.Achievements();
                    continue;
                }
                else if (action == "4")
                {
                    Console.Clear();
                    Main mobs = new Main();
                    mobs.ListMobs(player.Level);
                    
                    while (true)
                    {
                        int id_mob;
                        bool action_second = int.TryParse(Console.ReadLine(), out id_mob);
                        if (action_second)
                        {
                            if (id_mob == 0)
                            {
                                Console.Clear();
                                break;
                            }
                            else if (id_mob > 0 && id_mob <= 3)
                            {
                                Console.Clear();
                                player.Fight(id_mob - 1, mobs);
                                break;
                            }
                            else
                            {
                                Console.WriteLine(" ");
                                Console.WriteLine("Не корректное значение");
                                continue;
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
                else if (action == "5")
                {
                    
                    Console.Clear();
                    Shop shop = new Shop();
                    shop.Shops(player);
                    continue;
                }
                else if (action == "6")
                {

                    Console.Clear();
                    player.Trade();
                    continue;
                }
                else if (action == "0")
                {
                    break;
                }
                else
                {
                    Console.WriteLine(" ");
                    Console.WriteLine("Не корректное значение");
                    continue;
                }
                Console.Clear();
            }

        }
    }
}