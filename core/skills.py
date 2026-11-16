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
from core.data import CLASSES, ITEMS, class_data

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


def canon_skill(tab):
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


def _scale(value, factor):
    if isinstance(value, float):
        return round(value * factor, 2)
    return int(round(value * factor))


def _scale_effects(effects, factor):
    keep = ('summon', 'extra_stats', 'plain_weapons')
    scaled = {}
    for key, value in effects.items():
        scaled[key] = value if key in keep else _scale(value, factor)
    return scaled


# ── Шаблоны классовых веток по архетипу ──
# (название, требуемый уровень, эффекты)
_ARCHETYPE_TEMPLATES = {
    'warrior': (
        ('Стойка воина', 1, {'atk': 1}),
        ('Закалка', 5, {'hp': 6}),
        ('Тяжёлый шаг', 9, {'df': 1}),
        ('Точный удар', 13, {'crit': 0.03}),
        ('Натиск', 17, {'atk': 2}),
        ('Жажда боя', 21, {'lifesteal': 0.03}),
        ('Несокрушимость', 25, {'atk': 2, 'hp': 10}),
    ),
    'mage': (
        ('Медитация', 1, {'mana': 10}),
        ('Поток силы', 5, {'atk': 1}),
        ('Мантия тени', 9, {'dodge': 0.03}),
        ('Печать крита', 13, {'crit': 0.04}),
        ('Разряд', 17, {'atk': 2}),
        ('Источник', 21, {'mana': 20}),
        ('Архимаг', 25, {'atk': 3, 'crit': 0.03}),
    ),
    'rogue': (
        ('Ловкость', 1, {'dodge': 0.03}),
        ('Сноровка', 5, {'atk': 1}),
        ('Хладнокровие', 9, {'crit': 0.04}),
        ('Тень', 13, {'dodge': 0.04}),
        ('Коварство', 17, {'atk': 2}),
        ('Мародёр', 21, {'gold_pct': 0.15}),
        ('Ловчий', 25, {'atk': 2, 'crit': 0.04}),
    ),
    'artisan': (
        ('Ученик', 1, {'craft_xp': 0.10}),
        ('Выносливость', 5, {'hp': 5}),
        ('Верный глаз', 9, {'craft_quality': 1}),
        ('Смекалка', 13, {'atk': 1}),
        ('Мастерская', 17, {'craft_xp': 0.20}),
        ('Изобретательность', 21, {'extra_stats': 1}),
        ('Дело жизни', 25, {'craft_quality': 1, 'craft_xp': 0.30}),
    ),
}

_TIER_SCALE = {1: 1.0, 2: 1.4, 3: 1.8}
_CLASS_TREE_CACHE = {}
_NODE_INDEX = {}


