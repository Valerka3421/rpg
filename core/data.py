"""Игровые данные: монстры, боссы, предметы, магазин, дроп, редкости, крафт, классы."""
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
    # Алхимия: мана и баффы
    'mana_s': {'ch': '!', 'name': 'Малое зелье маны', 'type': 'mana', 'value': 12,
               'color': (120, 160, 255), 'tier': 1, 'sell': 3,
               'recipe': {'berry': 2, 'mushroom': 1},
               'skill': 'alchemy', 'skill_req': 1, 'desc': 'Восстанавливает 12 маны.'},
    'mana_m': {'ch': '!', 'name': 'Зелье маны', 'type': 'mana', 'value': 24,
               'color': (90, 130, 250), 'tier': 2, 'sell': 5,
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
                    'recipe': {'dragon_scale': 1, 'mushroom': 4, 'berry': 4},
                    'skill': 'alchemy', 'skill_req': 25,
                    'desc': 'Восстанавливает 80 HP.'},
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
    'leather_boots': {'ch': '|', 'name': 'Кожаные сапоги', 'type': 'legs', 'value': 1,
                      'level_req': 1, 'color': (160, 120, 80), 'tier': 1, 'sell': 6,
                      'dodge_bonus': 0.03, 'desc': '+1 DEF, +3% укл.'},
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


def item_category(item_key, default='blacksmith'):
    """Категория предмета — она же вкладка крафта и навык за крафт."""
    item = item_key if isinstance(item_key, dict) else ITEMS.get(item_key, {})
    cat = item.get('category')
    if cat in SKILLS:
        return cat
    kind = item.get('type')
    if kind in ('weapon', 'armor', 'head', 'legs', 'material'):
        return 'blacksmith'
    if kind == 'heal':
        return 'alchemy'
    if kind in ('scroll', 'rune'):
        return 'magic_engineering'
    return default


def item_recipes(item_key):
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


def select_recipe(item_key, inventory, recipe_index=None):
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


def skill_xp_to_next(level):
    """Опыт навыка для перехода с ``level`` на ``level + 1``."""
    return max(10, int(45 * (max(1, level) ** 1.35)))


def default_skills():
    return {key: {'level': 1, 'xp': 0} for key in SKILL_ORDER}


def craft_skill_reward(item_key, count=1):
    """Сколько опыта навыка даёт крафт предмета."""
    item = ITEMS.get(item_key, {})
    tier = item.get('tier', 1)
    req = item.get('level_req', 1)
    return max(1, int((3 + tier * 2 + req) * max(1, count)))


# ── Кристаллы маны ──
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


def mana_crystal_for(monster):
    """Размер кристалла зависит от силы моба."""
    hp = max(1, getattr(monster, 'max_hp', 10) or 10)
    if getattr(monster, 'boss', False) or hp >= 60:
        return MANA_CRYSTAL_BY_SIZE['large']
    if hp >= 18:
        return MANA_CRYSTAL_BY_SIZE['normal']
    return MANA_CRYSTAL_BY_SIZE['small']


def roll_mana_crystal(monster, chance=MANA_CRYSTAL_CHANCE):
    """Небольшой шанс выпадения кристалла маны с любого моба."""
    if random.random() >= chance:
        return None
    return mana_crystal_for(monster)


def mana_crystal_powder(crystal_key):
    return MANA_POWDER_BY_CRYSTAL.get(crystal_key, 0)

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
ARMOR_TYPE_NAMES = {
    'light': 'лёгкая', 'medium': 'средняя', 'heavy': 'тяжёлая', 'robe': 'мантия',
}


def _cls(name, tier, base, desc, color, *, next=(), bonus=None, stats=None,
         weapons=(), armor=(), effects=None, start_weapon=None,
         start_armor=None, start_items=None, perk=''):
    return {
        'name': name, 'desc': desc, 'tier': tier, 'base': base,
        'next': tuple(next), 'bonus': dict(bonus or {}),
        'stats': dict(stats) if stats else None,
        'weapons': tuple(weapons), 'armor_types': tuple(armor),
        'effects': dict(effects or {}), 'color': color,
        'start_weapon': start_weapon, 'start_armor': start_armor,
        'start_items': dict(start_items or {}), 'perk': perk,
    }


