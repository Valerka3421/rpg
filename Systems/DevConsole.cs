using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
namespace Aethelgard.Systems;

/// <summary>
/// Developer console — password-protected command interface.
/// Ported from Python game/dev_console_controller.py.
///
/// Security:
/// - Password stored as PBKDF2-HMAC-SHA256 hash (соль + 210 000 итераций)
/// - Сравнение constant-time, поэтому по времени ответа нельзя подобрать пароль
/// - 3 wrong attempts → 30 second lockout
/// - ETHELGARD_DEV_PASSWORD_HASH — готовый хеш (рекомендуется)
/// - ETHELGARD_DEV_PASSWORD — legacy-пароль, хешируется в памяти при старте
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
    private long _lockedUntilTicks = -1;   // Stopwatch-тики (не зависят от перевода часов)
    private static readonly string? PasswordHash = LoadPasswordHash();

    /// <summary>
    /// Загружает хеш пароля из окружения. Готовый хеш (ETHELGARD_DEV_PASSWORD_HASH)
    /// используется как есть, legacy-пароль (ETHELGARD_DEV_PASSWORD) хешируется
    /// в памяти — открытый текст не хранится и не сравнивается.
    /// </summary>
    private static string? LoadPasswordHash()
    {
        string? stored = Environment.GetEnvironmentVariable("ETHELGARD_DEV_PASSWORD_HASH");
        if (!string.IsNullOrEmpty(stored)) return stored;

        string? legacy = Environment.GetEnvironmentVariable("ETHELGARD_DEV_PASSWORD");
        if (!string.IsNullOrEmpty(legacy)) return PasswordHasher.Hash(legacy);

        return null;
    }

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

        // Python: консоль ОТКЛЮЧЕНА, пока не задан ETHELGARD_DEV_PASSWORD.
        if (PasswordHash == null || PasswordHash == "")
        {
            Error = "Консоль отключена: задайте ETHELGARD_DEV_PASSWORD";
            return false;
        }

        // Check lockout
        if (Stopwatch.GetTimestamp() < _lockedUntilTicks)
        {
            double secs = (_lockedUntilTicks - Stopwatch.GetTimestamp())
                / (double)Stopwatch.Frequency;
            Error = $"Консоль заблокирована ещё {secs:F0} с";
            return false;
        }

        // Need to enter password via buffer
        Open = true;
        Buffer = "";
        Error = "Введите пароль:";
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
                Open = false;
                return "Консоль отключена: задайте ETHELGARD_DEV_PASSWORD";
            }

            if (PasswordHasher.Verify(Buffer, PasswordHash))
            {
                _unlocked = true;
                _attempts = 0;
                Buffer = "";
                Error = "";
                return "Консоль разблокирована. Введите 'help' для списка команд.";
            }
            else
            {
                _attempts++;
                Buffer = "";
                if (_attempts >= 3)
                {
                    _lockedUntilTicks = Stopwatch.GetTimestamp() + (long)(30 * Stopwatch.Frequency);
                    Open = false;
                    return "Слишком много попыток. Блокировка на 30 секунд.";
                }
                Error = $"Неверный пароль ({_attempts}/3)";
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
                "god" => CmdGod(game),
                "stat" => CmdStat(args, game),
                "skill" => CmdSkill(args, game),
                "tp" => CmdTp(args, game),
                "goto" => CmdGoto(args, game),
                "depth" => CmdDepth(args, game),
                "spawn" => CmdSpawn(args, game),
                "killall" => CmdKillAll(game),
                "where" => $"Position: ({game.Player.X}, {game.Player.Y})  Depth: {game.Depth}",
                "save" => CmdSave(game),
                "fps" => CmdFps(game),
                "clear" => ClearOutput(),
                "lock" => CmdLock(),
                "boss" => CmdBoss(args, game),
                "close" => CloseConsole(),
                _ => $"Неизвестная команда: {command}. Введите 'help'.",
            };
        }
        catch (Exception e)
        {
            return $"Ошибка: {e.Message}";
        }
    }

    private string GetHelp()
    {
        return "Команды:\n" +
               "  give <предмет> [N]   — выдать предмет(ы)\n" +
               "  gold <N>             — установить золото\n" +
               "  xp <N>               — дать опыт\n" +
               "  level <N>            — установить уровень\n" +
               "  heal                 — полное лечение\n" +
               "  god                  — режим бога вкл/выкл\n" +
               "  stat <имя> <N>       — установить статы (str/agi/vit/luk/int)\n" +
               "  skill <ветка> <N>    — уровень ветки создания\n" +
               "  tp <x> <y>           — телепорт\n" +
               "  goto <локация>       — перейти в локацию\n" +
               "  depth <N>            — перейти на этаж подземелья\n" +
               "  spawn <моб> [N]      — создать монстра(ов)\n" +
               "  boss <этаж>          — создать босса\n" +
               "  killall              — убить всех монстров\n" +
               "  where                — показать позицию\n" +
               "  save                 — сохранить в текущий слот\n" +
               "  fps                  — показ FPS вкл/выкл\n" +
               "  clear                — очистить вывод\n" +
               "  lock                 — заблокировать консоль\n" +
               "  close                — закрыть консоль";
    }

    private string CmdGive(string[] args, GameController game)
    {
        if (args.Length < 1) return "Использование: give <предмет> [N]";
        string key = args[0];
        int count = args.Length > 1 && int.TryParse(args[1], out var n) ? n : 1;
        game.Player.AddItem(key, count);
        return $"Выдано: {ItemData.DisplayName(key)} ×{count}";
    }

    private string CmdGold(string[] args, GameController game)
    {
        if (args.Length < 1 || !int.TryParse(args[0], out int gold))
            return "Использование: gold <N>  (прибавляет N, можно отрицательное)";
        // Python: gold <N> прибавляет N (в т.ч. отрицательное).
        game.Player.Gold = Math.Max(0, game.Player.Gold + gold);
        return $"Золото: {game.Player.Gold} (+{gold})";
    }

    private string CmdXp(string[] args, GameController game)
    {
        if (args.Length < 1 || !int.TryParse(args[0], out int xp))
            return "Использование: xp <N>";
        game.Player.GainXp(xp);
        return $"Получено {xp} опыта. Уровень: {game.Player.Level}";
    }

    private string CmdLevel(string[] args, GameController game)
    {
        if (args.Length < 1 || !int.TryParse(args[0], out int level))
            return "Использование: level <N>";
        while (game.Player.Level < level)
            game.Player.GainXp(game.Player.XpNext - game.Player.Xp + 1);
        return $"Уровень установлен: {game.Player.Level}";
    }

    private string CmdHeal(GameController game)
    {
        game.Player.Hp = game.Player.MaxHp;
        game.Player.Mana = game.Player.MaxMana;
        return "Полное лечение выполнено.";
    }

    private string CmdGod(GameController game)
    {
        GodMode = !GodMode;
        // Флаг должен проверяться боевыми правилами, иначе команда ничего
        // не делает: Player.GodModeStat читает Rules.ResolveAttack.
        game.Player.GodModeStat = GodMode;
        return $"Режим бога: {(GodMode ? "ВКЛ" : "ВЫКЛ")}";
    }

    private string CmdStat(string[] args, GameController game)
    {
        if (args.Length < 2 || !int.TryParse(args[1], out int val))
            return "Использование: stat <str/agi/vit/luk/int> <N>";
        string stat = args[0].ToLower();
        switch (stat)
        {
            case "str": game.Player.Str = val; break;
            case "agi": game.Player.Agi = val; break;
            case "vit": game.Player.Vit = val; break;
            case "luk": game.Player.Luk = val; break;
            case "int": game.Player.IntStat = val; break;
            default: return $"Неизвестная характеристика: {stat}";
        }
        game.Player.Recalc();
        return $"{stat.ToUpper()} установлено: {val}";
    }

    private string CmdTp(string[] args, GameController game)
    {
        if (args.Length < 2 || !int.TryParse(args[0], out int x) || !int.TryParse(args[1], out int y))
            return "Использование: tp <x> <y>";
        if (x < 0 || x >= game.CurrentMap.W || y < 0 || y >= game.CurrentMap.H)
            return "Вне границ карты.";
        game.Player.X = x;
        game.Player.Y = y;
        return $"Телепорт в ({x}, {y})";
    }

    private string CmdDepth(string[] args, GameController game)
    {
        if (args.Length < 1 || !int.TryParse(args[0], out int depth))
            return "Использование: depth <N>";
        game.ScreenMode = "game";
        game.GotoDungeonFloor(depth);
        return $"Перешли на этаж {game.Depth}";
    }

    private string CmdSpawn(string[] args, GameController game)
    {
        if (args.Length < 1) return "Использование: spawn <ключ_монстра> [N]";
        string key = args[0];
        if (!MonsterData.All.ContainsKey(key))
            return $"Неизвестный монстр: {key}";
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
        return $"Создано: {key} ×{spawned}";
    }

    private string CmdKillAll(GameController game)
    {
        int count = game.CurrentMap.Monsters.Count;
        game.CurrentMap.Monsters.Clear();
        return $"Убито монстров: {count}";
    }

    private string CmdSave(GameController game)
    {
        if (SaveSystem.Save(game, game.CurrentSlot))
            return $"Сохранено в слот {game.CurrentSlot}";
        return "Ошибка сохранения!";
    }

    private string CmdSkill(string[] args, GameController game)
    {
        if (args.Length < 2) return "Использование: skill <blacksmith|alchemy|magic_engineering> <N>";
        string tab = args[0];
        if (!int.TryParse(args[1], out int level)) return "Неверный уровень";
        game.Player.CraftLevels[tab] = level;
        return $"{tab}: уровень {level}";
    }

    private string CmdGoto(string[] args, GameController game)
    {
        if (args.Length < 1) return "Использование: goto <локация|surface|dungeon>";
        string dest = args[0].ToLower();
        game.ScreenMode = "game";
        if (dest == "surface" || dest == "village")
        {
            game.GotoSurface();
            return "Вы поднялись на поверхность";
        }
        if (dest == "dungeon")
        {
            game.GotoDungeonFloor(1);
            return $"Вы вошли в подземелье, этаж {game.Depth}";
        }
        if (Aethelgard.World.Locations.All.ContainsKey(dest))
            return game.GotoLocation(dest)
                ? $"Вы перешли в локацию: {dest}"
                : $"Не удалось перейти в {dest}";
        return $"Неизвестная локация: {dest}";
    }

    /// <summary>fps — переключить показ FPS (settings.show_fps).</summary>
    private string CmdFps(GameController game)
    {
        game.ShowFps = !game.ShowFps;
        return game.ShowFps ? "Показ FPS: ВКЛ" : "Показ FPS: ВЫКЛ";
    }

    private string CmdBoss(string[] args, GameController game)
    {
        if (args.Length < 1 || !int.TryParse(args[0], out int floor)) return "Использование: boss <этаж>";
        if (!Aethelgard.Data.MonsterData.BossByDepth.ContainsKey(floor))
            return $"На этаже {floor} нет босса";
        string bossKey = Aethelgard.Data.MonsterData.BossByDepth[floor];
        var boss = Aethelgard.Data.MonsterData.CreateEntity(bossKey, floor);
        boss.X = game.Player.X + 2;
        boss.Y = game.Player.Y;
        game.CurrentMap.Monsters.Add(boss);
        return $"Создан босс: {boss.Name} ({boss.Hp} HP)";
    }

    private string CmdLock()
    {
        _unlocked = false;
        Open = false;
        _attempts = 0;
        return "Консоль заблокирована.";
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
