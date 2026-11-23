using game.Prd;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static game.Enemy.Mobs.Mob;

namespace game.Enemy.Mobs
{
    internal class Boss : Mob
    {
        public Boss()
        {
            _name = "Босс 'Орк Стукать'";
        }
        private Boss(int level, double health, double damage, double speed, double protect, double protectQuality, double missChance, RaceMob race, int goldDrop, int xpDrop)
        {
            _name = "Босс 'Орк Стукать'";
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

            RaceMob race = RaceMob.ork;
            int level = lvl + 5;
            double health = 10 + 5 * level;
            double damage = 6 + 4 * level;
            double speed = 3 + 1 * level;
            double protect = 5 + 3 * level;
            double protectQuality = 0.2 + 0.1 * level;
            int xp = 50 + 10 * level;

            
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
            double miss = 30 - 5 * level;
            if (miss < 5)
            {
                miss = 5;
            }

            Boss boss = new Boss(level, health, damage, speed, protect, protectQuality, miss, race, gold, xp);
            MobsList.Add(boss);
        }
    }
}
