"""Генерация миров, LOS, FOV, BFS."""
import random
from collections import deque

from core import overworld as ov
from core.config import FOV_RADIUS, MAP_H, MAP_W, Tile
from core.data import BOSSES, ITEMS, MONSTERS, scale_boss_stats, scale_stats
from core.entities import Dungeon, Entity, Room

OVERWORLD_RESOURCES = ('mushroom', 'wood', 'stone', 'ore')

# Ягоды больше не лежат предметом на земле: их собирают с кустов
# (core.harvest). Константы для генерации.
BUSH_COUNT_MIN = 36
TREE_HARVEST_COUNT = 70

# В лесу могут встретиться только эти обитатели. Гоблины и скелеты
# убраны из лесного спавна: гоблины живут в своей деревне, скелеты —
# в подземелье.
FOREST_MONSTERS = ('deer', 'wolf', 'bear', 'slime', 'boar', 'rat')

# Кто водится в каждом лесном крае. Ключи должны быть в MONSTERS.
BIOME_MONSTERS = {
    'pine':   ('deer', 'wolf', 'rat'),
    'birch':  ('deer', 'slime', 'rat'),
    'rock':   ('wolf', 'bear'),
    'autumn': ('deer', 'boar', 'wolf'),
    'swamp':  ('slime', 'rat', 'boar'),
    'dark':   ('wolf', 'bear', 'slime'),
    'meadow': ('deer', 'boar', 'rat'),
    'dead':   ('wolf', 'bear', 'rat'),
    # Пляж: водяные крысы, слизни, кабаны приходят из леса.
    'beach':  ('rat', 'slime', 'boar'),
}

# Спавн в Деревне гоблинов: все виды гоблинов, кабаны и волки.
GOBLIN_VILLAGE_MONSTERS = ('goblin', 'archer', 'shaman', 'boar', 'wolf')

# Сколько рощ и какого размера сеять в каждом крае.
BIOME_FORESTS = {
    'pine':   (55, 3),
    'birch':  (42, 2),
    'rock':   (30, 3),
    'autumn': (46, 3),
    'swamp':  (36, 2),
    'dark':   (66, 3),
    'meadow': (20, 2),
    'dead':   (40, 2),
    # Пляж: редкие кусты, основная полоса — песок и вода.
    'beach':  (8, 1),
}


def _scatter_harvestables(d, reachable):
    """Деревья для рубки и кусты ягод на поверхности."""
    from core.harvest import make_harvestable
    # Деревья: выбираем существующие лесные стены, чтобы текстура и
    # проходимость остались прежними.
    tree_spots = []
    for (x, y) in reachable:
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if not (0 <= nx < d.w and 0 <= ny < d.h):
                continue
            if d.tiles[ny][nx] != Tile.WALL:
                continue
            kind = ov.wall_kind(nx, ny)
            if kind.startswith('tree') or kind == 'rock':
                tree_spots.append((nx, ny))
    random.shuffle(tree_spots)
    for pos in tree_spots[:TREE_HARVEST_COUNT]:
        if pos not in d.harvestables:
            d.harvestables[pos] = make_harvestable(
                'tree' if not ov.in_dungeon_ruins(*pos) else 'ore')

    # Кусты ягод — на открытой траве, подальше от деревень и дорог.
    bush_spots = [p for p in reachable
                  if d.tiles[p[1]][p[0]] == Tile.FLOOR
                  and not ov.in_village(*p) and not ov.in_goblin_village(*p)
                  and not ov.is_road(*p) and not ov.in_dungeon_ruins(*p)
                  and p not in d.items and p != ov.DUNGEON_POS
                  and p not in d.harvestables]
    random.shuffle(bush_spots)
    for pos in bush_spots[:BUSH_COUNT_MIN]:
        d.harvestables[pos] = make_harvestable('bush')


def _flood_fill_reachable(d, start):
    seen = {start}
    q = deque([start])
    while q:
        cx, cy = q.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = cx + dx, cy + dy
            if not (0 <= nx < MAP_W and 0 <= ny < MAP_H):
                continue
            if (nx, ny) in seen:
                continue
            if d.tiles[ny][nx] in (Tile.WALL, Tile.LOCKED_DOOR):
                continue
            seen.add((nx, ny))
            q.append((nx, ny))
    return seen


