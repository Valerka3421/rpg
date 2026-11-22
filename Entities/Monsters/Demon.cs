namespace Aethelgard.Entities.Monsters;

/// <summary>
/// Демон — поджигает цель (burning). Все данные вида перенесены из data/monsters.json
/// прямо в класс (архитектура «Пример»: данные живут в классе).
/// </summary>
public class Demon : Monster
{
    public const string Key = "demon";

    /// <summary>Данные вида до масштабирования (статический конструктор ниже).</summary>
    private static readonly MonsterData.Def _def;

    static Demon()
    {
        _def = new MonsterData.Def
        {
            Key = "demon",
            Name = "Демон",
            Ch = "D",
            Hp = 50,
            Atk = 12,
            Df = 4,
            Xp = 70,
            Color = new Microsoft.Xna.Framework.Color(200,70,70),
            MinDepth = 6,
            Ability = "burning",
            Boss = false,
            BossDepth = 0,
            OverworldOnly = false,
            Passive = false,
            Flees = false,
            Gold = true,
        };
    }

    public Demon()
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
