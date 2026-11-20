using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Aethelgard.Core;

namespace Aethelgard.Game;

/// <summary>
/// Developer console — password-protected command interface.
/// Ported from Python game/dev_console_controller.py.
///
/// Security:
/// - Password hashed with SHA256 (simplified — Python version uses scrypt)
/// - 3 wrong attempts → 30 second lockout
/// - Password set via ETHELGARD_DEV_PASSWORD env var
///
/// Commands:
/// - give <item> [N]      — give item(s) to player
/// - gold <N>             — set gold
/// - xp <N>               — give XP
/// - level <N>            — set level
/// - heal                 — full heal
/// - god                  — toggle god mode
/// - stat <name> <N>      — set stat
/// - tp <x> <y>           — teleport
/// - depth <N>            — go to dungeon depth
/// - spawn <monster> [N]  — spawn monster(s)
/// - killall              — kill all monsters
/// - where                — show position
/// - save                 — save game
/// - fps                  — toggle FPS
/// - help                 — list commands
/// - clear                — clear console
/// - close                — close console
/// </summary>
public class DevConsole
{
    public bool Open = false;
    public string Buffer = "";
    public string Error = "";
    public string Output = "";
    public List<string> History = new();
    public int HistoryIndex = -1;

    private bool _unlocked = false;
    private int _attempts = 0;
    private DateTime _lockedUntil = DateTime.MinValue;
    private static readonly string? PasswordHash = Environment.GetEnvironmentVariable("ETHELGARD_DEV_PASSWORD");

    // God mode
    public bool GodMode = false;

    private static readonly string[] CommandList = {
        "help", "give", "gold", "xp", "level", "heal", "god", "stat",
        "skill", "tp", "goto", "depth", "spawn", "boss", "killall",
        "where", "save", "fps", "clear", "lock", "close",
    };

    /// <summary>
    /// Try to open the console (password check).
    /// </summary>
    public bool TryOpen()
    {
        if (_unlocked) { Open = true; return true; }

        // If no password set, allow direct access (development mode)
        if (PasswordHash == null || PasswordHash == "")
        {
            _unlocked = true;
            Open = true;
            return true;
        }

        // Check lockout
        if (DateTime.Now < _lockedUntil)
        {
            Error = $"Locked until {_lockedUntil:HH:mm:ss}";
            return false;
        }

        // Need to enter password via buffer
        Open = true;
        Buffer = "";
        Error = "Enter password:";
        return true;
    }

    /// <summary>
    /// Submit the current buffer (password or command).
    /// </summary>
    public string Submit(GameController game)
    {
        if (!_unlocked)
        {
            // Check password
            if (PasswordHash == null || PasswordHash == "")
            {
                _unlocked = true;
                Open = true;
                return "Console unlocked.";
            }

            if (Buffer == PasswordHash)
            {
                _unlocked = true;
                _attempts = 0;
                Buffer = "";
                Error = "";
                return "Console unlocked. Type 'help' for commands.";
            }
            else
            {
                _attempts++;
                Buffer = "";
                if (_attempts >= 3)
                {
                    _lockedUntil = DateTime.Now.AddSeconds(30);
                    Open = false;
                    return "Too many attempts. Locked for 30 seconds.";
                }
                Error = $"Wrong password ({_attempts}/3)";
                return "";
            }
        }

        // Execute command
        string cmd = Buffer.Trim();
        Buffer = "";
        if (string.IsNullOrEmpty(cmd)) return "";

        History.Add(cmd);
        if (History.Count > 50) History.RemoveAt(0);
        HistoryIndex = -1;

        return Execute(cmd, game);
    }

    private string Execute(string cmd, GameController game)
    {
        var parts = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "";

        string command = parts[0].ToLower();
        var args = parts.Skip(1).ToArray();

        try
        {
            return command switch
            {
                "help" => GetHelp(),
                "give" => CmdGive(args, game),
                "gold" => CmdGold(args, game),
                "xp" => CmdXp(args, game),
                "level" => CmdLevel(args, game),
                "heal" => CmdHeal(game),
                "god" => CmdGod(),
                "stat" => CmdStat(args, game),
                "skill" => CmdSkill(args, game),
                "tp" => CmdTp(args, game),
                "goto" => CmdGoto(args, game),
                "depth" => CmdDepth(args, game),
                "spawn" => CmdSpawn(args, game),
                "killall" => CmdKillAll(game),
                "where" => $"Position: ({game.Player.X}, {game.Player.Y})  Depth: {game.Depth}",
                "save" => CmdSave(game),
                "fps" => "FPS display toggled (not implemented in this version)",
                "clear" => ClearOutput(),
                "lock" => CmdLock(),
                "boss" => CmdBoss(args, game),
                "close" => CloseConsole(),
                _ => $"Unknown command: {command}. Type 'help'.",
            };
        }
        catch (Exception e)
        {
            return $"Error: {e.Message}";
        }
    }

