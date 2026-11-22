using Microsoft.Xna.Framework;

namespace Aethelgard.Entities;

/// <summary>
/// Житель города: стражник или торговец. Фабрика строит существо
/// из NpcDef таблицы CityData (в стиле фабрики Spawn из проекта «Пример»).
/// </summary>
public class Npc : Entity
{
    private Npc() { }

    /// <summary>Создать NPC из описания в таблице города.</summary>
    public static Npc Spawn(CityData.NpcDef def, int x, int y)
    {
        var npc = new Npc
        {
            X = x,
            Y = y,
            Name = def.Name,
            Ch = def.Ch.Length > 0 ? def.Ch[0] : '?',
            Color = def.Color,
            Hp = 1,
            MaxHp = 1,
            Atk = 0,
            Df = 0,
            IsNpc = true,
            NpcType = def.Key,
            IsMerchant = def.IsShop,
            Immortal = def.IsGuard,
        };
        return npc;
    }

    /// <summary>Доска заданий (B) и старейшина (E) — особые NPC города.</summary>
    public static Npc SpawnSpecial(string npcType, string name, char ch,
                                   Color color, int x, int y)
    {
        return new Npc
        {
            X = x,
            Y = y,
            Name = name,
            Ch = ch,
            Color = color,
            Hp = 1,
            MaxHp = 1,
            IsNpc = true,
            NpcType = npcType,
        };
    }
}
