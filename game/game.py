"""Ядро игры: состояние, движение, бой, предметы, save/load, маршрутизация input."""
from core import config, rules, overworld as ov
import os
import random
import json
import pygame
import time

from core.quests import QuestLog
from game.dialogue_controller import DialogueController
from game.quest_controller import QuestController
from game.stats_controller import StatsController
from core.config import (
    TILE, MAP_W, MAP_H, MAX_DEPTH, MERCHANT_FLOORS, Tile, INVENTORY_PAGE_SIZE,
    PLAYER_MOVE_COOLDOWN, PLAYER_ATTACK_COOLDOWN, SAVE_VERSION,
    STATUS_TICK_INTERVAL, REGEN_TICK_INTERVAL,
    COL_TEXT, COL_DIM, COL_GOLD, COL_DMG, COL_XP, COL_CRIT,
    COL_HEAL, COL_DODGE, COL_WIN, COL_MERCH,
)
from core.data import BOSSES, parse_item_key, item_display_name
from core.entities import Player, Entity, Message, FloatText
from core.world import (generate_dungeon, generate_overworld, place_entry_tile,
                        has_los, compute_fov, bfs_toward_player)
from core.save import (entity_to_dict, entity_from_dict,
                       player_to_dict, player_from_dict,
                       dungeon_to_dict, dungeon_from_dict,
                       dungeon_cache_to_dict, dungeon_cache_from_dict,
                       save_slot_path)
from render import Renderer
from core.settings import load_settings, save_settings
from game.menu_controller import MenuController
from game.shop_controller import ShopController
from game.craft_controller import CraftController


# Порядок характеристик для цифровых клавиш в окне статов
STAT_ORDER = ('str', 'agi', 'vit', 'luk', 'df_stat')


