"""Отдельные карты локаций и переходы между ними.

Раньше весь мир был одной картой 140×70: деревня в центре, восемь биомов
вокруг неё и Деревня гоблинов на востоке. Теперь каждая локация — своя
карта (120×70, деревня — 100×70), а между ними стоят каменные врата
(:data:`core.config.Tile.GATE`).

Карты генерируются лениво и кэшируются в ``Game.locations``. Вместе с
состоянием (тайлы, мобы, лут, добываемые ресурсы) они попадают в сейв,
поэтому при возвращении локация выглядит ровно так, как её оставили.
"""
from __future__ import annotations

import random
from collections import deque

from core.config import Tile
from core.data import MONSTERS
from core.entities import Dungeon, Entity, Room
from core import overworld as ov
from core.harvest import make_harvestable

# Размер обычной карты локации. Старый биом занимал ~33×23 клетки внутри
# общей карты, так что отдельная карта в 4+ раза просторнее.
LOCATION_W, LOCATION_H = 120, 70
VILLAGE_W, VILLAGE_H = ov.LEGACY_MAP_W, ov.LEGACY_MAP_H

# Возврат из биома — к тем же вратам деревни, из которых вышли.
VILLAGE_GATES = {
    'pine':           (8, 8),
    'birch':          (50, 1),
    'rock':           (93, 8),
    'autumn':         (1, 30),
    'swamp':          (98, 30),
    'dark':           (8, 61),
    'meadow':         (50, 68),
    'dead':           (93, 61),
    'goblin_village': (98, 45),
}

BIOME_POOLS = {
    'pine':   ('deer', 'wolf', 'rat'),
    'birch':  ('deer', 'slime', 'rat'),
    'rock':   ('wolf', 'bear'),
    'autumn': ('deer', 'boar', 'wolf'),
    'swamp':  ('slime', 'rat', 'boar'),
    'dark':   ('wolf', 'bear', 'slime'),
    'meadow': ('deer', 'boar', 'rat'),
    'dead':   ('wolf', 'bear', 'rat'),
}

BIOME_DENSITY = {
    'pine': 0.46, 'birch': 0.36, 'rock': 0.30, 'autumn': 0.40,
    'swamp': 0.30, 'dark': 0.56, 'meadow': 0.17, 'dead': 0.36,
}

LOCATIONS = {
    'village': {
        'title': ov.VILLAGE_TITLE, 'kind': 'village',
        'w': VILLAGE_W, 'h': VILLAGE_H,
    },
    'goblin_village': {
        'title': ov.GOBLIN_VILLAGE_TITLE, 'kind': 'goblin',
        'w': LOCATION_W, 'h': LOCATION_H,
    },
    'mine': {
        'title': 'Заброшенная шахта', 'kind': 'mine',
        'w': LOCATION_W, 'h': LOCATION_H,
    },
}
for _biome, _title in ov.BIOME_TITLES.items():
    LOCATIONS[_biome] = {
        'title': _title, 'kind': 'biome', 'biome': _biome,
        'w': LOCATION_W, 'h': LOCATION_H,
    }


def _l_path(a, b):
    """Клетки Г-образной дороги с полутолщиной 1."""
    ax, ay = a
    bx, by = b
    cells = set()
    for x in range(min(ax, bx), max(ax, bx) + 1):
        for dy in (-1, 0, 1):
            cells.add((x, ay + dy))
    for y in range(min(ay, by), max(ay, by) + 1):
        for dx in (-1, 0, 1):
            cells.add((bx + dx, y))
    return cells


VILLAGE_ROADS = set()
for _gate in VILLAGE_GATES.values():
    VILLAGE_ROADS |= _l_path(ov.VILLAGE_CENTER, _gate)


def _village_inward(gate):
    """Клетка сразу внутри деревни, рядом с вратами."""
    gx, gy = gate
    cx, cy = ov.VILLAGE_CENTER
    dx = 0 if gx == cx else (1 if gx < cx else -1)
    dy = 0 if gy == cy else (1 if gy < cy else -1)
    return (gx + dx, gy + dy)


VILLAGE_INWARD = {k: _village_inward(v) for k, v in VILLAGE_GATES.items()}

