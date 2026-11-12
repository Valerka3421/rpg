"""Процедурные тайлы 64×64 в духе Stoneshard.

Палитра намеренно тяжёлая и приглушённая: тёплый серо-коричневый камень,
почти чёрный раствор, оливковый мох и янтарный огонь. Холодные акценты
оставлены только теням — так сцена читается как сырое подземелье, а не
как разноцветная аркада.

Тайл строится один раз на вариант, кэшируется и только блитится, поэтому
вся детализация (кладка, сколы, подтёки, гравий, лужи) бесплатна в кадре.
"""
import math

import pygame

from render import textures

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
        'grass': (46, 58, 39), 'grass_dark': (28, 39, 26),
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
}

VILLAGE_PALETTE = {
    'grass': (66, 78, 46), 'grass_dark': (44, 56, 32),
    'grass_dry': (116, 108, 62),
    'tree': (60, 78, 46), 'tree_dark': (34, 50, 30),
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
    s = _grass(variant + 2, palette)
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


# ════════════════════════ КЭШ ════════════════════════
_BUILDERS = {
    'wall': _wall,
    'wall_moss': _wall_moss,
    'wall_torch': _wall_torch,
    'floor': _floor,
    'stairs': _stairs_down,
    'upstairs': _stairs_up,
    'exit': _exit_daylight,
    'grass': _grass,
    'tree': _tree,
    'cave': _cave_entrance,
}

# сколько вариантов каждого вида (для выбора по координате)
_VARIANTS = {
    'wall': 3, 'wall_moss': 1, 'wall_torch': 1,
    'floor': 12, 'grass': 8, 'tree': 3,
    'stairs': 1, 'upstairs': 1, 'exit': 1, 'cave': 1,
    'road': 4, 'village_grass': 8, 'village_path': 4,
    'village_plaza': 4, 'village_wall': 3, 'village_well': 1,
    'dungeon_ground': 6, 'rock': 3,
}


def _register_biome_tiles():
    """Добавить в кэш по паре тайлов на каждый из восьми биомов."""
    for name, palette in BIOME_PALETTES.items():
        _BUILDERS[f'grass_{name}'] = (
            lambda variant, p=palette: _grass(variant, p))
        _BUILDERS[f'tree_{name}'] = (
            lambda variant, p=palette: _tree(variant, p))
        _VARIANTS[f'grass_{name}'] = 6
        _VARIANTS[f'tree_{name}'] = 3


_BUILDERS.update({
    'road': _road,
    'village_grass': _village_grass,
    'village_path': _village_path,
    'village_plaza': _village_plaza,
    'village_wall': _village_wall,
    'village_well': _village_well,
    'dungeon_ground': _dungeon_ground,
    'rock': _rock,
})
_register_biome_tiles()


class Tiles:
    """Кэш готовых тайлов: вид + вариант + уровень освещения."""

    _cache = {}
    _glow = None

    @classmethod
    def clear(cls):
        cls._cache.clear()
        cls._glow = None

    @classmethod
    def prewarm(cls):
        """Собрать все варианты заранее: первый кадр этажа не должен
        подтормаживать на генерации тайлов."""
        for kind, count in _VARIANTS.items():
            for variant in range(count):
                cls.get(kind, variant, 1.0)
                cls.get(kind, variant, 0.72)

    @classmethod
    def variant(cls, kind, x, y):
        count = _VARIANTS.get(kind, 1)
        if kind == 'wall':
            # редкие особые стены: факел и мох
            roll = textures.hash01(x, y, seed=41)
            if roll < 0.07:
                return 1, 'wall_torch'
            if roll < 0.16:
                return 0, 'wall_moss'
            return textures.hash_int(x, y, 41, count), 'wall'
        return textures.hash_int(x, y, 13, count), kind

    @classmethod
    def get(cls, kind, variant=0, light=1.0):
        """Тайл с запечённым освещением (квантован, чтобы кэш не пух)."""
        band = int(round(max(0.3, min(1.0, light)) * 20))
        v = variant % _VARIANTS.get(kind, 1)      # ← один v для ключа и builder
        key = (kind, v, band)
        surf = cls._cache.get(key)
        if surf is not None:
            return surf
        builder = _BUILDERS.get(kind)
        if builder is None:
            builder = _floor
        base = builder(v)
        if band < 20:
            base = textures.apply_light(base, band / 20.0)
        if len(cls._cache) >= 900:
            cls._cache.clear()
        cls._cache[key] = base
        return base

    @classmethod
    def torch_glow(cls):
        """Тёплый ореол факела: аддитивная добавка, без «выжженного» диска."""
        if cls._glow is None:
            size = SIZE * 2
            glow = pygame.Surface((size, size))
            glow.fill((0, 0, 0))
            center = size // 2
            for radius in range(center, 0, -1):
                t = radius / center
                k = int(88 * (1 - t) ** 2)
                if k <= 0:
                    continue
                pygame.draw.circle(glow, (k, int(k * 0.58), int(k * 0.25)),
                                   (center, center), radius)
            cls._glow = glow
        return cls._glow


_BUILDERS.update({
    'village_stall': _village_stall, 'village_statue': _village_statue,
    'village_torch_post': _village_torch_post,
    'dungeon_ruins': _dungeon_ruins_floor, 'ruins_pillar': _ruins_pillar,
})
_VARIANTS.update({
    'village_stall': 2, 'village_statue': 1, 'village_torch_post': 1,
    'dungeon_ruins': 4, 'ruins_pillar': 2,
})