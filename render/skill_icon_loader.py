"""Загрузка иконок древа навыков из assets/skills/.

Если PNG существует — грузим его. Иначе — fallback на процедурную иконку
из render.icon_renderer (или простую заглушку с первой буквой названия).

Структура файлов:
    assets/skills/class/<class_key>:<node_index>.png   — узлы классовых веток
    assets/skills/craft/<craft_tab>:<node_index>.png    — узлы веток крафта

Имена файлов = ``node['key']`` из core.skills.class_tree() / craft_tree().
Примеры:
    assets/skills/class/warrior:0.png  — Стойка воина
    assets/skills/class/mage:0.png     — Медитация
    assets/skills/craft/blacksmith:0.png — Основы ковки
"""
import os
import pygame
from core.config import ASSETS_DIR

_SKILLS_DIR = os.path.join(ASSETS_DIR, 'skills')
_CACHE = {}


def load_skill_icon(node_key, size=48):
    """Загрузить иконку узла навыка из assets/skills/, иначе fallback.

    ``node_key`` — ключ вида 'warrior:0', 'mage:3', 'blacksmith:5'.
    ``size`` — целевой размер в пикселях (квадрат).
    """
    if node_key in _CACHE and _CACHE[node_key][0] == size:
        return _CACHE[node_key][1]

    # Сначала ищем в class/, потом в craft/, потом в корне skills/.
    candidates = [
        os.path.join(_SKILLS_DIR, 'class', f'{node_key}.png'),
        os.path.join(_SKILLS_DIR, 'craft', f'{node_key}.png'),
        os.path.join(_SKILLS_DIR, f'{node_key}.png'),
    ]
    for path in candidates:
        if os.path.exists(path):
            try:
                surf = pygame.image.load(path).convert_alpha()
                if surf.get_width() != size or surf.get_height() != size:
                    surf = pygame.transform.smoothscale(surf, (size, size))
                _CACHE[node_key] = (size, surf)
                return surf
            except Exception:
                continue

    # Fallback: цветной квадрат с первой буквой node_key
    surf = _procedural_skill_icon(node_key, size)
    _CACHE[node_key] = (size, surf)
    return surf


def _procedural_skill_icon(node_key, size=48):
    """Простая иконка: цветной круг с первой буквой названия узла."""
    surf = pygame.Surface((size, size), pygame.SRCALPHA)
    # Детерминированный цвет из ключа
    h = sum(ord(c) for c in str(node_key)) % 360
    color = pygame.Color(0)
    color.hsva = (h, 50, 70, 100)
    pygame.draw.circle(surf, color, (size // 2, size // 2), size // 2 - 2)
    pygame.draw.circle(surf, (240, 220, 180), (size // 2, size // 2),
                       size // 2 - 2, 1)
    # Первая буква (после двоеточия, если есть)
    label = str(node_key).split(':')[-1][:1].upper()
    font = pygame.font.SysFont('consolas', size // 2, bold=True)
    t = font.render(label, True, (240, 230, 200))
    surf.blit(t, (size // 2 - t.get_width() // 2,
                  size // 2 - t.get_height() // 2))
    return surf


def clear_cache():
    """Очистить кэш (нужно при смене набора ассетов)."""
    _CACHE.clear()


# Полный список нужных иконок (для документации и генерации заглушек).
# Источник: core.skills.class_tree() и core.skills.craft_tree().
REQUIRED_ICONS_CLASS_BASE = [
    'warrior:0', 'warrior:1', 'warrior:2', 'warrior:3',
    'warrior:4', 'warrior:5', 'warrior:6',
    'mage:0', 'mage:1', 'mage:2', 'mage:3',
    'mage:4', 'mage:5', 'mage:6',
    'rogue:0', 'rogue:1', 'rogue:2', 'rogue:3',
    'rogue:4', 'rogue:5', 'rogue:6',
    'artisan:0', 'artisan:1', 'artisan:2', 'artisan:3',
    'artisan:4', 'artisan:5', 'artisan:6',
]

REQUIRED_ICONS_CRAFT = [
    'blacksmith:0', 'blacksmith:1', 'blacksmith:2', 'blacksmith:3',
    'blacksmith:4', 'blacksmith:5', 'blacksmith:6', 'blacksmith:7',
    'alchemy:0', 'alchemy:1', 'alchemy:2', 'alchemy:3',
    'alchemy:4', 'alchemy:5', 'alchemy:6', 'alchemy:7',
    'magic_engineering:0', 'magic_engineering:1', 'magic_engineering:2',
    'magic_engineering:3', 'magic_engineering:4', 'magic_engineering:5',
    'magic_engineering:6', 'magic_engineering:7',
]


def all_required_icons():
    """Возвращает список всех необходимых имён файлов (без расширения)."""
    base = list(REQUIRED_ICONS_CLASS_BASE) + list(REQUIRED_ICONS_CRAFT)
    # Специализации tier 2/3: для каждого class_key из CLASSES с tier>=2
    # прогнать class_tree(key) и собрать node['key'].
    try:
        from core.data import CLASSES
        from core.skills import class_tree
        for class_key, cls in CLASSES.items():
            if cls.get('tier', 1) >= 2:
                for node in class_tree(class_key):
                    base.append(node['key'])
    except Exception:
        pass
    # Уникальные
    seen = set()
    out = []
    for k in base:
        if k not in seen:
            seen.add(k)
            out.append(k)
    return out
