"""Регрессионные тесты на исправленные баги.

Каждый тест соответствует конкретной проблеме из разбора: загрузка сейва
теряла карту, квест «Первый шаг» не завершался, из подземелья нельзя было
подняться, магазин не реагировал на мышь/клавиатуру и т.д.
"""
import json
import os

import pygame

from core.config import Tile, SAVE_VERSION
from core.data import xp_to_next
from core.entities import Entity
from core.save import dungeon_to_dict, dungeon_from_dict


# ════════════════════ 1. Загрузка сейва ════════════════════
def test_load_in_dungeon_restores_map(make_game, tmp_path):
    g = make_game()
    g.enter_dungeon()
    g.new_dungeon(4, autosave=False)
    floor = g.dungeon
    stairs = floor.stairs_pos
    monsters = len(floor.monsters)
    g.player.gold = 321
    path = str(tmp_path / 'save.json')
    g.save(path=path)

    other = make_game()
    assert other.load(path=path) is True
    assert other.world_mode == 'dungeon' and other.depth == 4
    assert other.dungeon is not other.overworld
    assert other.dungeon.stairs_pos == stairs
    assert len(other.dungeon.monsters) == monsters
    assert other.dungeon_cache[4]['dungeon'] is other.dungeon
    assert other.player.gold == 321


def test_load_overworld_keeps_dungeon_identity(make_game, tmp_path):
    g = make_game()
    g.player.gold = 55
    path = str(tmp_path / 'save.json')
    g.save(path=path)
    other = make_game()
    other.load(path=path)
    assert other.dungeon is other.overworld
    assert other.player.gold == 55


def test_save_format_is_compact_and_versioned(make_game, tmp_path):
    g = make_game()
    g.enter_dungeon()
    g.new_dungeon(3, autosave=False)
    path = str(tmp_path / 'save.json')
    g.save(path=path)
    with open(path, encoding='utf-8') as f:
        data = json.load(f)
    assert data['version'] == SAVE_VERSION
    # тайлы — строки цифр, а не вложенные списки
    assert isinstance(data['overworld']['tiles'][0], str)
    # текущий этаж не дублируется в кэше
    assert str(g.depth) not in data['dungeon_cache']
    # и это заметно компактнее старого формата
    assert os.path.getsize(path) < 200 * 1024


def test_load_rejects_newer_save(make_game, tmp_path):
    g = make_game()
    path = str(tmp_path / 'save.json')
    g.save(path=path)
    with open(path, encoding='utf-8') as f:
        data = json.load(f)
    data['version'] = SAVE_VERSION + 99
    with open(path, 'w', encoding='utf-8') as f:
        json.dump(data, f, ensure_ascii=False)
    other = make_game()
    assert other.load(path=path) is False


def test_legacy_tiles_format_still_loads():
    """Старые сейвы хранили тайлы вложенными списками."""
    from core.config import MAP_W, MAP_H
    from core.entities import Dungeon
    d = Dungeon(MAP_W, MAP_H)
    d.tiles[0][0] = Tile.FLOOR
    d.rooms = []
    payload = dungeon_to_dict(d)
    payload['tiles'] = [[int(t) for t in row] for row in d.tiles]
    restored = dungeon_from_dict(payload)
    assert restored.tiles[0][0] == Tile.FLOOR
    assert restored.tiles[1][1] == Tile.WALL


def test_save_migrates_old_xp_curve(make_game, tmp_path):
    """Старый сейв с xp_next = 10 * 1.6**n не должен блокировать уровни."""
    g = make_game()
    path = str(tmp_path / 'save.json')
    g.save(path=path)
    with open(path, encoding='utf-8') as f:
        data = json.load(f)
    data['player']['level'] = 10
    data['player']['xp'] = 0
    data['player']['xp_next'] = 10 * int(1.6 ** 9)
    with open(path, 'w', encoding='utf-8') as f:
        json.dump(data, f, ensure_ascii=False)
    other = make_game()
    assert other.load(path=path) is True
    assert other.player.xp_next == xp_to_next(10)


# ════════════════════ 2. Квесты ════════════════════
def test_quest_reach_completes_on_descent(make_game):
    g = make_game()
    g.dialogue.open('elder')
    g.quest_log.accept('q_first_step', 1, g.player.inventory)
    g.enter_dungeon()
    for depth in (2, 3, 4, 5):
        g.new_dungeon(depth, autosave=False)
    assert 'q_first_step' in g.quest_log.completed


