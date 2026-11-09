"""Тесты кэшей рендера, мини-карты и клиентской части MP."""
import time

import pygame
import pytest

from core import config
from core.config import MAP_W, MAP_H, MINIMAP_SCALE, MINIMAP_MARGIN, Tile
from core.entities import Player, Entity
from core.data import CLASSES
from net.view import ClientView
from render.theme import Theme


# ── Текст ──
def test_theme_render_is_cached(renderer):
    surf1 = Theme.render("Тест", (255, 255, 255), Theme.FONT_BODY)
    surf2 = Theme.render("Тест", (255, 255, 255), Theme.FONT_BODY)
    assert surf1 is surf2
    surf3 = Theme.render("Тест", (255, 0, 0), Theme.FONT_BODY)
    assert surf3 is not surf1


def test_theme_text_cache_is_bounded(renderer):
    Theme.clear_text_cache()
    for i in range(Theme.TEXT_CACHE_MAX + 100):
        Theme.render(f"строка {i}", (255, 255, 255), Theme.FONT_SMALL)
    assert len(Theme._text_cache) <= Theme.TEXT_CACHE_MAX


def test_theme_text_returns_width(renderer):
    width = Theme.text(renderer.screen, "Привет", 0, 0, (255, 255, 255))
    assert width > 0


def test_fit_text_cache(renderer):
    long = "очень длинная строка " * 10
    a = renderer.game.fit_text(long, 100)
    b = renderer.game.fit_text(long, 100)
    assert a == b and a.endswith("…")


# ── Кэш карты ──
def test_field_cache_reused_on_idle(make_game, renderer):
    g = make_game()
    g.renderer = renderer
    g.screen_mode = 'game'
    g.draw()
    key = renderer.game._map_key
    g.draw()
    assert renderer.game._map_key == key


def test_field_cache_invalidated_on_move(make_game, renderer):
    g = make_game()
    g.renderer = renderer
    g.draw()
    key = renderer.game._map_key
    g.player.x += 3
    g.update_fov()
    g.draw()
    assert renderer.game._map_key != key


def test_field_cache_invalidated_on_revision(make_game, renderer):
    g = make_game()
    g.renderer = renderer
    g.draw()
    key = renderer.game._map_key
    g.render_revision += 1
    g.draw()
    assert renderer.game._map_key != key


# ── Мини-карта ──
def test_minimap_cache_reused(make_game, renderer):
    g = make_game()
    g.explored = {(10, 10), (11, 10)}
    first = renderer.game._minimap_scaled(g)
    second = renderer.game._minimap_scaled(g)
    assert first is second
    g.explored.add((12, 10))
    assert renderer.game._minimap_scaled(g) is not first


def test_minimap_marker_position():
    g = ClientView()
    g.dungeon.tiles[5][5] = Tile.FLOOR
    g.player.x, g.player.y = 5, 5
    renderer = _renderer()
    renderer.game.draw_minimap(g)
    pos_x = config.SCREEN_W - MAP_W * MINIMAP_SCALE - MINIMAP_MARGIN
    pos_y = MINIMAP_MARGIN
    pixel = renderer.screen.get_at((pos_x + 5 * MINIMAP_SCALE,
                                    pos_y + 5 * MINIMAP_SCALE))[:3]
    assert pixel == (255, 255, 255)


def test_minimap_shows_remote_players():
    g = ClientView()
    g.my_id = 0
    me = Player(class_key='warrior')
    me.x, me.y = 10, 10
    g.player = me
    g.remote_players = {0: me}
    ally = Player(class_key='assassin')
    ally.x, ally.y = 20, 20
    g.remote_players[1] = ally
    g.dungeon.tiles[20][20] = Tile.FLOOR
    renderer = _renderer()
    renderer.game.draw_minimap(g)
    pos_x = config.SCREEN_W - MAP_W * MINIMAP_SCALE - MINIMAP_MARGIN
    pos_y = MINIMAP_MARGIN
    expected = CLASSES['assassin']['color']
    pixel = renderer.screen.get_at((pos_x + 20 * MINIMAP_SCALE,
                                    pos_y + 20 * MINIMAP_SCALE))[:3]
    assert pixel == tuple(expected)


