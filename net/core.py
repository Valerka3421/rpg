"""Серверная логика без pygame. Авторитетное состояние.

Все правила боя/лута/статусов берутся из :mod:`core.rules` — того же
модуля, что использует одиночная игра, поэтому режимы не расходятся.
"""
import random
import json
import os
import time

from core.config import (
    MAP_W, MAP_H, MERCHANT_FLOORS, Tile,
    PLAYER_MOVE_COOLDOWN, PLAYER_ATTACK_COOLDOWN,
    STATUS_TICK_INTERVAL, REGEN_TICK_INTERVAL, SAVE_VERSION,
    COL_TEXT, COL_GOLD, COL_XP, COL_DMG, COL_HEAL, COL_MERCH,
)
from core import rules
from core import skills
from core import overworld as ov
from core.data import (BOSSES, make_shop_stock, reroll_price, item_sell_price,
                       item_display_name, xp_to_next, ITEMS,
                       craft_skill_reward, item_category, select_recipe,
                       MONSTERS_BY_CH)
from core.entities import Player, Entity
from core.quests import QuestLog
from core.data import QUESTS
from core.world import (
    generate_overworld, generate_dungeon, place_entry_tile, has_los,
    bfs_toward_player, clear_boss_barriers,
)
from core.save import dungeon_to_dict, dungeon_from_dict
from core import harvest as harvest_mod


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

        # Общий журнал квестов отряда: задания общие, награды персональные
        self.quest_log = QuestLog()
        # Активный обмен между двумя игроками (или None)
        self.trade = None
        self._harvest_cache = {}

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
            'mana': getattr(p, 'mana', 0),
            'level': p.level, 'xp': p.xp, 'xp_next': p.xp_next,
            'free_points': p.free_points,
            'str': p.str, 'agi': p.agi, 'vit': p.vit, 'luk': p.luk,
            'df_stat': p.df_stat,
            'weapon': p.weapon, 'armor': p.armor,
            'head': p.head, 'legs': p.legs,
            'helmet': p.head, 'leggings': p.legs,
            'inventory': dict(p.inventory),
            'gold': p.gold,
            'class_key': p.class_key,
            'class_tier': getattr(p, 'class_tier', 1),
            'double_strike': p.double_strike,
            'regen': p.regen, 'vampire': p.vampire,
            'thorns': p.thorns, 'greedy': p.greedy,
            'skills': {k: dict(v) for k, v in getattr(p, 'skills', {}).items()},
            'statuses': dict(p.statuses),
            'skill_points': getattr(p, 'skill_points', 0),
            'skill_xp': dict(getattr(p, 'skill_xp', {})),
            'skill_nodes': sorted(getattr(p, 'skill_nodes', set())),
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
        p.head = d.get('head', d.get('helmet'))
        p.legs = d.get('legs', d.get('leggings'))
        p.inventory = dict(d.get('inventory', {}))
        p.gold = d.get('gold', 0)
        p.double_strike = d.get('double_strike', False)
        p.regen = d.get('regen', False)
        p.vampire = d.get('vampire', False)
        p.thorns = d.get('thorns', False)
        p.greedy = d.get('greedy', False)
        from core.data import default_skills
        p.skills = default_skills()
        old_xp = d.get('skill_xp') or {}
        for skill, val in (d.get('skills') or {}).items():
            skill = skills.canon_skill(skill)
            if skill not in p.skills:
                continue
            if isinstance(val, dict):
                p.skills[skill]['level'] = max(1, int(val.get('level', 1)))
                p.skills[skill]['xp'] = max(0, int(val.get('xp', 0)))
            else:
                p.skills[skill]['level'] = max(1, int(val))
                p.skills[skill]['xp'] = max(0, int(old_xp.get(skill, 0)))
        p.statuses = dict(d.get('statuses', {}))
        p.class_tier = int(d.get('class_tier', p.class_tier))
        p.skill_points = int(d.get('skill_points', 1))
        p.skill_xp = {k: v['xp'] for k, v in p.skills.items()}
        p.skill_nodes = set(d.get('skill_nodes') or ())
        p.recalc()
        p.hp = min(d['hp'], p.max_hp)
        p.mana = min(int(d.get('mana', p.max_mana)), p.max_mana)
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
            'version': SAVE_VERSION,
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
        slot = {'helmet': 'head', 'leggings': 'legs'}.get(slot, slot)
        if p is None or slot not in ('weapon', 'armor', 'head', 'legs'):
            return
        key = getattr(p, slot, None)
        if not key:
            return
        setattr(p, slot, None)
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
        if key is None:
            recipes = rules.craftable_list(p.inventory, p.level, p)
            if idx < 0 or idx >= len(recipes):
                return False
            key = recipes[idx][0]
            recipe_index = recipes[idx][5]
        result = rules.craft_item(p, key, recipe_index=recipe_index)
        if result.ok:
            self._event(f"{p.class_name} создал: {item_display_name(result.key)}",
                        COL_GOLD)
        return result.ok

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
        elif t == 'harvest':
            self.harvest_adjacent(pid, msg.get('x'), msg.get('y'))
        elif t == 'quest_accept':
            self.quest_accept(pid, msg.get('key'))
        elif t == 'quest_turn_in':
            self.quest_turn_in(pid, msg.get('key'))
        elif t == 'quest_talk':
            self.quest_talk(pid)
        elif t == 'trade_request':
            self.trade_request(pid, msg.get('target'))
        elif t == 'trade_accept':
            self.trade_accept(pid)
        elif t == 'trade_decline':
            self.trade_decline(pid)
        elif t == 'trade_cancel':
            self.trade_cancel(pid)
        elif t == 'trade_offer_add':
            self.trade_offer_add(pid, msg.get('key'), msg.get('count', 1))
        elif t == 'trade_offer_remove':
            self.trade_offer_remove(pid, msg.get('key'), msg.get('count', 1))
        elif t == 'trade_gold':
            self.trade_gold(pid, msg.get('amount', 0))
        elif t == 'trade_ready':
            self.trade_ready(pid)
        elif t == 'duel_request':
            self.duel_request(pid, msg.get('target'))
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
        # Ресурсы добываются только клавишей «E» (harvest).
        if (nx, ny) in self.dungeon.harvestables:
            return False
        tile = self.dungeon.tiles[ny][nx]
        if tile in (Tile.WALL, Tile.LOCKED_DOOR, Tile.CHEST):
            if tile == Tile.LOCKED_DOOR:
                if p.inventory.get('boss_key', 0) > 0:
                    rules.consume(p.inventory, 'boss_key')
                    self.dungeon.tiles[ny][nx] = Tile.FLOOR
                    self._event(f"{p.class_name} отпер дверь ключом босса",
                                COL_GOLD)
                    return True
                self._event("Дверь заперта: нужен ключ босса", COL_TEXT)
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
        """Квесты общие: подбор предмета двигает прогресс всего отряда."""
        self.quest_log.update_collect(result.key, player.inventory)
        if result.kind == 'gold':
            self._event(f"{player.class_name}: +{result.gold} золота", COL_GOLD)
        else:
            self._event(f"{player.class_name} подобрал: {result.name}", COL_TEXT)

    # ════════════════════════ ДОБЫЧА («E») ════════════════════════
    def harvest_adjacent(self, pid, tx=None, ty=None):
        """«E»: добыть ресурс под курсором (смежный) или открыть сундук."""
        p = self.players.get(pid)
        if p is None:
            return
        target = None
        if (isinstance(tx, int) and not isinstance(tx, bool)
                and isinstance(ty, int) and not isinstance(ty, bool)
                and max(abs(tx - p.x), abs(ty - p.y)) <= 1):
            target = (tx, ty)
        if target and (0 <= tx < MAP_W and 0 <= ty < MAP_H
                       and self.dungeon.tiles[ty][tx] == Tile.CHEST):
            self._open_chest(p, tx, ty)
            return
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            tx, ty = p.x + dx, p.y + dy
            if (0 <= tx < MAP_W and 0 <= ty < MAP_H
                    and self.dungeon.tiles[ty][tx] == Tile.CHEST):
                self._open_chest(p, tx, ty)
                return
        if self.world_mode != 'overworld':
            return
        # Ресурс под курсором важнее соседних.
        if target and self.dungeon.harvestables.get(target) is not None:
            self._hit_harvestable(p, target)
            return
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                if dx == 0 and dy == 0:
                    continue
                pos = (p.x + dx, p.y + dy)
                record = self.dungeon.harvestables.get(pos)
                if record is None or not record.get('has', True):
                    continue
                self._hit_harvestable(p, pos)
                return

    def _hit_harvestable(self, p, pos):
        record = self.dungeon.harvestables.get(pos)
        if record is None or not record.get('has', True):
            return
        now = time.time()
        result = harvest_mod.hit(self.dungeon, p, pos[0], pos[1], p.atk, now)
        if result is None or not result['done']:
            return
        names = ', '.join(f"{item_display_name(key)}×{count}"
                          for key, count in result['items'])
        for key, count in result['items']:
            self.quest_log.update_collect(key, p.inventory)
        self._event(f"{p.class_name} добыл: {names}", COL_HEAL)

    def _open_chest(self, p, x, y):
        """Сундук казны: золото + пара предметов по глубине."""
        depth = max(1, self.depth)
        gold = 20 + depth * 8
        p.gold += gold
        self._event(f"{p.class_name} открыл сундук: +{gold} золота", COL_GOLD)
        tier = min(3, max(1, (depth + 1) // 3))
        pool = [k for k, v in ITEMS.items()
                if v.get('tier', 1) <= tier
                and v.get('type') not in ('book', 'resource', 'key')]
        for _ in range(random.randint(2, 3)):
            rules.place_loot(self.dungeon, p.inventory, x, y,
                             random.choice(pool))
        self.dungeon.tiles[y][x] = Tile.FLOOR

    # ════════════════════════ КВЕСТЫ (общие) ════════════════════════
    def quest_talk(self, pid):
        """Разговор со старейшиной открывает его квесты всему отряду."""
        p = self.players.get(pid)
        if p is None or not self._near_elder(p):
            return
        from core.data import quests_by_giver
        self.quest_log.add_available(quests_by_giver('elder'))

    def _near_elder(self, player):
        return (self.elder is not None
                and max(abs(player.x - self.elder.x),
                        abs(player.y - self.elder.y)) <= 1)

    def quest_accept(self, pid, key):
        p = self.players.get(pid)
        q = QUESTS.get(key or '')
        if p is None or not q:
            return
        if key in self.quest_log.active or key in self.quest_log.turned_in:
            return
        if not self._near_elder(p):
            self._event(f"{p.class_name}: квесты даёт старейшина — подойди "
                        "к нему.", COL_TEXT)
            return
        self.quest_log.accept(key, q['count'], p.inventory)
        self._event(f"{p.class_name} взял квест: {q['name']}", COL_GOLD)

    def quest_turn_in(self, pid, key):
        p = self.players.get(pid)
        state = self.quest_log.active.get(key or '')
        if p is None or state is None:
            return
        if state['progress'] < state['count']:
            self._event(f"{p.class_name}: задание ещё не выполнено.", COL_TEXT)
            return
        if not self._near_elder(p):
            self._event(f"{p.class_name}: сдать квест можно у старейшины.",
                        COL_TEXT)
            return
        q = QUESTS.get(key, {})
        self._turn_in_player = p
        if self.quest_log.turn_in(key, p, self):
            self._event(f"{p.class_name} сдал квест: {q.get('name', key)} "
                        f"(+{q.get('reward_gold', 0)} з. лично)", COL_HEAL)

    def level_up(self):
        """Хук QuestLog.turn_in: уровняет игрока, сдающего квест."""
        p = getattr(self, '_turn_in_player', None)
        if p is None:
            return
        while p.xp >= p.xp_next:
            p.xp -= p.xp_next
            p.level += 1
            p.xp_next = xp_to_next(p.level)
            p.free_points += 5
            p.recalc()
            self._event(f"{p.class_name}: уровень {p.level}!", COL_GOLD)

    # ════════════════════════ ОБМЕН МЕЖДУ ИГРОКАМИ ════════════════════════
    def _trade_partner(self, pid):
        if not self.trade:
            return None
        players = self.trade['players']
        if pid not in players:
            return None
        return players[1] if players[0] == pid else players[0]

    def _trade_release(self):
        self.trade = None

    def trade_request(self, pid, target):
        p = self.players.get(pid)
        other = self.players.get(target)
        if p is None or other is None or target == pid:
            return
        if self.trade:
            self._event("Обмен уже идёт — дождитесь его окончания.", COL_TEXT)
            return
        if max(abs(p.x - other.x), abs(p.y - other.y)) > 1:
            self._event("Для обмена нужно стоять рядом.", COL_TEXT)
            return
        self.trade = {'players': [pid, target], 'offers': {pid: {}, target: {}},
                      'gold': {pid: 0, target: 0}, 'ready': set(),
                      'accepted': {pid}}
        self._event(f"{p.class_name} предлагает обмен "
                    f"{other.class_name}. «Принять» в окне обмена.", COL_MERCH)

    def trade_accept(self, pid):
        if not self.trade or pid not in self.trade['players']:
            return
        self.trade['accepted'].add(pid)
        if len(self.trade['accepted']) == len(self.trade['players']):
            self._event("Обмен открыт: выложите предметы и подтвердите.",
                        COL_MERCH)

    def trade_decline(self, pid):
        if not self.trade or pid not in self.trade['players']:
            return
        names = [self.players[q].class_name for q in self.trade['players']
                 if q in self.players]
        self._trade_release()
        self._event(f"{names[0] if names else 'Игрок'} отклонил обмен.",
                    COL_TEXT)

    def trade_cancel(self, pid):
        if not self.trade or pid not in self.trade['players']:
            return
        self._trade_release()
        self._event("Обмен отменён.", COL_TEXT)

    def trade_offer_add(self, pid, key, count=1):
        if not self.trade or pid not in self.trade['players']:
            return
        p = self.players.get(pid)
        if p is None or not key:
            return
        count = max(1, int(count))
        have = p.inventory.get(key, 0)
        already = self.trade['offers'][pid].get(key, 0)
        if already + count > have:
            count = have - already
        if count <= 0:
            return
        self.trade['offers'][pid][key] = already + count

    def trade_offer_remove(self, pid, key, count=1):
        if not self.trade or pid not in self.trade['players']:
            return
        offers = self.trade['offers'].get(pid, {})
        if not key or key not in offers:
            return
        offers[key] = max(0, offers[key] - max(1, int(count)))
        if offers[key] <= 0:
            del offers[key]

    def trade_gold(self, pid, amount):
        if not self.trade or pid not in self.trade['players']:
            return
        p = self.players.get(pid)
        if p is None:
            return
        try:
            amount = int(amount)
        except (TypeError, ValueError):
            return
        self.trade['gold'][pid] = max(0, min(amount, p.gold))

    def trade_ready(self, pid):
        trade = self.trade
        if not trade or pid not in trade['players']:
            return
        if pid in trade['ready']:
            trade['ready'].discard(pid)
            return
        trade['ready'].add(pid)
        if len(trade['ready']) == len(trade['players']):
            self._trade_execute(trade)

    def _trade_execute(self, trade):
        """Меняет предметы и золото, если всё сходится."""
        players = trade['players']
        a, b = (self.players.get(q) for q in players)
        if a is None or b is None:
            self._trade_release()
            return
        offer_a, offer_b = trade['offers'][players[0]], trade['offers'][players[1]]
        gold_a, gold_b = trade['gold'][players[0]], trade['gold'][players[1]]
        # Проверка: у каждого хватает выкладываемого
        for p, offer, gold in ((a, offer_a, gold_a), (b, offer_b, gold_b)):
            if gold > p.gold:
                self._event("Обмен сорвался: не хватает золота.", COL_DMG)
                self._trade_release()
                return
            for key, cnt in offer.items():
                if p.inventory.get(key, 0) < cnt:
                    self._event("Обмен сорвался: предмета нет в инвентаре.",
                                COL_DMG)
                    self._trade_release()
                    return
        # Списание
        for p, offer, gold in ((a, offer_a, gold_a), (b, offer_b, gold_b)):
            for key, cnt in offer.items():
                for _ in range(cnt):
                    rules.consume(p.inventory, key)
            if gold:
                p.gold -= gold
        # Выдача
        for p, offer, gold in ((a, offer_b, gold_b), (b, offer_a, gold_a)):
            for key, cnt in offer.items():
                p.inventory[key] = p.inventory.get(key, 0) + cnt
            if gold:
                p.gold += gold
        names = [q.class_name for q in (a, b)]
        self._event(f"Обмен завершён: {names[0]} ⇄ {names[1]}", COL_MERCH)
        self._trade_release()

    def duel_request(self, pid, target):
        p = self.players.get(pid)
        other = self.players.get(target)
        if p is None or other is None or target == pid:
            return
        self._event(f"{p.class_name} вызывает {other.class_name} на дуэль!",
                    COL_DMG)

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
        self.quest_log.update_reach(depth)
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
        xp = monster.xp_value
        levels = []
        if killer is not None:
            killer.gold += reward.gold
            # Опыт 70/30: убийце 70%, остальные 30% делятся между союзниками.
            others = [p for q, p in self.players.items() if p is not killer]
            killer_xp = int(xp * 0.7)
            levels = rules.give_xp(killer, killer_xp)
            if others:
                share = (xp - killer_xp) // len(others)
                if share > 0:
                    for o in others:
                        rules.give_xp(o, share)
                    self._event(f"Опыт поделен: {killer_xp} убийце, "
                                f"{share} каждому союзнику", COL_XP)
            self.quest_log.update_kill(monster.ch)
        else:
            inventory = {}
        for item_key in reward.drops:
            rules.place_loot(self.dungeon, killer.inventory if killer else {},
                             monster.x, monster.y, item_key)
        if killer is not None:
            self._event(f"{monster.name} повержен ({killer.class_name}): "
                        f"+{reward.gold} зол.", COL_XP)
            for level in levels:
                self._event(f"{killer.class_name}: уровень {level}!", COL_GOLD)
        if monster.boss and clear_boss_barriers(self.dungeon):
            self._event("Выходы арены босса открыты.", COL_WIN)
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
            if self.dungeon.tiles[ny][nx] in (Tile.WALL, Tile.LOCKED_DOOR,
                                              Tile.CHEST):
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
        if self.dungeon.tiles[y][x] in (Tile.WALL, Tile.LOCKED_DOOR,
                                        Tile.CHEST):
            return False
        if (x, y) in self.dungeon.harvestables:
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
                'mana': getattr(p, 'mana', 0),
                'max_mana': getattr(p, 'max_mana', 0),
                'class': p.class_key,
                'class_tier': getattr(p, 'class_tier', 1),
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
                'skill_points': getattr(p, 'skill_points', 0),
                'statuses': dict(p.statuses),
                'str': p.str, 'agi': p.agi, 'vit': p.vit,
                'luk': p.luk, 'df_stat': p.df_stat,
                'weapon': p.weapon, 'armor': p.armor,
                'head': p.head, 'legs': p.legs,
                'helmet': p.head, 'leggings': p.legs,
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
        harvest = self._harvest_snapshot()
        snapshot = {
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
            'quests': self.quest_log.to_dict(),
            'trade': self._trade_snapshot(),
        }
        if harvest is not None:
            snapshot['harvestables'] = harvest
        return snapshot

    def _harvest_snapshot(self):
        """Состояние ресурсов: шлётся целиком только при изменении."""
        current = {}
        for (x, y), rec in self.dungeon.harvestables.items():
            current[(x, y)] = (rec.get('kind'), bool(rec.get('has', True)),
                               rec.get('hp', 0), rec.get('max_hp', 0))
        if current == self._harvest_cache:
            return None
        self._harvest_cache = dict(current)
        return {f"{x},{y}": {'kind': k, 'has': h, 'hp': hp, 'max_hp': mhp}
                for (x, y), (k, h, hp, mhp) in current.items()}

    def _trade_snapshot(self):
        trade = self.trade
        if not trade:
            return None
        return {
            'players': [str(q) for q in trade['players']],
            'offers': {str(q): dict(offer)
                       for q, offer in trade['offers'].items()},
            'gold': {str(q): amount for q, amount in trade['gold'].items()},
            'ready': [str(q) for q in trade['ready']],
            'accepted': [str(q) for q in trade['accepted']],
        }

