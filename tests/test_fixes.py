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


def test_quest_ui_passes_inventory_on_accept(make_game):
    """Кнопка «взять квест» тоже должна учитывать уже собранные кости."""
    from game.quest_controller import QuestController
    g = make_game()
    g.player.inventory = {'bone': 3}
    g.dialogue.open('elder')
    quests = QuestController(g)
    idx = next(i for i, (key, state) in enumerate(quests.visible_quests())
               if key == 'q_bones' and state == 'available')
    quests.activate(idx)
    assert 'q_bones' in g.quest_log.completed


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


def test_transition_sets_move_cooldown(make_game):
    """После перехода удержание направления не должно сразу вернуть назад."""
    g = make_game()
    g.enter_dungeon()
    assert _step_onto(g, Tile.EXIT) is True
    assert g.world_mode == 'overworld'
    assert g.player_move_timer > 0


# ════════════════════ 4. Мультиплеер ════════════════════
def test_mp_rejects_teleport_step():
    """Клиент не может одним пакетом переместиться на несколько клеток."""
    from net.core import GameCore
    core = GameCore(seed=11)
    core.add_player(0, 'warrior')
    p = core.players[0]
    x, y = p.x, p.y
    for i in range(1, 6):
        core.dungeon.tiles[y][x + i] = Tile.FLOOR
    core.apply_input(0, {'type': 'move', 'dx': 10, 'dy': 0})
    assert (p.x, p.y) == (x, y)
    assert core.move_player(0, 2, 0) is False


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


def test_shop_digit_keys_do_nothing(make_game, renderer):
    g = _shop_game(make_game(), renderer)
    g.handle_key(pygame.K_2)                  # цифры больше не покупают
    assert g.player.inventory == {}
    assert g.player.gold == 100


def test_shop_keyboard_sells_via_mode_only(make_game, renderer):
    g = _shop_game(make_game(), renderer)
    g.player.inventory = {'potion_s': 1}
    g.handle_key(pygame.K_s)                  # режим продажи
    assert g.shop_submode == 'sell'
    g.handle_key(pygame.K_1)                  # цифра не продаёт
    assert 'potion_s' in g.player.inventory


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
    view.world_mode = 'dungeon'
    view.screen_mode = 'game'
    view.merchant = Entity(view.player.x + 1, view.player.y, 1, 1, 0, 0,
                           "Торговец", "M", (240, 200, 100), 0)
    view.shop_stock = [{'key': 'potion_s', 'price': 5, 'sold': False}]
    view.handle_key(pygame.K_t)
    assert view.screen_mode == 'dialogue'      # сначала диалог с NPC
    assert view.dialogue.npc_type == 'merchant'
    view.handle_key(pygame.K_RETURN)           # «Торговля»
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
    view.player.inventory = {'potion_s': 1}
    renderer.draw(view)
    _, sell_rect = view._ui_hits['shop']['rows'][0]
    view.handle_mouse_click((sell_rect[0] + 3, sell_rect[1] + 3))
    assert sent[-1] == {'type': 'sell', 'idx': 0}


# ════════════════════ 8б. Мультиплеер: диалоги с NPC ════════════════════
def _mp_elder_view():
    from net.view import ClientView
    view = ClientView()
    view.world_mode = 'overworld'
    view.elder = Entity(view.player.x + 1, view.player.y, 1, 1, 0, 0,
                        "Старейшина", "E", (240, 210, 100), 0)
    view.elder.is_npc = True
    view.elder.npc_type = 'elder'
    return view


def test_mp_elder_dialogue_opens_and_draws(renderer):
    view = _mp_elder_view()
    view.handle_key(pygame.K_t)
    assert view.screen_mode == 'dialogue'
    assert view.dialogue.npc_type == 'elder'
    # квестов в MP нет — остаются «поговорить» и «уйти»
    assert [opt[0] for opt in view.dialogue.options] == ['talk', 'close']
    renderer.draw(view)                       # рисуется без ошибок
    assert view._modal_close_rect is not None


def test_mp_elder_dialogue_talks_and_closes(renderer):
    view = _mp_elder_view()
    view.handle_key(pygame.K_t)
    first = view.dialogue.current_line
    view.handle_key(pygame.K_RETURN)          # «Поговорить о мире»
    assert view.screen_mode == 'dialogue'
    assert view.dialogue.current_line != first
    view.handle_key(pygame.K_DOWN)            # «Уйти»
    view.handle_key(pygame.K_RETURN)
    assert view.screen_mode == 'game'
    view.handle_key(pygame.K_t)
    view.handle_key(pygame.K_ESCAPE)
    assert view.screen_mode == 'game'


