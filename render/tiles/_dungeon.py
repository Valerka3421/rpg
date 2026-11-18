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

from render.tiles._dungeon_ruins import (
    _dungeon_ruins_floor, _ruins_pillar, _dungeon_ground,
)

from render.tiles._mine import (
    _mine_wall, _mine_ground, _locked_door,
)


# ════════════════════════ КЛАДКА СТЕН ════════════════════════
def _wall_blocks(s, variant, base_color=WALL_BASE, mortar=MORTAR):
    s.fill(mortar)
    rng = textures.rng_for(variant, 2, seed=31)
    # 4 ряда невысоких блоков со смещением — кладка читается как кладка,
    # а не как 2 «шоколадки» на тайл.
    for row in range(4):
        y = row * 16
        offset = 0 if row % 2 == 0 else 16
        for col in range(-1, 3):
            x = col * 32 + offset
            rect = pygame.Rect(x + 1, y + 1, 30, 13)
            tone = (0.76 + textures.hash01(x, y, variant) * 0.34)
            block = textures.shade(base_color, tone)
            textures.bevel_rect(s, rect, block, width=1,
                                light=textures.shade(block, 1.24),
                                dark=textures.shade(block, 0.52))
            pygame.draw.line(s, textures.shade(block, 1.10),
                             (rect.x + 2, rect.y + 1),
                             (rect.right - 3, rect.y + 1))
            textures.speckles(s, rect.inflate(-6, -4), block, rng, 18, 0.13)
            textures.grain(s, rect.inflate(-5, -3), textures.shade(block, 0.80),
                           6, rng)

            # сколы
            if textures.hash01(x, y, variant + 7) < 0.42:
                _chip(s, rect, mortar, rng)
            if textures.hash01(x, y, variant + 17) < 0.20:
                _chip(s, rect, textures.shade(base_color, 0.62), rng)

            # трещина по блоку
            if textures.hash01(x, y, variant + 13) < 0.20:
                _crack(s, x + rng.randint(6, 20), y + 3,
                       rng.randint(1, 2), textures.shade(block, 0.38), rng,
                       drift=2)

            # сырой подтёк сверху
            if textures.hash01(x, y, variant + 29) < 0.26:
                sx = x + rng.randint(4, 26)
                wet = textures.mix(block, mortar, 0.55)
                pygame.draw.line(s, wet, (sx, y + 1),
                                 (sx + rng.choice((-1, 0, 1)), y + 1 +
                                  rng.randint(6, 14)), 1)

    # объём всего тайла: свет сверху, провал снизу
    pygame.draw.rect(s, textures.shade(base_color, 1.30), (0, 0, SIZE, 2))
    pygame.draw.rect(s, textures.shade(base_color, 1.08), (0, 2, SIZE, 1))
    pygame.draw.rect(s, textures.shade(base_color, 0.40), (0, SIZE - 4, SIZE, 4))
    pygame.draw.line(s, textures.shade(base_color, 0.52), (0, SIZE - 5),
                     (SIZE, SIZE - 5))
    _grime(s, rng)


def _wall(variant):
    s = _new()
    _wall_blocks(s, variant)
    return s


def _wall_moss(variant):
    s = _new()
    _wall_blocks(s, variant + 5)
    rng = textures.rng_for(variant, 5, seed=57)
    # мох растёт из швов, а не кругами поверх кладки
    for _ in range(7):
        x = rng.randint(2, SIZE - 10)
        y = rng.choice((1, 29, 31, 60)) + rng.randint(-1, 1)
        w = rng.randint(6, 18)
        h = rng.randint(2, 5)
        pygame.draw.ellipse(s, MOSS_DARK, (x, y, w, h))
        pygame.draw.ellipse(s, MOSS, (x + 1, y, max(2, w - 4),
                                      max(2, h - 2)))
    for _ in range(10):                      # мелкие пятна
        x = rng.randint(3, SIZE - 4)
        y = rng.randint(3, SIZE - 4)
        pygame.draw.circle(s, textures.shade(MOSS, rng.uniform(0.7, 1.1)),
                           (x, y), rng.choice((1, 1, 2)))
    _grime(s, rng, 160, 10)
    return s


