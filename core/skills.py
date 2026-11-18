"""Дерево навыков, смена класса и ремесленные ветки.

Модуль чистый (без pygame): им пользуются и одиночная игра, и сервер.

Модель:
  * «Навыки класса» — узлы покупаются за очки навыков (1 очко за уровень).
    Узлы открываются с 1/5/9/13/17/21/25 уровня персонажа.
  * «Кузнечное дело», «Алхимия», «Магическая инженерия» — общие для всех
    классов ветки. Уровень 1..25 растёт от опыта за крафт, и с ним
    открываются рецепты (у предметов есть ``skill`` и ``skill_req``).
  * Смена класса: первая на 5-м уровне, вторая на 15-м. Ветка прокачки
    ограничена 25-м уровнем.

Эффекты узлов складываются в словарь бонусов, который читает
``Player.recalc`` (характеристики) и боевые правила (вампиризм, золото).
"""
from __future__ import annotations

from typing import Any, Optional

from core.data import CLASSES, class_data

SKILL_CAP = 25
CLASS_CHANGE_LEVELS = (5, 15)
SKILL_XP_BASE = 26
SKILL_XP_EXPONENT = 1.28

TAB_CLASS = 'class'
# Канонические ключи веток создания совпадают с категориями предметов
# (core.data.SKILLS). Старые имена оставлены как алиасы.
CRAFT_TABS = ('blacksmith', 'alchemy', 'magic_engineering')
TABS = (TAB_CLASS,) + CRAFT_TABS
TAB_NAMES = {
    TAB_CLASS: 'Навыки класса',
    'blacksmith': 'Кузнечное дело',
    'alchemy': 'Алхимия',
    'magic_engineering': 'Магическая инженерия',
}
TAB_SHORT = {
    TAB_CLASS: 'Класс',
    'blacksmith': 'Ковка',
    'alchemy': 'Алхимия',
    'magic_engineering': 'Инженерия',
}
# Алиасы старых названий веток (сейвы и код до слияния).
SKILL_ALIASES = {
    'smithing': 'blacksmith',
    'engineering': 'magic_engineering',
    'blacksmith': 'blacksmith',
    'alchemy': 'alchemy',
    'magic_engineering': 'magic_engineering',
}
# Канонический ключ -> имя эффекта опыта у класса.
SKILL_XP_EFFECT = {
    'blacksmith': 'smithing_xp',
    'alchemy': 'alchemy_xp',
    'magic_engineering': 'engineering_xp',
}


def canon_skill(tab: Any) -> Any: 
    """Приводит старое/новое имя ветки к каноническому ключу."""
    if tab is None:
        return None
    return SKILL_ALIASES.get(tab, tab)


# Названия бонусов для UI.
EFFECT_NAMES = {
    'atk': 'ATK', 'df': 'DEF', 'hp': 'HP', 'crit': 'крит', 'dodge': 'уклонение',
    'lifesteal': 'вампиризм', 'gold_pct': 'золото', 'xp_pct': 'опыт',
    'quest_gold': 'золото за задания', 'quest_xp': 'опыт за задания',
    'attack_speed': 'скорость атаки', 'move_speed': 'скорость хода',
    'mana': 'мана', 'fov': 'обзор', 'craft_quality': 'качество крафта',
    'craft_xp': 'опыт создания', 'smithing_xp': 'опыт ковки',
    'alchemy_xp': 'опыт алхимии',
    'engineering_xp': 'опыт инженерии', 'extra_stats': 'доп. характеристики',
    'rage': 'ярость', 'frenzy': 'исступление', 'summon': 'спутник',
    'plain_weapons': 'любое оружие', 'slow_hit': 'замедление удара',
}
PERCENT_EFFECTS = {
    'crit', 'dodge', 'lifesteal', 'gold_pct', 'xp_pct', 'quest_gold',
    'quest_xp', 'attack_speed', 'move_speed', 'craft_xp', 'smithing_xp',
    'alchemy_xp', 'engineering_xp', 'rage', 'frenzy', 'slow_hit',
}


