namespace Aethelgard.Entities.Monsters;

/// <summary>
/// Волк — хищник поверхности (bleeding). Все данные вида перенесены из data/monsters.json
/// прямо в класс (архитектура «Пример»: данные живут в классе).
/// </summary>
public class Wolf : Monster
{
    public const string Key = "wolf";

    /// <summary>Данные вида до масштабирования (статический конструктор ниже).</summary>
    private static readonly MonsterData.Def _def;

    static Wolf()
    {
        _def = new MonsterData.Def
        {
            Key = "wolf",
            Name = "Волк",
            Ch = "w",
            Hp = 14,
            Atk = 4,
            Df = 1,
            Xp = 12,
            Color = new Microsoft.Xna.Framework.Color(130,135,145),
            MinDepth = 1,
            Ability = "bleeding",
            Boss = false,
            BossDepth = 0,
            OverworldOnly = true,
            Passive = false,
            Flees = false,
            Gold = false,
        };
    }

    public Wolf()
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
