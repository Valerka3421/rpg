"""Общие импорты для всех mixin-модулей screens_renderer.

Этот модуль — единственное место, где перечислены все импорты.
Mixins делают `from render.screens_renderer._common import *`.
"""
"""Экраны поверх игры: статы, инвентарь, магазин, крафт, диалог, квесты, статистика."""
import math

import pygame

from core import config, rules
from core.config import INVENTORY_PAGE_SIZE, SHOP_PAGE_SIZE
from core.data import (
    CITY_NPC_NAMES,
    ITEMS,
    PERK_BOOK_MAP,
    PERK_INFO,
    QUESTS,
    is_city_shop_npc,
    item_display_color,
    item_display_name,
    item_sell_price,
    parse_item_key,
)
from render.base_renderer import BaseRenderer
from render.theme import Theme as T

