"""Общие примитивы процедурной графики.

Здесь живут операции, которые нужны и тайлам, и спрайтам: детерминированный
шум (одинаковый тайл = одинаковый вид), оттенки, бевелы, обводка и тени.
Никакой игровой логики — только пиксели.
"""
import math
import random

import pygame

# ── Палитра ──
# Тёплый почти-чёрный контур: холодная фиолетовая обводка выдавала
# «мультяшность», а Stoneshard строит силуэт на коричнево-чёрном.
INK = (11, 8, 6)
SHADOW_COLOR = (5, 4, 3)


# ════════════════════ ЦВЕТ ════════════════════
def clamp_byte(value):
    return 0 if value < 0 else (255 if value > 255 else int(value))


def shade(color, factor):
    """Умножить яркость: 1.0 — как есть, 0.5 — вдвое темнее."""
    return tuple(clamp_byte(c * factor) for c in color[:3])


def mix(a, b, t):
    """Линейная смесь двух цветов (t=0 → a, t=1 → b)."""
    t = max(0.0, min(1.0, t))
    return tuple(clamp_byte(a[i] * (1 - t) + b[i] * t) for i in range(3))


def with_alpha(color, alpha):
    return (color[0], color[1], color[2], clamp_byte(alpha))


# ════════════════════ ЦВЕТОКОРРЕКЦИЯ ════════════════════
def desaturate(surface, amount=0.30):
    """Смешать изображение с серым: убирает кислотные цвета."""
    if amount <= 0:
        return surface
    gray = pygame.transform.grayscale(surface)
    gray.fill((255, 255, 255, clamp_byte(255 * amount)),
              special_flags=pygame.BLEND_RGBA_MULT)
    out = surface.copy()
    out.blit(gray, (0, 0))
    return out


def stoneshard_grade(surface, amount=0.35):
    """Единый грейд для всех спрайтов: приглушённые цвета, тёплый верх,
    холодный низ.

    Применяется к процедурным и PNG-спрайтам, чтобы предметы, монстры и
    игрок не выбивались из палитры тайлов. Вертикальный градиент даёт
    объём и «подземное» освещение даже плоским иконкам.
    """
    out = desaturate(surface, amount)
    # убираем холодный фиолетовый уклон и слегка прижимаем яркость
    out.fill((244, 232, 210), special_flags=pygame.BLEND_RGB_MULT)
    w, h = out.get_size()
    if w and h:
        top = (255, 250, 236)
        bottom = (126, 134, 152)
        ramp = pygame.Surface((1, h), pygame.SRCALPHA)
        for y in range(h):
            t = (y / max(1, h - 1)) ** 0.85
            ramp.set_at((0, y), (*mix(top, bottom, t), 255))
        ramp = pygame.transform.scale(ramp, (w, h))
        out.blit(ramp, (0, 0), special_flags=pygame.BLEND_RGBA_MULT)
    return out


# ════════════════════ ДЕТЕРМИНИРОВАННЫЙ ШУМ ════════════════════
def _mix32(value):
    """Целочисленное перемешивание (splitmix-подобное) с лавинным эффектом."""
    value &= 0xFFFFFFFF
    value ^= value >> 16
    value = (value * 0x7FEB352D) & 0xFFFFFFFF
    value ^= value >> 15
    value = (value * 0x846CA68B) & 0xFFFFFFFF
    value ^= value >> 16
    return value & 0xFFFFFFFF


def hash01(x, y, seed=0):
    """Стабильное «случайное» число 0..1 для координаты/варианта.

    Важно, чтобы соседние клетки не попадали в один и тот же вариант:
    слабое перемешивание давало видимые прямоугольные «пятна» тайлов.
    """
    h = _mix32(int(x) * 0x27D4EB2D + int(y) * 0x165667B1
               + int(seed) * 0x9E3779B1)
    return (h & 0xFFFFFF) / 16777216.0


def hash_int(x, y, seed=0, modulo=1000):
    return int(hash01(x, y, seed) * modulo) % max(1, modulo)


def rng_for(x, y, seed=0):
    return random.Random(hash_int(x, y, seed, 1_000_000))


