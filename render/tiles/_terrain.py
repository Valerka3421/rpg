"""Auto-generated from render/tiles.py — see git history."""
import math

import pygame

from render import textures
from render.tiles._common import (
    SIZE, GRASS_BASE, STONE, STONE_DARK,
    _new, _grime,
)

from render.tiles._dungeon_ruins import _dungeon_ground
from render.tiles._settlement import _cobbles
from render.tiles._nature import _grass


# ════════════════════════ МЕСТНОСТЬ ════════════════════════
def _sand(variant):
    """Песчаный берег: тёплый песок с редкими камешками."""
    base = (196, 178, 128)
    s = _new()
    s.fill(base)
    rng = textures.rng_for(variant, 71, seed=211)
    textures.speckles(s, (0, 0, SIZE, SIZE), base, rng, 460, 0.10)
    for _ in range(14):
        x = rng.randint(2, SIZE - 3)
        y = rng.randint(2, SIZE - 3)
        col = textures.shade(base, rng.uniform(0.86, 1.10))
        pygame.draw.circle(s, col, (x, y), rng.randint(1, 2))
    for _ in range(3):
        x = rng.randint(4, SIZE - 6)
        y = rng.randint(4, SIZE - 6)
        pygame.draw.circle(s, (150, 140, 118), (x, y), 2)
    return s


def _water(variant):
    """Морская вода: волны и блики."""
    base = (46, 92, 128)
    s = _new()
    s.fill(base)
    rng = textures.rng_for(variant, 73, seed=223)
    textures.speckles(s, (0, 0, SIZE, SIZE), base, rng, 300, 0.12)
    for _ in range(7):
        x = rng.randint(-6, SIZE - 16)
        y = rng.randint(2, SIZE - 6)
        w = rng.randint(14, 34)
        pygame.draw.arc(s, (126, 178, 204), (x, y, w, 10),
                        0.15, 3.0, 2)
    return s


