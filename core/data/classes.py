"""Классы персонажей и их эволюции."""
from __future__ import annotations

from typing import Any


from .items import ITEMS, parse_item_key

WCLASS_NAMES = {
    'sword': 'мечи', 'greatsword': 'двуручники', 'axe': 'топоры',
    'mace': 'булавы', 'hammer': 'молоты', 'shield': 'щиты',
    'spear': 'копья', 'halberd': 'алебарды', 'dagger': 'кинжалы',
    'knife': 'ножи', 'rapier': 'шпаги', 'katana': 'катаны',
    'staff': 'посохи', 'book': 'книги', 'bow': 'луки',
    'crossbow': 'арбалеты', 'fist': 'перчатки',
}
ARMOR_TYPE_NAMES = {
    'light': 'лёгкая', 'medium': 'средняя', 'heavy': 'тяжёлая', 'robe': 'мантия',
}


def _cls(name, tier, base, desc, color, *, next=(), bonus=None, stats=None,
         weapons=(), armor=(), effects=None, start_weapon=None,
         start_armor=None, start_items=None, perk='', active_skills=None):
    return {
        'name': name, 'desc': desc, 'tier': tier, 'base': base,
        'next': tuple(next), 'bonus': dict(bonus or {}),
        'stats': dict(stats) if stats else None,
        'weapons': tuple(weapons), 'armor_types': tuple(armor),
        'effects': dict(effects or {}), 'color': color,
        'start_weapon': start_weapon, 'start_armor': start_armor,
        'start_items': dict(start_items or {}), 'perk': perk,
        # Активные навыки (магия, способности) для быстрой панели.
        # Каждый навык: {'key','name','mana','cooldown','kind','damage'}
        'active_skills': tuple(active_skills or ()),
    }


