namespace Aethelgard.Entities.Monsters;

/// <summary>
/// Гоблин — рядовой монстр подземелья. Все данные вида перенесены из data/monsters.json
/// прямо в класс (архитектура «Пример»: данные живут в классе).
/// </summary>
public class Goblin : Monster
{
    public const string Key = "goblin";

    /// <summary>Данные вида до масштабирования (статический конструктор ниже).</summary>
    private static readonly MonsterData.Def _def;

    static Goblin()
    {
        _def = new MonsterData.Def
        {
            Key = "goblin",
            Name = "Гоблин",
            Ch = "g",
            Hp = 8,
            Atk = 3,
            Df = 1,
            Xp = 6,
            Color = new Microsoft.Xna.Framework.Color(120,190,100),
            MinDepth = 1,
            Ability = null,
            Boss = false,
            BossDepth = 0,
            OverworldOnly = false,
            Passive = false,
            Flees = false,
            Gold = true,
        };
    }

    public Goblin()
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
