"""Сериализация + слоты + dungeon_cache."""
import os
import json
from core.config import MAP_W, MAP_H, DATA_DIR, Tile

# Имя BASE_DIR сохранено для тестов/совместимости, но указывает на
# записываемый каталог (рядом с exe или в %APPDATA%), а не на _MEIPASS.
BASE_DIR = DATA_DIR
from core.data import xp_to_next
from core.entities import Entity, Player, Room, Dungeon


# ── Базовые сериализаторы ──
def entity_to_dict(e: Entity) -> dict:
    return {
        'x': e.x, 'y': e.y, 'hp': e.hp, 'max_hp': e.max_hp,
        'atk': e.atk, 'df': e.df,
        'name': e.name, 'ch': e.ch, 'color': list(e.color),
        'xp_value': e.xp_value, 'boss': e.boss,
        'is_merchant': e.is_merchant,
        'ability': e.ability,
        'statuses': dict(e.statuses),
        'is_npc': e.is_npc,
        'npc_type': e.npc_type,
    }

def entity_from_dict(d: dict) -> Entity:
    e = Entity(d['x'], d['y'], d['hp'], d['max_hp'], d['atk'], d['df'],
               d['name'], d['ch'], tuple(d['color']), d['xp_value'],
               d['boss'], ability=d.get('ability'))
    e.is_merchant = d.get('is_merchant', False)
    e.statuses = dict(d.get('statuses', {}))
    e.is_npc = d.get('is_npc', False)
    e.npc_type = d.get('npc_type')
    return e

def player_to_dict(p: Player) -> dict:
    return {
        'x': p.x, 'y': p.y, 'hp': p.hp, 'mana': getattr(p, 'mana', 0),
        'class_key': getattr(p, 'class_key', 'default'),
        'class_tier': getattr(p, 'class_tier', 1),
        'class_base': getattr(p, 'class_base', getattr(p, 'class_key', 'default')),
        'class_changes': getattr(p, 'class_changes', 0),
        'level': p.level, 'xp': p.xp, 'xp_next': p.xp_next,
        'free_points': p.free_points,
        'str': p.str, 'agi': p.agi, 'vit': p.vit, 'luk': p.luk,
        'df_stat': p.df_stat, 'int': getattr(p, 'int_stat', 5),
        'weapon': p.weapon, 'armor': p.armor,
        'head': getattr(p, 'head', None),
        'legs': getattr(p, 'legs', None),
        'boots': getattr(p, 'boots', None),
        # Совместимость со старыми сейвами ветки навыков.
        'helmet': getattr(p, 'head', None),
        'leggings': getattr(p, 'legs', None),
        'inventory': dict(p.inventory),
        'gold': p.gold,
        'double_strike': p.double_strike,
        'regen': p.regen, 'vampire': p.vampire,
        'thorns': p.thorns, 'greedy': p.greedy,
        'step_counter': p.step_counter,
        'skills': {k: dict(v) if isinstance(v, dict)
                   else {'level': int(v), 'xp': 0}
                   for k, v in getattr(p, 'skills', {}).items()},
        'statuses': dict(p.statuses),
        'skill_points': getattr(p, 'skill_points', 0),
        'skill_xp': dict(getattr(p, 'skill_xp', {})),
        'skill_nodes': sorted(getattr(p, 'skill_nodes', set())),
        'blueprints': sorted(getattr(p, 'blueprints', set())),
    }