def test_minimap_unexplored_stays_dark():
    g = ClientView()
    g.dungeon.tiles[5][2] = Tile.FLOOR
    g.dungeon.tiles[5][8] = Tile.FLOOR
    g.explored = {(2, 5), (5, 5)}
    g.player.x, g.player.y = 5, 5
    renderer = _renderer()
    renderer.game.draw_minimap(g)
    pos_x = config.SCREEN_W - MAP_W * MINIMAP_SCALE - MINIMAP_MARGIN
    pos_y = MINIMAP_MARGIN
    explored_pixel = renderer.screen.get_at(
        (pos_x + 2 * MINIMAP_SCALE, pos_y + 5 * MINIMAP_SCALE))[:3]
    unexplored_pixel = renderer.screen.get_at(
        (pos_x + 8 * MINIMAP_SCALE, pos_y + 5 * MINIMAP_SCALE))[:3]
    assert explored_pixel != unexplored_pixel


def test_minimap_off_hides_everything():
    g = ClientView()
    g.settings['show_minimap'] = False
    g.draw = lambda: None
    renderer = _renderer()
    renderer.screen.fill((0, 0, 0))
    renderer.game.draw_minimap(g)
    assert renderer.screen.get_at((config.SCREEN_W - 50, 20)) == (0, 0, 0)


# ── Другие игроки на поле ──
def test_visible_remotes_respect_fov():
    g = ClientView()
    g.my_id = 0
    me = Player(class_key='warrior')
    g.player = me
    g.remote_players = {0: me}
    ally = Player(class_key='rogue')
    ally.x, ally.y = 6, 6
    g.remote_players[1] = ally
    renderer = _renderer()
    g.visible = set()
    assert renderer.game.visible_remotes(g) == []
    g.visible = {(6, 6)}
    assert renderer.game.visible_remotes(g) == [ally]


# ── Лог и чат клиента ──
def test_client_log_appends_and_trims():
    g = ClientView()
    for i in range(60):
        g.log(f"msg {i}")
    assert len(g.messages) == 50
    assert g.messages[-1].text == "msg 59"


def test_client_snapshot_events_go_to_log():
    g = ClientView()
    snap = _snap()
    snap['events'] = [{'text': 'Крыса повержена', 'color': [80, 160, 240]},
                      {'text': 'Воин: привет', 'color': [225, 225, 235]}]
    g.update_from_snapshot(snap, None, 0)
    assert [m.text for m in g.messages] == ['Крыса повержена', 'Воин: привет']


def test_client_chat_input():
    sent = []
    g = ClientView(send_fn=sent.append)
    g.handle_key(pygame.K_y)
    assert g.chat_active
    for ch in "привет":
        g.handle_chat_key(0, ch)
    g.handle_chat_key(pygame.K_BACKSPACE, '')
    g.handle_chat_key(pygame.K_RETURN, '')
    assert sent == [{'type': 'chat', 'text': 'приве'}]
    assert not g.chat_active


def test_client_chat_escape_cancels():
    sent = []
    g = ClientView(send_fn=sent.append)
    g.handle_key(pygame.K_y)
    g.handle_chat_key(0, 'x')
    g.handle_chat_key(pygame.K_ESCAPE, '')
    assert sent == [] and not g.chat_active


def test_client_log_scroll_clamps():
    g = ClientView()
    for i in range(20):
        g.log(f"m{i}")
    g.scroll_log(10_000)
    assert g.log_scroll == 14
    g.scroll_log(-10_000)
    assert g.log_scroll == 0


# ── Интерполяция ──
def test_interpolation_moves_between_snapshots():
    g = ClientView()
    g.update_from_snapshot(_snap(x=10, y=10), None, 0)
    assert g.player.render_x == 10
    g.update_from_snapshot(_snap(x=14, y=10), None, 0)
    # сразу после снапшота — ещё на старой позиции
    assert g.player.render_x == 10
    time.sleep(0.03)
    g.tick(0.0)
    assert 10 < g.player.render_x < 14
    time.sleep(0.06)
    g.tick(0.0)
    assert g.player.render_x == pytest.approx(14)


