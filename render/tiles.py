"""Процедурные тайлы 64×64.

Раньше каждый тайл рисовался десятком примитивов прямо в кадре. Теперь
тайл — это Surface с деталями (кладка, плиты, трещины, мох, факелы),
который строится один раз на вариант, кэшируется и только блитится.
Это и красивее, и быстрее.
"""
import math

import pygame

from render import textures

SIZE = 64

# ── Палитра подземелья ──
WALL_BASE = (78, 71, 96)
MORTAR = (26, 23, 36)
FLOOR_BASE = (56, 52, 59)
FLOOR_GROUT = (30, 28, 33)
TORCH_GLOW = (255, 168, 72)
MOSS = (72, 104, 62)

# ── Палитра поверхности ──
GRASS_BASE = (62, 108, 56)
GRASS_DARK = (44, 82, 42)
TREE_BASE = (46, 96, 48)
TRUNK = (86, 62, 38)
STONE = (96, 92, 104)
STONE_DARK = (58, 55, 68)


def _new():
    return pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)


# ════════════════════════ КЛАДКА СТЕН ════════════════════════
def _wall_blocks(s, variant, base_color=WALL_BASE, mortar=MORTAR):
    s.fill(mortar)
    rng = textures.rng_for(variant, 2, seed=31)
    for row in range(2):
        y = row * 32
        offset = 0 if row % 2 == 0 else 16
        for col in range(-1, 3):
            x = col * 32 + offset
            rect = pygame.Rect(x + 2, y + 2, 28, 28)
            shade = 0.80 + textures.hash01(x, y, variant) * 0.36
            block = textures.shade(base_color, shade)
            textures.bevel_rect(s, rect, block, width=2)
            textures.speckles(s, rect.inflate(-8, -8), block, rng, 22, 0.10)
            # выщербленный угол (залит раствором)
            if textures.hash01(x, y, variant + 7) < 0.32:
                corner = rng.choice(((x + 2, y + 2, 1, 1),
                                     (x + 27, y + 2, -1, 1),
                                     (x + 2, y + 27, 1, -1),
                                     (x + 27, y + 27, -1, -1)))
                cx, cy, dx, dy = corner
                size = rng.randint(3, 6)
                pygame.draw.polygon(s, mortar, [
                    (cx, cy), (cx + dx * size, cy), (cx, cy + dy * size)])
            # трещина
            if textures.hash01(x, y, variant + 13) < 0.22:
                cx = x + rng.randint(8, 18)
                cy = y + 4
                pygame.draw.lines(s, textures.shade(block, 0.45), False, [
                    (cx, cy), (cx + 3, cy + 8), (cx - 1, cy + 15),
                    (cx + 3, cy + 22)], 1)
    # объём всего тайла: свет сверху, тень снизу
    pygame.draw.rect(s, textures.shade(base_color, 1.55), (0, 0, SIZE, 2))
    pygame.draw.rect(s, textures.shade(base_color, 1.25), (0, 2, SIZE, 1))
    pygame.draw.rect(s, textures.shade(base_color, 0.45), (0, SIZE - 4, SIZE, 4))


def _wall(variant):
    s = _new()
    _wall_blocks(s, variant)
    return s


def _wall_moss(variant):
    s = _new()
    s.fill(MORTAR)
    _wall_blocks(s, variant + 5)
    rng = textures.rng_for(variant, 5, seed=57)
    for _ in range(3):
        cx = rng.randint(4, SIZE - 8)
        cy = rng.randint(4, SIZE - 8)
        radius = rng.randint(4, 9)
        pygame.draw.circle(s, textures.shade(MOSS, 0.8), (cx, cy), radius)
        pygame.draw.circle(s, MOSS, (cx - 1, cy - 1), max(2, radius - 3))
    textures.grain(s, (0, 0, SIZE, SIZE), textures.shade(MOSS, 0.7), 24, rng)
    return s


def _wall_torch(variant):
    s = _new()
    s.fill(MORTAR)
    _wall_blocks(s, variant + 9)
    cx = 32
    # кронштейн
    pygame.draw.polygon(s, (40, 36, 46), [
        (cx - 6, 38), (cx + 6, 38), (cx + 3, 46), (cx - 3, 46)])
    pygame.draw.rect(s, (58, 52, 66), (cx - 2, 30, 4, 12))
    pygame.draw.circle(s, (74, 66, 82), (cx, 40), 3)
    # древко
    pygame.draw.rect(s, (88, 60, 36), (cx - 2, 18, 4, 14))
    pygame.draw.rect(s, (120, 86, 50), (cx - 2, 18, 1, 14))
    # пламя: тёмный контур, оранжевый, светлый сердечник
    pygame.draw.polygon(s, (40, 20, 12), [
        (cx, 0), (cx + 7, 13), (cx + 4, 19), (cx - 4, 19), (cx - 7, 13)])
    pygame.draw.polygon(s, (222, 90, 30), [
        (cx, 2), (cx + 6, 13), (cx + 3, 18), (cx - 3, 18), (cx - 6, 13)])
    pygame.draw.polygon(s, (255, 150, 52), [
        (cx, 6), (cx + 4, 13), (cx + 2, 17), (cx - 2, 17), (cx - 4, 13)])
    pygame.draw.polygon(s, (255, 236, 164), [
        (cx, 10), (cx + 2, 14), (cx, 17), (cx - 2, 14)])
    # искры
    pygame.draw.circle(s, (255, 210, 120), (cx + 5, 6), 1)
    pygame.draw.circle(s, (255, 180, 90), (cx - 6, 10), 1)
    return s


