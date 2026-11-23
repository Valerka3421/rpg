using static game.enemy.Player;

namespace game.Structs
{
    public class ItemStruct
    {
        public struct Item
        {
            public string Name;
            public double Health;
            public double Damage;
            public double Speed;
            public double Protect;
            public double ProtectQuality;
            public SizeItem Size;
            public TypeItem Type;
            public ClassPlayer Class;
            public int Price;
        }

        public enum SizeItem
        {
            Small,
            Medium,
            Large
        }

        public enum TypeItem
        {
            Weapons,
            Armor,
            Accessories
        }

    }
}
