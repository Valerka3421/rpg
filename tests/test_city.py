"""Город Этельгард: стены, ворота, горожане, магазины и мана."""
from core import overworld as ov


def test_city_title_and_kind():
    from core.locations import LOCATIONS, title
    assert ov.VILLAGE_TITLE == 'Город Этельгард'
    assert LOCATIONS['village']['kind'] == 'city'
    assert title('village') == ov.VILLAGE_TITLE
    assert ov.location_title(*ov.VILLAGE_CENTER) == ov.VILLAGE_TITLE


def test_city_wall_ring_and_gates():
    """Кольцо стены сплошное, но на четырёх дорогах оставлены ворота."""
    from core.config import Tile
    from core.world import generate_overworld
    d = generate_overworld()
    vx, vy, vw, vh = ov.VILLAGE_RECT
    for x in range(vx + 1, vx + vw - 1):
        if ov.is_city_wall(x, vy):
            assert d.tiles[vy][x] == Tile.WALL, (x, vy)
        if ov.is_city_wall(x, vy + vh - 1):
            assert d.tiles[vy + vh - 1][x] == Tile.WALL, (x, vy + vh - 1)
    for y in range(vy + 1, vy + vh - 1):
        if ov.is_city_wall(vx, y):
            assert d.tiles[y][vx] == Tile.WALL, (vx, y)
        if ov.is_city_wall(vx + vw - 1, y):
            assert d.tiles[y][vx + vw - 1] == Tile.WALL, (vx + vw - 1, y)

    for gx, gy in ((50, vy), (50, vy + vh - 1), (vx, 35), (vx + vw - 1, 35)):
        assert d.tiles[gy][gx] != Tile.WALL, (gx, gy)
        assert (gx, gy) not in ov.CITY_WALL_CELLS


def test_city_center_reaches_all_gates():
    from collections import deque
    from core.config import Tile
    from core.locations import generate_location, VILLAGE_GATES
    d = generate_location('village')
    start = ov.VILLAGE_CENTER
    seen = {start}
    queue = deque([start])
    while queue:
        x, y = queue.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if not (0 <= nx < d.w and 0 <= ny < d.h):
                continue
            if (nx, ny) in seen or d.tiles[ny][nx] == Tile.WALL:
                continue
            seen.add((nx, ny))
            queue.append((nx, ny))
    for gate in VILLAGE_GATES.values():
        assert gate in seen, gate


def test_city_tiles_are_registered_for_rendering():
    from render.tiles import _VARIANTS
    for kind in ('city_grass', 'city_path', 'city_plaza', 'city_wall',
                 'city_house', 'city_well', 'city_stall', 'city_statue',
                 'city_torch'):
        assert kind in _VARIANTS, kind
    # Старые ключи деревни остаются алиасами — сохранения совместимы.
    for kind in ('village_grass', 'village_path', 'village_plaza',
                 'village_wall', 'village_well', 'village_stall',
                 'village_statue', 'village_torch_post'):
        assert kind in _VARIANTS, kind


def test_biomes_have_unique_ground_and_tree_tiles():
    from render.tiles import _BUILDERS, BIOME_PALETTES
    for biome in BIOME_PALETTES:
        assert f'grass_{biome}' in _BUILDERS, biome
        assert f'tree_{biome}' in _BUILDERS, biome
    assert len({id(fn) for fn in
                (_BUILDERS[f'tree_{b}'] for b in BIOME_PALETTES)}) == \
        len(BIOME_PALETTES)


def test_overworld_monsters_stay_away_from_city():
    from core.world import generate_overworld
    d = generate_overworld()
    for m in d.monsters:
        assert not ov.in_city_safe(m.x, m.y, margin=8), (m.x, m.y, m.name)


def test_city_location_monsters_stay_away_from_city():
    from core.locations import generate_location
    d = generate_location('village')
    for m in d.monsters:
        assert not ov.in_city_safe(m.x, m.y, margin=6), (m.x, m.y, m.name)


