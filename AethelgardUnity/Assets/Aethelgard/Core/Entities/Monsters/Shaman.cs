namespace Aethelgard.Entities.Monsters;

/// <summary>
/// Гоблин-шаман — лечит союзников (healer). Все данные вида перенесены из data/monsters.json
/// прямо в класс (архитектура «Пример»: данные живут в классе).
/// </summary>
public class Shaman : Monster
{
    public const string Key = "shaman";

    /// <summary>Данные вида до масштабирования (статический конструктор ниже).</summary>
    private static readonly MonsterData.Def _def;

    static Shaman()
    {
        _def = new MonsterData.Def
        {
            Key = "shaman",
            Name = "Гоблин-шаман",
            Ch = "S",
            Hp = 10,
            Atk = 2,
            Df = 1,
            Xp = 14,
            Color = new Microsoft.Xna.Framework.Color(180,140,220),
            MinDepth = 3,
            Ability = "healer",
            Boss = false,
            BossDepth = 0,
            OverworldOnly = false,
            Passive = false,
            Flees = false,
            Gold = true,
        };
    }

    public Shaman()
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
