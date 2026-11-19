"""Auto-generated mixin from render/icon_renderer.py."""

import pygame

from render import textures
from render.icons._base import CANVAS

class NPCMixin:
    """Городские NPC, торговцы, старейшина, доска поручений, UI-иконки."""

    def draw_city_npc(self, npc_type, px, py, size=CANVAS):
        ck = (npc_type, size)
        if ck not in self._city_npc_cache:
            s = pygame.Surface((CANVAS, CANVAS), pygame.SRCALPHA)
            self._draw_city_npc_64(s, npc_type)
            if size != CANVAS:
                s = pygame.transform.smoothscale(s, (size, size))
            self._city_npc_cache[ck] = textures.finish(s, shadow=True)
        self.screen.blit(self._city_npc_cache[ck], (px, py))

    def _draw_city_npc_64(self, s, npc_type):
        # ноги
        pygame.draw.rect(s, (54, 44, 34), (26, 50, 5, 9))
        pygame.draw.rect(s, (54, 44, 34), (33, 50, 5, 9))
        if npc_type == 'guard':
            # кольчуга и нагрудник
            pygame.draw.polygon(s, (120, 126, 142), [(22, 26), (42, 26),
                                                     (45, 52), (19, 52)])
            pygame.draw.polygon(s, (78, 84, 100), [(22, 26), (42, 26),
                                                   (45, 52), (19, 52)], 2)
            pygame.draw.rect(s, (156, 162, 178), (25, 30, 14, 14))
            pygame.draw.rect(s, (98, 104, 120), (25, 30, 14, 14), 1)
            # наплечники
            pygame.draw.circle(s, (140, 146, 162), (20, 29), 6)
            pygame.draw.circle(s, (140, 146, 162), (44, 29), 6)
            # голова и шлем
            pygame.draw.circle(s, (232, 200, 160), (32, 18), 9)
            pygame.draw.arc(s, (150, 156, 172), (21, 5, 22, 22), 3.14, 6.28, 4)
            pygame.draw.rect(s, (150, 156, 172), (21, 14, 22, 3))
            pygame.draw.line(s, (206, 210, 220), (32, 4), (32, 10), 2)
            pygame.draw.circle(s, (24, 20, 16), (29, 19), 1)
            pygame.draw.circle(s, (24, 20, 16), (35, 19), 1)
            # копьё и щит
            pygame.draw.line(s, (122, 92, 58), (14, 12), (14, 58), 3)
            pygame.draw.polygon(s, (210, 214, 224), [(14, 4), (10, 12), (18, 12)])
            pygame.draw.circle(s, (100, 70, 40), (46, 38), 11)
            pygame.draw.circle(s, (140, 104, 62), (46, 38), 11, 2)
            pygame.draw.circle(s, (200, 170, 90), (46, 38), 4)
        elif npc_type == 'blacksmith':
            # кожаный передник поверх рубахи
            pygame.draw.polygon(s, (104, 70, 42), [(22, 24), (42, 24),
                                                   (45, 56), (19, 56)])
            pygame.draw.polygon(s, (66, 44, 26), [(22, 24), (42, 24),
                                                  (45, 56), (19, 56)], 2)
            pygame.draw.rect(s, (150, 116, 78), (27, 28, 10, 22))
            # руки
            pygame.draw.circle(s, (226, 176, 132), (18, 44), 5)
            pygame.draw.circle(s, (226, 176, 132), (46, 44), 5)
            # голова, борода
            pygame.draw.circle(s, (228, 184, 140), (32, 18), 9)
            pygame.draw.polygon(s, (74, 48, 30), [(24, 23), (40, 23), (32, 37)])
            pygame.draw.circle(s, (24, 18, 12), (29, 18), 1)
            pygame.draw.circle(s, (24, 18, 12), (35, 18), 1)
            # молот
            pygame.draw.line(s, (96, 70, 44), (48, 34), (54, 56), 3)
            pygame.draw.rect(s, (128, 132, 142), (42, 22, 16, 10))
        elif npc_type == 'alchemist':
            # мантия
            pygame.draw.polygon(s, (92, 74, 138), [(21, 25), (43, 25),
                                                   (47, 56), (17, 56)])
            pygame.draw.polygon(s, (60, 44, 96), [(21, 25), (43, 25),
                                                  (47, 56), (17, 56)], 2)
            pygame.draw.rect(s, (196, 170, 80), (21, 44, 24, 3))
            # голова и остроконечная шляпа
            pygame.draw.circle(s, (232, 206, 168), (32, 18), 9)
            pygame.draw.ellipse(s, (74, 54, 112), (14, 10, 36, 8))
            pygame.draw.polygon(s, (98, 76, 148), [(24, 12), (40, 12), (36, 0),
                                                   (28, 0)])
            pygame.draw.circle(s, (24, 18, 12), (29, 19), 1)
            pygame.draw.circle(s, (24, 18, 12), (35, 19), 1)
            # склянка
            pygame.draw.rect(s, (166, 210, 220), (46, 42, 8, 10))
            pygame.draw.rect(s, (196, 240, 240), (48, 40, 4, 3))
            pygame.draw.circle(s, (120, 220, 140), (50, 48), 3)
        else:   # innkeeper
            pygame.draw.polygon(s, (126, 96, 58), [(21, 25), (43, 25),
                                                   (46, 56), (18, 56)])
            pygame.draw.polygon(s, (86, 62, 38), [(21, 25), (43, 25),
                                                  (46, 56), (18, 56)], 2)
            pygame.draw.rect(s, (206, 196, 166), (26, 26, 12, 20))
            pygame.draw.rect(s, (118, 84, 48), (21, 45, 24, 4))
            pygame.draw.circle(s, (228, 184, 140), (32, 18), 10)
            pygame.draw.polygon(s, (110, 76, 46), [(23, 22), (41, 22), (32, 30)])
            pygame.draw.circle(s, (24, 18, 12), (29, 18), 1)
            pygame.draw.circle(s, (24, 18, 12), (35, 18), 1)
            # кружка
            pygame.draw.rect(s, (140, 140, 150), (44, 38, 10, 13))
            pygame.draw.rect(s, (198, 198, 206), (44, 38, 10, 3))
            pygame.draw.arc(s, (150, 150, 160), (50, 41, 8, 7), -1.2, 1.2, 2)

    def draw_merchant(self, px, py, size=CANVAS):
        ck = (size,)
        if ck not in self._merchant_cache:
            s = pygame.Surface((CANVAS, CANVAS), pygame.SRCALPHA)
            self._draw_merchant_64(s)
            if size != CANVAS:
                s = pygame.transform.smoothscale(s, (size, size))
            self._merchant_cache[ck] = textures.finish(s, shadow=True)
        self.screen.blit(self._merchant_cache[ck], (px, py))

    def _draw_merchant_64(self, s):
        # тень
        pygame.draw.ellipse(s, (0, 0, 0, 90), (14, 54, 36, 8))
        # ноги
        pygame.draw.rect(s, (58, 42, 28), (26, 48, 5, 10))
        pygame.draw.rect(s, (58, 42, 28), (33, 48, 5, 10))
        # роба: уже в плечах, шире к подолу
        robe = [(23, 24), (41, 24), (46, 56), (18, 56)]
        pygame.draw.polygon(s, (112, 74, 40), robe)
        pygame.draw.polygon(s, (70, 44, 20), robe, 2)
        # объём робы
        pygame.draw.polygon(s, (138, 94, 48), [(23, 24), (32, 24), (32, 56), (18, 56)])
        # рукава и руки
        pygame.draw.polygon(s, (128, 86, 44), [(23, 26), (17, 44), (22, 47), (27, 32)])
        pygame.draw.polygon(s, (128, 86, 44), [(41, 26), (47, 44), (42, 47), (37, 32)])
        pygame.draw.circle(s, (232, 200, 160), (20, 48), 3)
        pygame.draw.circle(s, (232, 200, 160), (44, 48), 3)
        # пояс и пряжка
        pygame.draw.rect(s, (240, 200, 60), (21, 45, 22, 3))
        pygame.draw.rect(s, (170, 130, 30), (30, 44, 4, 5))
        # кошель
        pygame.draw.circle(s, (150, 100, 50), (46, 51), 6)
        pygame.draw.circle(s, (196, 156, 88), (46, 51), 6, 2)
        pygame.draw.circle(s, (240, 200, 60), (46, 49), 2)
        # голова и борода
        pygame.draw.circle(s, (236, 206, 166), (32, 20), 9)
        pygame.draw.circle(s, (176, 146, 108), (32, 20), 9, 1)
        pygame.draw.polygon(s, (214, 214, 214), [(25, 24), (39, 24), (32, 34)])
        pygame.draw.circle(s, (24, 18, 12), (29, 19), 1)
        pygame.draw.circle(s, (24, 18, 12), (35, 19), 1)
        # шляпа
        pygame.draw.ellipse(s, (198, 158, 78), (13, 9, 38, 8))
        pygame.draw.ellipse(s, (132, 94, 38), (13, 9, 38, 8), 2)
        pygame.draw.ellipse(s, (224, 186, 96), (21, 1, 22, 12))
        pygame.draw.ellipse(s, (132, 94, 38), (21, 1, 22, 12), 2)

    def draw_elder(self, px, py, size=CANVAS):
        ck = (size,)
        if ck not in self._elder_cache:
            s = pygame.Surface((CANVAS, CANVAS), pygame.SRCALPHA)
            self._draw_elder_64(s)
            if size != CANVAS:
                s = pygame.transform.smoothscale(s, (size, size))
            self._elder_cache[ck] = textures.finish(s, shadow=True)
        self.screen.blit(self._elder_cache[ck], (px, py))

    def _draw_elder_64(self, s):
        # Мантия (фиолетовая)
        pygame.draw.polygon(s, (100, 80, 140), [
            (10, 58), (16, 22), (48, 22), (54, 58), (32, 64),
        ])
        pygame.draw.polygon(s, (60, 40, 90), [
            (10, 58), (16, 22), (48, 22), (54, 58), (32, 64),
        ], 2)
        # Пояс
        pygame.draw.rect(s, (200, 160, 60), (18, 46, 28, 3))
        # Голова
        pygame.draw.circle(s, (235, 205, 165), (32, 18), 11)
        pygame.draw.circle(s, (180, 150, 110), (32, 18), 11, 2)
        # Борода (длинная, белая)
        pygame.draw.polygon(s, (240, 240, 240), [
            (24, 24), (40, 24), (32, 44),
        ])
        pygame.draw.polygon(s, (180, 180, 180), [
            (24, 24), (40, 24), (32, 44),
        ], 1)
        # Глаза
        pygame.draw.circle(s, (20, 20, 20), (28, 18), 2)
        pygame.draw.circle(s, (20, 20, 20), (36, 18), 2)
        # Волосы / капюшон
        pygame.draw.arc(s, (100, 80, 140), (20, 6, 24, 20), 0, 3.14, 3)
        # Посох с камнем
        pygame.draw.line(s, (120, 80, 50), (54, 18), (54, 58), 3)
        pygame.draw.circle(s, (200, 100, 255), (54, 14), 5)
        pygame.draw.circle(s, (255, 200, 255), (52, 12), 2)

    def draw_quest_board(self, px, py, size=CANVAS):
        ck = ('board', size)
        if not hasattr(self, '_board_cache'):
            self._board_cache = {}
        if ck not in self._board_cache:
            s = pygame.Surface((CANVAS, CANVAS), pygame.SRCALPHA)
            pygame.draw.rect(s, (140, 95, 55), (10, 20, 44, 32))
            pygame.draw.rect(s, (80, 55, 30), (10, 20, 44, 32), 3)
            for y in range(22, 50, 4):
                pygame.draw.line(s, (110, 75, 40), (12, y), (52, y), 1)
            pygame.draw.rect(s, (240, 230, 200), (16, 24, 32, 24))
            pygame.draw.rect(s, (180, 170, 140), (16, 24, 32, 24), 1)
            for y in range(28, 45, 4):
                pygame.draw.line(s, (100, 90, 70), (20, y), (44, y), 1)
            pygame.draw.circle(s, (200, 50, 50), (16, 26), 2)
            pygame.draw.circle(s, (200, 50, 50), (48, 26), 2)
            pygame.draw.rect(s, (90, 60, 35), (14, 52, 4, 10))
            pygame.draw.rect(s, (90, 60, 35), (46, 52, 4, 10))
            if size != CANVAS:
                s = pygame.transform.smoothscale(s, (size, size))
            self._board_cache[ck] = textures.finish(s, shadow=True)
        self.screen.blit(self._board_cache[ck], (px, py))

    # ════════════════════════ ИКОНКИ ТУЛБАРА ════════════════════════
    def draw_toolbar_icon(self, name, x, y, size=44, hover=False):
        s = pygame.Surface((size, size), pygame.SRCALPHA)
        cx = cy = size // 2
        col = (255, 240, 180) if hover else (220, 220, 235)
        dim = (180, 160, 90) if hover else (140, 140, 155)
        k = size / 44

        if name == 'inventory':
            bw, bh = int(28 * k), int(20 * k)
            pygame.draw.rect(s, (60, 50, 35),
                             (cx - bw // 2, cy - bh // 2 + 4, bw, bh),
                             border_radius=int(4 * k))
            pygame.draw.rect(s, col,
                             (cx - bw // 2, cy - bh // 2 + 4, bw, bh),
                             int(2 * k), border_radius=int(4 * k))
            lw, lh = int(20 * k), int(8 * k)
            pygame.draw.rect(s, (80, 65, 40),
                             (cx - lw // 2, cy - bh // 2 - lh + 6, lw, lh),
                             border_radius=int(3 * k))
            pygame.draw.rect(s, col,
                             (cx - lw // 2, cy - bh // 2 - lh + 6, lw, lh),
                             int(2 * k), border_radius=int(3 * k))
            pk = int(8 * k)
            pygame.draw.rect(s, dim,
                             (cx - pk // 2, cy + 4, pk, int(6 * k)),
                             border_radius=int(2 * k))
        elif name == 'craft':
            # Молот
            pygame.draw.rect(s, (120, 90, 50),
                             (cx - int(3 * k), cy - int(2 * k),
                              int(6 * k), int(20 * k)))
            pygame.draw.rect(s, (60, 40, 20),
                             (cx - int(3 * k), cy - int(2 * k),
                              int(6 * k), int(20 * k)), 1)
            hw, hh = int(24 * k), int(11 * k)
            pygame.draw.rect(s, (90, 90, 105),
                             (cx - hw // 2, cy - int(14 * k), hw, hh),
                             border_radius=int(3 * k))
            pygame.draw.rect(s, col,
                             (cx - hw // 2, cy - int(14 * k), hw, hh),
                             int(2 * k), border_radius=int(3 * k))
            pygame.draw.line(s, (180, 180, 200),
                             (cx - hw // 2 + 3, cy - int(11 * k)),
                             (cx + hw // 2 - 3, cy - int(11 * k)),
                             int(1 * k))
        elif name == 'skills':
            # Три узла ветки навыков
            pygame.draw.line(s, dim, (cx, cy + int(10 * k)),
                             (cx, cy - int(2 * k)), int(2 * k))
            pygame.draw.line(s, dim, (cx, cy - int(2 * k)),
                             (cx - int(11 * k), cy - int(13 * k)), int(2 * k))
            pygame.draw.line(s, dim, (cx, cy - int(2 * k)),
                             (cx + int(11 * k), cy - int(13 * k)), int(2 * k))
            for nx, ny in ((cx, cy + int(12 * k)),
                           (cx - int(11 * k), cy - int(15 * k)),
                           (cx + int(11 * k), cy - int(15 * k))):
                pygame.draw.circle(s, (40, 34, 24), (nx, ny), int(6 * k))
                pygame.draw.circle(s, col, (nx, ny), int(6 * k), int(2 * k))
        elif name == 'stats':
            bar_w = int(7 * k)
            gap = int(4 * k)
            heights = [int(10 * k), int(16 * k), int(13 * k)]
            total = 3 * bar_w + 2 * gap
            start = cx - total // 2
            for i, hgt in enumerate(heights):
                bx = start + i * (bar_w + gap)
                by = cy + int(8 * k) - hgt
                pygame.draw.rect(s, dim, (bx, by, bar_w, hgt))
                pygame.draw.rect(s, col, (bx, by, bar_w, hgt), int(2 * k))
        elif name == 'quests':
            sw, sh = int(20 * k), int(24 * k)
            sx, sy = cx - sw // 2 - int(2 * k), cy - sh // 2
            pygame.draw.rect(s, (220, 210, 175), (sx, sy, sw, sh),
                             border_radius=int(2 * k))
            pygame.draw.rect(s, col, (sx, sy, sw, sh),
                             int(2 * k), border_radius=int(2 * k))
            for i in range(4):
                yy = sy + int(5 * k) + i * int(5 * k)
                pygame.draw.line(s, (120, 100, 80),
                                 (sx + int(3 * k), yy),
                                 (sx + sw - int(3 * k), yy), int(1 * k))
            px0, py0 = cx + int(4 * k), cy - int(8 * k)
            pygame.draw.polygon(s, (240, 210, 100), [
                (px0, py0), (px0 + int(8 * k), py0 + int(16 * k)),
                (px0 + int(2 * k), py0 + int(14 * k)),
            ])
        elif name == 'bigmap':
            # Сложенная карта с дорожкой и отметкой
            mw, mh = int(28 * k), int(20 * k)
            mx, my = cx - mw // 2, cy - mh // 2
            pygame.draw.rect(s, (70, 62, 44), (mx, my, mw, mh),
                             border_radius=int(2 * k))
            pygame.draw.rect(s, col, (mx, my, mw, mh),
                             int(2 * k), border_radius=int(2 * k))
            pygame.draw.line(s, dim, (mx + mw // 3, my + int(2 * k)),
                             (mx + mw // 3, my + mh - int(2 * k)),
                             int(1 * k))
            pygame.draw.line(s, dim, (mx + 2 * mw // 3, my + int(2 * k)),
                             (mx + 2 * mw // 3, my + mh - int(2 * k)),
                             int(1 * k))
            pygame.draw.line(s, (240, 210, 100),
                             (mx + int(4 * k), my + int(13 * k)),
                             (mx + mw - int(4 * k), my + int(7 * k)),
                             int(2 * k))
            pygame.draw.circle(s, (250, 120, 90),
                               (mx + mw - int(5 * k), my + int(7 * k)),
                               int(3 * k))
        self.screen.blit(s, (x, y))