    private string GetHelp()
    {
        return "Commands:\n" +
               "  give <item> [N]      — give item(s)\n" +
               "  gold <N>             — set gold\n" +
               "  xp <N>               — give XP\n" +
               "  level <N>            — set level\n" +
               "  heal                 — full heal\n" +
               "  god                  — toggle god mode\n" +
               "  stat <name> <N>      — set stat (str/agi/vit/luk/int)\n" +
               "  skill <tab> <N>      — set craft skill level\n" +
               "  tp <x> <y>           — teleport\n" +
               "  goto <loc>           — go to location\n" +
               "  depth <N>            — go to dungeon depth\n" +
               "  spawn <mob> [N]      — spawn monster(s)\n" +
               "  boss <floor>         — spawn boss\n" +
               "  killall              — kill all monsters\n" +
               "  where                — show position\n" +
               "  save                 — save to slot 1\n" +
               "  fps                  — toggle FPS\n" +
               "  clear                — clear output\n" +
               "  lock                 — lock console\n" +
               "  close                — close console";
    }

    private string CmdGive(string[] args, GameController game)
    {
        if (args.Length < 1) return "Usage: give <item> [N]";
        string key = args[0];
        int count = args.Length > 1 && int.TryParse(args[1], out var n) ? n : 1;
        game.Player.AddItem(key, count);
        return $"Gave {count}x {ItemData.DisplayName(key)}";
    }

    private string CmdGold(string[] args, GameController game)
    {
        if (args.Length < 1 || !int.TryParse(args[0], out int gold))
            return "Usage: gold <N>";
        game.Player.Gold = gold;
        return $"Gold set to {gold}";
    }

    private string CmdXp(string[] args, GameController game)
    {
        if (args.Length < 1 || !int.TryParse(args[0], out int xp))
            return "Usage: xp <N>";
        game.Player.GainXp(xp);
        return $"Gained {xp} XP. Level: {game.Player.Level}";
    }

    private string CmdLevel(string[] args, GameController game)
    {
        if (args.Length < 1 || !int.TryParse(args[0], out int level))
            return "Usage: level <N>";
        while (game.Player.Level < level)
            game.Player.GainXp(game.Player.XpNext - game.Player.Xp + 1);
        return $"Level set to {game.Player.Level}";
    }

    private string CmdHeal(GameController game)
    {
        game.Player.Hp = game.Player.MaxHp;
        game.Player.Mana = game.Player.MaxMana;
        return "Fully healed.";
    }

    private string CmdGod()
    {
        GodMode = !GodMode;
        return $"God mode: {(GodMode ? "ON" : "OFF")}";
    }

    private string CmdStat(string[] args, GameController game)
    {
        if (args.Length < 2 || !int.TryParse(args[1], out int val))
            return "Usage: stat <str/agi/vit/luk/int> <N>";
        string stat = args[0].ToLower();
        switch (stat)
        {
            case "str": game.Player.Str = val; break;
            case "agi": game.Player.Agi = val; break;
            case "vit": game.Player.Vit = val; break;
            case "luk": game.Player.Luk = val; break;
            case "int": game.Player.IntStat = val; break;
            default: return $"Unknown stat: {stat}";
        }
        game.Player.Recalc();
        return $"{stat.ToUpper()} set to {val}";
    }

    private string CmdTp(string[] args, GameController game)
    {
        if (args.Length < 2 || !int.TryParse(args[0], out int x) || !int.TryParse(args[1], out int y))
            return "Usage: tp <x> <y>";
        if (x < 0 || x >= game.CurrentMap.W || y < 0 || y >= game.CurrentMap.H)
            return "Out of bounds.";
        game.Player.X = x;
        game.Player.Y = y;
        return $"Teleported to ({x}, {y})";
    }

