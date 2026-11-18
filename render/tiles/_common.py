"""Общие константы, палитры и примитивы для тайлов.

Этот модуль — общий «словарь» для всех пакета render.tiles:
цвета камня/травы/дерева, палитры биомов и базовые примитивы
(_new, _clamp, _grime, _chip, _crack).
"""

import pygame


SIZE = 64

# ── Палитра подземелья ──
WALL_BASE = (74, 68, 59)          # тёплый серо-коричневый камень
MORTAR = (24, 22, 19)             # почти чёрный раствор
FLOOR_BASE = (60, 56, 49)
FLOOR_GROUT = (31, 29, 25)
TORCH_GLOW = (255, 170, 94)
MOSS = (74, 84, 49)               # оливковый мох
MOSS_DARK = (47, 55, 32)
RUST = (104, 70, 40)
BRONZE = (186, 142, 76)
BONE = (192, 180, 152)

# ── Палитра поверхности ──
GRASS_BASE = (55, 64, 42)         # выцветшая олива, а не «газон»
GRASS_DARK = (39, 46, 32)
GRASS_DRY = (98, 92, 58)
TREE_BASE = (46, 58, 40)
TREE_DARK = (27, 35, 24)
TRUNK = (66, 52, 38)
STONE = (86, 82, 74)
STONE_DARK = (48, 46, 41)

# Восемь лесных краёв. У каждого своя палитра травы и крон, чтобы
# переход из деревни в новый биом читался сразу.
BIOME_PALETTES = {
    'pine': {

        'grass_dry': (80, 82, 50),
        'tree': (35, 54, 36), 'tree_dark': (18, 31, 21),
        'trunk': (58, 46, 34),
    },
    'birch': {
        'grass': (66, 76, 46), 'grass_dark': (43, 53, 32),
        'grass_dry': (118, 108, 64),
        'tree': (76, 94, 52), 'tree_dark': (44, 62, 32),
        'trunk': (156, 150, 132),
    },
    'rock': {
        'grass': (70, 66, 59), 'grass_dark': (44, 42, 38),
        'grass_dry': (106, 98, 82),
        'tree': (90, 86, 78), 'tree_dark': (50, 48, 44),
        'trunk': (66, 62, 56),
    },
    'autumn': {
        'grass': (76, 66, 42), 'grass_dark': (50, 42, 28),
        'grass_dry': (138, 106, 52),
        'tree': (120, 80, 38), 'tree_dark': (76, 46, 22),
        'trunk': (72, 54, 36),
    },
    'swamp': {
        'grass': (43, 57, 47), 'grass_dark': (27, 38, 33),
        'grass_dry': (78, 82, 56),
        'tree': (46, 62, 50), 'tree_dark': (23, 37, 31),
        'trunk': (54, 48, 38),
    },
    'dark': {
        'grass': (37, 47, 35), 'grass_dark': (21, 29, 23),
        'grass_dry': (68, 66, 46),
        'tree': (29, 45, 33), 'tree_dark': (14, 25, 19),
        'trunk': (48, 40, 31),
    },
    'meadow': {
        'grass': (62, 74, 44), 'grass_dark': (38, 50, 30),
        'grass_dry': (110, 104, 58),
        'tree': (56, 74, 44), 'tree_dark': (32, 48, 30),
        'trunk': (68, 54, 38),
    },
    'dead': {
        'grass': (59, 57, 47), 'grass_dark': (37, 35, 31),
        'grass_dry': (96, 88, 70),
        'tree': (74, 66, 54), 'tree_dark': (42, 38, 32),
        'trunk': (70, 64, 54),
    },
    # Пляж: песок вместо травы, прибрежные кусты вместо деревьев.
    'beach': {
        'grass': (220, 200, 150), 'grass_dark': (180, 165, 120),
        'grass_dry': (235, 220, 175),
        'tree': (110, 130, 100), 'tree_dark': (75, 90, 70),
        'trunk': (130, 100, 70),
    },
}

VILLAGE_PALETTE = {
    'grass': (66, 78, 46), 'grass_dark': (44, 56, 32),
    'grass_dry': (116, 108, 62),
    'tree': (60, 78, 46), 'tree_dark': (34, 50, 30),
    'trunk': (70, 56, 38),
}

# Город: трава чуть холоднее и «причёсаннее», чем в лесу.
CITY_PALETTE = {
    'grass': (62, 76, 48), 'grass_dark': (42, 54, 34),
    'grass_dry': (108, 104, 64),
    'tree': (58, 76, 48), 'tree_dark': (34, 50, 32),
    'trunk': (70, 56, 38),
}


def _new(fill=None):
    s = pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)
    if fill is not None:
        s.fill(fill)
    return s


def _clamp(value):
    return 0 if value < 0 else (255 if value > 255 else int(value))


def _grime(s, rng, count=280, spread=13):
    """Мелкий разнотон поверх тайла: убирает «пластиковую» заливку."""
    for _ in range(count):
        x = rng.randrange(SIZE)
        y = rng.randrange(SIZE)
        c = s.get_at((x, y))
        if c.a < 40:
            continue
        d = rng.randint(-spread, spread)
        s.set_at((x, y), (_clamp(c.r + d), _clamp(c.g + d),
                          _clamp(c.b + d), c.a))


def _chip(s, rect, color, rng):
    """Сколотый угол блока — кладка перестаёт быть ровной сеткой."""
    size = rng.randint(3, 8)
    corner = rng.choice(('tl', 'tr', 'bl', 'br'))
    if corner == 'tl':
        pts = [(rect.x, rect.y), (rect.x + size, rect.y),
               (rect.x, rect.y + size)]
    elif corner == 'tr':
        pts = [(rect.right - 1, rect.y), (rect.right - 1 - size, rect.y),
               (rect.right - 1, rect.y + size)]
    elif corner == 'bl':
        pts = [(rect.x, rect.bottom - 1), (rect.x + size, rect.bottom - 1),
               (rect.x, rect.bottom - 1 - size)]
    else:
        pts = [(rect.right - 1, rect.bottom - 1),
               (rect.right - 1 - size, rect.bottom - 1),
               (rect.right - 1, rect.bottom - 1 - size)]
    pygame.draw.polygon(s, color, pts)


def _crack(s, x, y, steps, color, rng, drift=3):
    """Ломаная трещина: короткие сегменты со случайным уводом."""
    pts = [(x, y)]
    for _ in range(steps):
        x += rng.randint(-drift, drift)
        y += rng.randint(4, 7)
        pts.append((x, y))
    pygame.draw.lines(s, color, False, pts, 1)


