namespace Aethelgard.Entities.Monsters;

/// <summary>
/// Гоблин-лучник — стреляет с расстояния (ranged). Все данные вида перенесены из data/monsters.json
/// прямо в класс (архитектура «Пример»: данные живут в классе).
/// </summary>
public class Archer : Monster
{
    public const string Key = "archer";

    /// <summary>Данные вида до масштабирования (статический конструктор ниже).</summary>
    private static readonly MonsterData.Def _def;

    static Archer()
    {
        _def = new MonsterData.Def
        {
            Key = "archer",
            Name = "Гоблин-лучник",
            Ch = "a",
            Hp = 6,
            Atk = 2,
            Df = 0,
            Xp = 7,
            Color = new Microsoft.Xna.Framework.Color(150,200,100),
            MinDepth = 2,
            Ability = "ranged",
            Boss = false,
            BossDepth = 0,
            OverworldOnly = false,
            Passive = false,
            Flees = false,
            Gold = true,
        };
    }

    public Archer()
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
