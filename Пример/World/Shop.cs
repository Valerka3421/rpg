using game.enemy;
using game.Prd;
using game.Structs;
using static game.Structs.ItemStruct;

namespace game.World
{
    public class Shop
    {
        public void Shops(Player player)
        {
            if (player.Level >= 3)
            {
                Console.WriteLine("Добро пожаловать в магазин");
                Console.WriteLine("Доступные товары:");
                Items items = new Items();
                items.ListItems(player.Level);
                int j = 1;
                for (int i = 0; i < items.items.Count; i++)
                {
                    if (i > 0)
                    {
                        Console.WriteLine("");
                    }
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine("----" + "Товар" + "--------------------------------" + j + "-----------------------------");
                    Console.ResetColor();
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine("Название предмета: " + items.items[i].Name);
                    Console.WriteLine("Предмет для класса: " + items.items[i].Class);
                    Console.WriteLine("Дополнительное здоровье: " + items.items[i].Health);
                    Console.WriteLine("Дополнительная скорость: " + items.items[i].Speed);
                    Console.WriteLine("Тип предмета: " + items.items[i].Type);
                    Console.WriteLine("Размер предмета: " + items.items[i].Size);
                    Console.WriteLine("Урон предмета: " + items.items[i].Damage);
                    Console.WriteLine("Защита предмета: " + items.items[i].Protect);
                    Console.WriteLine("Качество защиты предмета: " + items.items[i].ProtectQuality);
                    Console.WriteLine("Цена предмета: " + items.items[i].Price);
                    Console.ResetColor();
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine("------------------------------------------------------------------------");
                    Console.ResetColor();
                    j++;
                }
                Console.WriteLine("Вы желаете что-нибудь купить? (да, нет)");
                while (true)
                {
                    string? buy = Console.ReadLine();
                    if (buy == null || buy == "" || buy == "нет") 
                    {
                        Console.WriteLine("Удачной дороги");
                        Thread.Sleep(1000);
                        Console.Clear();
                        break;
                    }
                    else if (buy == "да")
                    {
                        while (true)
                        {
                            Console.WriteLine("Какой товар вы желаете купить? ( Номер товара )");
                            string? product = Console.ReadLine();
                            if(player.Level < 10)
                            {
                                if (product == "1")
                                {
                                    Trade(0, items, player);
                                    break;
                                }
                                else if(product == "2")
                                {
                                    Trade(1, items, player);
                                    break;
                                }
                                else if (product == "3")
                                {
                                    Trade(2, items, player);
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
                                if (product == "1")
                                {
                                    Trade(0, items, player);
                                    break;
                                }
                                else if (product == "2")
                                {
                                    Trade(1, items, player);
                                    break;
                                }
                                else if (product == "3")
                                {
                                    Trade(2, items, player);
                                    break;
                                }
                                else if (product == "4")
                                {
                                    Trade(3, items, player);
                                    break;

                                }
                                else if (product == "5")
                                {
                                    Trade(4, items, player);
                                    break;
                                }
                                else
                                {
                                    Console.WriteLine(" ");
                                    Console.WriteLine("Не корректное значение");
                                    continue;
                                }
                            }
                        }
                        Console.Clear();
                        break;
                        
                    }
                    else 
                    {
                        Console.WriteLine(" ");
                        Console.WriteLine("Не корректное значение");
                        continue;
                    }
                }
                
            }
            else
            {
                Console.WriteLine("Магазин доступен с 3 уровня");
                Console.WriteLine("К сожалению, ваш уровень меньше 3");
                Console.WriteLine("Ваш уровень: " + player.Level);
                Console.WriteLine("Нажмите Enter чтобы продолжить");
                Console.ReadLine();
                Console.Clear();
            }
        }

        public void Trade(int id, Items items, Player player)
        {
            int m = 0;
            int l = 0;
            int s = 0;
            if(player.PlayerClass == items.items[id].Class)
            {
                if (player.Gold - items.items[id].Price > 0)
                {
                    foreach (var item in player.InventoryPlayer)
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
                    if (m >= 3 && items.items[id].Size == SizeItem.Medium)
                    {
                        Console.WriteLine("Достигнуто максимальное количество предметов размера Medium");
                        Console.WriteLine("Покупка не возможна");
                        Thread.Sleep(2000);
                    }
                    else
                    {
                        if (items.items[id].Size == SizeItem.Medium)
                        {
                            player.Gold = items.items[id].Price;
                            player.AddInventory(items.items[id]);
                            Console.WriteLine("Вы успешно купили предмет " + items.items[id].Name);
                            Thread.Sleep(1000);
                        }
                    }

                    if (s >= 3 && items.items[id].Size == SizeItem.Small)
                    {
                        Console.WriteLine("Достигнуто максимальное количество предметов размера Small");
                        Console.WriteLine("Покупка не возможна");
                        Thread.Sleep(2000);
                    }
                    else
                    {
                        if (items.items[id].Size == SizeItem.Small)
                        {
                            player.Gold = items.items[id].Price;
                            player.AddInventory(items.items[id]);
                            Console.WriteLine("Вы успешно купили предмет " + items.items[id].Name);
                            Thread.Sleep(1000);
                        }
                    }

                    if (l >= 3 && items.items[id].Size == SizeItem.Large)
                    {
                        Console.WriteLine("Достигнуто максимальное количество предметов размера Large");
                        Console.WriteLine("Покупка не возможна");
                        Thread.Sleep(2000);
                    }
                    else
                    {
                        if (items.items[id].Size == SizeItem.Large)
                        {
                            player.Gold = items.items[id].Price;
                            player.AddInventory(items.items[id]);
                            Console.WriteLine("Вы успешно купили предмет " + items.items[id].Name);
                            Thread.Sleep(1000);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("У вас недостаточно золота");
                    Thread.Sleep(1000);
                }
            }
            else
            {
                Console.WriteLine("Вы не можете купить предмет не своего класса");
                Thread.Sleep(1000);
            }
            

        }
    }
}
