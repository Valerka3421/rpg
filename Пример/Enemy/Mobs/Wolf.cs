using game.Prd;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static game.Enemy.Mobs.Mob;

namespace game.Enemy.Mobs
{
    public class Wolf : Mob
    {
        public Wolf()
        {
            _name = "Волк";
        }
        private Wolf(int level, double health, double damage, double speed, double protect, double protectQuality, double missChance, RaceMob race, int goldDrop, int xpDrop)
        {
            _name = "Волк";
            _level = level;
            _health = health;
            _damage = damage;
            _speed = speed;
            _protect = protect;
            _protectQuality = protectQuality;
            _missChance = missChance;
            _race = race;
            _goldDrop = goldDrop;
            _xpDrop = xpDrop;
        }
        public void Spawn(int lvl, List<Mob> MobsList)
        {
            Random rnd = new Random();

            Items items = new Items();
            items.ListItems(lvl);
            int id = rnd.Next(0, items.items.Count);

            RaceMob race = RaceMob.wolf;
            int level = rnd.Next(lvl - 1, lvl + 1);
            double health = 10 + 5 * level;
            double damage = 3 + 3 * level;
            double speed = 1 + 1 * level;
            double protect = 0.5 + 0.5 * level;
            double protectQuality = 0.01 + 0.01 * level;
            int xp = 5 + 5 * level;

            if (rnd.Next(0, 100) < 20)
            {
                health += items.items[id].Health;
                damage += items.items[id].Damage;
                speed += items.items[id].Speed;
                protect += items.items[id].Protect;
                protectQuality += items.items[id].ProtectQuality;
                xp += 10;
            }


            health = Math.Round(health, 2);
            damage = Math.Round(damage, 2);
            speed = Math.Round(speed, 2);
            protect = Math.Round(protect, 2);
            protectQuality = Math.Round(protectQuality, 3);

            if (protectQuality > 1)
            {
                protectQuality = 1;
            }
            int gold = 10 + 5 * level;
            double miss = 20 - 5 * level;
            if (miss < 15)
            {
                miss = 15;
            }

            Wolf wolf = new Wolf(level, health, damage, speed, protect, protectQuality, miss, race, gold, xp);
            MobsList.Add(wolf);
        }
    }
}
