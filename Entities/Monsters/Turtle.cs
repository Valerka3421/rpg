namespace Aethelgard.Entities.Monsters;

/// <summary>
/// Черепаха — медленное панцирное животное пляжа. Все данные вида перенесены из data/monsters.json
/// прямо в класс (архитектура «Пример»: данные живут в классе).
/// </summary>
public class Turtle : Monster
{
    public const string Key = "turtle";

    /// <summary>Данные вида до масштабирования (статический конструктор ниже).</summary>
    private static readonly MonsterData.Def _def;

    static Turtle()
    {
        _def = new MonsterData.Def
        {
            Key = "turtle",
            Name = "Черепаха",
            Ch = "u",
            Hp = 16,
            Atk = 3,
            Df = 3,
            Xp = 12,
            Color = new Microsoft.Xna.Framework.Color(120,160,110),
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

    public Turtle()
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