def _scale(value: Any, factor: Any) -> Any: 
    if isinstance(value, float):
        return round(value * factor, 2)
    return int(round(value * factor))


def _scale_effects(effects: Any, factor: Any) -> Any: 
    keep = ('summon', 'extra_stats', 'plain_weapons')
    scaled = {}
    for key, value in effects.items():
        scaled[key] = value if key in keep else _scale(value, factor)
    return scaled


# ── Шаблоны классовых веток по архетипу ──
# (название, требуемый уровень, эффекты)
# Расширено с 7 до 12 узлов на архетип — больше вариантов для траты
# очков навыков (25 уровней = 25 очков, 7 узлов = 18 очков некуда).
# Уровни: 1/3/5/7/9/11/13/15/17/19/21/23/25.
_ARCHETYPE_TEMPLATES = {
    'warrior': (
        ('Стойка воина', 1, {'atk': 1}),
        ('Боевой навык', 3, {'crit': 0.02}),
        ('Закалка', 5, {'hp': 6}),
        ('Крепкий хват', 7, {'df': 1}),
        ('Тяжёлый шаг', 9, {'df': 1}),
        ('Боевой транс', 11, {'attack_speed': 0.10}),
        ('Точный удар', 13, {'crit': 0.03}),
        ('Отпор', 15, {'thorns': 1}),
        ('Натиск', 17, {'atk': 2}),
        ('Железная кожа', 19, {'df': 2}),
        ('Жажда боя', 21, {'lifesteal': 0.03}),
        ('Несокрушимость', 25, {'atk': 2, 'hp': 10, 'df': 2}),
    ),
    'mage': (
        ('Медитация', 1, {'mana': 10}),
        ('Лёгкая поступь', 3, {'move_speed': 0.05}),
        ('Поток силы', 5, {'atk': 1}),
        ('Резонанс маны', 7, {'mana': 5}),
        ('Мантия тени', 9, {'dodge': 0.03}),
        ('Эфирный щит', 11, {'df': 1}),
        ('Печать крита', 13, {'crit': 0.04}),
        ('Разряд', 15, {'atk': 1, 'crit': 0.02}),
        ('Глубокий колодец', 17, {'mana': 15}),
        ('Цепная молния', 19, {'atk': 2}),
        ('Источник', 21, {'mana': 20}),
        ('Архимаг', 25, {'atk': 3, 'crit': 0.03, 'mana': 10}),
    ),
    'rogue': (
        ('Ловкость', 1, {'dodge': 0.03}),
        ('Тихий шаг', 3, {'move_speed': 0.08}),
        ('Сноровка', 5, {'atk': 1}),
        ('Лёгкая мишень', 7, {'dodge': 0.02}),
        ('Хладнокровие', 9, {'crit': 0.04}),
        ('Скользящий хват', 11, {'attack_speed': 0.08}),
        ('Тень', 13, {'dodge': 0.04}),
        ('Слабое место', 15, {'crit': 0.03}),
        ('Коварство', 17, {'atk': 2}),
        ('Неуловимость', 19, {'dodge': 0.05}),
        ('Мародёр', 21, {'gold_pct': 0.15}),
        ('Ловчий', 25, {'atk': 2, 'crit': 0.04, 'dodge': 0.03}),
    ),
    'artisan': (
        ('Ученик', 1, {'craft_xp': 0.10}),
        ('Сборщик', 3, {'gold_pct': 0.05}),
        ('Выносливость', 5, {'hp': 5}),
        ('Ремесленный глаз', 7, {'craft_xp': 0.05}),
        ('Верный глаз', 9, {'craft_quality': 1}),
        ('Смекалка', 11, {'atk': 1}),
        ('Тщательность', 13, {'craft_quality': 1}),
        ('Решимость', 15, {'atk': 1, 'df': 1}),
        ('Мастерская', 17, {'craft_xp': 0.20}),
        ('Подмастерье', 19, {'craft_xp': 0.10, 'hp': 5}),
        ('Изобретательность', 21, {'extra_stats': 1}),
        ('Дело жизни', 25, {'craft_quality': 1, 'craft_xp': 0.30, 'hp': 10}),
    ),
}

