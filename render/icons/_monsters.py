"""Auto-generated mixin from render/icon_renderer.py."""

import pygame

from render import textures
from render.icons._base import CANVAS

class MonsterMixin:
    """Спрайты монстров и боссов по ключу из MONSTERS_BY_CH."""

    def draw_monster(self, ch, px, py, size=CANVAS):
        from render.constants import MONSTER_CH_TO_KEY
        key = MONSTER_CH_TO_KEY.get(ch)
        if not key:
            return False
        ck = (key, size)
        if ck not in self._monster_cache:
            self._monster_cache[ck] = self._make_monster_surf(key, size)
        self.screen.blit(self._monster_cache[ck], (px, py))
        return True

    def _make_monster_surf(self, key, size):
        s = pygame.Surface((CANVAS, CANVAS), pygame.SRCALPHA)
        fn = {
            'rat': self._monster_rat,
            'goblin': self._monster_goblin,
            'archer': self._monster_archer,
            'shaman': self._monster_shaman,
            'skeleton': self._monster_skeleton,
            'orc': self._monster_orc,
            'troll': self._monster_troll,
            'demon': self._monster_demon,
            'vampire': self._monster_vampire,
            'dragonling': self._monster_dragonling,
            'golem': self._monster_golem,
            'wraith': self._monster_wraith,
            'goblin_king': self._monster_goblin_king,
            'lich': self._monster_lich,
            'vampire_lord': self._monster_vampire_lord,
            'dragon': self._monster_dragon,
            'deer': self._monster_deer,
            'wolf': self._monster_wolf,
            'bear': self._monster_bear,
            'slime': self._monster_slime,
            'boar': self._monster_boar,
        }.get(key)
        if fn:
            fn(s)
        if size != CANVAS:
            s = pygame.transform.smoothscale(s, (size, size))
        return textures.finish(s, shadow=True)

    def _monster_deer(self, s):
        # Ноги
        for x in (20, 28, 40, 48):
            pygame.draw.line(s, (120, 86, 52), (x, 42), (x, 58), 3)
        # Тело и шея
        pygame.draw.ellipse(s, (158, 112, 68), (14, 28, 38, 22))
        pygame.draw.ellipse(s, (96, 66, 40), (14, 28, 38, 22), 2)
        pygame.draw.polygon(s, (158, 112, 68),
                            [(44, 30), (54, 14), (58, 28), (50, 34)])
        # Голова
        pygame.draw.ellipse(s, (172, 126, 78), (48, 10, 14, 12))
        pygame.draw.circle(s, (20, 16, 12), (58, 15), 2)
        pygame.draw.circle(s, (20, 16, 12), (51, 13), 1)
        # Рога
        pygame.draw.lines(s, (214, 196, 158), False,
                          [(52, 11), (48, 2), (42, 0)], 2)
        pygame.draw.lines(s, (214, 196, 158), False,
                          [(55, 10), (58, 2), (64, 0)], 2)
        # Пятна
        for x, y in ((24, 34), (32, 40), (40, 34)):
            pygame.draw.circle(s, (208, 172, 124), (x, y), 2)
        # Хвост
        pygame.draw.line(s, (230, 225, 210), (15, 32), (10, 26), 2)

    def _monster_wolf(self, s):
        # Тело
        pygame.draw.ellipse(s, (118, 124, 136), (12, 28, 40, 22))
        pygame.draw.ellipse(s, (68, 74, 86), (12, 28, 40, 22), 2)
        # Голова
        pygame.draw.polygon(s, (128, 134, 146), [
            (42, 22), (58, 18), (62, 28), (50, 36), (42, 32)])
        pygame.draw.polygon(s, (68, 74, 86), [
            (42, 22), (58, 18), (62, 28), (50, 36), (42, 32)], 2)
        # Уши
        pygame.draw.polygon(s, (108, 114, 126),
                            [(44, 20), (46, 10), (52, 20)])
        pygame.draw.polygon(s, (108, 114, 126),
                            [(50, 19), (56, 9), (60, 20)])
        # Глаз и нос
        pygame.draw.circle(s, (240, 210, 90), (54, 24), 2)
        pygame.draw.circle(s, (24, 22, 20), (62, 28), 2)
        # Ноги
        for x in (18, 28, 42, 50):
            pygame.draw.line(s, (96, 102, 114), (x, 44), (x, 58), 3)
        # Хвост
        pygame.draw.lines(s, (118, 124, 136), False,
                          [(12, 32), (2, 24), (4, 16)], 4)

    def _monster_bear(self, s):
        # Крупное тело
        pygame.draw.ellipse(s, (96, 66, 42), (8, 24, 48, 28))
        pygame.draw.ellipse(s, (56, 38, 24), (8, 24, 48, 28), 2)
        # Голова
        pygame.draw.circle(s, (108, 76, 48), (48, 22), 14)
        pygame.draw.circle(s, (56, 38, 24), (48, 22), 14, 2)
        # Уши
        pygame.draw.circle(s, (96, 66, 42), (40, 10), 5)
        pygame.draw.circle(s, (96, 66, 42), (56, 10), 5)
        # Морда
        pygame.draw.ellipse(s, (140, 104, 70), (50, 24, 14, 10))
        pygame.draw.circle(s, (24, 18, 14), (61, 27), 2)
        pygame.draw.circle(s, (20, 16, 12), (44, 18), 2)
        # Лапы с когтями
        for x in (16, 30, 44):
            pygame.draw.rect(s, (80, 54, 34), (x, 48, 9, 10),
                             border_radius=3)
            pygame.draw.line(s, (225, 215, 190), (x + 2, 58), (x + 2, 61), 2)
        # Светлая грудь
        pygame.draw.ellipse(s, (120, 88, 58), (30, 36, 18, 12))

    def _monster_slime(self, s):
        # Полупрозрачное тело
        pygame.draw.ellipse(s, (86, 176, 82, 210), (8, 20, 48, 38))
        pygame.draw.ellipse(s, (44, 110, 44), (8, 20, 48, 38), 3)
        pygame.draw.ellipse(s, (140, 220, 120, 160), (14, 26, 36, 27))
        # Блик
        pygame.draw.ellipse(s, (220, 255, 200, 150), (18, 26, 10, 7))
        # Глаза
        pygame.draw.circle(s, (24, 48, 20), (24, 38), 4)
        pygame.draw.circle(s, (24, 48, 20), (42, 38), 4)
        pygame.draw.circle(s, (255, 255, 255), (25, 37), 1)
        pygame.draw.circle(s, (255, 255, 255), (43, 37), 1)
        # Капли
        pygame.draw.circle(s, (86, 176, 82), (14, 56), 4)
        pygame.draw.circle(s, (86, 176, 82), (52, 54), 3)

    def _monster_boar(self, s):
        # Тело
        pygame.draw.ellipse(s, (122, 100, 78), (8, 28, 44, 24))
        pygame.draw.ellipse(s, (70, 55, 40), (8, 28, 44, 24), 2)
        # Щетина
        for x in range(14, 46, 7):
            pygame.draw.line(s, (52, 40, 28), (x, 28), (x + 2, 22), 2)
        # Голова
        pygame.draw.ellipse(s, (134, 110, 86), (42, 28, 20, 18))
        pygame.draw.ellipse(s, (70, 55, 40), (42, 28, 20, 18), 2)
        # Пятачок и глаз
        pygame.draw.ellipse(s, (178, 140, 118), (54, 34, 10, 8))
        pygame.draw.circle(s, (30, 22, 18), (58, 38), 1)
        pygame.draw.circle(s, (240, 210, 90), (50, 31), 2)
        pygame.draw.polygon(s, (96, 76, 58), [(46, 28), (50, 18), (56, 28)])
        # Клыки
        pygame.draw.lines(s, (240, 235, 210), False,
                          [(58, 42), (64, 48), (62, 54)], 3)
        pygame.draw.lines(s, (240, 235, 210), False,
                          [(50, 44), (46, 50), (49, 55)], 2)
        # Ноги
        for x in (14, 26, 40, 48):
            pygame.draw.line(s, (90, 72, 54), (x, 48), (x, 59), 4)

    def _monster_rat(self, s):
        # Тело
        pygame.draw.ellipse(s, (140, 120, 100), (10, 32, 44, 24))
        pygame.draw.ellipse(s, (90, 75, 60), (10, 32, 44, 24), 2)
        # Голова
        pygame.draw.circle(s, (140, 120, 100), (48, 38), 10)
        pygame.draw.circle(s, (90, 75, 60), (48, 38), 10, 2)
        # Уши
        pygame.draw.circle(s, (200, 170, 140), (44, 28), 5)
        pygame.draw.circle(s, (200, 170, 140), (52, 28), 5)
        # Глаз
        pygame.draw.circle(s, (200, 40, 40), (52, 36), 2)
        pygame.draw.circle(s, (0, 0, 0), (52, 36), 1)
        # Нос
        pygame.draw.circle(s, (30, 20, 20), (58, 40), 2)
        # Хвост
        pygame.draw.lines(s, (140, 120, 100), False,
                          [(10, 44), (2, 52), (6, 58)], 3)
        pygame.draw.lines(s, (90, 75, 60), False,
                          [(10, 44), (2, 52), (6, 58)], 1)

    def _monster_goblin(self, s):
        # Тело
        pygame.draw.ellipse(s, (80, 130, 70), (14, 30, 36, 26))
        pygame.draw.ellipse(s, (40, 80, 40), (14, 30, 36, 26), 2)
        # Голова
        pygame.draw.circle(s, (100, 160, 80), (32, 22), 14)
        pygame.draw.circle(s, (50, 100, 40), (32, 22), 14, 2)
        # Уши (острые)
        pygame.draw.polygon(s, (100, 160, 80),
                            [(18, 22), (10, 8), (22, 16)])
        pygame.draw.polygon(s, (100, 160, 80),
                            [(46, 22), (54, 8), (42, 16)])
        pygame.draw.polygon(s, (50, 100, 40),
                            [(18, 22), (10, 8), (22, 16)], 2)
        pygame.draw.polygon(s, (50, 100, 40),
                            [(46, 22), (54, 8), (42, 16)], 2)
        # Глаза
        pygame.draw.circle(s, (200, 40, 40), (26, 20), 3)
        pygame.draw.circle(s, (200, 40, 40), (38, 20), 3)
        pygame.draw.circle(s, (10, 10, 10), (26, 20), 1)
        pygame.draw.circle(s, (10, 10, 10), (38, 20), 1)
        # Рот
        pygame.draw.line(s, (30, 60, 20), (26, 30), (38, 30), 2)
        # Кинжал
        pygame.draw.line(s, (200, 200, 220), (50, 30), (58, 42), 3)
        pygame.draw.line(s, (90, 60, 30), (48, 28), (50, 30), 3)

    def _monster_archer(self, s):
        # Тело
        pygame.draw.ellipse(s, (80, 130, 70), (12, 30, 34, 26))
        pygame.draw.ellipse(s, (40, 80, 40), (12, 30, 34, 26), 2)
        # Голова
        pygame.draw.circle(s, (100, 160, 80), (30, 22), 12)
        pygame.draw.circle(s, (50, 100, 40), (30, 22), 12, 2)
        # Уши
        pygame.draw.polygon(s, (100, 160, 80),
                            [(18, 22), (12, 10), (22, 18)])
        pygame.draw.polygon(s, (100, 160, 80),
                            [(42, 22), (48, 10), (38, 18)])
        # Глаза
        pygame.draw.circle(s, (200, 40, 40), (25, 20), 2)
        pygame.draw.circle(s, (200, 40, 40), (35, 20), 2)
        # Лук
        pygame.draw.arc(s, (150, 100, 50),
                        (46, 16, 14, 32), -1.3, 1.3, 3)
        pygame.draw.line(s, (200, 180, 130),
                         (52, 20), (52, 44), 1)
        # Стрела
        pygame.draw.line(s, (150, 100, 50),
                         (54, 18), (54, 46), 2)
        pygame.draw.polygon(s, (200, 200, 220),
                            [(52, 16), (56, 16), (54, 12)])

    def _monster_shaman(self, s):
        # Мантия (фиолетовая)
        pygame.draw.polygon(s, (120, 80, 160), [
            (10, 56), (20, 30), (44, 30), (54, 56),
        ])
        pygame.draw.polygon(s, (70, 40, 100), [
            (10, 56), (20, 30), (44, 30), (54, 56),
        ], 2)
        # Голова
        pygame.draw.circle(s, (100, 160, 80), (32, 22), 11)
        pygame.draw.circle(s, (50, 100, 40), (32, 22), 11, 2)
        # Капюшон
        pygame.draw.arc(s, (120, 80, 160),
                        (18, 8, 28, 24), 0, 3.14, 3)
        # Уши
        pygame.draw.polygon(s, (100, 160, 80),
                            [(22, 22), (16, 12), (24, 18)])
        pygame.draw.polygon(s, (100, 160, 80),
                            [(42, 22), (48, 12), (40, 18)])
        # Глаза
        pygame.draw.circle(s, (255, 200, 40), (27, 21), 2)
        pygame.draw.circle(s, (255, 200, 40), (37, 21), 2)
        # Посох
        pygame.draw.line(s, (140, 100, 60), (50, 56), (50, 8), 3)
        pygame.draw.circle(s, (180, 140, 220), (50, 8), 5)
        pygame.draw.circle(s, (100, 60, 140), (50, 8), 5, 2)
        pygame.draw.circle(s, (255, 255, 255), (48, 6), 1)

    def _monster_skeleton(self, s):
        # Рёбра (тело)
        pygame.draw.circle(s, (220, 220, 220), (32, 22), 14)
        pygame.draw.circle(s, (90, 90, 90), (32, 22), 14, 2)
        # Глазницы
        pygame.draw.circle(s, (20, 20, 20), (27, 22), 4)
        pygame.draw.circle(s, (20, 20, 20), (37, 22), 4)
        # Нос
        pygame.draw.polygon(s, (20, 20, 20),
                            [(32, 28), (30, 32), (34, 32)])
        # Зубы
        for xx in range(26, 40, 3):
            pygame.draw.line(s, (80, 80, 80),
                             (xx, 34), (xx, 38), 1)
        # Позвоночник
        pygame.draw.line(s, (220, 220, 220),
                         (32, 36), (32, 58), 3)
        # Рёбра
        for yy in range(40, 54, 4):
            pygame.draw.arc(s, (220, 220, 220),
                            (22, yy - 2, 20, 8), 0, 3.14, 2)
        # Руки
        pygame.draw.line(s, (220, 220, 220),
                         (16, 42), (10, 52), 2)
        pygame.draw.line(s, (220, 220, 220),
                         (48, 42), (54, 52), 2)

    def _monster_orc(self, s):
        # Тело (широкое)
        pygame.draw.rect(s, (140, 90, 50), (14, 26, 36, 32),
                         border_radius=6)
        pygame.draw.rect(s, (70, 45, 25), (14, 26, 36, 32), 2,
                         border_radius=6)
        # Кожа на теле
        pygame.draw.rect(s, (100, 70, 40), (22, 40, 20, 14))
        # Голова
        pygame.draw.rect(s, (140, 90, 50), (16, 8, 32, 24),
                         border_radius=4)
        pygame.draw.rect(s, (70, 45, 25), (16, 8, 32, 24), 2,
                         border_radius=4)
        # Уши
        pygame.draw.polygon(s, (140, 90, 50),
                            [(16, 14), (8, 8), (16, 22)])
        pygame.draw.polygon(s, (140, 90, 50),
                            [(48, 14), (56, 8), (48, 22)])
        # Глаза
        pygame.draw.circle(s, (255, 200, 40), (24, 16), 3)
        pygame.draw.circle(s, (255, 200, 40), (40, 16), 3)
        pygame.draw.circle(s, (10, 10, 10), (24, 16), 1)
        pygame.draw.circle(s, (10, 10, 10), (40, 16), 1)
        # Клыки
        pygame.draw.polygon(s, (240, 240, 220),
                            [(26, 28), (28, 28), (27, 34)])
        pygame.draw.polygon(s, (240, 240, 220),
                            [(36, 28), (38, 28), (37, 34)])
        # Топор в руке
        pygame.draw.line(s, (90, 60, 30), (54, 20), (58, 44), 4)
        pygame.draw.polygon(s, (170, 170, 180), [
            (50, 14), (60, 12), (62, 24), (52, 22),
        ])

    def _monster_troll(self, s):
        # Тело (большое, зелёное)
        pygame.draw.ellipse(s, (85, 140, 85), (6, 20, 52, 40))
        pygame.draw.ellipse(s, (40, 80, 40), (6, 20, 52, 40), 2)
        # Голова
        pygame.draw.circle(s, (100, 160, 90), (32, 20), 14)
        pygame.draw.circle(s, (40, 80, 40), (32, 20), 14, 2)
        # Глаза
        pygame.draw.circle(s, (255, 200, 40), (26, 18), 3)
        pygame.draw.circle(s, (255, 200, 40), (38, 18), 3)
        pygame.draw.circle(s, (10, 10, 10), (26, 18), 1)
        pygame.draw.circle(s, (10, 10, 10), (38, 18), 1)
        # Клыки
        pygame.draw.polygon(s, (240, 240, 220),
                            [(26, 30), (28, 30), (27, 38)])
        pygame.draw.polygon(s, (240, 240, 220),
                            [(36, 30), (38, 30), (37, 38)])
        # Морщины
        pygame.draw.arc(s, (50, 100, 50), (24, 24, 16, 8), 0, 3.14, 1)
        # Дубина
        pygame.draw.line(s, (90, 60, 30), (52, 20), (58, 46), 5)
        pygame.draw.ellipse(s, (90, 60, 30), (52, 8, 12, 14))

    def _monster_demon(self, s):
        # Тело
        pygame.draw.ellipse(s, (160, 50, 50), (10, 24, 44, 34))
        pygame.draw.ellipse(s, (80, 20, 20), (10, 24, 44, 34), 2)
        # Рога
        pygame.draw.polygon(s, (40, 30, 30), [
            (16, 16), (10, 4), (20, 12),
        ])
        pygame.draw.polygon(s, (40, 30, 30), [
            (48, 16), (54, 4), (44, 12),
        ])
        # Голова
        pygame.draw.circle(s, (160, 50, 50), (32, 22), 14)
        pygame.draw.circle(s, (80, 20, 20), (32, 22), 14, 2)
        # Глаза
        pygame.draw.circle(s, (255, 220, 40), (26, 20), 4)
        pygame.draw.circle(s, (255, 220, 40), (38, 20), 4)
        pygame.draw.circle(s, (20, 10, 10), (26, 20), 2)
        pygame.draw.circle(s, (20, 10, 10), (38, 20), 2)
        # Клыки
        pygame.draw.polygon(s, (240, 240, 220),
                            [(28, 32), (30, 32), (29, 40)])
        pygame.draw.polygon(s, (240, 240, 220),
                            [(34, 32), (36, 32), (35, 40)])
        # Огненная аура
        for dx, dy in ((-4, -6), (4, -6), (-2, -2), (2, -2)):
            pygame.draw.circle(s, (255, 150, 50, 80),
                               (32 + dx * 4, 4 + dy), 3)

    def _monster_vampire(self, s):
        # Плащ
        pygame.draw.polygon(s, (30, 10, 40), [
            (8, 58), (14, 20), (50, 20), (56, 58), (32, 62),
        ])
        pygame.draw.polygon(s, (60, 20, 70), [
            (8, 58), (14, 20), (50, 20), (56, 58), (32, 62),
        ], 2)
        # Голова (бледная)
        pygame.draw.circle(s, (220, 200, 220), (32, 20), 12)
        pygame.draw.circle(s, (100, 80, 100), (32, 20), 12, 2)
        # Волосы
        pygame.draw.polygon(s, (20, 10, 30), [
            (20, 14), (32, 4), (44, 14), (44, 8), (32, 0), (20, 8),
        ])
        # Глаза (красные)
        pygame.draw.circle(s, (220, 30, 60), (27, 18), 3)
        pygame.draw.circle(s, (220, 30, 60), (37, 18), 3)
        pygame.draw.circle(s, (10, 5, 15), (27, 18), 1)
        pygame.draw.circle(s, (10, 5, 15), (37, 18), 1)
        # Клыки
        pygame.draw.polygon(s, (255, 255, 255),
                            [(28, 26), (30, 26), (29, 34)])
        pygame.draw.polygon(s, (255, 255, 255),
                            [(34, 26), (36, 26), (35, 34)])

    def _monster_dragonling(self, s):
        # Крылья
        pygame.draw.polygon(s, (150, 110, 40), [
            (22, 30), (4, 12), (10, 40), (22, 46),
        ])
        pygame.draw.polygon(s, (150, 110, 40), [
            (42, 30), (60, 12), (54, 40), (42, 46),
        ])
        pygame.draw.polygon(s, (80, 50, 20), [
            (22, 30), (4, 12), (10, 40), (22, 46),
        ], 2)
        pygame.draw.polygon(s, (80, 50, 20), [
            (42, 30), (60, 12), (54, 40), (42, 46),
        ], 2)
        # Тело
        pygame.draw.ellipse(s, (200, 150, 60), (16, 26, 32, 28))
        pygame.draw.ellipse(s, (100, 70, 30), (16, 26, 32, 28), 2)
        # Голова
        pygame.draw.circle(s, (200, 150, 60), (32, 20), 10)
        pygame.draw.circle(s, (100, 70, 30), (32, 20), 10, 2)
        # Глаза
        pygame.draw.circle(s, (255, 230, 80), (27, 18), 2)
        pygame.draw.circle(s, (255, 230, 80), (37, 18), 2)
        pygame.draw.circle(s, (20, 15, 5), (27, 18), 1)
        pygame.draw.circle(s, (20, 15, 5), (37, 18), 1)
        # Рожки
        pygame.draw.polygon(s, (100, 70, 30), [
            (26, 12), (22, 4), (28, 10),
        ])
        pygame.draw.polygon(s, (100, 70, 30), [
            (38, 12), (42, 4), (36, 10),
        ])
        # Пасть
        pygame.draw.line(s, (80, 40, 20), (26, 26), (38, 26), 2)

    def _monster_golem(self, s):
        # Тело (каменный блок)
        pygame.draw.rect(s, (120, 120, 130), (12, 16, 40, 42),
                         border_radius=4)
        pygame.draw.rect(s, (60, 60, 70), (12, 16, 40, 42), 3,
                         border_radius=4)
        # Плечи (широкие)
        pygame.draw.rect(s, (140, 140, 150), (6, 14, 52, 10),
                         border_radius=2)
        pygame.draw.rect(s, (60, 60, 70), (6, 14, 52, 10), 2,
                         border_radius=2)
        # Глаза (светящиеся)
        pygame.draw.circle(s, (200, 220, 255), (24, 26), 4)
        pygame.draw.circle(s, (200, 220, 255), (40, 26), 4)
        pygame.draw.circle(s, (20, 30, 50), (24, 26), 2)
        pygame.draw.circle(s, (20, 30, 50), (40, 26), 2)
        # Трещины
        pygame.draw.line(s, (60, 60, 70), (16, 34), (20, 44), 2)
        pygame.draw.line(s, (60, 60, 70), (48, 30), (44, 46), 2)
        pygame.draw.line(s, (60, 60, 70), (28, 40), (36, 44), 1)
        # Ноги
        pygame.draw.rect(s, (100, 100, 110), (14, 52, 12, 8))
        pygame.draw.rect(s, (100, 100, 110), (38, 52, 12, 8))

    def _monster_wraith(self, s):
        # Призрачный шлейф
        pygame.draw.polygon(s, (180, 180, 220, 180), [
            (12, 30), (52, 30), (48, 60), (36, 52),
            (32, 62), (28, 52), (16, 60),
        ])
        # Голова
        pygame.draw.circle(s, (200, 200, 230), (32, 22), 14)
        pygame.draw.circle(s, (140, 140, 180), (32, 22), 14, 2)
        # Глаза (тёмные провалы)
        pygame.draw.circle(s, (30, 30, 40), (27, 22), 4)
        pygame.draw.circle(s, (30, 30, 40), (37, 22), 4)
        # Рот
        pygame.draw.ellipse(s, (30, 30, 40), (28, 32, 8, 4))
        # Аура
        for dx, dy, r in ((0, 0, 20), (0, 0, 16)):
            pygame.draw.circle(s, (140, 140, 200, 40),
                               (32, 22), r, 2)

    def _monster_goblin_king(self, s):
        # Как гоблин, но крупнее с короной
        pygame.draw.ellipse(s, (80, 130, 70), (10, 28, 44, 28))
        pygame.draw.ellipse(s, (40, 80, 40), (10, 28, 44, 28), 2)
        # Голова
        pygame.draw.circle(s, (100, 160, 80), (32, 20), 15)
        pygame.draw.circle(s, (50, 100, 40), (32, 20), 15, 2)
        # Корона
        pygame.draw.polygon(s, (240, 200, 60), [
            (18, 10), (22, 0), (26, 8), (32, -2), (38, 8), (42, 0), (46, 10),
        ])
        pygame.draw.polygon(s, (180, 150, 40), [
            (18, 10), (22, 0), (26, 8), (32, -2), (38, 8), (42, 0), (46, 10),
        ], 2)
        # Уши
        pygame.draw.polygon(s, (100, 160, 80),
                            [(16, 22), (6, 8), (20, 16)])
        pygame.draw.polygon(s, (100, 160, 80),
                            [(48, 22), (58, 8), (44, 16)])
        # Глаза
        pygame.draw.circle(s, (220, 40, 40), (26, 20), 3)
        pygame.draw.circle(s, (220, 40, 40), (38, 20), 3)
        pygame.draw.circle(s, (10, 10, 10), (26, 20), 1)
        pygame.draw.circle(s, (10, 10, 10), (38, 20), 1)
        # Скипетр
        pygame.draw.line(s, (200, 160, 60), (56, 26), (56, 58), 3)
        pygame.draw.circle(s, (255, 200, 60), (56, 22), 5)
        pygame.draw.circle(s, (200, 40, 40), (56, 22), 3)

    def _monster_lich(self, s):
        # Аура
        aura = pygame.Surface((CANVAS, CANVAS), pygame.SRCALPHA)
        pygame.draw.circle(aura, (140, 60, 200, 90), (32, 32), 28)
        pygame.draw.circle(aura, (180, 100, 240, 50), (32, 32), 22)
        s.blit(aura, (0, 0))
        # Мантия
        pygame.draw.polygon(s, (60, 40, 90), [
            (8, 58), (16, 22), (48, 22), (56, 58), (32, 64),
        ])
        pygame.draw.polygon(s, (30, 20, 50), [
            (8, 58), (16, 22), (48, 22), (56, 58), (32, 64),
        ], 2)
        # Череп
        pygame.draw.circle(s, (220, 220, 220), (32, 20), 12)
        pygame.draw.circle(s, (90, 90, 90), (32, 20), 12, 2)
        # Глазницы (светящиеся)
        pygame.draw.circle(s, (220, 100, 255), (27, 20), 4)
        pygame.draw.circle(s, (220, 100, 255), (37, 20), 4)
        pygame.draw.circle(s, (255, 200, 255), (26, 18), 1)
        pygame.draw.circle(s, (255, 200, 255), (36, 18), 1)
        # Корона
        pygame.draw.polygon(s, (180, 100, 220), [
            (22, 10), (25, 0), (29, 6), (32, 0),
            (35, 6), (39, 0), (42, 10),
        ])
        # Посох
        pygame.draw.line(s, (90, 60, 30), (54, 20), (54, 58), 3)
        pygame.draw.circle(s, (200, 100, 255), (54, 14), 5)
        pygame.draw.circle(s, (255, 255, 255), (53, 13), 2)

    def _monster_vampire_lord(self, s):
        # Плащ (тёмно-фиолетовый)
        pygame.draw.polygon(s, (30, 10, 40), [
            (6, 60), (14, 20), (50, 20), (58, 60), (32, 64),
        ])
        pygame.draw.polygon(s, (60, 20, 70), [
            (6, 60), (14, 20), (50, 20), (58, 60), (32, 64),
        ], 2)
        # Голова
        pygame.draw.circle(s, (220, 200, 220), (32, 20), 13)
        pygame.draw.circle(s, (100, 80, 100), (32, 20), 13, 2)
        # Волосы
        pygame.draw.polygon(s, (20, 10, 30), [
            (19, 12), (32, 0), (45, 12), (45, 6), (32, -4), (19, 6),
        ])
        # Глаза (ярко-красные)
        pygame.draw.circle(s, (220, 30, 60), (26, 18), 4)
        pygame.draw.circle(s, (220, 30, 60), (38, 18), 4)
        pygame.draw.circle(s, (10, 5, 15), (26, 18), 2)
        pygame.draw.circle(s, (10, 5, 15), (38, 18), 2)
        pygame.draw.circle(s, (255, 100, 150), (25, 17), 1)
        pygame.draw.circle(s, (255, 100, 150), (37, 17), 1)
        # Клыки
        pygame.draw.polygon(s, (255, 255, 255),
                            [(27, 26), (30, 26), (28, 36)])
        pygame.draw.polygon(s, (255, 255, 255),
                            [(34, 26), (37, 26), (36, 36)])

    def _monster_dragon(self, s):
        # Крылья
        pygame.draw.polygon(s, (150, 70, 30), [
            (18, 26), (0, 4), (6, 34), (18, 40),
        ])
        pygame.draw.polygon(s, (150, 70, 30), [
            (46, 26), (64, 4), (58, 34), (46, 40),
        ])
        pygame.draw.polygon(s, (80, 40, 15), [
            (18, 26), (0, 4), (6, 34), (18, 40),
        ], 2)
        pygame.draw.polygon(s, (80, 40, 15), [
            (46, 26), (64, 4), (58, 34), (46, 40),
        ], 2)
        # Тело
        pygame.draw.ellipse(s, (200, 100, 40), (8, 22, 48, 38))
        pygame.draw.ellipse(s, (100, 50, 20), (8, 22, 48, 38), 2)
        # Голова
        pygame.draw.circle(s, (200, 100, 40), (32, 16), 13)
        pygame.draw.circle(s, (100, 50, 20), (32, 16), 13, 2)
        # Рога
        pygame.draw.polygon(s, (60, 30, 10), [
            (22, 8), (16, -4), (24, 4),
        ])
        pygame.draw.polygon(s, (60, 30, 10), [
            (42, 8), (48, -4), (40, 4),
        ])
        # Глаза
        pygame.draw.circle(s, (255, 230, 40), (26, 14), 3)
        pygame.draw.circle(s, (255, 230, 40), (38, 14), 3)
        pygame.draw.circle(s, (30, 15, 5), (26, 14), 1)
        pygame.draw.circle(s, (30, 15, 5), (38, 14), 1)
        # Пасть с зубами
        pygame.draw.line(s, (60, 30, 10), (24, 24), (40, 24), 2)
        for xx in (26, 30, 34, 38):
            pygame.draw.polygon(s, (255, 255, 255),
                                [(xx - 1, 24), (xx + 1, 24), (xx, 28)])

    # ════════════════════════ NPC ════════════════════════
