"""Игровые данные: монстры, боссы, предметы, магазин, дроп, редкости, крафт, классы."""
import random


MONSTERS = {
    'rat':      {'ch': 'r', 'name': 'Крыса',  'hp': 3,  'atk': 1, 'df': 0, 'xp': 3,  'color': (170, 150, 130), 'min_depth': 1, 'ability': 'poison'},
    'goblin':   {'ch': 'g', 'name': 'Гоблин', 'hp': 8,  'atk': 3, 'df': 1, 'xp': 6,  'color': (120, 190, 100), 'min_depth': 1},
    'archer':   {'ch': 'a', 'name': 'Гоблин-лучник', 'hp': 6, 'atk': 2, 'df': 0, 'xp': 7, 'color': (150, 200, 100), 'min_depth': 2, 'ability': 'ranged'},
    'skeleton': {'ch': 's', 'name': 'Скелет', 'hp': 12, 'atk': 4, 'df': 1, 'xp': 12, 'color': (220, 220, 220), 'min_depth': 2},
    'shaman':   {'ch': 'S', 'name': 'Гоблин-шаман', 'hp': 10, 'atk': 2, 'df': 1, 'xp': 14, 'color': (180, 140, 220), 'min_depth': 3, 'ability': 'healer'},
    'orc':      {'ch': 'O', 'name': 'Орк',    'hp': 20, 'atk': 6, 'df': 2, 'xp': 22, 'color': (150, 95, 60),   'min_depth': 3},
    'troll':    {'ch': 'T', 'name': 'Тролль', 'hp': 35, 'atk': 8, 'df': 3, 'xp': 40, 'color': (85, 140, 85),   'min_depth': 4},
    'demon':    {'ch': 'D', 'name': 'Демон',  'hp': 50, 'atk': 12,'df': 4, 'xp': 70, 'color': (200, 70, 70),   'min_depth': 6, 'ability': 'burning'},
    'vampire':  {'ch': 'v', 'name': 'Вампир', 'hp': 70, 'atk': 14, 'df': 5, 'xp': 100, 'color': (180, 50, 80), 'min_depth': 8, 'ability': 'poison'},
    'dragonling':{'ch': 'd','name': 'Дракончик','hp': 90, 'atk': 16, 'df': 5, 'xp': 130, 'color': (200, 150, 60), 'min_depth': 10, 'ability': 'burning'},
    'golem':    {'ch': 'Y', 'name': 'Голем',  'hp': 120, 'atk': 14, 'df': 8, 'xp': 150, 'color': (130, 130, 140), 'min_depth': 12},
    'wraith':   {'ch': 'W', 'name': 'Призрак','hp': 80, 'atk': 18, 'df': 4, 'xp': 140, 'color': (180, 180, 220), 'min_depth': 14, 'ability': 'bleeding'},
}

BOSSES = {
    5:  {'ch': 'G', 'name': 'Король гоблинов',  'hp': 80,  'atk': 9,  'df': 2, 'xp': 150,  'color': (255, 100, 100)},
    10: {'ch': 'L', 'name': 'Лич',              'hp': 150, 'atk': 12, 'df': 4, 'xp': 500,  'color': (200, 100, 255)},
    15: {'ch': 'V', 'name': 'Владыка вампиров', 'hp': 250, 'atk': 18, 'df': 6, 'xp': 700,  'color': (200, 40, 80)},
    20: {'ch': 'A', 'name': 'Древний дракон',   'hp': 400, 'atk': 22, 'df': 8, 'xp': 1200, 'color': (240, 130, 40)},
}