# ════════════════════════ ПОЛ ════════════════════════
def _floor(variant):
    s = _new()
    s.fill(FLOOR_GROUT)
    rng = textures.rng_for(variant, 9, seed=73)
    for gy in range(2):
        for gx in range(2):
            rect = pygame.Rect(gx * 32 + 1, gy * 32 + 1, 30, 30)
            shade = 0.82 + textures.hash01(gx, gy, variant) * 0.32
            stone = textures.shade(FLOOR_BASE, shade)
            textures.bevel_rect(s, rect, stone, width=2,
                                light=textures.shade(stone, 1.28),
                                dark=textures.shade(stone, 0.55))
            textures.speckles(s, rect.inflate(-6, -6), stone, rng, 24, 0.12)

    def crack():
        x = rng.randint(8, 52)
        y = rng.randint(6, 14)
        pygame.draw.lines(s, textures.shade(FLOOR_BASE, 0.45), False, [
            (x, y), (x + 5, y + 8), (x + 1, y + 16), (x + 6, y + 24)], 1)

    def pebbles():
        for _ in range(4):
            x = rng.randint(8, 52)
            y = rng.randint(8, 52)
            r = rng.randint(1, 2)
            pygame.draw.circle(s, textures.shade(FLOOR_BASE, 1.30), (x, y), r)
            pygame.draw.circle(s, textures.shade(FLOOR_BASE, 0.55),
                               (x + 1, y + 1), r, 1)

    def moss():
        for _ in range(3):
            x = rng.randint(4, 52)
            y = rng.choice((2, 30, 58))
            pygame.draw.ellipse(s, textures.shade(MOSS, 0.8),
                                (x, y, rng.randint(7, 13), 3))

    def puddle():
        pygame.draw.ellipse(s, (28, 36, 52), (10, 34, 44, 20))
        pygame.draw.ellipse(s, (44, 60, 82), (16, 38, 22, 8))

    # 12 вариантов: 6 обычных плит, 2 трещины, 2 камешка, мох, лужа
    decor = {6: crack, 7: pebbles, 8: moss, 9: puddle,
             10: crack, 11: pebbles}
    fn = decor.get(variant % 12)
    if fn:
        fn()
    return s


# ════════════════════════ ЛЕСТНИЦЫ ════════════════════════
def _stairs_down(variant=0):
    s = _floor(0)
    # каменный портал
    pygame.draw.rect(s, textures.shade(WALL_BASE, 0.6), (2, 2, 60, 60))
    pygame.draw.rect(s, textures.shade(WALL_BASE, 1.2), (4, 4, 56, 56), 2)
    # яма
    pygame.draw.rect(s, (10, 9, 16), (8, 8, 48, 50))
    # ступени уходят вниз: сверху узкие и светлые, снизу шире и темнее
    for i in range(4):
        y = 10 + i * 11
        inset = 4 + i * 2
        col = textures.shade((120, 112, 136), 0.95 - i * 0.18)
        pygame.draw.rect(s, col, (8 + inset, y, 48 - inset * 2, 9))
        pygame.draw.line(s, textures.shade(col, 1.35),
                         (8 + inset, y), (56 - inset, y))
    # дно
    pygame.draw.rect(s, (6, 5, 10), (16, 54, 32, 4))
    # маркер
    pygame.draw.polygon(s, (245, 200, 90), [
        (32, 56), (26, 46), (38, 46)])
    return s