_TIER_SCALE = {1: 1.0, 2: 1.4, 3: 1.8}
_CLASS_TREE_CACHE = {}
_NODE_INDEX = {}

# Названия узлов по основному эффекту: разные классы получают разные
# наборы, поэтому ветки специализаций не выглядят копией базовой.
# Пулы расширены до 4-8 имён на эффект — достаточно для 12+ узлов на архетип.
_EFFECT_NAME_POOLS = {
    'atk': ('Точный удар', 'Натиск', 'Сокрушение', 'Казнь', 'Размах',
            'Пробой', 'Рассечение', 'Мощь', 'Сила'),
    'hp': ('Закалка', 'Стойкость', 'Живучесть', 'Второе дыхание',
           'Толстокожесть', 'Выносливость', 'Крепкое здоровье', 'Гигант'),
    'df': ('Тяжёлый шаг', 'Щит', 'Бастион', 'Крепость', 'Отпор', 'Защита',
           'Стена', 'Железная кожа'),
    'crit': ('Печать крита', 'Хладнокровие', 'Смертельный глаз', 'Остриё',
             'Меткий глаз', 'Слабое место', 'Снайпер', 'Точный расчёт'),
    'dodge': ('Ловкость', 'Тень', 'Скольжение', 'Уворот', 'Неуловимость',
              'Воздушный шаг', 'Призрак', 'Танец ветра'),
    'mana': ('Медитация', 'Поток силы', 'Источник', 'Резонанс', 'Средоточие',
             'Печать силы', 'Глубокий колодец', 'Эфир'),
    'lifesteal': ('Жажда боя', 'Кровопийца', 'Вампиризм', 'Пир смерти',
                  'Кровавый обряд'),
    'gold_pct': ('Мародёр', 'Делец', 'Добытчик', 'Золотая жила',
                 'Скупой рыцарь'),
    'xp_pct': ('Искатель', 'Бывалый', 'Следопыт', 'Мудрец'),
    'craft_xp': ('Ученик', 'Мастерская', 'Дело жизни', 'Ремесленный глаз',
                 'Подмастерье', 'Эксперт'),
    'craft_quality': ('Верный глаз', 'Изобретательность', 'Ювелирная работа',
                      'Тщательность', 'Шедевр'),
    'attack_speed': ('Быстрый взмах', 'Скорость', 'Вихрь', 'Стремительность',
                     'Боевой транс'),
    'move_speed': ('Лёгкий шаг', 'Проворство', 'Стремительный', 'Ветер в ногах'),
    'quest_gold': ('Награда', 'Заказчик', 'Щедрость'),
    'quest_xp': ('Опыт заданий', 'Исполнитель', 'Знаток'),
    'smithing_xp': ('Кузнец', 'Наковальня', 'Молотобоец'),
    'alchemy_xp': ('Алхимик', 'Котёл', 'Зельевар'),
    'engineering_xp': ('Инженер', 'Руны', 'Магомеханик'),
    'thorns': ('Шипы', 'Колючий доспех', 'Еж', 'Отражение'),
    'regen': ('Регенерация', 'Восстановление', 'Живительная сила'),
    'summon': ('Спутник', 'Призыв', 'Фамильяр'),
    'extra_stats': ('Изобретательность', 'Эрудиция', 'Талант'),
    'plain_weapons': ('Любое оружие', 'Универсал'),
    'slow_hit': ('Замедление удара', 'Ледяной касание'),
    'frenzy': ('Исступление', 'Берсерк', 'Ярость'),
    'rage': ('Ярость', 'Гнев', 'Кровожадность'),
    'fov': ('Орлиный глаз', 'Дальнозоркость', 'Обзор'),
}


def _stable_offset(key: Any) -> Any: 
    """Детерминированный сдвиг по имени класса (не зависит от hash seed)."""
    return sum(ord(ch) for ch in str(key)) % 97