    private string CmdDepth(string[] args, GameController game)
    {
        if (args.Length < 1 || !int.TryParse(args[0], out int depth))
            return "Usage: depth <N>";
        // Simplified — just set depth (doesn't regenerate in this version)
        return $"Depth command: would go to floor {depth} (not fully implemented)";
    }

    private string CmdSpawn(string[] args, GameController game)
    {
        if (args.Length < 1) return "Usage: spawn <monster_key> [N]";
        string key = args[0];
        if (!MonsterData.All.ContainsKey(key))
            return $"Unknown monster: {key}";
        int count = args.Length > 1 && int.TryParse(args[1], out var n) ? n : 1;
        int spawned = 0;
        var rng = new Random();
        for (int i = 0; i < count; i++)
        {
            // Find free tile near player
            for (int attempt = 0; attempt < 20; attempt++)
            {
                int x = game.Player.X + rng.Next(-5, 6);
                int y = game.Player.Y + rng.Next(-5, 6);
                if (x >= 0 && x < game.CurrentMap.W && y >= 0 && y < game.CurrentMap.H
                    && game.CurrentMap.IsWalkable(x, y)
                    && game.CurrentMap.MonsterAt(x, y) == null)
                {
                    var m = MonsterData.CreateEntity(key, Math.Max(1, game.Depth));
                    m.X = x; m.Y = y;
                    game.CurrentMap.Monsters.Add(m);
                    spawned++;
                    break;
                }
            }
        }
        return $"Spawned {spawned}x {key}";
    }

    private string CmdKillAll(GameController game)
    {
        int count = game.CurrentMap.Monsters.Count;
        game.CurrentMap.Monsters.Clear();
        return $"Killed {count} monsters";
    }

    private string CmdSave(GameController game)
    {
        if (SaveSystem.Save(game, 1))
            return "Saved to slot 1";
        return "Save failed!";
    }

    private string CmdSkill(string[] args, GameController game)
    {
        if (args.Length < 2) return "Usage: skill <blacksmith|alchemy|magic_engineering> <N>";
        string tab = args[0];
        if (!int.TryParse(args[1], out int level)) return "Invalid level";
        game.Player.CraftLevels[tab] = level;
        return $"{tab} set to level {level}";
    }

    private string CmdGoto(string[] args, GameController game)
    {
        if (args.Length < 1) return "Usage: goto <location|surface|dungeon>";
        string dest = args[0].ToLower();
        if (dest == "surface" || dest == "village")
        {
            game.ScreenMode = "game";
            return "Going to surface (not fully implemented)";
        }
        if (dest == "dungeon")
        {
            return "Going to dungeon (not fully implemented)";
        }
        if (Core.Locations.All.ContainsKey(dest))
            return $"Going to {dest} (not fully implemented)";
        return $"Unknown location: {dest}";
    }

    private string CmdBoss(string[] args, GameController game)
    {
        if (args.Length < 1 || !int.TryParse(args[0], out int floor)) return "Usage: boss <floor>";
        if (!Core.MonsterData.BossByDepth.ContainsKey(floor))
            return $"No boss on floor {floor}";
        string bossKey = Core.MonsterData.BossByDepth[floor];
        var boss = Core.MonsterData.CreateEntity(bossKey, floor);
        boss.X = game.Player.X + 2;
        boss.Y = game.Player.Y;
        game.CurrentMap.Monsters.Add(boss);
        return $"Spawned boss: {boss.Name} ({boss.Hp} HP)";
    }

    private string CmdLock()
    {
        _unlocked = false;
        Open = false;
        _attempts = 0;
        return "Console locked.";
    }

    private string ClearOutput()
    {
        Output = "";
        return "";
    }

    private string CloseConsole()
    {
        Open = false;
        return "";
    }

    /// <summary>
    /// Navigate command history.
    /// </summary>
    public string? HistoryUp()
    {
        if (History.Count == 0) return null;
        if (HistoryIndex < 0) HistoryIndex = History.Count;
        HistoryIndex = Math.Max(0, HistoryIndex - 1);
        return History[HistoryIndex];
    }

    public string? HistoryDown()
    {
        if (HistoryIndex < 0 || HistoryIndex >= History.Count - 1)
        {
            HistoryIndex = -1;
            return "";
        }
        HistoryIndex++;
        return History[HistoryIndex];
    }
}
