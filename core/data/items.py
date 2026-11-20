"""Данные предметов: оружие, броня, зелья, ресурсы."""
from __future__ import annotations

from typing import Any

import random

ITEMS = {
    # Зелья
    'potion_s': {'ch': '!', 'name': 'Малое зелье', 'type': 'heal', 'value': 10,
                 'color': (230, 130, 130), 'tier': 1, 'sell': 2,
                 'category': 'alchemy', 'level_req': 1,
                 'recipe': {'berry': 2, 'mushroom': 1},
                 'skill': 'alchemy', 'skill_req': 1,
                 'desc': 'Восстанавливает 10 HP.'},
    'potion_m': {'ch': '!', 'name': 'Зелье', 'type': 'heal', 'value': 20,
                 'color': (230, 90, 100), 'tier': 2, 'sell': 4,
                 'category': 'alchemy', 'level_req': 3,
                 'recipe': {'mushroom': 2, 'magic_powder': 1},
                 'skill': 'alchemy', 'skill_req': 3,
                 'desc': 'Восстанавливает 20 HP.'},
    'potion_l': {'ch': '!', 'name': 'Большое зелье', 'type': 'heal', 'value': 40,
                 'color': (255, 60, 80), 'tier': 3, 'sell': 6,
                 'category': 'alchemy', 'level_req': 6,
                 'recipe': {'potion_m': 1, 'magic_powder': 2},
                 'skill': 'alchemy', 'skill_req': 6,
                 'desc': 'Восстанавливает 40 HP.'},
    # Алхимия: мана и баффы
    'mana_s': {'ch': '!', 'name': 'Малое зелье маны', 'type': 'mana', 'value': 12,
               'color': (120, 160, 255), 'tier': 1, 'sell': 3,
               'category': 'alchemy',
               'recipe': {'berry': 2, 'mushroom': 1},
               'skill': 'alchemy', 'skill_req': 1, 'desc': 'Восстанавливает 12 маны.'},
    'mana_m': {'ch': '!', 'name': 'Зелье маны', 'type': 'mana', 'value': 24,
               'color': (90, 130, 250), 'tier': 2, 'sell': 5,
               'category': 'alchemy',
               'recipe': {'berry': 3, 'mushroom': 2},
               'skill': 'alchemy', 'skill_req': 4, 'desc': 'Восстанавливает 24 маны.'},
    'potion_strength': {'ch': '!', 'name': 'Зелье силы', 'type': 'buff',
                        'status': 'strength', 'color': (240, 120, 90),
                        'tier': 3, 'sell': 14,
                        'recipe': {'orc_tusk': 1, 'mushroom': 2},
                        'skill': 'alchemy', 'skill_req': 12,
                        'desc': '+3 ATK на 20 ходов.'},
    'potion_agility': {'ch': '!', 'name': 'Зелье ловкости', 'type': 'buff',
                       'status': 'agility', 'color': (140, 230, 120),
                       'tier': 3, 'sell': 14,
                       'recipe': {'goblin_ear': 2, 'berry': 2},
                       'skill': 'alchemy', 'skill_req': 14,
                       'desc': '+8% уклонения на 20 ходов.'},
    'potion_stone_skin': {'ch': '!', 'name': 'Зелье каменной кожи',
                          'type': 'buff', 'status': 'stone_skin',
                          'color': (170, 170, 160), 'tier': 3, 'sell': 16,
                          'recipe': {'stone': 3, 'bone': 1},
                          'skill': 'alchemy', 'skill_req': 16,
                          'desc': '+3 DEF на 20 ходов.'},
    'potion_speed': {'ch': '!', 'name': 'Зелье скорости', 'type': 'buff',
                     'status': 'speed', 'color': (150, 220, 250),
                     'tier': 3, 'sell': 16,
                     'recipe': {'rat_tail': 2, 'berry': 2},
                     'skill': 'alchemy', 'skill_req': 18,
                     'desc': '+25% скорости хода на 20 ходов.'},
    'potion_dexterity': {'ch': '!', 'name': 'Зелье проворности',
                         'type': 'buff', 'status': 'dexterity',
                         'color': (250, 210, 120), 'tier': 3, 'sell': 18,
                         'recipe': {'wraith_essence': 1, 'berry': 3},
                         'skill': 'alchemy', 'skill_req': 20,
                         'desc': '+20% скорости атаки на 20 ходов.'},
    'poison_vial': {'ch': '!', 'name': 'Флакон яда', 'type': 'poison',
                    'value': 8, 'color': (120, 200, 90), 'tier': 2, 'sell': 12,
                    'recipe': {'rat_tail': 2, 'mushroom': 2},
                    'skill': 'alchemy', 'skill_req': 10,
                    'desc': 'Отравляет ближайшего врага (8 урона + яд).'},
    'elixir_life': {'ch': '!', 'name': 'Эликсир жизни', 'type': 'heal',
                    'value': 80, 'color': (255, 80, 120), 'tier': 3, 'sell': 26,
                    'category': 'alchemy',
                    'recipe': {'dragon_scale': 1, 'mushroom': 4, 'berry': 4},
                    'skill': 'alchemy', 'skill_req': 25,
                    'desc': 'Восстанавливает 80 HP.'},
    # Готовая еда трактирщика
    'fried_mushrooms': {'ch': '%', 'name': 'Жареные грибы', 'type': 'food',
                        'heal': 8, 'value': 6, 'sell': 2, 'color': (196, 150, 92),
                        'tier': 1, 'desc': 'Сытно и просто. +8 HP.'},
    'berry_compote': {'ch': '%', 'name': 'Компот из ягод', 'type': 'food',
                      'heal': 6, 'mana': 6, 'value': 8, 'sell': 3,
                      'color': (210, 90, 130), 'tier': 1,
                      'desc': '+6 HP и +6 маны.'},
    'roast_venison': {'ch': '%', 'name': 'Жареная оленина', 'type': 'food',
                      'heal': 14, 'value': 12, 'sell': 5, 'color': (168, 92, 78),
                      'tier': 1, 'desc': 'Нежное мясо. +14 HP.'},
    'roast_boar': {'ch': '%', 'name': 'Жареная кабанина', 'type': 'food',
                   'heal': 18, 'value': 16, 'sell': 7, 'color': (150, 84, 60),
                   'tier': 2, 'desc': 'Плотное мясо. +18 HP.'},
    'roast_bear': {'ch': '%', 'name': 'Жареная медвежатина', 'type': 'food',
                   'heal': 24, 'value': 22, 'sell': 9, 'color': (120, 70, 48),
                   'tier': 2, 'desc': 'Сытно и согревает. +24 HP.'},
    # Оружие
    'dagger':     {'ch': '/', 'name': 'Кинжал', 'type': 'weapon', 'value': 1,
                   'level_req': 1, 'color': (180, 200, 230), 'tier': 1, 'sell': 7,
                   'wtype': 'dagger', 'crit_bonus': 0.02,
                   'desc': '+1 ATK, +2% крит.'},
    'shortsword': {'ch': '/', 'name': 'Короткий меч', 'type': 'weapon', 'value': 1,
                   'level_req': 2, 'color': (190, 210, 240), 'tier': 1, 'sell': 9,
                   'wtype': 'sword', 'desc': '+1 ATK.'},
    'sword':      {'ch': '/', 'name': 'Меч', 'type': 'weapon', 'value': 2,
                   'level_req': 4, 'color': (170, 200, 240), 'tier': 2, 'sell': 14,
                   'wtype': 'sword', 'desc': '+2 ATK.'},
    'axe':        {'ch': '/', 'name': 'Топор', 'type': 'weapon', 'value': 3,
                   'level_req': 5, 'color': (200, 180, 150), 'tier': 2, 'sell': 18,
                   'wtype': 'axe', 'dodge_bonus': -0.02,
                   'desc': '+3 ATK, −2% укл.'},
    'greatsword': {'ch': '/', 'name': 'Двуручник', 'type': 'weapon', 'value': 4,
                   'level_req': 7, 'color': (210, 230, 255), 'tier': 3, 'sell': 21,
                   'wtype': 'greatsword', 'desc': '+4 ATK.'},
    'warhammer':  {'ch': '/', 'name': 'Молот', 'type': 'weapon', 'value': 5,
                   'level_req': 8, 'color': (180, 170, 160), 'tier': 3, 'sell': 26,
                   'wtype': 'hammer', 'dodge_bonus': -0.03,
                   'desc': '+5 ATK, −3% укл.'},
    # Крафт-оружие
    'bone_sword': {'ch': '/', 'name': 'Костяной меч', 'type': 'weapon', 'value': 2,
                   'level_req': 3, 'color': (230, 220, 200), 'tier': 2, 'sell': 12,
                   'wtype': 'sword', 'category': 'blacksmith',
                   'recipe': {'bone': 2, 'wood': 1},
                   'skill': 'smithing', 'skill_req': 3, 'desc': '+2 ATK.'},
    'demon_blade': {'ch': '/', 'name': 'Клинок демона', 'type': 'weapon', 'value': 5,
                    'level_req': 8, 'color': (200, 70, 70), 'tier': 3, 'sell': 35,
                    'wtype': 'sword', 'category': 'blacksmith',
                    'recipe': {'demon_scale': 3, 'orc_tusk': 1, 'sword': 1},
                    'skill': 'smithing', 'skill_req': 18, 'desc': '+5 ATK.'},
    'golem_hammer': {'ch': '/', 'name': 'Молот голема', 'type': 'weapon', 'value': 6,
                     'level_req': 10, 'color': (130, 130, 140), 'tier': 3, 'sell': 40,
                     'wtype': 'hammer', 'category': 'blacksmith',
                     'recipe': {'golem_core': 1, 'stone': 5, 'greatsword': 1},
                     'skill': 'smithing', 'skill_req': 22, 'desc': '+6 ATK.'},
    # ── Новое оружие под классы ──
    'wooden_club': {'ch': '/', 'name': 'Дубина', 'type': 'weapon', 'value': 1,
                    'level_req': 1, 'color': (160, 120, 70), 'tier': 1, 'sell': 3,
                    'wtype': 'mace', 'category': 'blacksmith',
                    'recipe': {'wood': 2},
                    'skill': 'smithing', 'skill_req': 1, 'desc': '+1 ATK.'},
    'stone_hatchet': {'ch': '/', 'name': 'Каменный топор', 'type': 'weapon',
                      'value': 1, 'level_req': 1, 'color': (170, 170, 175),
                      'tier': 1, 'sell': 4, 'wtype': 'axe',
                      'category': 'blacksmith',
                      'recipe': {'wood': 1, 'stone': 2},
                      'skill': 'smithing', 'skill_req': 1, 'desc': '+1 ATK.'},
    'wooden_shield': {'ch': ']', 'name': 'Деревянный щит', 'type': 'weapon',
                      'value': 0, 'level_req': 2, 'color': (150, 110, 60),
                      'tier': 1, 'sell': 5, 'wtype': 'shield', 'df_bonus': 1,
                      'category': 'blacksmith',
                      'recipe': {'wood': 3},
                      'skill': 'smithing', 'skill_req': 3,
                      'desc': '+1 DEF, +1 ATK.'},
    'battle_hammer': {'ch': '/', 'name': 'Боевой молот', 'type': 'weapon',
                      'value': 5, 'level_req': 9, 'color': (190, 180, 170),
                      'tier': 3, 'sell': 32, 'wtype': 'hammer',
                      'category': 'blacksmith',
                      'recipe': {'ore': 4, 'wood': 2},
                      'skill': 'smithing', 'skill_req': 12, 'desc': '+5 ATK.'},
    'hunting_bow': {'ch': ')', 'name': 'Охотничий лук', 'type': 'weapon',
                    'value': 2, 'level_req': 4, 'color': (180, 140, 90),
                    'tier': 2, 'sell': 16, 'wtype': 'bow',
                    'category': 'blacksmith',
                    'recipe': {'wood': 3, 'rat_tail': 1},
                    'skill': 'smithing', 'skill_req': 6, 'desc': '+2 ATK.'},
    'long_staff': {'ch': '|', 'name': 'Длинный посох', 'type': 'weapon',
                   'value': 2, 'level_req': 3, 'color': (140, 180, 210),
                   'tier': 2, 'sell': 15, 'wtype': 'staff', 'crit_bonus': 0.02,
                   'category': 'blacksmith',
                   'recipe': {'wood': 3, 'stone': 1},
                   'skill': 'smithing', 'skill_req': 4,
                   'desc': '+2 ATK, +2% крит.'},
    'runed_blade': {'ch': '/', 'name': 'Рунный клинок', 'type': 'weapon',
                    'value': 6, 'level_req': 12, 'color': (150, 200, 240),
                    'tier': 3, 'sell': 46, 'wtype': 'sword', 'crit_bonus': 0.04,
                    'category': 'blacksmith',
                    'recipe': {'ore': 3, 'golem_core': 1, 'bone': 2},
                    'skill': 'smithing', 'skill_req': 20,
                    'desc': '+6 ATK, +4% крит.'},
    'dragon_blade': {'ch': '/', 'name': 'Клинок дракона', 'type': 'weapon',
                     'value': 8, 'level_req': 16, 'color': (245, 150, 70),
                     'tier': 3, 'sell': 60, 'wtype': 'greatsword',
                     'category': 'blacksmith',
                     'recipe': {'dragon_scale': 3, 'ore': 4, 'demon_scale': 2},
                     'skill': 'smithing', 'skill_req': 25, 'desc': '+8 ATK.'},
    # Оружие из новых лесных материалов
    'iron_sword': {'ch': '/', 'name': 'Железный меч', 'type': 'weapon', 'value': 3,
                   'level_req': 3, 'color': (185, 195, 210), 'tier': 2, 'sell': 16,
                   'wtype': 'sword', 'category': 'blacksmith',
                   'recipe': {'iron_ingot': 2, 'wood': 1},
                   'skill': 'smithing', 'skill_req': 6, 'desc': '+3 ATK.'},
    'steel_sword': {'ch': '/', 'name': 'Стальной меч', 'type': 'weapon', 'value': 4,
                    'level_req': 5, 'color': (210, 220, 240), 'tier': 3, 'sell': 24,
                    'wtype': 'sword', 'category': 'blacksmith',
                    'recipe': {'steel_ingot': 2, 'leather': 1},
                    'skill': 'smithing', 'skill_req': 12, 'desc': '+4 ATK.'},
    'tusk_axe': {'ch': '/', 'name': 'Клыкастый топор', 'type': 'weapon', 'value': 4,
                 'level_req': 4, 'color': (210, 190, 160), 'tier': 2, 'sell': 20,
                 'wtype': 'axe', 'category': 'blacksmith', 'dodge_bonus': -0.02,
                 'recipe': {'boar_tusk': 2, 'iron_ingot': 1, 'wood': 1},
                 'skill': 'smithing', 'skill_req': 4,
                 'desc': '+4 ATK, −2% укл.'},
    # Броня
    'padded':     {'ch': '[', 'name': 'Стёганая куртка', 'type': 'armor', 'value': 1,
                   'level_req': 1, 'color': (200, 180, 140), 'tier': 1, 'sell': 7,
                   'armor_type': 'light', 'desc': '+1 DEF.'},
    'leather':    {'ch': '[', 'name': 'Кожаная броня', 'type': 'armor', 'value': 1,
                   'level_req': 1, 'color': (200, 170, 100), 'tier': 1, 'sell': 6,
                   'armor_type': 'light', 'desc': '+1 DEF.'},
    'chain':      {'ch': '[', 'name': 'Кольчуга', 'type': 'armor', 'value': 2,
                   'level_req': 4, 'color': (170, 170, 180), 'tier': 2, 'sell': 12,
                   'armor_type': 'medium', 'desc': '+2 DEF.'},
    'scale':      {'ch': '[', 'name': 'Чешуйчатая', 'type': 'armor', 'value': 2,
                   'level_req': 4, 'color': (160, 150, 130), 'tier': 2, 'sell': 14,
                   'armor_type': 'medium', 'hp_bonus': 3, 'desc': '+2 DEF, +3 HP.'},
    'plate':      {'ch': '[', 'name': 'Латы', 'type': 'armor', 'value': 3,
                   'level_req': 7, 'color': (220, 220, 240), 'tier': 3, 'sell': 18,
                   'armor_type': 'heavy', 'desc': '+3 DEF.'},
    'mithril':    {'ch': '[', 'name': 'Мифрил', 'type': 'armor', 'value': 3,
                   'level_req': 7, 'color': (180, 210, 230), 'tier': 3, 'sell': 28,
                   'armor_type': 'medium', 'dodge_bonus': 0.05,
                   'desc': '+3 DEF, +5% укл.'},
    # Крафт-броня
    'leather_armor': {'ch': '[', 'name': 'Броня из огра', 'type': 'armor', 'value': 3,
                      'level_req': 5, 'color': (150, 110, 60), 'tier': 2, 'sell': 22,
                      'armor_type': 'light', 'category': 'blacksmith',
                      'recipe': {'ogre_leather': 3, 'goblin_ear': 1},
                      'skill': 'smithing', 'skill_req': 8,
                      'desc': '+3 DEF.'},
    'dragon_armor': {'ch': '[', 'name': 'Броня дракона', 'type': 'armor', 'value': 4,
                     'level_req': 10, 'color': (240, 130, 40), 'tier': 3, 'sell': 45,
                     'armor_type': 'heavy', 'category': 'blacksmith',
                     'recipe': {'dragon_scale': 5, 'plate': 1},
                     'skill': 'smithing', 'skill_req': 20,
                     'desc': '+4 DEF.'},
    'lich_robe': {'ch': '[', 'name': 'Мантия лича', 'type': 'armor', 'value': 4,
                  'level_req': 9, 'color': (200, 100, 255), 'tier': 3, 'sell': 40,
                  'armor_type': 'robe', 'category': 'blacksmith',
                  'recipe': {'bone': 3, 'wraith_essence': 2, 'plate': 1},
                  'skill': 'smithing', 'skill_req': 22,
                  'desc': '+4 DEF.'},
    'mage_robe': {'ch': '[', 'name': 'Мантия мага', 'type': 'armor', 'value': 1,
                  'level_req': 1, 'color': (130, 150, 220), 'tier': 1, 'sell': 8,
                  'armor_type': 'robe', 'category': 'blacksmith',
                  'recipe': {'hide': 2, 'wood': 1},
                  'skill': 'smithing', 'skill_req': 1,
                  'desc': '+1 DEF.'},
    'arcane_robe': {'ch': '[', 'name': 'Магическая мантия', 'type': 'armor',
                    'value': 3, 'level_req': 9, 'color': (160, 180, 240),
                    'tier': 3, 'sell': 30, 'armor_type': 'robe',
                    'category': 'blacksmith',
                    'recipe': {'hide': 4, 'golem_core': 1, 'ore': 2},
                    'skill': 'smithing', 'skill_req': 16,
                    'desc': '+3 DEF, +5 маны.', 'mana_bonus': 5},
    # ── Шлемы ──
    'leather_cap': {'ch': '^', 'name': 'Кожаный шлем', 'type': 'head',
                    'value': 1, 'level_req': 1, 'color': (200, 170, 110),
                    'tier': 1, 'sell': 5, 'armor_type': 'light',
                    'category': 'blacksmith',
                    'recipe': {'hide': 2}, 'skill': 'smithing', 'skill_req': 1,
                    'desc': '+1 DEF.'},
    'iron_helm': {'ch': '^', 'name': 'Железный шлем', 'type': 'head',
                  'value': 2, 'level_req': 4, 'color': (190, 195, 205),
                  'tier': 2, 'sell': 12, 'armor_type': 'medium',
                  'hp_bonus': 3, 'category': 'blacksmith',
                  'recipe': {'ore': 2, 'hide': 1}, 'skill': 'smithing',
                  'skill_req': 8, 'desc': '+2 DEF, +3 HP.'},
    'steel_helm': {'ch': '^', 'name': 'Стальной шлем', 'type': 'head',
                   'value': 3, 'level_req': 8, 'color': (215, 225, 245),
                   'tier': 3, 'sell': 22, 'armor_type': 'heavy',
                   'category': 'blacksmith',
                   'recipe': {'ore': 3, 'bone': 1}, 'skill': 'smithing',
                   'skill_req': 14, 'desc': '+3 DEF, +2 HP.', 'hp_bonus': 2},
    'crown': {'ch': '^', 'name': 'Корона', 'type': 'head', 'value': 3,
              'level_req': 7, 'color': (240, 210, 100), 'tier': 3, 'sell': 30,
              'hp_bonus': 5, 'desc': '+3 DEF, +5 HP.'},
    'mage_hood': {'ch': '^', 'name': 'Капюшон мага', 'type': 'head',
                  'value': 1, 'level_req': 2, 'color': (140, 160, 230),
                  'tier': 2, 'sell': 10, 'armor_type': 'robe',
                  'category': 'blacksmith',
                  'recipe': {'hide': 3, 'mushroom': 1}, 'skill': 'smithing',
                  'skill_req': 6, 'desc': '+1 DEF, +3 маны.', 'mana_bonus': 3},
    'dragon_helm': {'ch': '^', 'name': 'Шлем дракона', 'type': 'head',
                    'value': 4, 'level_req': 14, 'color': (245, 150, 70),
                    'tier': 3, 'sell': 34, 'armor_type': 'heavy',
                    'category': 'blacksmith',
                    'recipe': {'dragon_scale': 2, 'ore': 3}, 'skill': 'smithing',
                    'skill_req': 20, 'desc': '+4 DEF, +4 HP.', 'hp_bonus': 4},
    'wolf_hood': {'ch': '^', 'name': 'Волчий капюшон', 'type': 'head', 'value': 1,
                  'level_req': 3, 'color': (135, 140, 150), 'tier': 2, 'sell': 14,
                  'dodge_bonus': 0.03, 'category': 'blacksmith',
                  'recipe': {'wolf_pelt': 2, 'leather_cap': 1},
                  'skill': 'smithing', 'skill_req': 6,
                  'desc': '+1 DEF, +3% укл.'},
    # ── Поножи ──
    'cloth_pants':   {'ch': '|', 'name': 'Штаны', 'type': 'legs', 'value': 1,
                      'level_req': 1, 'color': (150, 130, 100), 'tier': 1, 'sell': 4,
                      'desc': '+1 DEF.'},
    'leather_boots': {'ch': '|', 'name': 'Кожаные сапоги', 'type': 'boots', 'value': 1,
                      'level_req': 1, 'color': (160, 120, 80), 'tier': 1, 'sell': 6,
                      'dodge_bonus': 0.03, 'armor_type': 'light',
                      'category': 'blacksmith',
                      'recipe': {'hide': 2, 'wood': 1}, 'skill': 'smithing',
                      'skill_req': 1, 'desc': '+1 DEF, +3% укл.'},
    'chain_boots': {'ch': '|', 'name': 'Кольчужные сапоги', 'type': 'boots',
                    'value': 2, 'level_req': 5, 'color': (170, 170, 180),
                    'tier': 2, 'sell': 14, 'dodge_bonus': 0.02,
                    'armor_type': 'medium', 'category': 'blacksmith',
                    'recipe': {'ore': 2, 'hide': 1}, 'skill': 'smithing',
                    'skill_req': 10, 'desc': '+2 DEF, +2% укл.'},
    'plate_boots': {'ch': '|', 'name': 'Латные сапоги', 'type': 'boots',
                    'value': 3, 'level_req': 9, 'color': (215, 220, 240),
                    'tier': 3, 'sell': 24, 'armor_type': 'heavy',
                    'category': 'blacksmith',
                    'recipe': {'ore': 3, 'bone': 1}, 'skill': 'smithing',
                    'skill_req': 16, 'desc': '+3 DEF.'},
    'plate_greaves': {'ch': '|', 'name': 'Латные поножи', 'type': 'legs', 'value': 2,
                      'level_req': 6, 'color': (220, 220, 240), 'tier': 2, 'sell': 15,
                      'desc': '+2 DEF.'},
    'leather_legs': {'ch': 'n', 'name': 'Кожаные поножи', 'type': 'legs',
                     'value': 1, 'level_req': 1, 'color': (190, 160, 100),
                     'tier': 1, 'sell': 4, 'armor_type': 'light',
                     'category': 'blacksmith',
                     'recipe': {'hide': 3, 'wood': 1}, 'skill': 'smithing',
                     'skill_req': 1, 'desc': '+1 DEF.'},
    'chain_legs': {'ch': 'n', 'name': 'Кольчужные поножи', 'type': 'legs',
                   'value': 2, 'level_req': 5, 'color': (170, 170, 180),
                   'tier': 2, 'sell': 14, 'armor_type': 'medium',
                   'category': 'blacksmith',
                   'recipe': {'ore': 2, 'hide': 2}, 'skill': 'smithing',
                   'skill_req': 10, 'desc': '+2 DEF.'},
    'plate_legs': {'ch': 'n', 'name': 'Латные поножи', 'type': 'legs',
                   'value': 3, 'level_req': 9, 'color': (215, 220, 240),
                   'tier': 3, 'sell': 24, 'armor_type': 'heavy',
                   'category': 'blacksmith',
                   'recipe': {'ore': 4, 'bone': 1}, 'skill': 'smithing',
                   'skill_req': 16, 'desc': '+3 DEF, +2 HP.', 'hp_bonus': 2},
    'cloth_legs': {'ch': 'n', 'name': 'Тканевые поножи', 'type': 'legs',
                   'value': 1, 'level_req': 2, 'color': (140, 160, 220),
                   'tier': 2, 'sell': 9, 'armor_type': 'robe',
                   'category': 'blacksmith',
                   'recipe': {'hide': 3, 'berry': 2}, 'skill': 'smithing',
                   'skill_req': 6, 'desc': '+1 DEF, +2 маны.', 'mana_bonus': 2},
    # Броня из шкур новых лесных зверей
    'wolf_armor': {'ch': '[', 'name': 'Волчья броня', 'type': 'armor', 'value': 2,
                   'level_req': 3, 'color': (135, 140, 150), 'tier': 2, 'sell': 18,
                   'hp_bonus': 2, 'category': 'blacksmith',
                   'recipe': {'wolf_pelt': 3, 'padded': 1},
                   'skill': 'smithing', 'skill_req': 4,
                   'desc': '+2 DEF, +2 HP.'},
    'bear_cloak': {'ch': '[', 'name': 'Медвежья шкура', 'type': 'armor', 'value': 3,
                   'level_req': 5, 'color': (110, 78, 50), 'tier': 2, 'sell': 26,
                   'hp_bonus': 4, 'category': 'blacksmith',
                   'recipe': {'bear_pelt': 3, 'leather_armor': 1},
                   'skill': 'smithing', 'skill_req': 8,
                   'desc': '+3 DEF, +4 HP.'},
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
    'book_int': {'ch': '?', 'name': 'Книга интеллекта', 'type': 'book',
                 'stat': 'int', 'value': 5, 'color': (180, 150, 255),
                 'tier': 3, 'sell': 30, 'shop_price': 80,
                 'desc': '+5 INT навсегда: мана и урон магией.'},
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
    # Квестовый ключ: падает с босса (100%), отпирает дверь в казну.
    'boss_key': {'ch': '¶', 'name': 'Ключ босса', 'type': 'key',
                 'value': 0, 'sell': 0, 'color': (255, 215, 100), 'tier': 3,
                 'desc': 'Отпирает запертую дверь в сокровищнице подземелья.'},
    'wood':     {'ch': '=', 'name': 'Дерево', 'type': 'resource',
                 'value': 0, 'sell': 2, 'color': (150, 100, 60), 'tier': 1,
                 'desc': 'Материал для крафта.'},
    'stone':    {'ch': '*', 'name': 'Камень', 'type': 'resource',
                 'value': 0, 'sell': 1, 'color': (150, 150, 160), 'tier': 1,
                 'desc': 'Материал для крафта.'},
    'ore':      {'ch': '#', 'name': 'Руда', 'type': 'resource',
                 'value': 0, 'sell': 5, 'color': (200, 170, 80), 'tier': 2,
                 'desc': 'Ценный материал.'},
    'hide':     {'ch': '~', 'name': 'Шкура', 'type': 'resource',
                 'value': 0, 'sell': 2, 'color': (180, 140, 95), 'tier': 1,
                 'desc': 'Материал для кожи и мантий.'},
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
    # ── Материалы новых лесных мобов ──
    'venison':       {'ch': '~', 'name': 'Оленина',        'type': 'material', 'sell': 2,  'color': (170, 90, 80),   'tier': 1, 'desc': 'Свежее мясо оленя.'},
    'wolf_pelt':     {'ch': '~', 'name': 'Волчья шкура',   'type': 'material', 'sell': 4,  'color': (130, 135, 145), 'tier': 1, 'desc': 'Прочная серая шкура.'},
    'bear_pelt':     {'ch': '~', 'name': 'Медвежья шкура', 'type': 'material', 'sell': 7,  'color': (105, 72, 45),   'tier': 2, 'desc': 'Толстая бурая шкура.'},
    'bear_claw':     {'ch': 'v', 'name': 'Медвежий коготь','type': 'material', 'sell': 5,  'color': (220, 210, 190), 'tier': 2, 'desc': 'Острый коготь.'},
    'slime_gel':     {'ch': 'o', 'name': 'Слизь',          'type': 'material', 'sell': 2,  'color': (100, 190, 90),  'tier': 1, 'desc': 'Желеобразная субстанция.'},
    'boar_tusk':     {'ch': 'v', 'name': 'Кабаний клык',   'type': 'material', 'sell': 3,  'color': (235, 230, 205), 'tier': 1, 'desc': 'Крепкий клык кабана.'},
    # ── Магические кристаллы и порошок ──
    'mana_crystal_small': {'ch': 'o', 'name': 'Малый кристалл маны', 'type': 'material',
                           'sell': 6, 'color': (120, 170, 255), 'tier': 1,
                           'desc': 'Крупица чистой маны.'},
    'mana_crystal':       {'ch': 'o', 'name': 'Кристалл маны', 'type': 'material',
                           'sell': 12, 'color': (145, 120, 255), 'tier': 2,
                           'desc': 'Сгусток магической энергии.'},
    'mana_crystal_large': {'ch': 'o', 'name': 'Большой кристалл маны', 'type': 'material',
                           'sell': 24, 'color': (205, 150, 255), 'tier': 3,
                           'desc': 'Мощный магический кристалл.'},
    'magic_powder': {'ch': '*', 'name': 'Магический порошок', 'type': 'material',
                     'sell': 10, 'color': (190, 150, 255), 'tier': 2,
                     'category': 'alchemy', 'level_req': 1,
                     'recipe': {'mana_crystal_small': 1},
                     'recipes': [
                         {'materials': {'mana_crystal_small': 1}, 'count': 1},
                         {'materials': {'mana_crystal': 1}, 'count': 2},
                         {'materials': {'mana_crystal_large': 1}, 'count': 4},
                     ],
                     'desc': 'Основа алхимии и магической инженерии.'},
    # ── Слитки ──
    'iron_ingot':   {'ch': '=', 'name': 'Железный слиток', 'type': 'material',
                     'sell': 6, 'color': (175, 180, 190), 'tier': 1,
                     'category': 'blacksmith', 'level_req': 1,
                     'recipe': {'ore': 2, 'wood': 1},
                     'desc': 'Заготовка для оружия и брони.'},
    'steel_ingot':  {'ch': '=', 'name': 'Стальной слиток', 'type': 'material',
                     'sell': 11, 'color': (205, 215, 230), 'tier': 2,
                     'category': 'blacksmith', 'level_req': 2,
                     'recipe': {'iron_ingot': 2, 'stone': 1},
                     'desc': 'Крепкая сталь для снаряжения.'},
    'mithril_ingot': {'ch': '=', 'name': 'Мифриловый слиток', 'type': 'material',
                      'sell': 20, 'color': (180, 210, 230), 'tier': 3,
                      'category': 'blacksmith', 'level_req': 5,
                      'recipe': {'ore': 3, 'stone': 2},
                      'desc': 'Лёгкий и прочный металл.'},
    # ── Особые (ремесленные) крафты ──
    # Открываются только чтением чертежа и только классам ветки
    # «Ремесленник» (artisan и его эволюции).
    'artisan_blade': {'ch': '/', 'name': 'Клинок ремесленника',
                      'type': 'weapon', 'wtype': 'sword', 'value': 9,
                      'level_req': 8, 'color': (210, 230, 255), 'tier': 3,
                      'sell': 90, 'category': 'blacksmith',
                      'recipe': {'mithril_ingot': 2, 'dragon_scale': 1,
                                 'magic_powder': 2},
                      'skill': 'blacksmith', 'skill_req': 18,
                      'special': True, 'craft_classes': ('artisan',),
                      'blueprint_required': 'artisan_blade',
                      'desc': 'Шедевр кузнеца: +9 ATK, +2 DEF.',
                      'df_bonus': 2},
    'philosophers_stone': {'ch': 'o', 'name': 'Философский камень',
                           'type': 'material', 'sell': 150,
                           'color': (255, 200, 120), 'tier': 3,
                           'category': 'alchemy',
                           'recipe': {'magic_powder': 3,
                                      'mana_crystal_large': 1,
                                      'iron_ingot': 1},
                           'skill': 'alchemy', 'skill_req': 20,
                           'special': True, 'craft_classes': ('artisan',),
                           'blueprint_required': 'philosophers_stone',
                           'desc': 'Венец алхимии. Очень дорого продаётся.'},
    'grand_elixir': {'ch': '!', 'name': 'Великий эликсир', 'type': 'heal',
                     'value': 140, 'color': (255, 140, 200), 'tier': 3,
                     'sell': 70, 'shop_price': 220,
                     'category': 'alchemy',
                     'recipe': {'magic_powder': 4, 'mana_crystal': 2,
                                'berry': 3},
                     'skill': 'alchemy', 'skill_req': 22,
                     'special': True, 'craft_classes': ('artisan',),
                     'blueprint_required': 'grand_elixir',
                     'desc': 'Восстанавливает 140 HP.'},
    # ── Чертежи особых крафтов ──
    'blueprint_artisan_blade': {'ch': '?', 'name': 'Чертёж: клинок ремесленника',
                                'type': 'blueprint', 'teaches': 'artisan_blade',
                                'sell': 40, 'color': (230, 210, 160),
                                'tier': 3, 'shop_price': 180,
                                'desc': 'Ремесленнику: открывает крафт клинка.'},
    'blueprint_philosophers_stone': {'ch': '?', 'name': 'Чертёж: философский камень',
                                     'type': 'blueprint',
                                     'teaches': 'philosophers_stone',
                                     'sell': 40, 'color': (240, 220, 150),
                                     'tier': 3, 'shop_price': 200,
                                     'desc': 'Ремесленнику: открывает философский камень.'},
    'blueprint_grand_elixir': {'ch': '?', 'name': 'Чертёж: великий эликсир',
                               'type': 'blueprint', 'teaches': 'grand_elixir',
                               'sell': 40, 'color': (240, 180, 210),
                               'tier': 3, 'shop_price': 200,
                               'desc': 'Ремесленнику: открывает великий эликсир.'},
    # ── Свитки и руны ──
    'scroll_heal': {'ch': '&', 'name': 'Свиток лечения', 'type': 'scroll',
                    'effect': 'heal', 'value': 30, 'color': (120, 240, 120),
                    'tier': 2, 'sell': 12, 'shop_price': 60, 'mana_cost': 5,
                    'category': 'magic_engineering', 'level_req': 1,
                    'recipe': {'magic_powder': 1, 'berry': 2},
                    'skill': 'magic_engineering', 'skill_req': 1,
                    'desc': 'Восстанавливает 30 HP. Мана: 5.'},
    'scroll_fireball': {'ch': '&', 'name': 'Свиток огня', 'type': 'scroll',
                        'effect': 'fireball', 'value': 20, 'color': (255, 120, 50),
                        'tier': 2, 'sell': 15, 'shop_price': 80, 'mana_cost': 7,
                        'category': 'magic_engineering', 'level_req': 3,
                        'recipe': {'magic_powder': 2, 'ore': 1},
                        'skill': 'magic_engineering', 'skill_req': 3,
                        'desc': 'Огонь 20 урона по области 3×3. Мана: 7.'},
    'scroll_freeze': {'ch': '&', 'name': 'Свиток заморозки', 'type': 'scroll',
                      'effect': 'freeze', 'value': 3, 'color': (120, 180, 255),
                      'tier': 3, 'sell': 18, 'shop_price': 100, 'mana_cost': 8,
                      'category': 'magic_engineering', 'level_req': 5,
                      'recipe': {'mana_crystal_small': 1, 'magic_powder': 2},
                      'skill': 'magic_engineering', 'skill_req': 5,
                      'desc': 'Замедляет врагов в радиусе 5 на 3 хода. Мана: 8.'},
    'rune_fire': {'ch': '¤', 'name': 'Руна огня', 'type': 'rune',
                  'effect': 'fireball', 'value': 30, 'color': (255, 130, 60),
                  'tier': 3, 'sell': 26, 'shop_price': 120, 'mana_cost': 5,
                  'category': 'magic_engineering', 'level_req': 4,
                  'recipe': {'magic_powder': 3, 'mana_crystal_small': 1},
                  'skill': 'magic_engineering', 'skill_req': 4,
                  'desc': 'Высекает огонь: 30 урона по области. Мана: 5.'},
    'rune_frost': {'ch': '¤', 'name': 'Руна льда', 'type': 'rune',
                   'effect': 'freeze', 'value': 4, 'color': (130, 190, 255),
                   'tier': 3, 'sell': 26, 'shop_price': 120, 'mana_cost': 6,
                   'category': 'magic_engineering', 'level_req': 4,
                   'recipe': {'magic_powder': 2, 'mana_crystal': 1},
                   'skill': 'magic_engineering', 'skill_req': 4,
                   'desc': 'Замедляет врагов в радиусе 5 на 4 хода. Мана: 6.'},
    'rune_heal': {'ch': '¤', 'name': 'Руна лечения', 'type': 'rune',
                  'effect': 'heal', 'value': 45, 'color': (130, 245, 150),
                  'tier': 3, 'sell': 24, 'shop_price': 110, 'mana_cost': 10,
                  'category': 'magic_engineering', 'level_req': 3,
                  'recipe': {'magic_powder': 2, 'mushroom': 2},
                  'skill': 'magic_engineering', 'skill_req': 3,
                  'desc': 'Восстанавливает 45 HP. Мана: 10.'},
    # ── Дополнительные руны и свитки ──
    'rune_flame': {'ch': '&', 'name': 'Руна пламени', 'type': 'scroll',
                   'effect': 'fireball', 'value': 14, 'color': (255, 160, 80),
                   'tier': 1, 'sell': 8, 'mana_cost': 5,
                   'category': 'magic_engineering', 'level_req': 2,
                   'recipe': {'stone': 1, 'berry': 1},
                   'skill': 'magic_engineering', 'skill_req': 2,
                   'desc': 'Огонь 14 урона по области 3×3. Мана: 5.'},
    'rune_ward': {'ch': '&', 'name': 'Руна защиты', 'type': 'scroll',
                  'effect': 'ward', 'value': 3, 'color': (200, 200, 160),
                  'tier': 2, 'sell': 14, 'mana_cost': 6,
                  'category': 'magic_engineering', 'level_req': 5,
                  'recipe': {'stone': 2, 'bone': 1},
                  'skill': 'magic_engineering', 'skill_req': 10,
                  'desc': 'Каменная кожа на 20 ходов. Мана: 6.'},
    'scroll_lightning': {'ch': '&', 'name': 'Свиток молнии', 'type': 'scroll',
                         'effect': 'lightning', 'value': 30, 'color': (240, 230, 110),
                         'tier': 3, 'sell': 22, 'mana_cost': 9,
                         'category': 'magic_engineering', 'level_req': 8,
                         'recipe': {'ore': 3, 'golem_core': 1},
                         'skill': 'magic_engineering', 'skill_req': 14,
                         'desc': '30 урона и замедление. Мана: 9.'},
    'scroll_inferno': {'ch': '&', 'name': 'Свиток инферно', 'type': 'scroll',
                       'effect': 'fireball', 'value': 45, 'color': (255, 90, 40),
                       'tier': 3, 'sell': 40, 'mana_cost': 12,
                       'category': 'magic_engineering', 'level_req': 12,
                       'recipe': {'demon_scale': 2, 'ore': 3, 'stone': 2},
                       'skill': 'magic_engineering', 'skill_req': 20,
                       'desc': 'Огонь 45 урона по области 3×3. Мана: 12.'},
    'rune_guardian': {'ch': '&', 'name': 'Руна стража', 'type': 'scroll',
                      'effect': 'heal', 'value': 60, 'color': (150, 240, 180),
                      'tier': 3, 'sell': 34, 'mana_cost': 10,
                      'category': 'magic_engineering', 'level_req': 12,
                      'recipe': {'golem_core': 1, 'wraith_essence': 2},
                      'skill': 'magic_engineering', 'skill_req': 22,
                      'desc': 'Восстанавливает 60 HP. Мана: 10.'},
    'orb_arcane': {'ch': 'o', 'name': 'Тайная сфера', 'type': 'weapon',
                   'value': 4, 'level_req': 12, 'color': (170, 140, 240),
                   'tier': 3, 'sell': 50, 'wtype': 'book', 'crit_bonus': 0.05,
                   'category': 'magic_engineering',
                   'recipe': {'golem_core': 2, 'wraith_essence': 2},
                   'skill': 'magic_engineering', 'skill_req': 25,
                   'desc': '+4 ATK, +5% крит.'},
}
MATERIAL_KEYS = ('bone', 'ogre_leather', 'goblin_ear', 'rat_tail', 'orc_tusk',
                 'demon_scale', 'vampire_fang', 'dragon_scale',
                 'golem_core', 'wraith_essence',
                 # материалы новых лесных мобов
                 'venison', 'wolf_pelt', 'bear_pelt', 'bear_claw',
                 'slime_gel', 'boar_tusk',
                 # кристаллы, порошок и слитки
                 'mana_crystal_small', 'mana_crystal', 'mana_crystal_large',
                 'magic_powder', 'iron_ingot', 'steel_ingot', 'mithril_ingot')

