using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
namespace Aethelgard.Systems;

/// <summary>
/// Журнал событий с вкладками (parity журналa событий python): «Бой»,
/// «Предметы», «Общение», «Опыт» и общий «Всё». Вкладки работают
/// асинхронно — одно событие может попасть в несколько вкладок
/// (например, выпавший из моба предмет и в «Бой», и в «Предметы»).
/// Каждая вкладка хранит последние 200 сообщений.
///
/// Доступ из правил боя/предметов — через <see cref="Instance"/>
/// (правила статичны и не знают о контроллере).
/// </summary>
public class GameChat
{
    public enum Tab { Combat = 0, Items = 1, Social = 2, Xp = 3 }

    public const int MaxMessages = 200;

    public sealed record Entry(string Text, Color Color, DateTime Time);

    private readonly List<Entry>[] _tabs = { new(), new(), new(), new() };
    private readonly List<Entry> _all = new();
    // Хост-игра пишет в журнал и из серверного потока (Rules/MonsterAI в
    // UpdateLoop), пока UI-поток читает списки в рендере — нужен общий лок
    private readonly object _lock = new();

    /// <summary>Глобальный журнал (правила боя/предметов пишут сюда).</summary>
    public static GameChat Instance { get; set; } = new();

    public IReadOnlyList<Entry> All => _all;
    public IReadOnlyList<Entry> For(Tab tab) => _tabs[(int)tab];

    // ── Потокобезопасное чтение для рендера ──
    // Живые списки мутируются под _lock из серверного потока; рендер получает
    // кэшированные копии, обновляемые только при смене версии (без аллокаций
    // в кадрах без новых сообщений).
    public int Version { get; private set; }
    private List<Entry>? _allCache; private int _allCacheVer = -1;
    private readonly List<Entry>?[] _tabCache = new List<Entry>?[4];
    private readonly int[] _tabCacheVer = { -1, -1, -1, -1 };

    public IReadOnlyList<Entry> AllSafe
    {
        get
        {
            lock (_lock)
            {
                if (_allCacheVer != Version) { _allCache = _all.ToList(); _allCacheVer = Version; }
                return _allCache!;
            }
        }
    }

    public IReadOnlyList<Entry> TabSafe(Tab tab)
    {
        lock (_lock)
        {
            int i = (int)tab;
            if (_tabCacheVer[i] != Version) { _tabCache[i] = _tabs[i].ToList(); _tabCacheVer[i] = Version; }
            return _tabCache[i]!;
        }
    }

    /// <summary>
    /// Добавить сообщение: в общий журнал «Всё» и в перечисленные вкладки.
    /// </summary>
    public void Add(string text, Color color,
        bool combat = false, bool items = false, bool social = false, bool xp = false)
    {
        var e = new Entry(text, color, DateTime.Now);
        lock (_lock)
        {
            if (combat) Push(_tabs[(int)Tab.Combat], e);
            if (items) Push(_tabs[(int)Tab.Items], e);
            if (social) Push(_tabs[(int)Tab.Social], e);
            if (xp) Push(_tabs[(int)Tab.Xp], e);
            Push(_all, e);
        }
    }

    // ── Хелперы для типовых событий ──

    public void Combat(string text) => Add(text, new Color(230, 200, 170), combat: true);
    public void CombatDim(string text) => Add(text, new Color(160, 150, 135), combat: true);
    public void Item(string text) => Add(text, new Color(214, 206, 192), items: true);
    public void Loot(string text) =>
        Add(text, new Color(206, 166, 82), combat: true, items: true);
    public void Social(string text) => Add(text, new Color(214, 206, 192), social: true);
    public void Xp(string text) => Add(text, new Color(150, 200, 130), xp: true);
    public void Level(string text) =>
        Add(text, new Color(206, 166, 82), xp: true, combat: false);

    private static void Push(List<Entry> list, Entry e)
    {
        list.Add(e);
        if (list.Count > MaxMessages)
            list.RemoveRange(0, list.Count - MaxMessages);
        Instance.Version++;
    }
}
