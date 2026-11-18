"""Auto-generated mixin from render/icon_renderer.py."""
import math
import os

import pygame

from core.config import TILE
from core.data import ITEMS, MATERIAL_KEYS, item_display_color, parse_item_key
from render import textures
from render.base_renderer import BaseRenderer
from render.icons._base import CANVAS, CX
from render.sprites import Sprites
from render.theme import Theme as T

class ClassIconMixin:
    """Маленькие иконки классов для дерева навыков и UI."""

    def draw_class_icon(self, class_key, x, y, size=32):
        img = Sprites.get(f'class_icons/{class_key}', (size, size))
        if img:
            self.screen.blit(img, (x, y))
            return
        ck = (class_key, size)
        icon = self._class_icon_cache.get(ck)
        if icon is None:
            icon = self._make_class_icon(class_key, size)
            self._class_icon_cache[ck] = icon
        self.screen.blit(icon, (x, y))

    def _make_class_icon(self, class_key, size):
        """Абстрактные иконки классов: оружие + символ."""
        s = pygame.Surface((CANVAS, CANVAS), pygame.SRCALPHA)
        fn = {
            'warrior': self._class_icon_warrior,
            'assassin': self._class_icon_assassin,
            'rogue': self._class_icon_rogue,
            'paladin': self._class_icon_paladin,
        }.get(class_key, self._class_icon_default)
        fn(s)
        if size != CANVAS:
            s = pygame.transform.smoothscale(s, (size, size))
        return textures.finish(s, shadow=False)

    def _class_icon_warrior(self, s):
        # Скрещённые меч и щит
        # Щит
        pts = [(CX - 22, CX - 18), (CX - 4, CX - 18), (CX - 4, CX + 8),
               (CX - 13, CX + 22), (CX - 22, CX + 8)]
        pygame.draw.polygon(s, (200, 180, 100), pts)
        pygame.draw.polygon(s, (120, 90, 40), pts, 3)
        # Крест на щите
        pygame.draw.line(s, (240, 200, 60),
                         (CX - 13, CX - 14), (CX - 13, CX + 14), 3)
        pygame.draw.line(s, (240, 200, 60),
                         (CX - 22, CX - 4), (CX - 4, CX - 4), 3)
        # Меч
        pygame.draw.polygon(s, (220, 220, 240), [
            (CX + 4, CX - 22), (CX + 12, CX - 22),
            (CX + 16, CX + 16), (CX + 8, CX + 22), (CX + 4, CX + 16),
        ])
        pygame.draw.polygon(s, (100, 100, 120), [
            (CX + 4, CX - 22), (CX + 12, CX - 22),
            (CX + 16, CX + 16), (CX + 8, CX + 22), (CX + 4, CX + 16),
        ], 2)
        pygame.draw.line(s, (255, 255, 255),
                         (CX + 8, CX - 20), (CX + 8, CX + 12), 2)
        pygame.draw.rect(s, (240, 200, 60), (CX + 2, CX + 12, 14, 4))
        pygame.draw.rect(s, (90, 60, 30), (CX + 6, CX + 16, 6, 8))

    def _class_icon_assassin(self, s):
        # Два скрещённых кинжала + капля крови
        for dx, dy in ((-1, 1), (1, -1)):
            x1 = CX + dx * 22
            y1 = CX + dy * 22
            x2 = CX - dx * 22
            y2 = CX - dy * 22
            pygame.draw.polygon(s, (200, 215, 235), [
                (x1, y1),
                (x1 - dx * 4, y1 - dy * 4),
                (x2 + dx * 6, y2 + dy * 6),
                (x2, y2),
            ])
            pygame.draw.polygon(s, (100, 120, 150), [
                (x1, y1),
                (x1 - dx * 4, y1 - dy * 4),
                (x2 + dx * 6, y2 + dy * 6),
                (x2, y2),
            ], 2)
            # Гарда и рукоять
            pygame.draw.circle(s, (240, 200, 60), (x2 + dx * 4, y2 + dy * 4), 3)
            pygame.draw.line(s, (90, 60, 30),
                             (x2 + dx * 4, y2 + dy * 4),
                             (x2 + dx * 8, y2 + dy * 8), 3)
        # Капля крови в центре
        pygame.draw.circle(s, (200, 30, 60), (CX, CX), 5)
        pygame.draw.polygon(s, (200, 30, 60), [
            (CX - 3, CX - 4), (CX + 3, CX - 4), (CX, CX - 10),
        ])

    def _class_icon_rogue(self, s):
        # Лук + стрела
        # Лук
        pygame.draw.arc(s, (150, 100, 50),
                        (CX - 20, CX - 22, 40, 44), -1.4, 1.4, 4)
        pygame.draw.arc(s, (90, 60, 30),
                        (CX - 20, CX - 22, 40, 44), -1.4, 1.4, 1)
        # Тетива
        pygame.draw.line(s, (220, 200, 160),
                         (CX - 6, CX - 22), (CX - 6, CX + 22), 1)
        # Стрела
        pygame.draw.line(s, (150, 100, 50),
                         (CX - 18, CX), (CX + 22, CX), 3)
        # Наконечник
        pygame.draw.polygon(s, (220, 220, 230), [
            (CX + 22, CX - 5), (CX + 22, CX + 5), (CX + 28, CX),
        ])
        # Оперение
        pygame.draw.polygon(s, (200, 60, 60), [
            (CX - 16, CX - 6), (CX - 10, CX), (CX - 16, CX + 6),
        ])

    def _class_icon_paladin(self, s):
        # Молот + крест
        # Молот (вертикально)
        pygame.draw.rect(s, (90, 60, 30),
                         (CX - 3, CX - 4, 6, 26))
        pygame.draw.rect(s, (60, 30, 10),
                         (CX - 3, CX - 4, 6, 26), 1)
        # Голова молота
        pygame.draw.rect(s, (240, 210, 100),
                         (CX - 16, CX - 24, 32, 18), border_radius=3)
        pygame.draw.rect(s, (180, 140, 40),
                         (CX - 16, CX - 24, 32, 18), 3, border_radius=3)
        pygame.draw.line(s, (255, 240, 150),
                         (CX - 14, CX - 22), (CX + 14, CX - 22), 2)
        # Крест на голове молота
        pygame.draw.line(s, (200, 40, 40),
                         (CX, CX - 22), (CX, CX - 8), 3)
        pygame.draw.line(s, (200, 40, 40),
                         (CX - 6, CX - 15), (CX + 6, CX - 15), 3)
        # Нимб
        pygame.draw.arc(s, (255, 240, 150),
                        (CX - 20, CX - 30, 40, 12), 0, 3.14, 2)

    def _class_icon_default(self, s):
        # Компас / звезда
        for i in range(4):
            angle = i * 1.57
            ex = CX + int(20 * math.cos(angle))
            ey = CX + int(20 * math.sin(angle))
            pygame.draw.line(s, (240, 210, 100), (CX, CX), (ex, ey), 4)
        pygame.draw.circle(s, (240, 210, 100), (CX, CX), 22, 2)
        pygame.draw.circle(s, (255, 255, 200), (CX, CX), 4)

    # ════════════════════════ МОНСТРЫ 64×64 ════════════════════════
