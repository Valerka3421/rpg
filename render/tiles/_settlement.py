"""Auto-generated from render/tiles.py — see git history."""

import pygame

from render import textures
from render.tiles._common import (
    SIZE, BONE,
    GRASS_BASE, VILLAGE_PALETTE, CITY_PALETTE,
    _new, _grime, _chip,
)

from render.tiles._nature import _grass
from render.tiles._dungeon import _wall_blocks


# ════════════════════════ ПОСЕЛЕНИЯ ════════════════════════
def _cobbles(variant, base_color, grout, cells=4, seed=131):
    s = _new()
    s.fill(grout)
    rng = textures.rng_for(variant, cells + 2, seed=seed)
    cell = SIZE // cells
    for gy in range(cells):
        for gx in range(cells):
            rect = pygame.Rect(gx * cell + 1, gy * cell + 1,
                               cell - 2, cell - 2)
            tone = 0.78 + textures.hash01(gx, gy, variant + 7) * 0.34
            stone = textures.shade(base_color, tone)
            textures.bevel_rect(s, rect, stone, width=1,
                                light=textures.shade(stone, 1.22),
                                dark=textures.shade(stone, 0.55))
            textures.speckles(s, rect.inflate(-3, -3), stone, rng, 12, 0.12)
            if textures.hash01(gx, gy, variant + 19) < 0.30:
                _chip(s, rect, grout, rng)
    for _ in range(6):
        x = rng.randint(2, SIZE - 6)
        y = rng.randint(2, SIZE - 4)
        pygame.draw.line(s, textures.shade(GRASS_BASE, 0.85),
                         (x, y), (x, y - rng.randint(2, 4)))
    _grime(s, rng, 170, 9)
    return s


def _village_path(variant):
    return _cobbles(variant, (104, 90, 66), (47, 41, 32), cells=4, seed=137)


def _village_plaza(variant):
    return _cobbles(variant, (120, 106, 80), (54, 47, 36), cells=3, seed=139)


def _village_grass(variant):
    s = _grass(variant, VILLAGE_PALETTE)
    rng = textures.rng_for(variant, 23, seed=149)
    # цветы на лужайках
    if variant % 3 == 0:
        for _ in range(5):
            x = rng.randint(6, SIZE - 8)
            y = rng.randint(6, SIZE - 8)
            pygame.draw.circle(s, textures.shade(BONE, 1.05), (x, y), 2)
            pygame.draw.circle(s, (168, 122, 58), (x, y), 1)
    return s


def _village_wall(variant):
    """Крыша деревенского дома — сплошная стена для проходимости."""
    s = _village_grass(variant + 1)
    rng = textures.rng_for(variant, 25, seed=151)
    roof_base = (92, 66, 43)
    roof = textures.shade(roof_base, 0.88 + textures.hash01(
        variant, 3, seed=53) * 0.22)
    pygame.draw.rect(s, (28, 23, 17), (2, 6, 60, 56))
    pygame.draw.rect(s, roof, (3, 4, 58, 56))

    # дранка: ряды с небольшим смещением
    for row, y in enumerate(range(7, 60, 8)):
        pygame.draw.line(s, textures.shade(roof, 0.58),
                         (3, y), (61, y), 1)
        offset = 0 if row % 2 else 8
        for x in range(-8 + offset, 64, 16):
            pygame.draw.line(s, textures.shade(roof, 0.66),
                             (x, y), (x, min(63, y + 7)), 1)
    # конёк и тень у основания
    pygame.draw.rect(s, textures.shade(roof, 1.24), (3, 4, 58, 3))
    pygame.draw.rect(s, (42, 31, 20), (3, 58, 58, 3))
    # труба
    chx = 8 + (variant % 2) * 40
    pygame.draw.rect(s, (62, 56, 50), (chx, 8, 9, 11))
    pygame.draw.rect(s, (86, 78, 68), (chx + 1, 8, 7, 3))
    # мох на крыше
    for _ in range(4):
        x = rng.randint(5, 54)
        y = rng.randint(10, 52)
        pygame.draw.ellipse(s, (66, 76, 42), (x, y, rng.randint(3, 8), 2))
    _grime(s, rng, 120, 7)
    return s