def _carve_road(d, x0, y0, x1, y1, half_width=None):
    """Прорубить прямую дорогу в лесу (лес уже расставлен)."""
    half = ov.ROAD_HALF_WIDTH if half_width is None else half_width
    if y0 == y1:
        for x in range(min(x0, x1), max(x0, x1) + 1):
            for y in range(y0 - half, y0 + half + 1):
                if 0 <= x < d.w and 0 <= y < d.h:
                    d.tiles[y][x] = Tile.FLOOR
    elif x0 == x1:
        for y in range(min(y0, y1), max(y0, y1) + 1):
            for x in range(x0 - half, x0 + half + 1):
                if 0 <= x < d.w and 0 <= y < d.h:
                    d.tiles[y][x] = Tile.FLOOR


def _clear_circle(d, center, radius):
    cx, cy = center
    for y in range(max(0, cy - radius), min(d.h, cy + radius + 1)):
        for x in range(max(0, cx - radius), min(d.w, cx + radius + 1)):
            if (x - cx) ** 2 + (y - cy) ** 2 <= radius * radius:
                d.tiles[y][x] = Tile.FLOOR


def _plant_forests(d):
    """Насыпать рощи в восьми внешних регионах, не заходя в деревню."""
    for rx in range(3):
        for ry in range(3):
            biome = ov.BIOME_NAMES.get((rx, ry))
            if biome is None:                 # центр — деревня
                continue
            cluster_count, max_radius = BIOME_FORESTS[biome]
            x0, x1 = ov.REGION_X[rx], ov.REGION_X[rx + 1]
            y0, y1 = ov.REGION_Y[ry], ov.REGION_Y[ry + 1]
            for _ in range(cluster_count):
                cx = random.randint(x0 + 1, x1 - 2)
                cy = random.randint(y0 + 1, y1 - 2)
                r = random.randint(1, max_radius)
                for yy in range(cy - r, cy + r + 1):
                    for xx in range(cx - r, cx + r + 1):
                        if not (0 <= yy < MAP_H and 0 <= xx < MAP_W):
                            continue
                        if (xx - cx) ** 2 + (yy - cy) ** 2 > r * r:
                            continue
                        if ov.in_village(xx, yy) or ov.is_road(xx, yy):
                            continue
                        if ov.in_dungeon_clearing(xx, yy):
                            continue
                        if random.random() < 0.58:
                            d.tiles[yy][xx] = Tile.WALL


def _plant_goblin_wilds(d):
    """Лес вокруг Деревни гоблинов — только восточная часть карты."""
    for _ in range(85):
        cx = random.randint(ov.LEGACY_MAP_W + 1, MAP_W - 2)
        cy = random.randint(1, MAP_H - 2)
        r = random.randint(1, 3)
        for yy in range(cy - r, cy + r + 1):
            for xx in range(cx - r, cx + r + 1):
                if not (0 <= yy < MAP_H and 0 <= xx < MAP_W):
                    continue
                if (xx - cx) ** 2 + (yy - cy) ** 2 > r * r:
                    continue
                if ov.in_goblin_village(xx, yy) or ov.is_road(xx, yy):
                    continue
                if random.random() < 0.52:
                    d.tiles[yy][xx] = Tile.WALL


def _build_village(d):
    for hx, hy, hw, hh in ov.VILLAGE_HOUSES:
        for yy in range(hy, hy + hh):
            for xx in range(hx, hx + hw):
                d.tiles[yy][xx] = Tile.WALL
    # Городская стена: кольцо с воротами на четырёх дорогах.
    for wx, wy in ov.CITY_WALL_CELLS:
        d.tiles[wy][wx] = Tile.WALL
    wx, wy = ov.WELL_POS
    d.tiles[wy][wx] = Tile.WALL
    for sx, sy in ov.VILLAGE_STALLS:      d.tiles[sy][sx] = Tile.WALL
    for sx, sy in ov.VILLAGE_STATUES:     d.tiles[sy][sx] = Tile.WALL
    for tx, ty in ov.VILLAGE_TORCHES:     d.tiles[ty][tx] = Tile.WALL


