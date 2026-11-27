using System;
using System.Collections.Generic;

namespace Aethelgard.Systems;

/// <summary>
/// Контракт мира, достаточный для сохранения/загрузки (SaveSystem.Save/
/// ApplyLoad). Вводится, чтобы SaveSystem жил в ядре и компилировался и
/// MonoGame-, и Unity-сборкой: GameController (движковый слой, свой в
/// каждом движке) реализует интерфейс неявно, менять его не нужно.
/// </summary>
public interface ISaveableGame
{
    Player Player { get; }
    string WorldMode { get; }
    int Depth { get; }
    Dungeon CurrentMap { get; }
    Dungeon Overworld { get; set; }
    QuestLog Quests { get; }
    DateTime RunStart { get; set; }
    HashSet<string> VisitedLocations { get; }

    Dictionary<int, Dungeon> GetFloorCache();
    Dictionary<string, Dungeon> GetLocationCache();
    void SetFloorCache(int depth, Dungeon floor, string? locationKey);
    void SetWorldMode(string mode, int depth, string? locationKey = null);
    void MarkVisited(string key);
}
