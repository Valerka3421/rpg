"""Пакет рендера иконок и спрайтов.

Разбит на подмодули по категориям:
- _base: константы, конструктор, диспетчер draw_item
- _items: примитивы предметов (оружие, броня, зелья, материалы, книги)
- _players: спрайты классов игрока
- _class_icons: маленькие иконки классов для UI
- _monsters: спрайты монстров и боссов
- _npcs: городские NPC, торговцы, старейшина, UI-иконки

IconRenderer собирается из этих примесей через множественное
наследование — все методы оказываются на одном `self`.
"""
import math

import pygame

from core.config import TILE
from core.data import ITEMS, MATERIAL_KEYS, item_display_color, parse_item_key
from render import textures
from render.base_renderer import BaseRenderer
from render.icons._base import CANVAS, CX
from render.icons._items import ItemMixin
from render.icons._players import PlayerMixin
from render.icons._class_icons import ClassIconMixin
from render.icons._monsters import MonsterMixin
from render.icons._npcs import NPCMixin
from render.sprites import Sprites
from render.theme import Theme as T


class IconRenderer(ItemMixin, PlayerMixin, ClassIconMixin,
                     MonsterMixin, NPCMixin, BaseRenderer):
    """Рендер иконок и спрайтов: предметы, игрок, монстры, NPC.

    Класс тонкий — только конструктор и диспетчер `_make_item_surf`.
    Все примитивы рисования — в примесях (mixins) по категориям.
    """

    def __init__(self, screen):
        super().__init__(screen)
        self._item_cache = {}
        self._monster_cache = {}
        self._merchant_cache = {}
        self._city_npc_cache = {}
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
        elif key == 'mana_s':
            self._potion(s, (110, 160, 250), big=False, sparkle=False)
        elif key == 'mana_m':
            self._potion(s, (132, 108, 240), big=True, sparkle=True)
        elif key in ('fried_mushrooms', 'berry_compote', 'roast_venison',
                     'roast_boar', 'roast_bear'):
            self._food(s, key)
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
            # Универсальная заглушка: у любого предмета должна быть
            # нарисованная иконка, а не сырой символ/буква.
            self._generic_item(s, key, color)
        if size != CANVAS:
            s = pygame.transform.smoothscale(s, (size, size))
        return textures.finish(s, shadow=False)

    def _generic_item(self, s, key, color):
        """Иконка для предмета без специального билдера.

        Форма подбирается по типу предмета, поэтому даже новый ресурс
        или ключ выглядит как вещь, а не как буква из ``ch``.
        """
        it = ITEMS.get(key, {})
        typ = it.get('type')
        if typ == 'weapon':
            self._blade(s, 30, color, guard=10, gem=False)
        elif typ == 'armor':
            self._armor(s, color, style='leather')
        elif typ in ('head', 'helmet'):
            self._cap(s, color, 'leather')
        elif typ in ('legs', 'leggings'):
            self._pants(s, color)
        elif typ == 'boots':
            self._boots(s, color)
        elif typ in ('heal', 'mana', 'buff', 'poison'):
            self._potion(s, color, big=False, sparkle=False)
        elif typ == 'book':
            self._book(s, key or 'book', color)
        else:
            # Ключи, ресурсы и прочее — гранёный кристалл/медальон.
            points = [(CX, 13), (CX + 17, 26), (CX + 11, 48),
                      (CX - 11, 48), (CX - 17, 26)]
            pygame.draw.polygon(s, color, points)
            pygame.draw.polygon(s, textures.shade(color, 0.65), points, 2)
            pygame.draw.line(s, textures.shade(color, 1.25),
                             (CX, 15), (CX - 7, 30), 2)
            pygame.draw.circle(s, textures.shade(color, 1.3),
                               (CX, 36), 4)

    # ════════════════════════ ЗЕЛЬЯ ════════════════════════