def player_from_dict(d: dict) -> Player:
    from core import skills
    p = Player(class_key=d.get('class_key', 'default'))
    p.x, p.y = d['x'], d['y']
    p.level = d['level']; p.xp = d['xp']
    # Кривая опыта пересчитывается из уровня: так старые сейвы (где xp_next
    # рос как 1.6**n и становился недостижимым) автоматически чинятся.
    p.xp_next = xp_to_next(p.level)
    p.free_points = d['free_points']
    p.str, p.agi, p.vit, p.luk = d['str'], d['agi'], d['vit'], d['luk']
    p.df_stat = d.get('df_stat', 5)
    p.int_stat = d.get('int', 5)
    p.weapon, p.armor = d['weapon'], d['armor']
    p.head = d.get('head', d.get('helmet'))
    p.legs = d.get('legs', d.get('leggings'))
    p.boots = d.get('boots')
    p.inventory = dict(d['inventory'])
    # Старые сейвы: сапоги лежали в слоте «ноги», потому что у них был
    # тип legs. Теперь у обуви отдельный тип и слот — переносим.
    from core.data import get_item_data
    from core.rules import SLOT_FOR_TYPE
    for slot in ('weapon', 'armor', 'head', 'legs', 'boots'):
        key = getattr(p, slot, None)
        if not key:
            continue
        expected = SLOT_FOR_TYPE.get(get_item_data(key).get('type'))
        if expected and expected != slot:
            setattr(p, slot, None)
            if not getattr(p, expected, None):
                setattr(p, expected, key)
            else:
                p.inventory[key] = p.inventory.get(key, 0) + 1
    p.gold = d.get('gold', 0)
    p.double_strike = d.get('double_strike', False)
    p.regen = d.get('regen', False); p.vampire = d.get('vampire', False)
    p.thorns = d.get('thorns', False); p.greedy = d.get('greedy', False)
    p.step_counter = d.get('step_counter', 0)
    # Навыки крафта: новый формат — {'level': N, 'xp': M}; старый —
    # плоские уровни в skills + skill_xp. Поддерживаем оба.
    from core.data import default_skills
    raw_skills = d.get('skills') or {}
    old_xp = d.get('skill_xp') or {}
    p.skills = default_skills()
    for key, val in raw_skills.items():
        key = skills.canon_skill(key)
        if key not in p.skills:
            continue
        if isinstance(val, dict):
            p.skills[key]['level'] = max(1, int(val.get('level', 1)))
            p.skills[key]['xp'] = max(0, int(val.get('xp', 0)))
        else:
            p.skills[key]['level'] = max(1, int(val))
            p.skills[key]['xp'] = max(0, int(old_xp.get(key, 0)))
    p.statuses = dict(d.get('statuses', {}))
    p.class_tier = int(d.get('class_tier', p.class_tier))
    p.class_base = d.get('class_base', p.class_base)
    p.class_changes = int(d.get('class_changes', p.class_changes))
    p.skill_points = int(d.get('skill_points', 1))
    p.skill_xp = {k: v['xp'] for k, v in p.skills.items()}
    p.skill_nodes = set(d.get('skill_nodes') or ())
    p.blueprints = set(d.get('blueprints') or ())
    p.recalc()
    p.hp = min(d['hp'], p.max_hp)
    p.mana = min(d.get('mana', p.max_mana), p.max_mana)
    # Смена правил могла сделать экипировку недоступной — снимаем в сумку.
    for slot, key, _reason in skills.disallowed_equipment(p):
        setattr(p, slot, None)
        p.inventory[key] = p.inventory.get(key, 0) + 1
    p.recalc()
    return p


def dungeon_to_dict(d: Dungeon) -> dict:
    """Тайлы пишутся строками цифр — компактнее вложенных списков int."""
    from core.harvest import serialize as harvest_serialize
    return {
        'w': d.w, 'h': d.h,
        'tiles': [''.join(str(int(t)) for t in row) for row in d.tiles],
        'rooms': [{'x': r.x, 'y': r.y, 'w': r.w, 'h': r.h} for r in d.rooms],
        'monsters': [entity_to_dict(m) for m in d.monsters],
        'items': [{'x': k[0], 'y': k[1], 'key': v} for k, v in d.items.items()],
        'stairs_pos': list(d.stairs_pos) if d.stairs_pos else None,
        'transitions': [
            {'x': k[0], 'y': k[1], 'to': v.get('to'),
             'entry': list(v.get('entry', (k[0], k[1]))),
             'side': v.get('side')}
            for k, v in d.transitions.items()
        ],
        'barrier_tiles': [list(p) for p in getattr(d, 'barrier_tiles', ())],
        'treasury_pos': (list(d.treasury_pos)
                         if getattr(d, 'treasury_pos', None) else None),
        'boss_door_cells': [list(p)
                            for p in getattr(d, 'boss_door_cells', ())],
        'harvestables': harvest_serialize(d),
        'chest_loot': [
            {'x': k[0], 'y': k[1], 'loot': dict(v)}
            for k, v in getattr(d, 'chest_loot', {}).items()
        ],
    }


