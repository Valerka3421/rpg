"""Auto-generated from render/tiles.py — see git history."""
import math

import pygame

from render import textures
from render.tiles._common import (
    SIZE, RUST, BRONZE, BONE,
    GRASS_BASE, GRASS_DARK, GRASS_DRY, TREE_BASE, TREE_DARK, TRUNK,
    STONE, STONE_DARK,
    BIOME_PALETTES, _new, _grime, _crack,
)

from render.tiles._dungeon_ruins import _dungeon_ground


# ════════════════════════ ПОВЕРХНОСТЬ ════════════════════════
def _grass(variant, palette=None):
    p = palette or {}
    base = p.get('grass', GRASS_BASE)
    dark = p.get('grass_dark', GRASS_DARK)
    dry = p.get('grass_dry', GRASS_DRY)
    s = _new()
    s.fill(base)
    rng = textures.rng_for(variant, 4, seed=17)
    v = variant % 8
    textures.speckles(s, (0, 0, SIZE, SIZE), base, rng, 420, 0.16)
    # Крупные пятна тона: без них поле из одинаковых тайлов выглядит
    # заливкой. Пятна полупрозрачные и низкочастотные, поэтому швы между
    # клетками не читаются.
    patch = pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)
    for _ in range(2):
        px = rng.randint(-10, SIZE - 20)
        py = rng.randint(-10, SIZE - 20)
        pw = rng.randint(30, 56)
        ph = rng.randint(24, 44)
        tone = (dark if rng.random() < 0.5
                else textures.shade(base, 1.10))
        pygame.draw.ellipse(patch, (*tone, 42), (px, py, pw, ph))
    s.blit(patch, (0, 0))
    # травинки
    for _ in range(34):
        x = rng.randint(2, SIZE - 3)
        y = rng.randint(4, SIZE - 3)
        h = rng.randint(2, 6)
        col = textures.shade(base, rng.uniform(0.62, 1.30))
        pygame.draw.line(s, col, (x, y), (x + rng.choice((-1, 0, 1)), y - h))

    if v == 4:                               # сухие цветы
        for _ in range(4):
            x = rng.randint(6, SIZE - 8)
            y = rng.randint(6, SIZE - 8)
            pygame.draw.circle(s, textures.shade(BONE, 0.95), (x, y), 2)
            pygame.draw.circle(s, textures.shade(RUST, 1.25), (x, y), 1)
    elif v == 5:                             # проплешина земли в углу
        x, y = rng.choice(((2, 4), (34, 8), (6, 38), (36, 40)))
        pygame.draw.ellipse(s, (72, 64, 48), (x, y, 24, 16))
        pygame.draw.ellipse(s, (56, 49, 37), (x + 5, y + 4, 13, 8))
        for _ in range(4):
            gx = x + rng.randint(2, 20)
            gy = y + rng.randint(3, 13)
            pygame.draw.line(s, textures.shade(dry, 0.9),
                             (gx, gy), (gx, gy - rng.randint(2, 4)))
    elif v == 6:                             # камни
        for _ in range(3):
            x = rng.randint(8, SIZE - 12)
            y = rng.randint(8, SIZE - 10)
            r = rng.randint(2, 3)
            pygame.draw.circle(s, textures.shade(STONE_DARK, 1.15), (x, y), r)
            pygame.draw.circle(s, textures.shade(STONE, 0.9),
                               (x - 1, y - 1), max(1, r - 1))
    elif v == 7:                             # высокая сухая трава
        for _ in range(12):
            x = rng.randint(4, SIZE - 6)
            y = rng.randint(12, SIZE - 4)
            pygame.draw.line(s, textures.shade(dry, 1.0),
                             (x, y), (x + 1, y - rng.randint(4, 8)))
            pygame.draw.line(s, textures.shade(base, 0.78),
                             (x + 2, y), (x + 2, y - rng.randint(3, 6)))

    _grime(s, rng, 200, 10)
    return s


