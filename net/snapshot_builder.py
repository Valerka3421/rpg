"""Snapshot builder — построение снапшотов состояния сервера.

Извлечено из net/core.py:GameCore.get_snapshot для уменьшения размера файла.
GameCore делегирует сюда построение dict-снапшота для отправки клиентам.
"""


def build_snapshot(core):
    """Построить полный снапшот состояния для рассылки клиентам.

    Возвращает dict с игроками, монстрами, предметами, событиями и т.д.
    """
    players_data = {}
    for pid, p in core.players.items():
        data = {
            'x': p.x, 'y': p.y,
            'hp': p.hp, 'max_hp': p.max_hp,
            'mana': getattr(p, 'mana', 0),
            'max_mana': getattr(p, 'max_mana', 0),
            'class': p.class_key,
            'class_tier': getattr(p, 'class_tier', 1),
            'level': p.level,
            'xp': p.xp, 'xp_next': p.xp_next,
            'name': p.class_name,
            'anim_time': round(p.anim_time, 2),
            'attack_anim': round(p.attack_anim, 2),
            'facing': list(p.facing),
            'gold': p.gold,
            'atk': p.atk, 'df': p.df,
            'crit': round(p.crit, 2), 'dodge': round(p.dodge, 2),
            'free_points': p.free_points,
            'skill_points': getattr(p, 'skill_points', 0),
            'statuses': dict(p.statuses),
            'str': p.str, 'agi': p.agi, 'vit': p.vit,
            'luk': p.luk, 'df_stat': p.df_stat,
            'int': getattr(p, 'int_stat', 5),
            'weapon': p.weapon, 'armor': p.armor,
            'head': p.head, 'legs': p.legs, 'boots': p.boots,
            'helmet': p.head, 'leggings': p.legs,
            'skills': {k: dict(v) for k, v in p.skills.items()},
        }
        inventory = dict(p.inventory)
        if core._inventory_cache.get(pid) != inventory:
            data['inventory'] = inventory
            core._inventory_cache[pid] = inventory
        players_data[str(pid)] = data

    alive = [m for m in core.dungeon.monsters if m.hp > 0]
    alive_ids = {id(m) for m in alive}
    core._monster_ids = {k: v for k, v in core._monster_ids.items()
                         if k in alive_ids}
    monsters_data = [{
        'mid': core._monster_id(m),
        'x': m.x, 'y': m.y, 'hp': m.hp, 'max_hp': m.max_hp,
        'ch': m.ch, 'color': list(m.color), 'boss': m.boss,
        'statuses': dict(m.statuses),
    } for m in alive]

    items_data = [{'x': k[0], 'y': k[1], 'key': v}
                  for k, v in core.dungeon.items.items()]

    harvest = core._harvest_snapshot()
    snapshot = {
        'tick': core.tick,
        'world_mode': core.world_mode,
        'depth': core.depth,
        'players': players_data,
        'monsters': monsters_data,
        'items': items_data,
        'merchant': {'x': core.merchant.x, 'y': core.merchant.y}
                    if core.merchant else None,
        'elder': {'x': core.elder.x, 'y': core.elder.y}
                 if core.elder else None,
        'shop_stock': core.shop_stock,
        'events': core._take_events(),
        'quests': core.quest_log.to_dict(),
        'trade': core._trade_snapshot(),
    }
    if harvest is not None:
        snapshot['harvestables'] = harvest
    return snapshot
