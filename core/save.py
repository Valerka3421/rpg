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
def entity_to_dict(e):
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

def entity_from_dict(d):
    e = Entity(d['x'], d['y'], d['hp'], d['max_hp'], d['atk'], d['df'],
               d['name'], d['ch'], tuple(d['color']), d['xp_value'],
               d['boss'], ability=d.get('ability'))
    e.is_merchant = d.get('is_merchant', False)
    e.statuses = dict(d.get('statuses', {}))
    e.is_npc = d.get('is_npc', False)
    e.npc_type = d.get('npc_type')
    return e

def player_to_dict(p):
    return {
        'x': p.x, 'y': p.y, 'hp': p.hp,
        'class_key': getattr(p, 'class_key', 'default'),
        'level': p.level, 'xp': p.xp, 'xp_next': p.xp_next,
        'free_points': p.free_points,
        'str': p.str, 'agi': p.agi, 'vit': p.vit, 'luk': p.luk,
        'df_stat': p.df_stat,
        'weapon': p.weapon, 'armor': p.armor,
        'head': p.head, 'legs': p.legs,
        'inventory': dict(p.inventory),
        'gold': p.gold,
        'double_strike': p.double_strike,
        'regen': p.regen, 'vampire': p.vampire,
        'thorns': p.thorns, 'greedy': p.greedy,
        'step_counter': p.step_counter,
        'skills': {k: dict(v) for k, v in getattr(p, 'skills', {}).items()},
        'statuses': dict(p.statuses),
    }


def player_from_dict(d):
    p = Player(class_key=d.get('class_key', 'default'))
    p.x, p.y = d['x'], d['y']
    p.level = d['level']; p.xp = d['xp']
    # Кривая опыта пересчитывается из уровня: так старые сейвы (где xp_next
    # рос как 1.6**n и становился недостижимым) автоматически чинятся.
    p.xp_next = xp_to_next(p.level)
    p.free_points = d['free_points']
    p.str, p.agi, p.vit, p.luk = d['str'], d['agi'], d['vit'], d['luk']
    p.df_stat = d.get('df_stat', 5)
    p.weapon, p.armor = d['weapon'], d['armor']
    p.head = d.get('head')
    p.legs = d.get('legs')
    p.inventory = dict(d['inventory'])
    p.gold = d.get('gold', 0)
    p.double_strike = d.get('double_strike', False)
    p.regen = d.get('regen', False); p.vampire = d.get('vampire', False)
    p.thorns = d.get('thorns', False); p.greedy = d.get('greedy', False)
    p.step_counter = d.get('step_counter', 0)
    # Навыки крафта появились в v7; старые сейвы получают базовые значения.
    from core.data import default_skills
    p.skills = default_skills()
    for key, val in (d.get('skills') or {}).items():
        if key in p.skills and isinstance(val, dict):
            p.skills[key]['level'] = max(1, int(val.get('level', 1)))
            p.skills[key]['xp'] = max(0, int(val.get('xp', 0)))
    p.statuses = dict(d.get('statuses', {}))
    p.recalc()
    p.hp = min(d['hp'], p.max_hp)
    return p


def dungeon_to_dict(d):
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
             'entry': list(v.get('entry', (k[0], k[1])))}
            for k, v in d.transitions.items()
        ],
        'harvestables': harvest_serialize(d),
    }


def _decode_tiles(rows):
    tiles = []
    for row in rows:
        if isinstance(row, str):
            tiles.append([Tile(int(ch)) for ch in row])
        else:                                  # старый формат: [[0,1,2], ...]
            tiles.append([Tile(int(t)) for t in row])
    return tiles


def dungeon_from_dict(d, is_overworld=False, is_location=False):
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
            except (KeyError, TypeError, ValueError, IndexError):
                continue
        from core.harvest import deserialize as harvest_deserialize
        dun.harvestables = harvest_deserialize(d.get('harvestables', []))
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
        except (KeyError, TypeError, ValueError, IndexError):
            continue
    from core.harvest import deserialize as harvest_deserialize
    dun.harvestables = harvest_deserialize(d.get('harvestables', []))
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
def save_slot_path(slot):
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