def _wall_torch(variant):
    s = _new()
    _wall_blocks(s, variant + 9)
    cx = 32

    # копоть вокруг факела
    soot = pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)
    for radius in range(20, 0, -2):
        alpha = int(70 * (1 - radius / 20) ** 2)
        pygame.draw.circle(soot, (8, 6, 5, alpha), (cx, 14), radius)
    s.blit(soot, (0, 0))

    # железный кронштейн
    pygame.draw.polygon(s, (44, 40, 36), [
        (cx - 7, 40), (cx + 7, 40), (cx + 4, 47), (cx - 4, 47)])
    pygame.draw.rect(s, (58, 52, 45), (cx - 2, 32, 4, 11))
    pygame.draw.circle(s, (34, 31, 28), (cx, 41), 3)
    pygame.draw.circle(s, (96, 82, 60), (cx, 40), 2)

    # древко
    pygame.draw.rect(s, (82, 59, 38), (cx - 2, 20, 4, 14))
    pygame.draw.rect(s, (112, 82, 50), (cx - 2, 20, 1, 14))

    # пламя: тёмный контур, оранжевый, светлый сердечник
    pygame.draw.polygon(s, (36, 18, 8), [
        (cx, 0), (cx + 8, 14), (cx + 4, 20), (cx - 4, 20), (cx - 8, 14)])
    pygame.draw.polygon(s, (198, 92, 34), [
        (cx, 2), (cx + 6, 14), (cx + 3, 19), (cx - 3, 19), (cx - 6, 14)])
    pygame.draw.polygon(s, (240, 150, 62), [
        (cx, 6), (cx + 4, 14), (cx + 2, 18), (cx - 2, 18), (cx - 4, 14)])
    pygame.draw.polygon(s, (255, 226, 160), [
        (cx, 10), (cx + 2, 14), (cx, 17), (cx - 2, 14)])
    pygame.draw.circle(s, (255, 200, 110), (cx + 5, 7), 1)
    pygame.draw.circle(s, (255, 170, 80), (cx - 6, 11), 1)
    return s


# ════════════════════════ ПОЛ ════════════════════════
def _floor(variant):
    s = _new()
    s.fill(FLOOR_GROUT)
    rng = textures.rng_for(variant, 9, seed=73)

    for gy in range(2):
        for gx in range(2):
            rect = pygame.Rect(gx * 32 + 1, gy * 32 + 1, 30, 30)
            tone = 0.80 + textures.hash01(gx, gy, variant) * 0.34
            stone = textures.shade(FLOOR_BASE, tone)
            textures.bevel_rect(s, rect, stone, width=2,
                                light=textures.shade(stone, 1.28),
                                dark=textures.shade(stone, 0.48))
            textures.speckles(s, rect.inflate(-6, -6), stone, rng, 28, 0.14)
            textures.grain(s, rect.inflate(-8, -8), textures.shade(stone, 0.82),
                           8, rng)
            if textures.hash01(gx, gy, variant + 3) < 0.48:
                _chip(s, rect, FLOOR_GROUT, rng)

    def crack():
        _crack(s, rng.randint(8, 52), rng.randint(6, 14), 3,
               textures.shade(FLOOR_BASE, 0.40), rng)

    def pebbles():
        for _ in range(4):
            x = rng.randint(8, 52)
            y = rng.randint(8, 52)
            r = rng.randint(1, 2)
            pygame.draw.circle(s, textures.shade(FLOOR_BASE, 1.32), (x, y), r)
            pygame.draw.circle(s, textures.shade(FLOOR_BASE, 0.50),
                               (x + 1, y + 1), r, 1)

    def moss():
        # мох в швах между плитами
        for _ in range(4):
            x = rng.randint(2, 48)
            y = rng.choice((0, 30, 31, 60)) + rng.randint(-1, 1)
            pygame.draw.ellipse(s, MOSS_DARK, (x, y, rng.randint(8, 16), 3))
            pygame.draw.ellipse(s, MOSS, (x + 1, y, rng.randint(5, 11), 2))

    def puddle():
        # стоячая вода: тёмная, мутная, с холодным бликом
        pygame.draw.ellipse(s, (24, 28, 30), (12, 36, 40, 16))
        pygame.draw.ellipse(s, (37, 44, 47), (18, 39, 22, 7))
        pygame.draw.line(s, (58, 68, 72), (22, 41), (34, 41), 1)

    def stain():
        # тёмное пятно (кровь/масло) с рваным краем
        for _ in range(5):
            pygame.draw.circle(s, (44, 34, 27),
                               (rng.randint(18, 46), rng.randint(18, 46)),
                               rng.randint(3, 7))
        crack()

    def gravel():
        for _ in range(12):
            x = rng.randint(4, 58)
            y = rng.randint(4, 58)
            c = textures.shade(FLOOR_BASE, rng.uniform(0.55, 1.25))
            pygame.draw.rect(s, c, (x, y, rng.choice((1, 2)), 1))
        if textures.hash01(variant, 5, 77) < 0.5:
            pygame.draw.circle(s, BONE, (rng.randint(12, 50),
                                         rng.randint(12, 50)), 2)
            pygame.draw.circle(s, textures.shade(BONE, 0.6),
                               (rng.randint(12, 50), rng.randint(12, 50)),
                               1)

    # 12 вариантов: 6 обычных плит, 2 трещины, 2 камешка, мох, лужа
    decor = {6: crack, 7: pebbles, 8: moss, 9: puddle,
             10: stain, 11: gravel}
    fn = decor.get(variant % 12)
    if fn:
        fn()
    _grime(s, rng, 220, 11)
    return s


