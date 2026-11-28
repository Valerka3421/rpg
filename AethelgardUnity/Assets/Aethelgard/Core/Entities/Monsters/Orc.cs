namespace Aethelgard.Entities.Monsters;

/// <summary>
/// Орк — сильный монстр средних этажей. Все данные вида перенесены из data/monsters.json
/// прямо в класс (архитектура «Пример»: данные живут в классе).
/// </summary>
public class Orc : Monster
{
    public const string Key = "orc";

    /// <summary>Данные вида до масштабирования (статический конструктор ниже).</summary>
    private static readonly MonsterData.Def _def;

    static Orc()
    {
        _def = new MonsterData.Def
        {
            Key = "orc",
            Name = "Орк",
            Ch = "O",
            Hp = 20,
            Atk = 6,
            Df = 2,
            Xp = 22,
            Color = new Microsoft.Xna.Framework.Color(150,95,60),
            MinDepth = 3,
            Ability = null,
            Boss = false,
            BossDepth = 0,
            OverworldOnly = false,
            Passive = false,
            Flees = false,
            Gold = true,
        };
    }

    public Orc()
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