def test_collect_quest_counts_existing_items(make_game):
    g = make_game()
    g.player.inventory = {'bone': 3}
    g.dialogue.open('elder')
    g.quest_log.accept('q_bones', 3, g.player.inventory)
    assert 'q_bones' in g.quest_log.completed
    assert g.quest_log.turn_in('q_bones', g.player, g) is True
    assert 'bone' not in g.player.inventory


# ════════════════════ 3. Лестницы ════════════════════
def test_depth_1_has_exit_and_deeper_has_upstairs(make_game):
    g = make_game()
    g.enter_dungeon()
    assert any(t == Tile.EXIT for row in g.dungeon.tiles for t in row)
    for depth in (2, 3):
        g.new_dungeon(depth, autosave=False)
        assert any(t == Tile.UPSTAIRS for row in g.dungeon.tiles for t in row)
        assert not any(t == Tile.EXIT for row in g.dungeon.tiles for t in row)


def _step_onto(g, tile_type):
    """Найти клетку с тайлом и шагнуть на неё с соседнего пола."""
    for y, row in enumerate(g.dungeon.tiles):
        for x, tile in enumerate(row):
            if tile != tile_type:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                px, py = x + dx, y + dy
                if g.dungeon.tiles[py][px] == Tile.FLOOR:
                    g.player.x, g.player.y = px, py
                    g.update_fov()
                    g.move_player(-dx, -dy)
                    return True
    return False


def test_upstairs_returns_one_floor_up(make_game):
    g = make_game()
    g.enter_dungeon()
    g.new_dungeon(3, autosave=False)
    assert _step_onto(g, Tile.UPSTAIRS) is True
    assert g.depth == 2
    assert (g.player.x, g.player.y) == g.dungeon.stairs_pos


def test_upstairs_from_depth_1_goes_outside(make_game):
    g = make_game()
    g.enter_dungeon()
    assert _step_onto(g, Tile.EXIT) is True
    assert g.world_mode == 'overworld'
    assert g.dungeon is g.overworld


def test_overworld_memory_survives_deep_descent(make_game):
    g = make_game()
    g.explored.add((7, 7))
    g.enter_dungeon()
    g.new_dungeon(5, autosave=False)
    g.exit_to_overworld()
    assert (7, 7) in g.explored


# ════════════════════ 4. Магазин ════════════════════
def _shop_game(g, renderer):
    g.renderer = renderer
    g.depth = 3
    g.shop_stock = [{'key': 'potion_s', 'price': 5, 'sold': False},
                    {'key': 'potion_m', 'price': 10, 'sold': False}]
    g.shop.page = 0
    g.shop_submode = 'buy'
    g.player.gold = 100
    g.screen_mode = 'shop'
    g.player.inventory = {}
    g.draw()                       # регистрирует кликабельные строки
    return g


def test_shop_renders_clickable_rows(make_game, renderer):
    g = _shop_game(make_game(), renderer)
    rows = g._ui_hits.get('shop', {}).get('rows', [])
    assert len(rows) == 2


def test_shop_mouse_click_buys(make_game, renderer):
    g = _shop_game(make_game(), renderer)
    row_idx, rect = g._ui_hits['shop']['rows'][0]
    assert g.handle_mouse_click((rect[0] + 4, rect[1] + 4)) is True
    assert g.player.inventory.get('potion_s') == 1
    assert g.player.gold == 95
    assert g.shop_stock[0]['sold'] is True


def test_shop_keyboard_buys(make_game, renderer):
    g = _shop_game(make_game(), renderer)
    g.handle_key(pygame.K_2)
    assert g.player.inventory.get('potion_m') == 1
    assert g.player.gold == 90


def test_shop_keyboard_sells(make_game, renderer):
    g = _shop_game(make_game(), renderer)
    g.player.inventory = {'potion_s': 1}
    g.handle_key(pygame.K_s)                  # режим продажи
    assert g.shop_submode == 'sell'
    g.handle_key(pygame.K_1)
    assert 'potion_s' not in g.player.inventory
    assert g.player.gold == 102


def test_shop_mouse_sells(make_game, renderer):
    g = _shop_game(make_game(), renderer)
    g.player.inventory = {'potion_s': 1}
    g.shop_submode = 'sell'
    g.draw()
    row_idx, rect = g._ui_hits['shop']['rows'][0]
    g.handle_mouse_click((rect[0] + 4, rect[1] + 4))
    assert 'potion_s' not in g.player.inventory
    assert g.player.gold > 100


# ════════════════════ 5. Клавиатурное управление ════════════════════
def test_inventory_digit_uses_item(make_game):
    g = make_game()
    g.player.hp = 10
    g.player.inventory = {'potion_s': 1}
    g.screen_mode = 'inventory'
    g.handle_key(pygame.K_1)
    assert g.player.hp == 20