def _build_goblin_village(d):
    """Хижины, тотемы и костёр гоблинов. Дороги прорубаются после."""
    for hx, hy, hw, hh in ov.GOBLIN_HOUSES:
        for yy in range(hy, hy + hh):
            for xx in range(hx, hx + hw):
                if 0 <= yy < d.h and 0 <= xx < d.w:
                    d.tiles[yy][xx] = Tile.WALL
    cx, cy = ov.GOBLIN_CAMPFIRE
    d.tiles[cy][cx] = Tile.WALL
    for tx, ty in ov.GOBLIN_TOTEMS:
        d.tiles[ty][tx] = Tile.WALL


def generate_overworld() -> Dungeon:
    """Поверхность: деревня в центре, 8 биомов и вход в подземелье."""
    d = Dungeon(MAP_W, MAP_H)
    for y in range(MAP_H):
        for x in range(MAP_W):
            d.tiles[y][x] = Tile.FLOOR

    # 1. Леса по краям карты и в новых восточных землях
    _plant_forests(d)
    _plant_goblin_wilds(d)

    # 2. Деревни: люди в центре, гоблины на востоке
    _build_village(d)
    _build_goblin_village(d)

    # 3. Дороги — после леса, поэтому они всегда проходимы
    for x0, y0, x1, y1 in ov.ROAD_SEGMENTS:
        _carve_road(d, x0, y0, x1, y1)

    # 4. Каменистая поляна и вход в подземелье на северо-востоке
    _clear_circle(d, ov.DUNGEON_POS, ov.DUNGEON_RUINS_RADIUS)
    for px, py in ov.RUINS_PILLARS:
        if 0 <= px < MAP_W and 0 <= py < MAP_H:
            d.tiles[py][px] = Tile.WALL
    ex, ey = ov.DUNGEON_POS
    d.tiles[ey][ex] = Tile.STAIRS
    d.stairs_pos = (ex, ey)

    # 5. Гарантировать достижимость входа из деревни
    spawn = ov.VILLAGE_CENTER
    reachable = _flood_fill_reachable(d, spawn)
    if (ex, ey) not in reachable:
        _carve_road(d, spawn[0], spawn[1], ex, spawn[1])
        _carve_road(d, ex, spawn[1], ex, ey)
        reachable = _flood_fill_reachable(d, spawn)

    # 6. Ресурсы по всем краям, кроме деревни
    spots = [p for p in reachable
             if not ov.in_village(*p) and not ov.in_goblin_village(*p)
             and p != (ex, ey)]
    random.shuffle(spots)
    for rx, ry in spots[:96]:
        if (rx, ry) not in d.items:
            d.items[(rx, ry)] = random.choice(OVERWORLD_RESOURCES)

    # Кусты ягод и деревья для рубки.
    _scatter_harvestables(d, reachable)

    def spawn_monster(x, y, key):
        m = MONSTERS[key]
        d.monsters.append(Entity(
            x, y, m['hp'], m['hp'], m['atk'], m['df'],
            m['name'], m['ch'], m['color'], m['xp'],
            ability=m.get('ability')))

    # 7. Лесные мобы. Деревни остаются безопасными зонами.
    monster_spots = []
    for p in spots:
        if any(o.x == p[0] and o.y == p[1] for o in d.monsters):
            continue
        # Безопасная зона: не только сам город, но и полоса вокруг стен.
        if ov.in_city_safe(p[0], p[1], margin=8):
            continue
        if (p[0] - ex) ** 2 + (p[1] - ey) ** 2 <= 9:
            continue
        monster_spots.append(p)
    random.shuffle(monster_spots)
    for mx, my in monster_spots:
        if len(d.monsters) >= 40:
            break
        biome = ov.biome_at(mx, my)
        pool = [k for k in BIOME_MONSTERS.get(biome, FOREST_MONSTERS)
                if k in MONSTERS and k in FOREST_MONSTERS]
        if not pool:
            pool = [k for k in FOREST_MONSTERS if k in MONSTERS]
        spawn_monster(mx, my, random.choice(pool))

    # 8. Деревня гоблинов: все виды гоблинов, кабаны и волки.
    gx, gy = ov.GOBLIN_VILLAGE_CENTER
    goblin_spots = [p for p in reachable
                    if ov.in_goblin_village(*p)
                    and p != (ex, ey)
                    and not any(o.x == p[0] and o.y == p[1]
                                for o in d.monsters)
                    and (p[0] - gx) ** 2 + (p[1] - gy) ** 2 > 4
                    and not ov.is_goblin_totem(*p)
                    and not ov.is_goblin_campfire(*p)]
    random.shuffle(goblin_spots)
    # Сначала гарантируем по одному гоблину каждого вида.
    guaranteed = [k for k in ('goblin', 'archer', 'shaman')
                  if k in MONSTERS]
    for i, (mx, my) in enumerate(goblin_spots[:14]):
        if i < len(guaranteed):
            key = guaranteed[i]
        else:
            pool = [k for k in GOBLIN_VILLAGE_MONSTERS if k in MONSTERS]
            key = random.choice(pool)
        spawn_monster(mx, my, key)

    # Комната появления = центр деревни (её использует и мультиплеер)
    d.rooms = [Room(spawn[0] - 1, spawn[1] - 1, 3, 3)]

    # 9. Руины древнего города в юго-восточном биоме 'dead'.
    _build_city_ruins(d)

    # 10. Кардинальные переходы (Tile.GATE) на краях карты — 4 выхода.
    _place_cardinal_gates(d)

    return d