def _village_well(variant=0):
    s = _village_plaza(0)
    rng = textures.rng_for(variant, 27, seed=157)
    # тень и каменное кольцо
    pygame.draw.circle(s, (0, 0, 0, 100), (33, 38), 20)
    pygame.draw.circle(s, (74, 68, 58), (32, 34), 19)
    pygame.draw.circle(s, (112, 102, 84), (32, 32), 17)
    pygame.draw.circle(s, (86, 78, 64), (32, 32), 14)
    # вода
    pygame.draw.circle(s, (18, 24, 26), (32, 32), 11)
    pygame.draw.circle(s, (32, 43, 46), (31, 31), 8)
    pygame.draw.arc(s, (92, 112, 112), (24, 24, 16, 16), 0.2, 2.4, 2)
    # деревянная стойка и ведро
    pygame.draw.rect(s, (70, 52, 34), (18, 12, 4, 22))
    pygame.draw.rect(s, (70, 52, 34), (42, 12, 4, 22))
    pygame.draw.rect(s, (96, 72, 44), (16, 8, 32, 5))
    pygame.draw.line(s, (48, 36, 24), (20, 13), (32, 28), 1)
    pygame.draw.line(s, (48, 36, 24), (44, 13), (32, 28), 1)
    pygame.draw.rect(s, (58, 44, 30), (29, 26, 6, 5))
    _grime(s, rng, 80, 7)
    return s

def _village_stall(variant):
    s = _village_path(variant + 1)
    rng = textures.rng_for(variant, 41, seed=131)
    pygame.draw.rect(s, (60, 40, 20), (4, 30, 56, 30))
    pygame.draw.rect(s, (100, 70, 40), (4, 30, 56, 4))
    for i in range(3):
        pygame.draw.rect(s, (140, 100, 60), (8, 34 + i * 8, 48, 5))
    pygame.draw.rect(s, (180, 40, 40), (2, 2, 60, 26))
    pygame.draw.rect(s, (220, 60, 60), (2, 2, 60, 6))
    for x in range(2, 62, 8):
        pygame.draw.line(s, (140, 30, 30), (x, 2), (x, 28), 2)
    for _ in range(6):
        x = rng.randint(10, 54)
        pygame.draw.circle(s, rng.choice([(240,200,100),(200,80,80),(100,180,80)]), (x, 36), 3)
    _grime(s, rng, 60, 7)
    return s


def _village_statue(variant):
    s = _village_plaza(variant + 2)
    rng = textures.rng_for(variant, 43, seed=137)
    pygame.draw.rect(s, (140, 130, 110), (14, 50, 36, 12))
    pygame.draw.rect(s, (100, 90, 70), (14, 50, 36, 12), 2)
    pygame.draw.rect(s, (160, 150, 130), (18, 44, 28, 8))
    pygame.draw.circle(s, (200, 195, 180), (32, 22), 8)
    pygame.draw.rect(s, (200, 195, 180), (26, 30, 12, 16))
    pygame.draw.line(s, (220, 220, 220), (42, 18), (42, 46), 3)
    _grime(s, rng, 40, 6)
    return s


def _village_torch_post(variant):
    s = _village_plaza(variant + 1)
    pygame.draw.rect(s, (70, 50, 30), (30, 20, 4, 38))
    pygame.draw.rect(s, (100, 70, 45), (30, 20, 1, 38))
    pygame.draw.circle(s, (255, 200, 100), (32, 16), 5)
    pygame.draw.circle(s, (255, 150, 50), (32, 16), 3)
    pygame.draw.circle(s, (255, 240, 180), (32, 16), 1)
    glow = pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)
    pygame.draw.circle(glow, (255, 180, 80, 40), (32, 16), 14)
    s.blit(glow, (0, 0))
    return s


# ════════════════════════ ГОРОД ════════════════════════
def _city_grass(variant):
    s = _grass(variant, CITY_PALETTE)
    rng = textures.rng_for(variant, 33, seed=181)
    # ухоженные клумбы и подметённая трава
    if variant % 2 == 0:
        for _ in range(4):
            x = rng.randint(8, SIZE - 10)
            y = rng.randint(10, SIZE - 10)
            pygame.draw.circle(s, (226, 214, 148), (x, y), 2)
            pygame.draw.circle(s, (196, 118, 146), (x + 3, y + 1), 1)
            pygame.draw.line(s, (58, 84, 44), (x, y + 2), (x, y + 5), 1)
    for _ in range(5):
        x, y = rng.randint(2, SIZE - 6), rng.randint(2, SIZE - 6)
        pygame.draw.circle(s, (142, 132, 112), (x, y), 1)
    return s


