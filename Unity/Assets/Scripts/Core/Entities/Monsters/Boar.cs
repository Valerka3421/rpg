namespace Aethelgard.Entities.Monsters;

/// <summary>
/// Кабан — животное поверхности. Все данные вида перенесены из data/monsters.json
/// прямо в класс (архитектура «Пример»: данные живут в классе).
/// </summary>
public class Boar : Monster
{
    public const string Key = "boar";

    /// <summary>Данные вида до масштабирования (статический конструктор ниже).</summary>
    private static readonly MonsterData.Def _def;

    static Boar()
    {
        _def = new MonsterData.Def
        {
            Key = "boar",
            Name = "Кабан",
            Ch = "p",
            Hp = 18,
            Atk = 5,
            Df = 1,
            Xp = 15,
            Color = new Microsoft.Xna.Framework.Color(120,100,80),
            MinDepth = 1,
            Ability = null,
            Boss = false,
            BossDepth = 0,
            OverworldOnly = true,
            Passive = false,
            Flees = false,
            Gold = false,
        };
    }

    public Boar()
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
