"""Генерация миров, LOS, FOV, BFS."""
import random
from collections import deque

from core.config import MAP_W, MAP_H, FOV_RADIUS, Tile
from core.data import MONSTERS, BOSSES, ITEMS, scale_stats, scale_boss_stats
from core.entities import Entity, Room, Dungeon
from core import overworld as ov


OVERWORLD_RESOURCES = ('mushroom', 'berry', 'wood', 'stone', 'ore')

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
}


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
            if d.tiles[ny][nx] == Tile.WALL:
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


def generate_overworld():
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
        if (p[0] - spawn[0]) ** 2 + (p[1] - spawn[1]) ** 2 < 100:
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
    return d


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


def generate_dungeon(depth, _attempt=0):
    """Комнаты в центральной зоне 60×40, подключение к ближайшей."""
    d = Dungeon(MAP_W, MAP_H)
    rooms = []

    margin_x = max(1, (ov.LEGACY_MAP_W - 60) // 2)
    margin_y = max(1, (ov.LEGACY_MAP_H - 40) // 2)
    # Подземелья остаются в исторической части карты 100×70: расширение
    # мира предназначено для поверхности, а не для этажей.
    cx_min, cx_max = margin_x, ov.LEGACY_MAP_W - margin_x
    cy_min, cy_max = margin_y, ov.LEGACY_MAP_H - margin_y

    for _ in range(400):
        if len(rooms) >= 14:
            break
        w, h = random.randint(5, 10), random.randint(4, 8)
        x = random.randint(cx_min, cx_max - w)
        y = random.randint(cy_min, cy_max - h)
        new_room = Room(x, y, w, h)
        if any(new_room.intersects(r) for r in rooms):
            continue

        for yy in range(y, y + h):
            for xx in range(x, x + w):
                d.tiles[yy][xx] = Tile.FLOOR

        if rooms:
            bx, by = new_room.center()
            nearest = min(rooms, key=lambda r:
            abs(r.center()[0] - bx) + abs(r.center()[1] - by))
            ax, ay = nearest.center()
            # Горизонтальный участок — 2 клетки в ширину
            for xx in range(min(ax, bx), max(ax, bx) + 1):
                d.tiles[ay][xx] = Tile.FLOOR
                if ay + 1 < MAP_H:
                    d.tiles[ay + 1][xx] = Tile.FLOOR
            # Вертикальный участок — 2 клетки в ширину
            for yy in range(min(ay, by), max(ay, by) + 1):
                d.tiles[yy][bx] = Tile.FLOOR
                if bx + 1 < MAP_W:
                    d.tiles[yy][bx + 1] = Tile.FLOOR
        rooms.append(new_room)

    # Fallback после 3 неудач
    if len(rooms) < 2:
        if _attempt >= 3:
            cx, cy = MAP_W // 2, MAP_H // 2
            for yy in range(cy - 3, cy + 4):
                for xx in range(cx - 4, cx + 5):
                    d.tiles[yy][xx] = Tile.FLOOR
            d.rooms = [Room(cx - 4, cy - 3, 9, 7), Room(cx - 4, cy - 3, 9, 7)]
            sx, sy = cx, cy
            d.tiles[sy][sx] = Tile.STAIRS
            d.stairs_pos = (sx, sy)
            place_entry_tile(d, depth)
            return d
        return generate_dungeon(depth, _attempt + 1)

    d.rooms = rooms
    sx, sy = rooms[-1].center()
    d.tiles[sy][sx] = Tile.STAIRS
    d.stairs_pos = (sx, sy)
    # Лестница назад: на глубине 1 — выход на поверхность, глубже — вверх.
    place_entry_tile(d, depth)

    boss_here = depth in BOSSES
    boss_data = BOSSES.get(depth)

    # Монстры
    for room in rooms[1:]:
        if boss_here and room is rooms[-1]:
            continue
        n = max(1, (room.w * room.h) // 22)
        for _ in range(n):
            pool = [k for k, v in MONSTERS.items()
                    if v['min_depth'] <= depth and not v.get('overworld_only')]
            m = MONSTERS[random.choice(pool)]
            hp, atk, df = scale_stats(m['hp'], m['atk'], m['df'], depth)
            mx = random.randint(room.x, room.x + room.w - 1)
            my = random.randint(room.y, room.y + room.h - 1)
            if (mx, my) == (sx, sy):
                continue
            if any(o.x == mx and o.y == my for o in d.monsters):
                continue
            e = Entity(mx, my, hp, hp, atk, df,
                       m['name'], m['ch'], m['color'], m['xp'],
                       ability=m.get('ability'))       # ← ability
            d.monsters.append(e)

    # Босс
    if boss_here:
        hp, atk, df = scale_boss_stats(boss_data['hp'], boss_data['atk'],
                                       boss_data['df'], depth)
        d.monsters.append(Entity(sx, sy, hp, hp, atk, df,
                                 boss_data['name'], boss_data['ch'],
                                 boss_data['color'], boss_data['xp'], boss=True))

    # Лут
    for room in rooms[1:]:
        if boss_here and room is rooms[-1]:
            continue
        if random.random() < 0.6:
            tier = min(3, max(1, (depth + 1) // 3))
            pool = [k for k, v in ITEMS.items()
                    if v['tier'] <= tier and v['type'] != 'book'
                    and v['type'] != 'resource']
            ix = random.randint(room.x, room.x + room.w - 1)
            iy = random.randint(room.y, room.y + room.h - 1)
            if (ix, iy) == (sx, sy):
                continue
            d.items[(ix, iy)] = random.choice(pool)

    return d


# ════════════════════ LOS / FOV / BFS ════════════════════
def compute_fov(d, px, py, radius=FOV_RADIUS):
    """Per-cell Bresenham FOV. Каждая клетка проверяется через has_los."""
    vis = set()
    r2 = radius * radius
    for y in range(max(0, py - radius), min(d.h, py + radius + 1)):
        for x in range(max(0, px - radius), min(d.w, px + radius + 1)):
            if (x - px) ** 2 + (y - py) ** 2 > r2:
                continue
            if has_los(d, px, py, x, y):
                vis.add((x, y))
    return vis


def has_los(d, x0, y0, x1, y1):
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
            if d.tiles[ny][nx] == Tile.WALL:
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