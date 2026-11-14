"""Серверная логика без pygame. Авторитетное состояние.

Все правила боя/лута/статусов берутся из :mod:`core.rules` — того же
модуля, что использует одиночная игра, поэтому режимы не расходятся.
"""
import random
import json
import os

from core.config import (
    MAP_W, MAP_H, MERCHANT_FLOORS, Tile,
    PLAYER_MOVE_COOLDOWN, PLAYER_ATTACK_COOLDOWN,
    STATUS_TICK_INTERVAL, REGEN_TICK_INTERVAL,
    COL_TEXT, COL_GOLD, COL_XP, COL_DMG, COL_HEAL, COL_MERCH,
)
from core import rules
from core import overworld as ov
from core.data import (BOSSES, make_shop_stock, reroll_price, item_sell_price,
                       item_display_name, xp_to_next, ITEMS,
                       craft_skill_reward, item_category, select_recipe,
                       MONSTERS_BY_CH)
from core.entities import Player, Entity
from core.world import (
    generate_overworld, generate_dungeon, place_entry_tile, has_los,
    bfs_toward_player,
)
from core.save import dungeon_to_dict, dungeon_from_dict


class GameCore:
    """Авторитетное состояние. Все мутации — через apply_input / update."""

    def __init__(self, seed=42):
        random.seed(seed)
        self.seed = seed
        self.tick = 0
        # Растёт при каждой смене карты — сервер по нему понимает,
        # что клиентам пора переслать тайлы (id() переиспользуется!)
        self.world_serial = 0

        # Мир
        self.overworld = generate_overworld()
        self.dungeon = self.overworld
        self.world_mode = 'overworld'
        self.depth = 0
        self.dungeon_cache = {}
        self.merchant = None
        self.shop_stock = []

        # Игроки
        self.players = {}                 # {pid: Player}
        self.move_timers = {}             # {pid: seconds}
        self.attack_timers = {}
        self.regen_timers = {}
        self.status_tick_timer = STATUS_TICK_INTERVAL

        # События для лога клиентов (копится между снапшотами)
        self.events = []
        # Стабильные id мобов — клиент по ним интерполирует движение
        self._next_mid = 1
        self._monster_ids = {}
        # Последний отправленный инвентарь: в снапшот попадает только
        # изменившийся, а не весь словарь 20 раз в секунду.
        self._inventory_cache = {}

        # Elder в деревне, на центральной площади
        self.elder = None
        ex, ey = ov.ELDER_POS
        if self.overworld.tiles[ey][ex] == Tile.FLOOR:
            self.elder = Entity(ex, ey, 1, 1, 0, 0,
                                "Старейшина", "E", (240, 210, 100), 0)
            self.elder.is_npc = True
            self.elder.npc_type = 'elder'
        else:
            spot = rules.find_free_tile(
                self.overworld, ov.VILLAGE_CENTER[0], ov.VILLAGE_CENTER[1])
            if spot:
                self.elder = Entity(spot[0], spot[1], 1, 1, 0, 0,
                                    "Старейшина", "E", (240, 210, 100), 0)
                self.elder.is_npc = True
                self.elder.npc_type = 'elder'
        rules.init_action_timers(self.overworld.monsters)

    # ════════════════════════ PLAYERS ════════════════════════
    def add_player(self, pid, class_key='warrior'):
        p = Player(class_key=class_key)
        cx, cy = self.dungeon.rooms[0].center() if self.dungeon.rooms \
            else (MAP_W // 2, MAP_H // 2)
        occupied = {(o.x, o.y) for o in self.players.values()}
        spot = rules.find_free_tile(self.dungeon, cx, cy, occupied)
        p.x, p.y = spot if spot else (cx, cy)
        p.anim_time = 0.0
        p.attack_anim = 0.0
        p.facing = (1, 0)
        self.players[pid] = p
        self.move_timers[pid] = 0.0
        self.attack_timers[pid] = 0.0
        self.regen_timers[pid] = REGEN_TICK_INTERVAL
        return p

    def remove_player(self, pid):
        self.players.pop(pid, None)
        self.move_timers.pop(pid, None)
        self.attack_timers.pop(pid, None)
        self.regen_timers.pop(pid, None)
        self._inventory_cache.pop(pid, None)

    # ════════════════════════ СОБЫТИЯ ════════════════════════
    def _event(self, text, color=COL_TEXT):
        if len(self.events) < 40:
            self.events.append({'text': text, 'color': list(color)})

    def _take_events(self):
        events, self.events = self.events, []
        return events

    def chat(self, pid, text):
        p = self.players.get(pid)
        if p is None:
            return
        text = ' '.join(str(text).split())[:120]
        if text:
            self._event(f"{p.class_name}: {text}", COL_TEXT)

    def _free_spot(self):
        """Свободная клетка рядом с точкой входа текущей карты."""
        if self.dungeon.rooms:
            cx, cy = self.dungeon.rooms[0].center()
        else:
            cx, cy = MAP_W // 2, MAP_H // 2
        occupied = {(p.x, p.y) for p in self.players.values()}
        occupied.update((m.x, m.y) for m in self.dungeon.monsters if m.hp > 0)
        return rules.find_free_tile(self.dungeon, cx, cy, occupied)

    def _player_to_dict(self, p):
        return {
            'x': p.x, 'y': p.y, 'hp': p.hp,
            'level': p.level, 'xp': p.xp, 'xp_next': p.xp_next,
            'free_points': p.free_points,
            'str': p.str, 'agi': p.agi, 'vit': p.vit, 'luk': p.luk,
            'df_stat': p.df_stat,
            'weapon': p.weapon, 'armor': p.armor,
            'head': p.head, 'legs': p.legs,
            'inventory': dict(p.inventory),
            'gold': p.gold,
            'class_key': p.class_key,
            'double_strike': p.double_strike,
            'regen': p.regen, 'vampire': p.vampire,
            'thorns': p.thorns, 'greedy': p.greedy,
            'skills': {k: dict(v) for k, v in getattr(p, 'skills', {}).items()},
            'statuses': dict(p.statuses),
        }

    def _player_from_dict(self, d):
        p = Player(class_key=d.get('class_key', 'warrior'))
        p.x, p.y = d['x'], d['y']
        p.level = d['level']
        p.xp = d['xp']
        p.xp_next = xp_to_next(p.level)
        p.free_points = d['free_points']
        p.str, p.agi, p.vit, p.luk = d['str'], d['agi'], d['vit'], d['luk']
        p.df_stat = d['df_stat']
        p.weapon = d.get('weapon')
        p.armor = d.get('armor')
        p.head = d.get('head')
        p.legs = d.get('legs')
        p.inventory = dict(d.get('inventory', {}))
        p.gold = d.get('gold', 0)
        p.double_strike = d.get('double_strike', False)
        p.regen = d.get('regen', False)
        p.vampire = d.get('vampire', False)
        p.thorns = d.get('thorns', False)
        p.greedy = d.get('greedy', False)
        from core.data import default_skills
        p.skills = default_skills()
        for skill, val in (d.get('skills') or {}).items():
            if skill in p.skills and isinstance(val, dict):
                p.skills[skill]['level'] = max(1, int(val.get('level', 1)))
                p.skills[skill]['xp'] = max(0, int(val.get('xp', 0)))
        p.statuses = dict(d.get('statuses', {}))
        p.recalc()
        p.hp = min(d['hp'], p.max_hp)
        p.anim_time = 0.0
        p.attack_anim = 0.0
        p.facing = (1, 0)
        return p

    # ════════════════════════ SAVE / LOAD ════════════════════════
    def save_world(self, path):
        if self.world_mode == 'dungeon':
            self.dungeon_cache[self.depth] = {
                'dungeon': self.dungeon,
                'merchant': self.merchant,
                'shop_stock': self.shop_stock,
            }
        data = {
            'version': 6,
            'world_mode': self.world_mode,
            'depth': self.depth,
            'overworld': dungeon_to_dict(self.overworld),
            'dungeon': (dungeon_to_dict(self.dungeon)
                        if self.world_mode == 'dungeon' else None),
            'dungeon_cache': {
                str(d): {
                    'dungeon': dungeon_to_dict(c['dungeon']),
                    'merchant': ({'x': c['merchant'].x, 'y': c['merchant'].y}
                                 if c.get('merchant') else None),
                    'shop_stock': c.get('shop_stock', []),
                }
                for d, c in self.dungeon_cache.items()
                if d != (self.depth if self.world_mode == 'dungeon' else None)
            },
            'merchant': ({'x': self.merchant.x, 'y': self.merchant.y}
                         if self.merchant else None),
            'shop_stock': self.shop_stock,
            'elder': ({'x': self.elder.x, 'y': self.elder.y}
                      if self.elder else None),
        }
        tmp = path + '.tmp'
        with open(tmp, 'w', encoding='utf-8') as f:
            json.dump(data, f, ensure_ascii=False)
        os.replace(tmp, path)

    def load_world(self, path):
        if not os.path.exists(path):
            return False
        try:
            with open(path, 'r', encoding='utf-8') as f:
                data = json.load(f)
            if 'overworld' not in data:
                # Сейв старого формата (v5): хранил только текущую карту
                return self._load_world_legacy(data)
            self.overworld = dungeon_from_dict(data['overworld'],
                                              is_overworld=True)
            self.world_mode = data['world_mode']
            self.depth = int(data['depth'])
            if self.world_mode == 'dungeon' and data.get('dungeon'):
                self.dungeon = dungeon_from_dict(data['dungeon'])
                place_entry_tile(self.dungeon, self.depth)
            else:
                self.dungeon = self.overworld
            self.dungeon_cache = {}
            for depth_str, cached in (data.get('dungeon_cache') or {}).items():
                depth = int(depth_str)
                merchant = None
                if cached.get('merchant'):
                    merchant = self._make_merchant(dict(cached['merchant']))
                self.dungeon_cache[depth] = {
                    'dungeon': dungeon_from_dict(cached['dungeon']),
                    'merchant': merchant,
                    'shop_stock': cached.get('shop_stock', []),
                }
            if self.world_mode == 'dungeon':
                self.dungeon_cache[self.depth] = {
                    'dungeon': self.dungeon,
                    'merchant': self.merchant,
                    'shop_stock': self.shop_stock,
                }
            # Торговец и его сток текущего этажа
            self.merchant = (self._make_merchant(dict(data['merchant']))
                             if data.get('merchant') else None)
            self.shop_stock = data.get('shop_stock') or []
            # Старейшина — привязан к открытому миру
            self.elder = (self._make_elder(dict(data['elder']))
                          if data.get('elder') else self.elder)
            if self.elder is None and self.overworld.stairs_pos:
                spot = rules.find_free_tile(
                    self.overworld, self.overworld.stairs_pos[0] + 1,
                    self.overworld.stairs_pos[1] + 1)
                if spot:
                    self.elder = self._make_elder({'x': spot[0], 'y': spot[1]})
            rules.init_action_timers(self.dungeon.monsters)
            return True
        except Exception as e:
            print(f"[CORE] load_world: {e}")
            return False

    def _load_world_legacy(self, data):
        """Сейв v5: есть только текущая карта, без отдельного overworld."""
        current = dungeon_from_dict(data['dungeon'])
        self.world_mode = data.get('world_mode', 'overworld')
        self.depth = int(data.get('depth', 0))
        self.dungeon_cache = {}
        self.shop_stock = []
        if self.world_mode == 'dungeon':
            self.overworld = generate_overworld()
            self.dungeon = current
            place_entry_tile(self.dungeon, self.depth)
            self.merchant = (self._make_merchant(dict(data['merchant']))
                             if data.get('merchant') else None)
        else:
            self.overworld = current
            self.dungeon = current
            self.merchant = None
        # Старейшину ставим заново — старая позиция относилась к другой карте
        self.elder = None
        if self.overworld.stairs_pos:
            spot = rules.find_free_tile(self.overworld,
                                        self.overworld.stairs_pos[0] + 1,
                                        self.overworld.stairs_pos[1] + 1)
            if spot:
                self.elder = self._make_elder({'x': spot[0], 'y': spot[1]})
        rules.init_action_timers(self.dungeon.monsters)
        print("[CORE] world loaded in legacy v5 format")
        return True

    @staticmethod
    def _make_merchant(pos):
        merchant = Entity(pos['x'], pos['y'], 1, 1, 0, 0,
                          "Торговец", "M", (240, 200, 100), 0)
        merchant.is_merchant = True
        merchant.is_npc = True
        merchant.npc_type = 'merchant'
        return merchant

    @staticmethod
    def _make_elder(pos):
        elder = Entity(pos['x'], pos['y'], 1, 1, 0, 0,
                       "Старейшина", "E", (240, 210, 100), 0)
        elder.is_npc = True
        elder.npc_type = 'elder'
        return elder

    # ════════════════════════ ITEMS ════════════════════════
    def use_item(self, pid, key):
        p = self.players.get(pid)
        if p is None or not key:
            return
        result = rules.use_item(p, key, self.dungeon.monsters)
        if result is None or not result.ok:
            return
        if result.kind == 'fireball':
            self._resolve_victims(p, result.victims)

    def _resolve_victims(self, killer, victims):
        for monster, _damage in victims:
            if monster.hp <= 0 and monster in self.dungeon.monsters:
                self._kill_monster(killer, monster)

    def unequip(self, pid, slot):
        p = self.players.get(pid)
        if p is None:
            return
        if slot == 'weapon' and p.weapon:
            key = p.weapon
            p.weapon = None
            p.inventory[key] = p.inventory.get(key, 0) + 1
            p.recalc()
        elif slot == 'armor' and p.armor:
            key = p.armor
            p.armor = None
            p.inventory[key] = p.inventory.get(key, 0) + 1
            p.recalc()

    # ════════════════════════ STATS / CRAFT / SHOP ════════════════════════
    def allocate_stat(self, pid, stat):
        p = self.players.get(pid)
        if p is None or p.free_points <= 0:
            return
        if stat not in ('str', 'agi', 'vit', 'luk', 'df_stat'):
            return
        setattr(p, stat, getattr(p, stat) + 1)
        p.free_points -= 1
        p.recalc()

    def craft_item(self, pid, idx=-1, key=None, recipe_index=None):
        p = self.players.get(pid)
        if p is None:
            return False
        count = 1
        if key is not None:
            item = ITEMS.get(key)
            if not item or not ('recipe' in item or 'recipes' in item):
                return False
            category = item_category(key)
            _mats, count, _ri = select_recipe(key, p.inventory, recipe_index)
            ok, _reason = rules.craft(key, p.inventory, p.level, recipe_index)
        else:
            recipes = rules.craftable_list(p.inventory, p.level)
            if idx < 0 or idx >= len(recipes):
                return False
            key = recipes[idx][0]
            category = recipes[idx][4]
            recipe_index = recipes[idx][5]
            count = recipes[idx][6]
            ok, _reason = rules.craft(key, p.inventory, p.level, recipe_index)
        if ok:
            xp = craft_skill_reward(key, count)
            rules.give_skill_xp(p, category, xp)
            self._event(f"{p.class_name} создал: {item_display_name(key)}",
                        COL_GOLD)
        return ok

    def _merchant_in_reach(self, player):
        return (self.merchant is not None
                and max(abs(player.x - self.merchant.x),
                        abs(player.y - self.merchant.y)) <= 1)

    def buy_item(self, pid, index):
        p = self.players.get(pid)
        if p is None or not self._merchant_in_reach(p):
            return False
        ok, _reason, price = rules.buy_from_stock(self.shop_stock, index, p)
        if ok:
            key = self.shop_stock[index]['key']
            self._event(f"{p.class_name} купил: {item_display_name(key)} "
                        f"за {price} з.", COL_MERCH)
        return ok

    def sell_item(self, pid, index):
        p = self.players.get(pid)
        if p is None or not self._merchant_in_reach(p):
            return False
        items = sorted(p.inventory.items())
        if not (0 <= index < len(items)):
            return False
        key = items[index][0]
        price = item_sell_price(key)
        if price <= 0:
            return False
        rules.consume(p.inventory, key)
        p.gold += price
        self._event(f"{p.class_name} продал: {item_display_name(key)} "
                    f"за {price} з.", COL_MERCH)
        return True

    def reroll_shop(self, pid):
        p = self.players.get(pid)
        if p is None or not self._merchant_in_reach(p):
            return False
        price = reroll_price(self.depth)
        if p.gold < price:
            return False
        p.gold -= price
        self._event(f"{p.class_name} обновил ассортимент (-{price} з.)", COL_MERCH)
        self.shop_stock = make_shop_stock(self.depth)
        if p.double_strike:
            self.shop_stock = [s for s in self.shop_stock
                               if s['key'] != 'book_double']
        if self.depth in self.dungeon_cache:
            self.dungeon_cache[self.depth]['shop_stock'] = self.shop_stock
        return True

    # ════════════════════════ INPUT ════════════════════════
    def apply_input(self, pid, msg):
        if pid not in self.players:
            return
        t = msg.get('type')
        if t == 'move':
            if self.move_timers.get(pid, 0) <= 0:
                dx = msg.get('dx', 0)
                dy = msg.get('dy', 0)
                # Клиент может прислать что угодно: разрешаем ровно один
                # шаг по одной оси, иначе это телепорт через полкарты.
                if not self._is_valid_step(dx, dy):
                    return
                if self.move_player(pid, dx, dy):
                    self.move_timers[pid] = PLAYER_MOVE_COOLDOWN
        elif t == 'attack':
            if self.attack_timers.get(pid, 0) <= 0:
                self.player_attack(pid)
                self.attack_timers[pid] = PLAYER_ATTACK_COOLDOWN
        elif t == 'use_item':
            self.use_item(pid, msg.get('key'))
        elif t == 'unequip':
            self.unequip(pid, msg.get('slot'))
        elif t == 'allocate_stat':
            self.allocate_stat(pid, msg.get('stat'))
        elif t == 'craft':
            self.craft_item(pid, msg.get('idx', -1), key=msg.get('key'),
                            recipe_index=msg.get('recipe'))
        elif t == 'buy':
            self.buy_item(pid, msg.get('idx', -1))
        elif t == 'sell':
            self.sell_item(pid, msg.get('idx', -1))
        elif t == 'reroll':
            self.reroll_shop(pid)
        elif t == 'chat':
            self.chat(pid, msg.get('text', ''))
        elif t == 'wait':
            pass

    @staticmethod
    def _is_valid_step(dx, dy):
        """Один шаг по одной оси. (0, 0) разрешён — это подбор под собой."""
        return (isinstance(dx, int) and not isinstance(dx, bool)
                and isinstance(dy, int) and not isinstance(dy, bool)
                and abs(dx) <= 1 and abs(dy) <= 1
                and not (dx and dy))

    def move_player(self, pid, dx, dy):
        p = self.players.get(pid)
        if p is None:
            return False
        if not self._is_valid_step(dx, dy):
            return False
        nx, ny = p.x + dx, p.y + dy
        if not (0 <= nx < MAP_W and 0 <= ny < MAP_H):
            return False
        if self.elder and (nx, ny) == (self.elder.x, self.elder.y):
            return False
        if self.merchant and (nx, ny) == (self.merchant.x, self.merchant.y):
            return False
        for oid, other in self.players.items():
            if oid != pid and (other.x, other.y) == (nx, ny):
                return False
        target = next((m for m in self.dungeon.monsters
                       if m.x == nx and m.y == ny and m.hp > 0), None)
        if target:
            self._strike(p, target)
            return True
        if self.dungeon.tiles[ny][nx] == Tile.WALL:
            return False
        p.x, p.y = nx, ny
        if (nx, ny) in self.dungeon.items:
            result = rules.pickup(self.dungeon, p, nx, ny)
            if result:
                self._on_pickup(p, result)
        tile = self.dungeon.tiles[ny][nx]
        if tile == Tile.STAIRS:
            if self.world_mode == 'overworld':
                self._descend()
            else:
                self._new_dungeon(self.depth + 1)
        elif tile == Tile.EXIT:
            self._ascend()
        elif tile == Tile.UPSTAIRS:
            if self.depth <= 1:
                self._ascend()
            else:
                self._new_dungeon(self.depth - 1, arrive='stairs')
        return True

    def _on_pickup(self, player, result):
        """Квестов в MP пока нет, но подбор попадает в лог отряда."""
        if result.kind == 'gold':
            self._event(f"{player.class_name}: +{result.gold} золота", COL_GOLD)
        else:
            self._event(f"{player.class_name} подобрал: {result.name}", COL_TEXT)

    def player_attack(self, pid):
        p = self.players.get(pid)
        if p is None:
            return
        best, best_dist = None, 999
        for m in self.dungeon.monsters:
            if m.hp <= 0:
                continue
            dist = max(abs(m.x - p.x), abs(m.y - p.y))
            if dist <= 1 and dist < best_dist:
                best, best_dist = m, dist
        if best:
            self._strike(p, best)

    # ════════════════════════ WORLD TRANSITIONS ════════════════════════
    def _descend(self):
        if self.world_mode == 'overworld':
            self.world_mode = 'dungeon'
            self.depth = 0
            self._new_dungeon(1)
        else:
            self._new_dungeon(self.depth + 1)

    def _ascend(self):
        if self.depth <= 1:
            self.world_mode = 'overworld'
            self.depth = 0
            self.dungeon = self.overworld
            self.merchant = None
            self.shop_stock = []
            self._place_players_near(self.overworld,
                                     ov.DUNGEON_POS[0], ov.DUNGEON_POS[1] + 1)
            self.world_serial += 1
            self._event("Возвращение на поверхность", COL_HEAL)
        else:
            self._new_dungeon(self.depth - 1, arrive='stairs')

    def _place_players_near(self, dungeon, cx, cy):
        occupied = set()
        for pid, p in self.players.items():
            spot = rules.find_free_tile(dungeon, cx, cy, occupied)
            if spot:
                p.x, p.y = spot
                occupied.add(spot)

    def _new_dungeon(self, depth, arrive='entry'):
        if self.world_mode == 'dungeon' and depth in self.dungeon_cache:
            self.dungeon_cache[self.depth] = {
                'dungeon': self.dungeon,
                'merchant': self.merchant,
                'shop_stock': self.shop_stock,
            }
        self.world_mode = 'dungeon'
        self.depth = depth
        if depth in self.dungeon_cache:
            cached = self.dungeon_cache[depth]
            self.dungeon = cached['dungeon']
            self.merchant = cached.get('merchant')
            self.shop_stock = cached.get('shop_stock', [])
            place_entry_tile(self.dungeon, depth)
        else:
            self.dungeon = generate_dungeon(depth)
            self.merchant = None
            self.shop_stock = []
            if depth in MERCHANT_FLOORS and self.dungeon.rooms:
                room = self.dungeon.rooms[0]
                candidates = [
                    (room.x + 1, room.y + 1),
                    (room.x + room.w - 2, room.y + 1),
                    (room.x + 1, room.y + room.h - 2),
                    (room.x + room.w - 2, room.y + room.h - 2),
                ]
                random.shuffle(candidates)
                entry = self.dungeon.rooms[0].center()
                for mx, my in candidates:
                    if not (0 <= mx < MAP_W and 0 <= my < MAP_H):
                        continue
                    if self.dungeon.tiles[my][mx] == Tile.WALL:
                        continue
                    if (mx, my) == entry:
                        continue
                    self.merchant = self._make_merchant({'x': mx, 'y': my})
                    self.shop_stock = make_shop_stock(depth)
                    if any(p.double_strike for p in self.players.values()):
                        self.shop_stock = [s for s in self.shop_stock
                                           if s['key'] != 'book_double']
                    break
            self.dungeon_cache[depth] = {
                'dungeon': self.dungeon,
                'merchant': self.merchant,
                'shop_stock': self.shop_stock,
            }

        if arrive == 'stairs' and self.dungeon.stairs_pos:
            cx, cy = self.dungeon.stairs_pos
        else:
            cx, cy = self.dungeon.rooms[0].center()
        self._place_players_near(self.dungeon, cx, cy)
        rules.init_action_timers(self.dungeon.monsters)
        self.world_serial += 1
        if depth > 1:
            self._event(f"Спуск на глубину {depth}", COL_GOLD)
        if depth in BOSSES:
            self._event(f"⚠ {BOSSES[depth]['name']} на глубине {depth}!", COL_DMG)

    # ════════════════════════ COMBAT ════════════════════════
    def _strike(self, a, b):
        result = rules.resolve_attack(a, b)
        if result.dodged:
            return
        if result.defender_dead:
            if isinstance(b, Player):
                self._respawn(b)
            elif b in self.dungeon.monsters:
                self._kill_monster(a, b)
            return
        if result.thorns and result.attacker_dead and isinstance(a, Entity):
            if a in self.dungeon.monsters:
                self._kill_monster(b, a)

    def _respawn(self, player):
        player.hp = player.max_hp
        self._event(f"{player.class_name} пал и возродился на этаже", COL_DMG)
        player.statuses.clear()
        if self.dungeon.rooms:
            spot = rules.find_free_tile(self.dungeon, *self.dungeon.rooms[0].center())
            if spot:
                player.x, player.y = spot
        print(f"[CORE] player {player.class_name} respawned")

    def _kill_monster(self, killer, monster):
        if not isinstance(killer, Player):
            killer = self._nearest_player(monster)
        greedy = killer.greedy if killer is not None else False
        reward = rules.roll_kill_reward(monster, self.depth, greedy)
        if killer is not None:
            killer.gold += reward.gold
            levels = rules.give_xp(killer, monster.xp_value)
            inventory = killer.inventory
        else:
            levels = []
            inventory = {}
        for item_key in reward.drops:
            rules.place_loot(self.dungeon, inventory, monster.x, monster.y,
                             item_key)
        if killer is not None:
            self._event(f"{monster.name} повержен ({killer.class_name}): "
                        f"+{monster.xp_value} XP, +{reward.gold} зол.", COL_XP)
            for level in levels:
                self._event(f"{killer.class_name}: уровень {level}!", COL_GOLD)
        self.dungeon.monsters = [m for m in self.dungeon.monsters if m.hp > 0]

    def _tick_statuses(self):
        for p in list(self.players.values()):
            for event in rules.tick_statuses(p):
                if p.hp <= 0:
                    self._respawn(p)
                    break
        for m in list(self.dungeon.monsters):
            if m.hp <= 0:
                continue
            rules.tick_statuses(m)
            if m.hp <= 0:
                self._kill_monster(self._nearest_player(m), m)

    def _nearest_player(self, monster):
        best = None
        best_dist = 10 ** 9
        for p in self.players.values():
            dist = max(abs(p.x - monster.x), abs(p.y - monster.y))
            if dist < best_dist:
                best, best_dist = p, dist
        return best

    # ════════════════════════ UPDATE ════════════════════════
    def update(self, dt):
        self.tick += 1
        for pid in list(self.move_timers):
            self.move_timers[pid] = max(0.0, self.move_timers[pid] - dt)
        for pid in list(self.attack_timers):
            self.attack_timers[pid] = max(0.0, self.attack_timers[pid] - dt)
        for p in self.players.values():
            p.anim_time += dt
            if p.attack_anim > 0:
                p.attack_anim = max(0.0, p.attack_anim - dt / 0.25)

        # Регенерация (перк)
        for pid, p in list(self.players.items()):
            self.regen_timers[pid] = self.regen_timers.get(pid, 0.0) - dt
            if self.regen_timers[pid] <= 0:
                self.regen_timers[pid] += REGEN_TICK_INTERVAL
                if p.regen and p.hp < p.max_hp:
                    p.hp += 1

        self.status_tick_timer -= dt
        if self.status_tick_timer <= 0:
            self.status_tick_timer += STATUS_TICK_INTERVAL
            self._tick_statuses()

        self._monsters_update(dt)

    def _monsters_update(self, dt):
        for m in list(self.dungeon.monsters):
            if m.hp <= 0:
                continue
            if m.boss:
                closest = self._nearest_player(m)
                if closest is not None:
                    distance = max(abs(closest.x - m.x),
                                   abs(closest.y - m.y))
                    damage = rules.boss_breath_tick(m, closest, distance, dt)
                    if damage and closest.hp <= 0:
                        self._respawn(closest)
            m.act_timer -= dt
            if m.act_timer > 0:
                continue
            m.act_timer = rules.action_interval(m)
            self._monster_act(m)

    def _monster_act(self, m):
        if m.has_status('slow'):
            return
        closest, best = None, 999
        for p in self.players.values():
            d = max(abs(m.x - p.x), abs(m.y - p.y))
            if d < best:
                closest, best = p, d
        if closest is None:
            return
        ability = getattr(m, 'ability', None)
        # Мирные животные не нападают; после урона убегают.
        profile = MONSTERS_BY_CH.get(m.ch, {})
        if profile.get('passive'):
            if m.hp < m.max_hp or best <= 3:
                self._monster_flee(m, closest)
            return
        # Шаман лечит раненого союзника вместо хода.
        if ability == 'healer' and self._healer_action(m):
            return
        # Лучник стреляет с дистанции, не подходя вплотную.
        if (ability == 'ranged' and 2 <= best <= 4
                and has_los(self.dungeon, m.x, m.y,
                            closest.x, closest.y)):
            self._strike(m, closest)
            return
        if best <= 1:
            self._strike(m, closest)
        elif best <= 8 and has_los(self.dungeon, m.x, m.y,
                                   closest.x, closest.y):
            dx = closest.x - m.x
            dy = closest.y - m.y
            mx = (dx > 0) - (dx < 0)
            my = (dy > 0) - (dy < 0)
            if abs(dx) > abs(dy):
                if not self._monster_move(m, m.x + mx, m.y):
                    self._monster_move(m, m.x, m.y + my)
            else:
                if not self._monster_move(m, m.x, m.y + my):
                    self._monster_move(m, m.x + mx, m.y)
        elif m.boss and best <= 15:
            blocked = {(o.x, o.y) for o in self.dungeon.monsters
                       if o is not m and o.hp > 0}
            nxt = bfs_toward_player(self.dungeon, (m.x, m.y),
                                    (closest.x, closest.y), blocked)
            if nxt:
                m.x, m.y = nxt

    def _monster_flee(self, m, threat):
        """Убегает от угрозы (олень после удара)."""
        options = []
        for fx, fy in ((1, 0), (-1, 0), (0, 1), (0, -1),
                       (1, 1), (1, -1), (-1, 1), (-1, -1)):
            nx, ny = m.x + fx, m.y + fy
            if not (0 <= nx < MAP_W and 0 <= ny < MAP_H):
                continue
            if self.dungeon.tiles[ny][nx] == Tile.WALL:
                continue
            if any(o is not m and o.hp > 0 and (o.x, o.y) == (nx, ny)
                   for o in self.dungeon.monsters):
                continue
            score = (nx - threat.x) ** 2 + (ny - threat.y) ** 2
            options.append((score, nx, ny))
        if not options:
            return False
        best = max(s for s, _, _ in options)
        nx, ny = random.choice([(x, y) for s, x, y in options if s == best])
        return self._monster_move(m, nx, ny)

    def _healer_action(self, healer):
        """Лечит самого раненого союзника в радиусе 5. True — походил."""
        target = None
        best_ratio = 1.0
        for other in self.dungeon.monsters:
            if other is healer or other.hp <= 0 or other.hp >= other.max_hp:
                continue
            dist = max(abs(other.x - healer.x), abs(other.y - healer.y))
            if dist > 5:
                continue
            ratio = other.hp / other.max_hp
            if ratio < best_ratio and has_los(self.dungeon, healer.x,
                                              healer.y, other.x, other.y):
                target, best_ratio = other, ratio
        if target is None:
            return False
        heal = min(8, target.max_hp - target.hp)
        target.hp += heal
        self._event(f"{healer.name} лечит {target.name}: +{heal}", COL_HEAL)
        return True

    def _monster_move(self, m, x, y):
        if not (0 <= x < MAP_W and 0 <= y < MAP_H):
            return False
        if self.dungeon.tiles[y][x] == Tile.WALL:
            return False
        if self.merchant and (x, y) == (self.merchant.x, self.merchant.y):
            return False
        if self.elder and (x, y) == (self.elder.x, self.elder.y):
            return False
        for p in self.players.values():
            if (p.x, p.y) == (x, y):
                return False
        for o in self.dungeon.monsters:
            if o is not m and o.hp > 0 and (o.x, o.y) == (x, y):
                return False
        m.x, m.y = x, y
        return True

    # ════════════════════════ SNAPSHOT ════════════════════════
    def get_tiles(self):
        return [[int(t) for t in row] for row in self.dungeon.tiles]

    def _monster_id(self, monster):
        """Стабильный id моба: клиент по нему склеивает кадры для
        интерполяции (объекты мобов пересоздаются в каждом снапшоте нельзя —
        позиции скачут)."""
        key = id(monster)
        mid = self._monster_ids.get(key)
        if mid is None:
            mid = self._next_mid
            self._next_mid += 1
            self._monster_ids[key] = mid
        return mid

    def get_snapshot(self):
        players_data = {}
        for pid, p in self.players.items():
            data = {
                'x': p.x, 'y': p.y,
                'hp': p.hp, 'max_hp': p.max_hp,
                'class': p.class_key,
                'level': p.level,
                'xp': p.xp, 'xp_next': p.xp_next,
                'name': p.class_name,
                'anim_time': round(p.anim_time, 2),
                'attack_anim': round(p.attack_anim, 2),
                'facing': list(p.facing),
                'gold': p.gold,
                'atk': p.atk, 'df': p.df,
                'crit': round(p.crit, 2), 'dodge': round(p.dodge, 2),
                'free_points': p.free_points,
                'statuses': dict(p.statuses),
                'str': p.str, 'agi': p.agi, 'vit': p.vit,
                'luk': p.luk, 'df_stat': p.df_stat,
                'weapon': p.weapon, 'armor': p.armor,
                'head': p.head, 'legs': p.legs,
                'skills': {k: dict(v) for k, v in p.skills.items()},
            }
            # Инвентарь меняется редко — шлём только при изменении.
            # Первый снапшот для игрока всегда содержит его.
            inventory = dict(p.inventory)
            if self._inventory_cache.get(pid) != inventory:
                data['inventory'] = inventory
                self._inventory_cache[pid] = inventory
            players_data[str(pid)] = data
        alive = [m for m in self.dungeon.monsters if m.hp > 0]
        # Чистим id умерших, чтобы id() переиспользованных объектов не врал
        alive_ids = {id(m) for m in alive}
        self._monster_ids = {k: v for k, v in self._monster_ids.items()
                             if k in alive_ids}
        monsters_data = [{
            'mid': self._monster_id(m),
            'x': m.x, 'y': m.y, 'hp': m.hp, 'max_hp': m.max_hp,
            'ch': m.ch, 'color': list(m.color), 'boss': m.boss,
            'statuses': dict(m.statuses),
        } for m in alive]
        items_data = [{'x': k[0], 'y': k[1], 'key': v}
                      for k, v in self.dungeon.items.items()]
        return {
            'tick': self.tick,
            'world_mode': self.world_mode,
            'depth': self.depth,
            'players': players_data,
            'monsters': monsters_data,
            'items': items_data,
            'merchant': {'x': self.merchant.x, 'y': self.merchant.y}
                        if self.merchant else None,
            'elder': {'x': self.elder.x, 'y': self.elder.y}
                     if self.elder else None,
            'shop_stock': self.shop_stock,
            'events': self._take_events(),
        }

