"""Добываемые ресурсы мира: деревья, рудные камни и кусты ягод.

Деревья и руда занимают непроходимый тайл (как стены), но по ним можно
ударить, стоя рядом. Куст ягод стоит на полу, ягоды на нём появляются
через ``BERRY_REGROW`` секунд после сбора.

Состояние хранится в ``Dungeon.harvestables`` и сериализуется вместе с
картой, поэтому собранные кусты не «перегенерируются» при загрузке.
"""
from __future__ import annotations

import random
import time

# Сколько секунд восстанавливается ресурс после сбора.
BERRY_REGROW = 45.0
TREE_REGROW = 80.0
ORE_REGROW = 120.0

# Прочность (сколько урона нужно нанести).
TREE_HP = 3
ORE_HP = 6
BUSH_HP = 1

# Награда за добычу: (предмет, минимум, максимум).
HARVEST_REWARDS = {
    'tree': ('wood', 2, 4),
    'ore': ('ore', 2, 4),
    'bush': ('berry', 1, 2),
}

HARVEST_NAMES = {
    'tree': 'Дерево',
    'ore': 'Рудный камень',
    'bush': 'Куст ягод',
}


def make_harvestable(kind, now=None):
    """Создаёт запись ресурса. У куста ягоды есть уже при генерации."""
    if now is None:
        now = time.time()
    return {
        'kind': kind,
        'hp': BUSH_HP if kind == 'bush' else (TREE_HP if kind == 'tree' else ORE_HP),
        'max_hp': BUSH_HP if kind == 'bush' else (TREE_HP if kind == 'tree' else ORE_HP),
        'has': True,
        'ready_at': 0.0,
        'spawned_at': now,
    }


def regrow_time(kind):
    return {'tree': TREE_REGROW, 'ore': ORE_REGROW, 'bush': BERRY_REGROW}.get(
        kind, 60.0)


def is_blocking(record):
    """Дерево и руда непроходимы; куст тоже не даёт встать на клетку."""
    return record is not None


def can_harvest(record):
    return bool(record) and record.get('has', True)


def hit(dungeon, player, x, y, damage, now=None):
    """Удар по ресурсу. Возвращает dict-событие или None.

    Событие: {'kind', 'name', 'done', 'items': [(key, count)], 'hp', 'max_hp'}
    """
    if now is None:
        now = time.time()
    record = dungeon.harvestables.get((x, y))
    if not record or not record.get('has', True):
        return None
    kind = record['kind']
    result = {
        'kind': kind,
        'name': HARVEST_NAMES.get(kind, 'Ресурс'),
        'done': False,
        'items': [],
        'hp': record['hp'],
        'max_hp': record['max_hp'],
    }
    if kind == 'bush':
        record['has'] = False
        record['ready_at'] = now + BERRY_REGROW
        key, lo, hi = HARVEST_REWARDS['bush']
        count = random.randint(lo, hi)
        player.inventory[key] = player.inventory.get(key, 0) + count
        result.update(done=True, items=[(key, count)], hp=0)
        return result

    record['hp'] = max(0, record['hp'] - max(1, int(damage)))
    result['hp'] = record['hp']
    if record['hp'] > 0:
        return result

    record['has'] = False
    record['ready_at'] = now + regrow_time(kind)
    key, lo, hi = HARVEST_REWARDS[kind]
    count = random.randint(lo, hi)
    player.inventory[key] = player.inventory.get(key, 0) + count
    items = [(key, count)]
    if kind == 'ore' and random.random() < 0.5:
        player.inventory['stone'] = player.inventory.get('stone', 0) + 1
        items.append(('stone', 1))
    result.update(done=True, items=items)
    return result


def tick(dungeon, now=None):
    """Возвращает список восстановившихся клеток."""
    if now is None:
        now = time.time()
    ready = []
    for pos, record in dungeon.harvestables.items():
        if not record.get('has', True) and now >= record.get('ready_at', 0.0):
            record['has'] = True
            record['hp'] = record.get('max_hp', 1)
            ready.append(pos)
    return ready


def serialize(dungeon):
    """Список записей для JSON (координаты внутри записи)."""
    out = []
    for (x, y), rec in dungeon.harvestables.items():
        item = dict(rec)
        item['x'] = x
        item['y'] = y
        out.append(item)
    return out


def deserialize(rows):
    result = {}
    for row in rows or []:
        try:
            x = int(row['x'])
            y = int(row['y'])
            rec = {
                'kind': row.get('kind', 'bush'),
                'hp': int(row.get('hp', 1)),
                'max_hp': int(row.get('max_hp', 1)),
                'has': bool(row.get('has', True)),
                'ready_at': float(row.get('ready_at', 0.0)),
                'spawned_at': float(row.get('spawned_at', 0.0)),
            }
            result[(x, y)] = rec
        except (KeyError, TypeError, ValueError):
            continue
    return result
