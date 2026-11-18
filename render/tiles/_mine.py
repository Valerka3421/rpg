"""Auto-generated from render/tiles.py — see git history."""
import math

import pygame

from render import textures
from render.tiles._common import (
    SIZE, WALL_BASE, MORTAR, FLOOR_BASE, FLOOR_GROUT, TORCH_GLOW,
    MOSS, MOSS_DARK, RUST, BRONZE, BONE,
    GRASS_BASE, GRASS_DARK, GRASS_DRY, TREE_BASE, TREE_DARK, TRUNK,
    STONE, STONE_DARK,
    BIOME_PALETTES, VILLAGE_PALETTE, CITY_PALETTE,
    _new, _clamp, _grime, _chip, _crack,
)

from render.tiles._dungeon_ruins import _dungeon_ground
from render.tiles._nature import _road


# ════════════════════════ ШАХТА ════════════════════════
def _arrow(variant, direction='s'):
    """Дорожный указатель на дороге: столб с доской-стрелкой.

    Доска рисуется со стрелкой вправо и поворачивается по стороне
    перехода (pygame.transform.rotate, кратные 90° — без искажений).
    """
    s = _road(variant)
    rng = textures.rng_for(variant, 92, seed=257)
    wood = (108, 82, 52)
    # тень у столба
    shadow = pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)
    pygame.draw.ellipse(shadow, (0, 0, 0, 100), (22, 52, 22, 9))
    s.blit(shadow, (0, 0))
    # столб
    pygame.draw.rect(s, textures.shade(wood, 0.55), (30, 20, 7, 38))
    pygame.draw.rect(s, wood, (31, 20, 5, 38))
    pygame.draw.line(s, textures.shade(wood, 1.25), (31, 22), (31, 56), 1)
    # доска со стрелкой (базовое направление — вправо)
    plank = pygame.Surface((46, 20), pygame.SRCALPHA)
    pygame.draw.rect(plank, textures.shade(wood, 0.7), (0, 0, 46, 20),
                     border_radius=4)
    pygame.draw.rect(plank, wood, (1, 1, 44, 18), border_radius=4)
    pygame.draw.line(plank, textures.shade(wood, 1.2), (3, 3), (42, 3), 1)
    pygame.draw.line(plank, textures.shade(wood, 0.55), (3, 17), (42, 17), 1)
    paint = textures.shade(BRONZE, 0.55)
    pygame.draw.rect(plank, paint, (5, 8, 24, 5))
    pygame.draw.polygon(plank, paint, [(27, 3), (41, 10), (27, 17)])
    pygame.draw.polygon(plank, textures.shade(BRONZE, 0.9),
                        [(28, 5), (38, 10), (28, 15)])
    rot = {'e': 0, 's': -90, 'w': 180, 'n': 90}.get(direction, 0)
    if rot:
        plank = pygame.transform.rotate(plank, rot)
    px = (SIZE - plank.get_width()) // 2
    py = 4
    s.blit(plank, (px, py))
    # гвозди
    for nx, ny in ((33, 26), (33, 34)):
        pygame.draw.circle(s, (52, 44, 34), (nx, ny), 1)
    _grime(s, rng, 100, 8)
    return s


# ════════════════════════ ШАХТА ════════════════════════
def _mine_wall(variant):
    """Стена шахты: тёмная порода с деревянными распорками."""
    s = _new()
    base = (54, 46, 38)
    s.fill(base)
    rng = textures.rng_for(variant, 93, seed=263)
    textures.speckles(s, (0, 0, SIZE, SIZE), base, rng, 420, 0.20)
    # порода: крупные глыбы
    for _ in range(5):
        x = rng.randint(2, SIZE - 16)
        y = rng.randint(2, SIZE - 14)
        w = rng.randint(10, 18)
        h = rng.randint(8, 14)
        tone = rng.uniform(0.7, 1.05)
        pygame.draw.ellipse(s, textures.shade((66, 56, 46), tone),
                            (x, y, w, h))
        pygame.draw.ellipse(s, textures.shade((40, 33, 27), tone),
                            (x, y, w, h), 1)
    # трещины
    for _ in range(3):
        _crack(s, rng.randint(4, 56), rng.randint(2, 12), 3,
               (28, 23, 19), rng, drift=5)
    # деревянные распорки по краям (крепь тоннеля)
    beam = (96, 70, 42)
    pygame.draw.rect(s, textures.shade(beam, 0.55), (0, 0, 8, SIZE))
    pygame.draw.rect(s, beam, (1, 0, 5, SIZE))
    pygame.draw.rect(s, textures.shade(beam, 0.55), (SIZE - 8, 0, 8, SIZE))
    pygame.draw.rect(s, beam, (SIZE - 6, 0, 5, SIZE))
    pygame.draw.rect(s, textures.shade(beam, 0.8), (8, 6, SIZE - 16, 7))
    pygame.draw.line(s, textures.shade(beam, 1.25), (9, 7), (55, 7), 1)
    for gx in range(3):
        x = 14 + gx * 18
        pygame.draw.line(s, textures.shade(beam, 0.45), (x, 13), (x, 2), 2)
    _grime(s, rng, 140, 9)
    return s


