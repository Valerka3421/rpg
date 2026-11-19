"""Навыки крафта, рецепты и кривая опыта."""
from __future__ import annotations

from typing import Any

import random

from .items import ITEMS, make_loot_key, roll_rarity

SKILLS = {
    'blacksmith': {
        'name': 'Кузнечное дело',
        'desc': 'Слитки, оружие и броня.',
    },
    'alchemy': {
        'name': 'Алхимия',
        'desc': 'Зелья и магический порошок.',
    },
    'magic_engineering': {
        'name': 'Магическая инженерия',
        'desc': 'Свитки и руны.',
    },
}
SKILL_ORDER = ('blacksmith', 'alchemy', 'magic_engineering')

# Вкладки окна крафта. Ключ совпадает с категорией предмета.
CRAFT_TABS = (('all', 'Все крафты'),) + tuple(
    (key, SKILLS[key]['name']) for key in SKILL_ORDER)


def skill_xp_to_next(level: Any) -> Any: 
    """Опыт навыка для перехода с ``level`` на ``level + 1``."""
    return max(10, int(45 * (max(1, level) ** 1.35)))


def default_skills() -> Any: 
    return {key: {'level': 1, 'xp': 0} for key in SKILL_ORDER}


def craft_skill_reward(item_key: Any, count: Any = 1) -> Any: 
    """Сколько опыта навыка даёт крафт предмета."""
    item = ITEMS.get(item_key, {})
    tier = item.get('tier', 1)
    req = item.get('level_req', 1)
    return max(1, int((3 + tier * 2 + req) * max(1, count)))


# ── Кристаллы маны ──
def get_craftable_items() -> Any: 
    """Список (key, item) для крафта: предметы с рецептом."""
    from .items import ITEMS  # lazy: avoid circular import
    return [(k, v) for k, v in ITEMS.items()
            if v.get('recipe') or v.get('recipes')]


def xp_to_next(level: Any) -> Any: 
    """Сколько XP нужно для перехода с `level` на `level + 1`.

    Раньше кривая была экспоненциальной (10 * 1.6**n), из-за чего после
    14-го уровня требовались миллионы XP. Теперь — степенная: дороже,
    но достижимо на всей глубине подземелья.
    """
    from core.config import XP_BASE, XP_EXPONENT
    return max(1, int(XP_BASE * (max(1, level) ** XP_EXPONENT)))


def reroll_price(depth: Any) -> Any: 
    return 15 + depth * 3


def book_price_at_depth(base_price: Any, depth: Any) -> Any: 
    """Книги дорожают с глубиной: на 1-й — базовая цена, на 20-й — x3."""
    return int(base_price * (1 + depth * 0.1))


def make_shop_stock(depth: Any) -> Any: 
    stock = []
    def add(key: Any, price: Any) -> Any: 
        stock.append({'key': key, 'price': price, 'sold': False})

    # Книги перков
    perks = [
        ('book_double', 0.05, 120), ('book_regen', 0.10, 80),
        ('book_vampire', 0.08, 120), ('book_thorns', 0.10, 100),
        ('book_greedy', 0.10, 90),
    ]
    for bk, chance, price in perks:
        if random.random() < chance:
            add(bk, book_price_at_depth(price, depth))
    # Книги статов
    for bk in ('book_str', 'book_agi', 'book_vit', 'book_luk', 'book_def',
               'book_int'):
        if random.random() < 0.25:
            add(bk, book_price_at_depth(80, depth))

    # Чертёж особого крафта — редкая находка у торговца.
    if depth >= 3 and random.random() < 0.15:
        add(random.choice(('blueprint_artisan_blade',
                           'blueprint_philosophers_stone',
                           'blueprint_grand_elixir')),
            book_price_at_depth(180, depth))

    # Свитки
    if random.random() < 0.20:
        add('scroll_heal', book_price_at_depth(60, depth))
    if random.random() < 0.15:
        add('scroll_fireball', book_price_at_depth(80, depth))
    if depth >= 5 and random.random() < 0.10:
        add('scroll_freeze', book_price_at_depth(100, depth))

    tier = min(3, max(1, (depth + 2) // 3))
    potion_key = {1: 'potion_s', 2: 'potion_m', 3: 'potion_l'}[tier]
    weapon_pool = {1: ['dagger', 'shortsword'], 2: ['sword', 'axe'],
                   3: ['greatsword', 'warhammer']}[tier]
    armor_pool = {1: ['padded', 'leather'], 2: ['chain', 'scale'],
                  3: ['plate', 'mithril']}[tier]
    head_pool = {1: ['leather_cap'],
                 2: ['leather_cap', 'iron_helm'],
                 3: ['iron_helm', 'steel_helm', 'crown']}[tier]
    legs_pool = {1: ['cloth_pants'],
                 2: ['leather_legs', 'chain_legs', 'plate_greaves'],
                 3: ['plate_greaves', 'plate_legs']}[tier]
    boots_pool = {1: ['leather_boots'],
                  2: ['leather_boots', 'chain_boots'],
                  3: ['chain_boots', 'plate_boots']}[tier]
    if random.random() < 0.45:
        add(random.choice(head_pool), 10 * tier)
    if random.random() < 0.45:
        add(random.choice(legs_pool),  8 * tier)
    if random.random() < 0.40:
        add(random.choice(boots_pool), 7 * tier)

    add(potion_key, 5 * tier)
    add(potion_key, 5 * tier)
    if tier >= 2:
        add('mana_m' if tier >= 3 else 'mana_s', 4 * tier)
    wk = random.choice(weapon_pool)
    ak = random.choice(armor_pool)
    wr = roll_rarity()
    ar = roll_rarity()
    add(make_loot_key(wk, wr), 15 * tier + wr * 10)
    add(make_loot_key(ak, ar), 12 * tier + ar * 10)
    add(head_pool[0], 8 * tier)
    add(random.choice(legs_pool), 8 * tier)
    add(random.choice(boots_pool), 6 * tier)
    return stock


# ════════════════════ ДРОП ════════════════════
# Только материалы/трофеи. Золото начисляется отдельно в
# rules.roll_kill_reward и только разумным монстрам: животные (крыса,
# олень, волк, медведь, слайм, кабан) монет не носят.