def test_mp_dialogue_mouse_click_closes(renderer):
    view = _mp_elder_view()
    view.handle_key(pygame.K_t)
    renderer.draw(view)
    _, rect = view.dialogue.button_rects()[-1]   # «Уйти»
    view.handle_mouse_click((rect[0] + 5, rect[1] + 5))
    assert view.screen_mode == 'game'


def test_mp_no_npc_keeps_game_mode():
    from net.view import ClientView
    view = ClientView()
    view.handle_key(pygame.K_t)
    assert view.screen_mode == 'game'


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



# ════════════════════ 9. Поле по центру при смене разрешения ════════════════════
def test_view_origin_centers_field_above_hud():
    from core import config

    original = (config.SCREEN_W, config.SCREEN_H)
    try:
        for width, height in config.RESOLUTIONS:
            config.set_resolution(width, height)
            ox, oy = config.view_origin()
            field_w = config.VIEW_W * config.TILE
            field_h = config.VIEW_H * config.TILE
            # поле не вылезает за экран и за пределы области над HUD
            assert ox >= 0 and oy >= 0
            assert ox + field_w <= width
            assert oy + field_h <= height - config.HUD_HEIGHT
            # остаток пикселей делится на два края, а не уходит в один
            assert abs(ox - (width - field_w - ox)) <= 1
            assert abs(oy - (height - config.HUD_HEIGHT - field_h - oy)) <= 1
    finally:
        config.set_resolution(*original)


def test_handle_key_accepts_event_unicode_and_mods(make_game):
    g = make_game()
    g.screen_mode = 'menu_mp_join'
    g.menu.mp_ip = ''

    # русская раскладка присылает unicode '.', а не K_PERIOD
    g.handle_key(pygame.K_UNKNOWN, 0, '.', 0)
    assert g.menu.mp_ip == '.'

    # запасной путь по коду клавиши (нумпад и основная точка)
    g.handle_key(pygame.K_KP_PERIOD)
    g.handle_key(pygame.K_PERIOD)
    assert g.menu.mp_ip == '...'

    # цифры, дефис и двоеточие тоже набираются
    g.handle_key(pygame.K_1, 0, '1', 0)
    g.handle_key(pygame.K_MINUS, 0, '-', 0)
    g.handle_key(pygame.K_COLON, 0, ':', 0)
    assert g.menu.mp_ip == '...1-:'


def test_join_page_ignores_keys_without_handler(make_game):
    g = make_game()
    g.screen_mode = 'menu_mp_join'
    g.menu.mp_ip = 'host'
    for key in (0, pygame.K_F1, pygame.K_INSERT, pygame.K_HOME,
                pygame.K_KP_PLUS, pygame.K_RCTRL):
        g.handle_key(key)                      # не должно быть исключений
    assert g.menu.mp_ip == 'host'


def test_join_page_ctrl_v_pastes_and_sanitizes(make_game, monkeypatch):
    import game.menu_controller as menu_mod

    g = make_game()
    g.screen_mode = 'menu_mp_join'
    g.menu.mp_ip = ''

    monkeypatch.setattr(menu_mod, 'read_clipboard_text',
                        lambda: '  192.168.0.10\r\n')
    g.handle_key(pygame.K_v, 0, 'v', pygame.KMOD_CTRL)
    assert g.menu.mp_ip == '192.168.0.10'

    # нелатинская раскладка: другой keycode, но тот же Ctrl+V
    g.menu.mp_ip = ''
    g.handle_key(pygame.K_m, 0, '\x16', pygame.KMOD_CTRL)
    assert g.menu.mp_ip == '192.168.0.10'

    monkeypatch.setattr(menu_mod, 'read_clipboard_text', lambda: '')
    before = g.menu.mp_ip
    g.handle_key(pygame.K_v, 0, 'v', pygame.KMOD_CTRL)
    assert g.menu.mp_ip == before              # пустой буфер ничего не ломает

    # лишние символы отбрасываются, длина ограничена 40
    monkeypatch.setattr(menu_mod, 'read_clipboard_text',
                        lambda: 'x' * 100 + '!?/')
    g.menu.mp_ip = ''
    g.handle_key(pygame.K_v, 0, 'v', pygame.KMOD_CTRL)
    assert g.menu.mp_ip == 'x' * 40


