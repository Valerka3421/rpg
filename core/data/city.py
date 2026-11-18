"""Городские торговцы: фиксированный ассортимент и имена горожан.

Ассортимент горожан не зависит от глубины и случайности — покупки в городе
предсказуемы, а реролл для них отключён. Товары можно покупать повторно
(``infinite``), иначе после одной закупки лавка опустела бы.
"""

CITY_SHOP_STOCK = {
    'blacksmith': (
        ('iron_ingot', 12), ('steel_ingot', 22), ('mithril_ingot', 40),
        ('ore', 10), ('wood', 4), ('stone', 3),
    ),
    'alchemist': (
        ('potion_s', 8), ('potion_m', 16), ('potion_l', 30),
        ('mana_s', 10), ('mana_m', 22),
        ('magic_powder', 18),
    ),
    'innkeeper': (
        ('fried_mushrooms', 6), ('berry_compote', 8),
        ('roast_venison', 12), ('roast_boar', 16), ('roast_bear', 22),
    ),
}

CITY_NPC_NAMES = {
    'guard': 'Стражник',
    'blacksmith': 'Кузнец',
    'alchemist': 'Алхимик',
    'innkeeper': 'Трактирщик',
}

CITY_NPC_GLYPHS = {
    'guard': ('G', (196, 200, 220)),
    'blacksmith': ('K', (206, 152, 88)),
    'alchemist': ('A', (156, 126, 224)),
    'innkeeper': ('I', (214, 172, 112)),
}

CITY_NPC_SHOPS = {
    'blacksmith': 'Кузнец',
    'alchemist': 'Алхимик',
    'innkeeper': 'Трактирщик',
}


def make_city_shop_stock(npc_type):
    """Фиксированный ассортимент городского торговца."""
    stock = []
    for key, price in CITY_SHOP_STOCK.get(npc_type, ()):
        stock.append({'key': key, 'price': price, 'sold': False,
                      'infinite': True})
    return stock


def is_city_shop_npc(npc_type):
    return npc_type in CITY_SHOP_STOCK
