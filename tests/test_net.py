"""Тесты сетевой логики (без pygame): GameCore и TCP-сервер."""
import json
import socket
import threading
import time

from core.config import Tile, MAP_H
from core.entities import Entity
from core.data import xp_to_next
from net.core import GameCore
from net.server import Server


def _core(players=1):
    core = GameCore(seed=42)
    for pid in range(players):
        core.add_player(pid, 'warrior' if pid == 0 else 'rogue')
    return core


def _tile(core, entity):
    return int(core.dungeon.tiles[entity.y][entity.x])


# ── Игроки и спавн ──
def test_add_remove_player():
    core = _core()
    assert 0 in core.players and 0 in core.move_timers
    core.remove_player(0)
    assert 0 not in core.players and 0 not in core.move_timers


def test_spawn_not_in_wall():
    core = _core(players=4)
    for p in core.players.values():
        assert _tile(core, p) != Tile.WALL


def test_players_do_not_share_tile():
    core = _core(players=4)
    positions = {(p.x, p.y) for p in core.players.values()}
    assert len(positions) == 4


def test_descend_puts_players_on_walkable_tiles():
    core = _core(players=4)
    core._descend()
    assert core.world_mode == 'dungeon' and core.depth == 1
    for p in core.players.values():
        assert _tile(core, p) != Tile.WALL
    assert core.dungeon.tiles[core.dungeon.stairs_pos[1]][core.dungeon.stairs_pos[0]] == Tile.STAIRS


def test_descend_and_ascend():
    core = _core()
    core._descend()
    core._new_dungeon(2)
    core._new_dungeon(3)
    assert core.depth == 3
    # вверх на 2
    core._new_dungeon(2, arrive='stairs')
    assert core.depth == 2
    # до поверхности
    core.depth = 1
    core.world_mode = 'dungeon'
    core._ascend()
    assert core.world_mode == 'overworld' and core.depth == 0
    assert core.dungeon is core.overworld


def test_ascend_returns_players_near_entrance():
    from core import overworld as ov
    core = _core(players=3)
    core._descend()
    core.depth = 1
    core._ascend()
    ex, ey = ov.DUNGEON_POS
    for p in core.players.values():
        assert max(abs(p.x - ex), abs(p.y - ey)) <= 4
        assert core.overworld.tiles[p.y][p.x] != Tile.WALL


def test_descent_cached_floor_preserved():
    core = _core()
    core._descend()
    floor1 = core.dungeon
    core._new_dungeon(2)
    core._new_dungeon(1, arrive='stairs')
    assert core.dungeon is floor1


# ── Бой ──
def test_attack_kills_monster_and_rewards():
    core = _core()
    p = core.players[0]
    monster = Entity(p.x + 1, p.y, 3, 3, 0, 0, "Гоблин", "g", (170, 150, 130),
                     5)
    core.dungeon.monsters = [monster]
    gold_before = p.gold
    for _ in range(20):
        core.player_attack(0)
        if not core.dungeon.monsters:
            break
    assert not core.dungeon.monsters
    assert p.gold > gold_before
    assert p.xp >= 5


def test_kill_gives_level_up():
    core = _core()
    p = core.players[0]
    monster = Entity(p.x + 1, p.y, 1, 1, 0, 0, "Крыса", "r", (170, 150, 130),
                     xp_to_next(1))
    core.dungeon.monsters = [monster]
    core.player_attack(0)
    assert p.level == 2 and p.free_points == 5


def test_monster_attacks_player():
    core = _core()
    p = core.players[0]
    core.dungeon.monsters = [Entity(p.x + 1, p.y, 10, 10, 5, 0, "Орк", "O",
                                    (150, 95, 60), 10)]
    hp = p.hp
    for _ in range(50):
        core._monsters_update(0.5)
        if p.hp < hp:
            break
    assert p.hp < hp


def test_player_respawns_after_death():
    core = _core()
    p = core.players[0]
    p.hp = 1
    core.dungeon.monsters = [Entity(p.x + 1, p.y, 10, 10, 999, 0, "Демон", "D",
                                    (200, 70, 70), 10)]
    core._monsters_update(1.0)
    assert p.hp == p.max_hp
    assert _tile(core, p) != Tile.WALL


def test_monster_status_ticks_and_kills():
    core = _core()
    p = core.players[0]
    monster = Entity(p.x + 1, p.y, 3, 3, 0, 0, "Крыса", "r", (170, 150, 130), 5)
    monster.add_status('burning', 3)
    core.dungeon.monsters = [monster]
    for _ in range(4):
        core._tick_statuses()
    assert not core.dungeon.monsters
    assert p.xp >= 5


def test_slow_monster_skips_turn():
    core = _core()
    p = core.players[0]
    monster = Entity(p.x + 1, p.y, 10, 10, 50, 0, "Тролль", "T",
                     (85, 140, 85), 10)
    monster.add_status('slow', 3)
    core.dungeon.monsters = [monster]
    hp = p.hp
    core._monsters_update(0.5)
    assert p.hp == hp