# ════════════════════════ ЛЕСТНИЦЫ ════════════════════════
def _stairs_down(variant=0):
    s = _floor(0)
    rng = textures.rng_for(variant, 11, seed=83)
    # каменная рама
    pygame.draw.rect(s, textures.shade(WALL_BASE, 0.55), (2, 2, 60, 60))
    textures.bevel_rect(s, pygame.Rect(3, 3, 58, 58),
                        textures.shade(WALL_BASE, 0.72), width=2)
    _grime(s, rng, 80, 8)

    # провал
    pygame.draw.rect(s, (8, 7, 6), (8, 8, 48, 50))
    # ступени уходят вниз: сверху узкие и светлые, снизу шире и темнее
    for i in range(4):
        y = 10 + i * 11
        inset = 4 + i * 2
        col = textures.shade((104, 96, 82), 0.95 - i * 0.19)
        pygame.draw.rect(s, col, (8 + inset, y, 48 - inset * 2, 9))
        pygame.draw.line(s, textures.shade(col, 1.30),
                         (8 + inset, y), (56 - inset, y))
        pygame.draw.line(s, textures.shade(col, 0.55),
                         (8 + inset, y + 8), (56 - inset, y + 8))
    # дно
    pygame.draw.rect(s, (4, 4, 4), (16, 54, 32, 4))

    # янтарный маркер + слабое свечение
    glow = pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)
    pygame.draw.ellipse(glow, (210, 150, 60, 55), (18, 42, 28, 20))
    s.blit(glow, (0, 0))
    pygame.draw.polygon(s, textures.shade(BRONZE, 0.45), [
        (32, 60), (25, 48), (39, 48)])
    pygame.draw.polygon(s, BRONZE, [
        (32, 56), (27, 48), (37, 48)])
    return s


def _stairs_up(variant=0):
    s = _floor(2)
    rng = textures.rng_for(variant, 12, seed=87)
    pygame.draw.rect(s, textures.shade(WALL_BASE, 0.55), (2, 2, 60, 60))
    textures.bevel_rect(s, pygame.Rect(3, 3, 58, 58),
                        textures.shade(WALL_BASE, 0.72), width=2)
    pygame.draw.rect(s, (8, 7, 6), (8, 8, 48, 52))
    _grime(s, rng, 70, 8)

    # ступени, ведущие вверх, к тёплому свету
    for i in range(4):
        y = 46 - i * 9
        inset = 2 + i * 2
        col = textures.shade((110, 102, 88), 0.55 + i * 0.16)
        pygame.draw.rect(s, col, (10 + inset, y, 44 - inset * 2, 7))
        pygame.draw.line(s, textures.shade(col, 1.28),
                         (10 + inset, y), (54 - inset, y))
        pygame.draw.line(s, textures.shade(col, 0.52),
                         (10 + inset, y + 6), (54 - inset, y + 6))

    # холодный свет сверху
    glow = pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)
    pygame.draw.ellipse(glow, (176, 180, 168, 46), (16, 2, 32, 18))
    s.blit(glow, (0, 0))
    pygame.draw.polygon(s, textures.shade(BRONZE, 0.45), [
        (32, 12), (25, 22), (39, 22)])
    pygame.draw.polygon(s, BRONZE, [
        (32, 15), (27, 22), (37, 22)])
    return s


