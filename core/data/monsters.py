"""Данные монстров, боссов и масштабирование статов."""
from __future__ import annotations

from typing import Any

import random

MONSTERS = {
    'rat':      {'ch': 'r', 'name': 'Крыса',  'hp': 3,  'atk': 1, 'df': 0, 'xp': 3,  'color': (170, 150, 130), 'min_depth': 1, 'ability': 'poison', 'gold': False},
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
    # ── Лесные обитатели поверхности ──
    # overworld_only: они не должны появляться в подземелье.
    'deer':     {'ch': 'e', 'name': 'Олень',   'hp': 8,  'atk': 2, 'df': 0, 'xp': 8,  'color': (170, 125, 80),  'min_depth': 1, 'overworld_only': True, 'passive': True, 'flees': True, 'gold': False},
    'wolf':     {'ch': 'w', 'name': 'Волк',    'hp': 14, 'atk': 4, 'df': 1, 'xp': 12, 'color': (130, 135, 145), 'min_depth': 1, 'overworld_only': True, 'ability': 'bleeding', 'gold': False},
    'bear':     {'ch': 'b', 'name': 'Медведь', 'hp': 30, 'atk': 7, 'df': 2, 'xp': 25, 'color': (105, 72, 45),   'min_depth': 1, 'overworld_only': True, 'gold': False},
    'slime':    {'ch': 'j', 'name': 'Слайм',   'hp': 10, 'atk': 2, 'df': 1, 'xp': 6,  'color': (100, 190, 90),  'min_depth': 1, 'overworld_only': True, 'ability': 'poison', 'gold': False},
    'boar':     {'ch': 'p', 'name': 'Кабан',   'hp': 18, 'atk': 5, 'df': 1, 'xp': 15, 'color': (120, 100, 80),  'min_depth': 1, 'overworld_only': True, 'gold': False},
}

# Быстрый доступ к данным моба по символу: ИИ проверяет пассивность,
# не зная ключа вида (у сущности есть только ch).
MONSTERS_BY_CH = {v['ch']: dict(v, key=k) for k, v in MONSTERS.items()}

BOSSES = {
    5:  {'ch': 'G', 'name': 'Король гоблинов',  'hp': 80,  'atk': 9,  'df': 2, 'xp': 150,  'color': (255, 100, 100)},
    10: {'ch': 'L', 'name': 'Лич',              'hp': 150, 'atk': 12, 'df': 4, 'xp': 500,  'color': (200, 100, 255)},
    15: {'ch': 'V', 'name': 'Владыка вампиров', 'hp': 250, 'atk': 18, 'df': 6, 'xp': 700,  'color': (200, 40, 80)},
    20: {'ch': 'A', 'name': 'Древний дракон',   'hp': 400, 'atk': 22, 'df': 8, 'xp': 1200, 'color': (240, 130, 40)},
}

BOSS_DROPS = {
    'G': ['goblin_ear', 'goblin_ear', 'bone'],
    'L': ['bone', 'bone', 'wraith_essence', 'wraith_essence'],
    'V': ['vampire_fang', 'vampire_fang', 'vampire_fang'],
    'A': ['dragon_scale'] * 5,
}


MONSTER_DROPS = {
    'r': [('rat_tail', 0.50), ('mushroom', 0.10), ('hide', 0.30)],
    'g': [('goblin_ear', 0.50), ('wood', 0.15), ('hide', 0.30)],
    'a': [('goblin_ear', 0.45), ('wood', 0.20), ('hide', 0.25)],
    's': [('bone', 0.70), ('stone', 0.15)],
    'S': [('goblin_ear', 0.45), ('mushroom', 0.25)],
    'O': [('orc_tusk', 0.50), ('ore', 0.15)],
    'T': [('ogre_leather', 0.50), ('wood', 0.30), ('stone', 0.20),
          ('hide', 0.30)],
    'D': [('demon_scale', 0.50), ('ore', 0.20)],
    'v': [('vampire_fang', 0.50), ('bone', 0.10)],
    'd': [('dragon_scale', 0.45), ('ore', 0.20)],
    'Y': [('golem_core', 0.35), ('stone', 0.70), ('ore', 0.20)],
    'W': [('wraith_essence', 0.50), ('bone', 0.15)],
    # ── Лесные обитатели ──
    'e': [('venison', 0.70), ('bone', 0.20)],
    'w': [('wolf_pelt', 0.60), ('venison', 0.20), ('bone', 0.15),
          ('hide', 0.25)],
    'b': [('bear_pelt', 0.60), ('bear_claw', 0.50), ('venison', 0.25),
          ('hide', 0.30)],
    'j': [('slime_gel', 0.80), ('mushroom', 0.15)],
    'p': [('boar_tusk', 0.55), ('venison', 0.55), ('bone', 0.10),
          ('hide', 0.20)],
}


def roll_monster_drops(monster_ch: Any) -> Any: 
    return [k for k, c in MONSTER_DROPS.get(monster_ch, []) if random.random() < c]


def scale_stats(hp: Any, atk: Any, df: Any, depth: Any) -> Any: 
    """Обычные мобы: HP растёт заметно, ATK/DEF — умеренно."""
    hp_factor = 1 + (depth - 1) * 0.10
    return (int(hp * hp_factor),
            atk + (depth - 1) // 4,
            df + (depth - 1) // 5)


def scale_boss_stats(hp: Any, atk: Any, df: Any, depth: Any) -> Any: 
    """Боссы масштабируются мягче обычных мобов — иначе финальный
    дракон требует часов гринда."""
    hp_factor = 1 + (depth - 1) * 0.06
    return (int(hp * hp_factor),
            atk + (depth - 1) // 6,
            df + (depth - 1) // 8)


# ════════════════════ ОПЫТ И УРОВНИ ════════════════════
MANA_CRYSTAL_CHANCE = 0.08
MANA_CRYSTAL_BY_SIZE = {
    'small': 'mana_crystal_small',
    'normal': 'mana_crystal',
    'large': 'mana_crystal_large',
}
MANA_POWDER_BY_CRYSTAL = {
    'mana_crystal_small': 1,
    'mana_crystal': 2,
    'mana_crystal_large': 4,
}


def mana_crystal_for(monster: Any) -> Any: 
    """Размер кристалла зависит от силы моба."""
    hp = max(1, getattr(monster, 'max_hp', 10) or 10)
    if getattr(monster, 'boss', False) or hp >= 60:
        return MANA_CRYSTAL_BY_SIZE['large']
    if hp >= 18:
        return MANA_CRYSTAL_BY_SIZE['normal']
    return MANA_CRYSTAL_BY_SIZE['small']


def roll_mana_crystal(monster: Any, chance: Any = MANA_CRYSTAL_CHANCE) -> Any: 
    """Небольшой шанс выпадения кристалла маны с любого моба."""
    if random.random() >= chance:
        return None
    return mana_crystal_for(monster)


def mana_crystal_powder(crystal_key: Any) -> Any: 
    return MANA_POWDER_BY_CRYSTAL.get(crystal_key, 0)

# ════════════════════ СТАТУС-ЭФФЕКТЫ ════════════════════