def test_city_npcs_spawned(make_game):
    from core.data import CITY_NPC_NAMES
    g = make_game()
    by_type = {n.npc_type: n for n in g.city_npcs}
    assert set(by_type) == set(ov.CITY_NPC_POS) == set(CITY_NPC_NAMES)
    for npc_type, pos in ov.CITY_NPC_POS.items():
        npc = by_type[npc_type]
        assert (npc.x, npc.y) == pos
        assert npc.is_npc
        assert npc.name == CITY_NPC_NAMES[npc_type]
    guard = by_type['guard']
    assert guard.immortal
    assert guard.atk < 10


def _stand_next_to(g, npc):
    """Ставит игрока на пол рядом с горожанином."""
    from core.config import Tile
    for dx, dy in ((0, 1), (1, 0), (-1, 0), (0, -1)):
        x, y = npc.x + dx, npc.y + dy
        if g.dungeon.tiles[y][x] == Tile.FLOOR:
            g.player.x, g.player.y = x, y
            return
    raise AssertionError('нет свободной клетки рядом с NPC')


def test_city_shop_opens_and_has_stock(make_game):
    g = make_game()
    blacksmith = next(n for n in g.city_npcs
                      if n.npc_type == 'blacksmith')
    _stand_next_to(g, blacksmith)
    g.open_city_shop('blacksmith')
    assert g.screen_mode == 'shop'
    assert g.active_shop_npc == 'blacksmith'
    keys = [entry['key'] for entry in g.shop_stock]
    assert 'iron_ingot' in keys
    assert 'ore' in keys


def test_city_shop_buy_and_no_reroll(make_game):
    g = make_game()
    alchemist = next(n for n in g.city_npcs
                     if n.npc_type == 'alchemist')
    _stand_next_to(g, alchemist)
    g.open_city_shop('alchemist')
    g.player.gold = 1000
    idx = next(i for i, entry in enumerate(g.shop_stock)
               if entry['key'] == 'mana_s')
    price = g.shop_stock[idx]['price']
    g.shop.buy(idx)
    assert g.player.inventory.get('mana_s') == 1
    assert g.player.gold == 1000 - price
    # Товары горожан можно покупать повторно.
    assert not g.shop_stock[idx].get('sold')
    g.shop.buy(idx)
    assert g.player.inventory.get('mana_s') == 2
    assert g.player.gold == 1000 - price * 2

    before = list(g.shop_stock)
    g.shop.reroll()
    assert before == g.shop_stock


def test_innkeeper_sells_food_and_alchemist_mana(make_game):
    from core.data import CITY_SHOP_STOCK
    g = make_game()
    food = {key for key, _ in CITY_SHOP_STOCK['innkeeper']}
    assert {'fried_mushrooms', 'berry_compote',
            'roast_venison', 'roast_boar', 'roast_bear'} <= food
    potions = {key for key, _ in CITY_SHOP_STOCK['alchemist']}
    assert {'mana_s', 'mana_m'} <= potions
    smith = {key for key, _ in CITY_SHOP_STOCK['blacksmith']}
    assert {'iron_ingot', 'steel_ingot', 'ore'} <= smith


def test_mana_potion_restores_mana():
    from core.entities import Player
    from core.rules import use_item
    p = Player()
    p.mana = max(0, p.max_mana - 20)
    before = p.mana
    p.inventory['mana_s'] = 1
    res = use_item(p, 'mana_s')
    assert res.ok
    assert p.mana > before
    assert p.mana - before == min(12, p.max_mana - before)
    assert 'mana_s' not in p.inventory


def test_food_restores_hp_and_mana():
    from core.entities import Player
    from core.rules import use_item
    p = Player()
    p.hp = 1
    p.mana = 0
    p.inventory['berry_compote'] = 1
    res = use_item(p, 'berry_compote')
    assert res.ok
    assert p.hp == 1 + min(6, p.max_hp - 1)
    assert p.mana == min(6, p.max_mana)
    assert 'berry_compote' not in p.inventory