ITEMS = {
    # Зелья
    'potion_s': {'ch': '!', 'name': 'Малое зелье', 'type': 'heal', 'value': 10,
                 'color': (230, 130, 130), 'tier': 1, 'sell': 2,
                 'desc': 'Восстанавливает 10 HP.'},
    'potion_m': {'ch': '!', 'name': 'Зелье', 'type': 'heal', 'value': 20,
                 'color': (230, 90, 100), 'tier': 2, 'sell': 4,
                 'desc': 'Восстанавливает 20 HP.'},
    'potion_l': {'ch': '!', 'name': 'Большое зелье', 'type': 'heal', 'value': 40,
                 'color': (255, 60, 80), 'tier': 3, 'sell': 6,
                 'desc': 'Восстанавливает 40 HP.'},
    # Оружие
    'dagger':     {'ch': '/', 'name': 'Кинжал', 'type': 'weapon', 'value': 1,
                   'level_req': 1, 'color': (180, 200, 230), 'tier': 1, 'sell': 7,
                   'crit_bonus': 0.02,
                   'desc': '+1 ATK, +2% крит.'},
    'shortsword': {'ch': '/', 'name': 'Короткий меч', 'type': 'weapon', 'value': 1,
                   'level_req': 2, 'color': (190, 210, 240), 'tier': 1, 'sell': 9,
                   'desc': '+1 ATK.'},
    'sword':      {'ch': '/', 'name': 'Меч', 'type': 'weapon', 'value': 2,
                   'level_req': 4, 'color': (170, 200, 240), 'tier': 2, 'sell': 14,
                   'desc': '+2 ATK.'},
    'axe':        {'ch': '/', 'name': 'Топор', 'type': 'weapon', 'value': 3,
                   'level_req': 5, 'color': (200, 180, 150), 'tier': 2, 'sell': 18,
                   'dodge_bonus': -0.02, 'desc': '+3 ATK, −2% укл.'},
    'greatsword': {'ch': '/', 'name': 'Двуручник', 'type': 'weapon', 'value': 4,
                   'level_req': 7, 'color': (210, 230, 255), 'tier': 3, 'sell': 21,
                   'desc': '+4 ATK.'},
    'warhammer':  {'ch': '/', 'name': 'Молот', 'type': 'weapon', 'value': 5,
                   'level_req': 8, 'color': (180, 170, 160), 'tier': 3, 'sell': 26,
                   'dodge_bonus': -0.03, 'desc': '+5 ATK, −3% укл.'},
    # Крафт-оружие
    'bone_sword': {'ch': '/', 'name': 'Костяной меч', 'type': 'weapon', 'value': 2,
                   'level_req': 3, 'color': (230, 220, 200), 'tier': 2, 'sell': 12,
                   'recipe': {'bone': 2, 'wood': 1}, 'desc': '+2 ATK.'},
    'demon_blade': {'ch': '/', 'name': 'Клинок демона', 'type': 'weapon', 'value': 5,
                    'level_req': 8, 'color': (200, 70, 70), 'tier': 3, 'sell': 35,
                    'recipe': {'demon_scale': 3, 'orc_tusk': 1, 'sword': 1},
                    'desc': '+5 ATK.'},
    'golem_hammer': {'ch': '/', 'name': 'Молот голема', 'type': 'weapon', 'value': 6,
                     'level_req': 10, 'color': (130, 130, 140), 'tier': 3, 'sell': 40,
                     'recipe': {'golem_core': 1, 'stone': 5, 'greatsword': 1},
                     'desc': '+6 ATK.'},
    # Броня
    'padded':     {'ch': '[', 'name': 'Стёганая куртка', 'type': 'armor', 'value': 1,
                   'level_req': 1, 'color': (200, 180, 140), 'tier': 1, 'sell': 7,
                   'desc': '+1 DEF.'},
    'leather':    {'ch': '[', 'name': 'Кожаная броня', 'type': 'armor', 'value': 1,
                   'level_req': 1, 'color': (200, 170, 100), 'tier': 1, 'sell': 6,
                   'desc': '+1 DEF.'},
    'chain':      {'ch': '[', 'name': 'Кольчуга', 'type': 'armor', 'value': 2,
                   'level_req': 4, 'color': (170, 170, 180), 'tier': 2, 'sell': 12,
                   'desc': '+2 DEF.'},
    'scale':      {'ch': '[', 'name': 'Чешуйчатая', 'type': 'armor', 'value': 2,
                   'level_req': 4, 'color': (160, 150, 130), 'tier': 2, 'sell': 14,
                   'hp_bonus': 3, 'desc': '+2 DEF, +3 HP.'},
    'plate':      {'ch': '[', 'name': 'Латы', 'type': 'armor', 'value': 3,
                   'level_req': 7, 'color': (220, 220, 240), 'tier': 3, 'sell': 18,
                   'desc': '+3 DEF.'},
    'mithril':    {'ch': '[', 'name': 'Мифрил', 'type': 'armor', 'value': 3,
                   'level_req': 7, 'color': (180, 210, 230), 'tier': 3, 'sell': 28,
                   'dodge_bonus': 0.05, 'desc': '+3 DEF, +5% укл.'},
    # Крафт-броня
    'leather_armor': {'ch': '[', 'name': 'Броня из огра', 'type': 'armor', 'value': 3,
                      'level_req': 5, 'color': (150, 110, 60), 'tier': 2, 'sell': 22,
                      'recipe': {'ogre_leather': 3, 'goblin_ear': 1},
                      'desc': '+3 DEF.'},
    'dragon_armor': {'ch': '[', 'name': 'Броня дракона', 'type': 'armor', 'value': 4,
                     'level_req': 10, 'color': (240, 130, 40), 'tier': 3, 'sell': 45,
                     'recipe': {'dragon_scale': 5, 'plate': 1},
                     'desc': '+4 DEF.'},
    'lich_robe': {'ch': '[', 'name': 'Мантия лича', 'type': 'armor', 'value': 4,
                  'level_req': 9, 'color': (200, 100, 255), 'tier': 3, 'sell': 40,
                  'recipe': {'bone': 3, 'wraith_essence': 2, 'plate': 1},
                  'desc': '+4 DEF.'},
    # Золото
    'gold_small':  {'ch': '$', 'name': 'Монеты', 'type': 'gold', 'value': 5,
                    'color': (240, 210, 90), 'tier': 1, 'desc': 'Немного золота.'},
    'gold_medium': {'ch': '$', 'name': 'Куча монет', 'type': 'gold', 'value': 15,
                    'color': (250, 220, 100), 'tier': 2, 'desc': 'Приличная сумма.'},
    'gold_large':  {'ch': '$', 'name': 'Сокровище', 'type': 'gold', 'value': 40,
                    'color': (255, 230, 120), 'tier': 3, 'desc': 'Богатая находка.'},
    # Книги
    'book_str': {'ch': '?', 'name': 'Книга силы', 'type': 'book',
                 'stat': 'str', 'value': 5, 'color': (255, 130, 130),
                 'tier': 3, 'sell': 30, 'shop_price': 80, 'desc': '+5 STR навсегда.'},
    'book_agi': {'ch': '?', 'name': 'Книга ловкости', 'type': 'book',
                 'stat': 'agi', 'value': 5, 'color': (130, 255, 130),
                 'tier': 3, 'sell': 30, 'shop_price': 80, 'desc': '+5 AGI навсегда.'},
    'book_vit': {'ch': '?', 'name': 'Книга выносливости', 'type': 'book',
                 'stat': 'vit', 'value': 5, 'color': (130, 130, 255),
                 'tier': 3, 'sell': 30, 'shop_price': 80, 'desc': '+5 VIT навсегда.'},
    'book_luk': {'ch': '?', 'name': 'Книга удачи', 'type': 'book',
                 'stat': 'luk', 'value': 5, 'color': (255, 255, 130),
                 'tier': 3, 'sell': 30, 'shop_price': 80, 'desc': '+5 LUK навсегда.'},
    'book_def': {'ch': '?', 'name': 'Книга защиты', 'type': 'book',
                 'stat': 'df_stat', 'value': 5, 'color': (160, 200, 255),
                 'tier': 3, 'sell': 30, 'shop_price': 80, 'desc': '+5 DEF навсегда.'},
    'book_double': {'ch': '?', 'name': 'Книга двойного удара', 'type': 'book',
                    'stat': 'double_strike', 'value': 0.15,
                    'color': (200, 150, 255), 'tier': 3, 'sell': 40, 'shop_price': 120,
                    'desc': '+15% шанс ударить дважды.'},
    'book_regen': {'ch': '?', 'name': 'Книга регенерации', 'type': 'book',
                   'stat': 'regen', 'value': 1, 'color': (150, 240, 150),
                   'tier': 3, 'sell': 30, 'shop_price': 80,
                   'desc': '+1 HP каждые 2 секунды.'},
    'book_vampire': {'ch': '?', 'name': 'Книга вампиризма', 'type': 'book',
                     'stat': 'vampire', 'value': 0.05, 'color': (220, 80, 80),
                     'tier': 3, 'sell': 40, 'shop_price': 120,
                     'desc': '5% урона возвращается в HP.'},
    'book_thorns': {'ch': '?', 'name': 'Книга шипов', 'type': 'book',
                    'stat': 'thorns', 'value': 2, 'color': (200, 200, 80),
                    'tier': 3, 'sell': 35, 'shop_price': 100,
                    'desc': '2 урона атакующему.'},
    'book_greedy': {'ch': '?', 'name': 'Книга жадности', 'type': 'book',
                    'stat': 'greedy', 'value': 1.5, 'color': (255, 200, 80),
                    'tier': 3, 'sell': 30, 'shop_price': 90,
                    'desc': '+50% золота.'},
    # Ресурсы
    'mushroom': {'ch': ',', 'name': 'Гриб', 'type': 'resource', 'heal': 5,
                 'value': 0, 'sell': 1, 'color': (200, 150, 100), 'tier': 1,
                 'desc': '+5 HP.'},
    'berry':    {'ch': ':', 'name': 'Ягоды', 'type': 'resource', 'heal': 3,
                 'value': 0, 'sell': 1, 'color': (220, 80, 120), 'tier': 1,
                 'desc': '+3 HP.'},
    'wood':     {'ch': '=', 'name': 'Дерево', 'type': 'resource',
                 'value': 0, 'sell': 2, 'color': (150, 100, 60), 'tier': 1,
                 'desc': 'Материал для крафта.'},
    'stone':    {'ch': '*', 'name': 'Камень', 'type': 'resource',
                 'value': 0, 'sell': 1, 'color': (150, 150, 160), 'tier': 1,
                 'desc': 'Материал для крафта.'},
    'ore':      {'ch': '#', 'name': 'Руда', 'type': 'resource',
                 'value': 0, 'sell': 5, 'color': (200, 170, 80), 'tier': 2,
                 'desc': 'Ценный материал.'},
    # Материалы
    'bone':          {'ch': '%', 'name': 'Кость',            'type': 'material', 'sell': 3,  'color': (230, 220, 200), 'tier': 2, 'desc': 'Кость скелета.'},
    'ogre_leather':  {'ch': '~', 'name': 'Кожа огра',        'type': 'material', 'sell': 5,  'color': (150, 110, 60),  'tier': 2, 'desc': 'Прочная кожа.'},
    'goblin_ear':    {'ch': '?', 'name': 'Ухо гоблина',      'type': 'material', 'sell': 2,  'color': (100, 180, 100), 'tier': 1, 'desc': 'Трофей.'},
    'rat_tail':      {'ch': '~', 'name': 'Хвост крысы',      'type': 'material', 'sell': 1,  'color': (170, 150, 130), 'tier': 1, 'desc': 'Трофей.'},
    'orc_tusk':      {'ch': 'v', 'name': 'Клык орка',        'type': 'material', 'sell': 4,  'color': (240, 240, 220), 'tier': 2, 'desc': 'Острый клык.'},
    'demon_scale':   {'ch': '*', 'name': 'Чешуя демона',     'type': 'material', 'sell': 8,  'color': (200, 70, 70),   'tier': 3, 'desc': 'Обжигающая чешуя.'},
    'vampire_fang':  {'ch': 'v', 'name': 'Клык вампира',     'type': 'material', 'sell': 10, 'color': (220, 30, 60),   'tier': 3, 'desc': 'Острый клык.'},
    'dragon_scale':  {'ch': '*', 'name': 'Чешуя дракона',    'type': 'material', 'sell': 15, 'color': (240, 130, 40),  'tier': 3, 'desc': 'Прочная чешуя.'},
    'golem_core':    {'ch': 'o', 'name': 'Ядро голема',      'type': 'material', 'sell': 12, 'color': (200, 220, 255), 'tier': 3, 'desc': 'Магическое ядро.'},
    'wraith_essence':{'ch': '~', 'name': 'Эссенция призрака','type': 'material', 'sell': 8,  'color': (180, 180, 220), 'tier': 3, 'desc': 'Призрачная субстанция.'},
    # ── Свитки ──
    'scroll_heal': {'ch': '&', 'name': 'Свиток лечения', 'type': 'scroll',
                    'effect': 'heal', 'value': 30, 'color': (120, 240, 120),
                    'tier': 2, 'sell': 12, 'shop_price': 60,
                    'desc': 'Восстанавливает 30 HP.'},
    'scroll_fireball': {'ch': '&', 'name': 'Свиток огня', 'type': 'scroll',
                        'effect': 'fireball', 'value': 20, 'color': (255, 120, 50),
                        'tier': 2, 'sell': 15, 'shop_price': 80,
                        'desc': 'Огонь 20 урона по области 3×3 вокруг ближайшего врага.'},
    'scroll_freeze': {'ch': '&', 'name': 'Свиток заморозки', 'type': 'scroll',
                      'effect': 'freeze', 'value': 3, 'color': (120, 180, 255),
                      'tier': 3, 'sell': 18, 'shop_price': 100,
                      'desc': 'Замедляет всех врагов в радиусе 5 на 3 хода.'},
}

