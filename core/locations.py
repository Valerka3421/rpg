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
# От деревни остались только четыре перехода: север, юг, запад, восток.
VILLAGE_GATES = {
    'birch':  (50, 1),     # север
    'swamp':  (98, 35),    # восток
    'meadow': (50, 68),    # юг
    'autumn': (1, 35),     # запад
}


def _village_inward(gate):
    """Клетка сразу внутри деревни, рядом с вратами."""
    gx, gy = gate
    cx, cy = ov.VILLAGE_CENTER
    dx = 0 if gx == cx else (1 if gx < cx else -1)
    dy = 0 if gy == cy else (1 if gy < cy else -1)
    return (gx + dx, gy + dy)


VILLAGE_INWARD = {k: _village_inward(v) for k, v in VILLAGE_GATES.items()}


def _village_gate_side(gate):
    """Сторона врата деревни: по вектору от центра деревни к врату."""
    gx, gy = gate
    cx, cy = ov.VILLAGE_CENTER
    dx, dy = gx - cx, gy - cy
    if abs(dx) >= abs(dy):
        return 'e' if dx > 0 else 'w'
    return 's' if dy > 0 else 'n'


# ── Сетка локаций ──
# Каждая локация занимает клетку сетки: соседние клетки — смежные края,
# между ними можно ходить пешком через боковые врата со стрелками.
GRID_POS = {
    'pine': (0, 0), 'birch': (1, 0), 'rock': (2, 0),
    'autumn': (0, 1), 'village': (1, 1), 'swamp': (2, 1),
    'goblin_village': (3, 1),
    'beach': (0, 2), 'meadow': (1, 2), 'dead': (2, 2), 'graveyard': (3, 2),
    'ruins': (3, 3),
}

