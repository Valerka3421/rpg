"""Auto-generated from render/tiles.py — see git history."""

import pygame

from render import textures
from render.tiles._common import (
    SIZE, TRUNK,
    STONE, STONE_DARK,
    _grime,
)

from render.tiles._settlement import _city_grass
from render.tiles._dungeon import _wall
from render.tiles._nature import _road


# ════════════════════════ ОБЪЕКТЫ ════════════════════════
def _quest_board(variant=0):
    """Отдельная текстура доски поручений (не спрайт NPC)."""
    s = _city_grass(0)
    rng = textures.rng_for(variant, 71, seed=197)
    # столбы
    pygame.draw.rect(s, (78, 54, 32), (12, 24, 5, 36))
    pygame.draw.rect(s, (78, 54, 32), (47, 24, 5, 36))
    pygame.draw.rect(s, (52, 36, 22), (12, 24, 5, 36), 1)
    pygame.draw.rect(s, (52, 36, 22), (47, 24, 5, 36), 1)
    # сама доска
    pygame.draw.rect(s, (34, 24, 14), (8, 8, 48, 26))
    pygame.draw.rect(s, (120, 84, 48), (9, 9, 46, 24))
    for y in (15, 21, 27):
        pygame.draw.line(s, (94, 64, 36), (12, y), (52, y), 1)
    # записки
    for i, (x, y, w, h) in enumerate(((14, 12, 10, 8), (28, 10, 9, 11),
                                      (40, 15, 9, 7))):
        pygame.draw.rect(s, (225, 215, 190), (x, y, w, h))
        pygame.draw.rect(s, (150, 140, 120), (x, y, w, h), 1)
        pygame.draw.line(s, (120, 110, 100), (x + 2, y + 3),
                         (x + w - 2, y + 3), 1)
    # гвозди
    for x, y in ((16, 11), (50, 11), (16, 30), (50, 30)):
        pygame.draw.circle(s, (190, 190, 200), (x, y), 2)
    _grime(s, rng, 30, 5)
    return s


# ════════════════════════ ДОБЫВАЕМЫЕ РЕСУРСЫ ════════════════════════
def _stump(variant=0):
    """Спиленное дерево: пенёк с годовыми кольцами (фон прозрачный)."""
    s = pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)
    rng = textures.rng_for(variant, 73, seed=211)
    shadow = pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)
    pygame.draw.ellipse(shadow, (0, 0, 0, 105), (10, 40, 46, 15))
    s.blit(shadow, (0, 0))
    # корни
    pygame.draw.polygon(s, textures.shade(TRUNK, 0.55), [
        (22, 54), (28, 40), (38, 40), (44, 54), (38, 50), (28, 50)])
    pygame.draw.rect(s, textures.shade(TRUNK, 0.82), (24, 30, 17, 12))
    pygame.draw.rect(s, TRUNK, (25, 29, 15, 9))
    # срез
    pygame.draw.ellipse(s, (146, 112, 70), (24, 25, 17, 10))
    pygame.draw.ellipse(s, (108, 78, 48), (26, 27, 13, 6), 1)
    pygame.draw.ellipse(s, (88, 62, 38), (29, 28, 7, 3), 1)
    # щепки
    for _ in range(5):
        x = rng.randint(14, 50)
        y = rng.randint(40, 56)
        pygame.draw.line(s, textures.shade(TRUNK, rng.uniform(0.9, 1.3)),
                         (x, y), (x + rng.randint(2, 5), y), 1)
    _grime(s, rng, 60, 6)
    return s