MATERIAL_KEYS = ('bone', 'ogre_leather', 'goblin_ear', 'rat_tail', 'orc_tusk',
                 'demon_scale', 'vampire_fang', 'dragon_scale',
                 'golem_core', 'wraith_essence')

# ════════════════════ СТАТУС-ЭФФЕКТЫ ════════════════════
STATUSES = {
    'poison': {
        'name': 'Отравление', 'short': 'PSN',
        'color': (120, 200, 100),
        'damage': 1, 'heal': 0,
        'duration': 3,
    },
    'burning': {
        'name': 'Горение', 'short': 'BRN',
        'color': (255, 100, 50),
        'damage': 3, 'heal': 0,
        'duration': 3,
    },
    'bleeding': {
        'name': 'Кровотечение', 'short': 'BLD',
        'color': (200, 30, 30),
        'damage': 2, 'heal': 0,
        'duration': 4,
    },
    'slow': {
        'name': 'Замедление', 'short': 'SLW',
        'color': (100, 150, 255),
        'damage': 0, 'heal': 0,
        'duration': 2,
    },
    'regen': {
        'name': 'Регенерация', 'short': 'RGN',
        'color': (120, 240, 120),
        'damage': 0, 'heal': 2,
        'duration': 4,
    },
}

# Способность монстра → накладываемый статус (если есть)
ABILITY_TO_STATUS = {
    'poison':  'poison',
    'burning': 'burning',
    'bleeding':'bleeding',
    'slow':    'slow',
}