def _city_path(variant):
    s = _cobbles(variant, (112, 104, 88), (48, 45, 38), cells=5, seed=191)
    rng = textures.rng_for(variant, 35, seed=193)
    # сточная канавка вдоль края
    pygame.draw.line(s, (44, 41, 34), (0, SIZE - 4), (SIZE, SIZE - 4), 2)
    pygame.draw.line(s, (72, 68, 58), (0, SIZE - 6), (SIZE, SIZE - 6), 1)
    for _ in range(4):
        x = rng.randint(2, SIZE - 5)
        pygame.draw.line(s, (88, 102, 60), (x, SIZE - 5), (x, SIZE - 8), 1)
    return s


def _city_plaza(variant):
    s = _cobbles(variant, (128, 118, 98), (56, 51, 42), cells=3, seed=197)
    rng = textures.rng_for(variant, 37, seed=199)
    # декоративный ромб в центре площади
    if variant % 3 == 0:
        pygame.draw.polygon(s, (146, 132, 108),
                            [(32, 14), (50, 32), (32, 50), (14, 32)], 2)
        pygame.draw.polygon(s, (98, 88, 72),
                            [(32, 18), (46, 32), (32, 46), (18, 32)], 1)
    for _ in range(3):
        x, y = rng.randint(4, 54), rng.randint(4, 54)
        pygame.draw.circle(s, (96, 108, 66), (x, y), 1)
    return s


def _city_wall(variant):
    """Городская стена: светлая кладка с мхом и бойницами."""
    s = _new()
    _wall_blocks(s, variant, base_color=(122, 118, 108),
                 mortar=(58, 55, 50))
    rng = textures.rng_for(variant, 39, seed=211)
    # бойница
    x = 24 + (variant % 3) * 6
    pygame.draw.rect(s, (30, 29, 26), (x, 14, 8, 32))
    pygame.draw.rect(s, (74, 71, 64), (x - 2, 12, 12, 4))
    pygame.draw.rect(s, (74, 71, 64), (x - 2, 46, 12, 4))
    for _ in range(7):
        mx, my = rng.randint(2, SIZE - 8), rng.randint(2, SIZE - 8)
        pygame.draw.ellipse(s, (76, 92, 54), (mx, my, rng.randint(3, 9), 2))
    _grime(s, rng, 120, 8)
    return s


def _city_house(variant):
    """Городской дом: черепичная крыша."""
    s = _city_grass(variant + 1)
    rng = textures.rng_for(variant, 41, seed=223)
    roof_base = (138, 78, 56)
    roof = textures.shade(roof_base, 0.9 + textures.hash01(
        variant, 7, seed=227) * 0.2)
    pygame.draw.rect(s, (30, 24, 20), (2, 6, 60, 56))
    pygame.draw.rect(s, roof, (3, 4, 58, 56))
    # черепица: ряды полукруглых пластин
    for row, y in enumerate(range(6, 62, 7)):
        pygame.draw.line(s, textures.shade(roof, 0.58),
                         (3, y), (61, y), 1)
        offset = 0 if row % 2 else 5
        for x in range(-5 + offset, 64, 10):
            pygame.draw.arc(s, textures.shade(roof, 0.72),
                            (x, y - 4, 8, 7), 3.4, 6.0, 1)
            pygame.draw.arc(s, textures.shade(roof, 1.18),
                            (x + 1, y - 5, 6, 6), 3.4, 6.0, 1)
    pygame.draw.rect(s, textures.shade(roof, 1.28), (3, 4, 58, 3))
    pygame.draw.rect(s, (48, 32, 24), (3, 58, 58, 3))
    # слуховое окно
    wx = 12 + (variant % 2) * 32
    pygame.draw.rect(s, (66, 50, 36), (wx, 12, 12, 10))
    pygame.draw.rect(s, (150, 128, 74), (wx + 2, 14, 8, 6))
    for _ in range(3):
        x = rng.randint(6, 52)
        y = rng.randint(12, 50)
        pygame.draw.ellipse(s, (70, 88, 52), (x, y, rng.randint(3, 7), 2))
    _grime(s, rng, 90, 7)
    return s