def _class_order(class_key: Any, count: Any) -> Any: 
    """Детерминированная перестановка средних узлов ветки для класса."""
    order = list(range(count))
    seed = _stable_offset(class_key) + 17
    for i in range(count - 1, 0, -1):
        seed = (seed * 1103515245 + 12345) & 0x7fffffff
        j = seed % (i + 1)
        order[i], order[j] = order[j], order[i]
    return order


def _pool_name(effects: Any, offset: Any, fallback: Any, used: Any = None) -> Any: 
    for key in effects:
        pool = _EFFECT_NAME_POOLS.get(key)
        if pool:
            for step in range(len(pool)):
                candidate = pool[(offset + step) % len(pool)]
                if not used or candidate not in used:
                    return candidate
            return pool[offset % len(pool)]
    return fallback


def _class_profile(class_key: Any) -> Any: 
    """Характеристики и эффекты класса с учётом базового архетипа."""
    cls = class_data(class_key)
    base = class_data(cls.get('base', 'default'))
    stats = dict(base.get('stats') or {})
    for key, value in (cls.get('bonus') or {}).items():
        stats[key] = stats.get(key, 0) + value
    if cls.get('stats'):
        stats.update(cls['stats'])
    return stats, dict(cls.get('effects') or {})


def _profile_effects(stats: Any) -> Any: 
    """Прибавки к капительному узлу по «сильным» характеристикам класса."""
    out = {}
    if stats.get('str', 5) >= 6:
        out['atk'] = 1
    if stats.get('df_stat', 5) >= 6:
        out['df'] = 1
    if stats.get('vit', 5) >= 6:
        out['hp'] = 4
    if stats.get('agi', 5) >= 6:
        out['dodge'] = 0.02
    if stats.get('luk', 5) >= 6:
        out['crit'] = 0.02
    if stats.get('int', 5) >= 6:
        out['mana'] = 4
    return out


def _fragment(effects: Any, share: Any) -> Any: 
    """Часть эффектов класса для конкретного узла (доли/проценты)."""
    out = {}
    for key, value in effects.items():
        if key in ('summon', 'extra_stats', 'plain_weapons'):
            piece = value if share >= 0.5 else 0
        elif isinstance(value, float):
            piece = round(value * share, 2)
        else:
            piece = int(round(value * share))
        if piece:
            out[key] = piece
    return out


def class_tree(class_key: Any) -> Any: 
    """Узлы ветки класса: [{key, name, level, effects}].

    Ветка привязана к конкретному классу: у специализаций меняются и
    названия узлов, и эффекты (часть классовых бонусов + профиль
    характеристик), поэтому после смены класса дерево другое.
    """
    cached = _CLASS_TREE_CACHE.get(class_key)
    if cached is not None:
        return cached
    cls = class_data(class_key)
    template = _ARCHETYPE_TEMPLATES.get(cls.get('base', 'warrior'),
                                       _ARCHETYPE_TEMPLATES['warrior'])
    factor = _TIER_SCALE.get(cls.get('tier', 1), 1.0)
    tier = int(cls.get('tier', 1) or 1)
    stats, class_effects = _class_profile(class_key)
    profile = _profile_effects(stats)
    offset = _stable_offset(class_key)
    template_effects = [effects for (_name, _level, effects) in template]
    if tier >= 2 and len(template) > 3:
        # Специализации получают свой порядок средних узлов: ветка
        # отличается не только названием, но и набором бонусов.
        middle = list(range(1, len(template) - 1))
        shuffled = [template_effects[middle[k]]
                    for k in _class_order(class_key, len(middle))]
        for slot, effects in zip(middle, shuffled):
            template_effects[slot] = effects
    nodes = []
    used_names = set()
    for i, (name, level, effects) in enumerate(template):
        node_effects = dict(template_effects[i])
        if tier >= 2:
            # 40% классовых эффектов — средний узел, 60% — венец ветки.
            share = 0.4 if i == 3 else (0.6 if i == 6 else 0.0)
            if share:
                for key, value in _fragment(class_effects, share).items():
                    node_effects[key] = node_effects.get(key, 0) + value
            if i == 6:
                for key, value in profile.items():
                    node_effects[key] = node_effects.get(key, 0) + value
            if i == 0:
                node_name = f"Путь: {cls['name']}"
            elif i == len(template) - 1:
                node_name = f"Венец: {cls['name']}"
            else:
                node_name = _pool_name(node_effects, offset + i, name,
                                       used_names)
        else:
            node_name = name
        used_names.add(node_name)
        node = {
            'key': f'{class_key}:{i}',
            'name': node_name,
            'level': level,
            'class_key': class_key,
            'effects': _scale_effects(node_effects, factor),
        }
        nodes.append(node)
        _NODE_INDEX[node['key']] = node
    _CLASS_TREE_CACHE[class_key] = nodes
    return nodes


