using game.BaseClass;

namespace game.Enemy.Mobs
{
    public abstract class Mob : Specific
    {
        private protected RaceMob _race;
        public RaceMob Race => _race;
        private protected int _goldDrop;
        public int GoldDrop => _goldDrop;
        private protected int _xpDrop;
        public int XPDrop => _xpDrop;

        public enum RaceMob
        {
            zombie,
            skelet,
            wolf,
            slime,
            ork
        }

        public enum TypeMob
        {
            boss,
            semi_boss,
            simple
        }
    }
}