def _decode_tiles(rows):
    tiles = []
    for row in rows:
        if isinstance(row, str):
            tiles.append([Tile(int(ch)) for ch in row])
        else:                                  # старый формат: [[0,1,2], ...]
            tiles.append([Tile(int(t)) for t in row])
    return tiles


def dungeon_from_dict(d: dict, is_overworld: bool = False, is_location: bool = False) -> Dungeon:
    rows = _decode_tiles(d['tiles'])
    old_h = len(rows)
    old_w = max((len(r) for r in rows), default=0)
    needs_migration = old_w < MAP_W or old_h < MAP_H

    if is_location:
        # Карты локаций хранят собственный размер: не растягиваем их до
        # размеров подземелья и не засыпаем пустые клетки стенами.
        width = old_w or int(d.get('w', 1))
        height = old_h or int(d.get('h', 1))
        dun = Dungeon(width, height)
        dun.tiles = rows
        dun.rooms = [Room(r['x'], r['y'], r['w'], r['h'])
                     for r in d.get('rooms', [])]
        dun.monsters = [entity_from_dict(m) for m in d.get('monsters', [])]
        dun.items = {(i['x'], i['y']): i['key'] for i in d.get('items', [])}
        dun.stairs_pos = tuple(d['stairs_pos']) if d.get('stairs_pos') else None
        for tr in d.get('transitions', []) or []:
            try:
                pos = (int(tr['x']), int(tr['y']))
                entry = tr.get('entry') or list(pos)
                dun.transitions[pos] = {
                    'to': tr['to'],
                    'entry': (int(entry[0]), int(entry[1])),
                }
                if tr.get('side'):
                    dun.transitions[pos]['side'] = tr['side']
            except (KeyError, TypeError, ValueError, IndexError):
                continue
        from core.harvest import deserialize as harvest_deserialize
        dun.harvestables = harvest_deserialize(d.get('harvestables', []))
        dun.chest_loot = {(int(c['x']), int(c['y'])): dict(c.get('loot', {}))
                          for c in d.get('chest_loot', []) or []}
        return dun

    if needs_migration and is_overworld:
        # Старый открытый мир 100×70. Собираем новый и переносим на него
        # старую часть: прогресс сохраняется, а восточная Деревня гоблинов
        # становится доступной.
        from core.world import generate_overworld
        world = generate_overworld()
        for y in range(min(old_h, MAP_H)):
            for x in range(min(old_w, MAP_W)):
                world.tiles[y][x] = rows[y][x]
        world.monsters = ([entity_from_dict(m) for m in d['monsters']
                           if m['x'] < old_w]
                          + [m for m in world.monsters if m.x >= old_w])
        world.items.update({(i['x'], i['y']): i['key']
                            for i in d['items'] if i['x'] < old_w})
        if d.get('stairs_pos'):
            world.stairs_pos = tuple(d['stairs_pos'])
        return world

    if needs_migration:
        padded = [[Tile.WALL] * MAP_W for _ in range(MAP_H)]
        for y, row in enumerate(rows[:MAP_H]):
            for x, tile in enumerate(row[:MAP_W]):
                padded[y][x] = tile
        rows = padded

    dun = Dungeon(MAP_W, MAP_H)
    dun.tiles = rows
    dun.rooms = [Room(r['x'], r['y'], r['w'], r['h']) for r in d['rooms']]
    dun.monsters = [entity_from_dict(m) for m in d['monsters']]
    dun.items = {(i['x'], i['y']): i['key'] for i in d['items']}
    dun.stairs_pos = tuple(d['stairs_pos']) if d['stairs_pos'] else None
    for tr in d.get('transitions', []) or []:
        try:
            pos = (int(tr['x']), int(tr['y']))
            entry = tr.get('entry') or list(pos)
            dun.transitions[pos] = {'to': tr['to'],
                                    'entry': (int(entry[0]), int(entry[1]))}
            if tr.get('side'):
                dun.transitions[pos]['side'] = tr['side']
        except (KeyError, TypeError, ValueError, IndexError):
            continue
    dun.barrier_tiles = {tuple(p) for p in d.get('barrier_tiles', [])}
    dun.treasury_pos = (tuple(d['treasury_pos'])
                        if d.get('treasury_pos') else None)
    dun.boss_door_cells = {tuple(p) for p in d.get('boss_door_cells', [])}
    from core.harvest import deserialize as harvest_deserialize
    dun.harvestables = harvest_deserialize(d.get('harvestables', []))
    dun.chest_loot = {(int(c['x']), int(c['y'])): dict(c.get('loot', {}))
                      for c in d.get('chest_loot', []) or []}
    return dun


