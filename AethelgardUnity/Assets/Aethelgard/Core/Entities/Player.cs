using Microsoft.Xna.Framework;

namespace Aethelgard.Entities;

/// <summary>
/// Игровой персонаж: класс, характеристики, инвентарь, экипировка, опыт.
/// Наследует Entity → Specific. Логика статов вынесена в
/// Player.ComputeStats (общая с предпросмотром выбора класса).
/// Дописаны отсутствовавшие в оригинале члены: TickStatuses, UseItem,
/// CraftLevels/CraftXp (сохранения и крафт-ветки).
/// </summary>
public class Player : Entity
{
    // ── Класс персонажа ──
    public string ClassKey = "default";
    public string ClassName = "Приключенец";
    public int ClassTier = 1;
    public string ClassBase = "default";
    public int ClassChanges;
    public int Level = 1;
    public int Xp = 0;
    public int XpNext = Config.XpToNext(1);
    public int FreePoints = 0;
    public int Gold = 0;

    // ── Базовые характеристики ──
    public int Str = 5, Agi = 5, Vit = 5, Luk = 5, DfStat = 5, IntStat = 5;

    // ── Производные статы (пересчитываются в Recalc) ──
    public int MaxMana;
    public int Mana;
    public float DodgeChance;
    public float CritChance;
    public float MoveSpeed = 1.0f;

    // Внутренние бонусы от экипировки/навыков
    public float WCritBonus, WDodgeBonus, ADodgeBonus, SkillDodge, SkillCrit;

    // ── Экипировка ──
    public string? Weapon, Armor, Head, Legs, Boots;

    // Совместимость со старыми именами слотов
    public string? Helmet { get => Head; set => Head = value; }
    public string? Leggings { get => Legs; set => Legs = value; }

    // Инвентарь: ключ предмета → количество
    public Dictionary<string, int> Inventory = new();

    // Перк-флаги
    public bool DoubleStrike, Regen, Vampire, Thorns, Greedy;
    public bool GodModeStat;

    // ── Прогресс навыков ──
    public int SkillPoints = 1;
    public int StepCounter = 0;
    public Dictionary<string, RecipeData.SkillProgress> Skills = new();
    public HashSet<string> LearnedNodes = new();
    public HashSet<string> Blueprints = new();

    // Крафт-ветки (уровень и опыт по каждой)
    public Dictionary<string, int> CraftLevels = new();
    public Dictionary<string, int> CraftXp = new();

    // Квикбар и активные навыки
    public string?[] Quickbar = new string?[Config.QuickbarSlots];
    public Dictionary<string, float> ActiveSkillCd = new();

    // Кулдаун движения
    public float MoveTimer = 0f;

    public Player(string classKey = "default")
    {
        if (!ClassData.All.ContainsKey(classKey)) classKey = "default";
        var cls = ClassData.Data(classKey);
        var stats = ClassData.ClassStats(classKey);
        ClassKey = classKey;
        ClassName = cls.Name;
        ClassTier = cls.Tier;
        ClassBase = string.IsNullOrEmpty(cls.Base) ? classKey : cls.Base;
        ClassChanges = Math.Max(0, ClassTier - 1);
        Str = stats.GetValueOrDefault("str", 5);
        Agi = stats.GetValueOrDefault("agi", 5);
        Vit = stats.GetValueOrDefault("vit", 5);
        Luk = stats.GetValueOrDefault("luk", 5);
        DfStat = stats.GetValueOrDefault("df_stat", 5);
        IntStat = stats.GetValueOrDefault("int", 5);
        Weapon = cls.StartWeapon;
        Armor = cls.StartArmor;
        foreach (var (k, v) in cls.StartItems)
            Inventory[k] = Inventory.GetValueOrDefault(k) + v;
        Skills = RecipeData.DefaultSkills();
        Recalc();
        Hp = MaxHp;
        Mana = MaxMana;
    }

    // ── Расчёт статов ──
    public class StatResult
    {
        public int MaxHp, Atk, Df, MaxMana;
        public float Dodge, Crit;
        public float WCritBonus, WDodgeBonus, ADodgeBonus, SkillDodge, SkillCrit;
    }