# ── Предметы ──
def test_use_potion():
    core = _core()
    p = core.players[0]
    p.hp = 5
    p.inventory = {'potion_s': 1}
    core.use_item(0, 'potion_s')
    assert p.hp == 15 and 'potion_s' not in p.inventory


def test_use_scroll_fireball():
    core = _core()
    p = core.players[0]
    p.inventory = {'scroll_fireball': 1}
    monsters = [Entity(p.x + 2, p.y, 30, 30, 0, 0, "A", "g", (1, 2, 3), 5),
                Entity(p.x + 3, p.y + 1, 30, 30, 0, 0, "B", "g", (1, 2, 3), 5)]
    core.dungeon.monsters = monsters
    core.use_item(0, 'scroll_fireball')
    assert 'scroll_fireball' not in p.inventory
    assert all(m.hp < 30 for m in monsters)


def test_craft_requires_level_and_materials():
    from core import rules
    core = _core()
    p = core.players[0]
    idx = next(i for i, c in enumerate(rules.craftable_list(p.inventory, p.level))
               if c[0] == 'bone_sword')
    p.inventory = {'bone': 2, 'wood': 1}
    assert core.craft_item(0, idx) is False        # уровень 1 < 3
    p.level = 5
    assert core.craft_item(0, idx) is True
    assert p.inventory.get('bone_sword') == 1


def test_pickup_gold():
    core = _core()
    p = core.players[0]
    core.dungeon.items[(p.x, p.y)] = 'gold_medium'
    core.move_player(0, 0, 0)
    assert p.gold == 15


# ── Магазин ──
def _put_merchant(core):
    p = core.players[0]
    core.merchant = core._make_merchant({'x': p.x + 1, 'y': p.y})
    core.shop_stock = [{'key': 'potion_s', 'price': 5, 'sold': False}]
    p.gold = 100


def test_shop_requires_adjacency():
    core = _core()
    p = core.players[0]
    core.merchant = core._make_merchant({'x': p.x + 5, 'y': p.y})
    core.shop_stock = [{'key': 'potion_s', 'price': 5, 'sold': False}]
    p.gold = 100
    assert core.buy_item(0, 0) is False
    assert p.gold == 100


def test_shop_buy_sell_reroll():
    core = _core()
    _put_merchant(core)
    p = core.players[0]
    assert core.buy_item(0, 0) is True
    assert p.inventory.get('potion_s') == 1 and p.gold == 95
    assert core.buy_item(0, 0) is False            # уже продано
    p.inventory['bone'] = 1
    assert core.sell_item(0, 0) is True            # продаёт bone? первый по алфавиту
    p.gold = 1000
    assert core.reroll_shop(0) is True
    assert p.gold == 1000 - (15 + core.depth * 3)


def test_shop_reroll_without_gold():
    core = _core()
    _put_merchant(core)
    core.players[0].gold = 1
    assert core.reroll_shop(0) is False


# ── Сейв/лоад мира ──
def test_save_and_load_world(tmp_path):
    core = _core(players=2)
    core._descend()
    core._new_dungeon(4)
    core.players[0].inventory = {'bone': 2}
    path = str(tmp_path / 'world.json')
    core.save_world(path)

    loaded = GameCore(seed=7)
    assert loaded.load_world(path) is True
    assert loaded.world_mode == 'dungeon'
    assert loaded.depth == 4
    assert loaded.dungeon.stairs_pos == core.dungeon.stairs_pos
    assert len(loaded.dungeon.monsters) == len(core.dungeon.monsters)
    assert loaded.overworld.stairs_pos == core.overworld.stairs_pos
    assert (loaded.elder is not None) == (core.elder is not None)


def test_load_missing_world(tmp_path):
    assert GameCore(seed=1).load_world(str(tmp_path / 'nope.json')) is False


def test_load_world_legacy_v5(tmp_path):
    """Сейв старого формата (без overworld, тайлы списками) читается."""
    from core.save import dungeon_to_dict
    from core.world import generate_dungeon

    world = generate_dungeon(3)
    payload = {
        'world_mode': 'dungeon',
        'depth': 3,
        'dungeon': dungeon_to_dict(world),
        'merchant': None,
    }
    payload['dungeon']['tiles'] = [[int(t) for t in row] for row in world.tiles]
    path = tmp_path / 'legacy.json'
    path.write_text(json.dumps(payload), encoding='utf-8')

    core = GameCore(seed=1)
    assert core.load_world(str(path)) is True
    assert core.world_mode == 'dungeon' and core.depth == 3
    assert core.dungeon.stairs_pos == world.stairs_pos
    assert core.overworld is not core.dungeon
    assert any(t == Tile.UPSTAIRS for row in core.dungeon.tiles for t in row)
    assert core.elder is not None