def test_monster_objects_are_reused_by_mid():
    g = ClientView()
    g.update_from_snapshot(_snap(monsters=[_monster(5, 5, mid=7)]), None, 0)
    first = g.dungeon.monsters[0]
    g.update_from_snapshot(_snap(monsters=[_monster(6, 5, mid=7)]), None, 0)
    second = g.dungeon.monsters[0]
    assert first is second
    assert second.x == 6
    time.sleep(0.03)
    g.tick(0.0)
    assert 5 < second.render_x < 6


def test_dead_monster_removed_from_client():
    g = ClientView()
    g.update_from_snapshot(_snap(monsters=[_monster(5, 5, mid=7)]), None, 0)
    g.update_from_snapshot(_snap(monsters=[]), None, 0)
    assert g.dungeon.monsters == []
    assert g._monsters_by_mid == {}


# ── Сервер: события ──
def test_server_snapshot_has_monster_mid_and_events():
    from net.core import GameCore
    core = GameCore(seed=4)
    core.add_player(0, 'warrior')
    core._descend()
    snap = core.get_snapshot()
    assert all('mid' in m for m in snap['monsters'])
    assert 'events' in snap
    # события отдаются один раз
    first = core.get_snapshot()['events']
    assert core.get_snapshot()['events'] == []
    assert isinstance(first, list)


def test_server_kill_event():
    from net.core import GameCore
    core = GameCore(seed=5)
    core.add_player(0, 'warrior')
    p = core.players[0]
    monster = Entity(p.x + 1, p.y, 1, 1, 0, 0, "Крыса", "r",
                     (170, 150, 130), 5)
    core.dungeon.monsters = [monster]
    core.player_attack(0)
    texts = [e['text'] for e in core.get_snapshot()['events']]
    assert any('Крыса' in t for t in texts)


def test_server_chat_event():
    from net.core import GameCore
    core = GameCore(seed=6)
    core.add_player(0, 'warrior')
    core.apply_input(0, {'type': 'chat', 'text': '  привет   отряд '})
    texts = [e['text'] for e in core.get_snapshot()['events']]
    assert texts and 'привет отряд' in texts[0]


def test_server_chat_is_sanitized():
    from net.core import GameCore
    core = GameCore(seed=7)
    core.add_player(0, 'warrior')
    core.chat(0, 'x' * 500)
    text = core.get_snapshot()['events'][0]['text']
    assert len(text) < 200


# ── Вспомогательное ──
def _renderer():
    from render.game_renderer import GameRenderer
    from render.icon_renderer import IconRenderer
    screen = pygame.display.set_mode((config.SCREEN_W, config.SCREEN_H))
    return _Renderer(screen)


class _Renderer:
    """Мини-обёртка: нужен только game-рендерер."""
    def __init__(self, screen):
        from render.game_renderer import GameRenderer
        from render.icon_renderer import IconRenderer
        from render.theme import Theme
        Theme.init()
        self.screen = screen
        self.game = GameRenderer(screen, IconRenderer(screen))


def _player_dict(x=10, y=10):
    return {
        'x': x, 'y': y, 'hp': 25, 'max_hp': 25, 'class': 'warrior',
        'level': 1, 'xp': 0, 'xp_next': 10, 'name': 'Воин',
        'anim_time': 0.0, 'attack_anim': 0.0, 'facing': [1, 0],
        'gold': 0, 'inventory': {}, 'atk': 6, 'df': 4, 'crit': 0.06,
        'dodge': 0.07, 'free_points': 0, 'statuses': {},
        'str': 8, 'agi': 3, 'vit': 7, 'luk': 3, 'df_stat': 7,
        'weapon': None, 'armor': 'padded',
    }


def _snap(x=10, y=10, monsters=None):
    return {
        'world_mode': 'dungeon', 'depth': 1,
        'players': {'0': _player_dict(x, y)},
        'monsters': monsters or [],
        'items': [],
        'merchant': None,
        'elder': None,
        'shop_stock': [],
        'events': [],
    }


def _monster(x, y, mid=1):
    return {'mid': mid, 'x': x, 'y': y, 'hp': 10, 'max_hp': 10,
            'ch': 'g', 'color': [120, 190, 100], 'boss': False,
            'statuses': {}}
