"""Отрисовка летящих снарядов (стрел, магических шаров, молний).

Каждый Projectile из game.projectiles рисуется как:
- arrow — тонкая линия в направлении движения
- fireball — круг с ореолом
- frost — шестиугольник
- lightning — зигзаг
- heal — мягкий круг с эффектом свечения

Координаты — в тайлах. Перевод в пиксели через view_origin и TILE.
"""
import math

import pygame

from core import config


def draw_projectiles(screen, projectiles, view_origin):
    """Отрисовать все снаряды на игровом поле."""
    if not projectiles:
        return
    ox, oy = view_origin
    tile = config.TILE
    for p in projectiles:
        px = ox + p.x * tile + tile // 2
        py = oy + p.y * tile + tile // 2
        _draw_one(screen, p, px, py, tile)


def _draw_one(screen, p, px, py, tile):
    if p.kind == 'arrow':
        # Тонкая линия от центра к цели, длиной ~20px
        dx = p.tx - p.x
        dy = p.ty - p.y
        dist = max(0.1, math.hypot(dx, dy))
        ex = px + dx / dist * 20
        ey = py + dy / dist * 20
        pygame.draw.line(screen, p.color, (px, py), (ex, ey), 2)
        # Наконечник — маленький треугольник
        pygame.draw.circle(screen, p.color, (int(ex), int(ey)), 3)

    elif p.kind == 'fireball':
        # Основной круг + ореол
        pygame.draw.circle(screen, p.color, (int(px), int(py)), 8)
        for r, alpha in [(12, 80), (16, 40), (20, 20)]:
            s = pygame.Surface((r * 2 + 2, r * 2 + 2), pygame.SRCALPHA)
            pygame.draw.circle(s, (*p.color, alpha), (r + 1, r + 1), r)
            screen.blit(s, (px - r - 1, py - r - 1))

    elif p.kind == 'frost':
        # Шестиугольник
        r = 8
        points = []
        for i in range(6):
            angle = i * math.pi / 3
            points.append((px + r * math.cos(angle),
                           py + r * math.sin(angle)))
        pygame.draw.polygon(screen, p.color, points)
        pygame.draw.polygon(screen, (220, 240, 255), points, 1)

    elif p.kind == 'lightning':
        # Зигзаг от стрелка к цели (упрощённо — короткая ломаная)
        dx = p.tx - p.x
        dy = p.ty - p.y
        dist = max(0.1, math.hypot(dx, dy))
        ux, uy = dx / dist, dy / dist
        # Перпендикуляр для зигзага
        perp_x, perp_y = -uy, ux
        pts = [(px, py)]
        steps = 4
        for i in range(1, steps):
            t = i / steps
            base_x = px + dx * tile * 0.3 * t
            base_y = py + dy * tile * 0.3 * t
            offset = 4 if i % 2 == 0 else -4
            pts.append((base_x + perp_x * offset,
                        base_y + perp_y * offset))
        pts.append((px + dx * tile * 0.3, py + dy * tile * 0.3))
        if len(pts) >= 2:
            pygame.draw.lines(screen, p.color, False, pts, 3)

    elif p.kind == 'heal':
        # Мягкое свечение — несколько концентрических кругов
        for r, alpha in [(14, 30), (10, 80), (6, 160)]:
            s = pygame.Surface((r * 2 + 2, r * 2 + 2), pygame.SRCALPHA)
            pygame.draw.circle(s, (*p.color, alpha), (r + 1, r + 1), r)
            screen.blit(s, (px - r - 1, py - r - 1))

    else:
        # Неизвестный тип — точка
        pygame.draw.circle(screen, p.color, (int(px), int(py)), 4)