def _tree(variant, palette=None):
    p = palette or {}
    tree_base = p.get('tree', TREE_BASE)
    tree_dark = p.get('tree_dark', TREE_DARK)
    trunk = p.get('trunk', TRUNK)
    # Фон прозрачный: пол под деревом рисует карта по ground_kind, иначе
    # после рубки трава под пеньком отличалась бы от соседних клеток.
    s = pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)
    rng = textures.rng_for(variant, 6, seed=43)
    # тень под кроной
    shadow = pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)
    pygame.draw.ellipse(shadow, (0, 0, 0, 110), (4, 44, 56, 16))
    s.blit(shadow, (0, 0))

    # ствол с корнями
    pygame.draw.polygon(s, textures.shade(trunk, 0.55), [
        (27, 54), (30, 34), (34, 34), (37, 54)])
    pygame.draw.rect(s, trunk, (29, 34, 6, 21))
    pygame.draw.rect(s, textures.shade(trunk, 0.62), (33, 34, 2, 21))
    pygame.draw.line(s, textures.shade(trunk, 1.22), (29, 36), (29, 54), 1)
    pygame.draw.line(s, textures.shade(trunk, 0.45), (26, 55), (30, 51), 2)
    pygame.draw.line(s, textures.shade(trunk, 0.45), (38, 55), (34, 51), 2)

    # крона: тёмная олива, свет только сверху-слева
    crown = pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)
    circles = [(24, 27, 14), (40, 25, 15), (32, 16, 16),
               (17, 36, 9), (47, 36, 9)]
    for cx, cy, r in circles:
        pygame.draw.circle(crown, tree_base, (cx, cy), r)
    for cx, cy, r in circles:
        pygame.draw.circle(crown, tree_dark, (cx, cy + r // 3), max(3, r - 3))
    for cx, cy, r in circles:
        pygame.draw.circle(crown, textures.shade(tree_base, 1.16),
                           (cx - r // 4, cy - r // 3), max(3, r // 2))
    # листва: мелкие пятна
    for _ in range(30):
        x = rng.randint(10, 54)
        y = rng.randint(6, 46)
        if crown.get_at((x, y))[3] < 40:
            continue
        factor = rng.uniform(0.68, 1.22)
        pygame.draw.circle(crown, textures.shade(tree_base, factor), (x, y), 1)
    s.blit(textures.silhouette(crown, tree_dark, 1), (0, 0))
    s.blit(crown, (0, 0))
    return s


# ════════════════════════ БИОМЫ: НОВАЯ ОТРИСОВКА ════════════════════════
# Каждый край рисуется своим набором деталей поверх общей палитры: у
# сосен — хвоя и шишки, у болота — лужи и камыш, у мёртвого леса — кости.
BIOME_LEAF_COLORS = {
    'pine':   ((42, 64, 40), (25, 44, 28)),
    'birch':  ((96, 118, 62), (60, 84, 46)),
    'rock':   ((104, 100, 92), (62, 60, 54)),
    'autumn': ((152, 94, 40), (104, 58, 26)),
    'swamp':  ((56, 78, 58), (32, 50, 42)),
    'dark':   ((36, 54, 40), (20, 34, 26)),
    'meadow': ((76, 98, 52), (46, 68, 36)),
    'dead':   ((98, 88, 72), (58, 52, 44)),
}


def _decor_pine(s, rng):
    for _ in range(18):
        x = rng.randint(2, SIZE - 4)
        y = rng.randint(3, SIZE - 3)
        if rng.random() < 0.55:
            pygame.draw.line(s, (112, 86, 48), (x, y), (x + 2, y + 1), 1)
        else:
            pygame.draw.line(s, (58, 76, 46), (x, y), (x + 1, y + 3), 1)
    # шишка
    cx, cy = rng.randint(10, 52), rng.randint(10, 52)
    pygame.draw.ellipse(s, (84, 58, 34), (cx, cy, 6, 8))
    pygame.draw.ellipse(s, (126, 92, 54), (cx + 1, cy + 1, 4, 5))


def _decor_birch(s, rng):
    for _ in range(16):
        x = rng.randint(3, SIZE - 4)
        y = rng.randint(3, SIZE - 4)
        col = (196, 190, 168) if rng.random() < 0.5 else (110, 120, 62)
        pygame.draw.line(s, col, (x, y), (x + rng.randint(1, 3), y + 1), 1)
    for _ in range(4):
        x, y = rng.randint(6, 50), rng.randint(6, 50)
        pygame.draw.ellipse(s, (150, 142, 74), (x, y, 4, 2))


def _decor_rock(s, rng):
    for _ in range(3):
        x, y = rng.randint(4, 48), rng.randint(4, 48)
        w, h = rng.randint(8, 18), rng.randint(6, 12)
        pygame.draw.ellipse(s, (52, 49, 44), (x, y, w, h))
        pygame.draw.ellipse(s, (92, 88, 80), (x + 2, y + 1, w - 5, h - 4))
    for _ in range(12):
        x, y = rng.randint(2, SIZE - 6), rng.randint(2, SIZE - 6)
        pygame.draw.circle(s, textures.shade(STONE, rng.uniform(0.7, 1.2)),
                           (x, y), rng.choice((1, 1, 2)))
    _crack(s, rng.randint(8, 50), rng.randint(8, 48), 3,
           (44, 42, 38), rng)


def _decor_autumn(s, rng):
    palette = ((176, 92, 34), (198, 132, 40), (150, 54, 30), (120, 80, 28))
    for _ in range(22):
        x, y = rng.randint(2, SIZE - 5), rng.randint(2, SIZE - 5)
        pygame.draw.ellipse(s, rng.choice(palette), (x, y, 4, 3))
    for _ in range(6):
        x, y = rng.randint(6, 50), rng.randint(6, 48)
        pygame.draw.line(s, (116, 72, 34), (x, y), (x + 3, y + 2), 1)


def _decor_swamp(s, rng):
    for _ in range(2):
        x, y = rng.randint(3, 34), rng.randint(4, 40)
        w, h = rng.randint(14, 24), rng.randint(8, 14)
        pygame.draw.ellipse(s, (30, 44, 42), (x, y, w, h))
        pygame.draw.ellipse(s, (46, 66, 60), (x + 2, y + 2, max(4, w - 5),
                                              max(3, h - 5)))
        pygame.draw.arc(s, (96, 118, 104), (x + 4, y + 3, max(3, w - 9),
                                            max(2, h - 8)), 3.4, 5.6, 1)
    for _ in range(9):
        x, y = rng.randint(4, 56), rng.randint(10, 58)
        pygame.draw.line(s, (72, 88, 52), (x, y), (x, y - rng.randint(5, 10)), 1)
        pygame.draw.circle(s, (108, 88, 48), (x, y - rng.randint(5, 9)), 1)


def _decor_dark(s, rng):
    for _ in range(7):
        x, y = rng.randint(6, 52), rng.randint(10, 54)
        for dx in (-2, 0, 2):
            pygame.draw.line(s, (30, 52, 34), (x, y), (x + dx, y - 5), 1)
    for _ in range(3):
        x, y = rng.randint(8, 50), rng.randint(8, 48)
        pygame.draw.circle(s, (128, 104, 160), (x, y), 2)
        pygame.draw.circle(s, (188, 164, 224), (x, y), 1)


def _decor_meadow(s, rng):
    petals = ((226, 214, 130), (206, 120, 150), (150, 160, 220),
              (222, 226, 214), (218, 160, 84))
    for _ in range(12):
        x, y = rng.randint(4, SIZE - 5), rng.randint(8, SIZE - 4)
        pygame.draw.line(s, (58, 86, 42), (x, y), (x, y - 4), 1)
        pygame.draw.circle(s, rng.choice(petals), (x, y - 5), 2)
        pygame.draw.circle(s, (232, 196, 84), (x, y - 5), 1)


def _decor_dead(s, rng):
    for _ in range(6):
        x, y = rng.randint(4, SIZE - 8), rng.randint(6, SIZE - 4)
        ex, ey = x + rng.randint(3, 8), y + rng.randint(-3, 3)
        pygame.draw.line(s, (96, 84, 66), (x, y), (ex, ey), 1)
    for _ in range(3):
        x, y = rng.randint(6, 52), rng.randint(6, 52)
        pygame.draw.line(s, (214, 208, 190), (x, y), (x + 5, y + 1), 1)
        pygame.draw.circle(s, (202, 196, 180), (x + 5, y + 1), 1)
    for _ in range(14):
        x, y = rng.randint(1, SIZE - 2), rng.randint(1, SIZE - 2)
        pygame.draw.circle(s, (86, 80, 68), (x, y), 1)


def _decor_beach(s, rng):
    """Пляж: ракушки, галька и редкие водоросли на песке."""
    shells = ((236, 226, 206), (222, 188, 176), (240, 214, 172))
    for _ in range(7):
        x, y = rng.randint(4, SIZE - 6), rng.randint(4, SIZE - 6)
        pygame.draw.arc(s, rng.choice(shells), (x, y, 5, 4), 0.2, 2.9, 1)
        pygame.draw.line(s, (168, 148, 118), (x + 2, y + 3), (x + 2, y + 4), 1)
    for _ in range(10):
        x, y = rng.randint(2, SIZE - 4), rng.randint(2, SIZE - 4)
        pygame.draw.circle(s, rng.choice(((186, 172, 142), (150, 140, 120),
                                         (208, 196, 168))), (x, y), 1)
    for _ in range(2):
        x, y = rng.randint(6, 48), rng.randint(10, 50)
        pygame.draw.line(s, (110, 128, 92), (x, y), (x + rng.randint(3, 7), y - 2), 1)


BIOME_DECOR = {
    'pine': _decor_pine, 'birch': _decor_birch, 'rock': _decor_rock,
    'autumn': _decor_autumn, 'swamp': _decor_swamp, 'dark': _decor_dark,
    'meadow': _decor_meadow, 'dead': _decor_dead, 'beach': _decor_beach,
}


def _biome_grass(name, variant):
    s = _grass(variant, BIOME_PALETTES[name])
    rng = textures.rng_for(variant, 89, seed=31)
    BIOME_DECOR[name](s, rng)
    _grime(s, rng, 60, 7)
    return s


def _pine_tree(variant, name='pine'):
    palette = BIOME_PALETTES[name]
    s = _biome_grass(name, variant + 2)
    rng = textures.rng_for(variant, 93, seed=47)
    trunk = palette['trunk']
    dark = palette['tree_dark']
    top = palette['tree']
    pygame.draw.ellipse(s, (0, 0, 0, 90), (8, 46, 48, 14))
    pygame.draw.rect(s, textures.shade(trunk, 0.6), (29, 44, 7, 14))
    pygame.draw.rect(s, trunk, (30, 44, 5, 13))
    # три яруса хвои
    for i, (cy, half) in enumerate(((44, 22), (33, 18), (21, 14))):
        shade = textures.shade(top, 0.86 + i * 0.09)
        pts = [(32, cy - half), (32 - half, cy + 7), (32 + half, cy + 7)]
        pygame.draw.polygon(s, textures.shade(dark, 0.9), pts)
        inner = [(32, cy - half + 4), (32 - half + 5, cy + 5),
                 (32 + half - 5, cy + 5)]
        pygame.draw.polygon(s, shade, inner)
        pygame.draw.line(s, textures.shade(top, 1.3),
                         (32, cy - half + 4), (32 - half + 5, cy + 4), 1)
    for _ in range(10):
        x, y = rng.randint(18, 46), rng.randint(8, 46)
        pygame.draw.line(s, textures.shade(dark, 0.7), (x, y), (x - 2, y + 2), 1)
    return s


def _birch_tree(variant):
    palette = BIOME_PALETTES['birch']
    s = _biome_grass('birch', variant + 2)
    rng = textures.rng_for(variant, 95, seed=53)
    # ствол с чёрными штрихами
    pygame.draw.rect(s, (188, 184, 168), (28, 22, 8, 36))
    pygame.draw.rect(s, (220, 216, 202), (29, 22, 3, 36))
    for _ in range(7):
        y = rng.randint(24, 54)
        pygame.draw.line(s, (52, 50, 46), (28, y), (29 + rng.randint(1, 6), y), 1)
    crown = pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)
    lobes = ((24, 22, 14), (40, 22, 15), (32, 13, 15),
             (16, 32, 10), (48, 32, 10))
    for cx, cy, r in lobes:
        pygame.draw.circle(crown, palette['tree_dark'], (cx, cy + 3), r)
    for cx, cy, r in lobes:
        pygame.draw.circle(crown, palette['tree'], (cx, cy), max(3, r - 2))
    for _ in range(26):
        x, y = rng.randint(10, 54), rng.randint(4, 40)
        if crown.get_at((x, y))[3] < 40:
            continue
        pygame.draw.circle(crown, textures.shade(palette['tree'],
                                                 rng.uniform(0.75, 1.2)),
                           (x, y), 1)
    s.blit(textures.silhouette(crown, palette['tree_dark'], 1), (0, 0))
    s.blit(crown, (0, 0))
    return s


def _rock_cluster(variant):
    s = _biome_grass('rock', variant + 2)
    rng = textures.rng_for(variant, 97, seed=59)
    pygame.draw.ellipse(s, (0, 0, 0, 90), (6, 44, 52, 14))
    for cx, cy, w, h in ((24, 40, 30, 20), (36, 30, 20, 16), (20, 28, 16, 12)):
        pygame.draw.ellipse(s, (54, 52, 48), (cx - w // 2, cy - h // 2, w, h))
        pygame.draw.ellipse(s, (96, 92, 84),
                            (cx - w // 2 + 2, cy - h // 2 + 1, w - 5, h - 5))
        pygame.draw.arc(s, (140, 134, 122),
                        (cx - w // 2 + 4, cy - h // 2 + 2, w - 9, h - 8),
                        1.9, 4.6, 2)
    for _ in range(10):
        x, y = rng.randint(8, 56), rng.randint(8, 56)
        pygame.draw.circle(s, textures.shade(STONE, rng.uniform(0.7, 1.2)),
                           (x, y), 1)
    return s


def _bare_tree(variant):
    palette = BIOME_PALETTES['dead']
    s = _biome_grass('dead', variant + 2)
    rng = textures.rng_for(variant, 99, seed=61)
    trunk = palette['trunk']
    pygame.draw.ellipse(s, (0, 0, 0, 80), (10, 48, 44, 12))
    pygame.draw.polygon(s, textures.shade(trunk, 0.55), [
        (28, 56), (30, 26), (35, 26), (38, 56)])
    pygame.draw.rect(s, trunk, (30, 24, 5, 32))
    for _ in range(8):
        x, y = 32 + rng.randint(-2, 2), rng.randint(14, 40)
        dx = rng.choice((-1, 1)) * rng.randint(6, 16)
        dy = -rng.randint(3, 10)
        pygame.draw.line(s, textures.shade(trunk, 0.8), (x, y),
                         (x + dx, y + dy), 2)
        pygame.draw.line(s, textures.shade(trunk, 1.1), (x + dx, y + dy),
                         (x + dx + rng.choice((-1, 1)) * 5, y + dy - 4), 1)
    return s


def _autumn_tree(variant):
    palette = BIOME_PALETTES['autumn']
    s = _tree(variant + 2, palette)
    rng = textures.rng_for(variant, 101, seed=67)
    for _ in range(12):
        x, y = rng.randint(14, 50), rng.randint(10, 44)
        if s.get_at((x, y))[3] > 60:
            pygame.draw.circle(s, rng.choice(((196, 122, 42), (170, 76, 32),
                                              (210, 164, 58))), (x, y), 2)
    return s


def _swamp_tree(variant):
    palette = BIOME_PALETTES['swamp']
    s = _tree(variant + 2, palette)
    rng = textures.rng_for(variant, 103, seed=71)
    for _ in range(6):
        x = rng.randint(14, 50)
        y = rng.randint(22, 48)
        pygame.draw.line(s, (96, 110, 82), (x, y), (x, y + rng.randint(4, 10)), 1)
    for _ in range(2):
        x, y = rng.randint(10, 50), rng.randint(44, 56)
        pygame.draw.ellipse(s, (44, 58, 50), (x, y, 10, 5))
    return s


def _dark_tree(variant):
    s = _pine_tree(variant + 1, 'dark')
    rng = textures.rng_for(variant, 105, seed=73)
    for _ in range(8):
        x, y = rng.randint(16, 48), rng.randint(12, 44)
        pygame.draw.line(s, (20, 34, 26), (x, y), (x - 2, y + 2), 1)
    return s


def _meadow_tree(variant):
    palette = BIOME_PALETTES['meadow']
    s = _tree(variant + 2, palette)
    rng = textures.rng_for(variant, 107, seed=79)
    for _ in range(10):
        x, y = rng.randint(4, 58), rng.randint(44, 58)
        pygame.draw.circle(s, rng.choice(((226, 214, 130), (206, 120, 150),
                                          (150, 160, 220))), (x, y), 2)
    return s


BIOME_GRASS_BUILDERS = {
    name: (lambda variant, n=name: _biome_grass(n, variant))
    for name in BIOME_PALETTES
}

BIOME_TREE_BUILDERS = {
    'pine': _pine_tree,
    'birch': _birch_tree,
    'rock': _rock_cluster,
    'autumn': _autumn_tree,
    'swamp': _swamp_tree,
    'dark': _dark_tree,
    'meadow': _meadow_tree,
    'dead': _bare_tree,
    'beach': lambda variant: _tree(variant, BIOME_PALETTES['beach']),
}



def _cave_entrance(variant=0):
    s = _dungeon_ground(1)
    rng = textures.rng_for(variant, 8, seed=97)
    # каменистый холм
    pygame.draw.ellipse(s, STONE_DARK, (3, 25, 58, 35))
    pygame.draw.ellipse(s, textures.shade(STONE, 0.82), (7, 20, 50, 32))
    pygame.draw.ellipse(s, textures.shade(STONE, 1.05), (13, 22, 24, 13))
    # слои породы
    for i in range(4):
        pygame.draw.arc(s, textures.shade(STONE_DARK, 0.9 + i * 0.1),
                        (6 + i * 3, 22 + i * 5, 52 - i * 6, 24 - i * 3),
                        math.pi, math.pi * 1.9, 1)
    # вход
    pygame.draw.ellipse(s, (9, 8, 7), (17, 29, 30, 28))
    pygame.draw.ellipse(s, (16, 14, 12), (20, 32, 24, 22))
    for i in range(3):
        y = 36 + i * 6
        pygame.draw.rect(s, textures.shade((92, 86, 74), 1.0 - i * 0.22),
                         (22 + i, y, 20 - i * 2, 3))
    # янтарный маркер вниз
    pygame.draw.polygon(s, textures.shade(BRONZE, 0.45), [
        (32, 61), (25, 49), (39, 49)])
    pygame.draw.polygon(s, BRONZE, [
        (32, 57), (27, 49), (37, 49)])
    _grime(s, rng, 120, 8)
    return s


# ════════════════════════ ДОРОГИ И ДЕРЕВНЯ ════════════════════════
def _road(variant):
    """Утоптанная дорога: земля, колеи, камешки и редкая трава."""
    s = _new()
    base = (72, 62, 47)
    s.fill(base)
    rng = textures.rng_for(variant, 21, seed=101)
    textures.speckles(s, (0, 0, SIZE, SIZE), base, rng, 340, 0.18)

    # две колеи вдоль тайла
    for cx in (20, 44):
        pygame.draw.line(s, textures.shade(base, 0.72), (cx, 0), (cx, SIZE), 3)
        pygame.draw.line(s, textures.shade(base, 0.86), (cx - 2, 0),
                         (cx - 2, SIZE), 1)
    # камешки
    for _ in range(9):
        x = rng.randint(4, SIZE - 5)
        y = rng.randint(4, SIZE - 5)
        r = rng.choice((1, 1, 2))
        pygame.draw.circle(s, textures.shade(STONE, 1.05), (x, y), r)
        pygame.draw.circle(s, textures.shade(STONE_DARK, 0.85), (x + 1, y + 1), r)
    # трава по краям
    for _ in range(10):
        x = rng.choice((rng.randint(1, 7), rng.randint(SIZE - 8, SIZE - 2)))
        y = rng.randint(4, SIZE - 4)
        pygame.draw.line(s, textures.shade(GRASS_BASE, 0.92),
                         (x, y), (x, y - rng.randint(2, 5)))
    _grime(s, rng, 180, 9)
    return s