# ════════════════════ НАВЫКИ КРАФТА ════════════════════
def item_category(item_key: Any, default: Any = 'blacksmith') -> Any: 
    """Категория предмета — она же вкладка крафта и навык за крафт."""
    from .recipes import SKILLS  # lazy: avoid circular import
    item = item_key if isinstance(item_key, dict) else ITEMS.get(item_key, {})
    cat = item.get('category')
    if cat in SKILLS:
        return cat
    kind = item.get('type')
    if kind in ('weapon', 'armor', 'head', 'legs', 'boots', 'material'):
        return 'blacksmith'
    if kind in ('heal', 'mana', 'poison', 'buff'):
        return 'alchemy'
    if kind in ('scroll', 'rune', 'blueprint'):
        return 'magic_engineering'
    skill = item.get('skill')
    if skill in SKILLS:
        return skill
    return default


def item_recipes(item_key: Any) -> Any: 
    """[(материалы, сколько_получится)] для предмета.

    Поддерживает как старый ``recipe``, так и новый ``recipes`` —
    несколько вариантов одного и того же предмета (например, порошок
    из кристаллов разного размера).
    """
    item = item_key if isinstance(item_key, dict) else ITEMS.get(item_key, {})
    variants = item.get('recipes')
    out = []
    if variants:
        for variant in variants:
            mats = variant.get('materials') or variant.get('recipe') or {}
            if mats:
                out.append((dict(mats), max(1, int(variant.get('count', 1)))))
    if not out and item.get('recipe'):
        out.append((dict(item['recipe']), 1))
    return out


