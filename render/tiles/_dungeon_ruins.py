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


# ════════════════════════ РУИНЫ ════════════════════════
def _dungeon_ruins_floor(variant):
    """Древний каменный пол с рунами."""
    s = _new()
    base = (52, 48, 44)
    s.fill(base)
    rng = textures.rng_for(variant, 51, seed=151)
    for gy in range(2):
        for gx in range(2):
            rect = pygame.Rect(gx * 32 + 1, gy * 32 + 1, 30, 30)
            tone = 0.78 + textures.hash01(gx, gy, variant) * 0.28
            stone = textures.shade(base, tone)
            textures.bevel_rect(s, rect, stone, width=2,
                                light=textures.shade(stone, 1.32),
                                dark=textures.shade(stone, 0.45))
            if textures.hash01(gx, gy, variant + 5) < 0.4:
                _crack(s, rng.randint(8, 52), rng.randint(6, 14), 2,
                       textures.shade(base, 0.35), rng)
    if variant % 4 == 0:                     # руна на 1 из 4
        cx, cy = 32, 32
        pygame.draw.circle(s, (100, 160, 220, 180), (cx, cy), 8, 2)
        pygame.draw.line(s, (100, 160, 220, 180), (cx - 8, cy), (cx + 8, cy), 2)
        pygame.draw.line(s, (100, 160, 220, 180), (cx, cy - 8), (cx, cy + 8), 2)
    _grime(s, rng, 180, 10)
    return s


def _ruins_pillar(variant):
    """Сломанная колонна."""
    s = _dungeon_ruins_floor(variant + 1)
    rng = textures.rng_for(variant, 53, seed=157)
    pygame.draw.rect(s, (90, 84, 76), (16, 50, 32, 12))
    pygame.draw.rect(s, (60, 54, 48), (16, 50, 32, 12), 2)
    for i in range(6):
        y = 46 - i * 6
        inset = 2 + (i // 2)
        tone = 0.85 + (i % 2) * 0.1
        pygame.draw.rect(s, textures.shade((110, 104, 96), tone),
                         (20 + inset, y, 24 - inset * 2, 6))
        pygame.draw.line(s, textures.shade((110, 104, 96), tone * 1.3),
                         (20 + inset, y), (44 - inset, y), 1)
    pygame.draw.polygon(s, (130, 124, 116),
                        [(22, 10), (40, 12), (38, 18), (24, 16)])
    for _ in range(4):
        pygame.draw.ellipse(s, MOSS_DARK,
                            (rng.randint(20, 44), rng.randint(20, 46),
                             rng.randint(3, 7), 2))
    _grime(s, rng, 60, 8)
    return s

def _dungeon_ground(variant):
    """Каменистая земля вокруг входа в подземелье — не лес, а порода."""
    s = _new()
    base = (58, 54, 48)
    s.fill(base)
    rng = textures.rng_for(variant, 29, seed=163)
    textures.speckles(s, (0, 0, SIZE, SIZE), base, rng, 460, 0.15)

    # тёмные провалы и светлые сколы
    for _ in range(3):
        x = rng.randint(4, SIZE - 12)
        y = rng.randint(4, SIZE - 12)
        w = rng.randint(12, 26)
        h = rng.randint(8, 18)
        pygame.draw.ellipse(s, (39, 36, 32), (x, y, w, h))
        pygame.draw.ellipse(s, (70, 65, 57), (x + 3, y + 2,
                                              max(4, w - 8), max(3, h - 7)))
    # трещины
    for _ in range(2):
        _crack(s, rng.randint(8, 56), rng.randint(2, 10), 3,
               (33, 31, 28), rng, drift=4)
    # гравий
    for _ in range(14):
        x = rng.randint(2, SIZE - 3)
        y = rng.randint(2, SIZE - 3)
        c = textures.shade(base, rng.uniform(0.55, 1.30))
        pygame.draw.rect(s, c, (x, y, rng.choice((1, 2)), 1))
    # редкие кости
    if variant % 4 == 0:
        bx, by = rng.randint(10, 44), rng.randint(10, 44)
        pygame.draw.line(s, BONE, (bx, by), (bx + 8, by + 2), 2)
        pygame.draw.circle(s, BONE, (bx, by), 2)
        pygame.draw.circle(s, BONE, (bx + 8, by + 2), 2)
    # мох в расщелинах
    if variant % 3 == 0:
        for _ in range(3):
            x = rng.randint(4, SIZE - 16)
            y = rng.randint(4, SIZE - 8)
            pygame.draw.ellipse(s, MOSS_DARK, (x, y, rng.randint(6, 14), 3))
            pygame.draw.ellipse(s, MOSS, (x + 1, y, rng.randint(4, 9), 2))
    _grime(s, rng, 220, 10)
    return s