class Game:
    def __init__(self, screen):
        self.renderer = Renderer(screen)
        self.settings = load_settings()

        from audio import Audio
        Audio.init(volume=self.settings.get('volume', 7))

        # Контроллеры
        self.menu = MenuController(self)
        self.shop = ShopController(self)
        self.craft = CraftController(self)

        self.dialogue = DialogueController(self)
        self.quests = QuestController(self)
        self.stats_run = StatsController(self)

        # Состояние
        self.pending_class_key = 'warrior'
        self.current_slot = 1
        self.mouse_pos = (-1, -1)
        self.new_game()
        self.screen_mode = 'menu'

    # ════════════════════════ СОСТОЯНИЕ ════════════════════════
    def new_game(self, class_key=None):
        if class_key is None:
            class_key = getattr(self, 'pending_class_key', 'default') or 'default'
        self.pending_class_key = class_key

        # Инициализация контроллеров — для тестов через Game.__new__
        if not hasattr(self, 'menu'):
            self.menu = MenuController(self)
        if not hasattr(self, 'shop'):
            self.shop = ShopController(self)
        if not hasattr(self, 'craft'):
            self.craft = CraftController(self)
        if not hasattr(self, 'dialogue'):
            self.dialogue = DialogueController(self)
        if not hasattr(self, 'quests'):
            self.quests = QuestController(self)
        if not hasattr(self, 'stats_run'):
            self.stats_run = StatsController(self)
        if not hasattr(self, 'current_slot'):
            self.current_slot = 1
        if not hasattr(self, 'settings'):
            self.settings = load_settings()
        if not hasattr(self, 'mouse_pos'):
            self.mouse_pos = (-1, -1)

        self.player = Player(class_key=class_key)
        self.messages = []
        self.log_scroll = 0
        self._lvl_float_idx = 0
        self.dead = False
        self.victory = False
        self.visible = set()
        self.explored = set()
        self.overworld_explored = set()
        self.screen_mode = 'game'
        self.shop_submode = 'buy'
        self.inv_page = 0
        self.floats = []
        self.merchant = None
        self.shop_stock = []
        self.dungeon_cache = {}
        self.world_mode = 'overworld'
        self.depth = 0
        self.overworld = generate_overworld()
        self.dungeon = self.overworld
        sx, sy = ov.VILLAGE_CENTER
        if self.overworld.tiles[sy][sx] == Tile.FLOOR:
            self.player.x, self.player.y = sx, sy
        else:
            spot = rules.find_free_tile(self.overworld, sx, sy)
            if spot:
                self.player.x, self.player.y = spot
        self.shop.page = 0

        # Квесты — выдаём все доступные от обоих NPC
        self.quest_log = QuestLog()


        # Счётчики забега
        self.start_time = time.time()
        self.kills = 0
        self.boss_kills = 0
        self.total_gold_earned = 0
        self.total_kills_by_ch = {}
        self.total_items_collected = {}
        self.total_potions_used = 0
        self.total_crafts = 0
        self.total_quests_turned_in = 0
        self.quest_board = None

        self._modal_close_rect = None
        self.pending_resolution = None
        self.pending_multiplayer = None
        self._modal_content_rect = (0, 0, 0, 0)
        self._ui_hits = {}

        # Старейшина живёт в деревне, на центральной площади
        self.elder = None
        ex, ey = ov.ELDER_POS
        if (0 <= ex < MAP_W and 0 <= ey < MAP_H
                and self.overworld.tiles[ey][ex] == Tile.FLOOR):
            self.elder = Entity(ex, ey, 1, 1, 0, 0,
                                "Старейшина", "E", (240, 210, 100), 0)
            self.elder.is_npc = True
            self.elder.npc_type = 'elder'
        else:
            spot = rules.find_free_tile(
                self.overworld, ov.VILLAGE_CENTER[0], ov.VILLAGE_CENTER[1],
                self.occupied_tiles())
            if spot:
                tx, ty = spot
                self.elder = Entity(tx, ty, 1, 1, 0, 0,
                                    "Старейшина", "E", (240, 210, 100), 0)
                self.elder.is_npc = True
                self.elder.npc_type = 'elder'

        if self.elder is None:
            self.log("Старейшине не нашлось места — поговори с ним позже.", COL_DIM)

        self.quest_board = self._spawn_quest_board()
        self.update_fov()
        self.log("Добро пожаловать в деревню Этельгард!", COL_TEXT)
        self.log("Восемь лесных краёв ждут исследователя. Ищи дороги.", COL_GOLD)
        self.log("Вход в подземелье — на Каменном перевале (северо-восток).",
                 COL_GOLD)
        self.log(f"Цель: победить Древнего дракона на глубине {MAX_DEPTH}.", COL_GOLD)
        # Real-time таймеры
        self.player_move_timer = 0.0
        self.player_attack_timer = 0.0
        self.status_tick_timer = STATUS_TICK_INTERVAL
        self.regen_timer = REGEN_TICK_INTERVAL
        self._last_fov_pos = (self.player.x, self.player.y)
        # Счётчик для кэшей рендера: карта сменилась
        self.render_revision = getattr(self, 'render_revision', 0) + 1
        rules.init_action_timers(self.overworld.monsters)

    def occupied_tiles(self):
        """Занятые клетки: игрок, NPC, мобы, торговец."""
        occupied = {(self.player.x, self.player.y)}
        occupied.update((m.x, m.y) for m in self.dungeon.monsters if m.hp > 0)
        for npc in (self.merchant, self.elder):
            if npc is not None:
                occupied.add((npc.x, npc.y))
        return occupied

    def _spawn_quest_board(self):
        """Доска на текущей карте или None."""
        if self.world_mode == 'overworld':
            bx, by = ov.QUEST_BOARD_VILLAGE_POS
            if (0 <= bx < MAP_W and 0 <= by < MAP_H
                    and self.overworld.tiles[by][bx] != Tile.WALL):
                e = Entity(bx, by, 1, 1, 0, 0, "Доска поручений", "B",
                           (206, 170, 96), 0)
                e.is_npc = True;
                e.npc_type = 'quest_board'
                e.board_depth_min, e.board_depth_max = 0, 4
                return e
            return None
        if self.depth not in (5, 10, 15, 20) or not self.dungeon.rooms:
            return None
        room = self.dungeon.rooms[0]
        bx, by = room.center()
        for dx, dy in ((0, 1), (1, 0), (-1, 0), (0, -1)):
            tx, ty = bx + dx, by + dy
            if (room.x <= tx < room.x + room.w
                    and room.y <= ty < room.y + room.h
                    and self.dungeon.tiles[ty][tx] == Tile.FLOOR
                    and not any(m.x == tx and m.y == ty for m in self.dungeon.monsters)):
                e = Entity(tx, ty, 1, 1, 0, 0, "Доска поручений", "B",
                           (206, 170, 96), 0)
                e.is_npc = True;
                e.npc_type = 'quest_board'
                e.board_depth_min = self.depth
                e.board_depth_max = min(self.depth + 4, 20)
                return e
        return None

    def log(self, msg, color=COL_TEXT):
        self.messages.append(Message(msg, color))
        if len(self.messages) > 50:
            self.messages = self.messages[-50:]

    def scroll_log(self, delta):
        max_scroll = max(0, len(self.messages) - 6)
        self.log_scroll = max(0, min(max_scroll, self.log_scroll + delta))

    def add_float(self, x, y, text, color):
        self.floats.append(
            FloatText(x * TILE + TILE // 2, y * TILE + TILE // 2, text, color))

    # ════════════════════════ ПЕРЕХОДЫ МЕЖДУ МИРАМИ ════════════════════════
    def enter_dungeon(self):
        # Сохраняем память внешнего мира перед входом
        if self.world_mode == 'overworld':
            self.overworld_explored = set(self.explored)
        self.world_mode = 'dungeon'
        self.depth = 0
        self.new_dungeon(1)
        self.log("Ты входишь в подземелье...", COL_DMG)

    def exit_to_overworld(self):
        if self.depth > 0 and self.depth in self.dungeon_cache:
            self.dungeon_cache[self.depth]['explored'] = set(self.explored)

        self.world_mode = 'overworld'
        self.depth = 0
        self.dungeon = self.overworld
        self.explored = set(self.overworld_explored)
        self.visible = set()
        self.merchant = None
        self.shop_stock = []

        # Спавн рядом со входом в подземелье, а не в центре карты
        dx, dy = ov.DUNGEON_POS
        spot = rules.find_free_tile(self.overworld, dx, dy + 1,
                                    self.occupied_tiles())
        if spot:
            self.player.x, self.player.y = spot
        else:
            self.player.x, self.player.y = dx, dy + 1

        self.update_fov()
        self._last_fov_pos = (self.player.x, self.player.y)
        self.quest_board = self._spawn_quest_board()
        self.render_revision = getattr(self, 'render_revision', 0) + 1
        self.log("Ты возвращаешься на поверхность.", COL_HEAL)

    def ascend(self):
        """Подъём: с глубины 1 — на поверхность, глубже — на этаж выше."""
        from audio import Audio
        Audio.play('stairs')
        if self.depth <= 1:
            self.exit_to_overworld()
        else:
            depth = self.depth - 1
            self.new_dungeon(depth, arrive='stairs')
            self.log(f"Подъём на глубину {depth}.", COL_GOLD)

    def new_dungeon(self, depth, autosave=True, arrive='entry'):
        if self.world_mode == 'dungeon' and self.depth > 0:
            if self.depth in self.dungeon_cache:
                self.dungeon_cache[self.depth]['explored'] = set(self.explored)
                self.dungeon_cache[self.depth]['merchant'] = self.merchant
                self.dungeon_cache[self.depth]['shop_stock'] = self.shop_stock

        self.depth = depth
        self.world_mode = 'dungeon'

        if depth in self.dungeon_cache:
            cached = self.dungeon_cache[depth]
            self.dungeon = cached['dungeon']
            self.merchant = cached.get('merchant')
            self.shop_stock = cached.get('shop_stock', [])
            # Старые сейвы и кэш без лестницы назад — доставляем её
            place_entry_tile(self.dungeon, depth)
            if depth > 1:
                self.log(f"Возвращение на глубину {depth}.", COL_GOLD)
        else:
            self.dungeon = generate_dungeon(depth)
            self.merchant = None
            self.shop_stock = []

            if depth in MERCHANT_FLOORS:
                room = self.dungeon.rooms[0]
                candidates = [
                    (room.x + 1, room.y + 1),
                    (room.x + room.w - 2, room.y + 1),
                    (room.x + 1, room.y + room.h - 2),
                    (room.x + room.w - 2, room.y + room.h - 2),
                ]
                random.shuffle(candidates)
                for mx, my in candidates:
                    if not (0 <= mx < MAP_W and 0 <= my < MAP_H):
                        continue
                    if self.dungeon.tiles[my][mx] == Tile.WALL:
                        continue
                    if (mx, my) == tuple(self.dungeon.rooms[0].center()):
                        continue
                    self.merchant = Entity(mx, my, 1, 1, 0, 0,
                                           "Торговец", "M", COL_MERCH, 0)
                    self.merchant.is_merchant = True
                    self.merchant.is_npc = True
                    self.merchant.npc_type = 'merchant'
                    self.shop.refresh()
                    self.log("Торговец на этаже! Найди его (T).", COL_MERCH)
                    break

            self.dungeon_cache[depth] = {
                'dungeon': self.dungeon,
                'merchant': self.merchant,
                'shop_stock': self.shop_stock,
                'explored': set(),
            }

            if depth > 1:
                self.log(f"Спуск на глубину {depth}.", COL_GOLD)
            if depth in BOSSES:
                self.log(f"⚠ {BOSSES[depth]['name']} на глубине {depth}!", COL_DMG)

        # Точка появления: вход снизу (центр первой комнаты) или
        # лестница вниз, если поднимаемся с этажа ниже.
        if arrive == 'stairs' and self.dungeon.stairs_pos:
            cx, cy = self.dungeon.stairs_pos
        else:
            cx, cy = self.dungeon.rooms[0].center()
        self.player.x, self.player.y = cx, cy

        # Восстанавливаем explored для этой глубины
        self.explored = set(self.dungeon_cache[depth].get('explored', set()))
        self.update_fov()
        self._last_fov_pos = (self.player.x, self.player.y)
        # Синхронизируем кэш
        self.dungeon_cache[depth]['explored'] = set(self.explored)
        self.render_revision = getattr(self, 'render_revision', 0) + 1
        # Индивидуальные таймеры ходов и квест «достичь глубины»
        rules.init_action_timers(self.dungeon.monsters)
        self.quest_log.update_reach(depth)

        if autosave and depth > 1:
            self.save(silent=True)

    def update_fov(self):
        self.visible = compute_fov(self.dungeon, self.player.x, self.player.y)
        self.explored |= self.visible

    # ════════════════════════ SAVE / LOAD ════════════════════════
    def save(self, path=None, silent=False):
        if path is None:
            path = save_slot_path(self.current_slot)
        if self.dead or self.victory:
            if not silent:
                self.log("Нечего сохранять.", COL_DIM)
            return
        # Текущий этаж синхронизируем в кэш и сохраняем отдельным ключом
        # `dungeon` (в кэше дубликата не будет).
        if self.world_mode == 'dungeon':
            self.dungeon_cache[self.depth] = {
                'dungeon': self.dungeon,
                'merchant': self.merchant,
                'shop_stock': self.shop_stock,
                'explored': set(self.explored),
            }
        data = {
            'version': SAVE_VERSION,
            'world_mode': self.world_mode,
            'depth': self.depth,
            'explored': [list(p) for p in self.explored],
            'messages': [{'text': m.text, 'color': list(m.color)}
                         for m in self.messages],
            'player': player_to_dict(self.player),
            'overworld': dungeon_to_dict(self.overworld),
            'overworld_explored': [list(p) for p in self.overworld_explored],
            # В открытом мире dungeon — это и есть overworld: не дублируем.
            'dungeon': (dungeon_to_dict(self.dungeon)
                        if self.world_mode == 'dungeon' else None),
            'dungeon_cache': dungeon_cache_to_dict(
                self.dungeon_cache, current_depth=self.depth,
                skip_depth=self.depth if self.world_mode == 'dungeon' else None),
            'merchant': entity_to_dict(self.merchant) if self.merchant else None,
            'shop_stock': self.shop_stock,
            'quests': self.quest_log.to_dict(),
            'run_stats': {
                'total_gold_earned': self.total_gold_earned,
                'total_kills_by_ch': dict(self.total_kills_by_ch),
                'total_items_collected': dict(self.total_items_collected),
                'total_potions_used': self.total_potions_used,
                'total_crafts': self.total_crafts,
                'total_quests_turned_in': self.total_quests_turned_in,
                'start_time': self.start_time,
                'kills': self.kills,
                'boss_kills': self.boss_kills,
            },
            'elder': entity_to_dict(self.elder) if self.elder else None,
        }
        tmp = path + '.tmp'
        try:
            with open(tmp, 'w', encoding='utf-8') as f:
                # Компактно: сейв — машинные данные, отступы раздували
                # файл втрое (336 КБ против ~100 КБ).
                json.dump(data, f, ensure_ascii=False,
                          separators=(',', ':'))
            os.replace(tmp, path)
            if not silent:
                self.log(f"Сохранено в слот {self.current_slot}.", COL_HEAL)
        except Exception as e:
            self.log(f"Ошибка сохранения: {e}", COL_DMG)

    def load(self, path=None):
        if path is None:
            path = save_slot_path(self.current_slot)
        if not os.path.exists(path):
            self.log("Сохранения нет. Сначала F5.", COL_DMG)
            return False
        try:
            with open(path, 'r', encoding='utf-8') as f:
                data = json.load(f)

            version = int(data.get('version', 5))
            if version > SAVE_VERSION:
                raise ValueError(f"сейв новее игры (v{version} > v{SAVE_VERSION})")

            self.player = player_from_dict(data['player'])
            self.overworld = dungeon_from_dict(data['overworld'],
                                              is_overworld=True)
            self.world_mode = data.get('world_mode', 'overworld')
            self.depth = int(data.get('depth', 0))
            self.dungeon_cache = dungeon_cache_from_dict(
                data.get('dungeon_cache', {}))
            self.explored = {tuple(p) for p in data.get('explored', [])}
            self.overworld_explored = {
                tuple(p) for p in data.get('overworld_explored', [])}

            if self.world_mode == 'dungeon':
                raw = data.get('dungeon')
                if raw:
                    self.dungeon = dungeon_from_dict(raw)
                elif self.depth in self.dungeon_cache:
                    self.dungeon = self.dungeon_cache[self.depth]['dungeon']
                else:                                   # повреждённый сейв
                    self.dungeon = generate_dungeon(self.depth)
                place_entry_tile(self.dungeon, self.depth)
                cached = self.dungeon_cache.get(self.depth, {})
                self.merchant = (entity_from_dict(data['merchant'])
                                 if data.get('merchant') else cached.get('merchant'))
                self.shop_stock = data.get('shop_stock') or cached.get('shop_stock', [])
                # Приводим кэш к единому виду: текущий этаж = self.dungeon
                self.dungeon_cache[self.depth] = {
                    'dungeon': self.dungeon,
                    'merchant': self.merchant,
                    'shop_stock': self.shop_stock,
                    'explored': set(self.explored),
                }
            else:
                self.dungeon = self.overworld
                self.merchant = None
                self.shop_stock = []
                self.overworld_explored = set(self.explored)

            if self.elder is None:
                spot = rules.find_free_tile(
                    self.overworld, ov.VILLAGE_CENTER[0], ov.VILLAGE_CENTER[1],
                    self.occupied_tiles())
                if spot:
                    ex, ey = spot
                    self.elder = Entity(ex, ey, 1, 1, 0, 0,
                                        "Старейшина", "E", (240, 210, 100), 0)
                    self.elder.is_npc = True
                    self.elder.npc_type = 'elder'

            self.messages = [Message(m['text'], tuple(m['color']))
                             for m in data.get('messages', [])]
            self.dead = False
            self.victory = False
            self.screen_mode = 'game'
            self.shop_submode = 'buy'
            self.shop.page = 0
            self.inv_page = 0
            self.log_scroll = 0
            self.floats = []
            self._lvl_float_idx = 0
            self.quest_log = QuestLog.from_dict(data.get('quests', {}))
            rs = data.get('run_stats', {})
            self.total_gold_earned = rs.get('total_gold_earned', 0)
            self.total_kills_by_ch = dict(rs.get('total_kills_by_ch', {}))
            self.total_items_collected = dict(rs.get('total_items_collected', {}))
            self.total_potions_used = rs.get('total_potions_used', 0)
            self.total_crafts = rs.get('total_crafts', 0)
            self.total_quests_turned_in = rs.get('total_quests_turned_in', 0)
            self.start_time = rs.get('start_time', time.time())
            self.kills = rs.get('kills', 0)
            self.boss_kills = rs.get('boss_kills', 0)
            if data.get('elder'):
                self.elder = entity_from_dict(data['elder'])

            self.player_move_timer = 0.0
            self.player_attack_timer = 0.0
            self.status_tick_timer = STATUS_TICK_INTERVAL
            self.regen_timer = REGEN_TICK_INTERVAL
            rules.init_action_timers(self.dungeon.monsters)
            self.update_fov()
            self._last_fov_pos = (self.player.x, self.player.y)
            self.render_revision = getattr(self, 'render_revision', 0) + 1
            self.log(f"Загружено из слота {self.current_slot}.", COL_HEAL)
            return True
        except Exception as e:
            self.log(f"Ошибка загрузки: {e}", COL_DMG)
            return False

    # ════════════════════════ ДВИЖЕНИЕ ════════════════════════
    def move_player(self, dx, dy):
        nx, ny = self.player.x + dx, self.player.y + dy
        if not (0 <= nx < MAP_W and 0 <= ny < MAP_H):
            return False
        if self.merchant and nx == self.merchant.x and ny == self.merchant.y:
            return False
        if (self.world_mode == 'overworld' and self.elder
                and nx == self.elder.x and ny == self.elder.y):
            return False
        target = next((m for m in self.dungeon.monsters
                       if m.x == nx and m.y == ny and m.hp > 0), None)
        if target:
            self.attack(self.player, target)
            return True
        if self.dungeon.tiles[ny][nx] == Tile.WALL:
            return False
        self.player.x, self.player.y = nx, ny
        if (nx, ny) in self.dungeon.items:
            self.pickup(nx, ny)
        tile = self.dungeon.tiles[ny][nx]
        if tile == Tile.STAIRS:
            if self.world_mode == 'overworld':
                self.enter_dungeon()
            else:
                from audio import Audio
                Audio.play('stairs')
                self.new_dungeon(self.depth + 1)
            # Игрок появится на входной лестнице: без кулдауна удержание
            # направления сразу же отправляет его обратно.
            self.player_move_timer = PLAYER_MOVE_COOLDOWN
            return False
        elif tile == Tile.EXIT:
            self.exit_to_overworld()
            self.player_move_timer = PLAYER_MOVE_COOLDOWN
            return False
        elif tile == Tile.UPSTAIRS:
            self.ascend()
            self.player_move_timer = PLAYER_MOVE_COOLDOWN
            return False
        return True

    def player_attack(self):
        """Атака по пробелу: ближайший моб в радиусе 1."""
        target = None
        best_dist = 999
        for m in self.dungeon.monsters:
            if m.hp <= 0:
                continue
            dist = max(abs(m.x - self.player.x), abs(m.y - self.player.y))
            if dist <= 1 and dist < best_dist:
                target = m
                best_dist = dist
        if target:
            self.attack(self.player, target)

    def pickup(self, x, y):
        from audio import Audio
        result = rules.pickup(self.dungeon, self.player, x, y)
        if result is None:
            return
        Audio.play('pickup')
        self.total_items_collected[result.key] = (
            self.total_items_collected.get(result.key, 0) + 1)
        if result.kind == 'gold':
            self.total_gold_earned += result.gold
            self.log(f"Подобрано: {result.gold} золота", COL_GOLD)
            self.add_float(x, y, f"+{result.gold}g", COL_GOLD)
        elif result.kind == 'resource':
            self.log(f"Собрано: {result.name}", COL_HEAL)
        else:
            self.log(f"Подобрано: {result.name}", result.color)
        self.quest_log.update_collect(result.key, self.player.inventory)

    def consume(self, key):
        rules.consume(self.player.inventory, key)

    # ════════════════════════ ПРЕДМЕТЫ ════════════════════════
    def use_item(self, key):
        from audio import Audio
        result = rules.use_item(self.player, key, self.dungeon.monsters)
        if result is None:
            return
        if not result.ok:
            self.log(result.message, COL_DIM)
            return
        if result.kind == 'heal':
            Audio.play('heal')
            if parse_item_key(key)[0].startswith('potion'):
                self.total_potions_used += 1
            self.log(result.message, COL_HEAL)
            self.add_float(self.player.x, self.player.y, f"+{result.amount}", COL_HEAL)
        elif result.kind == 'equip':
            self.log(result.message, result.color)
        elif result.kind == 'book':
            self.log(result.message, COL_GOLD)
            if result.amount:
                self.add_float(self.player.x, self.player.y,
                               f"+{result.amount}", COL_GOLD)
        elif result.kind == 'fireball':
            Audio.play('fireball')
            for monster, damage in result.victims:
                self.log(f"Огонь → {monster.name}: {damage}", (255, 120, 50))
                self.add_float(monster.x, monster.y, str(damage), (255, 120, 50))
                if monster.hp <= 0:
                    self._kill_monster(monster)
            self.log(result.message, COL_GOLD)
        elif result.kind == 'freeze':
            self.log(result.message, result.color)

    def use_item_by_index(self, idx):
        items = sorted(self.player.inventory.items())
        start = self.inv_page * INVENTORY_PAGE_SIZE
        page_items = items[start:start + INVENTORY_PAGE_SIZE]
        if 0 <= idx < len(page_items):
            self.use_item(page_items[idx][0])
            self.inv_page = min(self.inv_page, self.inv_pages_count() - 1)

    def inv_pages_count(self):
        n = len(self.player.inventory)
        return max(1, (n + INVENTORY_PAGE_SIZE - 1) // INVENTORY_PAGE_SIZE)

    def inv_next_page(self):
        self.inv_page = min(self.inv_pages_count() - 1, self.inv_page + 1)

    def inv_prev_page(self):
        self.inv_page = max(0, self.inv_page - 1)

    def quick_potion(self):
        p = self.player
        if p.hp >= p.max_hp:
            self.log("HP полное.", COL_DIM); return
        key = rules.best_heal_key(p.inventory)
        if not key:
            self.log("Нет зелий!", COL_DMG); return
        self.use_item(key)

    def allocate_stat(self, name):
        p = self.player
        if p.free_points <= 0:
            self.log("Нет свободных очков.", COL_DIM); return
        setattr(p, name, getattr(p, name) + 1)
        p.free_points -= 1
        p.recalc()
        names = {'str': 'Сила', 'agi': 'Ловкость', 'vit': 'Выносливость',
                 'luk': 'Удача', 'df_stat': 'Защита'}
        self.log(f"{names[name]} +1 ({p.free_points} очков)", COL_GOLD)

    def unequip_weapon(self):
        p = self.player
        if not p.weapon:
            self.log("Оружие не надето.", COL_DIM); return
        key = p.weapon
        p.weapon = None
        p.inventory[key] = p.inventory.get(key, 0) + 1
        p.recalc()
        self.log(f"Снято: {item_display_name(key)}", COL_DIM)

    def unequip_armor(self):
        p = self.player
        if not p.armor:
            self.log("Броня не надета.", COL_DIM); return
        key = p.armor
        p.armor = None
        p.inventory[key] = p.inventory.get(key, 0) + 1
        p.recalc()
        self.log(f"Снято: {item_display_name(key)}", COL_DIM)

    def unequip_head(self):
        p = self.player
        if not p.head:
            self.log("Шлем не надет.", COL_DIM);
            return
        key = p.head
        p.head = None
        p.inventory[key] = p.inventory.get(key, 0) + 1
        p.recalc()
        self.log(f"Снято: {item_display_name(key)}", COL_DIM)

    def unequip_legs(self):
        p = self.player
        if not p.legs:
            self.log("Штаны не надеты.", COL_DIM);
            return
        key = p.legs
        p.legs = None
        p.inventory[key] = p.inventory.get(key, 0) + 1
        p.recalc()
        self.log(f"Снято: {item_display_name(key)}", COL_DIM)

    # ════════════════════════ БОЙ ════════════════════════
    def attack(self, a, b):
        self._single_attack(a, b)
        if (isinstance(a, Player) and a.double_strike
                and b.hp > 0 and not self.dead
                and random.random() < 0.15):
            self.log("Двойной удар!", COL_CRIT)
            self._single_attack(a, b)

    def _single_attack(self, a, b):
        """Один удар. Правила — в core.rules, здесь только звук/лог/смерть."""
        from audio import Audio
        from core.data import STATUSES

        result = rules.resolve_attack(a, b)
        if result.dodged:
            Audio.play('miss')
            self.log(f"{b.name} уклоняется от {a.name}!", COL_DODGE)
            self.add_float(b.x, b.y, "MISS", COL_DODGE)
            return

        Audio.play('crit' if result.crit else 'hit')
        color = COL_CRIT if result.crit else COL_DMG
        prefix = "КРИТ! " if result.crit else ""
        self.log(f"{prefix}{a.name} → {b.name}: {result.damage}", color)
        self.add_float(b.x, b.y, str(result.damage), color)

        if result.status:
            info = STATUSES[result.status]
            self.log(f"{b.name}: {info['name']}!", info['color'])
        if result.vampire_heal:
            self.add_float(a.x, a.y, f"+{result.vampire_heal}", COL_HEAL)

        if result.defender_dead:
            if b is self.player:
                Audio.play('death_player')
                self.dead = True
                self.death_time = time.time()
                self.log("Вы погибли. R — рестарт, F9 — загрузка.", COL_DMG)
            else:
                self._kill_monster(b)
            return

        if result.thorns:
            self.add_float(a.x, a.y, "ШИПЫ", (200, 200, 80))
            if result.attacker_dead:
                self._kill_monster(a)

    def _kill_monster(self, monster):
        from audio import Audio
        self.kills += 1
        if monster.boss:
            self.boss_kills += 1
        Audio.play('death_monster')
        self.log(f"{monster.name} погиб (+{monster.xp_value} XP)", COL_XP)
        self.total_kills_by_ch[monster.ch] = (
                self.total_kills_by_ch.get(monster.ch, 0) + 1)
        self.quest_log.update_kill(monster.ch)
        self.add_float(monster.x, monster.y, f"+{monster.xp_value} XP", COL_XP)

        reward = rules.roll_kill_reward(monster, self.depth, self.player.greedy)
        self.player.gold += reward.gold
        self.total_gold_earned += reward.gold
        self.log(f"+{reward.gold} золота", COL_GOLD)
        self.add_float(monster.x, monster.y - 0.6, f"+{reward.gold}g", COL_GOLD)
        for item_key in reward.drops:
            self._place_drop(monster.x, monster.y, item_key)

        if monster.boss and self.depth == MAX_DEPTH:
            self.victory = True
            self.log("ДРЕВНИЙ ДРАКОН ПОБЕЖДЁН! ВЫ ПОБЕДИЛИ!", COL_WIN)

        self.dungeon.monsters = [m for m in self.dungeon.monsters if m.hp > 0]
        for level in rules.give_xp(self.player, monster.xp_value):
            self._on_level_up(level)

    def _place_drop(self, x, y, item_key):
        rules.place_loot(self.dungeon, self.player.inventory, x, y, item_key,
                         avoid={(self.player.x, self.player.y)})

    def level_up(self):
        """Повышает уровень на 1 (совместимость с квестами и тестами)."""
        level = rules.level_up_once(self.player)
        self._on_level_up(level)

    def _on_level_up(self, level):
        from audio import Audio
        Audio.play('level_up')
        p = self.player
        self.log(f"Уровень {level}! +5 очков, HP восстановлено.", COL_GOLD)
        n = self._lvl_float_idx % 4
        self._lvl_float_idx = n + 1
        self.add_float(p.x, p.y - n * 0.8, f"LVL {level}", COL_GOLD)

    # ════════════════════════ AI ════════════════════════
    def monsters_turn(self):
        """Все мобы действуют (используется тестами и как «полный ход»)."""
        for m in list(self.dungeon.monsters):
            if self.dead:
                return
            self._monster_act(m)

    def monsters_update(self, dt):
        """Real-time ходы: у каждого моба свой таймер, без общего залпа."""
        for m in list(self.dungeon.monsters):
            if self.dead:
                return
            if m.hp <= 0:
                continue
            # Дыхание босса — отдельный таймер, вне обычного хода
            if m.boss:
                distance = max(abs(self.player.x - m.x),
                               abs(self.player.y - m.y))
                damage = rules.boss_breath_tick(m, self.player, distance, dt)
                if damage:
                    self._boss_breath_fx(m, damage)
            m.act_timer -= dt
            if m.act_timer > 0:
                continue
            m.act_timer = rules.action_interval(m)
            self._monster_act(m)

    def _boss_breath_fx(self, boss, damage):
        from audio import Audio
        Audio.play('fireball')
        self.log(f"{boss.name}: дыхание! {damage} урона", COL_DMG)
        self.add_float(self.player.x, self.player.y, f"-{damage}", COL_DMG)
        if self.player.hp <= 0 and not self.dead:
            self._player_death()

    def _player_death(self):
        from audio import Audio
        Audio.play('death_player')
        self.dead = True
        self.death_time = time.time()
        self.log("Вы погибли. R — рестарт, F9 — загрузка.", COL_DMG)

    def _monster_act(self, m):
        if m.hp <= 0:
            return
        if m.has_status('slow'):
            return  # замедлен — пропускает ход

        ability = getattr(m, 'ability', None)
        dx = self.player.x - m.x
        dy = self.player.y - m.y
        dist = max(abs(dx), abs(dy))

        if ability == 'healer':
            if self._healer_action(m):
                return
        if ability == 'ranged' and 2 <= dist <= 4:
            if has_los(self.dungeon, m.x, m.y, self.player.x, self.player.y):
                self.attack(m, self.player)
                return
        if dist <= 1:
            self.attack(m, self.player)
        elif dist <= 8 and has_los(self.dungeon, m.x, m.y,
                                   self.player.x, self.player.y):
            mx = (dx > 0) - (dx < 0)
            my = (dy > 0) - (dy < 0)
            if abs(dx) > abs(dy):
                if not self.try_move(m, m.x + mx, m.y):
                    self.try_move(m, m.x, m.y + my)
            else:
                if not self.try_move(m, m.x, m.y + my):
                    self.try_move(m, m.x + mx, m.y)
        elif m.boss and dist <= 15:
            blocked = {(o.x, o.y) for o in self.dungeon.monsters
                       if o is not m and o.hp > 0}
            nxt = bfs_toward_player(self.dungeon, (m.x, m.y),
                                    (self.player.x, self.player.y), blocked)
            if nxt:
                m.x, m.y = nxt

    def _tick_player_statuses(self):
        """Игрок получает урон/лечение от статусов в начале хода монстров."""
        for event in rules.tick_statuses(self.player):
            if event.damage:
                self.log(f"{event.name}: -{event.damage} HP", event.color)
                self.add_float(self.player.x, self.player.y,
                               f"-{event.damage}", event.color)
            if event.heal:
                self.log(f"{event.name}: +{event.heal} HP", event.color)
                self.add_float(self.player.x, self.player.y,
                               f"+{event.heal}", event.color)
        if self.player.hp <= 0 and not self.dead:
            self.dead = True
            self.log("Вы погибли от статус-эффекта. R — рестарт.", COL_DMG)

    def _tick_monster_statuses(self, m):
        """Монстр получает урон/лечение от статусов."""
        for event in rules.tick_statuses(m):
            if event.damage:
                self.log(f"{m.name}: {event.name} -{event.damage} HP",
                         event.color)
                self.add_float(m.x, m.y, f"-{event.damage}", event.color)
            if event.heal:
                self.add_float(m.x, m.y, f"+{event.heal}", event.color)
        if m.hp <= 0 and m in self.dungeon.monsters:
            self._kill_monster(m)

    def _tick_statuses(self):
        """Тик всех статусов — по таймеру."""
        self._tick_player_statuses()
        if self.dead:
            return
        for m in list(self.dungeon.monsters):
            if m.hp > 0:
                self._tick_monster_statuses(m)

    def _healer_action(self, healer):
        """Шаман лечит раненого союзника. Возвращает True, если походил."""
        target = None
        best_ratio = 1.0
        for m in self.dungeon.monsters:
            if m is healer or m.hp <= 0 or m.hp >= m.max_hp:
                continue
            dist = max(abs(m.x - healer.x), abs(m.y - healer.y))
            if dist > 5:
                continue
            ratio = m.hp / m.max_hp
            if ratio < best_ratio and has_los(self.dungeon, healer.x, healer.y,
                                              m.x, m.y):
                target = m
                best_ratio = ratio
        if not target:
            return False
        heal = min(8, target.max_hp - target.hp)
        target.hp += heal
        self.log(f"{healer.name} лечит {target.name}: +{heal}",
                 (120, 240, 120))
        self.add_float(target.x, target.y, f"+{heal}", (120, 240, 120))
        return True

    def try_move(self, m, x, y):
        from core.config import MAP_W, MAP_H
        from core.config import Tile
        if not (0 <= x < MAP_W and 0 <= y < MAP_H):
            return False
        if self.dungeon.tiles[y][x] == Tile.WALL:
            return False
        if x == self.player.x and y == self.player.y:
            return False
        # Торговец — только в подземелье
        if (self.world_mode == 'dungeon' and self.merchant
                and x == self.merchant.x and y == self.merchant.y):
            return False
        # Старейшина — только в открытом мире
        if (self.world_mode == 'overworld' and self.elder
                and x == self.elder.x and y == self.elder.y):
            return False
        # Другие мобы
        for o in self.dungeon.monsters:
            if o is not m and o.hp > 0 and o.x == x and o.y == y:
                return False
        m.x, m.y = x, y
        return True

    # ════════════════════════ МАРШРУТИЗАЦИЯ INPUT ════════════════════════
    def handle_key(self, key, scancode=0, unicode='', mods=0):
        # Меню и все его подэкраны
        if self.screen_mode in ('menu', 'menu_saves', 'menu_settings',
                                'class_select', 'menu_controls',
                                'menu_multiplayer', 'menu_mp_join'):
            self.menu.handle_key(key, scancode, unicode, mods);
            return
        if self.screen_mode == 'shop':
            self.shop.handle_key(key); return
        if self.screen_mode == 'craft':
            self.craft.handle_key(key); return
        if self.screen_mode == 'dialogue':
            self.dialogue.handle_key(key, scancode);
            return
        if self.screen_mode == 'quests':
            self.quests.handle_key(key, scancode);
            return
        if self.screen_mode == 'stats_run':
            self.stats_run.handle_key(key, scancode);
            return

        # Управление в игре
        if key == pygame.K_F9:
            self.load(); return
        if key == pygame.K_F5 and not (self.dead or self.victory):
            self.save(); return
        if self.dead or self.victory:
            if key == pygame.K_r:
                self.new_game()
            return

        # Скролл лога — только в игровом режиме
        if self.screen_mode == 'game':
            if scancode == 0x2F or key in (pygame.K_PAGEUP, pygame.K_LEFTBRACKET):
                self.scroll_log(3);
                return
            if scancode == 0x30 or key in (pygame.K_PAGEDOWN, pygame.K_RIGHTBRACKET):
                self.scroll_log(-3);
                return
            if key == pygame.K_HOME:
                self.scroll_log(10_000);
                return
            if key == pygame.K_END:
                self.log_scroll = 0;
                return

        # Экраны статов / инвентаря
        if self.screen_mode == 'stats':
            if key in (pygame.K_c, pygame.K_ESCAPE):
                self.screen_mode = 'game'
            elif key == pygame.K_i:
                self.screen_mode = 'inventory'
            elif pygame.K_1 <= key <= pygame.K_5:
                self.allocate_stat(STAT_ORDER[key - pygame.K_1])
            return
        if self.screen_mode == 'inventory':
            if key in (pygame.K_i, pygame.K_ESCAPE):
                self.screen_mode = 'game'
            elif key == pygame.K_c:
                self.screen_mode = 'stats'
            elif key == pygame.K_u: self.unequip_weapon()
            elif key == pygame.K_v: self.unequip_armor()
            elif key == pygame.K_h: self.unequip_head()
            elif key == pygame.K_b: self.unequip_legs()
            elif key == pygame.K_q:
                self.inv_prev_page()
            elif key == pygame.K_e:
                self.inv_next_page()
            elif pygame.K_1 <= key <= pygame.K_9:
                self.use_item_by_index(key - pygame.K_1)
            return

        # Открытие экранов
        if key == pygame.K_c:
            self.screen_mode = 'stats'; return
        if key == pygame.K_i:
            self.screen_mode = 'inventory'
            self.inv_page = min(self.inv_page, self.inv_pages_count() - 1)
            return
        if key == pygame.K_t:
            if (self.quest_board
                    and max(abs(self.player.x - self.quest_board.x),
                            abs(self.player.y - self.quest_board.y)) <= 1):
                self.dialogue.open('quest_board')
                return
            if (self.world_mode == 'overworld' and self.elder
                    and max(abs(self.player.x - self.elder.x),
                            abs(self.player.y - self.elder.y)) <= 1):
                self.dialogue.open('elder')
                return
            if (self.world_mode == 'dungeon' and self.merchant
                    and max(abs(self.player.x - self.merchant.x),
                            abs(self.player.y - self.merchant.y)) <= 1):
                self.dialogue.open('merchant')
                return
            self.log("Рядом нет NPC.", COL_DIM)
            return
        if key == pygame.K_l:
            self.quests.open();
            return
        if key == pygame.K_k:
            self.craft.open(); return
        if key == pygame.K_h:
            self.quick_potion()
            self.update_fov()
            return
        if key == pygame.K_m:
            self.settings['show_minimap'] = not self.settings.get('show_minimap', True)
            save_settings(self.settings)
            return
        if key == pygame.K_TAB:
            self.stats_run.open();
            return

    def handle_mouse_motion(self, pos):
        self.mouse_pos = pos
        if self.screen_mode in ('menu', 'menu_saves', 'menu_settings',
                                'class_select', 'menu_controls',
                                'menu_multiplayer', 'menu_mp_join'):
            self.menu.handle_mouse_motion(pos)

    def handle_mouse_click(self, pos):
        # Меню
        if self.screen_mode in ('menu', 'menu_saves', 'menu_settings',
                                'class_select', 'menu_controls',
                                'menu_multiplayer', 'menu_mp_join'):
            return self.menu.handle_mouse_click(pos)

        # Крестик — ПЕРВЫМ, до любых других обработчиков
        close = getattr(self, '_modal_close_rect', None)
        if close and self.screen_mode in ('stats', 'inventory', 'craft',
                                          'quests', 'shop', 'stats_run',
                                          'dialogue'):
            if self._hit(close, pos):
                self.screen_mode = 'game'
                return True

        # Диалог — после крестика
        if self.screen_mode == 'dialogue':
            return self.dialogue.handle_mouse_click(pos)

        if self.screen_mode == 'inventory':
            return self._handle_inventory_click(pos)
        if self.screen_mode == 'stats':
            return self._handle_stats_click(pos)
        if self.screen_mode == 'craft':
            return self._handle_craft_click(pos)
        if self.screen_mode == 'shop':
            return self._handle_shop_click(pos)
        if self.screen_mode == 'quests':
            return self.quests.handle_mouse_click(pos)
        if self.screen_mode != 'game':
            return False

        mx, my = pos

        # Панель иконок сверху
        for name, rect in self._toolbar_rects():
            x, y, w, h = rect
            if x <= mx <= x + w and y <= my <= y + h:
                if name == 'inventory':
                    self.screen_mode = 'inventory'
                    self.inv_page = min(self.inv_page, self.inv_pages_count() - 1)
                elif name == 'craft':
                    self.craft.open()
                elif name == 'stats':
                    self.screen_mode = 'stats'
                elif name == 'quests':
                    self.quests.open()
                return True

        # Клик по NPC на карте
        from core.config import TILE, MAP_W, MAP_H
        cam_x = max(0, min(self.player.x - config.VIEW_W // 2, MAP_W - config.VIEW_W))
        cam_y = max(0, min(self.player.y - config.VIEW_H // 2, MAP_H - config.VIEW_H))
        ox, oy = config.view_origin()
        field_w = config.VIEW_W * TILE
        field_h = config.VIEW_H * TILE
        if not (ox <= mx < ox + field_w and oy <= my < oy + field_h):
            return False
        tx = (mx - ox) // TILE + cam_x
        ty = (my - oy) // TILE + cam_y

        if (self.world_mode == 'overworld' and self.elder
                and (tx, ty) == (self.elder.x, self.elder.y)
                and (tx, ty) in self.visible):
            if max(abs(self.player.x - tx), abs(self.player.y - ty)) <= 1:
                self.dialogue.open('elder')
            else:
                self.log("Подойди ближе.", COL_DIM)
            return True
        if (self.world_mode == 'dungeon' and self.merchant
                and (tx, ty) == (self.merchant.x, self.merchant.y)
                and (tx, ty) in self.visible):
            if max(abs(self.player.x - tx), abs(self.player.y - ty)) <= 1:
                self.dialogue.open('merchant')
            else:
                self.log("Подойди ближе.", COL_DIM)
            return True

        return False

    def _hit(self, rect, pos):
        x, y, w, h = rect
        return x <= pos[0] <= x + w and y <= pos[1] <= y + h

    def _handle_inventory_click(self, pos):
        hits = self._ui_hits.get('inventory', {})
        for action, rect in hits.get('pages', []):
            if self._hit(rect, pos):
                if action == 'prev':
                    self.inv_prev_page()
                else:
                    self.inv_next_page()
                return True
        for slot, rect in hits.get('equip', []):
            if self._hit(rect, pos):
                if slot == 'weapon':
                    self.unequip_weapon()
                elif slot == 'armor':
                    self.unequip_armor()
                elif slot == 'head':
                    self.unequip_head()
                elif slot == 'legs':
                    self.unequip_legs()
                return True
        for key, rect in hits.get('items', []):
            if self._hit(rect, pos):
                self.use_item(key)
                self.inv_page = min(self.inv_page, self.inv_pages_count() - 1)
                return True
        return False

    def _handle_stats_click(self, pos):
        hits = self._ui_hits.get('stats', {})
        for stat, rect in hits.get('plus', []):
            if self._hit(rect, pos):
                self.allocate_stat(stat)
                return True
        return False

    def _handle_craft_click(self, pos):
        hits = self._ui_hits.get('craft', {})
        for tab, rect in hits.get('tabs', []):
            if self._hit(rect, pos):
                self.craft.set_tab(tab)
                return True
        if self._hit(hits.get('prev', (0, 0, 0, 0)), pos):
            self.craft.prev_page()
            return True
        if self._hit(hits.get('next', (0, 0, 0, 0)), pos):
            self.craft.next_page()
            return True
        for idx, rect in hits.get('hammer', []):
            if self._hit(rect, pos):
                self.craft.craft(idx)
                return True
        return False

    def _handle_shop_click(self, pos):
        """Клик по строке товара/предмета в магазине."""
        hits = self._ui_hits.get('shop', {})
        for idx, rect in hits.get('rows', []):
            if self._hit(rect, pos):
                if self.shop_submode == 'buy':
                    self.buy_item(idx)
                else:
                    self.sell_item(idx)
                return True
        return False

    def _toolbar_rects(self):
        from core.config import TOOLBAR_BTN, TOOLBAR_GAP, TOOLBAR_MARGIN
        result = []
        for i, name in enumerate(('inventory', 'craft', 'stats', 'quests')):
            x = TOOLBAR_MARGIN + i * (TOOLBAR_BTN + TOOLBAR_GAP)
            y = TOOLBAR_MARGIN
            result.append((name, (x, y, TOOLBAR_BTN, TOOLBAR_BTN)))
        return result

    def handle_input_continuous(self, keys):
        """Вызывается каждый кадр из main. Движение и атака по зажатым клавишам."""
        if self.dead or self.victory or self.screen_mode != 'game':
            return
        if self.player_move_timer > 0 and self.player_attack_timer > 0:
            return

        # ── Движение (приоритет: вертикаль, потом горизонталь) ──
        if self.player_move_timer <= 0:
            dx = dy = 0
            if keys[pygame.K_w] or keys[pygame.K_UP]:
                dy = -1
            elif keys[pygame.K_s] or keys[pygame.K_DOWN]:
                dy = 1
            elif keys[pygame.K_a] or keys[pygame.K_LEFT]:
                dx = -1
            elif keys[pygame.K_d] or keys[pygame.K_RIGHT]:
                dx = 1
            if dx or dy:
                if self.move_player(dx, dy):
                    self.player_move_timer = PLAYER_MOVE_COOLDOWN
                    self.update_fov()

        # ── Атака пробелом ──
        if keys[pygame.K_SPACE] and self.player_attack_timer <= 0:
            self.player_attack()
            self.player_attack_timer = PLAYER_ATTACK_COOLDOWN

    def death_summary(self):
        """Сводка для death recap."""
        import time
        elapsed = 0
        if getattr(self, 'start_time', None):
            end = getattr(self, 'death_time', time.time())
            elapsed = int(end - self.start_time)
        return {
            'kills': getattr(self, 'kills', 0),
            'boss_kills': getattr(self, 'boss_kills', 0),
            'depth': self.depth,
            'world_mode': self.world_mode,
            'level': self.player.level,
            'gold': self.player.gold,
            'time_seconds': elapsed,
            'last_messages': [m.text for m in self.messages[-5:]],
        }

    def run_summary(self):
        import time as _t
        elapsed = int(_t.time() - getattr(self, 'start_time', _t.time()))
        return {
            'kills': self.kills,
            'boss_kills': self.boss_kills,
            'gold': self.player.gold,  # ← текущее
            'earned_gold': self.total_gold_earned,  # ← добыто за забег
            'depth': self.depth,
            'level': self.player.level,
            'time_seconds': elapsed,
            'kills_by_ch': dict(self.total_kills_by_ch),
            'items_collected': dict(self.total_items_collected),
            'potions_used': self.total_potions_used,
            'crafts': self.total_crafts,
            'quests': self.total_quests_turned_in,
            'quests_active': len(self.quest_log.active),
        }

    # ════════════════════════ ОБЁРТКИ (для renderer и тестов) ════════════════════════
    def open_menu(self, from_game=False):
        from audio import Audio
        Audio.play('menu_open')
        self.menu.open(from_game)

    def menu_button_rects(self):
        return self.menu.button_rects()

    def menu_return_to_game(self):
        self.menu.return_to_game()

    def open_shop(self):
        if not self.merchant:
            self.log("На этом этаже нет торговца.", COL_DIM);
            return
        if max(abs(self.player.x - self.merchant.x),
               abs(self.player.y - self.merchant.y)) > 1:
            self.log("Подойди ближе к торговцу.", COL_DIM);
            return
        self.shop.open()

    def buy_item(self, idx):
        self.shop.buy(idx)

    def sell_item(self, idx):
        self.shop.sell(idx)

    def reroll_shop(self):
        self.shop.reroll()

    def refresh_shop(self):
        self.shop.refresh()

    def shop_pages_count(self):
        return self.shop.pages_count()

    def craftable_list(self):
        return self.craft.list()

    def craft_page_items(self):
        return self.craft.page_items()

    def craft_pages_count(self):
        return self.craft.pages_count()

    def craft_tabs(self):
        return self.craft.tabs()

    def craft_item(self, idx):
        self.craft.craft(idx)

    def menu_start_new(self):
        self.menu.start_new()

    def menu_load_slot(self, slot):
        self.menu.load_slot(slot)

    def class_select_confirm(self):
        self.menu.confirm_class()

    def _main_menu_labels(self):
        return self.menu.labels()

    @property
    def menu_selection(self):
        return self.menu.selection

    @menu_selection.setter
    def menu_selection(self, v):
        self.menu.selection = v

    # ════════════════════════ UPDATE / DRAW ════════════════════════
    def update(self, dt):
        # ── Анимации (работают даже в паузе, чтобы idle не замирал) ──
        self.player.anim_time += dt
        if self.player.attack_anim > 0:
            self.player.attack_anim = max(0.0, self.player.attack_anim - dt / 0.25)
        if self.merchant:
            self.merchant.anim_time += dt
        if self.elder:
            self.elder.anim_time += dt
        for m in self.dungeon.monsters:
            m.anim_time += dt
            if m.attack_anim > 0:
                m.attack_anim = max(0.0, m.attack_anim - dt / 0.25)

        # Floating texts
        for f in self.floats:
            f.life -= dt
            f.y -= 40 * dt
        self.floats = [f for f in self.floats if f.life > 0]

        # Пауза: смерть/победа/меню
        if self.dead or self.victory:
            return
        if self.screen_mode != 'game':
            self.player_move_timer = 0.0
            self.player_attack_timer = 0.0
            self.status_tick_timer = STATUS_TICK_INTERVAL
            self.regen_timer = REGEN_TICK_INTERVAL
            return

        # Тикаем таймеры
        self.player_move_timer = max(0.0, self.player_move_timer - dt)
        self.player_attack_timer = max(0.0, self.player_attack_timer - dt)
        self.status_tick_timer -= dt
        self.regen_timer -= dt

        # ── Защита: FOV пересчитывается сразу при смене позиции ──
        if (self.player.x, self.player.y) != self._last_fov_pos:
            self.update_fov()
            self._last_fov_pos = (self.player.x, self.player.y)

        # Реген
        if self.regen_timer <= 0:
            self.regen_timer += REGEN_TICK_INTERVAL
            if self.player.regen and self.player.hp < self.player.max_hp:
                self.player.hp += 1

        # Тик статусов
        if self.status_tick_timer <= 0:
            self.status_tick_timer += STATUS_TICK_INTERVAL
            self._tick_statuses()
            if self.dead:
                return

        # Ходы мобов: у каждого свой таймер (без синхронного «залпа»)
        self.monsters_update(dt)
        if self.dead:
            return
        # FOV уже пересчитан выше при смене позиции игрока. Мобы и статусы
        # не меняют геометрию обзора, поэтому второй вызов каждый кадр
        # был чистой потерей ~0.4 мс.

    def draw(self):
        self.renderer.draw(self)

    def _modal_panel_rect(self):
        from core.config import MODAL_MARGIN_X, MODAL_MARGIN_Y
        return (MODAL_MARGIN_X, MODAL_MARGIN_Y,
                config.SCREEN_W - MODAL_MARGIN_X * 2,
                config.SCREEN_H - MODAL_MARGIN_Y * 2)