# ── Общие ветки создания ──
CRAFT_TREES = {
    'blacksmith': {
        'name': 'Кузнечное дело',
        'desc': 'Оружие, броня, шлемы и поножи. С уровнем открываются рецепты.',
        'nodes': (
            ('Основы ковки', 1, {}),
            ('Работа с костью', 3, {}),
            ('Железо и кожа', 6, {}),
            ('Кольчуга', 10, {'craft_quality': 1}),
            ('Сталь', 14, {}),
            ('Латы', 18, {'craft_quality': 1}),
            ('Драконьи сплавы', 22, {}),
            ('Легендарная ковка', 25, {'craft_quality': 1}),
        ),
    },
    'alchemy': {
        'name': 'Алхимия',
        'desc': 'Зелья лечения, маны, баффы и яды.',
        'nodes': (
            ('Простые отвары', 1, {}),
            ('Настой маны', 4, {}),
            ('Концентрация', 8, {'craft_quality': 1}),
            ('Яды', 10, {}),
            ('Зелья силы и ловкости', 14, {}),
            ('Каменная кожа', 16, {'craft_quality': 1}),
            ('Скорость и проворность', 20, {}),
            ('Эликсиры', 25, {'craft_quality': 1}),
        ),
    },
    'magic_engineering': {
        'name': 'Магическая инженерия',
        'desc': 'Свитки и руны. Уровень открывает новые формулы.',
        'nodes': (
            ('Первые руны', 1, {}),
            ('Руна пламени', 2, {}),
            ('Свиток огня', 6, {'craft_quality': 1}),
            ('Заморозка', 8, {}),
            ('Руна защиты', 10, {}),
            ('Молния', 14, {'craft_quality': 1}),
            ('Инферно', 20, {}),
            ('Руна стража', 22, {'craft_quality': 1}),
        ),
    },
}


def craft_tree(tab: Any) -> Any: 
    tab = canon_skill(tab)
    tree = CRAFT_TREES.get(tab)
    if not tree:
        return []
    return [{'key': f'{tab}:{i}', 'name': name, 'level': level,
             'effects': dict(effects), 'tab': tab}
            for i, (name, level, effects) in enumerate(tree['nodes'])]


for _tab_key in CRAFT_TABS:
    for _node in craft_tree(_tab_key):
        _NODE_INDEX[_node['key']] = _node


# ── Опыт навыков создания ──
def skill_xp_to_next(level: Any) -> Any: 
    return max(1, int(SKILL_XP_BASE * (max(1, level) ** SKILL_XP_EXPONENT)))


def _merge_aliases(player: Any, tab: Any) -> Any: 
    """Переносит плоские/старые записи навыков в канонический ключ."""
    skills = getattr(player, 'skills', None)
    if not isinstance(skills, dict):
        skills = {}
        player.skills = skills
    for alias, canon in SKILL_ALIASES.items():
        if alias == canon or alias not in skills:
            continue
        legacy = skills.pop(alias)
        current = skills.get(canon)
        if isinstance(legacy, dict):
            level = int(legacy.get('level', 1) or 1)
            xp = int(legacy.get('xp', 0) or 0)
        else:
            level = int(legacy or 1)
            xp = int((getattr(player, 'skill_xp', {}) or {}).get(canon, 0) or 0)
        if not isinstance(current, dict):
            skills[canon] = {'level': level, 'xp': xp}
            continue
        if level > int(current.get('level', 1) or 1):
            current['level'] = level
            current['xp'] = max(xp, int(current.get('xp', 0) or 0))


