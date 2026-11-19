"""Раскладка поверхности: город в центре, восемь лесных краёв вокруг
и Деревня гоблинов на востоке.

Исторический мир 100×70 делится на сетку 3×3. Центральная клетка —
город Этельгард, восемь внешних — разные лесные/каменные биомы. В
северо-восточном крае находится вход в подземелье. Карта расширена вправо
до 140 клеток, но границы старых биомов зафиксированы, чтобы их
координаты не «уезжали» при изменении MAP_W.

Модуль не генерирует карту, а только описывает раскладку. Им пользуются
и генератор мира, и рендерер — поэтому город, дороги и биомы выглядят
одинаково в одиночной и мультиплеерной игре.
"""
from __future__ import annotations

from typing import Any

from core.config import MAP_H

# Исторический размер мира. Слева от этой границы — прежние 8 биомов.
LEGACY_MAP_W = 100
LEGACY_MAP_H = 70

# Границы регионов 3×3: 100×70 → 33/34 по X и 23/24 по Y.
# Это константы, а не MAP_W // 3: иначе расширение карты сдвинуло бы
# все старые биомы.
REGION_X = (0, LEGACY_MAP_W // 3, (LEGACY_MAP_W * 2) // 3, LEGACY_MAP_W)
REGION_Y = (0, LEGACY_MAP_H // 3, (LEGACY_MAP_H * 2) // 3, LEGACY_MAP_H)

VILLAGE_CENTER = (LEGACY_MAP_W // 2, LEGACY_MAP_H // 2)     # (50, 35)
VILLAGE_RECT = (REGION_X[1] + 1, REGION_Y[1] + 1,
                REGION_X[2] - REGION_X[1] - 2,
                REGION_Y[2] - REGION_Y[1] - 2)              # (34, 24, 32, 21)
WELL_POS = (VILLAGE_CENTER[0], VILLAGE_CENTER[1] - 3)
ELDER_POS = (VILLAGE_CENTER[0] + 2, VILLAGE_CENTER[1])

# Дома города — сплошные стены-крыши. Двери не нужны: это декорации,
# а не отдельные интерьеры.
VILLAGE_HOUSES = (
    (36, 25, 5, 4), (43, 25, 6, 4), (53, 25, 6, 4), (61, 25, 4, 4),
    (36, 40, 5, 4), (44, 40, 6, 4), (54, 40, 6, 4), (61, 40, 4, 4),
)

# ── Городская стена ──
# Каменное кольцо идёт по периметру VILLAGE_RECT. Ворота оставлены
# строго на четырёх дорогах, иначе караванные пути упирались бы в кладку.
CITY_GATE_WIDTH = 3


def _city_wall_cells() -> Any: 
    vx, vy, vw, vh = VILLAGE_RECT
    cx, cy = VILLAGE_CENTER
    half = CITY_GATE_WIDTH // 2
    cells = set()
    for x in range(vx, vx + vw):
        if abs(x - cx) > half:
            cells.add((x, vy))
            cells.add((x, vy + vh - 1))
    for y in range(vy, vy + vh):
        if abs(y - cy) > half:
            cells.add((vx, y))
            cells.add((vx + vw - 1, y))
    return frozenset(cells)


CITY_WALL_CELLS = _city_wall_cells()

# ── Горожане ──
# Стражники стоят прямо в проёмах каждых из четырёх городских ворот:
# север (50,24), восток (64,35), запад (34,35), юг (50,44).
# ``GUARD_POS`` оставлен как «главный» южный пост: на него опираются
# старые сохранения и тесты.
GUARD_POS = (50, 44)
BLACKSMITH_POS = (40, 32)
ALCHEMIST_POS = (60, 32)
INNKEEPER_POS = (48, 31)

# Посты стражников — в самих воротах, а не за ними. Южный пост идёт
# последним — именно он считается «главным» стражником.
GUARD_POSTS = (
    (VILLAGE_CENTER[0], VILLAGE_RECT[1]),                        # север
    (VILLAGE_RECT[0] + VILLAGE_RECT[2] - 1, VILLAGE_CENTER[1]),  # восток
    (VILLAGE_RECT[0], VILLAGE_CENTER[1]),                        # запад
    GUARD_POS,                                                   # юг
)

CITY_NPC_POS = {
    'guard': GUARD_POS,
    'blacksmith': BLACKSMITH_POS,
    'alchemist': ALCHEMIST_POS,
    'innkeeper': INNKEEPER_POS,
}

# Восемь внешних регионов, по часовой стрелке от северо-западного.
# (0, 2) — заменили 'dark' на 'beach' (Морской пляж) на западе юга.
BIOME_NAMES = {
    (0, 0): 'pine', (1, 0): 'birch', (2, 0): 'rock',
    (0, 1): 'autumn',               (2, 1): 'swamp',
    (0, 2): 'beach', (1, 2): 'meadow', (2, 2): 'dead',
}

BIOME_TITLES = {
    'pine':   'Сосновый бор',
    'birch':  'Берёзовая роща',
    'rock':   'Каменный перевал',
    'autumn': 'Осенний лес',
    'swamp':  'Гиблое болото',
    'dark':   'Дремучий ельник',
    'meadow': 'Цветущая поляна',
    'dead':   'Мёртвый лес',
    'beach':  'Морской пляж',
}

VILLAGE_TITLE = 'Город Этельгард'

# ── Деревня гоблинов (восточное расширение карты) ──
GOBLIN_VILLAGE_CENTER = (120, 35)
GOBLIN_VILLAGE_RECT = (106, 24, 28, 21)                # (x, y, w, h)
GOBLIN_VILLAGE_TITLE = 'Деревня гоблинов'
GOBLIN_WILDS_TITLE = 'Дикие земли гоблинов'

# Гоблинские хижины, тотемы и общий костёр. Как и дома людей, это
# непроходимые декорации-крыши, а не интерьеры.
GOBLIN_HOUSES = (
    (108, 25, 5, 4), (116, 25, 6, 4), (126, 25, 5, 4),
    (108, 41, 5, 4), (116, 41, 6, 4), (126, 41, 5, 4),
)
GOBLIN_TOTEMS = ((111, 32), (129, 32))
GOBLIN_CAMPFIRE = (120, 31)

# Вход в подземелье — в «каменном» северо-восточном крае.
DUNGEON_REGION = (2, 0)
DUNGEON_POS = (83, 10)

# Дороги: (x0, y0, x1, y1). Каждая — горизонтальная или вертикальная.
ROAD_HALF_WIDTH = 1

# Только 4 кардинальных выхода из деревни (север, юг, запад, восток)
# плюс дорога к входу в подземелье. Диагональные и боковые дороги убраны.
# Каждый кардинальный выход заканчивается переходом (Tile.GATE) на краю карты.
ROAD_SEGMENTS = (
    # Запад: от деревни до левого края карты
    (REGION_X[1], VILLAGE_CENTER[1], 0, VILLAGE_CENTER[1]),
    # Восток: от деревни до правого края исторической карты
    (REGION_X[2] - 1, VILLAGE_CENTER[1], LEGACY_MAP_W - 1, VILLAGE_CENTER[1]),
    # Север: от деревни до верхнего края карты
    (VILLAGE_CENTER[0], REGION_Y[1], VILLAGE_CENTER[0], 0),
    # Юг: от деревни до нижнего края карты
    (VILLAGE_CENTER[0], REGION_Y[2] - 1, VILLAGE_CENTER[0], MAP_H - 1),
    # Дорога к входу в подземелье на северо-востоке (критический путь)
    (REGION_X[2] - 1, 30, 83, 30),
    (83, 30, 83, DUNGEON_POS[1]),
)

# Кардинальные переходы на краях карты (для Tile.GATE).
# Каждый ведёт «наружу» из мира деревни — может быть расширен в future
# до отдельных карт-локаций.
GATE_POSITIONS = {
    'north': (VILLAGE_CENTER[0], 0),
    'south': (VILLAGE_CENTER[0], MAP_H - 1),
    'west':  (0, VILLAGE_CENTER[1]),
    'east':  (LEGACY_MAP_W - 1, VILLAGE_CENTER[1]),
}

# ── Декор живой деревни ──
VILLAGE_STALLS  = ((40, 30), (58, 30))      # рыночные прилавки (Tile.WALL)
VILLAGE_STATUES = ((46, 30),)               # статуи героев
VILLAGE_TORCHES = ((42, 33), (57, 33))      # факельные столбы

# ── Руины вокруг входа в подземелье ──
DUNGEON_RUINS_RADIUS = 10                   # расширенная археозона
DUNGEON_CLEARING_RADIUS = 6                 # зона, где лес не сеется
RUINS_PILLARS = (                           # сломанные колонны по периметру
    (75, 6), (91, 6), (75, 14), (91, 14),
    (73, 10), (93, 10),
)

# ── Доска поручений в деревне ──
QUEST_BOARD_VILLAGE_POS = (48, 37)

# ── Руины древнего города в биоме 'dead' (юго-восток) ──
# С 25% шансом при генерации мира здесь появляется сундук с базовыми ресурсами.
# Здания — разрушенные стены, статуи — целые декорации.
CITY_RUINS_REGION = (2, 2)
CITY_RUINS_RECT = (75, 50, 20, 15)               # в координатах карты
CITY_RUINS_BUILDINGS = (
    (77, 52, 4, 3), (82, 52, 5, 3), (88, 52, 4, 3),
    (77, 57, 5, 3), (83, 57, 6, 3),
    (78, 62, 4, 2), (84, 62, 5, 2),
)
CITY_RUINS_STATUES = ((80, 56), (87, 56), (80, 60))
CITY_RUINS_ROADS = (
    # Главная дорога через руины — горизонталь
    (76, 56, 94, 56),
    # Вертикальная дорога
    (82, 51, 82, 64),
)
CITY_RUINS_CHEST_CHANCE = 0.25
CITY_RUINS_BASE_RESOURCES = ('wood', 'stone', 'ore', 'hide', 'berry',
                             'mushroom')


def in_city_ruins(x: Any, y: Any) -> Any: 
    rx, ry, rw, rh = CITY_RUINS_RECT
    return rx <= x < rx + rw and ry <= y < ry + rh


def is_city_ruins_statue(x: Any, y: Any) -> Any: 
    return (x, y) in CITY_RUINS_STATUES


def is_stall(x: Any, y: Any) -> Any: return (x, y) in VILLAGE_STALLS
def is_statue(x: Any, y: Any) -> Any: return (x, y) in VILLAGE_STATUES
def is_torch(x: Any, y: Any) -> Any: return (x, y) in VILLAGE_TORCHES
def is_ruins_pillar(x: Any, y: Any) -> Any: return (x, y) in RUINS_PILLARS
def is_goblin_totem(x: Any, y: Any) -> Any: return (x, y) in GOBLIN_TOTEMS
def is_goblin_campfire(x: Any, y: Any) -> Any: return (x, y) == GOBLIN_CAMPFIRE
def is_city_wall(x: Any, y: Any) -> Any: return (x, y) in CITY_WALL_CELLS


def region_of(x: Any, y: Any) -> Any: 
    """Регион сетки 3×3, в котором находится клетка.

    Новые земли восточнее исторической карты возвращают фиктивный
    регион (3, 1): у них нет биома из BIOME_NAMES.
    """
    if x >= LEGACY_MAP_W:
        return (3, 1)
    rx = 0 if x < REGION_X[1] else (1 if x < REGION_X[2] else 2)
    ry = 0 if y < REGION_Y[1] else (1 if y < REGION_Y[2] else 2)
    return rx, ry


def in_village(x: Any, y: Any) -> Any: 
    vx, vy, vw, vh = VILLAGE_RECT
    return vx <= x < vx + vw and vy <= y < vy + vh


def in_goblin_village(x: Any, y: Any) -> Any: 
    vx, vy, vw, vh = GOBLIN_VILLAGE_RECT
    return vx <= x < vx + vw and vy <= y < vy + vh


def in_city_safe(x: Any, y: Any, margin: Any = 0) -> Any: 
    """Клетка внутри городских стен или в ``margin`` клетках от них.

    Мобы не должны появляться рядом с городом: там живут мирные горожане
    и стражник, который сразу бьёт всё, что подходит к воротам.
    """
    vx, vy, vw, vh = VILLAGE_RECT
    return (vx - margin <= x <= vx + vw - 1 + margin
            and vy - margin <= y <= vy + vh - 1 + margin)


def biome_at(x: Any, y: Any) -> Any: 
    """Имя биома или None для деревень и новых земель."""
    if x >= LEGACY_MAP_W:
        return None
    return BIOME_NAMES.get(region_of(x, y))


def is_road(x: Any, y: Any) -> Any: 
    """Клетка принадлежит дороге между деревнями и краями карты."""
    for x0, y0, x1, y1 in ROAD_SEGMENTS:
        if y0 == y1:
            if min(x0, x1) <= x <= max(x0, x1) \
                    and abs(y - y0) <= ROAD_HALF_WIDTH:
                return True
        elif x0 == x1:
            if min(y0, y1) <= y <= max(y0, y1) \
                    and abs(x - x0) <= ROAD_HALF_WIDTH:
                return True
    return False


def in_dungeon_clearing(x: Any, y: Any) -> Any: 
    dx, dy = DUNGEON_POS
    return (x - dx) ** 2 + (y - dy) ** 2 <= DUNGEON_CLEARING_RADIUS ** 2


def in_dungeon_ruins(x: Any, y: Any) -> Any: 
    """Вся расчищенная археозона вокруг входа (шире, чем зона без леса)."""
    dx, dy = DUNGEON_POS
    return (x - dx) ** 2 + (y - dy) ** 2 <= DUNGEON_RUINS_RADIUS ** 2


def in_house(x: Any, y: Any) -> Any: 
    for hx, hy, hw, hh in VILLAGE_HOUSES:
        if hx <= x < hx + hw and hy <= y < hy + hh:
            return True
    return False


def is_well(x: Any, y: Any) -> Any: 
    return (x, y) == WELL_POS


def city_ground_kind(x: Any, y: Any) -> Any: 
    """Вид земли в городе: площадь, мостовая или трава."""
    cx, cy = VILLAGE_CENTER
    if (x - cx) ** 2 + (y - (cy - 1)) ** 2 <= 16:
        return 'city_plaza'
    if y in (34, 35, 36) or x in (49, 50, 51):
        return 'city_path'
    return 'city_grass'


# Историческое имя оставлено для старых сохранений и внешнего кода.
def village_ground_kind(x: Any, y: Any) -> Any: 
    return city_ground_kind(x, y)


def city_wall_kind(x: Any, y: Any) -> Any: 
    """Вид стены в городе."""
    if is_well(x, y):     return 'city_well'
    if is_statue(x, y):   return 'city_statue'
    if is_stall(x, y):    return 'city_stall'
    if is_torch(x, y):    return 'city_torch'
    if is_city_wall(x, y): return 'city_wall'
    if in_house(x, y):    return 'city_house'
    return 'city_wall'


def goblin_ground_kind(x: Any, y: Any) -> Any: 
    """Вид земли в Деревне гоблинов."""
    cx, cy = GOBLIN_VILLAGE_CENTER
    if (x - cx) ** 2 + (y - (cy - 1)) ** 2 <= 16:
        return 'goblin_plaza'
    if y in (34, 35, 36) or x in (cx - 1, cx, cx + 1):
        return 'goblin_path'
    return 'goblin_grass'


def ground_kind(x: Any, y: Any) -> Any: 
    if in_goblin_village(x, y):
        return goblin_ground_kind(x, y)
    if in_village(x, y):
        return city_ground_kind(x, y)
    if in_dungeon_ruins(x, y):
        return 'dungeon_ruins'          # «рунический пол»
    if is_road(x, y):
        return 'road'
    if in_city_ruins(x, y):
        return 'city_ruins_floor'       # каменные плиты разрушенного города
    if x >= LEGACY_MAP_W:
        return 'grass_dark'             # сумрачные земли вокруг гоблинов
    biome = biome_at(x, y)
    if biome == 'beach':
        # Пляж: песок, у западного края — вода (Tile.WALL с kind='water').
        return 'beach_sand'
    return f'grass_{biome}' if biome else 'grass_pine'


def wall_kind(x: Any, y: Any) -> Any: 
    if in_goblin_village(x, y):
        if is_goblin_campfire(x, y): return 'goblin_campfire'
        if is_goblin_totem(x, y):    return 'goblin_totem'
        return 'goblin_hut'
    if in_village(x, y):
        return city_wall_kind(x, y)
    if in_city_ruins(x, y):
        if is_city_ruins_statue(x, y): return 'city_ruins_statue'
        return 'city_ruins_wall'
    if x >= LEGACY_MAP_W:
        return 'tree_dark'
    if is_ruins_pillar(x, y):
        return 'ruins_pillar'
    biome = biome_at(x, y) or 'pine'
    if biome == 'beach':
        # Полоса воды на западном краю пляжа
        if x < 4:
            return 'water'
        return 'beach_tree'   # прибрежные кусты
    return 'rock' if biome == 'rock' else f'tree_{biome}'


def location_title(x: Any, y: Any) -> Any: 
    """Название места для HUD."""
    if in_goblin_village(x, y):
        return GOBLIN_VILLAGE_TITLE
    if in_village(x, y):
        return VILLAGE_TITLE
    if in_dungeon_ruins(x, y):
        return BIOME_TITLES['rock']
    if in_city_ruins(x, y):
        return 'Руины древнего города'
    if x >= LEGACY_MAP_W:
        return GOBLIN_WILDS_TITLE
    biome = biome_at(x, y)
    return BIOME_TITLES.get(biome, 'Открытый мир')
