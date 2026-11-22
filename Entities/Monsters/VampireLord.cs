namespace Aethelgard.Entities.Monsters;

/// <summary>
/// Владыка вампиров — БОСС глубины 15. Все данные вида перенесены из data/monsters.json
/// прямо в класс (архитектура «Пример»: данные живут в классе).
/// </summary>
public class VampireLord : Monster
{
    public const string Key = "boss_v_15";

    /// <summary>Данные вида до масштабирования (статический конструктор ниже).</summary>
    private static readonly MonsterData.Def _def;

    static VampireLord()
    {
        _def = new MonsterData.Def
        {
            Key = "boss_v_15",
            Name = "Владыка вампиров",
            Ch = "V",
            Hp = 250,
            Atk = 18,
            Df = 6,
            Xp = 700,
            Color = new Microsoft.Xna.Framework.Color(200,40,80),
            MinDepth = 15,
            Ability = null,
            Boss = true,
            BossDepth = 15,
            OverworldOnly = false,
            Passive = false,
            Flees = false,
            Gold = true,
        };
    }

    public VampireLord()
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