CLASSES = {
    # ── База ──
    'warrior': _cls(
        'Воин', 1, 'warrior',
        'Крепкий боец. Больше всего STR — сильнейший удар.',
        (220, 130, 100),
        next=('squire', 'mercenary', 'fighter'),
        stats={'str': 8, 'agi': 3, 'vit': 7, 'luk': 3, 'df_stat': 7,
               'int': 3},
        weapons=('sword', 'axe', 'mace', 'hammer', 'shield', 'spear',
                 'halberd', 'greatsword', 'dagger'),
        armor=('heavy', 'medium', 'light'),
        start_armor='padded',
        perk='Универсал ближнего боя: любое тяжёлое оружие и броня.',
    ),
    'mage': _cls(
        'Маг', 1, 'mage',
        'Повелитель стихий. Много маны и критический удар посохом.',
        (120, 160, 240),
        next=('elements', 'nature', 'light', 'dark'),
        stats={'str': 2, 'agi': 5, 'vit': 4, 'luk': 6, 'df_stat': 4,
               'int': 10},
        weapons=('staff', 'book', 'dagger'),
        armor=('robe',),
        effects={'mana': 10},
        start_items={'potion_s': 2},
        perk='Носит только мантии, но водит посохом и книгой.',
        active_skills=(
            {'key': 'firebolt', 'name': 'Огненный снаряд',
             'mana': 5, 'cooldown': 1.5, 'kind': 'fireball', 'damage': 12,
             'desc': 'Огненный шар по ближайшему врагу.'},
            {'key': 'frost_bolt', 'name': 'Ледяной снаряд',
             'mana': 7, 'cooldown': 2.0, 'kind': 'frost', 'damage': 10,
             'desc': 'Замедляет цель.'},
            {'key': 'heal_self', 'name': 'Лечение',
             'mana': 8, 'cooldown': 4.0, 'kind': 'heal', 'damage': -20,
             'desc': 'Восстанавливает 20 HP.'},
        ),
    ),
    'rogue': _cls(
        'Разбойник', 1, 'rogue',
        'Быстрый и ловкий. Максимальное уклонение.',
        (140, 200, 130),
        next=('killer', 'archer', 'pickpocket', 'tracker'),
        stats={'str': 5, 'agi': 9, 'vit': 4, 'luk': 7, 'df_stat': 3,
               'int': 4},
        weapons=('dagger', 'knife', 'sword', 'rapier', 'bow',
                 'crossbow', 'katana'),
        armor=('light', 'medium'),
        start_weapon='dagger',
        perk='Лёгкая и средняя броня, оружие на любой вкус.',
    ),
    'artisan': _cls(
        'Ремесленник', 1, 'artisan',
        'Мастер на все руки. Быстрее всех прокачивает навыки создания.',
        (214, 184, 120),
        next=('apprentice', 'potionmaker'),
        stats={'str': 4, 'agi': 5, 'vit': 6, 'luk': 5, 'df_stat': 5,
               'int': 6},
        weapons=('hammer', 'mace', 'knife', 'dagger', 'staff'),
        armor=('light', 'medium'),
        effects={'craft_xp': 0.1},
        start_items={'wood': 3, 'stone': 3},
        perk='Перк «Ремесленник»: +10% опыта создания любых навыков.',
    ),
    'default': _cls(
        'Приключенец', 1, 'default',
        'Универсальный. 5/5/5/5/5.',
        (200, 200, 200),
        stats={'str': 5, 'agi': 5, 'vit': 5, 'luk': 5, 'df_stat': 5,
               'int': 5},
        weapons=('sword', 'axe', 'dagger', 'staff', 'bow'),
        armor=('light', 'medium', 'heavy', 'robe'),
        perk='Не имеет ограничений по снаряжению.',
    ),

    # ── Воин: 2-я ступень ──
    'squire': _cls(
        'Оруженосец', 2, 'warrior',
        'Щит и тяжёлая броня. Готовится стать рыцарем или паладином.',
        (214, 178, 120),
        next=('knight', 'paladin'),
        bonus={'vit': 2, 'df_stat': 2},
        weapons=('sword', 'axe', 'mace', 'shield', 'spear', 'hammer'),
        armor=('heavy', 'medium'),
        effects={'hp': 6},
        perk='Тяжёлая броня и щит.',
    ),
    'mercenary': _cls(
        'Наёмник', 2, 'warrior',
        'Практик: любое оружие в руках — оружие.',
        (200, 150, 110),
        next=('swordmaster', 'merc_captain'),
        bonus={'str': 2, 'agi': 1},
        weapons=('sword', 'axe', 'mace', 'hammer', 'spear', 'halberd',
                 'greatsword', 'shield', 'dagger'),
        armor=('medium',),
        effects={'atk': 1},
        perk='Средняя броня, широкий выбор оружия.',
    ),
    'fighter': _cls(
        'Боец', 2, 'warrior',
        'Агрессивный боец. Урон важнее защиты.',
        (230, 140, 90),
        next=('barbarian', 'berserker'),
        bonus={'str': 2, 'vit': 1},
        weapons=('axe', 'greatsword', 'sword', 'fist', 'hammer'),
        armor=('light',),
        effects={'atk': 1},
        perk='Лёгкая броня, максимум урона.',
    ),

    # ── Воин: 3-я ступень ──
    'knight': _cls(
        'Рыцарь', 3, 'warrior',
        'Танк: много брони и HP, основное оружие — щит.',
        (235, 205, 120),
        bonus={'vit': 3, 'df_stat': 3},
        weapons=('shield', 'sword', 'mace', 'hammer'),
        armor=('heavy',),
        effects={'hp': 12, 'df': 2},
        perk='+12 HP, +2 DEF, щит и тяжёлая броня.',
    ),
    'paladin': _cls(
        'Паладин', 3, 'warrior',
        'Полу-танк: самоисцеление и бафы. Основное оружие — молот.',
        (240, 220, 120),
        bonus={'str': 2, 'vit': 3, 'df_stat': 2},
        weapons=('hammer', 'mace', 'shield'),
        armor=('heavy',),
        effects={'hp': 8, 'lifesteal': 0.05},
        perk='5% нанесённого урона возвращается в HP.',
        stats={'str': 6, 'agi': 4, 'vit': 8, 'luk': 3, 'df_stat': 7},
        start_armor='leather',
    ),
    'swordmaster': _cls(
        'Мастер меча', 3, 'warrior',
        'Виртуоз клинка. Средняя броня, мечи и двуручники.',
        (200, 225, 250),
        bonus={'str': 3, 'agi': 2},
        weapons=('sword', 'greatsword', 'katana'),
        armor=('medium',),
        effects={'atk': 2, 'crit': 0.03},
        perk='+2 ATK, +3% крит. Только мечи и двуручники.',
    ),
    'merc_captain': _cls(
        'Глава наёмников', 3, 'warrior',
        'Мульти-оружие: копья, мечи, топоры, алебарды. Любое обычное оружие.',
        (205, 160, 100),
        bonus={'str': 2, 'agi': 2, 'vit': 2},
        weapons=('sword', 'axe', 'mace', 'hammer', 'spear', 'halberd',
                 'greatsword', 'shield', 'dagger', 'knife', 'rapier'),
        armor=('medium',),
        effects={'atk': 1, 'gold_pct': 0.15, 'plain_weapons': 1},
        perk='+15% золота; может носить любое оружие без «+».',
    ),
    'barbarian': _cls(
        'Варвар', 3, 'warrior',
        'ДПС-дилер: звериная ярость, топоры и лёгкая броня.',
        (220, 110, 80),
        bonus={'str': 4, 'vit': 1},
        weapons=('axe', 'greatsword'),
        armor=('light',),
        effects={'atk': 2, 'rage': 0.3},
        perk='Ярость: +30% урона, когда HP ниже 50%.',
    ),
    'berserker': _cls(
        'Берсерк', 3, 'warrior',
        'Скорость атаки растёт по мере потери здоровья.',
        (240, 90, 80),
        bonus={'str': 3, 'agi': 3},
        weapons=('fist', 'axe', 'sword'),
        armor=('light',),
        effects={'atk': 1, 'frenzy': 0.5},
        perk='Чем меньше HP, тем быстрее атака (до +50%).',
    ),

    # ── Маг: 2-я ступень ──
    'elements': _cls(
        'Школа Стихий', 2, 'mage',
        'Огонь, вода, молния, земля и воздух. Выбери стихию.',
        (140, 170, 250),
        next=('fire_mage', 'water_mage', 'lightning_mage',
              'earth_mage', 'air_mage'),
        bonus={'str': 1, 'luk': 2},
        weapons=('staff',),
        armor=('robe',),
        effects={'mana': 15},
        start_items={'scroll_fireball': 1},
        perk='Посох и мантия. +15 маны.',
    ),
    'nature': _cls(
        'Школа Природы', 2, 'mage',
        'Духи леса и их призыв.',
        (140, 200, 130),
        next=('druid',),
        bonus={'vit': 2, 'luk': 1},
        weapons=('staff',),
        armor=('robe',),
        effects={'hp': 6, 'summon': 1},
        perk='Дух-спутник помогает в бою.',
    ),
    'light': _cls(
        'Школа Света', 2, 'mage',
        'Исцеление и бафы союзников.',
        (245, 235, 170),
        next=('cleric',),
        bonus={'vit': 2, 'df_stat': 1},
        weapons=('book',),
        armor=('robe',),
        effects={'hp': 8},
        perk='Книга и мантия. Больше HP.',
    ),
    'dark': _cls(
        'Школа Тьмы', 2, 'mage',
        'Проклятия и поднятие павших.',
        (170, 120, 220),
        next=('necromancer',),
        bonus={'luk': 2, 'vit': 1},
        weapons=('book',),
        armor=('robe',),
        effects={'atk': 1, 'mana': 10},
        perk='Книга и мантия. Тёмная сила.',
    ),

    # ── Маг: 3-я ступень ──
    'fire_mage': _cls(
        'Маг Огня', 3, 'mage', 'Заклинания огня и испепеляющий посох.',
        (255, 130, 60), bonus={'luk': 3, 'str': 2},
        weapons=('staff',), armor=('robe',),
        effects={'atk': 3, 'mana': 20},
        perk='+3 ATK посохом, +20 маны.',
    ),
    'water_mage': _cls(
        'Маг Воды', 3, 'mage', 'Вода и лёд: замедление врагов.',
        (110, 170, 255), bonus={'luk': 3, 'str': 2},
        weapons=('staff',), armor=('robe',),
        effects={'atk': 2, 'mana': 25, 'slow_hit': 0.15},
        perk='Атаки иногда замедляют врага.',
    ),
    'lightning_mage': _cls(
        'Маг Молнии', 3, 'mage', 'Молнии бьют быстро и больно.',
        (230, 230, 120), bonus={'luk': 3, 'agi': 2},
        weapons=('staff',), armor=('robe',),
        effects={'atk': 2, 'crit': 0.05, 'mana': 20},
        perk='+5% крит, +20 маны.',
    ),
    'earth_mage': _cls(
        'Маг Земли', 3, 'mage', 'Каменная кожа и тяжёлые удары.',
        (180, 150, 100), bonus={'luk': 2, 'vit': 3},
        weapons=('staff',), armor=('robe',),
        effects={'atk': 2, 'df': 3, 'mana': 20},
        perk='+3 DEF, +20 маны.',
    ),
    'air_mage': _cls(
        'Маг Воздуха', 3, 'mage', 'Ветер: скорость и уклонение.',
        (200, 230, 240), bonus={'luk': 2, 'agi': 3},
        weapons=('staff',), armor=('robe',),
        effects={'dodge': 0.06, 'move_speed': 0.15, 'mana': 20},
        perk='+6% уклонения, быстрее ход.',
    ),
    'druid': _cls(
        'Друид', 3, 'mage', 'Духи атакуют врагов и бафают союзников.',
        (150, 210, 140), bonus={'vit': 3, 'luk': 2},
        weapons=('staff',), armor=('robe',),
        effects={'hp': 10, 'summon': 1, 'mana': 20},
        perk='Дух-спутник и +10 HP.',
    ),
    'cleric': _cls(
        'Клирик', 3, 'mage', 'Лечение себя и союзников, бафы.',
        (250, 240, 190), bonus={'vit': 3, 'df_stat': 2},
        weapons=('book',), armor=('robe',),
        effects={'hp': 12, 'lifesteal': 0.04, 'mana': 20},
        perk='4% вампиризма, +12 HP.',
    ),
    'necromancer': _cls(
        'Некромант', 3, 'mage', 'Проклятья и поднятые союзники.',
        (170, 110, 210), bonus={'luk': 3, 'vit': 1},
        weapons=('book',), armor=('robe',),
        effects={'atk': 2, 'summon': 1, 'mana': 25},
        perk='Слуга-нежить, +25 маны.',
    ),

    # ── Разбойник: 2-я ступень ──
    'killer': _cls(
        'Убийца', 2, 'rogue', 'Кинжалы, лёгкие мечи и лёгкая броня.',
        (170, 140, 240), next=('assassin', 'ninja'),
        bonus={'agi': 2, 'str': 1},
        weapons=('dagger', 'knife', 'sword'), armor=('light',),
        effects={'crit': 0.03},
        start_weapon='dagger',
        perk='+3% крит.',
    ),
    'archer': _cls(
        'Лучник', 2, 'rogue', 'Луки и арбалеты.',
        (170, 210, 140), next=('ranger', 'hunter'),
        bonus={'agi': 2, 'luk': 1},
        weapons=('bow', 'crossbow', 'dagger'), armor=('light',),
        effects={'atk': 1},
        start_weapon='bow',
        perk='Стрелковое оружие.',
    ),
    'pickpocket': _cls(
        'Карманник', 2, 'rogue', 'Больше золота с мобов.',
        (200, 190, 120), next=('thief',),
        bonus={'agi': 2, 'luk': 2},
        weapons=('knife', 'dagger'), armor=('light',),
        effects={'gold_pct': 0.15},
        perk='+15% золота с побеждённых.',
        start_weapon='knife',
    ),
    'tracker': _cls(
        'Ищейка', 2, 'rogue', 'Больше золота и опыта за задания, дальний обзор.',
        (180, 170, 140), next=('bounty_hunter',),
        bonus={'agi': 1, 'luk': 2},
        weapons=('rapier', 'sword'), armor=('light',),
        effects={'quest_gold': 0.2, 'quest_xp': 0.2, 'fov': 1},
        perk='+20% награды за задания, +1 обзор.',
        start_weapon='rapier',
    ),

    # ── Разбойник: 3-я ступень ──
    'assassin': _cls(
        'Ассасин', 3, 'rogue', 'ДПС: кинжалы и лёгкие мечи, лёгкая броня.',
        (160, 130, 240),
        bonus={'agi': 2, 'luk': 2},
        weapons=('dagger', 'sword', 'knife'), armor=('light',),
        effects={'crit': 0.05},
        perk='+5% крит. Смертоносный и хрупкий.',
        stats={'str': 3, 'agi': 7, 'vit': 4, 'luk': 9, 'df_stat': 4},
        start_weapon='dagger',
    ),
    'ninja': _cls(
        'Ниндзя', 3, 'rogue', 'Катаны и скорость.',
        (120, 120, 140),
        bonus={'agi': 4, 'luk': 3},
        weapons=('katana', 'sword'), armor=('light',),
        effects={'dodge': 0.05, 'atk': 1, 'move_speed': 0.1},
        perk='+5% уклонения, быстрее ход.',
    ),
    'ranger': _cls(
        'Рейнджер', 3, 'rogue', 'ДПС из луков и арбалетов.',
        (150, 220, 140),
        bonus={'agi': 3, 'luk': 2},
        weapons=('bow', 'crossbow'), armor=('light',),
        effects={'atk': 2, 'crit': 0.03},
        perk='+2 ATK, +3% крит.',
    ),
    'hunter': _cls(
        'Охотник', 3, 'rogue', 'Орёл или волк сражается рядом.',
        (190, 200, 130),
        bonus={'agi': 2, 'luk': 2, 'vit': 1},
        weapons=('bow', 'crossbow'), armor=('light',),
        effects={'atk': 1, 'summon': 1, 'hp': 4},
        perk='Спутник-зверь и +4 HP.',
    ),
    'thief': _cls(
        'Вор', 3, 'rogue', 'Золото и лут с мобов.',
        (230, 200, 110),
        bonus={'agi': 3, 'luk': 3},
        weapons=('dagger', 'knife'), armor=('light',),
        effects={'gold_pct': 0.25},
        perk='+25% золота с побеждённых.',
    ),
    'bounty_hunter': _cls(
        'Охотник за головами', 3, 'rogue', 'Награды за доски поручений и убийства.',
        (210, 180, 130),
        bonus={'agi': 3, 'luk': 3},
        weapons=('rapier', 'sword'), armor=('light',),
        effects={'quest_gold': 0.4, 'quest_xp': 0.4, 'fov': 1},
        perk='+40% награды за задания, +1 обзор.',
    ),

    # ── Ремесленник: 2-я ступень ──
    'apprentice': _cls(
        'Подмастерье', 2, 'artisan',
        'Мини-игра при создании вещей. x1,25 опыта кузнечного дела и инженерии.',
        (214, 190, 130),
        next=('blacksmith', 'magic_engineer'),
        bonus={'vit': 2},
        weapons=('hammer', 'knife', 'staff'),
        armor=('light', 'medium'),
        effects={'smithing_xp': 0.25, 'engineering_xp': 0.25,
                 'craft_quality': 1},
        perk='Мини-игра качества, x1,25 опыта создания.',
    ),
    'potionmaker': _cls(
        'Зельевар', 2, 'artisan',
        'Мини-игра при варке зелий. x1,5 опыта алхимии.',
        (170, 210, 150),
        next=('alchemist', 'herbalist'),
        bonus={'luk': 2},
        weapons=('knife', 'dagger'),
        armor=('light',),
        effects={'alchemy_xp': 0.5, 'craft_quality': 1},
        perk='Мини-игра качества, x1,5 опыта алхимии.',
    ),

    # ── Ремесленник: 3-я ступень ──
    'blacksmith': _cls(
        'Кузнец', 3, 'artisan',
        'Оружие и броня. До 3 дополнительных характеристик на предмет.',
        (220, 160, 110),
        bonus={'str': 2, 'vit': 3},
        weapons=('hammer', 'knife'), armor=('medium',),
        effects={'smithing_xp': 1.5, 'craft_quality': 1, 'extra_stats': 3},
        perk='x2,5 опыта ковки; +характеристики на крафт.',
    ),
    'magic_engineer': _cls(
        'Магический инженер', 3, 'artisan',
        'Руны и свитки. x2,5 опыта магической инженерии.',
        (150, 190, 230),
        bonus={'luk': 3, 'vit': 1},
        weapons=('staff', 'knife'), armor=('light',),
        effects={'engineering_xp': 1.5, 'craft_quality': 1},
        perk='x2,5 опыта инженерии.',
    ),
    'alchemist': _cls(
        'Алхимик', 3, 'artisan',
        'Зелья баффов: сила, ловкость, каменная кожа, скорость, проворность.',
        (190, 150, 220),
        bonus={'luk': 3, 'agi': 1},
        weapons=('knife', 'dagger'), armor=('light',),
        effects={'alchemy_xp': 1.5, 'craft_quality': 1},
        perk='x2,5 опыта алхимии, зелья баффов.',
    ),
    'herbalist': _cls(
        'Знахарь', 3, 'artisan',
        'Лучшие зелья восстановления и яды. x2,5 опыта алхимии.',
        (140, 200, 150),
        bonus={'luk': 2, 'vit': 2},
        weapons=('knife', 'dagger'), armor=('light',),
        effects={'alchemy_xp': 1.5, 'craft_quality': 1},
        perk='x2,5 опыта алхимии, яды.',
    ),
}

