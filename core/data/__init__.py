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
from .city import (
    CITY_NPC_GLYPHS,
    CITY_NPC_NAMES,
    CITY_NPC_SHOPS,
    CITY_SHOP_STOCK,
    is_city_shop_npc,
    make_city_shop_stock,
)
from .classes import (
    ARMOR_TYPE_NAMES,
    CLASS_ORDER,
    CLASSES,
    WCLASS_NAMES,
    _cls,
    class_can_use,
    class_data,
    class_stats,
)
from .items import (
    ITEMS,
    MATERIAL_KEYS,
    PERK_BOOK_MAP,
    PERK_INFO,
    RARITY_CHANCES,
    RARITY_COLORS,
    get_item_data,
    item_category,
    item_display_color,
    item_display_name,
    item_recipes,
    item_sell_price,
    make_loot_key,
    parse_item_key,
    roll_rarity,
    select_recipe,
)
from .monsters import (
    BOSS_DROPS,
    BOSSES,
    MANA_CRYSTAL_BY_SIZE,
    MANA_CRYSTAL_CHANCE,
    MANA_POWDER_BY_CRYSTAL,
    MONSTER_DROPS,
    MONSTERS,
    MONSTERS_BY_CH,
    mana_crystal_for,
    mana_crystal_powder,
    roll_mana_crystal,
    roll_monster_drops,
    scale_boss_stats,
    scale_stats,
)
from .quests import (
    QUESTS,
    ItemInstance,
    _board_collect_targets,
    _board_monsters_for,
    _make_board_quest,
    generate_board_quests,
    quests_by_giver,
)
from .recipes import (
    CRAFT_TABS,
    SKILL_ORDER,
    SKILLS,
    book_price_at_depth,
    craft_skill_reward,
    default_skills,
    get_craftable_items,
    make_shop_stock,
    reroll_price,
    skill_xp_to_next,
    xp_to_next,
)
from .statuses import ABILITY_TO_STATUS, STATUS_MODS, STATUSES

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
    'CITY_SHOP_STOCK', 'CITY_NPC_NAMES', 'CITY_NPC_GLYPHS', 'CITY_NPC_SHOPS',
    'make_city_shop_stock', 'is_city_shop_npc',
]
