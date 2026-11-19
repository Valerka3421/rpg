"""Статус-эффекты и модификаторы."""
from __future__ import annotations




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
    # Зелья баффов (алхимия)
    'strength': {
        'name': 'Сила', 'short': 'STR',
        'color': (240, 120, 90),
        'damage': 0, 'heal': 0,
        'duration': 20,
    },
    'agility': {
        'name': 'Ловкость', 'short': 'AGI',
        'color': (140, 230, 120),
        'damage': 0, 'heal': 0,
        'duration': 20,
    },
    'stone_skin': {
        'name': 'Каменная кожа', 'short': 'STN',
        'color': (170, 170, 160),
        'damage': 0, 'heal': 0,
        'duration': 20,
    },
    'speed': {
        'name': 'Скорость', 'short': 'SPD',
        'color': (150, 220, 250),
        'damage': 0, 'heal': 0,
        'duration': 20,
    },
    'dexterity': {
        'name': 'Проворность', 'short': 'DEX',
        'color': (250, 210, 120),
        'damage': 0, 'heal': 0,
        'duration': 20,
    },
}

# Модификаторы характеристик от статусов (временные баффы).
STATUS_MODS = {
    'strength':   {'atk': 3},
    'agility':    {'dodge': 0.08},
    'stone_skin': {'df': 3},
    'speed':      {'move_speed': 0.25},
    'dexterity':  {'attack_speed': 0.20},
}

# Способность монстра → накладываемый статус (если есть)
ABILITY_TO_STATUS = {
    'poison':  'poison',
    'burning': 'burning',
    'bleeding':'bleeding',
    'slow':    'slow',
}

# ════════════════════ КЛАССЫ ════════════════════
# Дерево классов: базовый класс (1 уровень) → специализация (5 уровень)
# → вершина ветки (15 уровень). «Ассасин» и «Паладин» больше не стартовые
# классы — это вершины ветвей разбойника и воина.
#
#   stats  — абсолютные характеристики (базовые классы и совместимость);
#   bonus  — прибавка к характеристикам при повышении класса;
#   effects — пассивные бонусы класса (складываются с навыками);
#   weapons/armor_types — что класс умеет носить.

WCLASS_NAMES = {
    'sword': 'мечи', 'greatsword': 'двуручники', 'axe': 'топоры',
    'mace': 'булавы', 'hammer': 'молоты', 'shield': 'щиты',
    'spear': 'копья', 'halberd': 'алебарды', 'dagger': 'кинжалы',
    'knife': 'ножи', 'rapier': 'шпаги', 'katana': 'катаны',
    'staff': 'посохи', 'book': 'книги', 'bow': 'луки',
    'crossbow': 'арбалеты', 'fist': 'перчатки',
}