# ════════════════════ КЛАССЫ ════════════════════
CLASSES = {
    'warrior': {
        'name': 'Воин',
        'desc': 'Крепкий боец. Больше всего STR — сильнейший удар.',
        'str': 8, 'agi': 3, 'vit': 7, 'luk': 3, 'df_stat': 7,
        'start_weapon': None, 'start_armor': 'padded', 'start_items': {},
        'color': (220, 130, 100),
    },
    'assassin': {
        'name': 'Ассасин',
        'desc': 'Мастер критических ударов. Хрупкий, но смертоносный.',
        'str': 3, 'agi': 7, 'vit': 4, 'luk': 9, 'df_stat': 4,
        'start_weapon': 'dagger', 'start_armor': None, 'start_items': {},
        'color': (160, 130, 240),
    },
    'rogue': {
        'name': 'Разбойник',
        'desc': 'Быстрый и ловкий. Максимальное уклонение.',
        'str': 5, 'agi': 9, 'vit': 4, 'luk': 7, 'df_stat': 3,
        'start_weapon': 'dagger', 'start_armor': None, 'start_items': {},
        'color': (140, 200, 130),
    },
    'paladin': {
        'name': 'Паладин',
        'desc': 'Священный танк. Много HP и защита.',
        'str': 6, 'agi': 4, 'vit': 8, 'luk': 3, 'df_stat': 7,
        'start_weapon': None, 'start_armor': 'leather', 'start_items': {},
        'color': (240, 220, 120),
    },
    'default': {
        'name': 'Приключенец',
        'desc': 'Универсальный. 5/5/5/5/5.',
        'str': 5, 'agi': 5, 'vit': 5, 'luk': 5, 'df_stat': 5,
        'start_weapon': None, 'start_armor': None, 'start_items': {},
        'color': (200, 200, 200),
    },
}
CLASS_ORDER = ('warrior', 'assassin', 'rogue', 'paladin')


