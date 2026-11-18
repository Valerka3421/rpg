"""Save manager — сохранение и загрузка игры.

Извлечено из game/game.py для уменьшения размера файла.
Game.save() и Game.load() делегируют сюда.
"""
import json
import os

from core.config import SAVE_VERSION
from core.save import (
    dungeon_cache_from_dict,
    dungeon_cache_to_dict,
    dungeon_from_dict,
    dungeon_to_dict,
    entity_from_dict,
    entity_to_dict,
    player_from_dict,
    player_to_dict,
    save_slot_path,
)


def save_game(game, path=None, slot=None, silent=False):
    """Сохранить игру в JSON."""
    if slot is not None:
        path = save_slot_path(slot)
    elif path is None:
        path = save_slot_path(getattr(game, 'current_slot', 1))
    data = _build_save_dict(game)
    tmp = path + '.tmp'
    with open(tmp, 'w', encoding='utf-8') as f:
        json.dump(data, f, ensure_ascii=False)
    os.replace(tmp, path)
    if not silent:
        from audio import Audio
        Audio.play('save') if hasattr(Audio, 'play') else None


def load_game(game, path=None, slot=None):
    """Загрузить игру из JSON."""
    if slot is not None:
        path = save_slot_path(slot)
    elif path is None:
        path = save_slot_path(getattr(game, 'current_slot', 1))
    if not os.path.exists(path):
        return False
    with open(path, encoding='utf-8') as f:
        data = json.load(f)
    if data.get('version', 0) > SAVE_VERSION:
        return False
    _apply_save_dict(game, data)
    return True


def _build_save_dict(game):
    """Построить dict для сохранения."""
    p = game.player
    d = game.dungeon
    return {
        'version': SAVE_VERSION,
        'player': player_to_dict(p),
        'overworld': dungeon_to_dict(game.overworld),
        'dungeon': dungeon_to_dict(d) if game.world_mode != 'overworld' else None,
        'depth': game.depth,
        'world_mode': game.world_mode,
        'dungeon_cache': dungeon_cache_to_dict(getattr(game, 'dungeon_cache', {})),
        'merchant': entity_to_dict(game.merchant) if game.merchant else None,
        'elder': entity_to_dict(game.elder) if game.elder else None,
        'shop_stock': game.shop_stock,
        'kills': getattr(game, 'kills', 0),
        'boss_kills': getattr(game, 'boss_kills', 0),
        'total_gold_earned': getattr(game, 'total_gold_earned', 0),
        'total_crafts': getattr(game, 'total_crafts', 0),
        'start_time': getattr(game, 'start_time', 0),
        'current_slot': getattr(game, 'current_slot', 1),
        'quest_log': game.quest_log.to_dict() if hasattr(game, 'quest_log') else {},
    }


def _apply_save_dict(game, data):
    """Применить загруженный dict к игре."""
    p_data = data.get('player', {})
    if p_data:
        game.player = player_from_dict(p_data)
    ov_data = data.get('overworld')
    if ov_data:
        game.overworld = dungeon_from_dict(ov_data, is_overworld=True)
    d_data = data.get('dungeon')
    if d_data:
        game.dungeon = dungeon_from_dict(d_data)
    else:
        game.dungeon = game.overworld
    game.depth = data.get('depth', 0)
    game.world_mode = data.get('world_mode', 'overworld')
    cache_data = data.get('dungeon_cache', {})
    game.dungeon_cache = dungeon_cache_from_dict(cache_data)
    m_data = data.get('merchant')
    game.merchant = entity_from_dict(m_data) if m_data else None
    e_data = data.get('elder')
    game.elder = entity_from_dict(e_data) if e_data else None
    game.shop_stock = data.get('shop_stock', [])
    game.kills = data.get('kills', 0)
    game.boss_kills = data.get('boss_kills', 0)
    game.total_gold_earned = data.get('total_gold_earned', 0)
    game.total_crafts = data.get('total_crafts', 0)
    game.start_time = data.get('start_time', 0)
    game.current_slot = data.get('current_slot', 1)
    game.player.recalc()
    game.update_fov()