def test_settings_file_lives_in_writable_data_dir():
    from core import config
    from core import settings

    assert settings.SETTINGS_FILE == os.path.join(config.DATA_DIR,
                                                  'settings.json')
    assert os.path.isdir(config.DATA_DIR)


def test_writable_data_dir_is_exe_dir_when_frozen(tmp_path, monkeypatch):
    from core import config

    fake_exe = tmp_path / 'Ethelgard.exe'
    fake_exe.write_text('exe', encoding='utf-8')
    monkeypatch.delenv('ETHELGARD_DATA_DIR', raising=False)
    monkeypatch.setattr(config.sys, 'frozen', True, raising=False)
    monkeypatch.setattr(config.sys, 'executable', str(fake_exe))

    assert config._writable_data_dir() == str(tmp_path)
    assert not (tmp_path / '.write_test').exists()   # пробный файл удалён


def test_writable_data_dir_env_override(tmp_path, monkeypatch):
    from core import config

    target = tmp_path / 'portable-data'
    monkeypatch.setenv('ETHELGARD_DATA_DIR', str(target))

    assert config._writable_data_dir() == os.path.abspath(str(target))
    assert target.is_dir()


# ════════════════════ 10. Мультиплеер: FPS и полный экран ════════════════════
def test_mp_settings_has_fullscreen_toggle():
    from net.client import SettingsMenu

    settings = {'volume': 7, 'show_fps': False, 'show_minimap': True,
                'fullscreen': False}
    saved = []
    menu = SettingsMenu(1280, 720, settings, saved.append)

    labels = [label for label, _ in menu.rows]
    assert 'Показать FPS' in labels
    assert 'Полный экран' in labels

    for _ in range(labels.index('Полный экран')):
        menu.handle_key(pygame.K_DOWN)
    menu.handle_key(pygame.K_RETURN)
    assert settings['fullscreen'] is True
    assert menu.display_changed is True
    assert saved and saved[-1]['fullscreen'] is True
    assert dict(menu.rows)['Полный экран'] == 'Вкл'

    menu.handle_key(pygame.K_RETURN)
    assert settings['fullscreen'] is False
    assert dict(menu.rows)['Полный экран'] == 'Выкл'


# ════════════════════ 11. Инвентарь-сетка ════════════════════
def _grid_inventory_game(g, renderer):
    from core.config import INVENTORY_PAGE_SIZE
    g.renderer = renderer
    g.player.inventory = {
        # 3 зелья
        'potion_s': 2, 'potion_m': 1, 'potion_l': 1,
        # 6 оружий
        'dagger': 1, 'shortsword': 1, 'sword': 1,
        'axe': 1, 'greatsword': 1, 'warhammer': 1,
        # 6 броней
        'padded': 1, 'leather': 1, 'chain': 1,
        'scale': 1, 'plate': 1, 'mithril': 1,
        # 6 ресурсов/материалов
        'bone': 5, 'wood': 3, 'stone': 2, 'ore': 1,
        'mushroom': 2, 'berry': 1,
        # 5 книг
        'book_str': 1, 'book_agi': 1, 'book_vit': 1,
        'book_luk': 1, 'book_def': 1,
        # 3 свитка
        'scroll_heal': 1, 'scroll_fireball': 1, 'scroll_freeze': 1,
        # 1 шлем
        'leather_cap': 1,
    }
    assert len(g.player.inventory) == 30
    assert INVENTORY_PAGE_SIZE == 25
    assert g.inv_pages_count() == 2      # 25 + 5
    g.inv_page = 0
    g.mouse_pos = (-1, -1)
    g.screen_mode = 'inventory'
    g.draw()
    return g


def test_inventory_items_form_grid(make_game, renderer):
    g = _grid_inventory_game(make_game(), renderer)
    hits = g._ui_hits['inventory']
    items = hits['items']
    assert len(items) == 25
    rects = [rect for _, rect in items]
    # первый ряд — пять ячеек на одной высоте, разные по X
    assert len({r[1] for r in rects[:5]}) == 1
    assert len({r[0] for r in rects[:5]}) == 5
    # второй ряд ниже первого
    assert rects[5][1] > rects[0][1]
    # всего две страницы
    assert len(hits['pages']) == 2


