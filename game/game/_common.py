"""Общие импорты и константы для всех mixin-модулей game.game.

Этот модуль — единственное место, где перечислены все импорты,
нужные классу Game. Mixin-модули делают `from game.game._common import *`,
чтобы получить доступ к pygame, core.*, render.*, и контроллерам.
"""
import json
import os
import random
import time

import pygame

from core import config, locations, rules
from core import overworld as ov
from core.config import (
    COL_CRIT,
    COL_DIM,
    COL_DMG,
    COL_DODGE,
    COL_GOLD,
    COL_HEAL,
    COL_MERCH,
    COL_TEXT,
    COL_WIN,
    COL_XP,
    INVENTORY_PAGE_SIZE,
    MAP_H,
    MAP_W,
    MAX_DEPTH,
    MERCHANT_FLOORS,
    PLAYER_MOVE_COOLDOWN,
    REGEN_TICK_INTERVAL,
    SAVE_VERSION,
    STATUS_TICK_INTERVAL,
    TILE,
    Tile,
)
from core.data import (
    BOSSES,
    CITY_NPC_GLYPHS,
    CITY_NPC_NAMES,
    ITEMS,
    MONSTERS_BY_CH,
    is_city_shop_npc,
    item_display_name,
    parse_item_key,
)
from core.entities import Entity, FloatText, Message, Player
from core.quests import QuestLog
from core.save import (
    dungeon_cache_from_dict,
    dungeon_cache_to_dict,
    dungeon_from_dict,
    dungeon_to_dict,
    entity_from_dict,
    entity_to_dict,
    player_from_dict,
    player_to_dict,
    save_slot_path,
)
from core.settings import load_settings, save_settings
from core.world import (
    bfs_toward_player,
    generate_dungeon,
    has_los,
    place_entry_tile,
)
from game.craft_controller import CraftController
from game.dev_console_controller import DevConsoleController
from game.dialogue_controller import DialogueController
from game.menu_controller import MenuController
from game.quest_controller import QuestController
from game.shop_controller import ShopController
from game.skills_controller import SkillsController
from game.stats_controller import StatsController
from render import Renderer


# Порядок характеристик для цифровых клавиш в окне статов
STAT_ORDER = ('str', 'agi', 'vit', 'luk', 'df_stat', 'int')