    /// <summary>Единая формула для Recalc и предпросмотра класса.</summary>
    public static StatResult ComputeStats(
        int str, int agi, int vit, int luk, int dfStat, int intStat, int level,
        string? weapon, string? armor, string? head, string? legs, string? boots,
        IReadOnlyDictionary<string, double> effects)
    {
        // Плоские бонусы к характеристикам от узлов/класса/статусов.
        // Раньше они мутировали Str/Agi/... навсегда при изучении узла —
        // теперь статы вычисляемые, а снятие узла корректно откатывает бонус.
        str += (int)effects.GetValueOrDefault("str");
        agi += (int)effects.GetValueOrDefault("agi");
        vit += (int)effects.GetValueOrDefault("vit");
        luk += (int)effects.GetValueOrDefault("luk");
        dfStat += (int)effects.GetValueOrDefault("df_stat");
        intStat += (int)effects.GetValueOrDefault("int");

        int wb = 0, ab = 0, hpExtra = 0;
        float wCrit = 0, wDodge = 0, aDodge = 0;
        int wDf = 0, manaExtra = 0;

        void Slot(string slot, string? key)
        {
            if (string.IsNullOrEmpty(key)) return;
            var (baseKey, lvl) = ItemData.ParseItemKey(key);
            var it = ItemData.Get(baseKey);
            if (it == null) return;
            int value = it.IntValue + lvl;
            if (slot == "weapon")
            {
                wb = value;
                wCrit = (float)it.CritBonus;
                wDodge = (float)it.DodgeBonus;
                wDf = it.DfBonus;
            }
            else
            {
                ab += value;
                aDodge += (float)it.DodgeBonus;
            }
            hpExtra += it.HpBonus;
            manaExtra += it.ManaBonus;
        }

        Slot("weapon", weapon);
        Slot("armor", armor);
        Slot("head", head);
        Slot("legs", legs);
        Slot("boots", boots);

        float skillDodge = (float)effects.GetValueOrDefault("dodge");
        float skillCrit = (float)effects.GetValueOrDefault("crit");

        float dodgeBase = Math.Min(Config.DodgeCap, agi * 0.015f);
        float dodgeTotal = Math.Max(0f, Math.Min(Config.DodgeTotalCap,
            dodgeBase + wDodge + aDodge + skillDodge));
        float critBase = Math.Min(Config.CritBaseCap, luk * 0.02f);
        float critTotal = Math.Min(Config.CritTotalCap, critBase + wCrit + skillCrit);

        return new StatResult
        {
            MaxHp = 15 + vit * 2 + hpExtra + (int)effects.GetValueOrDefault("hp"),
            Atk = 2 + str / 2 + wb + (int)effects.GetValueOrDefault("atk"),
            Df = dfStat / 2 + ab + wDf + (int)effects.GetValueOrDefault("df"),
            MaxMana = 10 + level * 2 + intStat * 2 + manaExtra + (int)effects.GetValueOrDefault("mana"),
            Dodge = dodgeTotal,
            Crit = critTotal,
            WCritBonus = wCrit,
            WDodgeBonus = wDodge,
            ADodgeBonus = aDodge,
            SkillDodge = skillDodge,
            SkillCrit = skillCrit,
        };
    }

    /// <summary>Сумма пассивных бонусов узлов навыков, класса и статус-эффектов.</summary>
    public Dictionary<string, double> Effects()
    {
        if (_effectsCache != null && _effectsStatusVersion == StatusVersion)
            return _effectsCache;

        var totals = SkillTree.Aggregate(this);

        // Пассивные эффекты класса (lifesteal, hp, quest_gold, craft_xp, ...)
        // раньше вообще не применялись — данные лежали мёртвым грузом.
        foreach (var (key, value) in ClassData.Data(ClassKey).Effects)
            totals[key] = totals.GetValueOrDefault(key) + value;

        foreach (var status in Statuses.Keys)
        {
            if (!StatusData.Mods.TryGetValue(status, out var mods)) continue;
            foreach (var (key, value) in mods)
                totals[key] = totals.GetValueOrDefault(key) + value;
        }

        _effectsCache = totals;
        _effectsStatusVersion = StatusVersion;
        return totals;
    }

    public double Effect(string name, double def = 0) =>
        Effects().GetValueOrDefault(name, def);

    public double Lifesteal => Effect("lifesteal");
    public double GoldPct => Effect("gold_pct");
    public double XpPct => Effect("xp_pct");
    public double QuestGoldPct => Effect("quest_gold");
    public double QuestXpPct => Effect("quest_xp");
    public int VisionBonus => (int)Effect("fov");