def _exit_daylight(variant=0):
    """Выход на поверхность: каменная арка, из которой льётся свет."""
    s = _new()
    _wall_blocks(s, 4)
    cx, top, bottom, half_w = 32, 8, 60, 18

    # маска арки
    mask = pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)
    pygame.draw.rect(mask, (255, 255, 255, 255),
                     (cx - half_w, top + half_w, half_w * 2,
                      bottom - top - half_w))
    pygame.draw.circle(mask, (255, 255, 255, 255), (cx, top + half_w), half_w)

    # проём: тёмная ниша
    niche = pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)
    niche.fill((12, 11, 10, 255))
    niche.blit(mask, (0, 0), special_flags=pygame.BLEND_RGBA_MULT)
    s.blit(niche, (0, 0))

    # свет: пасмурный день, книзу уходит в тёплую пыль
    light = pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)
    for y in range(top, bottom):
        t = (y - top) / max(1, bottom - top)
        col = textures.mix((214, 210, 190), (150, 124, 88), t ** 0.75)
        pygame.draw.line(light, col, (cx - half_w, y), (cx + half_w + 1, y))
    light.blit(mask, (0, 0), special_flags=pygame.BLEND_RGBA_MULT)
    s.blit(light, (0, 0))

    # ступени, уходящие вверх к свету
    for i in range(3):
        y = bottom - 4 - i * 5
        width = half_w * 2 - 4 - i * 4
        col = textures.shade((156, 146, 124), 1.0 - i * 0.10)
        pygame.draw.rect(s, col, (cx - width // 2, y, width, 3))
        pygame.draw.line(s, textures.shade(col, 1.22),
                         (cx - width // 2, y), (cx + width // 2, y))

    # наличник арки
    frame = textures.shade(WALL_BASE, 1.12)
    pygame.draw.arc(s, frame, (cx - half_w - 2, top - 2,
                               half_w * 2 + 4, half_w * 2 + 4),
                    0, math.pi, 2)
    pygame.draw.line(s, frame, (cx - half_w - 2, top + half_w),
                     (cx - half_w - 2, bottom - 2), 2)
    pygame.draw.line(s, frame, (cx + half_w + 2, top + half_w),
                     (cx + half_w + 2, bottom - 2), 2)

    # тёплый ореол вокруг проёма
    halo = pygame.Surface((SIZE, SIZE))
    halo.fill((0, 0, 0))
    for radius in range(half_w + 14, 0, -1):
        t = radius / (half_w + 14)
        k = int(24 * (1 - t) ** 2)
        if k <= 0:
            continue
        pygame.draw.circle(halo, (k, int(k * 0.72), int(k * 0.38)),
                           (cx, top + half_w + 6), radius)
    s.blit(halo, (0, 0), special_flags=pygame.BLEND_RGB_ADD)

    # трава на пороге и янтарная стрелка «наверх»
    rng = textures.rng_for(3, 3, seed=91)
    for _ in range(6):
        x = rng.randint(cx - half_w + 2, cx + half_w - 2)
        y = rng.randint(bottom - 2, bottom + 2)
        pygame.draw.line(s, textures.shade(GRASS_BASE, 1.15),
                         (x, y), (x, y - rng.randint(2, 4)))
    pygame.draw.polygon(s, textures.shade(BRONZE, 0.45), [
        (cx, bottom + 4), (cx - 6, bottom - 5), (cx + 6, bottom - 5)])
    pygame.draw.polygon(s, BRONZE, [
        (cx, bottom + 1), (cx - 5, bottom - 5), (cx + 5, bottom - 5)])
    return s