def _build_city_ruins(d):
    """Руины древнего города: разрушенные здания, статуи, заросшие дороги.

    С 25% шансом в центре руин появляется сундук (Tile.CHEST) с базовыми
    ресурсами (wood, stone, ore, hide, berry, mushroom) — 3-8 штук 3-х
    разных видов. Сундук открывается по «E», как обычно.
    """
    for bx, by, bw, bh in ov.CITY_RUINS_BUILDINGS:
        for yy in range(by, by + bh):
            for xx in range(bx, bx + bw):
                if 0 <= yy < d.h and 0 <= xx < d.w:
                    d.tiles[yy][xx] = Tile.WALL
    for sx, sy in ov.CITY_RUINS_STATUES:
        if 0 <= sy < d.h and 0 <= sx < d.w:
            d.tiles[sy][sx] = Tile.WALL
    for x0, y0, x1, y1 in ov.CITY_RUINS_ROADS:
        _carve_road(d, x0, y0, x1, y1)
    # 25% шанс сундука с базовыми ресурсами
    if random.random() < ov.CITY_RUINS_CHEST_CHANCE:
        rx, ry, rw, rh = ov.CITY_RUINS_RECT
        cx = rx + rw // 2
        cy = ry + rh // 2
        if 0 <= cy < d.h and 0 <= cx < d.w and d.tiles[cy][cx] == Tile.FLOOR:
            d.tiles[cy][cx] = Tile.CHEST
            loot = {}
            for _ in range(3):
                key = random.choice(ov.CITY_RUINS_BASE_RESOURCES)
                loot[key] = random.randint(3, 8)
            d.chest_loot[(cx, cy)] = loot


def _place_cardinal_gates(d):
    """4 перехода на краях карты (север, юг, запад, восток).

    Каждый — Tile.GATE с записью в ``d.transitions``. Это «выходы» из
    мира деревни; сейчас они ведут «наружу» (заглушка) — в future могут
    стать переходами на отдельные карты-локации.
    """
    for direction, (gx, gy) in ov.GATE_POSITIONS.items():
        if 0 <= gy < d.h and 0 <= gx < d.w:
            d.tiles[gy][gx] = Tile.GATE
            d.transitions[(gx, gy)] = {'to': direction,
                                         'entry': (gx, gy)}


def place_entry_tile(d, depth):
    """Ставит рядом с комнатой появления лестницу «назад».

    Глубина 1 — EXIT на поверхность, глубина 2+ — UPSTAIRS на этаж выше.
    Идемпотентна: если лестница уже есть, ничего не меняет (нужно для
    старых сейвов и кэша этажей).
    """
    want = Tile.EXIT if depth <= 1 else Tile.UPSTAIRS
    if not d.rooms:
        return None
    cx, cy = d.rooms[0].center()
    for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)):
        x, y = cx + dx, cy + dy
        if 0 <= x < d.w and 0 <= y < d.h and d.tiles[y][x] == want:
            return (x, y)
    for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)):
        x, y = cx + dx, cy + dy
        if not (0 <= x < d.w and 0 <= y < d.h):
            continue
        if d.tiles[y][x] != Tile.FLOOR:
            continue
        if d.stairs_pos and (x, y) == d.stairs_pos:
            continue
        d.tiles[y][x] = want
        return (x, y)
    return None