def _stairs_up(variant=0):
    s = _floor(2)
    pygame.draw.rect(s, textures.shade(WALL_BASE, 0.6), (2, 2, 60, 60))
    pygame.draw.rect(s, textures.shade(WALL_BASE, 1.2), (4, 4, 56, 56), 2)
    pygame.draw.rect(s, (10, 9, 16), (8, 8, 48, 52))
    # лестница: рельсы и перекладины
    rail = (132, 96, 58)
    rail_hi = (176, 132, 82)
    for x in (20, 44):
        pygame.draw.rect(s, rail, (x - 2, 10, 4, 48))
        pygame.draw.rect(s, rail_hi, (x - 2, 10, 1, 48))
    for i in range(5):
        y = 14 + i * 9
        pygame.draw.rect(s, rail, (18, y, 28, 3))
        pygame.draw.rect(s, rail_hi, (18, y, 28, 1))
    # свет сверху + маркер
    glow = pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)
    pygame.draw.ellipse(glow, (150, 255, 170, 60), (14, 2, 36, 18))
    s.blit(glow, (0, 0))
    pygame.draw.polygon(s, (140, 240, 150), [
        (32, 6), (25, 16), (39, 16)])
    return s


def _exit_daylight(variant=0):
    """Выход на поверхность: каменная арка, из которой льётся дневной свет."""
    s = _new()
    _wall_blocks(s, 4)
    cx, top, bottom, half_w = 32, 8, 60, 18

    # маска арки
    mask = pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)
    pygame.draw.rect(mask, (255, 255, 255, 255),
                     (cx - half_w, top + half_w, half_w * 2,
                      bottom - top - half_w))
    pygame.draw.circle(mask, (255, 255, 255, 255), (cx, top + half_w), half_w)

    # сам проём: тёмная ниша
    niche = pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)
    niche.fill((14, 12, 20, 255))
    niche.blit(mask, (0, 0), special_flags=pygame.BLEND_RGBA_MULT)
    s.blit(niche, (0, 0))

    # свет: сверху почти белый, книзу уходит в тёплую тень
    light = pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)
    for y in range(top, bottom):
        t = (y - top) / max(1, bottom - top)
        col = textures.mix((255, 253, 236), (178, 138, 82), t ** 0.75)
        pygame.draw.line(light, col, (cx - half_w, y), (cx + half_w + 1, y))
    light.blit(mask, (0, 0), special_flags=pygame.BLEND_RGBA_MULT)
    s.blit(light, (0, 0))

    # ступени, уходящие вверх к свету
    for i in range(3):
        y = bottom - 4 - i * 5
        width = half_w * 2 - 4 - i * 4
        col = textures.shade((190, 178, 152), 1.0 - i * 0.10)
        pygame.draw.rect(s, col, (cx - width // 2, y, width, 3))
        pygame.draw.line(s, textures.shade(col, 1.25),
                         (cx - width // 2, y), (cx + width // 2, y))

    # наличник арки
    pygame.draw.arc(s, (128, 120, 150), (cx - half_w - 2, top - 2,
                                         half_w * 2 + 4, half_w * 2 + 4),
                    0, math.pi, 2)
    pygame.draw.line(s, (128, 120, 150), (cx - half_w - 2, top + half_w),
                     (cx - half_w - 2, bottom - 2), 2)
    pygame.draw.line(s, (128, 120, 150), (cx + half_w + 2, top + half_w),
                     (cx + half_w + 2, bottom - 2), 2)

    # тёплый ореол вокруг проёма
    halo = pygame.Surface((SIZE, SIZE))
    halo.fill((0, 0, 0))
    for radius in range(half_w + 14, 0, -1):
        t = radius / (half_w + 14)
        k = int(26 * (1 - t) ** 2)
        if k <= 0:
            continue
        pygame.draw.circle(halo, (k, int(k * 0.7), int(k * 0.35)),
                           (cx, top + half_w + 6), radius)
    s.blit(halo, (0, 0), special_flags=pygame.BLEND_RGB_ADD)

    # трава на пороге и зелёная стрелка «наверх»
    rng = textures.rng_for(3, 3, seed=91)
    for _ in range(6):
        x = rng.randint(cx - half_w + 2, cx + half_w - 2)
        y = rng.randint(bottom - 2, bottom + 2)
        pygame.draw.line(s, (96, 150, 74), (x, y), (x, y - rng.randint(2, 4)))
    pygame.draw.polygon(s, (150, 235, 150), [(cx, bottom + 1),
                                             (cx - 5, bottom - 6),
                                             (cx + 5, bottom - 6)])
    return s


# ════════════════════════ ПОВЕРХНОСТЬ ════════════════════════
def _grass(variant):
    s = _new()
    s.fill(GRASS_BASE)
    rng = textures.rng_for(variant, 4, seed=17)
    v = variant % 8
    textures.speckles(s, (0, 0, SIZE, SIZE), GRASS_BASE, rng, 420, 0.13)
    # травинки
    for _ in range(32):
        x = rng.randint(2, SIZE - 3)
        y = rng.randint(4, SIZE - 3)
        h = rng.randint(2, 6)
        col = textures.shade(GRASS_BASE, rng.uniform(0.68, 1.32))
        pygame.draw.line(s, col, (x, y), (x + rng.choice((-1, 0, 1)), y - h))
    if v == 4:                               # цветы
        for _ in range(4):
            x = rng.randint(6, SIZE - 8)
            y = rng.randint(6, SIZE - 8)
            pygame.draw.circle(s, (226, 220, 148), (x, y), 2)
            pygame.draw.circle(s, (214, 176, 62), (x, y), 1)
    elif v == 5:                             # проплешина земли в углу
        x, y = rng.choice(((2, 4), (34, 8), (6, 38), (36, 40)))
        pygame.draw.ellipse(s, (92, 80, 56), (x, y, 24, 16))
        pygame.draw.ellipse(s, (74, 63, 44), (x + 5, y + 4, 13, 8))
        for _ in range(4):
            gx = x + rng.randint(2, 20)
            gy = y + rng.randint(3, 13)
            pygame.draw.line(s, textures.shade(GRASS_BASE, 1.1),
                             (gx, gy), (gx, gy - rng.randint(2, 4)))
    elif v == 6:                             # камни
        for _ in range(3):
            x = rng.randint(8, SIZE - 12)
            y = rng.randint(8, SIZE - 10)
            r = rng.randint(2, 3)
            pygame.draw.circle(s, (96, 98, 88), (x, y), r)
            pygame.draw.circle(s, (126, 128, 116), (x - 1, y - 1),
                               max(1, r - 1))
    elif v == 7:                             # высокая трава
        for _ in range(12):
            x = rng.randint(4, SIZE - 6)
            y = rng.randint(12, SIZE - 4)
            pygame.draw.line(s, textures.shade(GRASS_BASE, 1.18),
                             (x, y), (x + 1, y - rng.randint(4, 8)))
            pygame.draw.line(s, textures.shade(GRASS_BASE, 0.82),
                             (x + 2, y), (x + 2, y - rng.randint(3, 6)))
    return s


def _tree(variant):
    s = _grass(variant + 2)
    rng = textures.rng_for(variant, 6, seed=43)
    # тень под кроной
    shadow = pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)
    pygame.draw.ellipse(shadow, (0, 0, 0, 95), (6, 44, 52, 16))
    s.blit(shadow, (0, 0))
    # ствол
    pygame.draw.rect(s, TRUNK, (29, 36, 6, 22))
    pygame.draw.rect(s, textures.shade(TRUNK, 0.65), (33, 36, 2, 22))
    pygame.draw.line(s, textures.shade(TRUNK, 1.25), (29, 38), (29, 54), 1)
    # крона: несколько кругов, но контур — только у объединения
    crown = pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)
    circles = [(24, 27, 14), (40, 25, 15), (32, 17, 16),
               (18, 36, 9), (46, 36, 9)]
    for cx, cy, r in circles:
        pygame.draw.circle(crown, TREE_BASE, (cx, cy), r)
    # объём: тень снизу, свет сверху
    for cx, cy, r in circles:
        pygame.draw.circle(crown, textures.shade(TREE_BASE, 0.72),
                           (cx, cy + r // 3), max(3, r - 3))
    for cx, cy, r in circles:
        pygame.draw.circle(crown, textures.shade(TREE_BASE, 1.16),
                           (cx - r // 4, cy - r // 3), max(3, r // 2))
    # листва: мелкие пятна
    for _ in range(26):
        x = rng.randint(12, 52)
        y = rng.randint(8, 44)
        if crown.get_at((x, y))[3] < 40:
            continue
        factor = rng.uniform(0.75, 1.3)
        pygame.draw.circle(crown, textures.shade(TREE_BASE, factor), (x, y), 1)
    s.blit(textures.silhouette(crown, textures.shade(TREE_BASE, 0.4), 1), (0, 0))
    s.blit(crown, (0, 0))
    return s


def _cave_entrance(variant=0):
    s = _grass(1)
    # каменистый холм
    pygame.draw.ellipse(s, STONE_DARK, (4, 26, 56, 34))
    pygame.draw.ellipse(s, STONE, (8, 22, 48, 30))
    pygame.draw.ellipse(s, textures.shade(STONE, 1.2), (14, 24, 22, 12))
    # вход
    pygame.draw.rect(s, (10, 9, 14), (18, 30, 28, 26), border_radius=10)
    for i in range(3):
        y = 34 + i * 7
        pygame.draw.rect(s, textures.shade((110, 104, 118), 1.0 - i * 0.2),
                         (22 + i, y, 20 - i * 2, 4))
    # маркер вниз
    pygame.draw.polygon(s, (245, 200, 90), [(32, 58), (26, 48), (38, 48)])
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
                k = int(78 * (1 - t) ** 2)
                if k <= 0:
                    continue
                pygame.draw.circle(glow, (k, int(k * 0.62), int(k * 0.26)),
                                   (center, center), radius)
            cls._glow = glow
        return cls._glow
