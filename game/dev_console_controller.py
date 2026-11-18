"""Консоль разработчика с паролем.

Открывается по F12 или ` (обратный апостроф) в одиночной игре. До ввода
пароля доступны только ввод пароля и Esc. После разблокировки принимает
команды отладки: выдача предметов/золота/опыта, телепорт, спавн мобов,
переход по локациям и этажам, режим бессмертия и т. п.

Модуль не рисует: UI-слой берёт состояние из ``DevConsoleController``
(см. ``render/screens_renderer.draw_dev_console``).
"""
import difflib
import hashlib
import hmac
import random
import time

import pygame

from core import config, rules, skills
from core.config import Tile
from core.data import (ITEMS, MONSTERS, BOSSES, parse_item_key, make_loot_key,
                       item_display_name)
from core.entities import Entity


COMMANDS = {
    'help': 'список команд',
    'give': 'give <ключ|имя> [N] — выдать предмет',
    'gold': 'gold <N> — изменить золото (можно отрицательное)',
    'xp': 'xp <N> — начислить опыт',
    'level': 'level <N> — поднять уровень до N',
    'heal': 'heal — полное HP/MP и снятие статусов',
    'god': 'god — бессмертие вкл/выкл',
    'stat': 'stat <str|agi|vit|luk|df_stat> <N> — задать характеристику',
    'skill': 'skill <blacksmith|alchemy|magic_engineering> <N> — уровень навыка',
    'tp': 'tp <x> <y> — телепорт в координаты текущей карты',
    'goto': 'goto <локация|surface|dungeon> — переход',
    'depth': 'depth <N> — перейти на этаж подземелья',
    'spawn': 'spawn <моб> [N] — создать мобов рядом',
    'boss': 'boss <этаж> — создать босса этого этажа',
    'killall': 'killall — убрать всех мобов с карты',
    'where': 'where — координаты и состояние',
    'save': 'save — сохранить игру',
    'fps': 'fps — счётчик FPS вкл/выкл',
    'clear': 'clear — очистить вывод',
    'lock': 'lock — снова запереть консоль',
    'close': 'close — закрыть консоль',
}

STAT_ALIASES = {
    'str': 'str', 'сила': 'str', 'с': 'str',
    'agi': 'agi', 'ловкость': 'agi', 'л': 'agi',
    'vit': 'vit', 'выносливость': 'vit', 'в': 'vit',
    'luk': 'luk', 'удача': 'luk', 'у': 'luk',
    'df_stat': 'df_stat', 'df': 'df_stat', 'защита': 'df_stat',
}