def _mine_ground(variant):
    """Пол шахты: тёмная утоптанная порода, шпалы и рельсы."""
    s = _new()
    base = (46, 40, 34)
    s.fill(base)
    rng = textures.rng_for(variant, 94, seed=269)
    textures.speckles(s, (0, 0, SIZE, SIZE), base, rng, 420, 0.16)
    # лужи и провалы
    for _ in range(2):
        x = rng.randint(4, SIZE - 18)
        y = rng.randint(4, SIZE - 14)
        pygame.draw.ellipse(s, (32, 27, 23), (x, y, rng.randint(10, 20),
                                              rng.randint(7, 12)))
    # гравий
    for _ in range(16):
        x = rng.randint(2, SIZE - 3)
        y = rng.randint(2, SIZE - 3)
        c = textures.shade(base, rng.uniform(0.6, 1.4))
        pygame.draw.rect(s, c, (x, y, rng.choice((1, 2)), 1))
    # шпалы с рельсами (у половины вариантов)
    if variant % 2 == 0:
        rail = (110, 104, 96)
        sleeper = (74, 56, 34)
        vertical = bool(variant % 4)
        if vertical:
            for y in range(4, SIZE, 12):
                pygame.draw.rect(s, sleeper, (10, y, 44, 4))
            pygame.draw.rect(s, rail, (16, 0, 3, SIZE))
            pygame.draw.rect(s, rail, (45, 0, 3, SIZE))
        else:
            for x in range(4, SIZE, 12):
                pygame.draw.rect(s, sleeper, (x, 10, 4, 44))
            pygame.draw.rect(s, rail, (0, 16, SIZE, 3))
            pygame.draw.rect(s, rail, (0, 45, SIZE, 3))
    # рудные блёстки
    if variant % 3 == 0:
        for _ in range(4):
            x = rng.randint(6, SIZE - 6)
            y = rng.randint(6, SIZE - 6)
            pygame.draw.circle(s, textures.shade(BRONZE, 0.5), (x, y), 1)
    _grime(s, rng, 200, 10)
    return s


# ════════════════════════ ДВЕРЬ И СУНДУК КАЗНЫ ════════════════════════
def _locked_door(variant=0):
    """Запертая дверь босс-комплекса: окованная створка с замком."""
    s = _dungeon_ground(3)
    rng = textures.rng_for(variant, 95, seed=271)
    wood = (86, 60, 38)
    iron = (58, 60, 66)
    # тень
    shadow = pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)
    pygame.draw.ellipse(shadow, (0, 0, 0, 110), (10, 52, 44, 9))
    s.blit(shadow, (0, 0))
    # рама
    pygame.draw.rect(s, (26, 22, 18), (10, 2, 44, 60), border_radius=3)
    # створка с досками
    pygame.draw.rect(s, textures.shade(wood, 0.8), (13, 5, 38, 54),
                     border_radius=3)
    for i in range(3):
        x = 15 + i * 12
        pygame.draw.line(s, textures.shade(wood, 0.6), (x, 6), (x, 58), 2)
    pygame.draw.line(s, textures.shade(wood, 1.2), (14, 6), (50, 6), 1)
    # кованые поперечины
    for y in (14, 44):
        pygame.draw.rect(s, iron, (13, y, 38, 5))
        pygame.draw.line(s, textures.shade(iron, 1.5), (14, y + 1),
                         (50, y + 1), 1)
        for bx in (18, 44):
            pygame.draw.circle(s, (34, 36, 40), (bx, y + 2), 1)
    # замок
    pygame.draw.rect(s, iron, (26, 32, 12, 14), border_radius=2)
    pygame.draw.rect(s, textures.shade(iron, 1.4), (27, 33, 10, 3))
    pygame.draw.circle(s, (22, 24, 28), (32, 39), 2)
    pygame.draw.rect(s, (22, 24, 28), (31, 39, 2, 4))
    # дужка замка над створкой не нужна: дверь заперта изнутри коридора
    _grime(s, rng, 80, 7)
    return s


def _chest(variant=0):
    """Сундук казны: окованный, с дужкой-замком."""
    s = _dungeon_ground(5)
    rng = textures.rng_for(variant, 96, seed=277)
    wood = (110, 78, 44)
    wood_dark = textures.shade(wood, 0.6)
    iron = (58, 60, 66)
    # тень
    shadow = pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)
    pygame.draw.ellipse(shadow, (0, 0, 0, 115), (8, 50, 48, 11))
    s.blit(shadow, (0, 0))
    # корпус
    pygame.draw.rect(s, wood_dark, (10, 34, 44, 20), border_radius=3)
    pygame.draw.rect(s, wood, (11, 35, 42, 18), border_radius=3)
    for x in range(18, 52, 10):
        pygame.draw.line(s, textures.shade(wood, 0.65), (x, 36), (x, 52), 1)
    # крышка (полукруг)
    pygame.draw.rect(s, wood_dark, (10, 24, 44, 12), border_radius=6)
    pygame.draw.rect(s, textures.shade(wood, 1.08), (11, 25, 42, 10),
                     border_radius=6)
    pygame.draw.line(s, textures.shade(wood, 1.3), (12, 26), (51, 26), 1)
    # оковка
    for x in (14, 46):
        pygame.draw.rect(s, iron, (x, 24, 4, 30))
        pygame.draw.line(s, textures.shade(iron, 1.5), (x + 1, 25),
                         (x + 1, 53), 1)
    pygame.draw.rect(s, iron, (10, 32, 44, 3))
    # замок по центру
    pygame.draw.rect(s, textures.shade(iron, 0.85), (28, 32, 8, 10),
                     border_radius=2)
    pygame.draw.circle(s, (20, 22, 26), (32, 38), 2)
    pygame.draw.rect(s, (20, 22, 26), (31, 38, 2, 3))
    _grime(s, rng, 70, 7)
    return s


