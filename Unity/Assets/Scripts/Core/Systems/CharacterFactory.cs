namespace Aethelgard.Systems;

/// <summary>
/// Единая инициализация игрока для синглплеера и MP-сервера.
///
/// Раньше <see cref="GameController.NewGame"/> и <see cref="GameCore.AddPlayer"/>
/// почти построчно повторяли друг друга: бонусы класса, пересчёт статов,
/// стартовые предметы и автоэкипировка. Теперь это живёт здесь.
/// </summary>
public static class CharacterFactory
{
    /// <summary>Создать персонажа выбранного класса со стартовым набором.</summary>
    public static Player Create(string classKey) =>
        ApplyClass(new Player(classKey), classKey);

    /// <summary>
    /// Применить к уже созданному персонажу стартовую автоэкипировку.
    /// Бонусы класса НЕ начисляются здесь: конструктор <see cref="Player"/>
    /// получает полные статы через ClassData.ClassStats (bonus уже учтён) —
    /// раньше ApplyClass добавлял их второй раз. Стартовые предметы тоже
    /// добавляет конструктор.
    /// </summary>
    public static Player ApplyClass(Player player, string classKey)
    {
        var def = ClassData.Get(classKey);
        if (def == null) return player;

        player.ClassName = def.Name;
        player.ClassTier = def.Tier;
        player.Recalc();
        player.Hp = player.MaxHp;
        player.Mana = player.MaxMana;

        // Автоэкипировка первого оружия/брони/шлема из стартового набора.
        foreach (var (itemKey, _) in def.StartItems)
        {
            var itemDef = ItemData.Get(itemKey);
            if (itemDef != null &&
                (itemDef.Kind == ItemData.ItemType.Weapon
                 || itemDef.Kind == ItemData.ItemType.Armor
                 || itemDef.Kind == ItemData.ItemType.Helmet))
            {
                player.EquipItem(itemKey);
            }
        }

        return player;
    }
}