def _skill_entry(player: Any, tab: Any) -> Any: 
    """Запись навыка: {'level': N, 'xp': M}. Понимает и старый формат."""
    tab = canon_skill(tab)
    _merge_aliases(player, tab)
    skills = getattr(player, 'skills', None) or {}
    value = skills.get(tab, 1)
    if isinstance(value, dict):
        return value
    # Старый формат: плоский уровень в skills + опыт в skill_xp.
    level = int(value or 1)
    xp = int((getattr(player, 'skill_xp', {}) or {}).get(tab, 0))
    entry = {'level': level, 'xp': xp}
    skills[tab] = entry
    player.skills = skills
    return entry


def craft_level(player: Any, tab: Any) -> Any: 
    return max(1, int(_skill_entry(player, tab).get('level', 1) or 1))


def craft_xp(player: Any, tab: Any) -> Any: 
    return max(0, int(_skill_entry(player, tab).get('xp', 0) or 0))


def craft_multiplier(player: Any, tab: Any) -> Any: 
    """Множитель опыта создания от класса (1.25, 1.5, 2.5 ...)."""
    tab = canon_skill(tab)
    cls = class_data(getattr(player, 'class_key', 'default'))
    effects = cls.get('effects', {})
    specific = effects.get(SKILL_XP_EFFECT.get(tab, f'{tab}_xp'), 0.0)
    generic = (effects.get('craft_xp', 0.0)
               + aggregate(player).get('craft_xp', 0.0))
    return max(1.0, 1.0 + max(specific, generic))


def gain_craft_xp(player: Any, tab: Any, base_amount: Any) -> Any: 
    """Начисляет опыт навыка. Возвращает список новых уровней навыка."""
    tab = canon_skill(tab)
    if tab not in CRAFT_TABS:
        return []
    amount = max(1, int(round(base_amount * craft_multiplier(player, tab))))
    entry = _skill_entry(player, tab)
    entry['xp'] = craft_xp(player, tab) + amount
    gained = []
    while craft_level(player, tab) < SKILL_CAP:
        need = skill_xp_to_next(craft_level(player, tab))
        if entry['xp'] < need:
            break
        entry['xp'] -= need
        entry['level'] = craft_level(player, tab) + 1
        gained.append(entry['level'])
    # Дублируем плоский словарь для старых сейвов и UI.
    player.skill_xp = dict(getattr(player, 'skill_xp', {}) or {})
    player.skill_xp[tab] = entry['xp']
    return gained


# ── Рецепты ──
def recipe_requirement(item: Any) -> Any: 
    """(ветка, требуемый уровень) для предмета или (None, 0)."""
    if not item:
        return None, 0
    tab = canon_skill(item.get('skill'))
    if tab not in CRAFT_TABS:
        return None, 0
    return tab, int(item.get('skill_req', 1))


def recipe_unlocked(player: Any, item: Any) -> Any: 
    # Особые крафты: только для ветки ремесленника и после чтения чертежа.
    classes = item.get('craft_classes')
    if classes:
        base = getattr(player, 'class_base', '')
        key = getattr(player, 'class_key', '')
        if base not in classes and key not in classes:
            return False
    blueprint = item.get('blueprint_required')
    if blueprint and blueprint not in getattr(player, 'blueprints', set()):
        return False
    tab, req = recipe_requirement(item)
    if tab is None:
        return True
    return craft_level(player, tab) >= req


def craftable_keys(tab: Any, player: Any) -> Any: 
    """Ключи рецептов ветки, доступные игроку (включая ещё закрытые)."""
    from core.data import get_craftable_items
    result = []
    for key, item in get_craftable_items():
        if canon_skill(item.get('skill')) != canon_skill(tab):
            continue
        result.append(key)
    return result


# ── Классы ──
def class_tier(player: Any) -> Any: 
    return int(class_data(getattr(player, 'class_key', 'default')).get('tier', 1))


def class_change_requirement(player: Any) -> Any: 
    """Уровень персонажа, нужный для следующей смены класса (или None)."""
    tier = class_tier(player)
    if tier >= 3:
        return None
    return CLASS_CHANGE_LEVELS[tier - 1]