CLASSES = {
    # ── База ──
    'warrior': _cls(
        'Воин', 1, 'warrior',
        'Крепкий боец. Больше всего STR — сильнейший удар.',
        (220, 130, 100),
        next=('squire', 'mercenary', 'fighter'),
        stats={'str': 8, 'agi': 3, 'vit': 7, 'luk': 3, 'df_stat': 7},
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
        stats={'str': 2, 'agi': 5, 'vit': 4, 'luk': 6, 'df_stat': 4},
        weapons=('staff', 'book', 'dagger'),
        armor=('robe',),
        effects={'mana': 10},
        start_items={'potion_s': 2},
        perk='Носит только мантии, но водит посохом и книгой.',
    ),
    'rogue': _cls(
        'Разбойник', 1, 'rogue',
        'Быстрый и ловкий. Максимальное уклонение.',
        (140, 200, 130),
        next=('killer', 'archer', 'pickpocket', 'tracker'),
        stats={'str': 5, 'agi': 9, 'vit': 4, 'luk': 7, 'df_stat': 3},
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
        stats={'str': 4, 'agi': 5, 'vit': 6, 'luk': 5, 'df_stat': 5},
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
        stats={'str': 5, 'agi': 5, 'vit': 5, 'luk': 5, 'df_stat': 5},
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


def class_data(class_key):
    """Данные класса с подстановкой базового при неизвестном ключе."""
    return CLASSES.get(class_key) or CLASSES.get('default', {})


def class_stats(class_key):
    """Полные характеристики класса (для превью в меню)."""
    cls = class_data(class_key)
    if cls.get('stats'):
        return dict(cls['stats'])
    base = CLASSES.get(cls.get('base', 'default')) or CLASSES['default']
    stats = dict(base.get('stats') or {'str': 5, 'agi': 5, 'vit': 5,
                                       'luk': 5, 'df_stat': 5})
    for name, value in (cls.get('bonus') or {}).items():
        stats[name] = stats.get(name, 0) + value
    return stats


def class_can_use(class_key, full_key):
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
    head_pool = {1: ['leather_cap'],
                 2: ['leather_cap', 'iron_helm'],
                 3: ['iron_helm', 'steel_helm', 'crown']}[tier]
    legs_pool = {1: ['cloth_pants', 'leather_boots'],
                 2: ['leather_boots', 'leather_legs', 'chain_legs',
                     'plate_greaves'],
                 3: ['plate_greaves', 'plate_legs']}[tier]
    if random.random() < 0.45:
        add(random.choice(head_pool), 10 * tier)
    if random.random() < 0.45:
        add(random.choice(legs_pool),  8 * tier)

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
    add(legs_pool[0], 8 * tier)
    return stock


# ════════════════════ ГОРОДСКИЕ ТОРГОВЦЫ ════════════════════
# Ассортимент горожан фиксирован: цены и состав не зависят от глубины и
# случайности, чтобы покупки в городе были предсказуемыми.
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


def make_city_shop_stock(npc_type):
    """Фиксированный ассортимент городского торговца.

    Товары горожан не «продаются навсегда»: слитки, зелья и еду можно
    покупать повторно — иначе после одной закупки лавка опустела бы,
    а реролл для горожан отключён.
    """
    stock = []
    for key, price in CITY_SHOP_STOCK.get(npc_type, ()):
        stock.append({'key': key, 'price': price, 'sold': False,
                      'infinite': True})
    return stock


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


def is_city_shop_npc(npc_type):
    return npc_type in CITY_SHOP_STOCK


# ════════════════════ ДРОП ════════════════════
# Только материалы/трофеи. Золото начисляется отдельно в
# rules.roll_kill_reward и только разумным монстрам: животные (крыса,
# олень, волк, медведь, слайм, кабан) монет не носят.
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

BOSS_DROPS = {
    'G': ['goblin_ear', 'goblin_ear', 'bone'],
    'L': ['bone', 'bone', 'wraith_essence', 'wraith_essence'],
    'V': ['vampire_fang', 'vampire_fang', 'vampire_fang'],
    'A': ['dragon_scale'] * 5,
}


def roll_monster_drops(monster_ch):
    return [k for k, c in MONSTER_DROPS.get(monster_ch, []) if random.random() < c]


def get_craftable_items():
    return sorted([(k, v) for k, v in ITEMS.items()
                   if 'recipe' in v or 'recipes' in v],
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


# ════════════════════ ДОСКА ПОРУЧЕНИЙ ════════════════════
BOARD_BOSS_FLOORS = {5: 'G', 10: 'L', 15: 'V', 20: 'A'}


def _board_monsters_for(dmin, dmax):
    res = [k for k, m in MONSTERS.items()
           if dmin <= m['min_depth'] <= dmax
           and not m.get('overworld_only')]
    if not res and dmax > 0:
        res = [k for k, m in MONSTERS.items()
               if m['min_depth'] <= dmax and not m.get('overworld_only')]
    return res


def _board_collect_targets(dmin, dmax):
    chars = {MONSTERS[k]['ch'] for k in _board_monsters_for(dmin, dmax)
             if k in MONSTERS}
    targets = set()
    for ch in chars:
        for item_key, _ in MONSTER_DROPS.get(ch, []):
            base = item_key.split('+')[0]
            if ITEMS.get(base, {}).get('type') == 'material':
                targets.add(base)
    return sorted(targets)


def _make_board_quest(dmin, dmax, seed):
    r = random.Random(seed)

    # Boss-охота
    for bd, bch in BOARD_BOSS_FLOORS.items():
        if dmin <= bd <= dmax and r.random() < 0.30:
            boss = BOSSES[bd]
            return {
                'id': f"gen_boss_{bch}_{bd}_{seed}", 'type': 'kill',
                'target': bch, 'count': 1,
                'depth_min': dmin, 'depth_max': dmax,
                'name': f"Охота: {boss['name']}",
                'desc': f"Победить {boss['name']} (глубина {bd}).",
                'reward_gold': 150 + bd * 12, 'reward_xp': 100 + bd * 10,
            }

    monsters = _board_monsters_for(dmin, dmax)
    roll = r.random()

    if roll < 0.55 and monsters:                     # kill
        tgt = r.choice(monsters); m = MONSTERS[tgt]
        count = r.randint(3, 7)
        return {
            'id': f"gen_kill_{tgt}_{dmin}_{seed}", 'type': 'kill',
            'target': m['ch'], 'count': count,
            'depth_min': dmin, 'depth_max': dmax,
            'name': f"Уничтожить: {m['name']}",
            'desc': f"Убить {count} × {m['name']}.",
            'reward_gold': 25 + count * 6 + dmin * 3,
            'reward_xp': 15 + count * 4 + dmin * 2,
        }

    if roll < 0.80:                                  # collect
        targets = _board_collect_targets(dmin, dmax)
        if targets:
            tgt = r.choice(targets); it = ITEMS[tgt]
            count = r.randint(2, 5)
            return {
                'id': f"gen_collect_{tgt}_{dmin}_{seed}", 'type': 'collect',
                'target': tgt, 'count': count,
                'depth_min': dmin, 'depth_max': dmax,
                'name': f"Принести: {it['name']}",
                'desc': f"Собрать {count} × {it['name']}.",
                'reward_gold': 30 + count * 8 + dmin * 3,
                'reward_xp': 20 + count * 5 + dmin * 2,
            }

    # reach (fallback)
    target_depth = max(dmax, dmin + 1)
    return {
        'id': f"gen_reach_{target_depth}_{dmin}_{seed}", 'type': 'reach',
        'target': target_depth, 'count': 1,
        'depth_min': dmin, 'depth_max': dmax,
        'name': "Спуститься в глубину",
        'desc': f"Достичь глубины {target_depth}.",
        'reward_gold': 40 + target_depth * 6,
        'reward_xp': 30 + target_depth * 5,
    }


def generate_board_quests(dmin, dmax, count=3, base_seed=0):
    dmin = max(0, dmin); dmax = min(20, dmax)
    out, seen = [], set()
    for i in range(count * 4):
        if len(out) >= count:
            break
        q = _make_board_quest(dmin, dmax, base_seed + i * 7919)
        if q['id'] in seen:
            continue
        out.append(q); seen.add(q['id'])
    return out