    // Итоговые характеристики = база + плоские бонусы узлов/класса/статусов.
    public int TotalStr => Str + (int)Effect("str");
    public int TotalAgi => Agi + (int)Effect("agi");
    public int TotalVit => Vit + (int)Effect("vit");
    public int TotalLuk => Luk + (int)Effect("luk");
    public int TotalDfStat => DfStat + (int)Effect("df_stat");
    public int TotalInt => IntStat + (int)Effect("int");

    // Кэш эффектов: пересобирается при смене узлов/класса/статусов,
    // а не на каждый вызов из UI/боя.
    private Dictionary<string, double>? _effectsCache;
    private int _effectsStatusVersion = -1;

    public void InvalidateEffects() => _effectsCache = null;

    public void Recalc()
    {
        int oldMax = MaxHp;
        int oldMana = MaxMana;
        InvalidateEffects();
        var effects = Effects();
        var stats = ComputeStats(Str, Agi, Vit, Luk, DfStat, IntStat, Level,
            Weapon, Armor, Head, Legs, Boots, effects);
        MaxHp = stats.MaxHp;
        MaxMana = stats.MaxMana;
        Atk = stats.Atk;
        Df = stats.Df;
        WCritBonus = stats.WCritBonus;
        WDodgeBonus = stats.WDodgeBonus;
        ADodgeBonus = stats.ADodgeBonus;
        SkillDodge = stats.SkillDodge;
        SkillCrit = stats.SkillCrit;
        DodgeChance = stats.Dodge;
        CritChance = stats.Crit;
        if (MaxHp > oldMax) Hp += MaxHp - oldMax;
        if (MaxMana > oldMana) Mana += MaxMana - oldMana;
        Hp = Math.Min(Hp, MaxHp);
        Mana = Math.Min(Mana, MaxMana);
    }

    public float Dodge => DodgeChance;

    public float Crit => CritChance;

    public float AttackCooldown
    {
        get
        {
            double speed = Math.Max(0, Effect("attack_speed"));
            double frenzy = Math.Max(0, Effect("frenzy"));
            if (frenzy > 0)
                speed += frenzy * (1.0 - Hp / (double)Math.Max(1, MaxHp));
            return (float)(Config.PlayerAttackCooldown / (1.0 + speed));
        }
    }

    public float MoveCooldown
    {
        get
        {
            double speed = Math.Max(0, Effect("move_speed"));
            return (float)(Config.PlayerMoveCooldown / (1.0 + speed));
        }
    }

    public double RageBonus
    {
        get
        {
            double rage = Math.Max(0, Effect("rage"));
            if (rage > 0 && Hp < MaxHp * 0.5) return 1.0 + rage;
            return 1.0;
        }
    }

    public bool HasCompanion => (int)Effect("summon") != 0;

    public List<ClassData.ActiveSkill> ActiveSkills() =>
        ClassData.Data(ClassKey).ActiveSkills;

    public bool AssignQuickbar(int slotIdx, string? itemKey)
    {
        if (slotIdx < 0 || slotIdx >= Config.QuickbarSlots) return false;
        Quickbar[slotIdx] = itemKey;
        return true;
    }

    public bool ClearQuickbarSlot(int slotIdx)
    {
        if (slotIdx < 0 || slotIdx >= Config.QuickbarSlots) return false;
        Quickbar[slotIdx] = null;
        return true;
    }

    // ── Инвентарь ──
    public void AddItem(string key, int count = 1) =>
        Inventory[key] = Inventory.GetValueOrDefault(key) + count;

    public bool RemoveItem(string key, int count = 1)
    {
        if (!Inventory.TryGetValue(key, out int have) || have < count) return false;
        have -= count;
        if (have <= 0) Inventory.Remove(key);
        else Inventory[key] = have;
        return true;
    }

    public int ItemCount(string key) => Inventory.GetValueOrDefault(key);