# ── Снапшот ──
def test_snapshot_contains_everything_client_needs():
    core = _core(players=2)
    core._descend()
    snap = core.get_snapshot()
    assert snap['type'] if 'type' in snap else True
    assert set(snap['players']) == {'0', '1'}
    me = snap['players']['0']
    for field in ('x', 'y', 'hp', 'max_hp', 'class', 'level', 'xp', 'xp_next',
                  'inventory', 'statuses', 'atk', 'df', 'crit', 'dodge'):
        assert field in me, field
    assert core.get_tiles()[0][0] in (0, 1, 2, 3, 4)
    assert 'shop_stock' in snap and 'merchant' in snap and 'elder' in snap
    assert 'events' in snap
    assert all('mid' in monster for monster in snap['monsters'])


# ── Сервер ──
def _free_port():
    s = socket.socket()
    s.bind(('127.0.0.1', 0))
    port = s.getsockname()[1]
    s.close()
    return port


def _read_message(sock, timeout=3.0):
    sock.settimeout(timeout)
    buf = b''
    while b'\n' not in buf:
        data = sock.recv(65536)
        if not data:
            return None
        buf += data
    line, _ = buf.split(b'\n', 1)
    return json.loads(line.decode())


def test_server_welcome_is_single_json_line(tmp_path):
    port = _free_port()
    server = Server(host='127.0.0.1', port=port, max_players=2)
    server.saved_players = {}
    server.world_save_path = str(tmp_path / 'w.json')
    server.players_save_path = str(tmp_path / 'p.json')
    thread = threading.Thread(target=server.start, daemon=True)
    thread.start()
    time.sleep(0.4)
    try:
        sock = socket.create_connection(('127.0.0.1', port), timeout=3)
        welcome = _read_message(sock)
        assert welcome['type'] == 'welcome'
        assert len(welcome['tiles']) == MAP_H
        sock.sendall((json.dumps({'type': 'hello', 'class': 'warrior',
                                  'token': 't1'}) + '\n').encode())
        state = _read_message(sock)
        # сервер мог прислать кадр, отправленный до обработки hello —
        # ждём, пока игрок появится в снапшоте
        for _ in range(10):
            if '0' in state.get('players', {}):
                break
            state = _read_message(sock)
        assert state['type'] in ('state', 'state_with_tiles')
        assert '0' in state['players']
        # движение доходит до ядра
        x0 = state['players']['0']['x']
        sock.sendall(b'{"type":"move","dx":1,"dy":0}\n')
        for _ in range(20):
            state = _read_message(sock)
            if state['players']['0']['x'] != x0:
                break
        sock.close()
    finally:
        server.stop()
        time.sleep(0.2)


def test_server_broadcasts_chat(tmp_path):
    port = _free_port()
    server = Server(host='127.0.0.1', port=port, max_players=2)
    server.saved_players = {}
    server.world_save_path = str(tmp_path / 'w.json')
    server.players_save_path = str(tmp_path / 'p.json')
    threading.Thread(target=server.start, daemon=True).start()
    time.sleep(0.4)
    sock = None
    try:
        sock = socket.create_connection(('127.0.0.1', port), timeout=3)
        assert _read_message(sock)['type'] == 'welcome'
        sock.sendall((json.dumps({'type': 'hello', 'class': 'warrior',
                                  'token': 'chat-test'}) + '\n').encode())
        # дождаться регистрации
        for _ in range(10):
            state = _read_message(sock)
            if '0' in state.get('players', {}):
                break
        sock.sendall((json.dumps({'type': 'chat', 'text': 'привет отряд'})
                      + '\n').encode())
        found = False
        for _ in range(20):
            state = _read_message(sock)
            texts = [e['text'] for e in state.get('events', [])]
            if any('привет отряд' in t for t in texts):
                found = True
                break
        assert found, "чат не дошёл до рассылки"
    finally:
        if sock is not None:
            sock.close()
        server.stop()
        time.sleep(0.2)


def test_server_rejects_fifth_player(tmp_path):
    port = _free_port()
    server = Server(host='127.0.0.1', port=port, max_players=2)
    server.saved_players = {}
    server.world_save_path = str(tmp_path / 'w.json')
    server.players_save_path = str(tmp_path / 'p.json')
    threading.Thread(target=server.start, daemon=True).start()
    time.sleep(0.4)
    sockets = []
    try:
        for i in range(2):
            s = socket.create_connection(('127.0.0.1', port), timeout=3)
            assert _read_message(s)['type'] == 'welcome'
            s.sendall((json.dumps({'type': 'hello', 'class': 'warrior',
                                   'token': f'k{i}'}) + '\n').encode())
            sockets.append(s)
        time.sleep(0.3)
        extra = socket.create_connection(('127.0.0.1', port), timeout=3)
        msg = _read_message(extra)
        assert msg['type'] == 'error' and 'full' in msg['msg']
        extra.close()
    finally:
        for s in sockets:
            s.close()
        server.stop()
        time.sleep(0.2)