class DevConsoleController:
    """Пароль, ввод команд и их выполнение. Без pygame-отрисовки."""

    MAX_LINES = 250

    def __init__(self, game):
        self.game = game
        self.unlocked = False
        self.return_mode = 'game'
        self.buffer = ''
        self.error = ''
        self.lines = []          # [(text, color), ...]
        self.history = []
        self.history_pos = 0
        self.attempts = 0
        self.lock_until = 0.0

    # ── Состояние ──
    def open(self):
        """Открывает консоль разработчика.

        P2 правка безопасности: консоль доступна ТОЛЬКО в single-player
        режиме (``game.screen_mode == 'game'``). В меню, выборе класса,
        мультиплеере и т.п. — открывать нельзя, чтобы не дать читы в MP
        и не сломать экраны меню.

        Возвращает True если консоль открылась, False если заблокирована.
        """
        game = self.game
        # P2: блокировка консоли вне single-player 'game' режима.
        # Список режимов, в которых консоль доступна.
        ALLOWED_MODES = {'game', 'inventory', 'stats', 'craft',
                         'skills', 'quests', 'shop', 'trade'}
        if game.screen_mode not in ALLOWED_MODES:
            # Тихо ничего не делаем — консоль не должна открываться в меню/MP.
            return False
        if game.screen_mode != 'dev_console':
            self.return_mode = game.screen_mode
        game.screen_mode = 'dev_console'
        self.buffer = ''
        self.error = ''
        if self.unlocked and not self.lines:
            self._out('Консоль разработчика. help — список команд.', config.COL_DIM)
        return True

    def close(self):
        mode = self.return_mode
        if mode == 'dev_console':
            mode = 'game'
        self.game.screen_mode = mode
        self.buffer = ''
        self.error = ''

    def locked_for(self):
        return max(0.0, self.lock_until - time.time())

    @property
    def prompt(self):
        return 'dev> ' if self.unlocked else 'пароль> '

    # ── Ввод ──
    def handle_key(self, key, unicode='', mods=0):
        if key == pygame.K_ESCAPE:
            self.close()
            return
        if self.locked_for() > 0:
            return
        if not self.unlocked:
            self._handle_password_key(key, unicode, mods)
            return

        if key in (pygame.K_RETURN, pygame.K_KP_ENTER):
            self._submit()
        elif key == pygame.K_BACKSPACE:
            self.buffer = self.buffer[:-1]
            self.error = ''
        elif key == pygame.K_UP:
            self._history_move(-1)
        elif key == pygame.K_DOWN:
            self._history_move(1)
        elif key == pygame.K_TAB:
            self._complete()
        else:
            self._append_char(key, unicode, mods, 160)

    def _handle_password_key(self, key, unicode, mods):
        if key in (pygame.K_RETURN, pygame.K_KP_ENTER):
            self._try_password()
        elif key == pygame.K_BACKSPACE:
            self.buffer = self.buffer[:-1]
            self.error = ''
        else:
            self._append_char(key, unicode, mods, 64)

    def _append_char(self, key, unicode, mods, limit):
        if mods & pygame.KMOD_CTRL:
            return
        ch = unicode or ''
        if len(ch) != 1 or not ch.isprintable():
            return
        if len(self.buffer) < limit:
            self.buffer += ch
            self.error = ''

    def _try_password(self):
        entered = self.buffer
        self.buffer = ''
        if not entered:
            self.error = 'Пароль не введён.'
            return
        # Используем новую функцию verify_dev_console_password — она
        # поддерживает scrypt (P3 правка) и fallback на sha256 для
        # старых сейвов. Сравнение constant-time — внутри функции.
        expected_hash = config.dev_console_password_hash()
        if config.verify_dev_console_password(entered, expected_hash):
            self.unlocked = True
            self.attempts = 0
            self.lines = []
            self.error = ''
            self._out('Доступ разрешён. help — список команд, lock — запереть.',
                      config.COL_HEAL)
            return

        self.attempts += 1
        left = config.DEV_CONSOLE_MAX_ATTEMPTS - self.attempts
        if left <= 0:
            self.attempts = 0
            self.lock_until = time.time() + config.DEV_CONSOLE_LOCK_SECONDS
            self.error = ''
            self.close()
            if self.game.screen_mode in ('game', 'inventory', 'stats', 'craft',
                                         'skills', 'quests', 'shop', 'trade'):
                self.game.log(
                    f"Консоль разработчика заблокирована на "
                    f"{config.DEV_CONSOLE_LOCK_SECONDS} с.", config.COL_DIM)
        else:
            self.error = f'Неверный пароль. Осталось попыток: {left}.'

    def _submit(self):
        raw = self.buffer.strip()
        self.buffer = ''
        if not raw:
            return
        self.history.append(raw)
        self.history_pos = len(self.history)
        self._out('> ' + raw, config.COL_TEXT)
        try:
            self.execute(raw)
        except Exception as exc:                    # защита от кривых команд
            self._out(f'Ошибка: {exc}', config.COL_DMG)

    def _history_move(self, delta):
        if not self.history:
            return
        self.history_pos = max(0, min(len(self.history) - 1,
                                      self.history_pos + delta))
        self.buffer = self.history[self.history_pos]

    def _complete(self):
        if ' ' in self.buffer:
            return
        names = sorted(COMMANDS)
        matches = [name for name in names if name.startswith(self.buffer)]
        if len(matches) == 1:
            self.buffer = matches[0] + ' '

    # ── Вывод ──
    def _out(self, text, color=None):
        self.lines.append((str(text), color or config.COL_TEXT))
        if len(self.lines) > self.MAX_LINES:
            del self.lines[:-self.MAX_LINES]

    # ── Команды ──
    def execute(self, raw):
        parts = raw.split()
        if not parts:
            return
        cmd = parts[0].lower()
        args = parts[1:]

        handler = getattr(self, '_cmd_' + cmd, None)
        if handler is None:
            close = difflib.get_close_matches(cmd, COMMANDS, n=3)
            hint = f' Похожие: {", ".join(close)}.' if close else ''
            self._out(f'Неизвестная команда: {cmd}.{hint} help — список.',
                      config.COL_DMG)
            return
        handler(args)

    def _cmd_help(self, args):
        if args:
            name = args[0].lower()
            text = COMMANDS.get(name)
            if text:
                self._out(text, config.COL_GOLD)
            else:
                self._out(f'Нет команды {name}.', config.COL_DMG)
            return
        for name in COMMANDS:
            self._out(COMMANDS[name], config.COL_DIM)

    def _cmd_give(self, args):
        if not args:
            self._out('Использование: give <ключ|имя> [N]', config.COL_DMG)
            return
        count = self._int_arg(args, 1, default=1, low=1, high=999,
                              what='количество')
        if count is None:
            return
        full_key = self._resolve_item(args[0])
        if full_key is None:
            self._out(f'Неизвестный предмет: {args[0]}', config.COL_DMG)
            return
        inventory = self.game.player.inventory
        inventory[full_key] = inventory.get(full_key, 0) + count
        self._out(f'Выдано: {item_display_name(full_key)} ×{count}.',
                  config.COL_HEAL)

    def _cmd_gold(self, args):
        amount = self._int_arg(args, 0, what='количество золота')
        if amount is None:
            return
        player = self.game.player
        player.gold = max(0, player.gold + amount)
        if amount > 0:
            self.game.total_gold_earned += amount
        self._out(f'Золото: {player.gold}.', config.COL_GOLD)

    def _cmd_xp(self, args):
        amount = self._int_arg(args, 0, low=1, what='опыт')
        if amount is None:
            return
        gained = rules.give_xp(self.game.player, amount)
        for level in gained:
            self.game._on_level_up(level)
        text = f'+{amount} XP'
        if gained:
            text += f', новые уровни: {", ".join(map(str, gained))}'
        self._out(text, config.COL_XP)

    def _cmd_level(self, args):
        target = self._int_arg(args, 0, low=1, high=99, what='уровень')
        if target is None:
            return
        player = self.game.player
        if target <= player.level:
            self._out(f'Текущий уровень уже {player.level}.', config.COL_DIM)
            return
        while player.level < target:
            level = rules.level_up_once(player)
            self.game._on_level_up(level)
        self._out(f'Уровень: {player.level}.', config.COL_GOLD)

    def _cmd_heal(self, args):
        player = self.game.player
        player.hp = player.max_hp
        player.mana = player.max_mana
        player.clear_statuses()
        player.recalc()
        self._out(f'HP {player.hp}/{player.max_hp}, MP {player.mana}/'
                  f'{player.max_mana}.', config.COL_HEAL)

    def _cmd_god(self, args):
        player = self.game.player
        player.god_mode = not getattr(player, 'god_mode', False)
        state = 'включено' if player.god_mode else 'выключено'
        self._out(f'Бессмертие {state}.', config.COL_GOLD)

    def _cmd_stat(self, args):
        if len(args) < 2:
            self._out('Использование: stat <str|agi|vit|luk|df_stat> <N>',
                      config.COL_DMG)
            return
        name = STAT_ALIASES.get(args[0].lower())
        if name is None:
            self._out(f'Неизвестная характеристика: {args[0]}',
                      config.COL_DMG)
            return
        value = self._int_arg(args, 1, low=1, high=999, what='значение')
        if value is None:
            return
        setattr(self.game.player, name, value)
        self.game.player.recalc()
        self._out(f'{name} = {value}.', config.COL_GOLD)

    def _cmd_skill(self, args):
        if len(args) < 2:
            self._out('Использование: skill <ветка> <уровень>', config.COL_DMG)
            return
        tab = skills.canon_skill(args[0].lower())
        if tab not in skills.CRAFT_TABS:
            self._out('Ветки: ' + ', '.join(skills.CRAFT_TABS),
                      config.COL_DMG)
            return
        level = self._int_arg(args, 1, low=1, high=skills.SKILL_CAP,
                              what='уровень навыка')
        if level is None:
            return
        player = self.game.player
        player.skills[tab] = {'level': level, 'xp': 0}
        player.skill_xp = dict(getattr(player, 'skill_xp', {}) or {})
        player.skill_xp[tab] = 0
        self._out(f'{skills.TAB_NAMES.get(tab, tab)}: уровень {level}.',
                  config.COL_GOLD)

    def _cmd_tp(self, args):
        if len(args) < 2:
            self._out('Использование: tp <x> <y>', config.COL_DMG)
            return
        try:
            x, y = int(args[0]), int(args[1])
        except ValueError:
            self._out('Координаты должны быть числами.', config.COL_DMG)
            return
        dungeon = self.game.dungeon
        if not (0 <= x < dungeon.w and 0 <= y < dungeon.h):
            self._out(f'За границей карты ({dungeon.w}×{dungeon.h}).',
                      config.COL_DMG)
            return
        if not rules.tile_walkable(dungeon, x, y):
            self._out('Туда нельзя встать: стена.', config.COL_DMG)
            return
        self._teleport(x, y)
        self._out(f'Телепорт: {x}, {y}.', config.COL_GOLD)

    def _cmd_goto(self, args):
        if not args:
            self._out('Использование: goto <локация|surface|dungeon>',
                      config.COL_DMG)
            return
        target = args[0].lower()
        game = self.game
        if target in ('surface', 'out', 'outside', 'overworld'):
            game.exit_to_overworld()
            self._out('Ты на поверхности.', config.COL_GOLD)
            return
        if target in ('dungeon', 'dungeon1', 'подземелье'):
            game.enter_dungeon()
            self._out('Ты в подземелье.', config.COL_GOLD)
            return
        from core import locations
        if target in locations.LOCATIONS:
            game.travel_to(target)
            self._out(f'Локация: {locations.title(target)}.', config.COL_GOLD)
            return
        self._out('Локации: ' + ', '.join(sorted(locations.LOCATIONS)),
                  config.COL_DMG)

    def _cmd_depth(self, args):
        depth = self._int_arg(args, 0, low=1, high=config.MAX_DEPTH,
                              what='глубина')
        if depth is None:
            return
        self.game.new_dungeon(depth, autosave=False)
        self._out(f'Глубина: {depth}.', config.COL_GOLD)

    def _cmd_spawn(self, args):
        if not args:
            self._out('Использование: spawn <моб> [N]', config.COL_DMG)
            return
        key = self._resolve_monster(args[0])
        if key is None:
            self._out(f'Неизвестный моб: {args[0]}', config.COL_DMG)
            return
        count = self._int_arg(args, 1, default=1, low=1, high=20,
                              what='количество')
        if count is None:
            return
        spawned = self._spawn_monsters(key, count)
        if spawned:
            self._out(f'Создано: {MONSTERS[key]["name"]} ×{spawned}.',
                      config.COL_GOLD)
        else:
            self._out('Не нашлось свободных клеток.', config.COL_DMG)

    def _cmd_boss(self, args):
        depth = self._int_arg(args, 0, low=1, high=config.MAX_DEPTH,
                              what='этаж босса')
        if depth is None:
            return
        data = BOSSES.get(depth)
        if data is None:
            self._out('Боссы есть на этажах: ' + ', '.join(map(str, BOSSES)),
                      config.COL_DMG)
            return
        spot = self._find_spot()
        if spot is None:
            self._out('Не нашлось свободной клетки.', config.COL_DMG)
            return
        boss = Entity(spot[0], spot[1], data['hp'], data['hp'], data['atk'],
                      data['df'], data['name'], data['ch'], data['color'],
                      data['xp'], boss=True)
        rules.init_action_timers([boss])
        self.game.dungeon.monsters.append(boss)
        self._out(f'Создан босс: {data["name"]}.', config.COL_DMG)

    def _cmd_killall(self, args):
        count = len(self.game.dungeon.monsters)
        self.game.dungeon.monsters.clear()
        self._out(f'Убрано мобов: {count}.', config.COL_GOLD)

    def _cmd_where(self, args):
        game = self.game
        player = game.player
        if game.world_mode == 'overworld':
            from core import locations
            place = f'поверхность · {locations.title(game.location)}'
        else:
            place = f'подземелье · этаж {game.depth}/{config.MAX_DEPTH}'
        self._out(f'{place}', config.COL_GOLD)
        self._out(f'xy={player.x},{player.y} · HP {player.hp}/{player.max_hp}'
                  f' · MP {player.mana}/{player.max_mana}', config.COL_TEXT)
        self._out(f'Ур. {player.level} · XP {player.xp}/{player.xp_next}'
                  f' · золото {player.gold}'
                  f' · мобов {len(game.dungeon.monsters)}', config.COL_TEXT)

    def _cmd_save(self, args):
        if self.game.dead or self.game.victory:
            self._out('Нечего сохранять.', config.COL_DMG)
            return
        self.game.save(silent=True)
        self._out('Игра сохранена.', config.COL_HEAL)

    def _cmd_fps(self, args):
        settings = self.game.settings
        settings['show_fps'] = not settings.get('show_fps', False)
        from core.settings import save_settings
        save_settings(settings)
        state = 'включён' if settings['show_fps'] else 'выключен'
        self._out(f'Счётчик FPS {state}.', config.COL_GOLD)

    def _cmd_clear(self, args):
        self.lines.clear()

    def _cmd_lock(self, args):
        self.unlocked = False
        self.buffer = ''
        self.lines.clear()
        self.error = 'Консоль заблокирована.'

    def _cmd_close(self, args):
        self.close()

    # ── Помощники ──
    def _int_arg(self, args, index, default=None, low=None, high=None,
                 what='значение'):
        if index >= len(args):
            if default is not None:
                return default
            self._out(f'Не указано: {what}.', config.COL_DMG)
            return None
        try:
            value = int(args[index])
        except ValueError:
            self._out(f'{what.capitalize()} должно быть числом.',
                      config.COL_DMG)
            return None
        if low is not None and value < low:
            self._out(f'{what.capitalize()} не меньше {low}.', config.COL_DMG)
            return None
        if high is not None and value > high:
            self._out(f'{what.capitalize()} не больше {high}.', config.COL_DMG)
            return None
        return value

    def _resolve_item(self, token):
        token = token.strip()
        if token in ITEMS:
            return token
        base, rarity = parse_item_key(token)
        if base in ITEMS:
            return make_loot_key(base, max(0, min(3, rarity)))
        low = token.lower()
        for key in ITEMS:
            if key.lower() == low:
                return key
        # Поиск по названию — удобно с кириллицей.
        matches = [key for key, data in ITEMS.items()
                   if low in str(data.get('name', '')).lower()]
        if len(matches) == 1:
            return matches[0]
        return None

    def _resolve_monster(self, token):
        token = token.strip()
        if token in MONSTERS:
            return token
        low = token.lower()
        for key, data in MONSTERS.items():
            if data.get('ch') == token or key.lower() == low:
                return key
            if low in str(data.get('name', '')).lower():
                return key
        return None

    def _find_spot(self, occupied=None):
        game = self.game
        dungeon = game.dungeon
        blocked = set(occupied or ())
        for m in dungeon.monsters:
            blocked.add((m.x, m.y))
        px, py = game.player.x, game.player.y
        for radius in range(1, 10):
            for dx in range(-radius, radius + 1):
                for dy in range(-radius, radius + 1):
                    if max(abs(dx), abs(dy)) != radius:
                        continue
                    x, y = px + dx, py + dy
                    if not (0 <= x < dungeon.w and 0 <= y < dungeon.h):
                        continue
                    if (x, y) in blocked:
                        continue
                    if dungeon.tiles[y][x] != Tile.FLOOR:
                        continue
                    if (x, y) in dungeon.items:
                        continue
                    return x, y
        # Резерв: любая свободная клетка карты.
        spots = [(x, y) for y in range(1, dungeon.h - 1)
                 for x in range(1, dungeon.w - 1)
                 if dungeon.tiles[y][x] == Tile.FLOOR
                 and (x, y) not in blocked
                 and (x, y) not in dungeon.items]
        return random.choice(spots) if spots else None

    def _spawn_monsters(self, key, count):
        data = MONSTERS[key]
        occupied = set()
        spawned = 0
        for _ in range(count):
            spot = self._find_spot(occupied)
            if spot is None:
                break
            occupied.add(spot)
            monster = Entity(spot[0], spot[1], data['hp'], data['hp'],
                             data['atk'], data['df'], data['name'],
                             data['ch'], data['color'], data['xp'],
                             ability=data.get('ability'))
            rules.init_action_timers([monster])
            self.game.dungeon.monsters.append(monster)
            spawned += 1
        return spawned

    def _teleport(self, x, y):
        game = self.game
        game.player.x, game.player.y = x, y
        game.update_fov()
        game._last_fov_pos = (x, y)
        game.render_revision = getattr(game, 'render_revision', 0) + 1
