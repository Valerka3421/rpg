using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static game.enemy.Player;

namespace game.BaseClass
{
    public class Specific
    {
        private protected string _name;
        public string Name => _name;
        private protected int _level;
        public int Level => _level;
        private protected double _health;
        public double Health => _health;
        private protected double _damage;
        public double Damage => _damage;
        private protected double _speed;
        public double Speed => _speed;
        private protected double _protect;
        public double Protect => _protect;
        private protected double _protectQuality;
        public double ProtectQuality => _protectQuality;
        private protected double _missChance;
        public double MissChance => _missChance;
    }
}