def _rock(variant):
    """Валун на каменистой земле — стена каменного края."""
    s = _dungeon_ground(variant + 2)
    rng = textures.rng_for(variant, 31, seed=167)
    for i in range(2):
        cx = rng.randint(14, 50)
        cy = rng.randint(16, 48)
        r = rng.randint(9, 15)
        pygame.draw.circle(s, (0, 0, 0, 90), (cx + 2, cy + 3), r)
        pygame.draw.circle(s, textures.shade(STONE_DARK, 0.9), (cx, cy), r)
        pygame.draw.circle(s, textures.shade(STONE, 0.94), (cx - 2, cy - 3),
                           max(4, r - 3))
        pygame.draw.circle(s, textures.shade(STONE, 1.16), (cx - r // 3,
                                                           cy - r // 2),
                           max(2, r // 3))
        pygame.draw.line(s, textures.shade(STONE_DARK, 0.7),
                         (cx - r // 2, cy + r // 3),
                         (cx + r // 3, cy + r // 2), 1)
    for _ in range(4):
        x = rng.randint(4, SIZE - 6)
        y = rng.randint(4, SIZE - 4)
        pygame.draw.line(s, textures.shade(GRASS_BASE, 0.9),
                         (x, y), (x, y - rng.randint(2, 5)))
    _grime(s, rng, 120, 8)
    return s


def _goblin_grass(variant):
    """Тёмная, вытоптанная трава Диких земель."""
    palette = {
        'grass': (50, 62, 40), 'grass_dark': (30, 40, 28),
        'grass_dry': (78, 76, 46),
        'tree': (44, 56, 36), 'tree_dark': (22, 32, 22),
        'trunk': (62, 48, 34),
    }
    s = _grass(variant, palette)
    rng = textures.rng_for(variant, 61, seed=173)
    # кости и редкие грибы вокруг логова
    if variant % 3 == 0:
        pygame.draw.line(s, (210, 205, 185), (14, 46), (26, 40), 3)
        pygame.draw.circle(s, (210, 205, 185), (13, 46), 3)
        pygame.draw.circle(s, (210, 205, 185), (27, 39), 3)
    if variant % 4 == 1:
        pygame.draw.circle(s, (150, 70, 70), (46, 20), 4)
        pygame.draw.circle(s, (110, 50, 50), (46, 20), 4, 1)
    _grime(s, rng, 90, 7)
    return s


def _goblin_path(variant):
    return _cobbles(variant, (94, 78, 52), (42, 35, 24), cells=4, seed=181)


def _goblin_plaza(variant):
    s = _cobbles(variant, (104, 86, 58), (46, 38, 26), cells=3, seed=183)
    rng = textures.rng_for(variant, 63, seed=187)
    if variant == 0:
        pygame.draw.circle(s, (54, 40, 26), (32, 34), 17, 2)
        pygame.draw.line(s, (150, 130, 90), (32, 22), (32, 46), 1)
    _grime(s, rng, 70, 8)
    return s


def _goblin_hut(variant):
    """Крыша гоблинской хижины: тёмное дерево, солома и шипы."""
    s = _goblin_grass(variant + 1)
    rng = textures.rng_for(variant, 65, seed=191)
    roof_base = (74, 62, 38)
    roof = textures.shade(roof_base, 0.86 + textures.hash01(
        variant, 5, seed=59) * 0.2)
    pygame.draw.rect(s, (22, 18, 12), (2, 8, 60, 54))
    pygame.draw.rect(s, roof, (3, 6, 58, 54))
    # соломенные ряды
    for y in range(10, 60, 7):
        pygame.draw.line(s, textures.shade(roof, 0.55), (3, y), (61, y), 1)
        for x in range(4 + (y // 7 % 2) * 6, 62, 12):
            pygame.draw.line(s, textures.shade(roof, 0.68),
                             (x, y), (x - 3, y + 6), 1)
    # шипы на коньке
    for x in (12, 30, 48):
        pygame.draw.polygon(s, (58, 46, 30),
                            [(x, 8), (x + 4, 1), (x + 8, 8)])
    pygame.draw.rect(s, (34, 27, 17), (3, 58, 58, 2))
    _grime(s, rng, 120, 7)
    return s


def _goblin_totem(variant):
    s = _goblin_plaza(variant + 1)
    rng = textures.rng_for(variant, 67, seed=193)
    # столб
    pygame.draw.rect(s, (66, 48, 30), (26, 8, 12, 50))
    pygame.draw.rect(s, (44, 31, 19), (26, 8, 12, 50), 2)
    # череп и рога
    pygame.draw.circle(s, (220, 212, 190), (32, 22), 9)
    pygame.draw.circle(s, (40, 35, 30), (28, 21), 2)
    pygame.draw.circle(s, (40, 35, 30), (36, 21), 2)
    pygame.draw.polygon(s, (220, 212, 190),
                        [(23, 24), (14, 14), (22, 17)])
    pygame.draw.polygon(s, (220, 212, 190),
                        [(41, 24), (50, 14), (42, 17)])
    # краска
    pygame.draw.line(s, (150, 50, 40), (28, 38), (36, 42), 2)
    pygame.draw.line(s, (150, 50, 40), (36, 38), (28, 42), 2)
    _grime(s, rng, 40, 6)
    return s


def _goblin_campfire(variant):
    s = _goblin_plaza(variant + 2)
    # камни
    for angle in range(0, 360, 60):
        x = 32 + int(15 * math.cos(math.radians(angle)))
        y = 36 + int(9 * math.sin(math.radians(angle)))
        pygame.draw.circle(s, (92, 88, 80), (x, y), 5)
        pygame.draw.circle(s, (60, 58, 52), (x, y), 5, 1)
    # угли и пламя
    pygame.draw.ellipse(s, (40, 24, 16), (20, 32, 24, 12))
    pygame.draw.polygon(s, (240, 110, 40),
                        [(32, 12), (40, 30), (32, 36), (24, 30)])
    pygame.draw.polygon(s, (255, 205, 90),
                        [(32, 20), (36, 31), (32, 34), (28, 31)])
    glow = pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)
    pygame.draw.circle(glow, (255, 160, 60, 46), (32, 30), 22)
    s.blit(glow, (0, 0))
    return s