def promotion_options(player: Any) -> Any: 
    """Следующие классы ветки (пусто, если вершина достигнута)."""
    cls = class_data(getattr(player, 'class_key', 'default'))
    return list(cls.get('next', ()))


def can_change_class(player: Any) -> Any: 
    req = class_change_requirement(player)
    return req is not None and player.level >= req and bool(promotion_options(player))


def _node_class_key(node_key: Any) -> Any: 
    """Класс, которому принадлежит узел (или None для ремесленных)."""
    node = _NODE_INDEX.get(node_key)
    if node is not None:
        if node.get('tab'):
            return None
        return node.get('class_key')
    if ':' in node_key:
        return node_key.split(':', 1)[0]
    return None


def release_class_nodes(player: Any, keep_class: Any) -> Any: 
    """Убирает узлы прежних классов. Возвращает число возвращённых очков.

    Ветка привязана к классу, поэтому после повышения старая ветка
    недоступна: её узлы снимаются, а потраченные очки возвращаются,
    чтобы игрок мог вложить их в новую ветку.
    """
    learned = set(getattr(player, 'skill_nodes', set()) or ())
    kept = set()
    refund = 0
    for key in learned:
        node_class = _node_class_key(key)
        if node_class is not None and node_class != keep_class:
            refund += 1
        else:
            kept.add(key)
    if refund:
        player.skill_nodes = kept
    return refund


def change_class(player: Any, new_key: Any) -> Any: 
    """Смена класса. Возвращает (успех, причина)."""
    cls = class_data(getattr(player, 'class_key', 'default'))
    if new_key not in cls.get('next', ()):
        return False, 'path'
    req = class_change_requirement(player)
    if req is None:
        return False, 'max'
    if player.level < req:
        return False, 'level'
    target = CLASSES.get(new_key)
    if not target:
        return False, 'unknown'

    for stat, value in (target.get('bonus') or {}).items():
        setattr(player, stat, getattr(player, stat, 0) + value)
    refunded = release_class_nodes(player, new_key)
    player.class_key = new_key
    player.class_name = target['name']
    player.class_tier = target.get('tier', player.class_tier + 1)
    player.class_changes = getattr(player, 'class_changes', 0) + 1
    player.skill_points = getattr(player, 'skill_points', 0) + 2 + refunded
    player.recalc()
    return True, 'ok'


def disallowed_equipment(player: Any) -> Any: 
    """Слоты, которые текущий класс носить не может."""
    from core.data import class_can_use
    result = []
    for slot in ('weapon', 'armor', 'head', 'legs', 'boots'):
        key = getattr(player, slot, None)
        if not key:
            continue
        ok, reason = class_can_use(player.class_key, key)
        if not ok:
            result.append((slot, key, reason))
    return result


# ── Узлы класса ──
def learn_node(player: Any, node_key: Any) -> Any: 
    """Покупает узел классовой ветки за очко навыка."""
    node = _NODE_INDEX.get(node_key)
    if node is None and ':' in node_key:
        # Ветка ещё не запрашивалась в этой сессии (например, после
        # загрузки сейва) — строим её, чтобы ключ стал известен.
        class_tree(node_key.split(':', 1)[0])
        node = _NODE_INDEX.get(node_key)
    if not node or node.get('tab'):
        return False, 'unknown'
    learned = getattr(player, 'skill_nodes', set())
    if node_key in learned:
        return False, 'learned'
    if node.get('class_key') != player.class_key:
        return False, 'class'
    if player.level < node['level']:
        return False, 'level'
    if getattr(player, 'skill_points', 0) <= 0:
        return False, 'points'
    player.skill_points -= 1
    learned.add(node_key)
    player.skill_nodes = learned
    player.recalc()
    return True, 'ok'


def is_learned(player: Any, node_key: Any) -> Any: 
    return node_key in getattr(player, 'skill_nodes', set())


def node_effects(node_key: Any) -> Any: 
    node = _NODE_INDEX.get(node_key)
    return dict(node.get('effects', {})) if node else {}


