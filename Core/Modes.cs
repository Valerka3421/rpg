using System;
using System.Collections.Generic;

namespace Aethelgard.Core;

/// <summary>Application screen modes — mirrors Python core/modes.py.</summary>
public static class ScreenMode
{
    public const string Menu = "menu";
    public const string MenuSaves = "menu_saves";
    public const string MenuSettings = "menu_settings";
    public const string MenuControls = "menu_controls";
    public const string MenuCredits = "menu_credits";
    public const string MenuMultiplayer = "menu_multiplayer";
    public const string MenuMpJoin = "menu_mp_join";
    public const string ClassSelect = "class_select";
    public const string Game = "game";
    public const string Inventory = "inventory";
    public const string Stats = "stats";
    public const string StatsRun = "stats_run";
    public const string Craft = "craft";
    public const string Skills = "skills";
    public const string Quests = "quests";
    public const string Shop = "shop";
    public const string Trade = "trade";
    public const string Dialogue = "dialogue";
    public const string Bigmap = "bigmap";
    public const string DevConsole = "dev_console";
    public const string Exit = "exit";

    public static readonly HashSet<string> All = new()
    {
        Menu, MenuSaves, MenuSettings, MenuControls, MenuCredits,
        MenuMultiplayer, MenuMpJoin, ClassSelect, Game, Inventory, Stats,
        StatsRun, Craft, Skills, Quests, Shop, Trade, Dialogue, Bigmap,
        DevConsole, Exit,
    };

    public static bool IsValid(string value) => All.Contains(value);
}