def select_recipe(item_key: Any, inventory: Any, recipe_index: Any = None) -> Any: 
    """(материалы, сколько_получится, индекс_варианта).

    Без явного индекса выбирается первый доступный вариант, иначе —
    первый вариант (чтобы окно крафта показывало хоть что-то).
    """
    variants = item_recipes(item_key)
    if not variants:
        return None, 0, -1
    if recipe_index is not None and 0 <= recipe_index < len(variants):
        mats, count = variants[recipe_index]
        return mats, count, recipe_index
    for i, (mats, count) in enumerate(variants):
        if all(inventory.get(mat, 0) >= cnt for mat, cnt in mats.items()):
            return mats, count, i
    mats, count = variants[0]
    return mats, count, 0


RARITY_CHANCES = [(0, 0.85), (1, 0.10), (2, 0.04), (3, 0.01)]
RARITY_COLORS = {0: None, 1: (170, 220, 170), 2: (140, 190, 255), 3: (255, 180, 80)}


def roll_rarity() -> Any: 
    r = random.random()
    c = 0.0
    for level, chance in RARITY_CHANCES:
        c += chance
        if r < c:
            return level
    return 0


def make_loot_key(base_key: Any, rarity: Any) -> Any: 
    return base_key if rarity <= 0 else f"{base_key}+{rarity}"