def test_scroll_needs_mana():
    from core.entities import Player
    from core.rules import use_item
    p = Player()
    p.mana = 0
    p.hp = 1
    p.inventory['scroll_heal'] = 1
    res = use_item(p, 'scroll_heal')
    assert not res.ok
    assert res.kind == 'mana'
    assert p.inventory['scroll_heal'] == 1
    assert p.hp == 1


def test_guard_damages_monsters_near_gate(make_game):
    from core.data import MONSTERS
    from core.entities import Entity
    g = make_game()
    guard = next(n for n in g.city_npcs if n.npc_type == 'guard')
    data = MONSTERS['wolf']
    m = Entity(guard.x, guard.y - 3, data['hp'], data['hp'], data['atk'],
               data['df'], data['name'], data['ch'], data['color'],
               data['xp'])
    g.dungeon.monsters.append(m)
    g.player.xp = 0
    g.player.gold = 0
    hp_before = m.hp
    g._guard_timer = 0.0
    g._update_city_guard(0.0)
    assert m.hp < hp_before
    assert g.player.xp == 0
    assert g.player.gold == 0


def test_guard_ignores_monsters_beyond_radius(make_game):
    from core.data import MONSTERS
    from core.entities import Entity
    g = make_game()
    guard = next(n for n in g.city_npcs if n.npc_type == 'guard')
    data = MONSTERS['wolf']
    m = Entity(guard.x + 9, guard.y, data['hp'], data['hp'], data['atk'],
               data['df'], data['name'], data['ch'], data['color'],
               data['xp'])
    g.dungeon.monsters.append(m)
    hp_before = m.hp
    g._guard_timer = 0.0
    g._update_city_guard(0.0)
    assert m.hp == hp_before


def test_mana_is_saved_and_loaded():
    from core.entities import Player
    from core.save import player_to_dict, player_from_dict
    p = Player()
    p.mana = 3
    data = player_to_dict(p)
    assert data['mana'] == 3
    q = player_from_dict(data)
    assert q.mana == 3
    assert q.max_mana == p.max_mana

    # Старые сейвы без маны получают полный запас.
    old = player_to_dict(p)
    old.pop('mana')
    r = player_from_dict(old)
    assert r.mana == r.max_mana


def test_city_npcs_survive_save_load(make_game, tmp_path):
    g = make_game()
    assert g.location_title() == ov.VILLAGE_TITLE
    g.player.mana = 5
    before = sorted((n.npc_type, n.x, n.y) for n in g.city_npcs)
    path = str(tmp_path / 'city_save.json')
    g.save(path=path, silent=True)

    h = make_game(seed=1)
    h.load(path=path)
    after = sorted((n.npc_type, n.x, n.y) for n in h.city_npcs)
    assert after == before
    assert h.player.mana == 5
    guard = next(n for n in h.city_npcs if n.npc_type == 'guard')
    assert guard.immortal
    assert h.active_shop_npc is None


def test_new_item_icons_render(renderer):
    for key in ('mana_s', 'mana_m', 'fried_mushrooms',
                'berry_compote', 'roast_venison', 'roast_boar',
                'roast_bear'):
        renderer.icons.draw_item(key, 0, 0, size=32)


def test_city_render_smoke(make_game, renderer):
    g = make_game()
    renderer.game.draw_field(g)
    renderer.game.draw_minimap(g)
    guard = next(n for n in g.city_npcs if n.npc_type == 'guard')
    _stand_next_to(g, guard)
    g.dialogue.open('guard')
    renderer.screens.draw_dialogue(g)
    shop_npc = next(n for n in g.city_npcs if n.npc_type == 'blacksmith')
    _stand_next_to(g, shop_npc)
    g.open_city_shop('blacksmith')
    renderer.screens.draw_shop(g)