    /// <summary>
    /// Список (ключ, количество) с учётом лимита стака — parity
    /// core/rules.py:inventory_stacks. Снаряжение показывается по 1,
    /// остальное разбивается на стаки не больше Config.MaxStack.
    /// </summary>
    public List<(string Key, int Count)> InventoryStacks()
    {
        var entries = new List<(string, int)>();
        foreach (var key in Inventory.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            int count = Inventory.GetValueOrDefault(key);
            if (count <= 0) continue;
            var def = ItemData.Get(ItemData.ParseItemKey(key).Base);
            if (def == null || !ItemData.IsStackable(def))
            {
                for (int i = 0; i < count; i++) entries.Add((key, 1));
                continue;
            }
            while (count > Config.MaxStack)
            {
                entries.Add((key, Config.MaxStack));
                count -= Config.MaxStack;
            }
            if (count > 0) entries.Add((key, count));
        }
        return entries;
    }

    public bool EquipItem(string key)
    {
        var def = ItemData.Get(key);
        if (def == null) return false;
        string? slot = def.Kind switch
        {
            ItemData.ItemType.Weapon => "weapon",
            ItemData.ItemType.Armor => "armor",
            ItemData.ItemType.Helmet => "head",
            ItemData.ItemType.Leggings => "legs",
            ItemData.ItemType.Boots => "boots",
            _ => null,
        };
        if (slot == null) return false;
        var (ok, _) = ClassData.CanUse(ClassKey, key);
        if (!ok) return false;

        string? current = slot switch
        {
            "weapon" => Weapon, "armor" => Armor, "head" => Head,
            "legs" => Legs, "boots" => Boots, _ => null,
        };
        if (current != null) AddItem(current, 1);
        switch (slot)
        {
            case "weapon": Weapon = key; break;
            case "armor": Armor = key; break;
            case "head": Head = key; break;
            case "legs": Legs = key; break;
            case "boots": Boots = key; break;
        }
        RemoveItem(key, 1);
        Recalc();
        return true;
    }

    public bool UnequipSlot(string slot)
    {
        string? current = slot switch
        {
            "weapon" => Weapon, "armor" => Armor, "head" => Head,
            "legs" => Legs, "boots" => Boots, _ => null,
        };
        if (current == null) return false;
        AddItem(current, 1);
        switch (slot)
        {
            case "weapon": Weapon = null; break;
            case "armor": Armor = null; break;
            case "head": Head = null; break;
            case "legs": Legs = null; break;
            case "boots": Boots = null; break;
        }
        Recalc();
        return true;
    }

    /// <summary>
    /// Использовать предмет из инвентаря (зелье, еда, книга и т.д.).
    /// В оригинале Player.UseItem вызывался GameController и Net/GameCore,
    /// но не был объявлен — теперь реализован по типам предметов.
    /// </summary>
    public bool UseItem(string fullKey) => UseItem(fullKey, null);

