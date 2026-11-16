"""Раскладка поверхности: деревня в центре, восемь лесных краёв вокруг
и Деревня гоблинов на востоке.

Исторический мир 100×70 делится на сетку 3×3. Центральная клетка —
деревня Этельгард, восемь внешних — разные лесные/каменные биомы. В
северо-восточном крае находится вход в подземелье. Карта расширена вправо
до 140 клеток, но границы старых биомов зафиксированы, чтобы их
координаты не «уезжали» при изменении MAP_W.

Модуль не генерирует карту, а только описывает раскладку. Им пользуются
и генератор мира, и рендерер — поэтому деревни, дороги и биомы выглядят
одинаково в одиночной и мультиплеерной игре.
"""
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

# Дома деревни — сплошные стены-крыши. Двери не нужны: это декорации,
# а не отдельные интерьеры.
VILLAGE_HOUSES = (
    (36, 25, 5, 4), (43, 25, 6, 4), (53, 25, 6, 4), (61, 25, 4, 4),
    (36, 40, 5, 4), (44, 40, 6, 4), (54, 40, 6, 4), (61, 40, 4, 4),
)

# Восемь внешних регионов, по часовой стрелке от северо-западного.
BIOME_NAMES = {
    (0, 0): 'pine', (1, 0): 'birch', (2, 0): 'rock',
    (0, 1): 'autumn',               (2, 1): 'swamp',
    (0, 2): 'dark', (1, 2): 'meadow', (2, 2): 'dead',
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
}

VILLAGE_TITLE = 'Деревня Этельгард'

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

ROAD_SEGMENTS = (
    (REGION_X[1], 35, 0, 35),                       # запад
    (REGION_X[2] - 1, 35, LEGACY_MAP_W, 35),        # восток (старая карта)
    (50, REGION_Y[1], 50, 0),                       # север
    (50, REGION_Y[2] - 1, 50, MAP_H - 1),           # юг
    # Северо-запад и юго-запад
    (REGION_X[1], 30, 16, 30), (16, 30, 16, 0),
    (REGION_X[1], 39, 16, 39), (16, 39, 16, MAP_H - 1),
    # Северо-восток (к подземелью) и юго-восток
    (REGION_X[2] - 1, 30, 83, 30),
    (83, 30, 83, DUNGEON_POS[1]),
    (REGION_X[2] - 1, 39, 83, 39), (83, 39, 83, MAP_H - 1),
    # Продолжение восточной дороги в Деревню гоблинов.
    (LEGACY_MAP_W, 35, GOBLIN_VILLAGE_CENTER[0], 35),
)

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


def is_stall(x, y):     return (x, y) in VILLAGE_STALLS
def is_statue(x, y):    return (x, y) in VILLAGE_STATUES
def is_torch(x, y):     return (x, y) in VILLAGE_TORCHES
def is_ruins_pillar(x, y): return (x, y) in RUINS_PILLARS
def is_goblin_totem(x, y): return (x, y) in GOBLIN_TOTEMS
def is_goblin_campfire(x, y): return (x, y) == GOBLIN_CAMPFIRE


def region_of(x, y):
    """Регион сетки 3×3, в котором находится клетка.

    Новые земли восточнее исторической карты возвращают фиктивный
    регион (3, 1): у них нет биома из BIOME_NAMES.
    """
    if x >= LEGACY_MAP_W:
        return (3, 1)
    rx = 0 if x < REGION_X[1] else (1 if x < REGION_X[2] else 2)
    ry = 0 if y < REGION_Y[1] else (1 if y < REGION_Y[2] else 2)
    return rx, ry


def in_village(x, y):
    vx, vy, vw, vh = VILLAGE_RECT
    return vx <= x < vx + vw and vy <= y < vy + vh


def in_goblin_village(x, y):
    vx, vy, vw, vh = GOBLIN_VILLAGE_RECT
    return vx <= x < vx + vw and vy <= y < vy + vh


def biome_at(x, y):
    """Имя биома или None для деревень и новых земель."""
    if x >= LEGACY_MAP_W:
        return None
    return BIOME_NAMES.get(region_of(x, y))


def is_road(x, y):
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


def in_dungeon_clearing(x, y):
    dx, dy = DUNGEON_POS
    return (x - dx) ** 2 + (y - dy) ** 2 <= DUNGEON_CLEARING_RADIUS ** 2


def in_dungeon_ruins(x, y):
    """Вся расчищенная археозона вокруг входа (шире, чем зона без леса)."""
    dx, dy = DUNGEON_POS
    return (x - dx) ** 2 + (y - dy) ** 2 <= DUNGEON_RUINS_RADIUS ** 2


def in_house(x, y):
    for hx, hy, hw, hh in VILLAGE_HOUSES:
        if hx <= x < hx + hw and hy <= y < hy + hh:
            return True
    return False


def is_well(x, y):
    return (x, y) == WELL_POS


def village_ground_kind(x, y):
    """Вид земли в деревне: площадь, дорога или трава."""
    cx, cy = VILLAGE_CENTER
    if (x - cx) ** 2 + (y - (cy - 1)) ** 2 <= 16:
        return 'village_plaza'
    if y in (34, 35, 36) or x in (49, 50, 51):
        return 'village_path'
    return 'village_grass'


def goblin_ground_kind(x, y):
    """Вид земли в Деревне гоблинов."""
    cx, cy = GOBLIN_VILLAGE_CENTER
    if (x - cx) ** 2 + (y - (cy - 1)) ** 2 <= 16:
        return 'goblin_plaza'
    if y in (34, 35, 36) or x in (cx - 1, cx, cx + 1):
        return 'goblin_path'
    return 'goblin_grass'


def ground_kind(x, y):
    if in_goblin_village(x, y):
        return goblin_ground_kind(x, y)
    if in_village(x, y):
        return village_ground_kind(x, y)
    if in_dungeon_ruins(x, y):
        return 'dungeon_ruins'          # «рунический пол»
    if is_road(x, y):
        return 'road'
    if x >= LEGACY_MAP_W:
        return 'grass_dark'             # сумрачные земли вокруг гоблинов
    biome = biome_at(x, y)
    return f'grass_{biome}' if biome else 'grass_pine'


def wall_kind(x, y):
    if in_goblin_village(x, y):
        if is_goblin_campfire(x, y): return 'goblin_campfire'
        if is_goblin_totem(x, y):    return 'goblin_totem'
        return 'goblin_hut'
    if in_village(x, y):
        if is_statue(x, y): return 'village_statue'
        if is_stall(x, y):  return 'village_stall'
        if is_torch(x, y):  return 'village_torch_post'
        return 'village_wall'
    if x >= LEGACY_MAP_W:
        return 'tree_dark'
    if is_ruins_pillar(x, y):
        return 'ruins_pillar'
    biome = biome_at(x, y) or 'pine'
    return 'rock' if biome == 'rock' else f'tree_{biome}'


def location_title(x, y):
    """Название места для HUD."""
    if in_goblin_village(x, y):
        return GOBLIN_VILLAGE_TITLE
    if in_village(x, y):
        return VILLAGE_TITLE
    if in_dungeon_ruins(x, y):
        return BIOME_TITLES['rock']
    if x >= LEGACY_MAP_W:
        return GOBLIN_WILDS_TITLE
    biome = biome_at(x, y)
    return BIOME_TITLES.get(biome, 'Открытый мир')
