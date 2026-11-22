namespace Aethelgard.Entities.Monsters;

/// <summary>
/// Вампир — отравляет цель (poison), глубокие этажи. Все данные вида перенесены из data/monsters.json
/// прямо в класс (архитектура «Пример»: данные живут в классе).
/// </summary>
public class Vampire : Monster
{
    public const string Key = "vampire";

    /// <summary>Данные вида до масштабирования (статический конструктор ниже).</summary>
    private static readonly MonsterData.Def _def;

    static Vampire()
    {
        _def = new MonsterData.Def
        {
            Key = "vampire",
            Name = "Вампир",
            Ch = "v",
            Hp = 70,
            Atk = 14,
            Df = 5,
            Xp = 100,
            Color = new Microsoft.Xna.Framework.Color(180,50,80),
            MinDepth = 8,
            Ability = "poison",
            Boss = false,
            BossDepth = 0,
            OverworldOnly = false,
            Passive = false,
            Flees = false,
            Gold = true,
        };
    }

    public Vampire()
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