    /// <summary>
    /// Использовать предмет из инвентаря (зелье, еда, книга и т.д.).
    /// Свитки/руны требуют ману (scroll: 5–12, руны: 5–10) — списываются только
    /// если эффект сработал; при переданной карте применяют AOE-эффекты
    /// (огненный шар 3×3, заморозка — радиус 5) через Rules.CastMagic.
    /// Чертёж открывает особый крафт (в оригинале type: blueprint).
    /// </summary>
    public bool UseItem(string fullKey, World.Dungeon? map)
    {
        var (baseKey, _) = ItemData.ParseItemKey(fullKey);
        var def = ItemData.Get(baseKey);
        if (def == null) return false;
        if (def.Kind is ItemData.ItemType.Key
                     or ItemData.ItemType.Resource
                     or ItemData.ItemType.Material
                     or ItemData.ItemType.Gold)
            return false; // эти типы «использовать» нельзя

        // Оружие/броня — экипировка: предмет НЕ расходуется (раньше здесь
        // был баг: RemoveItem выполнялся до switch, и шмотка уничтожалась)
        if (def.Kind is ItemData.ItemType.Weapon or ItemData.ItemType.Armor
                     or ItemData.ItemType.Helmet or ItemData.ItemType.Leggings
                     or ItemData.ItemType.Boots)
            return EquipItem(baseKey ?? fullKey);

        // Чертёж — не расходуется, открывает рецепт
        if (def.Kind == ItemData.ItemType.Blueprint)
        {
            string learns = !string.IsNullOrEmpty(def.Teaches)
                ? def.Teaches
                : (baseKey ?? "").Replace("blueprint_", "");
            if (Blueprints.Contains(learns)) return false;
            Blueprints.Add(learns);
            return true;
        }

        // Свитки/руны — через Rules.CastMagic (мана + AOE + статусы)
        if (def.Kind is ItemData.ItemType.Scroll or ItemData.ItemType.Rune)
        {
            if (map != null)
            {
                var cast = Rules.CastMagic(this, map, baseKey ?? fullKey);
                if (!cast.Ok) return false;
                RemoveItem(fullKey, 1);
                return true;
            }
            // без карты — только статусные эффекты с проверкой маны
            if (Mana < def.ManaCost) return false;
            if (string.IsNullOrEmpty(def.Status) && string.IsNullOrEmpty(def.Effect)
                && def.Heal <= 0) return false;
        }

        if (!RemoveItem(fullKey, 1) && !RemoveItem(baseKey ?? fullKey, 1)) return false;

        switch (def.Kind)
        {
            case ItemData.ItemType.Heal:
                if (def.Heal > 0 || def.Value > 0)
                    Hp = Math.Min(MaxHp, Hp + Math.Max(def.Heal, def.IntValue));
                break;

            case ItemData.ItemType.Mana:
                Mana = Math.Min(MaxMana, Mana + Math.Max(def.Mana, def.IntValue));
                break;

            case ItemData.ItemType.Book:
                // Книга повышает характеристику из def.Stat на def.Value
                if (!string.IsNullOrEmpty(def.Stat))
                    ApplyStat(def.Stat, Math.Max(1, def.IntValue));
                // Книга перка включает флаг (book_double/regen/vampire/thorns/greedy)
                else if (!string.IsNullOrEmpty(baseKey))
                {
                    switch (baseKey)
                    {
                        case "book_double": DoubleStrike = true; break;
                        case "book_regen": Regen = true; break;
                        case "book_vampire": Vampire = true; break;
                        case "book_thorns": Thorns = true; break;
                        case "book_greedy": Greedy = true; break;
                    }
                }
                Recalc();
                break;

            case ItemData.ItemType.Buff:
                if (!string.IsNullOrEmpty(def.Status)) AddStatus(def.Status);
                else if (!string.IsNullOrEmpty(def.Effect)) AddStatus(def.Effect);
                if (def.Heal > 0) Hp = Math.Min(MaxHp, Hp + def.Heal);
                if (def.Mana > 0) Mana = Math.Min(MaxMana, Mana + def.Mana);
                break;

            case ItemData.ItemType.Poison:
                // Отравление — статус действует и на игрока (риск при использовании)
                AddStatus(def.Status ?? "poisoned");
                if (def.Value > 0)
                    Hp = Math.Min(MaxHp, Hp + def.IntValue); // слабое зелье-противоядие
                break;

            case ItemData.ItemType.Food:
                if (def.Heal > 0) Hp = Math.Min(MaxHp, Hp + def.Heal);
                else Hp = Math.Min(MaxHp, Hp + Math.Max(1, def.IntValue));
                // компот восстанавливает и ману
                if (!string.IsNullOrEmpty(baseKey) && baseKey.Contains("compote"))
                    Mana = Math.Min(MaxMana, Mana + 6);
                break;

            case ItemData.ItemType.Scroll:
            case ItemData.ItemType.Rune:
                Mana -= Math.Max(0, def.ManaCost);
                if (!string.IsNullOrEmpty(def.Status)) AddStatus(def.Status);
                if (!string.IsNullOrEmpty(def.Effect)) AddStatus(def.Effect);
                if (def.Heal > 0) Hp = Math.Min(MaxHp, Hp + def.Heal);
                break;
        }

        if (Hp <= 0) Hp = 1; // использование предмета не убивает игрока
        return true;
    }

    private void ApplyStat(string stat, int value)
    {
        switch (stat)
        {
            case "str": Str += value; break;
            case "agi": Agi += value; break;
            case "vit": Vit += value; break;
            case "luk": Luk += value; break;
            case "int": IntStat += value; break;
            case "df_stat": DfStat += value; break;
            case "df": DfStat += value; break;
        }
    }

    // ── Опыт и уровни ──
    public void GainXp(int amount)
    {
        if (amount <= 0) return;
        Xp += (int)(amount * (1.0 + XpPct));
        int guard = 0;
        while (Xp >= XpNext && guard++ < Config.XpLevelGuard)
        {
            Xp -= XpNext;
            Level++;
            XpNext = Config.XpToNext(Level);
            FreePoints += 5;
            SkillPoints += 1;
            Recalc();
            Hp = MaxHp;
            Mana = MaxMana;
        }
    }
}
