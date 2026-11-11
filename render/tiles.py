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
def _grass(variant):
    s = _new()
    s.fill(GRASS_BASE)
    rng = textures.rng_for(variant, 4, seed=17)
    v = variant % 8
    textures.speckles(s, (0, 0, SIZE, SIZE), GRASS_BASE, rng, 420, 0.16)
    # Крупные пятна тона: без них поле из одинаковых тайлов выглядит
    # заливкой. Пятна полупрозрачные и низкочастотные, поэтому швы между
    # клетками не читаются.
    patch = pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)
    for _ in range(2):
        px = rng.randint(-10, SIZE - 20)
        py = rng.randint(-10, SIZE - 20)
        pw = rng.randint(30, 56)
        ph = rng.randint(24, 44)
        tone = (GRASS_DARK if rng.random() < 0.5
                else textures.shade(GRASS_BASE, 1.10))
        pygame.draw.ellipse(patch, (*tone, 42), (px, py, pw, ph))
    s.blit(patch, (0, 0))
    # травинки
    for _ in range(34):
        x = rng.randint(2, SIZE - 3)
        y = rng.randint(4, SIZE - 3)
        h = rng.randint(2, 6)
        col = textures.shade(GRASS_BASE, rng.uniform(0.62, 1.30))
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
            pygame.draw.line(s, textures.shade(GRASS_DRY, 0.9),
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
            pygame.draw.line(s, textures.shade(GRASS_DRY, 1.0),
                             (x, y), (x + 1, y - rng.randint(4, 8)))
            pygame.draw.line(s, textures.shade(GRASS_BASE, 0.78),
                             (x + 2, y), (x + 2, y - rng.randint(3, 6)))

    _grime(s, rng, 200, 10)
    return s


def _tree(variant):
    s = _grass(variant + 2)
    rng = textures.rng_for(variant, 6, seed=43)
    # тень под кроной
    shadow = pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)
    pygame.draw.ellipse(shadow, (0, 0, 0, 110), (4, 44, 56, 16))
    s.blit(shadow, (0, 0))

    # ствол с корнями
    pygame.draw.polygon(s, textures.shade(TRUNK, 0.55), [
        (27, 54), (30, 34), (34, 34), (37, 54)])
    pygame.draw.rect(s, TRUNK, (29, 34, 6, 21))
    pygame.draw.rect(s, textures.shade(TRUNK, 0.62), (33, 34, 2, 21))
    pygame.draw.line(s, textures.shade(TRUNK, 1.22), (29, 36), (29, 54), 1)
    pygame.draw.line(s, textures.shade(TRUNK, 0.45), (26, 55), (30, 51), 2)
    pygame.draw.line(s, textures.shade(TRUNK, 0.45), (38, 55), (34, 51), 2)

    # крона: тёмная олива, свет только сверху-слева
    crown = pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)
    circles = [(24, 27, 14), (40, 25, 15), (32, 16, 16),
               (17, 36, 9), (47, 36, 9)]
    for cx, cy, r in circles:
        pygame.draw.circle(crown, TREE_BASE, (cx, cy), r)
    for cx, cy, r in circles:
        pygame.draw.circle(crown, TREE_DARK, (cx, cy + r // 3), max(3, r - 3))
    for cx, cy, r in circles:
        pygame.draw.circle(crown, textures.shade(TREE_BASE, 1.16),
                           (cx - r // 4, cy - r // 3), max(3, r // 2))
    # листва: мелкие пятна
    for _ in range(30):
        x = rng.randint(10, 54)
        y = rng.randint(6, 46)
        if crown.get_at((x, y))[3] < 40:
            continue
        factor = rng.uniform(0.68, 1.22)
        pygame.draw.circle(crown, textures.shade(TREE_BASE, factor), (x, y), 1)
    s.blit(textures.silhouette(crown, TREE_DARK, 1), (0, 0))
    s.blit(crown, (0, 0))
    return s


def _cave_entrance(variant=0):
    s = _grass(1)
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
}


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
        key = (kind, variant % _VARIANTS.get(kind, 1), band)
        surf = cls._cache.get(key)
        if surf is not None:
            return surf
        builder = _BUILDERS.get(kind)
        if builder is None:
            builder = _floor
        base = builder(variant)
        if band < 20:
            base = textures.apply_light(base, band / 20.0)
        if len(cls._cache) >= 900:          # страховка от разрастания памяти
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
