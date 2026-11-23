using Microsoft.Xna.Framework;

namespace Aethelgard.Entities;

/// <summary>
/// Универсальная сущность мира: монстр, NPC или заготовка игрока.
/// Наследует Specific (BaseClass) — статы и статусы живут там.
/// Сохраняет конструктор оригинала для совместимости с World/WorldGen.
/// </summary>
public class Entity : Specific
{
    /// <summary>Ключ монстра в таблице данных (например "rat", "boss_goblin_king").</summary>
    public string MonsterKey = "";

    /// <summary>Базовые статы на момент спавна — для корректного
    /// масштабирования под размер отряда без накопления множителей.</summary>
    public int BaseMaxHp { get; set; }
    public int BaseAtk { get; set; }

    // Поля охранников города
    public int HomeX, HomeY;
    public float GuardTimer;

    // Флаги NPC
    public bool IsMerchant;
    public bool IsNpc;
    public string? NpcType;

    // Поведенческие флаги монстров (python: passive/flees/ranged/healer)
    public bool Passive;
    public bool Flees;
    public bool Ranged;
    public bool Healer;

    public Entity() { }

    public Entity(int x, int y, int hp, int maxHp, int atk, int df,
                  string name, char ch, Color color, int xpValue = 0,
                  bool boss = false, string? ability = null)
    {
        X = x; Y = y;
        Hp = hp; MaxHp = maxHp;
        BaseMaxHp = maxHp;
        Atk = atk; Df = df;
        BaseAtk = atk;
        Name = name; Ch = ch; Color = color;
        XpValue = xpValue;
        Boss = boss;
        Ability = ability;
    }
}