def _berry_bush(variant=0, berries=True):
    """Куст ягод. Пустой куст — то же растение без ягод (фон прозрачный)."""
    s = pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)
    rng = textures.rng_for(variant, 79, seed=223 + (0 if berries else 7))
    shadow = pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)
    pygame.draw.ellipse(shadow, (0, 0, 0, 110), (8, 42, 50, 16))
    s.blit(shadow, (0, 0))
    bush = pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)
    lobes = ((24, 34, 13), (40, 33, 13), (32, 24, 14),
             (18, 42, 9), (46, 42, 9))
    base = (44, 66, 38)
    for cx, cy, r in lobes:
        pygame.draw.circle(bush, textures.shade(base, 0.72), (cx, cy + 2), r)
    for cx, cy, r in lobes:
        pygame.draw.circle(bush, base, (cx, cy), max(3, r - 2))
        pygame.draw.circle(bush, textures.shade(base, 1.28),
                           (cx - r // 3, cy - r // 3), max(2, r // 3))
    for _ in range(26):
        x = rng.randint(12, 52)
        y = rng.randint(12, 48)
        if bush.get_at((x, y))[3] < 40:
            continue
        pygame.draw.circle(bush, textures.shade(base, rng.uniform(0.7, 1.25)),
                           (x, y), 1)
    if berries:
        for _ in range(9):
            x = rng.randint(14, 50)
            y = rng.randint(16, 46)
            if bush.get_at((x, y))[3] < 70:
                continue
            pygame.draw.circle(bush, (150, 32, 40), (x, y), 2)
            pygame.draw.circle(bush, (226, 74, 66), (x - 1, y - 1), 1)
    s.blit(bush, (0, 0))
    return s


def _ore_rock(variant=0):
    """Стена с рудной жилой: тёмная порода и бронзовые блёстки."""
    s = _wall(variant + 1)
    rng = textures.rng_for(variant, 83, seed=239)
    for _ in range(3):
        cx = rng.randint(12, 52)
        cy = rng.randint(12, 52)
        r = rng.randint(5, 10)
        pygame.draw.circle(s, (36, 34, 31), (cx, cy), r)
        pygame.draw.circle(s, (62, 58, 51), (cx - 1, cy - 1), max(2, r - 2))
        # жила
        for _ in range(4):
            x = cx + rng.randint(-r, r)
            y = cy + rng.randint(-r, r)
            pygame.draw.circle(s, (188, 138, 62), (x, y), 1)
    # крупный самородок
    pygame.draw.polygon(s, (150, 108, 52), [
        (30, 38), (37, 32), (42, 36), (39, 44), (31, 45)])
    pygame.draw.polygon(s, (224, 176, 88), [
        (33, 35), (37, 33), (39, 37), (34, 40)])
    _grime(s, rng, 120, 7)
    return s


def _ore_depleted(variant=0):
    """Выработанная жила — обычная порода с крошкой."""
    s = _wall(variant + 1)
    rng = textures.rng_for(variant, 89, seed=241)
    for _ in range(10):
        x = rng.randint(6, 56)
        y = rng.randint(6, 56)
        pygame.draw.rect(s, textures.shade(STONE_DARK, rng.uniform(0.7, 1.2)),
                         (x, y, rng.choice((1, 2)), 1))
    _grime(s, rng, 140, 8)
    return s


def _gate(variant=0):
    """Каменные врата локации: арка с тёмным проходом."""
    s = _road(variant)
    rng = textures.rng_for(variant, 91, seed=251)
    # опоры
    for x in (10, 46):
        pygame.draw.rect(s, (42, 38, 33), (x, 14, 8, 42))
        pygame.draw.rect(s, textures.shade(STONE, 0.95), (x + 1, 13, 6, 42))
        pygame.draw.rect(s, textures.shade(STONE, 1.18), (x + 1, 13, 2, 42))
        for yy in (22, 33, 44):
            pygame.draw.line(s, (52, 48, 42), (x + 1, yy), (x + 6, yy), 1)
    # арка
    pygame.draw.polygon(s, (42, 38, 33), [
        (8, 15), (18, 5), (46, 5), (56, 15), (56, 22), (8, 22)])
    pygame.draw.polygon(s, textures.shade(STONE, 1.02), [
        (10, 14), (19, 7), (45, 7), (54, 14), (54, 20), (10, 20)])
    pygame.draw.polygon(s, (16, 14, 12), [
        (19, 20), (23, 13), (41, 13), (45, 20), (45, 56), (19, 56)])
    pygame.draw.polygon(s, (8, 7, 6), [
        (22, 20), (26, 15), (38, 15), (42, 20), (42, 55), (22, 55)])
    # гравий у входа
    for _ in range(8):
        x = rng.randint(20, 44)
        y = rng.randint(46, 58)
        pygame.draw.circle(s, textures.shade(STONE_DARK, rng.uniform(0.8, 1.3)),
                           (x, y), 1)
    _grime(s, rng, 90, 7)
    return s


