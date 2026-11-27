namespace Aethelgard.Entities.Monsters;

/// <summary>
/// Крыса — обычный монстр подземелья (способность poison). Все данные вида перенесены из data/monsters.json
/// прямо в класс (архитектура «Пример»: данные живут в классе).
/// </summary>
public class Rat : Monster
{
    public const string Key = "rat";

    /// <summary>Данные вида до масштабирования (статический конструктор ниже).</summary>
    private static readonly MonsterData.Def _def;

    static Rat()
    {
        _def = new MonsterData.Def
        {
            Key = "rat",
            Name = "Крыса",
            Ch = "r",
            Hp = 3,
            Atk = 1,
            Df = 0,
            Xp = 3,
            Color = new Microsoft.Xna.Framework.Color(170,150,130),
            MinDepth = 1,
            Ability = "poison",
            Boss = false,
            BossDepth = 0,
            OverworldOnly = false,
            Passive = false,
            Flees = false,
            Gold = false,
        };
    }

    public Rat()
    {
    }

    /// <summary>Данные вида (перенесены из таблицы монстров в класс).</summary>
    public override MonsterData.Def Def => _def;

    public override void Spawn(int depth, List<Entity> list)
    {
        InitFromDef(_def, depth);
        list.Add(this);
    }
}
