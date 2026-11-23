namespace Aethelgard.Entities.Monsters;

/// <summary>
/// Древний дракон — БОСС глубины 20, финал игры. Все данные вида перенесены из data/monsters.json
/// прямо в класс (архитектура «Пример»: данные живут в классе).
/// </summary>
public class AncientDragon : Monster
{
    public const string Key = "boss_a_20";

    /// <summary>Данные вида до масштабирования (статический конструктор ниже).</summary>
    private static readonly MonsterData.Def _def;

    static AncientDragon()
    {
        _def = new MonsterData.Def
        {
            Key = "boss_a_20",
            Name = "Древний дракон",
            Ch = "A",
            Hp = 400,
            Atk = 22,
            Df = 8,
            Xp = 1200,
            Color = new Microsoft.Xna.Framework.Color(240,130,40),
            MinDepth = 20,
            Ability = null,
            Boss = true,
            BossDepth = 20,
            OverworldOnly = false,
            Passive = false,
            Flees = false,
            Gold = true,
        };
    }

    public AncientDragon()
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