# Стартовые классы (порядок выбора в меню).
CLASS_ORDER = ('warrior', 'mage', 'rogue', 'artisan')
# Совместимость со старыми сейвами и тестами.
CLASS_ALIASES = {'assassin': 'assassin', 'paladin': 'paladin'}


def class_data(class_key: Any) -> Any: 
    """Данные класса с подстановкой базового при неизвестном ключе."""
    return CLASSES.get(class_key) or CLASSES.get('default', {})


def class_stats(class_key: Any) -> Any: 
    """Полные характеристики класса (для превью в меню)."""
    cls = class_data(class_key)
    if cls.get('stats'):
        stats = dict(cls['stats'])
        base = CLASSES.get(cls.get('base', 'default')) or CLASSES['default']
        for key, value in (base.get('stats') or {}).items():
            stats.setdefault(key, value)
        stats.setdefault('int', 5)
        return stats
    base = CLASSES.get(cls.get('base', 'default')) or CLASSES['default']
    stats = dict(base.get('stats') or {'str': 5, 'agi': 5, 'vit': 5,
                                       'luk': 5, 'df_stat': 5, 'int': 5})
    stats.setdefault('int', 5)
    for name, value in (cls.get('bonus') or {}).items():
        stats[name] = stats.get(name, 0) + value
    return stats


