"""Генерация миров, LOS, FOV, BFS."""
import random
from collections import deque

from core.config import MAP_W, MAP_H, FOV_RADIUS, Tile, OVERWORLD_MONSTERS
from core.data import MONSTERS, BOSSES, ITEMS, scale_stats, scale_boss_stats
from core.entities import Entity, Room, Dungeon


OVERWORLD_RESOURCES = ('mushroom', 'berry', 'wood', 'stone', 'ore')


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


def generate_overworld():
    """Открытый мир: поляна с рощами, ресурсы, мобы, вход в пещеру."""
    d = Dungeon(MAP_W, MAP_H)
    for y in range(MAP_H):
        for x in range(MAP_W):
            d.tiles[y][x] = Tile.FLOOR

    # Рощи (35 кластеров)
    for _ in range(35):
        cx = random.randint(5, MAP_W - 6)
        cy = random.randint(5, MAP_H - 6)
        r = random.randint(1, 3)
        for yy in range(cy - r, cy + r + 1):
            for xx in range(cx - r, cx + r + 1):
                if 0 <= yy < MAP_H and 0 <= xx < MAP_W:
                    if random.random() < 0.55:
                        d.tiles[yy][xx] = Tile.WALL

    # Центральная поляна 7×7
    px, py = MAP_W // 2, MAP_H // 2
    for yy in range(py - 3, py + 4):
        for xx in range(px - 3, px + 4):
            if 0 <= yy < MAP_H and 0 <= xx < MAP_W:
                d.tiles[yy][xx] = Tile.FLOOR

    # Вход в пещеру
    entrance = None
    for _ in range(200):
        ex = px + random.randint(-12, 12)
        ey = py + random.randint(-12, 12)
        if (0 <= ex < MAP_W and 0 <= ey < MAP_H
                and d.tiles[ey][ex] == Tile.FLOOR):
            d.tiles[ey][ex] = Tile.STAIRS
            d.stairs_pos = (ex, ey)
            entrance = (ex, ey)
            break
    if entrance is None:
        ex, ey = min(px + 5, MAP_W - 2), py
        d.tiles[ey][ex] = Tile.STAIRS
        d.stairs_pos = (ex, ey)
        entrance = (ex, ey)

    # Гарантировать достижимость
    reachable = _flood_fill_reachable(d, (px, py))
    if entrance not in reachable:
        bx, by = entrance
        for xx in range(min(px, bx), max(px, bx) + 1):
            if d.tiles[py][xx] == Tile.WALL:
                d.tiles[py][xx] = Tile.FLOOR
        for yy in range(min(py, by), max(py, by) + 1):
            if d.tiles[yy][bx] == Tile.WALL:
                d.tiles[yy][bx] = Tile.FLOOR
        reachable = _flood_fill_reachable(d, (px, py))

    # Ресурсы (60)
    placed = 0
    for _ in range(2000):
        if placed >= 60:
            break
        rx = random.randint(1, MAP_W - 2)
        ry = random.randint(1, MAP_H - 2)
        if (rx, ry) in reachable and (rx, ry) != entrance:
            if (rx, ry) not in d.items:
                d.items[(rx, ry)] = random.choice(OVERWORLD_RESOURCES)
                placed += 1

    # Мобы (20)
    placed = 0
    for _ in range(2000):
        if placed >= 20:
            break
        pool = [k for k in OVERWORLD_MONSTERS if k in MONSTERS]
        m = MONSTERS[random.choice(pool)]
        mx = random.randint(2, MAP_W - 3)
        my = random.randint(2, MAP_H - 3)
        if (mx, my) not in reachable or (mx, my) == entrance:
            continue
        if any(o.x == mx and o.y == my for o in d.monsters):
            continue
        e = Entity(mx, my, m['hp'], m['hp'], m['atk'], m['df'],
                   m['name'], m['ch'], m['color'], m['xp'],
                   ability=m.get('ability'))           # ← ability
        d.monsters.append(e)
        placed += 1

    d.rooms = [Room(entrance[0], entrance[1], 1, 1)]
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

    margin_x = max(1, (MAP_W - 60) // 2)
    margin_y = max(1, (MAP_H - 40) // 2)
    cx_min, cx_max = margin_x, MAP_W - margin_x
    cy_min, cy_max = margin_y, MAP_H - margin_y

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
            pool = [k for k, v in MONSTERS.items() if v['min_depth'] <= depth]
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