# ════════════════════════ БОСС-КОМПЛЕКС ════════════════════════
TREASURY_SIZE = 5


def _treasury_candidates(arena):
    """Четыре позиции казны — вплотную к арене через один ряд стен."""
    w = h = TREASURY_SIZE
    ax, ay = arena.center()
    return [
        ('right', Room(arena.x + arena.w + 1, ay - h // 2, w, h)),
        ('left', Room(arena.x - w - 1, ay - h // 2, w, h)),
        ('bottom', Room(ax - w // 2, arena.y + arena.h + 1, w, h)),
        ('top', Room(ax - w // 2, arena.y - h - 1, w, h)),
    ]


def _ring_cells(room):
    """Клетки по периметру комнаты (снаружи, без самой комнаты)."""
    cells = []
    for y in range(room.y - 1, room.y + room.h + 1):
        for x in range(room.x - 1, room.x + room.w + 1):
            inside = (room.x <= x < room.x + room.w
                      and room.y <= y < room.y + room.h)
            if not inside:
                cells.append((x, y))
    return cells


def _door_cell(arena, treasury, side):
    """Единственная клетка между ареной и казной (там будет дверь)."""
    if side == 'right':
        return (arena.x + arena.w, treasury.center()[1])
    if side == 'left':
        return (arena.x - 1, treasury.center()[1])
    if side == 'bottom':
        return (treasury.center()[0], arena.y + arena.h)
    return (treasury.center()[0], arena.y - 1)


def _carve_treasury(d, rooms, arena):
    """Казна 5×5, соединённая только с ареной босса одной дверью.

    Ищем место вплотную к арене (через один ряд стен). Внутри казны и
    по её периметру не должно быть пола — тогда никакая другая комната
    или коридор не смогут к ней примкнуть. Возвращает
    ``(treasury, door_cell)`` или ``None``, если места нет.
    """
    for side, treasury in _treasury_candidates(arena):
        if treasury.x < 1 or treasury.y < 1:
            continue
        if treasury.x + treasury.w >= d.w - 1 or treasury.y + treasury.h >= d.h - 1:
            continue
        if any(treasury.intersects(r, pad=0) for r in rooms):
            continue
        # Внутри казны ещё не должно быть пола: иначе через неё уже
        # проходит чужой коридор.
        if any(d.tiles[y][x] != Tile.WALL
               for y in range(treasury.y, treasury.y + treasury.h)
               for x in range(treasury.x, treasury.x + treasury.w)):
            continue
        door = _door_cell(arena, treasury, side)
        if not (0 <= door[0] < d.w and 0 <= door[1] < d.h):
            continue
        if d.tiles[door[1]][door[0]] != Tile.WALL:
            continue
        ok = True
        for (x, y) in _ring_cells(treasury):
            if not (0 <= x < d.w and 0 <= y < d.h):
                ok = False
                break
            if (x, y) == door:
                continue
            if d.tiles[y][x] != Tile.WALL:
                ok = False
                break
        if not ok:
            continue

        for yy in range(treasury.y, treasury.y + treasury.h):
            for xx in range(treasury.x, treasury.x + treasury.w):
                d.tiles[yy][xx] = Tile.FLOOR
        d.tiles[door[1]][door[0]] = Tile.LOCKED_DOOR
        return treasury, door
    return None


def clear_boss_barriers(d):
    """После победы над боссом: открыть замурованные выходы арены.

    Возвращает True, если тайлы изменились (для перерисовки карты).
    """
    barrier = getattr(d, 'barrier_tiles', None)
    if not barrier:
        return False
    changed = False
    for (x, y) in list(barrier):
        if 0 <= x < d.w and 0 <= y < d.h and d.tiles[y][x] == Tile.WALL:
            d.tiles[y][x] = Tile.FLOOR
            changed = True
    d.barrier_tiles = set()
    return changed


def generate_dungeon(depth: int, _attempt: int = 0) -> Dungeon:
    """Переписанная генерация подземелья.

    Изменения относительно старой версии:
    * Узкие (1-клеточные) L-коридоры вместо 2-клеточных — это roguelike-стандарт.
    * Connectivity check: BFS из первой комнаты проверяет, что все комнаты достижимы.
    * Арена босса = самая большая комната (не последняя добавленная).
    * Fallback создаёт две РАЗНЫЕ комнаты, а не две одинаковые.
    * Сундук казны — только на боссовом этаже, соединён с ареной одной дверью.
    """
    from core.config import FOG_OF_WAR_ENABLED  # noqa: F401  (заглушка для будущего)
    d = Dungeon(MAP_W, MAP_H)
    rooms = []

    margin_x = max(1, (ov.LEGACY_MAP_W - 60) // 2)
    margin_y = max(1, (ov.LEGACY_MAP_H - 40) // 2)
    cx_min, cx_max = margin_x, ov.LEGACY_MAP_W - margin_x
    cy_min, cy_max = margin_y, ov.LEGACY_MAP_H - margin_y

    # 1. Комнаты: 8–16 штук, без пересечений (pad=1).
    target = random.randint(8, 14)
    for _ in range(500):
        if len(rooms) >= target:
            break
        w, h = random.randint(5, 10), random.randint(4, 8)
        x = random.randint(cx_min, cx_max - w)
        y = random.randint(cy_min, cy_max - h)
        new_room = Room(x, y, w, h)
        if any(new_room.intersects(r, pad=1) for r in rooms):
            continue
        # Выкопать пол комнаты
        for yy in range(y, y + h):
            for xx in range(x, x + w):
                d.tiles[yy][xx] = Tile.FLOOR
        # Соединить с ближайшей комнатой узким L-коридором (1 клетка)
        if rooms:
            _connect_l_corridor(d, new_room, rooms[-1])
        rooms.append(new_room)

    # 2. Fallback после 4 неудач: разные комнаты
    if len(rooms) < 2:
        if _attempt >= 4:
            cx, cy = ov.LEGACY_MAP_W // 2, ov.LEGACY_MAP_H // 2
            # Первая комната
            for yy in range(cy - 3, cy + 4):
                for xx in range(cx - 4, cx + 5):
                    d.tiles[yy][xx] = Tile.FLOOR
            r1 = Room(cx - 4, cy - 3, 9, 7)
            # Вторая комната — правее и ниже, другая форма
            cx2, cy2 = cx + 8, cy + 6
            for yy in range(cy2 - 2, cy2 + 3):
                for xx in range(cx2 - 3, cx2 + 4):
                    if 0 <= yy < MAP_H and 0 <= xx < MAP_W:
                        d.tiles[yy][xx] = Tile.FLOOR
            r2 = Room(cx2 - 3, cy2 - 2, 7, 5)
            _connect_l_corridor(d, r2, r1)
            d.rooms = [r1, r2]
            sx, sy = r1.center()
            d.tiles[sy][sx] = Tile.STAIRS
            d.stairs_pos = (sx, sy)
            place_entry_tile(d, depth)
            return d
        return generate_dungeon(depth, _attempt + 1)

    # 3. Проверка связности: BFS из центра первой комнаты.
    if not _all_rooms_connected(d, rooms):
        if _attempt < 6:
            return generate_dungeon(depth, _attempt + 1)
        # Жёсткий fallback: соединить каждую комнату со следующей напрямую
        for a, b in zip(rooms, rooms[1:]):
            _connect_l_corridor(d, b, a)

    d.rooms = rooms
    # 4. Арена босса = самая большая комната (по площади), не первая.
    if len(rooms) > 1:
        arena = max(rooms[1:], key=lambda r: r.w * r.h)
        rooms.remove(arena)
        rooms.append(arena)  # теперь arena = rooms[-1]
    else:
        arena = rooms[0]

    boss_here = depth in BOSSES
    boss_data = BOSSES.get(depth)

    # ── Босс-комплекс только на этажах с боссом ──
    treasury = None
    door_cell = None
    stairs_pos = None
    if boss_here:
        placed = _carve_treasury(d, rooms, arena)
        if placed is None and _attempt < 12:
            return generate_dungeon(depth, _attempt + 1)
        if placed is not None:
            treasury, door_cell = placed

    ax, ay = arena.center()
    if treasury is not None:
        tx, ty = treasury.center()
        d.tiles[ty][tx] = Tile.STAIRS
        stairs_pos = (tx, ty)
        d.treasury_pos = (tx, ty)
        d.boss_door_cells = {door_cell}
    else:
        tx, ty = ax, ay
        stairs_pos = (tx, ty)
        d.tiles[ty][tx] = Tile.STAIRS
    d.stairs_pos = stairs_pos

    place_entry_tile(d, depth)

    # Монстры (без арены босса)
    for room in rooms[1:]:
        if boss_here and room is rooms[-1]:
            continue
        n = max(1, (room.w * room.h) // 22)
        for _ in range(n):
            pool = [k for k, v in MONSTERS.items()
                    if v['min_depth'] <= depth and not v.get('overworld_only')]
            if not pool:
                break
            m = MONSTERS[random.choice(pool)]
            hp, atk, df = scale_stats(m['hp'], m['atk'], m['df'], depth)
            mx = random.randint(room.x, room.x + room.w - 1)
            my = random.randint(room.y, room.y + room.h - 1)
            if (mx, my) == (ax, ay) or (mx, my) == (tx, ty):
                continue
            if any(o.x == mx and o.y == my for o in d.monsters):
                continue
            e = Entity(mx, my, hp, hp, atk, df,
                       m['name'], m['ch'], m['color'], m['xp'],
                       ability=m.get('ability'))
            d.monsters.append(e)

    # Босс — в центре арены
    if boss_here:
        hp, atk, df = scale_boss_stats(boss_data['hp'], boss_data['atk'],
                                       boss_data['df'], depth)
        d.monsters.append(Entity(ax, ay, hp, hp, atk, df,
                                 boss_data['name'], boss_data['ch'],
                                 boss_data['color'], boss_data['xp'], boss=True))

    # Лут (без арены босса)
    for room in rooms[1:]:
        if boss_here and room is rooms[-1]:
            continue
        if random.random() < 0.6:
            tier = min(3, max(1, (depth + 1) // 3))
            pool = [k for k, v in ITEMS.items()
                    if v['tier'] <= tier and v['type'] != 'book'
                    and v['type'] != 'resource']
            if not pool:
                continue
            ix = random.randint(room.x, room.x + room.w - 1)
            iy = random.randint(room.y, room.y + room.h - 1)
            if (ix, iy) in ((ax, ay), (tx, ty)):
                continue
            d.items[(ix, iy)] = random.choice(pool)

    # Казна: сундук рядом с лестницей вниз — ТОЛЬКО на боссовом этаже.
    # Никаких других сундуков на обычных этажах (см. тест test_no_chest_on_non_boss_floor).
    if treasury is not None:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            cx, cy = tx + dx, ty + dy
            if not (treasury.x <= cx < treasury.x + treasury.w
                    and treasury.y <= cy < treasury.y + treasury.h):
                continue
            if (d.tiles[cy][cx] == Tile.FLOOR
                    and (cx, cy) not in d.items):
                d.tiles[cy][cx] = Tile.CHEST
                break

    return d


def _connect_l_corridor(d, room_a, room_b):
    """Узкий (1-клеточный) L-коридор между двумя комнатами.

    Сначала горизонталь, потом вертикаль — или наоборот (случайно).
    Не прорубает сквозь стены других комнат: если клетка уже пол — OK.
    """
    ax, ay = room_a.center()
    bx, by = room_b.center()
    if random.random() < 0.5:
        # Горизонталь → вертикаль
        for xx in range(min(ax, bx), max(ax, bx) + 1):
            if 0 <= ay < d.h and 0 <= xx < d.w:
                d.tiles[ay][xx] = Tile.FLOOR
        for yy in range(min(ay, by), max(ay, by) + 1):
            if 0 <= yy < d.h and 0 <= bx < d.w:
                d.tiles[yy][bx] = Tile.FLOOR
    else:
        # Вертикаль → горизонталь
        for yy in range(min(ay, by), max(ay, by) + 1):
            if 0 <= yy < d.h and 0 <= bx < d.w:
                d.tiles[yy][bx] = Tile.FLOOR
        for xx in range(min(ax, bx), max(ax, bx) + 1):
            if 0 <= ay < d.h and 0 <= xx < d.w:
                d.tiles[ay][xx] = Tile.FLOOR


def _all_rooms_connected(d, rooms):
    """BFS-проверка: все центры комнат достижимы из центра первой комнаты."""
    if not rooms:
        return True
    start = rooms[0].center()
    if d.tiles[start[1]][start[0]] == Tile.WALL:
        return False
    seen = {start}
    q = deque([start])
    while q:
        cx, cy = q.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = cx + dx, cy + dy
            if not (0 <= nx < d.w and 0 <= ny < d.h):
                continue
            if (nx, ny) in seen:
                continue
            if d.tiles[ny][nx] in (Tile.WALL, Tile.LOCKED_DOOR):
                continue
            seen.add((nx, ny))
            q.append((nx, ny))
    for r in rooms[1:]:
        if r.center() not in seen:
            return False
    return True


# ════════════════════ LOS / FOV / BFS ════════════════════
def compute_fov(d: Dungeon, px: int, py: int,
                 radius: int = FOV_RADIUS) -> set:
    """Per-cell Bresenham FOV с поддержкой отключения тумана войны.

    Если ``config.FOG_OF_WAR_ENABLED = False`` — возвращает все проходимые
    тайлы карты (полная видимость). Иначе — классический радиусный FOV.
    """
    from core.config import FOG_OF_WAR_ENABLED
    if not FOG_OF_WAR_ENABLED:
        # Вся карта видна. Игрок видит стены как «исследованные».
        vis = set()
        for y in range(d.h):
            for x in range(d.w):
                t = d.tiles[y][x]
                if t != Tile.WALL:
                    vis.add((x, y))
        # Стены, граничащие с полом, тоже видимы.
        for y in range(d.h):
            for x in range(d.w):
                if d.tiles[y][x] != Tile.WALL:
                    continue
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < d.w and 0 <= ny < d.h and (nx, ny) in vis:
                        vis.add((x, y))
                        break
        return vis
    # Классический FOV
    vis = set()
    r2 = radius * radius
    for y in range(max(0, py - radius), min(d.h, py + radius + 1)):
        for x in range(max(0, px - radius), min(d.w, px + radius + 1)):
            if (x - px) ** 2 + (y - py) ** 2 > r2:
                continue
            if has_los(d, px, py, x, y):
                vis.add((x, y))
    return vis


def has_los(d: Dungeon, x0: int, y0: int, x1: int, y1: int) -> bool:
    """Bresenham — оставляем для прямой видимости (ИИ мобов)."""
    dx, dy = abs(x1 - x0), abs(y1 - y0)
    x, y = x0, y0
    sx = 1 if x0 < x1 else -1
    sy = 1 if y0 < y1 else -1
    err = dx - dy
    while True:
        if ((x, y) != (x0, y0) and (x, y) != (x1, y1)
                and d.tiles[y][x] == Tile.WALL):
            return False
        if x == x1 and y == y1:
            return True
        e2 = 2 * err
        prev_x, prev_y = x, y
        if e2 > -dy:
            err -= dy; x += sx
        if e2 < dx:
            err += dx; y += sy
        if x != prev_x and y != prev_y:
            if (d.tiles[prev_y][x] == Tile.WALL
                    and d.tiles[y][prev_x] == Tile.WALL):
                return False

def bfs_toward_player(d, start, player_pos, blocked):
    px, py = player_pos
    targets = set()
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        tx, ty = px + dx, py + dy
        if 0 <= tx < MAP_W and 0 <= ty < MAP_H and d.tiles[ty][tx] != Tile.WALL:
            targets.add((tx, ty))

    q = deque([start])
    came = {start: None}
    goal = None
    while q:
        cx, cy = q.popleft()
        if (cx, cy) in targets:
            goal = (cx, cy)
            break
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = cx + dx, cy + dy
            if not (0 <= nx < MAP_W and 0 <= ny < MAP_H):
                continue
            if (nx, ny) in came:
                continue
            if d.tiles[ny][nx] in (Tile.WALL, Tile.LOCKED_DOOR):
                continue
            if (nx, ny) in blocked:
                continue
            came[(nx, ny)] = (cx, cy)
            q.append((nx, ny))

    if goal is None or goal == start:
        return None
    cur = goal
    while came[cur] != start:
        cur = came[cur]
    return cur