def _city_well(variant=0):
    s = _city_plaza(0)
    rng = textures.rng_for(variant, 43, seed=229)
    pygame.draw.circle(s, (0, 0, 0, 100), (33, 38), 20)
    pygame.draw.circle(s, (78, 74, 66), (32, 34), 19)
    pygame.draw.circle(s, (126, 120, 104), (32, 32), 17)
    pygame.draw.circle(s, (92, 86, 72), (32, 32), 14)
    pygame.draw.circle(s, (16, 24, 30), (32, 32), 11)
    pygame.draw.circle(s, (34, 52, 62), (31, 31), 8)
    pygame.draw.arc(s, (110, 140, 150), (24, 24, 16, 16), 0.2, 2.4, 2)
    pygame.draw.rect(s, (74, 56, 38), (18, 10, 4, 24))
    pygame.draw.rect(s, (74, 56, 38), (42, 10, 4, 24))
    pygame.draw.rect(s, (104, 78, 48), (16, 6, 32, 5))
    pygame.draw.line(s, (48, 36, 24), (20, 11), (32, 28), 1)
    pygame.draw.line(s, (48, 36, 24), (44, 11), (32, 28), 1)
    pygame.draw.rect(s, (64, 48, 32), (29, 26, 6, 5))
    _grime(s, rng, 70, 7)
    return s


def _city_stall(variant):
    s = _city_path(variant + 1)
    rng = textures.rng_for(variant, 45, seed=233)
    pygame.draw.rect(s, (58, 42, 24), (4, 30, 56, 30))
    pygame.draw.rect(s, (104, 76, 44), (4, 30, 56, 4))
    for i in range(3):
        pygame.draw.rect(s, (140, 104, 62), (8, 34 + i * 8, 48, 5))
    awning = (40, 92, 132) if variant % 2 else (128, 52, 116)
    pygame.draw.rect(s, awning, (2, 2, 60, 26))
    pygame.draw.rect(s, textures.shade(awning, 1.25), (2, 2, 60, 6))
    for x in range(2, 62, 8):
        pygame.draw.line(s, textures.shade(awning, 0.7), (x, 2), (x, 28), 2)
    for _ in range(6):
        x = rng.randint(10, 54)
        pygame.draw.circle(s, rng.choice([(240, 200, 100), (200, 80, 80),
                                          (100, 180, 80)]), (x, 36), 3)
    _grime(s, rng, 60, 7)
    return s


def _city_statue(variant):
    s = _city_plaza(variant + 2)
    rng = textures.rng_for(variant, 47, seed=239)
    pygame.draw.rect(s, (146, 138, 120), (12, 48, 40, 14))
    pygame.draw.rect(s, (104, 96, 78), (12, 48, 40, 14), 2)
    pygame.draw.rect(s, (168, 160, 140), (17, 42, 30, 8))
    pygame.draw.circle(s, (206, 200, 184), (32, 20), 8)
    pygame.draw.rect(s, (206, 200, 184), (26, 28, 12, 16))
    pygame.draw.line(s, (222, 222, 210), (42, 16), (42, 46), 3)
    pygame.draw.circle(s, (222, 222, 210), (42, 14), 3)
    _grime(s, rng, 40, 6)
    return s


def _city_torch(variant):
    s = _city_plaza(variant + 1)
    pygame.draw.rect(s, (66, 50, 32), (30, 20, 4, 38))
    pygame.draw.rect(s, (104, 78, 48), (30, 20, 1, 38))
    pygame.draw.rect(s, (88, 66, 42), (26, 18, 12, 4))
    pygame.draw.circle(s, (255, 200, 100), (32, 14), 5)
    pygame.draw.circle(s, (255, 150, 50), (32, 14), 3)
    pygame.draw.circle(s, (255, 240, 180), (32, 14), 1)
    glow = pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)
    pygame.draw.circle(glow, (255, 180, 80, 44), (32, 14), 15)
    s.blit(glow, (0, 0))
    return s