# Врата на краях карты локации: сторона → позиция врата.
SIDE_POS = {
    'n': (LOCATION_W // 2, 1),
    's': (LOCATION_W // 2, LOCATION_H - 4),
    'e': (LOCATION_W - 2, LOCATION_H // 2),
    'w': (1, LOCATION_H // 2),
}

# Точка появления в 2 клетках внутрь от врата той же стороны.
SIDE_ENTRY = {
    'n': (LOCATION_W // 2, 3),
    's': (LOCATION_W // 2, LOCATION_H - 6),
    'e': (LOCATION_W - 4, LOCATION_H // 2),
    'w': (3, LOCATION_H // 2),
}

# Врата каждой локации: (позиция, сторона, куда, точка появления).
BIOME_GATES = {
    'pine': (
        (SIDE_POS['e'], 'e', 'birch',   SIDE_ENTRY['w']),
        ((40, LOCATION_H - 4), 's', 'autumn', SIDE_ENTRY['n']),
    ),
    'birch': (
        (SIDE_POS['w'], 'w', 'pine', SIDE_ENTRY['e']),
        (SIDE_POS['e'], 'e', 'rock', SIDE_ENTRY['w']),
        (SIDE_POS['s'], 's', 'village', VILLAGE_INWARD['birch']),
    ),
    'rock': (
        (SIDE_POS['w'], 'w', 'birch',  SIDE_ENTRY['e']),
        ((80, LOCATION_H - 4), 's', 'swamp', SIDE_ENTRY['n']),
    ),
    'autumn': (
        (SIDE_POS['n'], 'n', 'pine',  (40, LOCATION_H - 6)),
        (SIDE_POS['s'], 's', 'beach', SIDE_ENTRY['n']),
        (SIDE_POS['e'], 'e', 'village', VILLAGE_INWARD['autumn']),
    ),
    'swamp': (
        (SIDE_POS['n'], 'n', 'rock',  (80, LOCATION_H - 6)),
        (SIDE_POS['s'], 's', 'dead',  SIDE_ENTRY['n']),
        (SIDE_POS['w'], 'w', 'village', VILLAGE_INWARD['swamp']),
        (SIDE_POS['e'], 'e', 'goblin_village', (LOCATION_W // 2, 4)),
    ),
    'beach': (
        (SIDE_POS['n'], 'n', 'autumn', SIDE_ENTRY['s']),
        (SIDE_POS['e'], 'e', 'meadow', SIDE_ENTRY['w']),
    ),
    'meadow': (
        (SIDE_POS['n'], 'n', 'village', VILLAGE_INWARD['meadow']),
        (SIDE_POS['w'], 'w', 'beach',   SIDE_ENTRY['e']),
        (SIDE_POS['e'], 'e', 'dead',    SIDE_ENTRY['w']),
    ),
    'dead': (
        (SIDE_POS['n'], 'n', 'swamp',     SIDE_ENTRY['s']),
        (SIDE_POS['w'], 'w', 'meadow',    SIDE_ENTRY['e']),
        (SIDE_POS['e'], 'e', 'graveyard', SIDE_ENTRY['w']),
    ),
    'graveyard': (
        (SIDE_POS['w'], 'w', 'dead', SIDE_ENTRY['e']),
        (SIDE_POS['s'], 's', 'ruins', SIDE_ENTRY['n']),
    ),
    'ruins': (
        (SIDE_POS['n'], 'n', 'graveyard', SIDE_ENTRY['s']),
    ),
}

BIOME_POOLS = {
    'pine':   ('deer', 'wolf', 'rat'),
    'birch':  ('deer', 'slime', 'rat'),
    'rock':   ('wolf', 'bear'),
    'autumn': ('deer', 'boar', 'wolf'),
    'swamp':  ('slime', 'rat', 'boar'),
    'beach':  ('rat', 'slime', 'deer'),
    'meadow': ('deer', 'boar', 'rat'),
    'dead':   ('wolf', 'bear', 'rat'),
    'graveyard': ('skeleton', 'wolf', 'rat'),
    'ruins':  ('skeleton', 'rat', 'slime'),
}

BIOME_DENSITY = {
    'pine': 0.46, 'birch': 0.36, 'rock': 0.30, 'autumn': 0.40,
    'swamp': 0.30, 'beach': 0.35, 'meadow': 0.17, 'dead': 0.36,
    'graveyard': 0.12, 'ruins': 0.28,
}

# Кусты по биомам: на кладбище и в скалах их нет/мало.
BIOME_BUSHES = {
    'rock': 10, 'graveyard': 0,
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
    if _biome == 'dark':          # заменён пляжем моря
        continue
    LOCATIONS[_biome] = {
        'title': _title, 'kind': 'biome', 'biome': _biome,
        'w': LOCATION_W, 'h': LOCATION_H,
    }
LOCATIONS['graveyard'] = {
    'title': 'Старое кладбище', 'kind': 'biome', 'biome': 'graveyard',
    'w': LOCATION_W, 'h': LOCATION_H,
}
LOCATIONS['beach'] = {
    'title': 'Пляж моря', 'kind': 'biome', 'biome': 'beach',
    'w': LOCATION_W, 'h': LOCATION_H,
}
LOCATIONS['ruins'] = {
    'title': 'Руины города', 'kind': 'ruins', 'biome': 'ruins',
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

_ROAD_CELLS_CACHE = {}


def road_cells(key):
    """Клетки дорог локации (для визуала и запрета спавна на дороге).

    Дорога — соединение центра локации с каждым вратём. На дорогах не
    спавнятся предметы, кусты и мобы.
    """
    cached = _ROAD_CELLS_CACHE.get(key)
    if cached is None:
        if key == 'village':
            cached = set(VILLAGE_ROADS)
        elif key == 'mine':
            cached = _l_path((60, 60), (60, LOCATION_H - 4))
        elif key == 'goblin_village':
            cached = _l_path(LOCAL_GOBLIN_CENTER,
                             (LOCAL_GOBLIN_CENTER[0], 4))
        else:
            cached = set()
            center = (LOCATION_W // 2, LOCATION_H // 2)
            for pos, _side, _to, _entry in BIOME_GATES.get(key, ()):
                cached |= _l_path(center, pos)
        _ROAD_CELLS_CACHE[key] = cached
    return cached


# Появление в биоме при выходе из деревни: у его врат на деревню.
VILLAGE_GATE_ENTRY = {
    'autumn': SIDE_ENTRY['e'],   # врата autumn→village на востоке
    'swamp': SIDE_ENTRY['w'],    # врата swamp→village на западе
    'meadow': SIDE_ENTRY['n'],   # врата meadow→village на севере
    'birch': SIDE_ENTRY['s'],    # врата birch→village на юге
}

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
        return 'mine_ground'
    if kind == 'ruins':
        return ('road' if (x, y) in road_cells(key)
                else 'dungeon_ruins' if (x + y) % 5 == 0
                else 'dungeon_ground')
    biome = spec.get('biome', 'pine')
    if biome == 'beach':
        return 'road' if (x, y) in road_cells(key) else 'sand'
    if biome == 'graveyard':
        # Кладбище покрыто выцветшей травой мёртвого леса.
        base = 'grass_dead'
    else:
        base = f'grass_{biome}'
    return 'road' if (x, y) in road_cells(key) else base


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
        return 'mine_wall'
    if kind == 'ruins':
        return 'ruins_pillar' if (x * 7 + y * 3) % 4 == 0 else 'wall'
    biome = spec.get('biome', 'pine')
    if biome == 'beach':
        return 'water'
    if biome == 'graveyard':
        return 'tombstone'
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
        side = _village_gate_side(gate)
        d.tiles[gy][gx] = Tile.GATE
        d.transitions[(gx, gy)] = {'to': key, 'entry': VILLAGE_GATE_ENTRY[key],
                                   'side': side}

    # Доска поручений и старейшина должны стоять на полу.
    for px, py in (ov.QUEST_BOARD_VILLAGE_POS, ov.ELDER_POS):
        d.tiles[py][px] = Tile.FLOOR

    avoid = ({ov.VILLAGE_CENTER, ov.ELDER_POS, ov.QUEST_BOARD_VILLAGE_POS}
             | road_cells('village'))
    _scatter_items(d, 26, avoid=avoid, pool=('mushroom', 'wood', 'stone'))
    _scatter_bushes(d, 'village', 24, avoid=avoid)
    _spawn_monsters(d, ('deer', 'wolf', 'boar', 'rat', 'slime'), 16,
                    avoid=avoid, min_dist=ov.VILLAGE_CENTER)
    d.rooms = [Room(ov.VILLAGE_CENTER[0] - 1, ov.VILLAGE_CENTER[1] - 1, 3, 3)]
    return d


def _scatter_ruins(d, center, density):
    """Развалины: обломки стен, колонны и пустые кварталы."""
    for _ in range(260):
        cx = random.randint(2, d.w - 3)
        cy = random.randint(2, d.h - 3)
        if (cx - center[0]) ** 2 + (cy - center[1]) ** 2 < 130:
            continue
        if random.random() > density:
            continue
        length = random.randint(1, 4)
        horizontal = random.random() < 0.5
        for i in range(length):
            x = cx + (i if horizontal else 0)
            y = cy + (0 if horizontal else i)
            if 0 <= x < d.w and 0 <= y < d.h:
                d.tiles[y][x] = Tile.WALL


def _generate_ruins(key):
    """Руины города: по дороге от кладбища, иногда — сундук с ресурсами."""
    spec = LOCATIONS[key]
    d = Dungeon(LOCATION_W, LOCATION_H)
    _fill_floor(d)
    center = (LOCATION_W // 2, LOCATION_H // 2)
    _clear(d, center, 12)
    _scatter_ruins(d, center, BIOME_DENSITY.get('ruins', 0.28))

    gates = BIOME_GATES.get(key, ())
    avoid = set()
    for pos, side, to, entry in gates:
        _carve(d, center, pos, half=1)
        _clear(d, pos, 2)
        d.tiles[pos[1]][pos[0]] = Tile.GATE
        d.transitions[pos] = {'to': to, 'entry': entry, 'side': side}
        avoid.add(pos)

    # 25% шанс: сундук с рандомными базовыми ресурсами.
    if random.random() < 0.25:
        for _ in range(200):
            cx = random.randint(6, d.w - 7)
            cy = random.randint(6, d.h - 7)
            if (cx, cy) in avoid or (cx, cy) in d.transitions:
                continue
            if (cx - center[0]) ** 2 + (cy - center[1]) ** 2 < 60:
                continue
            if d.tiles[cy][cx] != Tile.FLOOR:
                continue
            d.tiles[cy][cx] = Tile.CHEST
            pool = ('wood', 'stone', 'ore', 'hide', 'bone', 'berry',
                    'mushroom', 'magic_powder')
            loot = {}
            for base in random.sample(pool, random.randint(2, 4)):
                loot[base] = random.randint(5, 20)
            d.chest_loot[(cx, cy)] = loot
            avoid.add((cx, cy))
            break

    roads = road_cells(key)
    _scatter_items(d, 14, avoid=avoid | roads,
                   pool=('stone', 'ore', 'bone'))
    _spawn_monsters(d, BIOME_POOLS.get('ruins', ('skeleton', 'rat')), 12,
                    avoid=avoid | roads, min_dist=center)
    d.rooms = [Room(center[0] - 2, center[1] - 2, 5, 5)]
    return d


def _generate_biome(key):
    spec = LOCATIONS[key]
    biome = spec['biome']
    d = Dungeon(LOCATION_W, LOCATION_H)
    _fill_floor(d)
    center = (LOCATION_W // 2, LOCATION_H // 2)
    _clear(d, center, 10)

    density = BIOME_DENSITY.get(biome, 0.4)
    if biome == 'beach':
        # Море — на юге и западе; песчаный берег остаётся на севере/востоке.
        for y in range(d.h):
            for x in range(d.w):
                if y > d.h * 0.58 or x < d.w * 0.20:
                    d.tiles[y][x] = Tile.WALL
        d.tiles[center[1]][center[0]] = Tile.FLOOR
        _clear(d, center, 10)
    elif biome == 'ruins':
        _scatter_ruins(d, center, density)
    else:
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
                    if random.random() < density:
                        d.tiles[yy][xx] = Tile.WALL

    avoid = {center}
    gates = BIOME_GATES.get(key, ())
    for pos, side, to, entry in gates:
        _carve(d, center, pos, half=1)
        _clear(d, pos, 2)
        d.tiles[pos[1]][pos[0]] = Tile.GATE
        d.transitions[pos] = {'to': to, 'entry': entry, 'side': side}
        avoid.add(pos)

    if biome == 'rock':
        # Вход в шахту — на склоне горы (север карты).
        mine_pos = (86, 12)
        _clear(d, mine_pos, 5)
        d.tiles[mine_pos[1]][mine_pos[0]] = Tile.GATE
        d.transitions[mine_pos] = {'to': 'mine', 'entry': (60, LOCATION_H - 6),
                                   'side': 'n'}
        _carve(d, center, mine_pos, half=1)
        d.tiles[mine_pos[1]][mine_pos[0]] = Tile.GATE
        avoid |= {mine_pos}
        _add_ore(d, 40)

    if biome == 'graveyard':
        # Вход в главное подземелье — в центре кладбища.
        dungeon_pos = (60, 33)
        _clear(d, dungeon_pos, 4)
        d.tiles[dungeon_pos[1]][dungeon_pos[0]] = Tile.STAIRS
        d.stairs_pos = dungeon_pos
        avoid |= {dungeon_pos}

    roads = road_cells(key)
    _scatter_items(d, 22, avoid=avoid | roads,
                   pool=('stone', 'ore', 'mushroom') if biome == 'rock'
                   else ('mushroom', 'wood', 'stone'))
    bushes = BIOME_BUSHES.get(biome, 26)
    if bushes:
        _scatter_bushes(d, key, bushes, avoid=avoid | roads)
    _spawn_monsters(d, BIOME_POOLS.get(biome, ('deer', 'wolf')), 14,
                    avoid=avoid | roads, min_dist=center)
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

    # Северные врата ведут к болоту (деревня осталась с четырьмя переходами).
    gate_pos = SIDE_POS['n']
    _carve(d, gate_pos, LOCAL_GOBLIN_CENTER, half=1)
    _clear(d, (cx, 5), 2)
    d.tiles[gate_pos[1]][gate_pos[0]] = Tile.GATE
    d.transitions[gate_pos] = {
        'to': 'swamp', 'entry': SIDE_ENTRY['e'],
        'side': 'n'}

    avoid = ({gate_pos, campfire} | road_cells('goblin_village'))
    _scatter_items(d, 18, avoid=avoid, pool=('mushroom', 'wood', 'stone'))
    _scatter_bushes(d, 'goblin_village', 18, avoid=avoid)
    _spawn_monsters(d, ('goblin', 'archer', 'shaman', 'boar', 'wolf'), 20,
                    avoid=avoid, min_dist=LOCAL_GOBLIN_CENTER)
    # Гарантируем все три вида гоблинов (севернее дороги, в центре площади).
    for i, key in enumerate(('goblin', 'archer', 'shaman')):
        m = MONSTERS[key]
        pos = (cx - 1 + i, cy - 2)
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
    d.transitions[gate_pos] = {'to': 'rock', 'entry': (86, 14), 'side': 's'}

    roads = road_cells('mine')
    _add_ore(d, 70)
    _scatter_items(d, 14, avoid={gate_pos} | roads,
                   pool=('stone', 'ore', 'mushroom'))
    _spawn_monsters(d, ('rat', 'goblin'), 12, avoid={gate_pos} | roads)
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
    if key == 'ruins':
        return _generate_ruins(key)
    if key in LOCATIONS:
        return _generate_biome(key)
    return _generate_village()
