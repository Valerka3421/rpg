"""Player registry — управление игроками на сервере.

Извлечено из net/core.py для уменьшения размера файла.
GameCore делегирует сюда операции add/remove/serialize игроков.
"""

from core import rules
from core.config import MAP_H, MAP_W, REGEN_TICK_INTERVAL
from core.data import MONSTERS_BY_CH
from core.entities import Player


def add_player(core, pid, class_key='warrior'):
    """Создать игрока на сервере и разместить его в стартовой комнате."""
    p = Player(class_key=class_key)
    cx, cy = core.dungeon.rooms[0].center() if core.dungeon.rooms \
        else (MAP_W // 2, MAP_H // 2)
    occupied = {(o.x, o.y) for o in core.players.values()}
    spot = rules.find_free_tile(core.dungeon, cx, cy, occupied)
    p.x, p.y = spot if spot else (cx, cy)
    p.anim_time = 0.0
    p.attack_anim = 0.0
    p.facing = (1, 0)
    core.players[pid] = p
    core.move_timers[pid] = 0.0
    core.attack_timers[pid] = 0.0
    core.regen_timers[pid] = REGEN_TICK_INTERVAL
    core._rescale_monsters_for_players()
    core._event(f"Игрок {p.class_name} присоединился. "
                f"Всего игроков: {len(core.players)}.", (240, 210, 90))
    return p


def remove_player(core, pid):
    """Удалить игрока с сервера."""
    core.players.pop(pid, None)
    core.move_timers.pop(pid, None)
    core.attack_timers.pop(pid, None)
    core.regen_timers.pop(pid, None)
    core._inventory_cache.pop(pid, None)
    core._rescale_monsters_for_players()


def player_to_dict(core, pid):
    """Сериализовать игрока для save/restore."""
    p = core.players.get(pid)
    if p is None:
        return {}
    return {
        'x': p.x, 'y': p.y, 'hp': p.hp, 'mana': getattr(p, 'mana', 0),
        'class_key': getattr(p, 'class_key', 'default'),
        'class_tier': getattr(p, 'class_tier', 1),
        'class_base': getattr(p, 'class_base', getattr(p, 'class_key', 'default')),
        'class_changes': getattr(p, 'class_changes', 0),
        'level': p.level, 'xp': p.xp, 'xp_next': p.xp_next,
        'free_points': p.free_points,
        'str': p.str, 'agi': p.agi, 'vit': p.vit, 'luk': p.luk,
        'df_stat': p.df_stat, 'int': getattr(p, 'int_stat', 5),
        'weapon': p.weapon, 'armor': p.armor,
        'head': getattr(p, 'head', None),
        'legs': getattr(p, 'legs', None),
        'boots': getattr(p, 'boots', None),
        'helmet': getattr(p, 'head', None),
        'leggings': getattr(p, 'legs', None),
        'inventory': dict(p.inventory),
        'gold': p.gold,
        'double_strike': p.double_strike,
        'regen': p.regen, 'vampire': p.vampire,
        'thorns': p.thorns, 'greedy': p.greedy,
        'step_counter': p.step_counter,
        'skills': {k: dict(v) if isinstance(v, dict) else {}
                    for k, v in p.skills.items()},
        'skill_points': getattr(p, 'skill_points', 0),
        'skill_xp': dict(getattr(p, 'skill_xp', {})),
        'skill_nodes': list(getattr(p, 'skill_nodes', set())),
        'blueprints': list(getattr(p, 'blueprints', set())),
        'quickbar': list(getattr(p, 'quickbar', [None] * 10)),
    }


def player_from_dict(core, d):
    """Десериализовать игрока из dict."""
    class_key = d.get('class_key', 'default')
    p = Player(class_key=class_key)
    p.x = d['x']
    p.y = d['y']
    p.hp = d['hp']
    p.mana = d.get('mana', 0)
    p.class_tier = d.get('class_tier', 1)
    p.class_base = d.get('class_base', class_key)
    p.class_changes = d.get('class_changes', 0)
    p.level = d['level']
    p.xp = d['xp']
    p.xp_next = d['xp_next']
    p.free_points = d['free_points']
    p.str = d['str']
    p.agi = d['agi']
    p.vit = d['vit']
    p.luk = d['luk']
    p.df_stat = d['df_stat']
    p.int_stat = d.get('int', 5)
    p.weapon = d['weapon']
    p.armor = d['armor']
    p.head = d.get('head', d.get('helmet'))
    p.legs = d.get('legs', d.get('leggings'))
    p.boots = d.get('boots')
    p.inventory = dict(d.get('inventory', {}))
    p.gold = d['gold']
    p.double_strike = d.get('double_strike', False)
    p.regen = d.get('regen', False)
    p.vampire = d.get('vampire', False)
    p.thorns = d.get('thorns', False)
    p.greedy = d.get('greedy', False)
    p.step_counter = d.get('step_counter', 0)
    p.skills = {k: dict(v) for k, v in d.get('skills', {}).items()}
    p.skill_points = d.get('skill_points', 0)
    p.skill_xp = dict(d.get('skill_xp', {}))
    p.skill_nodes = set(d.get('skill_nodes', []))
    p.blueprints = set(d.get('blueprints', []))
    qb = d.get('quickbar', [None] * 10)
    p.quickbar = list(qb) if qb else [None] * 10
    p.recalc()
    return p


def rescale_monsters(core):
    """Масштаб HP/ATK мобов под количество игроков.

    +20% HP и +10% ATK за каждого игрока сверх первого.
    """
    from core.data import scale_stats
    n = max(1, len(core.players))
    hp_scale = 1.0 + (n - 1) * 0.20
    atk_scale = 1.0 + (n - 1) * 0.10
    for m in core.dungeon.monsters:
        base = MONSTERS_BY_CH.get(m.ch, {})
        if not base:
            continue
        depth = max(1, core.depth)
        hp, atk, df = scale_stats(base.get('hp', 10),
                                   base.get('atk', 1),
                                   base.get('df', 0), depth)
        m.max_hp = max(1, int(hp * hp_scale))
        m.hp = m.max_hp if m.hp >= m.max_hp - 1 else min(m.hp, m.max_hp)
        m.atk = max(1, int(atk * atk_scale))
        m.df = df