# Куда игрок попадает, выйдя из деревни в локацию.
LOCATION_ENTRY = (LOCATION_W // 2, LOCATION_H - 8)


# ── Виды поверхности по локации ──

def ground_kind(key, x, y):
    spec = LOCATIONS.get(key, LOCATIONS['village'])
    kind = spec['kind']
    if kind == 'village':
        if ov.in_village(x, y):
            return ov.village_ground_kind(x, y)
        if (x, y) in VILLAGE_ROADS or ov.is_road(x, y):
            return 'road'
        biome = ov.biome_at(x, y)
        return f'grass_{biome}' if biome else 'village_grass'
    if kind == 'goblin':
        if _in_local_goblin_village(x, y):
            cx, cy = LOCAL_GOBLIN_CENTER
            if (x - cx) ** 2 + (y - (cy - 1)) ** 2 <= 16:
                return 'goblin_plaza'
            if y in (34, 35, 36) or x in (cx - 1, cx, cx + 1):
                return 'goblin_path'
            return 'goblin_grass'
        return 'grass_dark'
    if kind == 'mine':
        return 'dungeon_ground'
    biome = spec.get('biome', 'pine')
    if biome == 'rock' and (x - 34) ** 2 + (y - 14) ** 2 <= 64:
        return 'dungeon_ruins'
    if x == LOCATION_W // 2 and 35 <= y <= LOCATION_H - 4:
        return 'road'
    return f'grass_{biome}'


def wall_kind(key, x, y):
    spec = LOCATIONS.get(key, LOCATIONS['village'])
    kind = spec['kind']
    if kind == 'village':
        if ov.in_village(x, y):
            if ov.is_statue(x, y):
                return 'village_statue'
            if ov.is_stall(x, y):
                return 'village_stall'
            if ov.is_torch(x, y):
                return 'village_torch_post'
            return 'village_wall'
        biome = ov.biome_at(x, y) or 'pine'
        return 'rock' if biome == 'rock' else f'tree_{biome}'
    if kind == 'goblin':
        if _in_local_goblin_village(x, y):
            return 'goblin_hut'
        return 'tree_dark'
    if kind == 'mine':
        return 'wall'
    biome = spec.get('biome', 'pine')
    return 'rock' if biome == 'rock' else f'tree_{biome}'


def title(key):
    return LOCATIONS.get(key, LOCATIONS['village'])['title']


def is_tree_wall(key, x, y):
    """Все деревья на поверхности можно рубить."""
    return wall_kind(key, x, y).startswith('tree')


# ── Генерация ──

def _fill_floor(d):
    for y in range(d.h):
        for x in range(d.w):
            d.tiles[y][x] = Tile.FLOOR


def _clear(d, center, radius):
    cx, cy = center
    for y in range(max(0, cy - radius), min(d.h, cy + radius + 1)):
        for x in range(max(0, cx - radius), min(d.w, cx + radius + 1)):
            if (x - cx) ** 2 + (y - cy) ** 2 <= radius * radius:
                d.tiles[y][x] = Tile.FLOOR


def _carve(d, a, b, half=1):
    """Прорубить Г-образный проход шириной 2*half+1."""
    ax, ay = a
    bx, by = b
    for x in range(min(ax, bx), max(ax, bx) + 1):
        for dy in range(-half, half + 1):
            if 0 <= ay + dy < d.h and 0 <= x < d.w:
                d.tiles[ay + dy][x] = Tile.FLOOR
    for y in range(min(ay, by), max(ay, by) + 1):
        for dx in range(-half, half + 1):
            if 0 <= y < d.h and 0 <= bx + dx < d.w:
                d.tiles[y][bx + dx] = Tile.FLOOR


def _reachable(d, start):
    if d.tiles[start[1]][start[0]] == Tile.WALL:
        return set()
    seen = {start}
    q = deque([start])
    while q:
        cx, cy = q.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = cx + dx, cy + dy
            if not (0 <= nx < d.w and 0 <= ny < d.h):
                continue
            if (nx, ny) in seen or d.tiles[ny][nx] == Tile.WALL:
                continue
            seen.add((nx, ny))
            q.append((nx, ny))
    return seen


def _spawn_monsters(d, pool, count, avoid=(), min_dist=None):
    blocked = set(avoid) | set(d.transitions) | set(d.harvestables)
    spots = []
    for y in range(1, d.h - 1):
        for x in range(1, d.w - 1):
            if d.tiles[y][x] != Tile.FLOOR:
                continue
            if (x, y) in blocked or (x, y) in d.items:
                continue
            if min_dist and (x - min_dist[0]) ** 2 + (y - min_dist[1]) ** 2 <= 64:
                continue
            spots.append((x, y))
    random.shuffle(spots)
    pool = [k for k in pool if k in MONSTERS]
    if not pool:
        return
    for x, y in spots[:count]:
        m = MONSTERS[random.choice(pool)]
        d.monsters.append(Entity(
            x, y, m['hp'], m['hp'], m['atk'], m['df'],
            m['name'], m['ch'], m['color'], m['xp'],
            ability=m.get('ability')))


def _scatter_items(d, count, avoid=(), pool=('mushroom', 'wood', 'stone')):
    blocked = set(avoid) | set(d.transitions) | set(d.harvestables)
    spots = [p for p in _reachable(d, (d.w // 2, d.h - 8))
             if p not in blocked and p not in d.items]
    random.shuffle(spots)
    for pos in spots[:count]:
        d.items[pos] = random.choice(pool)


def _scatter_bushes(d, key, count, avoid=()):
    blocked = set(avoid) | set(d.transitions) | set(d.harvestables)
    spots = []
    for y in range(1, d.h - 1):
        for x in range(1, d.w - 1):
            if d.tiles[y][x] != Tile.FLOOR:
                continue
            if (x, y) in blocked or (x, y) in d.items:
                continue
            if not ground_kind(key, x, y).startswith('grass'):
                continue
            spots.append((x, y))
    random.shuffle(spots)
    for pos in spots[:count]:
        d.harvestables[pos] = make_harvestable('bush')


def _add_ore(d, count):
    """Рудные жилы на стенах, к которым можно подойти."""
    walls = []
    for y in range(1, d.h - 1):
        for x in range(1, d.w - 1):
            if d.tiles[y][x] != Tile.WALL:
                continue
            if any(d.tiles[y + dy][x + dx] != Tile.WALL
                   for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                walls.append((x, y))
    random.shuffle(walls)
    for pos in walls[:count]:
        d.harvestables[pos] = make_harvestable('ore')


def _generate_village():
    d = Dungeon(VILLAGE_W, VILLAGE_H)
    _fill_floor(d)

    # Лес по краям: вид определяется старыми биомами, поэтому переходы
    # из деревни ведут в визуально знакомые края.
    for y in range(1, d.h - 1):
        for x in range(1, d.w - 1):
            if ov.in_village(x, y) or (x, y) in VILLAGE_ROADS:
                continue
            biome = ov.biome_at(x, y)
            if not biome:
                continue
            if random.random() < BIOME_DENSITY.get(biome, 0.4):
                d.tiles[y][x] = Tile.WALL

    _clear(d, ov.VILLAGE_CENTER, 7)

    # Дома и площадь — те же координаты, что и в исторической деревне.
    for hx, hy, hw, hh in ov.VILLAGE_HOUSES:
        for yy in range(hy, hy + hh):
            for xx in range(hx, hx + hw):
                d.tiles[yy][xx] = Tile.WALL
    wx, wy = ov.WELL_POS
    d.tiles[wy][wx] = Tile.WALL
    for sx, sy in ov.VILLAGE_STALLS:
        d.tiles[sy][sx] = Tile.WALL
    for sx, sy in ov.VILLAGE_STATUES:
        d.tiles[sy][sx] = Tile.WALL
    for tx, ty in ov.VILLAGE_TORCHES:
        d.tiles[ty][tx] = Tile.WALL

    # Дороги к вратам и сами врата.
    for key, gate in VILLAGE_GATES.items():
        _carve(d, ov.VILLAGE_CENTER, gate, half=1)
        _clear(d, gate, 2)
    for key, gate in VILLAGE_GATES.items():
        gx, gy = gate
        d.tiles[gy][gx] = Tile.GATE
        entry = LOCATION_ENTRY if key != 'goblin_village' else (LOCATION_W // 2,
                                                               LOCATION_H - 8)
        d.transitions[(gx, gy)] = {'to': key, 'entry': entry}

    # Доска поручений и старейшина должны стоять на полу.
    for px, py in (ov.QUEST_BOARD_VILLAGE_POS, ov.ELDER_POS):
        d.tiles[py][px] = Tile.FLOOR

    avoid = (ov.VILLAGE_CENTER, ov.ELDER_POS, ov.QUEST_BOARD_VILLAGE_POS)
    _scatter_items(d, 26, avoid=avoid, pool=('mushroom', 'wood', 'stone'))
    _scatter_bushes(d, 'village', 24, avoid=avoid)
    _spawn_monsters(d, ('deer', 'wolf', 'boar', 'rat', 'slime'), 16,
                    avoid=avoid, min_dist=ov.VILLAGE_CENTER)
    d.rooms = [Room(ov.VILLAGE_CENTER[0] - 1, ov.VILLAGE_CENTER[1] - 1, 3, 3)]
    return d


def _generate_biome(key):
    spec = LOCATIONS[key]
    biome = spec['biome']
    d = Dungeon(LOCATION_W, LOCATION_H)
    _fill_floor(d)
    center = (LOCATION_W // 2, LOCATION_H // 2)
    _clear(d, center, 10)
    gate_pos = (LOCATION_W // 2, LOCATION_H - 4)

    density = BIOME_DENSITY.get(biome, 0.4)
    for _ in range(120):
        cx = random.randint(2, d.w - 3)
        cy = random.randint(2, d.h - 3)
        radius = random.randint(1, 3)
        for yy in range(cy - radius, cy + radius + 1):
            for xx in range(cx - radius, cx + radius + 1):
                if not (0 <= xx < d.w and 0 <= yy < d.h):
                    continue
                if (xx - cx) ** 2 + (yy - cy) ** 2 > radius * radius:
                    continue
                if (xx - center[0]) ** 2 + (yy - center[1]) ** 2 < 110:
                    continue
                if xx == LOCATION_W // 2:
                    continue
                if random.random() < density:
                    d.tiles[yy][xx] = Tile.WALL

    # Дорога от врат к центру.
    _carve(d, gate_pos, center, half=1)
    _clear(d, (gate_pos[0], gate_pos[1] - 2), 2)
    d.tiles[gate_pos[1]][gate_pos[0]] = Tile.GATE
    d.transitions[gate_pos] = {'to': 'village',
                               'entry': VILLAGE_INWARD[key]}

    avoid = {gate_pos, center}

    if biome == 'rock':
        # Вход в подземелье на каменном перевале.
        dungeon_pos = (34, 14)
        _clear(d, dungeon_pos, 9)
        d.tiles[dungeon_pos[1]][dungeon_pos[0]] = Tile.STAIRS
        d.stairs_pos = dungeon_pos
        for px, py in ((25, 7), (43, 7), (25, 21), (43, 21)):
            d.tiles[py][px] = Tile.WALL
        _carve(d, center, dungeon_pos, half=1)
        d.tiles[dungeon_pos[1]][dungeon_pos[0]] = Tile.STAIRS

        # Вход в шахту — на склоне горы.
        mine_pos = (86, 12)
        _clear(d, mine_pos, 5)
        d.tiles[mine_pos[1]][mine_pos[0]] = Tile.GATE
        d.transitions[mine_pos] = {'to': 'mine', 'entry': (60, 62)}
        _carve(d, center, mine_pos, half=1)
        d.tiles[mine_pos[1]][mine_pos[0]] = Tile.GATE
        avoid |= {dungeon_pos, mine_pos}
        _add_ore(d, 40)

    _scatter_items(d, 22, avoid=avoid,
                   pool=('stone', 'ore', 'mushroom') if biome == 'rock'
                   else ('mushroom', 'wood', 'stone'))
    _scatter_bushes(d, key, 26 if biome != 'rock' else 10, avoid=avoid)
    _spawn_monsters(d, BIOME_POOLS.get(biome, ('deer', 'wolf')), 14,
                    avoid=avoid, min_dist=center)
    d.rooms = [Room(center[0] - 2, center[1] - 2, 5, 5)]
    return d


# ── Деревня гоблинов ──
LOCAL_GOBLIN_CENTER = (LOCATION_W // 2, LOCATION_H // 2)
LOCAL_GOBLIN_RECT = (LOCAL_GOBLIN_CENTER[0] - 16, LOCAL_GOBLIN_CENTER[1] - 13,
                     33, 27)


def _in_local_goblin_village(x, y):
    vx, vy, vw, vh = LOCAL_GOBLIN_RECT
    return vx <= x < vx + vw and vy <= y < vy + vh


def _generate_goblin_village():
    d = Dungeon(LOCATION_W, LOCATION_H)
    _fill_floor(d)
    cx, cy = LOCAL_GOBLIN_CENTER
    _clear(d, LOCAL_GOBLIN_CENTER, 15)

    for _ in range(100):
        x = random.randint(2, d.w - 3)
        y = random.randint(2, d.h - 3)
        radius = random.randint(1, 3)
        for yy in range(y - radius, y + radius + 1):
            for xx in range(x - radius, x + radius + 1):
                if not (0 <= xx < d.w and 0 <= yy < d.h):
                    continue
                if (xx - x) ** 2 + (yy - y) ** 2 > radius * radius:
                    continue
                if _in_local_goblin_village(xx, yy):
                    continue
                if xx == cx:
                    continue
                if random.random() < 0.5:
                    d.tiles[yy][xx] = Tile.WALL

    vx, vy, vw, vh = LOCAL_GOBLIN_RECT
    houses = (
        (vx + 2, vy + 2, 5, 4), (vx + 13, vy + 2, 6, 4), (vx + 25, vy + 2, 5, 4),
        (vx + 2, vy + 21, 5, 4), (vx + 13, vy + 21, 6, 4), (vx + 25, vy + 21, 5, 4),
    )
    for hx, hy, hw, hh in houses:
        for yy in range(hy, hy + hh):
            for xx in range(hx, hx + hw):
                d.tiles[yy][xx] = Tile.WALL
    totems = ((cx - 9, cy - 3), (cx + 9, cy - 3))
    for tx, ty in totems:
        d.tiles[ty][tx] = Tile.WALL
    campfire = (cx, cy - 4)
    d.tiles[campfire[1]][campfire[0]] = Tile.WALL

    gate_pos = (cx, LOCATION_H - 4)
    _carve(d, gate_pos, LOCAL_GOBLIN_CENTER, half=1)
    _clear(d, (cx, LOCATION_H - 6), 2)
    d.tiles[gate_pos[1]][gate_pos[0]] = Tile.GATE
    d.transitions[gate_pos] = {
        'to': 'village', 'entry': VILLAGE_INWARD['goblin_village']}

    avoid = {gate_pos, campfire}
    _scatter_items(d, 18, avoid=avoid, pool=('mushroom', 'wood', 'stone'))
    _scatter_bushes(d, 'goblin_village', 18, avoid=avoid)
    _spawn_monsters(d, ('goblin', 'archer', 'shaman', 'boar', 'wolf'), 20,
                    avoid=avoid, min_dist=LOCAL_GOBLIN_CENTER)
    # Гарантируем все три вида гоблинов.
    for i, key in enumerate(('goblin', 'archer', 'shaman')):
        m = MONSTERS[key]
        pos = (cx - 1 + i, cy)
        d.tiles[pos[1]][pos[0]] = Tile.FLOOR
        if any(o.x == pos[0] and o.y == pos[1] for o in d.monsters):
            continue
        d.monsters.append(Entity(
            pos[0], pos[1], m['hp'], m['hp'], m['atk'], m['df'],
            m['name'], m['ch'], m['color'], m['xp'],
            ability=m.get('ability')))
    d.rooms = [Room(cx - 2, cy - 2, 5, 5)]
    return d


def _generate_mine():
    d = Dungeon(LOCATION_W, LOCATION_H)
    rooms = []
    for _ in range(120):
        w = random.randint(6, 12)
        h = random.randint(4, 8)
        x = random.randint(4, d.w - w - 4)
        y = random.randint(4, 58 - h)
        room = Room(x, y, w, h)
        if any(room.intersects(r) for r in rooms):
            continue
        if room.x < 64 and room.x + room.w > 56 and room.y + room.h > 54:
            continue
        rooms.append(room)
        for yy in range(y, y + h):
            for xx in range(x, x + w):
                d.tiles[yy][xx] = Tile.FLOOR
        if len(rooms) >= 10:
            break
    if not rooms:
        rooms = [Room(54, 54, 9, 6), Room(20, 20, 9, 6)]
        for room in rooms:
            for yy in range(room.y, room.y + room.h):
                for xx in range(room.x, room.x + room.w):
                    d.tiles[yy][xx] = Tile.FLOOR

    rooms.sort(key=lambda r: r.center()[1], reverse=True)
    prev = rooms[0].center()
    _carve(d, prev, (60, 60), half=1)
    for room in rooms[1:]:
        center = room.center()
        _carve(d, prev, center, half=1)
        prev = center

    gate_pos = (60, LOCATION_H - 4)
    _clear(d, (60, 60), 3)
    _carve(d, (60, 60), gate_pos, half=1)
    d.tiles[gate_pos[1]][gate_pos[0]] = Tile.GATE
    d.transitions[gate_pos] = {'to': 'rock', 'entry': (86, 16)}

    _add_ore(d, 70)
    _scatter_items(d, 14, avoid={gate_pos}, pool=('stone', 'ore', 'mushroom'))
    _spawn_monsters(d, ('rat', 'goblin'), 12, avoid={gate_pos})
    d.rooms = rooms
    return d


def generate_location(key):
    """Создаёт карту локации с нуля (Game кэширует результат)."""
    if key == 'village':
        return _generate_village()
    if key == 'goblin_village':
        return _generate_goblin_village()
    if key == 'mine':
        return _generate_mine()
    if key in LOCATIONS:
        return _generate_biome(key)
    return _generate_village()
