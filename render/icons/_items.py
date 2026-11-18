"""Auto-generated mixin from render/icon_renderer.py."""
import math

import pygame

from core.data import ITEMS
from render import textures
from render.icons._base import CANVAS, CX
from render.theme import Theme as T



class ItemMixin:
    """Примитивы рисования предметов: зелья, оружие, броня, материалы, книги."""

    def _potion(self, s, liquid, big=False, sparkle=False):
        w = 24 if big else 18
        h = 26 if big else 22
        bx, by = CX - w // 2, CX - h // 2 + 4

        # Тень / подложка
        pygame.draw.ellipse(s, (30, 30, 45, 80), (bx - 2, by + h - 4, w + 4, 8))

        # Стекло (полупрозрачное)
        pygame.draw.ellipse(s, (230, 240, 250, 100), (bx, by, w, h))
        pygame.draw.ellipse(s, (170, 190, 210), (bx, by, w, h), 2)

        # Жидкость
        pygame.draw.ellipse(s, liquid, (bx + 2, by + h // 2 - 2, w - 4, h // 2))
        # Блик
        pygame.draw.line(s, (255, 255, 255),
                         (bx + 4, by + 4), (bx + 4, by + h // 2 - 4), 2)

        # Горлышко
        pygame.draw.rect(s, (200, 215, 230), (CX - 5, by - 6, 10, 8))
        pygame.draw.rect(s, (140, 160, 180), (CX - 5, by - 6, 10, 8), 1)

        # Пробка
        pygame.draw.rect(s, (140, 100, 60), (CX - 6, by - 11, 12, 6))
        pygame.draw.rect(s, (90, 60, 30), (CX - 6, by - 11, 12, 6), 1)

        if sparkle:
            for dx, dy in ((-8, 8), (10, -4), (-6, -10)):
                pygame.draw.circle(s, (255, 255, 200), (CX + dx, CX + dy), 2)
                pygame.draw.circle(s, (255, 255, 255), (CX + dx - 1, CX + dy - 1), 1)

    # ════════════════════════ ОРУЖИЕ ════════════════════════
    def _blade(self, s, length, color, guard, gem=False, wide=False):
        bw = 8 if wide else 6
        top = CX - length // 2 - 4
        tip = CX + length // 2 - 4
        # Клинок
        pygame.draw.polygon(s, color, [
            (CX - bw // 2, top),
            (CX + bw // 2, top),
            (CX + bw // 2, tip),
            (CX, tip + 6),
            (CX - bw // 2, tip),
        ])
        pygame.draw.polygon(s, T.shade(color, 0.55), [
            (CX - bw // 2, top),
            (CX + bw // 2, top),
            (CX + bw // 2, tip),
            (CX, tip + 6),
            (CX - bw // 2, tip),
        ], 2)
        # Дол и блик
        pygame.draw.line(s, T.shade(color, 0.8), (CX, top + 4), (CX, tip - 2), 1)
        pygame.draw.line(s, (255, 255, 255), (CX - bw // 2 + 2, top + 4),
                         (CX - bw // 2 + 2, tip - 4), 2)

        # Крестовина
        gy = tip + 4
        pygame.draw.rect(s, (220, 180, 60), (CX - guard // 2, gy, guard, 4))
        pygame.draw.rect(s, (120, 90, 20), (CX - guard // 2, gy, guard, 4), 1)
        if gem:
            pygame.draw.circle(s, (200, 40, 60), (CX, gy + 2), 3)
            pygame.draw.circle(s, (255, 200, 200), (CX - 1, gy + 1), 1)

        # Рукоять
        pygame.draw.rect(s, (90, 60, 30), (CX - 4, gy + 4, 8, 12))
        pygame.draw.rect(s, (50, 30, 15), (CX - 4, gy + 4, 8, 12), 1)
        for yy in range(gy + 6, gy + 14, 3):
            pygame.draw.line(s, (60, 40, 20), (CX - 4, yy), (CX + 4, yy), 1)

        # Навершие
        pygame.draw.circle(s, (240, 200, 60), (CX, gy + 20), 4)
        pygame.draw.circle(s, (140, 100, 30), (CX, gy + 20), 4, 1)

    def _axe(self, s, tint=(170, 170, 180)):
        # Рукоять
        pygame.draw.rect(s, (90, 60, 30), (CX - 3, 8, 6, 50))
        pygame.draw.rect(s, (50, 30, 15), (CX - 3, 8, 6, 50), 1)
        # Лезвие
        pygame.draw.polygon(s, tint, [
            (CX + 3, 10), (CX + 22, 14), (CX + 26, 30), (CX + 3, 34),
        ])
        pygame.draw.polygon(s, T.shade(tint, 0.5), [
            (CX + 3, 10), (CX + 22, 14), (CX + 26, 30), (CX + 3, 34),
        ], 2)
        # Блик
        pygame.draw.line(s, (220, 220, 230), (CX + 8, 16), (CX + 20, 24), 2)
        # Задняя часть
        pygame.draw.polygon(s, (140, 140, 150), [
            (CX - 3, 12), (CX - 12, 16), (CX - 12, 28), (CX - 3, 32),
        ])

    def _warhammer(self, s, color, runes=False):
        # Рукоять
        pygame.draw.rect(s, (90, 60, 30), (CX - 3, 8, 6, 52))
        pygame.draw.rect(s, (50, 30, 15), (CX - 3, 8, 6, 52), 1)
        # Голова
        pygame.draw.rect(s, color, (CX - 16, 10, 32, 18), border_radius=3)
        pygame.draw.rect(s, T.shade(color, 0.5), (CX - 16, 10, 32, 18), 2,
                         border_radius=3)
        # Верхняя грань
        pygame.draw.line(s, T.shade(color, 1.3), (CX - 15, 11), (CX + 15, 11), 2)
        if runes:
            pygame.draw.circle(s, (100, 180, 255), (CX - 7, 18), 3)
            pygame.draw.circle(s, (100, 180, 255), (CX + 7, 18), 3)
            pygame.draw.line(s, (100, 180, 255), (CX - 4, 20), (CX + 4, 20), 1)
        # Навершие
        pygame.draw.circle(s, color, (CX, 60), 4)
        pygame.draw.circle(s, T.shade(color, 0.5), (CX, 60), 4, 1)

    def _bone_sword(self, s):
        # Клинок из кости
        pygame.draw.polygon(s, (240, 235, 215), [
            (CX - 4, 8), (CX + 4, 8), (CX + 4, 42), (CX, 48), (CX - 4, 42),
        ])
        pygame.draw.polygon(s, (170, 160, 130), [
            (CX - 4, 8), (CX + 4, 8), (CX + 4, 42), (CX, 48), (CX - 4, 42),
        ], 2)
        # Трещины
        pygame.draw.line(s, (170, 160, 130), (CX - 1, 14), (CX + 1, 20), 1)
        pygame.draw.line(s, (170, 160, 130), (CX + 2, 26), (CX - 1, 34), 1)
        # Гарда
        pygame.draw.rect(s, (150, 100, 60), (CX - 12, 46, 24, 4))
        # Рукоять
        pygame.draw.rect(s, (90, 60, 30), (CX - 4, 50, 8, 8))
        # Навершие — косточка
        pygame.draw.circle(s, (240, 235, 215), (CX, 60), 4)
        pygame.draw.circle(s, (170, 160, 130), (CX, 60), 4, 1)

    def _demon_blade(self, s):
        # Аура
        aura = pygame.Surface((CANVAS, CANVAS), pygame.SRCALPHA)
        pygame.draw.ellipse(aura, (200, 40, 40, 80),
                            (CX - 10, 4, 20, 54))
        s.blit(aura, (0, 0))
        # Клинок
        pygame.draw.polygon(s, (220, 50, 50), [
            (CX - 4, 8), (CX + 4, 8), (CX + 4, 42), (CX, 48), (CX - 4, 42),
        ])
        pygame.draw.polygon(s, (90, 10, 10), [
            (CX - 4, 8), (CX + 4, 8), (CX + 4, 42), (CX, 48), (CX - 4, 42),
        ], 2)
        # Внутреннее свечение
        pygame.draw.line(s, (255, 150, 150), (CX, 12), (CX, 42), 2)
        # Шипы на гарде
        pygame.draw.polygon(s, (200, 40, 40), [
            (CX - 14, 48), (CX - 8, 44), (CX - 8, 52),
        ])
        pygame.draw.polygon(s, (200, 40, 40), [
            (CX + 14, 48), (CX + 8, 44), (CX + 8, 52),
        ])
        # Рукоять
        pygame.draw.rect(s, (30, 10, 10), (CX - 4, 50, 8, 10))

    # ════════════════════════ БРОНЯ ════════════════════════
    def _armor(self, s, color, style='plate'):
        # Основа нагрудника
        top = (CX - 18, 10)
        points = [
            top, (CX + 18, 10), (CX + 22, 24),
            (CX + 18, 50), (CX, 56), (CX - 18, 50), (CX - 22, 24),
        ]
        pygame.draw.polygon(s, color, points)
        pygame.draw.polygon(s, T.shade(color, 0.5), points, 2)

        # Ворот
        pygame.draw.ellipse(s, T.shade(color, 0.5), (CX - 10, 6, 20, 8))

        if style == 'plate':
            # Рёбра
            pygame.draw.line(s, T.shade(color, 1.15),
                             (CX - 8, 16), (CX - 8, 46), 2)
            pygame.draw.line(s, T.shade(color, 1.15),
                             (CX + 8, 16), (CX + 8, 46), 2)
            # Наплечники
            pygame.draw.circle(s, T.shade(color, 1.1), (CX - 16, 16), 4)
            pygame.draw.circle(s, T.shade(color, 1.1), (CX + 16, 16), 4)
            # Заклёпки
            for yy in (24, 36):
                pygame.draw.circle(s, T.shade(color, 0.7), (CX - 10, yy), 1)
                pygame.draw.circle(s, T.shade(color, 0.7), (CX + 10, yy), 1)
        elif style == 'chain':
            for yy in range(16, 50, 5):
                for xx in range(CX - 16, CX + 17, 5):
                    pygame.draw.circle(s, T.shade(color, 0.7), (xx, yy), 2)
        elif style == 'scale':
            for yy in range(14, 50, 5):
                offset = 0 if (yy // 5) % 2 == 0 else 3
                for xx in range(CX - 16 + offset, CX + 17, 5):
                    pygame.draw.ellipse(s, T.shade(color, 1.1),
                                        (xx - 2, yy - 2, 5, 3))
                    pygame.draw.ellipse(s, T.shade(color, 0.6),
                                        (xx - 2, yy - 2, 5, 3), 1)
        elif style == 'leather':
            pygame.draw.line(s, T.shade(color, 0.4), (CX, 12), (CX, 52), 1)
            for yy in (20, 32, 44):
                pygame.draw.circle(s, (240, 200, 100), (CX - 12, yy), 2)
                pygame.draw.circle(s, (240, 200, 100), (CX + 12, yy), 2)
            pygame.draw.line(s, T.shade(color, 0.7),
                             (CX - 16, 24), (CX + 16, 24), 1)
        elif style == 'quilted':
            for yy in range(16, 50, 6):
                pygame.draw.line(s, T.shade(color, 0.7),
                                 (CX - 16, yy), (CX + 16, yy), 1)
            for xx in range(CX - 12, CX + 13, 8):
                pygame.draw.line(s, T.shade(color, 0.7),
                                 (xx, 14), (xx, 50), 1)
        elif style == 'robe':
            pygame.draw.line(s, T.shade(color, 1.3), (CX, 14), (CX, 52), 2)
            for yy in range(20, 50, 8):
                pygame.draw.circle(s, (255, 240, 180), (CX, yy), 2)

    # ════════════════════════ РЕСУРСЫ ════════════════════════
    def _mushroom(self, s):
        # Ножка
        pygame.draw.rect(s, (235, 215, 180), (CX - 5, 36, 10, 20))
        pygame.draw.rect(s, (180, 160, 130), (CX - 5, 36, 10, 20), 2)
        # Шляпка
        pygame.draw.ellipse(s, (200, 80, 60), (CX - 22, 14, 44, 28))
        pygame.draw.ellipse(s, (150, 50, 40), (CX - 22, 14, 44, 28), 2)
        # Пятнышки
        for dx, dy in ((-10, 22), (8, 20), (0, 26), (-4, 30)):
            pygame.draw.circle(s, (255, 240, 220), (CX + dx, 14 + dy - 8), 3)
        # Тень
        pygame.draw.ellipse(s, (30, 30, 45, 60), (CX - 18, 54, 36, 6))

    def _berry(self, s):
        # Куст
        pygame.draw.ellipse(s, (50, 120, 50), (CX - 24, 18, 48, 34))
        pygame.draw.ellipse(s, (30, 80, 30), (CX - 24, 18, 48, 34), 2)
        pygame.draw.ellipse(s, (70, 150, 60), (CX - 20, 20, 30, 20))
        # Ягоды
        for dx, dy in ((-10, 6), (10, 4), (0, 14), (-6, 20), (12, 18)):
            pygame.draw.circle(s, (200, 40, 80), (CX + dx, 24 + dy), 5)
            pygame.draw.circle(s, (255, 180, 200),
                               (CX + dx - 2, 24 + dy - 2), 2)
        # Тень
        pygame.draw.ellipse(s, (30, 30, 45, 60), (CX - 20, 52, 40, 6))

    def _wood(self, s):
        # Бревно
        pygame.draw.rect(s, (130, 85, 50), (CX - 26, 20, 52, 22),
                         border_radius=4)
        pygame.draw.rect(s, (90, 55, 30), (CX - 26, 20, 52, 22), 2,
                         border_radius=4)
        # Годичные кольца (с торца)
        pygame.draw.ellipse(s, (160, 110, 70), (CX - 26, 20, 12, 22))
        pygame.draw.ellipse(s, (90, 55, 30), (CX - 26, 20, 12, 22), 2)
        pygame.draw.ellipse(s, (110, 70, 40), (CX - 24, 24, 8, 14), 1)
        # Трещины на теле
        for xx in (-14, -4, 8, 18):
            pygame.draw.line(s, (100, 65, 35),
                             (CX + xx, 24), (CX + xx, 38), 1)
        # Тень
        pygame.draw.ellipse(s, (30, 30, 45, 60), (CX - 24, 42, 48, 6))

    def _stone(self, s):
        pts = [
            (CX - 18, 42), (CX - 22, 20), (CX - 10, 8),
            (CX + 8, 6), (CX + 22, 14), (CX + 24, 36),
            (CX + 10, 50), (CX - 8, 52),
        ]
        pygame.draw.polygon(s, (140, 140, 150), pts)
        pygame.draw.polygon(s, (75, 75, 85), pts, 2)
        # Трещины
        pygame.draw.line(s, (85, 85, 95), (CX - 8, 16), (CX + 2, 30), 2)
        pygame.draw.line(s, (85, 85, 95), (CX + 2, 30), (CX - 2, 42), 1)
        # Блик
        pygame.draw.line(s, (180, 180, 190), (CX - 12, 12), (CX - 6, 18), 2)
        # Тень
        pygame.draw.ellipse(s, (30, 30, 45, 60), (CX - 20, 50, 40, 6))

    def _ore(self, s):
        pts = [
            (CX - 18, 42), (CX - 22, 20), (CX - 10, 8),
            (CX + 8, 6), (CX + 22, 14), (CX + 24, 36),
            (CX + 10, 50), (CX - 8, 52),
        ]
        pygame.draw.polygon(s, (110, 110, 125), pts)
        pygame.draw.polygon(s, (55, 55, 70), pts, 2)
        # Золотые вкрапления
        for dx, dy in ((-8, 14), (6, 10), (10, 26), (-2, 30), (0, 18),
                       (-4, 40), (14, 20), (-14, 28)):
            pygame.draw.circle(s, (240, 200, 60), (CX + dx, 10 + dy), 3)
            pygame.draw.circle(s, (255, 240, 150),
                               (CX + dx - 1, 10 + dy - 1), 1)
        pygame.draw.ellipse(s, (30, 30, 45, 60), (CX - 20, 50, 40, 6))

    def _food(self, s, key):
        color = ITEMS.get(key, {}).get('color', (180, 120, 70))
        # тарелка
        pygame.draw.ellipse(s, (60, 60, 70, 90), (10, 44, 44, 12))
        pygame.draw.ellipse(s, (208, 204, 192), (10, 40, 44, 12))
        pygame.draw.ellipse(s, (160, 156, 146), (10, 40, 44, 12), 2)
        pygame.draw.ellipse(s, (236, 232, 222), (16, 42, 32, 7))
        if key == 'berry_compote':
            # кружка с компотом
            pygame.draw.rect(s, (150, 158, 170), (22, 14, 20, 28),
                             border_radius=4)
            pygame.draw.rect(s, (200, 208, 218), (22, 14, 20, 28), 2,
                             border_radius=4)
            pygame.draw.ellipse(s, (176, 62, 104), (24, 16, 16, 8))
            for dx, dy in ((28, 26), (34, 24), (30, 33)):
                pygame.draw.circle(s, (214, 72, 112), (dx, dy), 3)
            pygame.draw.arc(s, (172, 180, 190), (40, 20, 12, 14), -1.3, 1.3, 3)
        elif key == 'fried_mushrooms':
            for cx, cy, r in ((22, 42, 7), (32, 37, 8), (42, 43, 6)):
                pygame.draw.ellipse(s, color, (cx - r, cy - r // 2, r * 2, r))
                pygame.draw.ellipse(s, (146, 106, 64),
                                    (cx - r, cy - r // 2, r * 2, r), 1)
                pygame.draw.ellipse(s, (222, 190, 138),
                                    (cx - r + 2, cy - r // 2 + 1,
                                     r + 2, max(2, r // 2)))
        else:
            # кусок жареного мяса с косточкой
            pygame.draw.ellipse(s, color, (16, 26, 32, 22))
            pygame.draw.ellipse(s, (86, 52, 36), (16, 26, 32, 22), 2)
            pygame.draw.ellipse(s, (min(255, color[0] + 36),
                                    min(255, color[1] + 30),
                                    min(255, color[2] + 26)),
                                (20, 29, 16, 10))
            pygame.draw.line(s, (234, 226, 204), (20, 30), (10, 22), 4)
            pygame.draw.circle(s, (234, 226, 204), (10, 21), 3)
        pygame.draw.ellipse(s, (30, 30, 45, 60), (12, 50, 40, 6))

    def _material(self, s, key):
        if key == 'bone':
            # Длинная кость
            pygame.draw.ellipse(s, (240, 235, 215), (CX - 22, CX - 8, 16, 16))
            pygame.draw.ellipse(s, (240, 235, 215), (CX + 6, CX - 8, 16, 16))
            pygame.draw.rect(s, (240, 235, 215), (CX - 14, CX - 4, 28, 8))
            pygame.draw.ellipse(s, (170, 160, 130), (CX - 22, CX - 8, 16, 16), 2)
            pygame.draw.ellipse(s, (170, 160, 130), (CX + 6, CX - 8, 16, 16), 2)
            pygame.draw.line(s, (200, 190, 160), (CX - 6, CX - 2),
                             (CX + 6, CX - 2), 1)
        elif key == 'ogre_leather':
            pygame.draw.ellipse(s, (150, 110, 60), (CX - 22, CX - 16, 44, 34))
            pygame.draw.ellipse(s, (90, 60, 30), (CX - 22, CX - 16, 44, 34), 2)
            for dx in (-12, -4, 4, 12):
                pygame.draw.line(s, (100, 70, 40),
                                 (CX + dx, CX - 12), (CX + dx, CX + 12), 1)
        elif key == 'goblin_ear':
            pygame.draw.polygon(s, (110, 170, 90), [
                (CX - 20, CX + 8), (CX + 18, CX - 16), (CX + 22, CX + 12),
            ])
            pygame.draw.polygon(s, (60, 100, 50), [
                (CX - 20, CX + 8), (CX + 18, CX - 16), (CX + 22, CX + 12),
            ], 2)
            pygame.draw.line(s, (60, 100, 50),
                             (CX - 8, CX + 4), (CX + 10, CX - 4), 2)
        elif key == 'rat_tail':
            pygame.draw.lines(s, (200, 150, 150), False, [
                (CX - 22, CX + 8), (CX - 12, CX - 8), (CX + 4, CX + 4),
                (CX + 14, CX - 10), (CX + 22, CX + 6),
            ], 5)
            pygame.draw.lines(s, (140, 100, 100), False, [
                (CX - 22, CX + 8), (CX - 12, CX - 8), (CX + 4, CX + 4),
                (CX + 14, CX - 10), (CX + 22, CX + 6),
            ], 2)
        elif key == 'orc_tusk':
            pygame.draw.polygon(s, (240, 240, 220), [
                (CX - 10, CX - 18), (CX + 10, CX - 16),
                (CX + 6, CX + 8), (CX, CX + 18), (CX - 6, CX + 8),
            ])
            pygame.draw.polygon(s, (160, 160, 140), [
                (CX - 10, CX - 18), (CX + 10, CX - 16),
                (CX + 6, CX + 8), (CX, CX + 18), (CX - 6, CX + 8),
            ], 2)
        elif key == 'demon_scale':
            pygame.draw.polygon(s, (200, 70, 70), [
                (CX, CX - 20), (CX + 20, CX), (CX, CX + 20), (CX - 20, CX),
            ])
            pygame.draw.polygon(s, (100, 30, 30), [
                (CX, CX - 20), (CX + 20, CX), (CX, CX + 20), (CX - 20, CX),
            ], 2)
            pygame.draw.line(s, (255, 150, 150),
                             (CX - 8, CX - 8), (CX, CX), 2)
        elif key == 'vampire_fang':
            pygame.draw.polygon(s, (250, 250, 250), [
                (CX - 6, CX - 18), (CX + 6, CX - 18),
                (CX + 3, CX + 4), (CX, CX + 18), (CX - 3, CX + 4),
            ])
            pygame.draw.polygon(s, (180, 180, 180), [
                (CX - 6, CX - 18), (CX + 6, CX - 18),
                (CX + 3, CX + 4), (CX, CX + 18), (CX - 3, CX + 4),
            ], 2)
            pygame.draw.circle(s, (200, 30, 40), (CX, CX + 14), 3)
        elif key == 'dragon_scale':
            pygame.draw.polygon(s, (240, 130, 40), [
                (CX, CX - 20), (CX + 20, CX), (CX, CX + 20), (CX - 20, CX),
            ])
            pygame.draw.polygon(s, (140, 70, 20), [
                (CX, CX - 20), (CX + 20, CX), (CX, CX + 20), (CX - 20, CX),
            ], 2)
            pygame.draw.line(s, (255, 200, 100),
                             (CX - 8, CX - 8), (CX, CX - 2), 2)
            pygame.draw.line(s, (255, 200, 100),
                             (CX - 8, CX - 8), (CX - 2, CX - 14), 2)
        elif key == 'golem_core':
            pts = [(CX, CX - 20), (CX + 14, CX - 6), (CX + 14, CX + 8),
                   (CX, CX + 20), (CX - 14, CX + 8), (CX - 14, CX - 6)]
            pygame.draw.polygon(s, (160, 200, 255), pts)
            pygame.draw.polygon(s, (60, 100, 160), pts, 2)
            pygame.draw.line(s, (255, 255, 255),
                             (CX - 4, CX - 10), (CX - 4, CX + 6), 2)
            pygame.draw.circle(s, (255, 255, 255), (CX - 6, CX - 12), 2)
        elif key == 'wraith_essence':
            for dx, dy, r in ((0, 0, 14), (-8, -4, 8), (8, 4, 8), (4, -8, 6)):
                pygame.draw.circle(s, (180, 180, 220), (CX + dx, CX + dy), r)
            pygame.draw.circle(s, (140, 140, 200), (CX, CX), 14, 2)
            pygame.draw.circle(s, (255, 255, 255), (CX - 4, CX - 4), 2)
        elif key.startswith('mana_crystal'):
            color = ITEMS.get(key, {}).get('color', (140, 160, 255))
            size = {'mana_crystal_small': 10,
                    'mana_crystal': 15,
                    'mana_crystal_large': 20}.get(key, 14)
            pts = [(CX, CX - size), (CX + size - 2, CX - size // 3),
                   (CX + size // 2, CX + size), (CX - size // 2, CX + size),
                   (CX - size + 2, CX - size // 3)]
            pygame.draw.polygon(s, color, pts)
            pygame.draw.polygon(s, textures.shade(color, 0.45), pts, 2)
            pygame.draw.line(s, (245, 245, 255),
                             (CX - size // 3, CX - size // 2),
                             (CX - size // 4, CX + size // 3), 2)
            pygame.draw.circle(s, (255, 255, 255), (CX + 2, CX - size // 2), 2)
        elif key == 'magic_powder':
            color = ITEMS.get(key, {}).get('color', (190, 150, 255))
            pygame.draw.ellipse(s, (44, 36, 58), (CX - 20, CX + 4, 40, 12))
            for dx, dy, r in ((-10, 0, 7), (4, -4, 8), (12, 4, 5),
                              (-2, 8, 5)):
                pygame.draw.circle(s, color, (CX + dx, CX + dy), r)
                pygame.draw.circle(s, textures.shade(color, 1.35),
                                   (CX + dx - 2, CX + dy - 2), max(1, r // 2))
        elif key.endswith('_ingot'):
            color = ITEMS.get(key, {}).get('color', (180, 185, 195))
            pts = [(CX - 20, CX + 8), (CX - 12, CX - 4), (CX + 16, CX - 4),
                   (CX + 22, CX + 8)]
            pygame.draw.polygon(s, color, pts)
            pygame.draw.polygon(s, textures.shade(color, 0.45), pts, 2)
            pygame.draw.polygon(s, textures.shade(color, 1.2),
                                [(CX - 12, CX - 4), (CX + 16, CX - 4),
                                 (CX + 12, CX + 1), (CX - 8, CX + 1)])
        elif key in ('wolf_pelt', 'bear_pelt', 'venison', 'slime_gel',
                     'bear_claw', 'boar_tusk'):
            color = ITEMS.get(key, {}).get('color', (160, 140, 120))
            if key.endswith('_pelt'):
                pygame.draw.ellipse(s, color, (CX - 22, CX - 15, 44, 32))
                pygame.draw.ellipse(s, textures.shade(color, 0.5),
                                    (CX - 22, CX - 15, 44, 32), 2)
                for dx in (-12, -2, 8):
                    pygame.draw.line(s, textures.shade(color, 0.7),
                                     (CX + dx, CX - 10), (CX + dx, CX + 10), 1)
            elif key in ('bear_claw', 'boar_tusk'):
                pygame.draw.polygon(s, color, [
                    (CX - 8, CX - 18), (CX + 8, CX - 16),
                    (CX + 4, CX + 6), (CX, CX + 20), (CX - 4, CX + 6)])
                pygame.draw.polygon(s, textures.shade(color, 0.5), [
                    (CX - 8, CX - 18), (CX + 8, CX - 16),
                    (CX + 4, CX + 6), (CX, CX + 20), (CX - 4, CX + 6)], 2)
            elif key == 'slime_gel':
                pygame.draw.ellipse(s, color, (CX - 16, CX - 10, 32, 24))
                pygame.draw.ellipse(s, textures.shade(color, 0.5),
                                    (CX - 16, CX - 10, 32, 24), 2)
                pygame.draw.ellipse(s, (200, 255, 180),
                                    (CX - 9, CX - 6, 10, 7))
            else:
                pygame.draw.ellipse(s, color, (CX - 18, CX - 10, 36, 22))
                pygame.draw.ellipse(s, textures.shade(color, 0.5),
                                    (CX - 18, CX - 10, 36, 22), 2)
                pygame.draw.circle(s, (235, 225, 205), (CX + 12, CX + 8), 4)
        else:
            # Общий фолбэк для новых материалов — цветной кристалл.
            color = ITEMS.get(key, {}).get('color', (180, 180, 190))
            pts = [(CX, CX - 16), (CX + 14, CX - 3), (CX + 8, CX + 15),
                   (CX - 8, CX + 15), (CX - 14, CX - 3)]
            pygame.draw.polygon(s, color, pts)
            pygame.draw.polygon(s, textures.shade(color, 0.5), pts, 2)

    # ════════════════════════ ЗОЛОТО, КНИГИ, СВИТКИ ════════════════════════
    def _gold(self, s, count):
        positions = [
            (CX, CX + 12), (CX - 10, CX + 4), (CX + 10, CX + 4),
            (CX - 6, CX - 4), (CX + 6, CX - 4),
        ]
        for i in range(min(count, len(positions))):
            x, y = positions[i]
            pygame.draw.ellipse(s, (240, 200, 60), (x - 8, y - 3, 16, 8))
            pygame.draw.ellipse(s, (180, 140, 30), (x - 8, y - 3, 16, 8), 1)
            pygame.draw.ellipse(s, (255, 240, 150), (x - 4, y - 2, 8, 4))

    def _book(self, s, key, color):
        """Книга с символом на обложке. Символ зависит от типа книги."""
        # ── Обложка ──
        pygame.draw.polygon(s, color, [
            (CX - 20, 8), (CX + 14, 8), (CX + 20, 14),
            (CX + 20, 52), (CX + 14, 58), (CX - 20, 58),
        ])
        pygame.draw.polygon(s, T.shade(color, 0.5), [
            (CX - 20, 8), (CX + 14, 8), (CX + 20, 14),
            (CX + 20, 52), (CX + 14, 58), (CX - 20, 58),
        ], 2)

        # ── Корешок ──
        pygame.draw.rect(s, T.shade(color, 0.6), (CX - 20, 8, 4, 50))
        # Золотые полоски на корешке
        for yy in (16, 30, 44):
            pygame.draw.line(s, (240, 200, 60),
                             (CX - 20, yy), (CX - 16, yy), 2)

        # ── Страницы (тонкая полоска справа) ──
        pygame.draw.rect(s, (245, 235, 210), (CX + 14, 12, 6, 44))
        pygame.draw.rect(s, (180, 160, 130), (CX + 14, 12, 6, 44), 1)

        # ── Закладка ──
        pygame.draw.polygon(s, (240, 80, 80), [
            (CX + 8, 8), (CX + 13, 8), (CX + 13, 20),
            (CX + 10, 16), (CX + 8, 20),
        ])

        # ── Символ на обложке ──
        self._book_symbol(s, CX - 3, 32, key, color)

        # ── Блик ──
        pygame.draw.line(s, (255, 255, 255, 120),
                         (CX - 16, 12), (CX - 16, 54), 1)
    def _cap(self, s, color, style='leather'):
        pygame.draw.ellipse(s, color, (12, 22, 40, 22))
        pygame.draw.ellipse(s, T.shade(color, 0.55), (12, 22, 40, 22), 2)
        pygame.draw.rect(s, T.shade(color, 0.75), (12, 38, 40, 8))
        pygame.draw.rect(s, T.shade(color, 0.55), (12, 38, 40, 8), 1)
        if style == 'leather':
            for yy in (28, 34):
                pygame.draw.line(s, T.shade(color, 0.7), (16, yy), (48, yy), 1)

    def _helm(self, s, color):
        pts = [(14, 46), (14, 26), (20, 16), (44, 16), (50, 26), (50, 46),
               (44, 50), (20, 50)]
        pygame.draw.polygon(s, color, pts)
        pygame.draw.polygon(s, T.shade(color, 0.5), pts, 2)
        pygame.draw.rect(s, (20, 20, 26), (24, 28, 16, 6))
        pygame.draw.line(s, T.shade(color, 0.35), (24, 34), (40, 34), 1)
        pygame.draw.line(s, T.shade(color, 1.25), (18, 22), (46, 22), 2)
        pygame.draw.polygon(s, (200, 40, 40), [(32, 6), (28, 16), (36, 16)])

    def _crown(self, s, gold):
        pts = [(14, 46), (14, 22), (20, 30), (26, 16), (32, 26),
               (38, 16), (44, 30), (50, 22), (50, 46), (44, 50), (20, 50)]
        pygame.draw.polygon(s, gold, pts)
        pygame.draw.polygon(s, T.shade(gold, 0.5), pts, 2)
        for x in (20, 32, 44):
            pygame.draw.circle(s, (200, 40, 40), (x, 42), 3)
            pygame.draw.circle(s, (255, 200, 200), (x - 1, 41), 1)
        pygame.draw.line(s, T.shade(gold, 1.3), (16, 46), (48, 46), 2)

    def _pants(self, s, color):
        pygame.draw.polygon(s, color, [(18, 10), (46, 10), (46, 22),
                                       (38, 22), (38, 54), (26, 54),
                                       (26, 22), (18, 22)])
        pygame.draw.polygon(s, T.shade(color, 0.5),
                            [(18, 10), (46, 10), (46, 22), (38, 22),
                             (38, 54), (26, 54), (26, 22), (18, 22)], 2)
        pygame.draw.line(s, T.shade(color, 0.7), (18, 22), (46, 22), 2)
        pygame.draw.line(s, T.shade(color, 1.15), (26, 26), (26, 50), 1)
        pygame.draw.line(s, T.shade(color, 1.15), (38, 26), (38, 50), 1)

    def _boots(self, s, color):
        for side in (-1, 1):
            bx = CX + side * 12 - 8
            pygame.draw.polygon(s, color, [
                (bx, 16), (bx + 16, 16), (bx + 16, 38),
                (bx + 20 if side > 0 else bx + 8, 46),
                (bx + 4 if side > 0 else bx - 8, 46), (bx, 38)])
            pygame.draw.polygon(s, T.shade(color, 0.5), [
                (bx, 16), (bx + 16, 16), (bx + 16, 38),
                (bx + 20 if side > 0 else bx + 8, 46),
                (bx + 4 if side > 0 else bx - 8, 46), (bx, 38)], 2)

    def _greaves(self, s, color):
        pygame.draw.polygon(s, color, [(20, 8), (44, 8), (46, 30),
                                       (44, 54), (36, 54), (36, 30),
                                       (28, 30), (28, 54), (20, 54),
                                       (18, 30)])
        pygame.draw.polygon(s, T.shade(color, 0.5),
                            [(20, 8), (44, 8), (46, 30), (44, 54),
                             (36, 54), (36, 30), (28, 30), (28, 54),
                             (20, 54), (18, 30)], 2)
        pygame.draw.circle(s, (200, 200, 220), (26, 20), 2)
        pygame.draw.circle(s, (200, 200, 220), (38, 20), 2)
        pygame.draw.circle(s, (200, 200, 220), (26, 42), 2)
        pygame.draw.circle(s, (200, 200, 220), (38, 42), 2)

    def _book_symbol(self, s, cx, cy, key, color):
        """Рисует символ типа книги в центре обложки."""
        gold = (255, 220, 100)
        light = (255, 240, 180)

        if key == 'book_str':
            # Меч
            pygame.draw.line(s, light, (cx, cy - 12), (cx, cy + 8), 3)
            pygame.draw.polygon(s, light, [
                (cx - 3, cy - 12), (cx + 3, cy - 12), (cx, cy - 16),
            ])
            pygame.draw.line(s, gold, (cx - 6, cy + 4), (cx + 6, cy + 4), 2)
            pygame.draw.line(s, (140, 100, 60), (cx, cy + 8), (cx, cy + 14), 2)

        elif key == 'book_agi':
            # Стрела
            pygame.draw.line(s, light, (cx - 10, cy + 8), (cx + 10, cy - 8), 2)
            pygame.draw.polygon(s, light, [
                (cx + 10, cy - 10), (cx + 12, cy - 6), (cx + 6, cy - 8),
            ])
            pygame.draw.polygon(s, gold, [
                (cx - 10, cy + 6), (cx - 14, cy + 12), (cx - 6, cy + 10),
            ])

        elif key == 'book_vit':
            # Сердце
            pts = [(cx, cy + 10)]
            for angle in range(0, 360, 15):
                rad = 8 + 2 * math.sin(math.radians(angle) * 3)  # ← radians
                px = cx + int(rad * math.cos(math.radians(angle - 90)))
                py = cy + int(rad * math.sin(math.radians(angle - 90)) * 1.2) - 2
                pts.append((px, py))
            pygame.draw.polygon(s, (220, 60, 80), pts)
            pygame.draw.circle(s, light, (cx - 3, cy - 4), 2)

        elif key == 'book_luk':
            # Клевер из четырёх кругов
            for dx, dy in ((-4, -4), (4, -4), (-4, 4), (4, 4)):
                pygame.draw.circle(s, (120, 200, 80), (cx + dx, cy + dy), 5)
                pygame.draw.circle(s, (60, 140, 40), (cx + dx, cy + dy), 5, 1)
            pygame.draw.circle(s, gold, (cx, cy), 2)

        elif key == 'book_def':
            # Щит
            pts = [(cx - 8, cy - 10), (cx + 8, cy - 10),
                   (cx + 8, cy + 4), (cx, cy + 12), (cx - 8, cy + 4)]
            pygame.draw.polygon(s, (180, 200, 240), pts)
            pygame.draw.polygon(s, (80, 100, 150), pts, 2)
            pygame.draw.line(s, gold, (cx, cy - 6), (cx, cy + 8), 2)
            pygame.draw.line(s, gold, (cx - 4, cy - 2), (cx + 4, cy - 2), 2)

        elif key == 'book_double':
            # Два скрещённых меча (редкая)
            for d in (-1, 1):
                pygame.draw.line(s, light,
                                 (cx + d * 10, cy - 10),
                                 (cx - d * 10, cy + 10), 2)
                pygame.draw.polygon(s, light, [
                    (cx + d * 10, cy - 12),
                    (cx + d * 12, cy - 8),
                    (cx + d * 6, cy - 10),
                ])
            pygame.draw.circle(s, gold, (cx, cy), 3)

        elif key == 'book_regen':
            # Крест
            pygame.draw.rect(s, (120, 220, 120), (cx - 3, cy - 12, 6, 24))
            pygame.draw.rect(s, (120, 220, 120), (cx - 12, cy - 3, 24, 6))
            pygame.draw.rect(s, (60, 140, 60),
                             (cx - 3, cy - 12, 6, 24), 1)
            pygame.draw.rect(s, (60, 140, 60),
                             (cx - 12, cy - 3, 24, 6), 1)

        elif key == 'book_vampire':
            # Капля крови + два клыка
            pygame.draw.polygon(s, (220, 30, 60), [
                (cx, cy - 10), (cx + 6, cy + 2), (cx + 4, cy + 8),
                (cx - 4, cy + 8), (cx - 6, cy + 2),
            ])
            pygame.draw.circle(s, (255, 150, 180), (cx - 1, cy), 2)
            # Клыки
            pygame.draw.polygon(s, light, [
                (cx - 3, cy + 10), (cx - 1, cy + 10), (cx - 2, cy + 14),
            ])
            pygame.draw.polygon(s, light, [
                (cx + 1, cy + 10), (cx + 3, cy + 10), (cx + 2, cy + 14),
            ])

        elif key == 'book_thorns':
            # Острые шипы звездой
            for i in range(6):
                angle = i * math.pi / 3
                ex = cx + int(11 * math.cos(angle))
                ey = cy + int(11 * math.sin(angle))
                pygame.draw.polygon(s, (140, 200, 80), [
                    (cx + int(3 * math.cos(angle + 0.3)),
                     cy + int(3 * math.sin(angle + 0.3))),
                    (cx + int(3 * math.cos(angle - 0.3)),
                     cy + int(3 * math.sin(angle - 0.3))),
                    (ex, ey),
                ])
            pygame.draw.circle(s, (200, 240, 120), (cx, cy), 3)

        elif key == 'book_greedy':
            # Монета с $
            pygame.draw.circle(s, gold, (cx, cy), 10)
            pygame.draw.circle(s, (180, 130, 20), (cx, cy), 10, 2)
            pygame.draw.circle(s, (255, 240, 150), (cx - 3, cy - 3), 2)
            pygame.draw.line(s, (140, 90, 20), (cx, cy - 6), (cx, cy + 6), 2)
            pygame.draw.line(s, (140, 90, 20), (cx - 3, cy - 2), (cx + 3, cy - 2), 2)
            pygame.draw.line(s, (140, 90, 20), (cx - 3, cy + 2), (cx + 3, cy + 2), 2)

        else:
            # Fallback — маленький кружок как раньше
            pygame.draw.circle(s, (255, 240, 150), (cx, cy), 2)

    def _scroll(self, s, tint):
        # Свиток
        pygame.draw.rect(s, (245, 235, 210), (CX - 14, 14, 28, 36))
        pygame.draw.rect(s, (180, 160, 120), (CX - 14, 14, 28, 36), 2)
        # Верхний рулон
        pygame.draw.ellipse(s, (200, 180, 130), (CX - 18, 8, 36, 10))
        pygame.draw.ellipse(s, (140, 110, 60), (CX - 18, 8, 36, 10), 2)
        # Нижний рулон
        pygame.draw.ellipse(s, (200, 180, 130), (CX - 18, 46, 36, 10))
        pygame.draw.ellipse(s, (140, 110, 60), (CX - 18, 46, 36, 10), 2)
        # Строки
        for yy in range(22, 44, 4):
            pygame.draw.line(s, (140, 110, 80),
                             (CX - 8, yy), (CX + 8, yy), 1)
        # Магический символ
        pygame.draw.circle(s, tint, (CX, 34), 4)
        pygame.draw.circle(s, (255, 255, 200), (CX - 1, 33), 1)

    def _rune(self, s, tint):
        """Каменная руна с высеченным светящимся знаком."""
        # Камень
        pts = [(CX - 18, CX + 14), (CX - 20, CX - 8), (CX - 6, CX - 20),
               (CX + 14, CX - 16), (CX + 21, CX + 4), (CX + 10, CX + 18)]
        pygame.draw.polygon(s, (92, 90, 96), pts)
        pygame.draw.polygon(s, (52, 50, 58), pts, 3)
        pygame.draw.polygon(s, (126, 124, 132),
                            [(CX - 16, CX + 10), (CX - 17, CX - 6),
                             (CX - 6, CX - 16), (CX + 10, CX - 13)])
        # Светящийся знак
        pygame.draw.circle(s, tint, (CX, CX), 11, 2)
        pygame.draw.line(s, tint, (CX, CX - 8), (CX, CX + 8), 2)
        pygame.draw.line(s, tint, (CX - 8, CX - 2), (CX + 8, CX + 2), 2)
        pygame.draw.circle(s, (255, 255, 255), (CX - 4, CX - 4), 2)

    # ════════════════════════ ИГРОК — 4 КЛАССА ════════════════════════