def test_stats_digit_allocates(make_game):
    g = make_game()
    g.player.free_points = 5
    g.screen_mode = 'stats'
    base = g.player.str
    g.handle_key(pygame.K_1)                  # STR
    assert g.player.str == base + 1
    assert g.player.free_points == 4


def test_craft_digit_creates(make_game):
    g = make_game()
    g.player.level = 5
    g.player.inventory = {'bone': 2, 'wood': 1}
    g.screen_mode = 'craft'
    idx = next(i for i, c in enumerate(g.craftable_list()) if c[0] == 'bone_sword')
    g.handle_key(getattr(pygame, f'K_{idx + 1}'))
    assert g.player.inventory.get('bone_sword') == 1


# ════════════════════ 6. Бой и баланс ════════════════════
def test_monsters_have_individual_timers(make_game):
    g = make_game()
    g.enter_dungeon()
    g.dungeon.monsters = [
        Entity(g.player.x + 2, g.player.y, 10, 10, 1, 0, "A", "g",
               (1, 2, 3), 1),
        Entity(g.player.x + 3, g.player.y, 10, 10, 1, 0, "B", "g",
               (1, 2, 3), 1),
    ]
    from core import rules
    rules.init_action_timers(g.dungeon.monsters)
    timers = {round(m.act_timer, 4) for m in g.dungeon.monsters}
    assert len(timers) == 2


def test_monster_timers_decrement_over_time(make_game):
    g = make_game()
    g.enter_dungeon()
    m = Entity(g.player.x + 2, g.player.y, 10, 10, 1, 0, "A", "g", (1, 2, 3), 1)
    from core import rules
    g.dungeon.monsters = [m]
    rules.init_action_timers(g.dungeon.monsters)
    before = m.act_timer
    g.update(0.05)
    assert m.act_timer < before


def test_boss_breath_damages_player(make_game):
    g = make_game()
    g.enter_dungeon()
    boss = Entity(g.player.x + 3, g.player.y, 500, 500, 20, 0,
                  "Босс", "G", (255, 0, 0), 100, boss=True)
    boss.breath_timer = 0.0
    g.dungeon.monsters = [boss]
    hp = g.player.hp
    g.monsters_update(0.1)
    assert g.player.hp < hp


def test_boss_scaling_is_gentler_than_monsters():
    from core.data import scale_stats, scale_boss_stats
    m_hp, _, _ = scale_stats(100, 10, 5, 20)
    b_hp, _, _ = scale_boss_stats(100, 10, 5, 20)
    assert b_hp < m_hp


# ════════════════════ 7. Звук ════════════════════
def test_audio_uses_assets_dir():
    from audio import Audio
    from core.config import ASSETS_DIR
    assert ASSETS_DIR.endswith('assets')
    # путь к WAV берётся из ASSETS_DIR, а не из соседнего каталога
    assert os.path.isdir(os.path.join(ASSETS_DIR, 'sounds'))
    assert Audio._rate > 0 and Audio._channels >= 1


# ════════════════════ 8. Кооператив: магазин и лестницы ════════════════════
def test_mp_client_shop_opens_and_draws(renderer):
    from net.view import ClientView
    view = ClientView()
    view.player.gold = 100
    view.depth = 3
    view.screen_mode = 'game'
    view.merchant = Entity(view.player.x + 1, view.player.y, 1, 1, 0, 0,
                           "Торговец", "M", (240, 200, 100), 0)
    view.shop_stock = [{'key': 'potion_s', 'price': 5, 'sold': False}]
    view.handle_key(pygame.K_t)
    assert view.screen_mode == 'shop'
    renderer.draw(view)
    rows = view._ui_hits['shop']['rows']
    assert len(rows) == 1
    sent = []
    view.send_fn = sent.append
    _, rect = rows[0]
    view.handle_mouse_click((rect[0] + 3, rect[1] + 3))
    assert sent == [{'type': 'buy', 'idx': 0}]
    view.handle_key(pygame.K_s)
    view.handle_key(pygame.K_1)
    assert sent[-1] == {'type': 'sell', 'idx': 0}


def test_mp_view_snapshot_updates_in_place(renderer):
    from net.view import ClientView
    from net.core import GameCore
    core = GameCore(seed=2)
    core.add_player(0, 'warrior')
    view = ClientView()
    snap = core.get_snapshot()
    view.update_from_snapshot(snap, core.get_tiles(), 0)
    player_id = id(view.player)
    view.update_from_snapshot(core.get_snapshot(), None, 0)
    assert id(view.player) == player_id          # объект переиспользуется
    assert view.screen_mode == 'game'
    renderer.draw(view)                          # рисуется без ошибок