def aggregate(player: Any) -> Any: 
    """Сумма всех пассивных бонусов: класс + изученные узлы."""
    totals = {}

    def merge(effects: Any) -> Any: 
        for key, value in effects.items():
            if key in ('summon', 'plain_weapons', 'extra_stats'):
                totals[key] = max(totals.get(key, 0), value)
            else:
                totals[key] = totals.get(key, 0) + value

    current = class_data(getattr(player, 'class_key', 'default'))
    merge(current.get('effects', {}))
    for node_key in getattr(player, 'skill_nodes', set()):
        merge(node_effects(node_key))
    # Уровень ремесленных веток тоже даёт качество крафта.
    for tab in CRAFT_TABS:
        level = craft_level(player, tab)
        for node in craft_tree(tab):
            if node['level'] <= level:
                for eff_key, eff_value in node['effects'].items():
                    if eff_key == 'craft_quality':
                        totals[eff_key] = totals.get(eff_key, 0) + eff_value
    return totals


def craft_quality_bonus(player: Any) -> Any: 
    return int(aggregate(player).get('craft_quality', 0))


# Кто получает мини-игру качества и для каких веток.
_MINIGAME_CLASSES = {
    'blacksmith': ('apprentice', 'blacksmith', 'magic_engineer'),
    'magic_engineering': ('apprentice', 'blacksmith', 'magic_engineer'),
    'alchemy': ('potionmaker', 'alchemist', 'herbalist'),
}


def has_craft_minigame(player: Any, tab: Any) -> Any: 
    tab = canon_skill(tab)
    return getattr(player, 'class_key', 'default') in _MINIGAME_CLASSES.get(tab, ())


def craft_tab_for(item: Any) -> Any: 
    tab, _req = recipe_requirement(item)
    return tab


def describe_effects(effects: Any) -> Any: 
    """'ATK +2, крит +3%' для подсказок UI."""
    parts = []
    for key, value in effects.items():
        if key in ('summon', 'plain_weapons'):
            if value:
                parts.append(EFFECT_NAMES.get(key, key))
            continue
        if key == 'extra_stats':
            parts.append(f"доп. характеристики +{value}")
            continue
        name = EFFECT_NAMES.get(key, key)
        if key in PERCENT_EFFECTS:
            parts.append(f"{name} +{int(round(value * 100))}%")
        else:
            parts.append(f"{name} +{value}")
    return ', '.join(parts)


def class_trait_text(class_key: Any) -> Any: 
    cls = class_data(class_key)
    perk = cls.get('perk', '')
    return perk


def tab_level(player: Any, tab: Any) -> Any: 
    if tab == TAB_CLASS:
        return player.level
    return craft_level(player, tab)


def tab_progress(player: Any, tab: Any) -> Any: 
    """(текущее, следующее, доля) для полоски навыка создания."""
    if tab == TAB_CLASS:
        return (player.level, None, 0.0)
    level = craft_level(player, tab)
    if level >= SKILL_CAP:
        return (level, None, 1.0)
    need = skill_xp_to_next(level)
    have = craft_xp(player, tab)
    return (level, need, min(1.0, have / max(1, need)))


def recipes_for_level(tab: Any, level: Any) -> Any: 
    """[(key, item)] рецептов ветки, открываемых на уровне level."""
    from core.data import get_craftable_items
    result = []
    for key, item in get_craftable_items():
        if canon_skill(item.get('skill')) == canon_skill(tab) and \
                int(item.get('skill_req', 1)) == level:
            result.append((key, item))
    return result


def available_nodes(player: Any, class_key: Any = None) -> Any: 
    """Узлы текущего класса с состоянием для UI."""
    class_key = class_key or player.class_key
    nodes = []
    for node in class_tree(class_key):
        learned = is_learned(player, node['key'])
        state = 'learned' if learned else (
            'available' if (player.skill_points > 0
                            and player.level >= node['level'])
            else ('level' if player.level < node['level'] else 'points'))
        nodes.append({**node, 'state': state, 'learned': learned})
    return nodes