# ════════════════════ РЕДКОСТЬ ════════════════════
RARITY_CHANCES = [(0, 0.85), (1, 0.10), (2, 0.04), (3, 0.01)]
RARITY_COLORS = {0: None, 1: (170, 220, 170), 2: (140, 190, 255), 3: (255, 180, 80)}


def roll_rarity():
    r = random.random()
    c = 0.0
    for level, chance in RARITY_CHANCES:
        c += chance
        if r < c:
            return level
    return 0


def make_loot_key(base_key, rarity):
    return base_key if rarity <= 0 else f"{base_key}+{rarity}"


def parse_item_key(full_key):
    """'sword+2' или ItemInstance('sword', 2) → (base, rarity)."""
    if not full_key:
        return None, 0
    if isinstance(full_key, ItemInstance):
        return full_key.base, full_key.rarity
    if '+' in full_key:
        base, lvl = full_key.rsplit('+', 1)
        try:
            return base, int(lvl)
        except ValueError:
            return full_key, 0
    return full_key, 0


def get_item_data(full_key):
    base, _ = parse_item_key(full_key)
    return ITEMS.get(base, {})


def item_display_name(full_key):
    base, lvl = parse_item_key(full_key)
    name = ITEMS.get(base, {}).get('name', base or '?')
    return f"{name} +{lvl}" if lvl > 0 else name


