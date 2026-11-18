"""Auto-generated mixin from render/icon_renderer.py."""

import pygame

from core.config import TILE
from render import textures
from render.icons._base import CANVAS, CX

class PlayerMixin:
    """Спрайты классов игрока (воин, разбойник, паладин, авантюрист)."""

    def draw_player(self, class_key, px, py, size=TILE):
        ck = (class_key, size)
        if ck not in self._player_cache:
            self._player_cache[ck] = self._make_player_surf(class_key, size)
        self.screen.blit(self._player_cache[ck], (px, py))

    def _make_player_surf(self, class_key, size):
        s = pygame.Surface((CANVAS, CANVAS), pygame.SRCALPHA)
        if class_key == 'warrior':
            self._draw_warrior(s)
        elif class_key == 'assassin':
            self._draw_assassin(s)
        elif class_key == 'rogue':
            self._draw_rogue(s)
        elif class_key == 'paladin':
            self._draw_paladin(s)
        else:
            self._draw_adventurer(s)
        if size != CANVAS:
            s = pygame.transform.smoothscale(s, (size, size))
        return textures.finish(s, shadow=True)

    def _draw_warrior(self, s):
        # Ноги (тяжёлые)
        pygame.draw.rect(s, (60, 60, 70), (CX - 12, 44, 10, 16))
        pygame.draw.rect(s, (60, 60, 70), (CX + 2, 44, 10, 16))
        pygame.draw.rect(s, (90, 90, 100), (CX - 12, 44, 10, 16), 2)
        pygame.draw.rect(s, (90, 90, 100), (CX + 2, 44, 10, 16), 2)
        pygame.draw.rect(s, (40, 40, 45), (CX - 13, 56, 12, 6))
        pygame.draw.rect(s, (40, 40, 45), (CX + 1, 56, 12, 6))
        # Тело
        pygame.draw.polygon(s, (140, 140, 160), [
            (CX - 16, 22), (CX + 16, 22), (CX + 18, 48), (CX - 18, 48),
        ])
        pygame.draw.polygon(s, (200, 200, 220), [
            (CX - 16, 22), (CX + 16, 22), (CX + 18, 48), (CX - 18, 48),
        ], 2)
        # Красный кант и V-вырез
        pygame.draw.line(s, (200, 40, 40),
                         (CX - 16, 24), (CX + 16, 24), 2)
        pygame.draw.polygon(s, (180, 180, 200), [
            (CX, 24), (CX - 10, 40), (CX + 10, 40),
        ])
        # Голова в шлеме
        pygame.draw.rect(s, (180, 180, 200), (CX - 10, 6, 20, 18),
                         border_radius=4)
        pygame.draw.rect(s, (60, 60, 70), (CX - 10, 6, 20, 18), 2,
                         border_radius=4)
        pygame.draw.rect(s, (20, 20, 30), (CX - 6, 12, 12, 5))
        pygame.draw.line(s, (100, 100, 110), (CX - 6, 14), (CX + 6, 14), 1)
        # Плюмаж
        pygame.draw.polygon(s, (200, 40, 40),
                            [(CX, 6), (CX - 5, -2), (CX + 5, -2)])
        pygame.draw.polygon(s, (255, 100, 100),
                            [(CX, 4), (CX - 2, 0), (CX + 2, 0)])
        # Меч
        pygame.draw.polygon(s, (220, 220, 240), [
            (CX + 22, 4), (CX + 26, 4), (CX + 26, 46),
            (CX + 24, 50), (CX + 22, 46),
        ])
        pygame.draw.polygon(s, (140, 140, 170), [
            (CX + 22, 4), (CX + 26, 4), (CX + 26, 46),
            (CX + 24, 50), (CX + 22, 46),
        ], 2)
        pygame.draw.rect(s, (200, 160, 40), (CX + 18, 46, 12, 4))
        pygame.draw.circle(s, (240, 200, 60), (CX + 24, 54), 3)
        # Щит
        pygame.draw.circle(s, (140, 100, 50), (CX - 22, 36), 11)
        pygame.draw.circle(s, (80, 50, 20), (CX - 22, 36), 11, 2)
        pygame.draw.circle(s, (200, 160, 100), (CX - 22, 36), 7)
        pygame.draw.circle(s, (140, 100, 50), (CX - 22, 36), 7, 1)
        pygame.draw.circle(s, (200, 160, 40), (CX - 22, 36), 3)

    def _draw_assassin(self, s):
        # Ноги
        pygame.draw.rect(s, (30, 25, 40), (CX - 10, 46, 8, 14))
        pygame.draw.rect(s, (30, 25, 40), (CX + 2, 46, 8, 14))
        pygame.draw.rect(s, (15, 10, 20), (CX - 10, 46, 8, 14), 2)
        pygame.draw.rect(s, (15, 10, 20), (CX + 2, 46, 8, 14), 2)
        # Тело (тёмный плащ)
        pygame.draw.polygon(s, (45, 35, 60), [
            (CX - 14, 24), (CX + 14, 24), (CX + 16, 46), (CX - 16, 46),
        ])
        pygame.draw.polygon(s, (15, 10, 20), [
            (CX - 14, 24), (CX + 14, 24), (CX + 16, 46), (CX - 16, 46),
        ], 2)
        # Пояс с пряжкой
        pygame.draw.rect(s, (90, 60, 30), (CX - 14, 36, 28, 4))
        pygame.draw.rect(s, (200, 160, 40), (CX - 3, 36, 6, 4))
        # Голова в капюшоне
        pygame.draw.ellipse(s, (45, 35, 60), (CX - 12, 6, 24, 20))
        pygame.draw.ellipse(s, (15, 10, 20), (CX - 12, 6, 24, 20), 2)
        # Лицо (тёмное)
        pygame.draw.ellipse(s, (10, 5, 15), (CX - 8, 10, 16, 14))
        # Глаза (яркие)
        pygame.draw.circle(s, (220, 30, 60), (CX - 3, 16), 2)
        pygame.draw.circle(s, (220, 30, 60), (CX + 3, 16), 2)
        # Капюшон закрывает
        pygame.draw.polygon(s, (45, 35, 60), [
            (CX - 10, 6), (CX + 10, 6), (CX, 12),
        ])
        # Кинжал 1
        pygame.draw.polygon(s, (200, 215, 235), [
            (CX + 20, 20), (CX + 24, 20), (CX + 24, 40),
            (CX + 22, 44), (CX + 20, 40),
        ])
        pygame.draw.polygon(s, (120, 140, 170), [
            (CX + 20, 20), (CX + 24, 20), (CX + 24, 40),
            (CX + 22, 44), (CX + 20, 40),
        ], 1)
        pygame.draw.rect(s, (90, 60, 30), (CX + 20, 44, 4, 6))
        # Кинжал 2
        pygame.draw.polygon(s, (200, 215, 235), [
            (CX - 24, 20), (CX - 20, 20), (CX - 20, 40),
            (CX - 22, 44), (CX - 24, 40),
        ])
        pygame.draw.polygon(s, (120, 140, 170), [
            (CX - 24, 20), (CX - 20, 20), (CX - 20, 40),
            (CX - 22, 44), (CX - 24, 40),
        ], 1)
        pygame.draw.rect(s, (90, 60, 30), (CX - 24, 44, 4, 6))

    def _draw_rogue(self, s):
        # Ноги (быстрая стойка)
        pygame.draw.rect(s, (60, 90, 50), (CX - 11, 46, 8, 14))
        pygame.draw.rect(s, (60, 90, 50), (CX + 3, 46, 8, 14))
        pygame.draw.rect(s, (30, 50, 25), (CX - 11, 46, 8, 14), 2)
        pygame.draw.rect(s, (30, 50, 25), (CX + 3, 46, 8, 14), 2)
        # Тело (зелёная кожа)
        pygame.draw.polygon(s, (80, 120, 70), [
            (CX - 13, 24), (CX + 13, 24), (CX + 15, 46), (CX - 15, 46),
        ])
        pygame.draw.polygon(s, (40, 70, 35), [
            (CX - 13, 24), (CX + 13, 24), (CX + 15, 46), (CX - 15, 46),
        ], 2)
        # Ремни
        pygame.draw.line(s, (90, 60, 30), (CX - 12, 30), (CX + 12, 40), 2)
        pygame.draw.line(s, (90, 60, 30), (CX - 12, 42), (CX + 12, 32), 2)
        # Голова в капюшоне (зелёный)
        pygame.draw.ellipse(s, (80, 120, 70), (CX - 11, 6, 22, 20))
        pygame.draw.ellipse(s, (40, 70, 35), (CX - 11, 6, 22, 20), 2)
        # Лицо (маска)
        pygame.draw.ellipse(s, (230, 200, 160), (CX - 7, 12, 14, 10))
        pygame.draw.ellipse(s, (40, 70, 35), (CX - 7, 12, 14, 10), 1)
        # Глаза
        pygame.draw.circle(s, (20, 20, 30), (CX - 3, 16), 2)
        pygame.draw.circle(s, (20, 20, 30), (CX + 3, 16), 2)
        # Лук
        pygame.draw.arc(s, (150, 100, 50),
                        (CX + 16, 16, 16, 34), -1.4, 1.4, 3)
        pygame.draw.line(s, (200, 180, 130),
                         (CX + 24, 18), (CX + 24, 48), 1)
        # Стрела
        pygame.draw.line(s, (150, 100, 50),
                         (CX + 26, 20), (CX + 26, 46), 2)
        pygame.draw.polygon(s, (200, 200, 210),
                            [(CX + 24, 18), (CX + 28, 18), (CX + 26, 14)])
        # Кинжал на поясе
        pygame.draw.polygon(s, (200, 215, 235), [
            (CX - 22, 38), (CX - 18, 38), (CX - 18, 50), (CX - 20, 54),
            (CX - 22, 50),
        ])

    def _draw_paladin(self, s):
        # Ноги (золотая броня)
        pygame.draw.rect(s, (180, 160, 60), (CX - 12, 44, 10, 16))
        pygame.draw.rect(s, (180, 160, 60), (CX + 2, 44, 10, 16))
        pygame.draw.rect(s, (240, 220, 120), (CX - 12, 44, 10, 16), 2)
        pygame.draw.rect(s, (240, 220, 120), (CX + 2, 44, 10, 16), 2)
        pygame.draw.rect(s, (140, 110, 30), (CX - 13, 56, 12, 6))
        pygame.draw.rect(s, (140, 110, 30), (CX + 1, 56, 12, 6))
        # Тело (золотая броня)
        pygame.draw.polygon(s, (240, 210, 100), [
            (CX - 16, 22), (CX + 16, 22), (CX + 18, 48), (CX - 18, 48),
        ])
        pygame.draw.polygon(s, (180, 140, 40), [
            (CX - 16, 22), (CX + 16, 22), (CX + 18, 48), (CX - 18, 48),
        ], 2)
        # Символ (крест)
        pygame.draw.line(s, (255, 255, 240),
                         (CX, 26), (CX, 44), 3)
        pygame.draw.line(s, (255, 255, 240),
                         (CX - 6, 32), (CX + 6, 32), 3)
        # Нимб
        halo_y = 2
        pygame.draw.arc(s, (255, 240, 150),
                        (CX - 12, halo_y - 4, 24, 12), 0, 3.14, 3)
        # Голова в шлеме с крестом
        pygame.draw.rect(s, (240, 210, 100), (CX - 10, 6, 20, 18),
                         border_radius=4)
        pygame.draw.rect(s, (180, 140, 40), (CX - 10, 6, 20, 18), 2,
                         border_radius=4)
        pygame.draw.rect(s, (60, 50, 20), (CX - 6, 12, 12, 5))
        pygame.draw.line(s, (255, 240, 150), (CX - 6, 14), (CX + 6, 14), 1)
        # Крест на шлеме
        pygame.draw.line(s, (200, 60, 60), (CX, 2), (CX, 8), 2)
        pygame.draw.line(s, (200, 60, 60), (CX - 3, 4), (CX + 3, 4), 2)
        # Молот
        pygame.draw.rect(s, (90, 60, 30), (CX + 22, 20, 6, 36))
        pygame.draw.rect(s, (50, 30, 15), (CX + 22, 20, 6, 36), 1)
        pygame.draw.rect(s, (240, 210, 100),
                         (CX + 14, 12, 22, 14), border_radius=2)
        pygame.draw.rect(s, (180, 140, 40),
                         (CX + 14, 12, 22, 14), 2, border_radius=2)
        # Щит-крест
        pygame.draw.polygon(s, (240, 210, 100), [
            (CX - 24, 24), (CX - 16, 24), (CX - 16, 40), (CX - 20, 46),
            (CX - 24, 40),
        ])
        pygame.draw.polygon(s, (180, 140, 40), [
            (CX - 24, 24), (CX - 16, 24), (CX - 16, 40), (CX - 20, 46),
            (CX - 24, 40),
        ], 2)
        pygame.draw.line(s, (200, 60, 60),
                         (CX - 20, 28), (CX - 20, 40), 2)
        pygame.draw.line(s, (200, 60, 60),
                         (CX - 24, 32), (CX - 16, 32), 2)

    def _draw_adventurer(self, s):
        # Ноги
        pygame.draw.rect(s, (60, 80, 50), (CX - 10, 46, 8, 14))
        pygame.draw.rect(s, (60, 80, 50), (CX + 2, 46, 8, 14))
        pygame.draw.rect(s, (40, 60, 35), (CX - 10, 46, 8, 14), 2)
        pygame.draw.rect(s, (40, 60, 35), (CX + 2, 46, 8, 14), 2)
        # Тело
        pygame.draw.polygon(s, (80, 140, 80), [
            (CX - 12, 24), (CX + 12, 24), (CX + 14, 46), (CX - 14, 46),
        ])
        pygame.draw.polygon(s, (40, 80, 40), [
            (CX - 12, 24), (CX + 12, 24), (CX + 14, 46), (CX - 14, 46),
        ], 2)
        pygame.draw.rect(s, (90, 60, 30), (CX - 12, 34, 24, 3))
        # Голова
        pygame.draw.circle(s, (240, 210, 170), (CX, 14), 9)
        pygame.draw.circle(s, (180, 150, 110), (CX, 14), 9, 2)
        pygame.draw.circle(s, (20, 20, 20), (CX - 3, 14), 2)
        pygame.draw.circle(s, (20, 20, 20), (CX + 3, 14), 2)
        pygame.draw.arc(s, (80, 50, 30), (CX - 8, 4, 16, 12), 0, 3.14, 2)
        # Меч
        pygame.draw.polygon(s, (200, 220, 240), [
            (CX + 20, 18), (CX + 24, 18), (CX + 24, 44),
            (CX + 22, 48), (CX + 20, 44),
        ])
        pygame.draw.line(s, (240, 180, 60),
                         (CX + 16, 44), (CX + 28, 46), 2)
        # Щит
        pygame.draw.ellipse(s, (160, 130, 90), (CX - 26, 32, 14, 18))
        pygame.draw.ellipse(s, (100, 80, 50), (CX - 26, 32, 14, 18), 2)
        pygame.draw.line(s, (220, 190, 100),
                         (CX - 19, 35), (CX - 19, 48), 2)

    # ════════════════════════ ИКОНКИ КЛАССОВ (для слотов сохранения) ════════════════════════