def parse_item_key(full_key: Any) -> Any: 
    """'sword+2', 'ore#2' или ItemInstance('sword', 2) → (base, rarity).

    Суффикс ``#N`` — это номер виртуального стака (см. ``rules.add_item``),
    а не часть имени предмета. Его нужно отбрасывать до разбора редкости,
    иначе в UI появлялись «Ore#2» и ломались иконки/цены.
    """
    if not full_key:
        return None, 0
    # Lazy import to avoid circular dependency (ItemInstance lives in quests.py)
    from .quests import ItemInstance
    if isinstance(full_key, ItemInstance):
        return full_key.base, full_key.rarity
    if not isinstance(full_key, str):
        return full_key, 0
    if '#' in full_key:
        full_key = full_key.split('#', 1)[0]
    if '+' in full_key:
        base, lvl = full_key.rsplit('+', 1)
        try:
            return base, int(lvl)
        except ValueError:
            return full_key, 0
    return full_key, 0


def get_item_data(full_key: Any) -> Any: 
    base, _ = parse_item_key(full_key)
    return ITEMS.get(base, {})


def item_display_name(full_key: Any) -> Any: 
    base, lvl = parse_item_key(full_key)
    name = ITEMS.get(base, {}).get('name', base or '?')
    return f"{name} +{lvl}" if lvl > 0 else name


def item_display_color(full_key: Any) -> Any: 
    base, lvl = parse_item_key(full_key)
    if lvl in RARITY_COLORS and RARITY_COLORS[lvl] is not None:
        return RARITY_COLORS[lvl]
    return ITEMS.get(base, {}).get('color', (200, 200, 200))


def item_sell_price(full_key: Any) -> Any: 
    base, lvl = parse_item_key(full_key)
    return ITEMS.get(base, {}).get('sell', 0) + lvl * 10


# ════════════════════ МАГАЗИН ════════════════════

# ── Перки (special abilities from books) ──
PERK_INFO = {
    'double_strike': ('Двойной удар', '15% шанс ударить дважды',      'book_double'),
    'regen':         ('Регенерация', '+1 HP каждые 2 секунды',          'book_regen'),
    'vampire':       ('Вампиризм',   '5% урона возвращается в HP',    'book_vampire'),
    'thorns':        ('Шипы',        '2 урона атакующему',            'book_thorns'),
    'greedy':        ('Жадность',    '+50% золота с побеждённых',     'book_greedy'),
}
PERK_BOOK_MAP = {flag: info[2] for flag, info in PERK_INFO.items()}
