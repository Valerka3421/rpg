"""Квесты и доска поручений."""
from __future__ import annotations

from typing import Any

import random
from dataclasses import dataclass

from .items import (
    ITEMS,
    item_display_color,
    item_display_name,
    item_sell_price,
    make_loot_key,
    parse_item_key,
)
from .monsters import BOSSES, MONSTER_DROPS, MONSTERS

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


def quests_by_giver(giver: Any) -> Any: 
    """Список ключей квестов, которые выдаёт NPC."""
    return [k for k, q in QUESTS.items() if q.get('giver') == giver]


# ════════════════════ ITEM INSTANCE ════════════════════


@dataclass(frozen=True)
class ItemInstance:
    """Типизированное представление предмета с редкостью.

    Ведёт себя как строка через .key() — для совместимости с dict
    и старым кодом. Можно использовать вместо 'sword+2':
        ItemInstance('sword', 2) == ItemInstance.from_key('sword+2')
    """
    base: str
    rarity: int = 0

    def key(self: Any) -> Any: 
        return make_loot_key(self.base, self.rarity)

    def __str__(self: Any) -> Any: 
        return self.key()

    def data(self: Any) -> Any: 
        return ITEMS.get(self.base, {})

    def display_name(self: Any) -> Any: 
        return item_display_name(self.key())

    def display_color(self: Any) -> Any: 
        return item_display_color(self.key())

    def sell_price(self: Any) -> Any: 
        return item_sell_price(self.key())

    @classmethod
    def from_key(cls: Any, full_key: Any) -> Any: 
        base, rarity = parse_item_key(full_key)
        return cls(base or '', rarity)


# ════════════════════ ДОСКА ПОРУЧЕНИЙ ════════════════════
BOARD_BOSS_FLOORS = {5: 'G', 10: 'L', 15: 'V', 20: 'A'}


def _board_monsters_for(dmin: Any, dmax: Any) -> Any: 
    res = [k for k, m in MONSTERS.items()
           if dmin <= m['min_depth'] <= dmax
           and not m.get('overworld_only')]
    if not res and dmax > 0:
        res = [k for k, m in MONSTERS.items()
               if m['min_depth'] <= dmax and not m.get('overworld_only')]
    return res


def _board_collect_targets(dmin: Any, dmax: Any) -> Any: 
    chars = {MONSTERS[k]['ch'] for k in _board_monsters_for(dmin, dmax)
             if k in MONSTERS}
    targets = set()
    for ch in chars:
        for item_key, _ in MONSTER_DROPS.get(ch, []):
            base = item_key.split('+')[0]
            if ITEMS.get(base, {}).get('type') == 'material':
                targets.add(base)
    return sorted(targets)


def _make_board_quest(dmin: Any, dmax: Any, seed: Any) -> Any: 
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


def generate_board_quests(dmin: Any, dmax: Any, count: Any = 3, base_seed: Any = 0) -> Any: 
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