# ════════════════════ ПРИМИТИВЫ ════════════════════
def bevel_rect(surface, rect, base, light=None, dark=None, width=1):
    """Прямоугольник с подсветкой сверху-слева и тенью снизу-справа."""
    x, y, w, h = rect
    light = light or shade(base, 1.45)
    dark = dark or shade(base, 0.62)
    pygame.draw.rect(surface, base, rect)
    for i in range(width):
        pygame.draw.line(surface, light, (x + i, y + i),
                         (x + w - 1 - i, y + i))
        pygame.draw.line(surface, light, (x + i, y + i),
                         (x + i, y + h - 1 - i))
        pygame.draw.line(surface, dark, (x + i, y + h - 1 - i),
                         (x + w - 1 - i, y + h - 1 - i))
        pygame.draw.line(surface, dark, (x + w - 1 - i, y + i),
                         (x + w - 1 - i, y + h - 1 - i))


def grain(surface, rect, color, count, rng, size=(1, 1), alpha=None):
    """Точечный шум внутри прямоугольника."""
    x, y, w, h = rect
    for _ in range(count):
        px = rng.randrange(x, x + w)
        py = rng.randrange(y, y + h)
        sw = rng.randint(size[0], size[1])
        sh = rng.randint(size[0], size[1])
        if alpha is None:
            surface.fill(color, (px, py, sw, sh))
        else:
            layer = pygame.Surface((sw, sh), pygame.SRCALPHA)
            layer.fill(with_alpha(color, alpha))
            surface.blit(layer, (px, py))


def speckles(surface, rect, base, rng, count=80, spread=0.14):
    """Крапинки светлее/темнее базы — объём без текстурных файлов."""
    for _ in range(count):
        x = rng.randrange(rect[0], rect[0] + rect[2])
        y = rng.randrange(rect[1], rect[1] + rect[3])
        factor = 1 + rng.uniform(-spread, spread)
        surface.set_at((x, y), shade(base, factor))


# ════════════════════ ОБВОДКА И ТЕНЬ ════════════════════
def silhouette(surface, color=INK, width=1, threshold=40):
    """Контур вокруг непрозрачных пикселей."""
    mask = pygame.mask.from_surface(surface, threshold)
    if mask.count() == 0:
        return pygame.Surface(surface.get_size(), pygame.SRCALPHA)
    solid = mask.to_surface(setcolor=with_alpha(color, 255),
                            unsetcolor=(0, 0, 0, 0))
    out = pygame.Surface(surface.get_size(), pygame.SRCALPHA)
    offsets = [(dx, dy)
               for dx in range(-width, width + 1)
               for dy in range(-width, width + 1)
               if (dx or dy) and dx * dx + dy * dy <= width * width + 0.25]
    for dx, dy in offsets:
        out.blit(solid, (dx, dy))
    return out


def finish(surface, outline=INK, width=1, shadow=True,
           shadow_offset=(2, 3), shadow_alpha=110, threshold=40):
    """Финальная обработка спрайта: грейд + тень + обводка + сам спрайт.

    Возвращает новую поверхность того же размера; исходник не меняется.
    """
    surface = stoneshard_grade(surface)
    size = surface.get_size()
    result = pygame.Surface(size, pygame.SRCALPHA)
    if shadow:
        mask = pygame.mask.from_surface(surface, threshold)
        if mask.count():
            shadow_surf = mask.to_surface(setcolor=(0, 0, 0, 255),
                                          unsetcolor=(0, 0, 0, 0))
            shadow_surf.set_alpha(shadow_alpha)
            result.blit(shadow_surf, shadow_offset)
    result.blit(silhouette(surface, outline, width, threshold), (0, 0))
    result.blit(surface, (0, 0))
    return result


# ════════════════════ СВЕТ ════════════════════
def light_factor(distance, max_distance, inner=0.65, outer=1.0):
    """Яркость тайла по расстоянию от игрока: 1.0 рядом, ``inner`` на краю.

    Раньше формула была перевёрнута (дальние тайлы светлее ближних) —
    теперь свет мягко убывает к границе видимости.
    """
    if max_distance <= 0:
        return outer
    t = min(1.0, max(0.0, distance / max_distance))
    # мягкая S-кривая
    t = t * t * (3 - 2 * t)
    return outer + (inner - outer) * t


def apply_light(surface, factor):
    """Копия поверхности с холодным затемнением.

    В тенях Stoneshard уходит не в серый, а в сине-зелёный сумрак, поэтому
    множитель не нейтральный: красный гаснет быстрее синего.
    """
    if abs(factor - 1.0) < 0.001:
        return surface
    lit = surface.copy()
    f = max(0.0, min(1.0, factor))
    r = clamp_byte(255 * (0.28 + 0.72 * f))
    g = clamp_byte(255 * (0.32 + 0.68 * f))
    b = clamp_byte(255 * (0.40 + 0.60 * f))
    lit.fill((r, g, b, 255), special_flags=pygame.BLEND_RGB_MULT)
    return lit