def item_display_color(full_key):
    base, lvl = parse_item_key(full_key)
    if lvl in RARITY_COLORS and RARITY_COLORS[lvl] is not None:
        return RARITY_COLORS[lvl]
    return ITEMS.get(base, {}).get('color', (200, 200, 200))


def item_sell_price(full_key):
    base, lvl = parse_item_key(full_key)
    return ITEMS.get(base, {}).get('sell', 0) + lvl * 10


# ════════════════════ МАГАЗИН ════════════════════
def scale_stats(hp, atk, df, depth):
    """Обычные мобы: HP растёт заметно, ATK/DEF — умеренно."""
    hp_factor = 1 + (depth - 1) * 0.10
    return (int(hp * hp_factor),
            atk + (depth - 1) // 4,
            df + (depth - 1) // 5)


def scale_boss_stats(hp, atk, df, depth):
    """Боссы масштабируются мягче обычных мобов — иначе финальный
    дракон требует часов гринда."""
    hp_factor = 1 + (depth - 1) * 0.06
    return (int(hp * hp_factor),
            atk + (depth - 1) // 6,
            df + (depth - 1) // 8)


# ════════════════════ ОПЫТ И УРОВНИ ════════════════════
def xp_to_next(level):
    """Сколько XP нужно для перехода с `level` на `level + 1`.

    Раньше кривая была экспоненциальной (10 * 1.6**n), из-за чего после
    14-го уровня требовались миллионы XP. Теперь — степенная: дороже,
    но достижимо на всей глубине подземелья.
    """
    from core.config import XP_BASE, XP_EXPONENT
    return max(1, int(XP_BASE * (max(1, level) ** XP_EXPONENT)))


def reroll_price(depth):
    return 15 + depth * 3


def book_price_at_depth(base_price, depth):
    """Книги дорожают с глубиной: на 1-й — базовая цена, на 20-й — x3."""
    return int(base_price * (1 + depth * 0.1))


def make_shop_stock(depth):
    stock = []
    def add(key, price):
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
    for bk in ('book_str', 'book_agi', 'book_vit', 'book_luk', 'book_def'):
        if random.random() < 0.25:
            add(bk, book_price_at_depth(80, depth))

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

    add(potion_key, 5 * tier)
    add(potion_key, 5 * tier)
    wk = random.choice(weapon_pool)
    ak = random.choice(armor_pool)
    wr = roll_rarity()
    ar = roll_rarity()
    add(make_loot_key(wk, wr), 15 * tier + wr * 10)
    add(make_loot_key(ak, ar), 12 * tier + ar * 10)
    return stock


# ════════════════════ ДРОП ════════════════════
MONSTER_DROPS = {
    'r': [('gold_small', 0.60), ('mushroom', 0.25), ('rat_tail', 0.30)],
    'g': [('gold_small', 0.70), ('wood', 0.25), ('goblin_ear', 0.25)],
    's': [('gold_medium', 0.45), ('stone', 0.30), ('bone', 0.45)],
    'O': [('gold_medium', 0.55), ('ore', 0.20), ('orc_tusk', 0.25)],
    'T': [('gold_medium', 0.60), ('wood', 0.40), ('ogre_leather', 0.35)],
    'D': [('gold_large', 0.30), ('ore', 0.35), ('demon_scale', 0.30)],
    'v': [('gold_large', 0.30), ('ore', 0.25), ('vampire_fang', 0.30)],
    'd': [('gold_large', 0.40), ('ore', 0.35), ('dragon_scale', 0.25)],
    'Y': [('gold_large', 0.35), ('stone', 0.70), ('golem_core', 0.30)],
    'W': [('gold_large', 0.30), ('ore', 0.20), ('wraith_essence', 0.30)],
}

BOSS_DROPS = {
    'G': ['goblin_ear', 'goblin_ear', 'bone'],
    'L': ['bone', 'bone', 'wraith_essence', 'wraith_essence'],
    'V': ['vampire_fang', 'vampire_fang', 'vampire_fang'],
    'A': ['dragon_scale'] * 5,
}


def roll_monster_drops(monster_ch):
    return [k for k, c in MONSTER_DROPS.get(monster_ch, []) if random.random() < c]


def get_craftable_items():
    return sorted([(k, v) for k, v in ITEMS.items() if 'recipe' in v],
                  key=lambda kv: (kv[1].get('level_req', 99), kv[0]))

# ════════════════════ ПЕРКИ ════════════════════
# flag → (название, описание, книга-источник)
PERK_INFO = {
    'double_strike': ('Двойной удар', '15% шанс ударить дважды',      'book_double'),
    'regen':         ('Регенерация', '+1 HP каждые 2 секунды',          'book_regen'),
    'vampire':       ('Вампиризм',   '5% урона возвращается в HP',    'book_vampire'),
    'thorns':        ('Шипы',        '2 урона атакующему',            'book_thorns'),
    'greedy':        ('Жадность',    '+50% золота с побеждённых',     'book_greedy'),
}
PERK_BOOK_MAP = {flag: info[2] for flag, info in PERK_INFO.items()}

# ════════════════════ КВЕСТЫ ════════════════════
QUESTS = {
    'q_rats': {
        'name': 'Нашествие крыс',
        'desc': 'Убей 5 крыс в открытом мире или подземелье.',
        'type': 'kill', 'target': 'r', 'count': 5,
        'reward_gold': 20, 'reward_xp': 15, 'reward_items': {},
        'giver': 'elder',
    },
    'q_bones': {
        'name': 'Кости для старейшины',
        'desc': 'Принеси 3 кости скелета.',
        'type': 'collect', 'target': 'bone', 'count': 3,
        'reward_gold': 30, 'reward_xp': 25,
        'reward_items': {'potion_m': 1},
        'giver': 'elder',
    },
    'q_first_step': {
        'name': 'Первый шаг',
        'desc': 'Спустись на глубину 5.',
        'type': 'reach', 'target': 5, 'count': 1,
        'reward_gold': 50, 'reward_xp': 50,
        'reward_items': {'sword': 1},
        'giver': 'elder',
    },
    'q_goblin_king': {
        'name': 'Голова короля',
        'desc': 'Убей Короля гоблинов на глубине 5.',
        'type': 'kill', 'target': 'G', 'count': 1,
        'reward_gold': 100, 'reward_xp': 100,
        'reward_items': {'book_str': 1},
        'giver': 'merchant',
    },
    'q_craft_bone': {
        'name': 'Кузнец из тебя',
        'desc': 'Создай костяной меч (2 кости + 1 дерево).',
        'type': 'craft', 'target': 'bone_sword', 'count': 1,
        'reward_gold': 40, 'reward_xp': 30, 'reward_items': {},
        'giver': 'merchant',
    },
    'q_dragon': {
        'name': 'Убить дракона',
        'desc': 'Победи Древнего дракона на глубине 20.',
        'type': 'kill', 'target': 'A', 'count': 1,
        'reward_gold': 500, 'reward_xp': 1000,
        'reward_items': {'book_double': 1},
        'giver': 'merchant',
    },
}


def quests_by_giver(giver):
    """Список ключей квестов, которые выдаёт NPC."""
    return [k for k, q in QUESTS.items() if q.get('giver') == giver]


# ════════════════════ ITEM INSTANCE ════════════════════
from dataclasses import dataclass


@dataclass(frozen=True)
class ItemInstance:
    """Типизированное представление предмета с редкостью.

    Ведёт себя как строка через .key() — для совместимости с dict
    и старым кодом. Можно использовать вместо 'sword+2':
        ItemInstance('sword', 2) == ItemInstance.from_key('sword+2')
    """
    base: str
    rarity: int = 0

    def key(self):
        return make_loot_key(self.base, self.rarity)

    def __str__(self):
        return self.key()

    def data(self):
        return ITEMS.get(self.base, {})

    def display_name(self):
        return item_display_name(self.key())

    def display_color(self):
        return item_display_color(self.key())

    def sell_price(self):
        return item_sell_price(self.key())

    @classmethod
    def from_key(cls, full_key):
        base, rarity = parse_item_key(full_key)
        return cls(base or '', rarity)
