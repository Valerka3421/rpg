"""Анимированный фон главного меню: медленный полёт камеры над миром.

Террейн строится один раз (процедурно, без игровых данных), кэшируется в
поверхность больше экрана, а кадр только смещает её — это дёшево и не
дёргает генератор мира при каждом возврате в меню.
"""
import math
import random

import pygame

from core import config


class MenuBackground:
    _surface = None
    _key = None
    _vignette = None
    _embers = None

    # ── Построение ──
    @classmethod
    def _build(cls, sw, sh):
        rng = random.Random(20241114)
        pad_x, pad_y = 260, 200
        w, h = sw + pad_x * 2, sh + pad_y * 2
        surf = pygame.Surface((w, h))

        # Низкочастотная «высота» — сглаженные случайные пятна.
        cell = 48
        gw, gh = w // cell + 2, h // cell + 2
        grid = [[rng.random() for _ in range(gw)] for _ in range(gh)]
        for _ in range(2):                      # размытие
            new = [[0.0] * gw for _ in range(gh)]
            for y in range(gh):
                for x in range(gw):
                    total = 0.0
                    for dy in (-1, 0, 1):
                        for dx in (-1, 0, 1):
                            total += grid[(y + dy) % gh][(x + dx) % gw]
                    new[y][x] = total / 9.0
            grid = new

        # Палитра сумеречного мира.
        water = (34, 52, 62)
        water_deep = (24, 38, 50)
        sand = (96, 86, 60)
        grass_a = (48, 58, 38)
        grass_b = (62, 72, 44)
        grass_c = (78, 84, 50)
        forest = (34, 44, 32)

        for py in range(0, h, cell // 2):
            gy = py // cell
            for px in range(0, w, cell // 2):
                gx = px // cell
                v = (grid[gy % gh][gx % gw] + grid[(gy + 1) % gh][gx % gw]
                     + grid[gy % gh][(gx + 1) % gw]) / 3.0
                if v < 0.30:
                    color = water_deep if v < 0.24 else water
                elif v < 0.36:
                    color = sand
                else:
                    t = min(1.0, (v - 0.36) / 0.5)
                    if t < 0.4:
                        a, b, k = grass_a, grass_b, t / 0.4
                    else:
                        a, b, k = grass_b, grass_c, (t - 0.4) / 0.6
                    color = tuple(int(a[i] + (b[i] - a[i]) * k) for i in range(3))
                pygame.draw.rect(surf, color, (px, py, cell // 2, cell // 2))

        # Леса: кластеры тёмных кругов.
        for _ in range(int(w * h / 26000)):
            cx, cy = rng.randrange(w), rng.randrange(h)
            if grid[(cy // cell) % gh][(cx // cell) % gw] < 0.42:
                continue
            for _ in range(rng.randint(2, 5)):
                tx = cx + rng.randint(-18, 18)
                ty = cy + rng.randint(-18, 18)
                r = rng.randint(8, 16)
                pygame.draw.circle(surf, (26, 34, 24), (tx + 3, ty + 4), r)
                pygame.draw.circle(surf, forest, (tx, ty), r)
                pygame.draw.circle(surf, (46, 58, 38), (tx - 2, ty - 2),
                                   max(2, r - 4))

        # Извилистая дорога.
        road = (94, 82, 60)
        x = -20
        y = rng.randint(int(h * 0.3), int(h * 0.7))
        points = []
        while x < w + 40:
            points.append((x, y))
            x += rng.randint(18, 34)
            y += rng.randint(-10, 10)
            y = max(40, min(h - 40, y))
        if len(points) > 1:
            pygame.draw.lines(surf, road, False, points, 7)
            pygame.draw.lines(surf, (112, 98, 70), False, points, 3)

        # Руины/камни для масштаба.
        for _ in range(26):
            rx, ry = rng.randrange(w), rng.randrange(h)
            size = rng.randint(5, 12)
            pygame.draw.rect(surf, (70, 66, 58),
                             (rx, ry, size, max(4, size - 3)),
                             border_radius=2)
            pygame.draw.rect(surf, (100, 94, 80),
                             (rx, ry, size, max(4, size - 3)), 1,
                             border_radius=2)

        # Туман/виньетка поверх мира, чтобы меню читалось.
        shade = pygame.Surface((w, h), pygame.SRCALPHA)
        for yy in range(0, h, 4):
            alpha = int(70 + 110 * (yy / h))
            pygame.draw.rect(shade, (8, 7, 6, alpha),
                             (0, yy, w, 4))
        surf.blit(shade, (0, 0))

        cls._surface = surf
        cls._embers = [(rng.uniform(0, sw), rng.uniform(0, sh),
                        rng.uniform(0.2, 0.7), rng.uniform(0, 6.28))
                       for _ in range(44)]

    @classmethod
    def _build_vignette(cls, sw, sh):
        surf = pygame.Surface((sw, sh), pygame.SRCALPHA)
        for i in range(70):
            alpha = int(2 + (70 - i) * 1.2)
            pygame.draw.rect(surf, (5, 4, 3, alpha),
                             (i, i, sw - i * 2, sh - i * 2), 1)
        cls._vignette = surf

    # ── Рисование ──
    @classmethod
    def draw(cls, screen):
        sw, sh = config.SCREEN_W, config.SCREEN_H
        key = (sw, sh)
        if cls._key != key or cls._surface is None:
            cls._build(sw, sh)
            cls._build_vignette(sw, sh)
            cls._key = key

        t = pygame.time.get_ticks() / 1000.0
        surf = cls._surface
        margin_x = max(1, surf.get_width() - sw)
        margin_y = max(1, surf.get_height() - sh)
        # Плавный полёт по эллипсу: камера «дышит» над миром.
        ox = margin_x / 2 + math.sin(t * 0.07) * (margin_x / 2 - 10)
        oy = margin_y / 2 + math.cos(t * 0.05) * (margin_y / 2 - 10)
        screen.blit(surf, (-ox, -oy))

        # Угольки/светлячки.
        for ex, ey, speed, phase in (cls._embers or ()):
            yy = (ey - t * 14 * speed) % sh
            xx = ex + math.sin(t * speed + phase) * 12
            glow = max(0.15, 0.5 + 0.5 * math.sin(t * 1.4 + phase))
            r = 1 + int(glow * 2)
            pygame.draw.circle(screen, (240, 190, 110),
                               (int(xx), int(yy)), r)

        if cls._vignette is not None:
            screen.blit(cls._vignette, (0, 0))