def class_can_use(class_key: Any, full_key: Any) -> Any: 
    """Может ли класс носить предмет. Возвращает (можно, причина)."""
    cls = class_data(class_key)
    base, rarity = parse_item_key(full_key)
    item = ITEMS.get(base)
    if not item:
        return True, ''
    req = item.get('class_req')
    if req:
        if class_key in req:
            return True, ''
        names = ', '.join(CLASSES[k]['name'] for k in req if k in CLASSES)
        return False, f"класс: {names}"
    wtype = item.get('wtype')
    if wtype and wtype not in cls.get('weapons', ()):
        # Глава наёмников носит любое обычное оружие без «+».
        if cls.get('effects', {}).get('plain_weapons') and rarity == 0:
            return True, ''
        return False, f"оружие: {WCLASS_NAMES.get(wtype, wtype)}"
    atype = item.get('armor_type')
    if atype and atype not in cls.get('armor_types', ()):
        return False, f"броня: {ARMOR_TYPE_NAMES.get(atype, atype)}"
    return True, ''


# ════════════════════ РЕДКОСТЬ ════════════════════
RARITY_CHANCES = [(0, 0.85), (1, 0.10), (2, 0.04), (3, 0.01)]
RARITY_COLORS = {0: None, 1: (170, 220, 170), 2: (140, 190, 255), 3: (255, 180, 80)}

