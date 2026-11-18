"""Пакет игровых данных.

Разбит на подмодули:
- monsters: MONSTERS, BOSSES, BOSS_DROPS, scale_*, mana_crystal_*
- items: ITEMS, MATERIAL_KEYS, item_*, parse_item_key, roll_rarity
- classes: CLASSES, CLASS_ORDER, _cls, class_*
- recipes: SKILLS, SKILL_ORDER, CRAFT_TABS, craft_*, get_craftable_items,
            make_shop_stock, xp_to_next
- statuses: STATUSES, STATUS_MODS, ABILITY_TO_STATUS
- quests: QUESTS, quests_by_giver, generate_board_quests

Все имена re-exported здесь, чтобы внешние импорты
``from core.data import ITEMS`` продолжали работать.
"""
from .monsters import (
    MONSTERS, MONSTERS_BY_CH, BOSSES, BOSS_DROPS, MONSTER_DROPS,
    scale_stats, scale_boss_stats,
    MANA_CRYSTAL_CHANCE, MANA_CRYSTAL_BY_SIZE, MANA_POWDER_BY_CRYSTAL,
    mana_crystal_for, roll_mana_crystal, mana_crystal_powder,
    roll_monster_drops,
)
from .items import (
    ITEMS, MATERIAL_KEYS,
    item_category, item_recipes, select_recipe,
    roll_rarity, RARITY_CHANCES, make_loot_key, parse_item_key,
    get_item_data, item_display_name, item_display_color, item_sell_price,
    PERK_INFO, PERK_BOOK_MAP, RARITY_COLORS,
)
from .classes import (
    ARMOR_TYPE_NAMES, _cls, CLASSES, CLASS_ORDER,
    class_data, class_stats, class_can_use, WCLASS_NAMES,
)
from .recipes import (
    SKILLS, SKILL_ORDER, CRAFT_TABS,
    skill_xp_to_next, default_skills, craft_skill_reward,
    get_craftable_items, make_shop_stock,
    reroll_price, book_price_at_depth, xp_to_next,
)
from .statuses import STATUSES, STATUS_MODS, ABILITY_TO_STATUS
from .quests import (
    QUESTS, quests_by_giver,
    ItemInstance,
    _board_monsters_for, _board_collect_targets,
    _make_board_quest, generate_board_quests,
)

__all__ = [
    'MONSTERS', 'MONSTERS_BY_CH', 'BOSSES', 'BOSS_DROPS',
    'scale_stats', 'scale_boss_stats',
    'MANA_CRYSTAL_CHANCE', 'MANA_CRYSTAL_BY_SIZE', 'MANA_POWDER_BY_CRYSTAL',
    'mana_crystal_for', 'roll_mana_crystal', 'mana_crystal_powder',
    'roll_monster_drops',
    'MONSTER_DROPS',
    'ITEMS', 'MATERIAL_KEYS',
    'item_category', 'item_recipes', 'select_recipe',
    'roll_rarity', 'RARITY_CHANCES', 'make_loot_key', 'parse_item_key',
    'get_item_data', 'item_display_name', 'item_display_color',
    'item_sell_price',
    'PERK_INFO', 'PERK_BOOK_MAP',
    'RARITY_COLORS',
    'ARMOR_TYPE_NAMES', '_cls', 'CLASSES', 'CLASS_ORDER',
    'class_data', 'class_stats', 'class_can_use',
    'WCLASS_NAMES',
    'SKILLS', 'SKILL_ORDER', 'CRAFT_TABS',
    'skill_xp_to_next', 'default_skills', 'craft_skill_reward',
    'get_craftable_items', 'make_shop_stock',
    'reroll_price', 'book_price_at_depth', 'xp_to_next',
    'STATUSES', 'STATUS_MODS', 'ABILITY_TO_STATUS',
    'QUESTS', 'quests_by_giver',
    '_board_monsters_for', '_board_collect_targets',
    '_make_board_quest', 'generate_board_quests',
]
