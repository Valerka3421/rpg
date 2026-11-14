"""Все процедурные иконки 64×64: классы, монстры, NPC, предметы, тайлы."""
import pygame
import math
from core.config import TILE
from core.data import ITEMS, parse_item_key, item_display_color, MATERIAL_KEYS
from render.theme import Theme as T
from render.sprites import Sprites
from render import textures
from render.base_renderer import BaseRenderer


CANVAS = 64
CX = 32


class IconRenderer(BaseRenderer):
    def __init__(self, screen):
        super().__init__(screen)
        self._item_cache = {}
        self._monster_cache = {}
        self._merchant_cache = {}
        self._elder_cache = {}
        self._player_cache = {}
        self._class_icon_cache = {}

    # ════════════════════════ ПРЕДМЕТЫ ════════════════════════
    def _item_sprite_name(self, key):
        base, _ = parse_item_key(key)
        if base.startswith('book_'):
            return f'items/books/{base}'
        if base.startswith('gold_'):
            return 'items/gold'
        if base.startswith('potion_'):
            return f'items/potions/{base}'
        if base.startswith('scroll_'):
            return f'items/scrolls/{base}'
        if base in ('dagger', 'shortsword', 'sword', 'axe', 'greatsword',
                    'warhammer', 'bone_sword', 'demon_blade', 'golem_hammer'):
            return f'items/weapons/{base}'
        if base in ('padded', 'leather', 'chain', 'scale', 'plate', 'mithril',
                    'leather_armor', 'dragon_armor', 'lich_robe'):
            return f'items/armor/{base}'
        if base in ('mushroom', 'berry', 'wood', 'stone', 'ore'):
            return f'items/resources/{base}'
        if base in MATERIAL_KEYS:
            return f'items/materials/{base}'
        return f'items/{base}'

    def draw_item(self, key, x, y, size=CANVAS):
        img = Sprites.get(self._item_sprite_name(key), (size, size))
        if img:
            self.screen.blit(img, (x, y))
            return
        surf = self._get_item_surf(key, size)
        if surf:
            self.screen.blit(surf, (x, y))
            return
        base, _ = parse_item_key(key)
        it = ITEMS.get(base, {})
        self.text(it.get('ch', '?'), x + size // 2, y + size // 2,
                  item_display_color(key), font=T.FONT_BODY, center=True)

    def _get_item_surf(self, key, size):
        base, _ = parse_item_key(key)
        ck = (base, size)
        if ck in self._item_cache:
            return self._item_cache[ck]
        surf = self._make_item_surf(base, size)
        self._item_cache[ck] = surf
        return surf

    def _make_item_surf(self, key, size):
        s = pygame.Surface((CANVAS, CANVAS), pygame.SRCALPHA)
        color = ITEMS.get(key, {}).get('color', (200, 200, 200))

        if key == 'potion_s':
            self._potion(s, (220, 80, 100), big=False, sparkle=False)
        elif key == 'potion_m':
            self._potion(s, (240, 50, 70), big=False, sparkle=True)
        elif key == 'potion_l':
            self._potion(s, (200, 20, 60), big=True, sparkle=True)
        elif key == 'dagger':
            self._blade(s, 18, (200, 215, 235), guard=6, gem=False)
        elif key == 'shortsword':
            self._blade(s, 26, (200, 220, 240), guard=12, gem=False)
        elif key == 'sword':
            self._blade(s, 34, (200, 220, 240), guard=18, gem=True)
        elif key == 'greatsword':
            self._blade(s, 42, (210, 225, 245), guard=22, gem=True, wide=True)
        elif key == 'axe':
            self._axe(s)
        elif key == 'warhammer':
            self._warhammer(s, (120, 120, 130))
        elif key == 'bone_sword':
            self._bone_sword(s)
        elif key == 'demon_blade':
            self._demon_blade(s)
        elif key == 'golem_hammer':
            self._warhammer(s, (110, 110, 125), runes=True)
        elif key in ('iron_sword', 'steel_sword'):
            self._blade(s, 34, color, guard=18, gem=(key == 'steel_sword'))
        elif key == 'tusk_axe':
            self._axe(s, color)
        elif key in ('wolf_armor', 'bear_cloak'):
            self._armor(s, color, style='leather')
        elif key == 'wolf_hood':
            self._cap(s, color, 'leather')
        elif key.startswith('rune_'):
            self._rune(s, color)
        elif key == 'padded':
            self._armor(s, (200, 180, 140), style='quilted')
        elif key == 'leather':
            self._armor(s, (200, 170, 100), style='leather')
        elif key == 'chain':
            self._armor(s, (170, 170, 180), style='chain')
        elif key == 'scale':
            self._armor(s, (160, 150, 130), style='scale')
        elif key == 'plate':
            self._armor(s, (220, 220, 240), style='plate')
        elif key == 'mithril':
            self._armor(s, (180, 210, 230), style='plate')
        elif key == 'leather_armor':
            self._armor(s, (150, 110, 60), style='leather')
        elif key == 'dragon_armor':
            self._armor(s, (240, 130, 40), style='scale')
        elif key == 'lich_robe':
            self._armor(s, (200, 100, 255), style='robe')
        elif key.startswith('gold_'):
            count = {'gold_small': 1, 'gold_medium': 3, 'gold_large': 5}.get(key, 1)
            self._gold(s, count)
        elif key.startswith('book_'):
            self._book(s, key, color)
        elif key.startswith('scroll_'):
            self._scroll(s, color)
        elif key == 'mushroom':
            self._mushroom(s)
        elif key == 'berry':
            self._berry(s)
        elif key == 'wood':
            self._wood(s)
        elif key == 'stone':
            self._stone(s)
        elif key == 'ore':
            self._ore(s)
        elif key in MATERIAL_KEYS:
            self._material(s, key)
        elif key == 'leather_cap':
            self._cap(s, (200, 170, 100), 'leather')
        elif key == 'iron_helm':
            self._helm(s, (180, 180, 190))
        elif key == 'crown':
            self._crown(s, (240, 210, 100))
        elif key == 'cloth_pants':
            self._pants(s, (150, 130, 100))
        elif key == 'leather_boots':
            self._boots(s, (160, 120, 80))
        elif key == 'plate_greaves':
            self._greaves(s, (220, 220, 240))
        else:
            return None

        if size != CANVAS:
            s = pygame.transform.smoothscale(s, (size, size))
        return textures.finish(s, shadow=False)

    # ════════════════════════ ЗЕЛЬЯ ════════════════════════
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
        self.screen.blit(s, (x, y))