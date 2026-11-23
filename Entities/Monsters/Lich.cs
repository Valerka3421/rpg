namespace Aethelgard.Entities.Monsters;

/// <summary>
/// Лич — БОСС глубины 10. Все данные вида перенесены из data/monsters.json
/// прямо в класс (архитектура «Пример»: данные живут в классе).
/// </summary>
public class Lich : Monster
{
    public const string Key = "boss_l_10";

    /// <summary>Данные вида до масштабирования (статический конструктор ниже).</summary>
    private static readonly MonsterData.Def _def;

    static Lich()
    {
        _def = new MonsterData.Def
        {
            Key = "boss_l_10",
            Name = "Лич",
            Ch = "L",
            Hp = 150,
            Atk = 12,
            Df = 4,
            Xp = 500,
            Color = new Microsoft.Xna.Framework.Color(200,100,255),
            MinDepth = 10,
            Ability = null,
            Boss = true,
            BossDepth = 10,
            OverworldOnly = false,
            Passive = false,
            Flees = false,
            Gold = true,
        };
    }

    public Lich()
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