def test_inventory_page_button_turns_page(make_game, renderer):
    g = _grid_inventory_game(make_game(), renderer)
    assert g.inv_pages_count() == 2
    pages = dict(g._ui_hits['inventory']['pages'])
    x, y, w, h = pages['next']
    assert g.handle_mouse_click((x + w // 2, y + h // 2)) is True
    assert g.inv_page == 1
    # на второй странице — оставшиеся 5 предметов (30 - 25)
    g.draw()
    assert len(g._ui_hits['inventory']['items']) == 5
    pages = dict(g._ui_hits['inventory']['pages'])
    x, y, w, h = pages['prev']
    g.handle_mouse_click((x + w // 2, y + h // 2))
    assert g.inv_page == 0


def test_inventory_hover_changes_slot_background(make_game, renderer):
    g = _grid_inventory_game(make_game(), renderer)
    rect = g._ui_hits['inventory']['items'][0][1]
    normal = tuple(renderer.screen.get_at((rect[0] + 6, rect[1] + 6))[:3])
    g.handle_mouse_motion((rect[0] + rect[2] // 2, rect[1] + rect[3] // 2))
    g.draw()
    hover = tuple(renderer.screen.get_at((rect[0] + 6, rect[1] + 6))[:3])
    assert hover != normal


def test_mp_inventory_grid_has_pages(renderer):
    from net.view import ClientView
    view = ClientView()
    view.player.inventory = {
        'potion_s': 2, 'potion_m': 1, 'potion_l': 1,
        'dagger': 1, 'shortsword': 1, 'sword': 1,
        'axe': 1, 'greatsword': 1, 'warhammer': 1,
        'padded': 1, 'leather': 1, 'chain': 1,
        'scale': 1, 'plate': 1, 'mithril': 1,
        'bone': 5, 'wood': 3, 'stone': 2, 'ore': 1,
        'mushroom': 2, 'berry': 1,
        'book_str': 1, 'book_agi': 1, 'book_vit': 1,
        'book_luk': 1, 'book_def': 1,
        'scroll_heal': 1, 'scroll_fireball': 1, 'scroll_freeze': 1,
        'leather_cap': 1,
    }
    view.screen_mode = 'inventory'
    view.inv_page = 0
    renderer.draw(view)
    assert len(view._ui_hits['inventory']['items']) == 25

    next_rect = dict(view._ui_hits['inventory']['pages'])['next']
    x, y, w, h = next_rect
    view.handle_mouse_click((x + w // 2, y + h // 2))
    assert view.inv_page == 1

    # на второй странице — 5
    renderer.draw(view)
    assert len(view._ui_hits['inventory']['items']) == 5

    view.handle_key(pygame.K_q)
    assert view.inv_page == 0


# ════════════════════ 12. Иконка окна ════════════════════
def test_window_icon_is_loaded(screen):
    from render.window_icon import ICON_PATH, apply_window_icon
    assert os.path.isfile(ICON_PATH)
    assert apply_window_icon() is True



def test_equip_head_slot(make_game):
    g = make_game()
    g.player.level = 5
    base_def = g.player.df
    g.player.inventory = {'leather_cap': 1}
    g.use_item('leather_cap')
    assert g.player.head == 'leather_cap'
    assert g.player.df == base_def + 1

def test_equip_legs_slot_with_dodge(make_game):
    g = make_game()
    g.player.level = 5
    base_dodge = g.player.dodge
    g.player.inventory = {'leather_boots': 1}
    g.use_item('leather_boots')
    assert g.player.legs == 'leather_boots'
    assert g.player.dodge > base_dodge

def test_unequip_head_and_legs(make_game):
    g = make_game()
    g.player.head = 'leather_cap'; g.player.legs = 'cloth_pants'
    g.player.recalc()
    g.player.inventory = {}
    g.unequip_head()
    g.unequip_legs()
    assert g.player.head is None and g.player.legs is None
    assert g.player.inventory.get('leather_cap') == 1
    assert g.player.inventory.get('cloth_pants') == 1

def test_exit_dungeon_spawns_near_entrance(make_game):
    from core import overworld as ov
    g = make_game()
    g.enter_dungeon()
    g.exit_to_overworld()
    ex, ey = ov.DUNGEON_POS
    assert max(abs(g.player.x - ex), abs(g.player.y - ey)) <= 3