def class_tree(class_key):
    """Узлы ветки класса: [{key, name, level, effects}]."""
    cached = _CLASS_TREE_CACHE.get(class_key)
    if cached is not None:
        return cached
    cls = class_data(class_key)
    template = _ARCHETYPE_TEMPLATES.get(cls.get('base', 'warrior'),
                                       _ARCHETYPE_TEMPLATES['warrior'])
    factor = _TIER_SCALE.get(cls.get('tier', 1), 1.0)
    nodes = []
    for i, (name, level, effects) in enumerate(template):
        node = {
            'key': f'{class_key}:{i}',
            'name': name,
            'level': level,
            'class_key': class_key,
            'effects': _scale_effects(effects, factor),
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


def craft_tree(tab):
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
def skill_xp_to_next(level):
    return max(1, int(SKILL_XP_BASE * (max(1, level) ** SKILL_XP_EXPONENT)))


def _merge_aliases(player, tab):
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


def _skill_entry(player, tab):
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


def craft_level(player, tab):
    return max(1, int(_skill_entry(player, tab).get('level', 1) or 1))


def craft_xp(player, tab):
    return max(0, int(_skill_entry(player, tab).get('xp', 0) or 0))


def craft_multiplier(player, tab):
    """Множитель опыта создания от класса (1.25, 1.5, 2.5 ...)."""
    tab = canon_skill(tab)
    cls = class_data(getattr(player, 'class_key', 'default'))
    effects = cls.get('effects', {})
    specific = effects.get(SKILL_XP_EFFECT.get(tab, f'{tab}_xp'), 0.0)
    generic = (effects.get('craft_xp', 0.0)
               + aggregate(player).get('craft_xp', 0.0))
    return max(1.0, 1.0 + max(specific, generic))


def gain_craft_xp(player, tab, base_amount):
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
def recipe_requirement(item):
    """(ветка, требуемый уровень) для предмета или (None, 0)."""
    if not item:
        return None, 0
    tab = canon_skill(item.get('skill'))
    if tab not in CRAFT_TABS:
        return None, 0
    return tab, int(item.get('skill_req', 1))


def recipe_unlocked(player, item):
    tab, req = recipe_requirement(item)
    if tab is None:
        return True
    return craft_level(player, tab) >= req


def craftable_keys(tab, player):
    """Ключи рецептов ветки, доступные игроку (включая ещё закрытые)."""
    from core.data import get_craftable_items
    result = []
    for key, item in get_craftable_items():
        if canon_skill(item.get('skill')) != canon_skill(tab):
            continue
        result.append(key)
    return result


# ── Классы ──
def class_tier(player):
    return int(class_data(getattr(player, 'class_key', 'default')).get('tier', 1))


def class_change_requirement(player):
    """Уровень персонажа, нужный для следующей смены класса (или None)."""
    tier = class_tier(player)
    if tier >= 3:
        return None
    return CLASS_CHANGE_LEVELS[tier - 1]


def promotion_options(player):
    """Следующие классы ветки (пусто, если вершина достигнута)."""
    cls = class_data(getattr(player, 'class_key', 'default'))
    return list(cls.get('next', ()))


def can_change_class(player):
    req = class_change_requirement(player)
    return req is not None and player.level >= req and bool(promotion_options(player))


def change_class(player, new_key):
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
    player.class_key = new_key
    player.class_name = target['name']
    player.class_tier = target.get('tier', player.class_tier + 1)
    player.class_changes = getattr(player, 'class_changes', 0) + 1
    player.skill_points = getattr(player, 'skill_points', 0) + 2
    player.recalc()
    return True, 'ok'


def disallowed_equipment(player):
    """Слоты, которые текущий класс носить не может."""
    from core.data import class_can_use
    result = []
    for slot in ('weapon', 'armor', 'head', 'legs'):
        key = getattr(player, slot, None)
        if not key:
            continue
        ok, reason = class_can_use(player.class_key, key)
        if not ok:
            result.append((slot, key, reason))
    return result


# ── Узлы класса ──
def learn_node(player, node_key):
    """Покупает узел классовой ветки за очко навыка."""
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


def is_learned(player, node_key):
    return node_key in getattr(player, 'skill_nodes', set())


def node_effects(node_key):
    node = _NODE_INDEX.get(node_key)
    return dict(node.get('effects', {})) if node else {}


def aggregate(player):
    """Сумма всех пассивных бонусов: класс + изученные узлы."""
    totals = {}

    def merge(effects):
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


def craft_quality_bonus(player):
    return int(aggregate(player).get('craft_quality', 0))


# Кто получает мини-игру качества и для каких веток.
_MINIGAME_CLASSES = {
    'blacksmith': ('apprentice', 'blacksmith', 'magic_engineer'),
    'magic_engineering': ('apprentice', 'blacksmith', 'magic_engineer'),
    'alchemy': ('potionmaker', 'alchemist', 'herbalist'),
}


def has_craft_minigame(player, tab):
    tab = canon_skill(tab)
    return getattr(player, 'class_key', 'default') in _MINIGAME_CLASSES.get(tab, ())


def craft_tab_for(item):
    tab, _req = recipe_requirement(item)
    return tab


def describe_effects(effects):
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


def class_trait_text(class_key):
    cls = class_data(class_key)
    perk = cls.get('perk', '')
    return perk


def tab_level(player, tab):
    if tab == TAB_CLASS:
        return player.level
    return craft_level(player, tab)


def tab_progress(player, tab):
    """(текущее, следующее, доля) для полоски навыка создания."""
    if tab == TAB_CLASS:
        return (player.level, None, 0.0)
    level = craft_level(player, tab)
    if level >= SKILL_CAP:
        return (level, None, 1.0)
    need = skill_xp_to_next(level)
    have = craft_xp(player, tab)
    return (level, need, min(1.0, have / max(1, need)))


def recipes_for_level(tab, level):
    """[(key, item)] рецептов ветки, открываемых на уровне level."""
    from core.data import get_craftable_items
    result = []
    for key, item in get_craftable_items():
        if canon_skill(item.get('skill')) == canon_skill(tab) and \
                int(item.get('skill_req', 1)) == level:
            result.append((key, item))
    return result


def available_nodes(player, class_key=None):
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