# ── dungeon_cache ──
def dungeon_cache_to_dict(cache, current_depth=None, max_keep=3, skip_depth=None):
    """Сериализует кэш этажей.

    ``skip_depth`` — этаж, который уже сохранён отдельным ключом ``dungeon``
    (не дублируем его в кэше: это ~1/3 размера сейва).
    """
    if not cache:
        return {}
    depths = [d for d in cache.keys() if d != skip_depth]
    if current_depth is not None:
        depths = sorted(depths, key=lambda d: abs(d - current_depth))[:max_keep]
    else:
        depths = sorted(depths)
    return {
        str(depth): {
            'dungeon': dungeon_to_dict(cache[depth]['dungeon']),
            'merchant': entity_to_dict(cache[depth]['merchant'])
                        if cache[depth].get('merchant') else None,
            'shop_stock': cache[depth].get('shop_stock', []),
            'explored': [list(p) for p in cache[depth].get('explored', set())],
        }
        for depth in depths
    }


def dungeon_cache_from_dict(data):
    result = {}
    for depth_str, c in (data or {}).items():
        try:
            result[int(depth_str)] = {
                'dungeon': dungeon_from_dict(c['dungeon']),
                'merchant': entity_from_dict(c['merchant'])
                            if c.get('merchant') else None,
                'shop_stock': c.get('shop_stock', []),
                'explored': {tuple(p) for p in c.get('explored', [])},
            }
        except Exception:
            continue
    return result


# ── Слоты ──
def save_slot_path(slot: int) -> str:
    return os.path.join(BASE_DIR, f'savegame_{slot}.json')


def save_slot_exists(slot):
    return os.path.exists(save_slot_path(slot))


def save_slot_info(slot):
    path = save_slot_path(slot)
    if not os.path.exists(path):
        return None
    try:
        with open(path, 'r', encoding='utf-8') as f:
            data = json.load(f)
        p = data.get('player', {})
        return {
            'level': p.get('level', 1),
            'gold': p.get('gold', 0),
            'depth': data.get('depth', 0),
            'world_mode': data.get('world_mode', 'overworld'),
            'class_key': p.get('class_key', 'default'),
            'mtime': os.path.getmtime(path),
        }
    except Exception:
        return None


def delete_slot(slot):
    path = save_slot_path(slot)
    if os.path.exists(path):
        try:
            os.remove(path)
            return True
        except Exception:
            return False
    return False