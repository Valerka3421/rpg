"""Тесты новых правок: карта, мобы, кристаллы, навыки и окно крафта."""
from types import SimpleNamespace

import pygame

from core.config import MAP_W, MAP_H, Tile
from core.data import (MONSTERS, SKILLS, CRAFT_TABS, generate_board_quests,
                       mana_crystal_for, roll_mana_crystal, item_recipes)
from core.world import BIOME_MONSTERS, FOREST_MONSTERS
from core.entities import Dungeon, Player
from core import rules
from core import harvest
from core import locations
from core import overworld as ov
from core.save import (player_to_dict, player_from_dict,
                       dungeon_to_dict, dungeon_from_dict)


# ── Карта и локации ──
def test_map_is_expanded_but_legacy_regions_kept():
    assert (MAP_W, MAP_H) == (140, 70)
    assert ov.LEGACY_MAP_W == 100
    assert ov.GOBLIN_VILLAGE_CENTER[0] > ov.LEGACY_MAP_W
    assert ov.in_goblin_village(*ov.GOBLIN_VILLAGE_CENTER)


def test_goblin_village_reachable_by_gate(make_game):
    from core import locations
    g = make_game()
    village = g.dungeon
    gate = locations.VILLAGE_GATES['goblin_village']
    assert village.transitions[gate]['to'] == 'goblin_village'
    goblin = g.get_location('goblin_village')
    assert any(tr['to'] == 'village' for tr in goblin.transitions.values())


def test_goblin_village_spawns_goblins_boars_wolves(make_game):
    g = make_game()
    goblin = g.get_location('goblin_village')
    chars = {m.ch for m in goblin.monsters}
    assert {'g', 'a', 'S'} <= chars
    assert chars <= {'g', 'a', 'S', 'p', 'w'}
    assert len(goblin.monsters) >= 10


def test_forest_monsters_are_only_new_pool():
    allowed = {'deer', 'wolf', 'bear', 'slime', 'boar', 'rat'}
    assert set(FOREST_MONSTERS) == allowed
    for pool in BIOME_MONSTERS.values():
        assert set(pool) <= allowed

    from core.world import generate_dungeon
    dungeon_chars = {m.ch for m in generate_dungeon(4).monsters}
    assert not dungeon_chars & {'e', 'b', 'j', 'w', 'p'}


def test_board_quests_do_not_use_overworld_only_monsters():
    from core.data import _board_monsters_for
    keys = _board_monsters_for(1, 20)
    assert keys
    assert all(not MONSTERS[k].get('overworld_only') for k in keys)
    for quest in generate_board_quests(1, 20, count=6, base_seed=1):
        if quest['type'] == 'kill' and quest['target'] in MONSTERS:
            assert not MONSTERS[quest['target']].get('overworld_only')


# ── Кристаллы маны и дроп ──
def test_mana_crystal_size_depends_on_monster():
    small = SimpleNamespace(max_hp=9, boss=False)
    normal = SimpleNamespace(max_hp=30, boss=False)
    large = SimpleNamespace(max_hp=30, boss=True)
    assert mana_crystal_for(small) == 'mana_crystal_small'
    assert mana_crystal_for(normal) == 'mana_crystal'
    assert mana_crystal_for(large) == 'mana_crystal_large'
    assert roll_mana_crystal(small, chance=1.0) == 'mana_crystal_small'
    assert roll_mana_crystal(small, chance=0.0) is None


def test_kill_reward_can_contain_mana_crystal(monkeypatch):
    from core import rules as rules_mod
    monster = SimpleNamespace(ch='r', boss=False, max_hp=10, xp_value=3)
    monkeypatch.setattr(rules_mod, 'roll_mana_crystal',
                        lambda m: 'mana_crystal_small')
    reward = rules_mod.roll_kill_reward(monster, depth=1)
    assert 'mana_crystal_small' in reward.drops


def test_powder_volume_depends_on_crystal_size():
    assert item_recipes('magic_powder')[0][1] == 1
    assert item_recipes('magic_powder')[1][1] == 2
    assert item_recipes('magic_powder')[2][1] == 4

    inv = {'mana_crystal_large': 1}
    ok, reason = rules.craft('magic_powder', inv, level=1)
    assert ok and reason == 'ok'
    assert inv['magic_powder'] == 4


# ── Навыки и вкладки крафта ──
def test_craft_tabs_and_categories():
    keys = [k for k, _ in CRAFT_TABS]
    assert keys == ['all', 'blacksmith', 'alchemy', 'magic_engineering']
    assert set(SKILLS) == {'blacksmith', 'alchemy', 'magic_engineering'}

    recipes = rules.craftable_list({}, 10)
    categories = {entry[0]: entry[4] for entry in recipes}
    assert categories['iron_ingot'] == 'blacksmith'
    assert categories['potion_s'] == 'alchemy'
    assert categories['scroll_heal'] == 'magic_engineering'
    assert categories['rune_fire'] == 'magic_engineering'


def test_craft_awards_skill_xp_and_uses_variant(make_game):
    g = make_game()
    g.player.level = 5
    g.player.inventory = {'mana_crystal': 1}
    g.craft.set_tab('alchemy')
    entries = g.craftable_list()
    idx = next(i for i, e in enumerate(entries) if e[0] == 'magic_powder')
    before = g.player.skills['alchemy']['xp']
    g.craft_item(idx)
    assert g.player.inventory.get('magic_powder') == 2
    assert 'mana_crystal' not in g.player.inventory
    assert g.player.skills['alchemy']['xp'] > before


def test_craft_pagination_maps_global_indexes(make_game):
    g = make_game()
    g.craft.set_tab('all')
    total = len(g.craftable_list())
    assert total > 8
    assert g.craft_pages_count() >= 2
    g.craft.next_page()
    assert g.craft.page == 1
    assert g.craft.page_offset() == 8
    assert all(e in g.craftable_list() for e in g.craft_page_items())


# ── Сейвы ──
def test_skills_roundtrip_and_legacy_default():
    p = Player()
    p.skills['alchemy'] = {'level': 4, 'xp': 12}
    restored = player_from_dict(player_to_dict(p))
    assert restored.skills['alchemy'] == {'level': 4, 'xp': 12}

    legacy = player_to_dict(p)
    legacy.pop('skills')
    fresh = player_from_dict(legacy)
    assert fresh.skills['alchemy'] == {'level': 1, 'xp': 0}


def test_old_overworld_save_is_padded_and_extended():
    old = Dungeon(100, 70)
    for y in range(10):
        for x in range(10):
            old.tiles[y][x] = Tile.FLOOR
    payload = dungeon_to_dict(old)
    restored = dungeon_from_dict(payload, is_overworld=True)
    assert len(restored.tiles) == MAP_H
    assert len(restored.tiles[0]) == MAP_W
    assert restored.tiles[3][3] == Tile.FLOOR
    # Восточная часть новой карты — не сплошная стена: там деревня гоблинов.
    assert ov.in_goblin_village(*ov.GOBLIN_VILLAGE_CENTER)


def test_old_dungeon_save_padded_with_walls():
    old = Dungeon(100, 70)
    old.tiles[5][5] = Tile.FLOOR
    restored = dungeon_from_dict(dungeon_to_dict(old))
    assert len(restored.tiles[0]) == MAP_W
    assert restored.tiles[5][5] == Tile.FLOOR
    assert restored.tiles[10][120] == Tile.WALL


# ── Рендер ──
def test_entities_are_drawn_above_world_tiles(renderer, make_game):
    """Игрок и мобы всегда поверх текстур мира (стены больше не «нависают»)."""
    g = make_game()
    order = []
    renderer.game._draw_map_cached = lambda *a, **k: order.append('map')
    renderer.game._draw_player = lambda *a, **k: order.append('player')
    renderer.game._draw_walls_layer = lambda *a, **k: order.append('walls')
    renderer.game.draw_field(g)
    assert order.index('map') < order.index('player')
    assert 'walls' not in order


def test_entities_are_painted_into_field_surface(renderer, make_game):
    """Иконки рисуются в поле, а не в дисплей (иначе их затирает блит)."""
    g = make_game()
    seen = {}
    original = renderer.icons.draw_player

    def spy(*args, **kwargs):
        seen['surface'] = renderer.icons.screen
        return original(*args, **kwargs)

    renderer.icons.draw_player = spy
    renderer.game.draw_field(g)
    assert seen['surface'] is renderer.game._field_surface
    # После отрисовки рендерер иконок возвращается на главный экран.
    assert renderer.icons.screen is renderer.screen


def test_craft_window_tabs_and_page_clicks(renderer, make_game):
    g = make_game()
    g.player.level = 10
    g.screen_mode = 'craft'
    renderer.draw(g)

    tabs = dict(g._ui_hits['craft']['tabs'])
    rect = tabs['alchemy']
    assert g._handle_craft_click((rect[0] + 3, rect[1] + 3))
    assert g.craft.tab == 'alchemy'

    # Возвращаемся на «Все крафты» и листаем вперёд.
    g.craft.set_tab('all')
    renderer.draw(g)
    assert g.craft_pages_count() > 1
    nxt = g._ui_hits['craft']['next']
    assert g._handle_craft_click((nxt[0] + 3, nxt[1] + 3))
    assert g.craft.page == 1

    renderer.draw(g)
    prv = g._ui_hits['craft']['prev']
    assert g._handle_craft_click((prv[0] + 3, prv[1] + 3))
    assert g.craft.page == 0


def test_quest_board_has_own_world_texture(renderer):
    from render.tiles import Tiles
    board = Tiles.get('quest_board')
    grass = Tiles.get('village_grass', 0)
    assert board.get_size() == grass.get_size()
    assert pygame.image.tostring(board, 'RGBA') != pygame.image.tostring(
        grass, 'RGBA')


def test_new_monsters_and_items_render(renderer):
    for ch in ('e', 'b', 'j', 'w', 'p'):
        assert renderer.icons.draw_monster(ch, 0, 0)
    for key in ('mana_crystal_small', 'mana_crystal_large', 'magic_powder',
                'iron_ingot', 'wolf_pelt', 'bear_pelt', 'venison',
                'slime_gel', 'boar_tusk', 'iron_sword', 'steel_sword',
                'tusk_axe', 'wolf_armor', 'bear_cloak', 'wolf_hood',
                'rune_fire', 'rune_frost', 'rune_heal'):
        renderer.icons.draw_item(key, 0, 0)


def test_goblin_village_tiles_render(renderer):
    from render.tiles import Tiles
    for kind in ('goblin_grass', 'goblin_path', 'goblin_plaza',
                 'goblin_hut', 'goblin_totem', 'goblin_campfire',
                 'dungeon_ruins', 'ruins_pillar',
                 'stump', 'berry_bush', 'berry_bush_empty',
                 'ore_rock', 'ore_depleted', 'gate'):
        surf = Tiles.get(kind, 0)
        assert surf.get_size() == (64, 64)


# ── Отдельные карты локаций ──
def test_each_location_is_a_separate_map(make_game):
    g = make_game()
    village = g.dungeon
    assert village.w == locations.VILLAGE_W
    for key in ('pine', 'birch', 'rock', 'autumn', 'swamp', 'dark',
                'meadow', 'dead', 'goblin_village'):
        gate = locations.VILLAGE_GATES[key]
        assert village.transitions[gate]['to'] == key
        target = g.get_location(key)
        assert target is not village
        assert (target.w, target.h) == (locations.LOCATION_W,
                                        locations.LOCATION_H)
        assert any(tr['to'] == 'village' for tr in target.transitions.values())


def test_travel_between_locations_keeps_state(make_game):
    g = make_game()
    copied = g.get_location('village')
    g.travel_to('pine')
    assert g.location == 'pine'
    assert g.dungeon is g.get_location('pine')
    assert g.dungeon is not copied
    # Возврат через карту: прогресс карты остаётся в объекте локации.
    tile = (10, 10)
    g.dungeon.tiles[tile[1]][tile[0]] = Tile.FLOOR
    g.explored.add(tile)
    g.travel_to('village')
    g.travel_to('pine')
    assert tile in g.explored
    assert g.dungeon.tiles[tile[1]][tile[0]] == Tile.FLOOR


def test_location_maps_are_saved_and_not_regenerated(make_game, tmp_path):
    g = make_game()
    g.travel_to('birch')
    g.dungeon.tiles[9][9] = Tile.FLOOR
    monster = g.dungeon.monsters[0]
    monster.hp = 1
    path = str(tmp_path / 'save.json')
    g.save(path=path)

    g.dungeon.tiles[9][9] = Tile.WALL
    monster.hp = 999
    g.locations.clear()
    assert g.load(path=path) is True
    assert g.location == 'birch'
    assert g.dungeon.tiles[9][9] == Tile.FLOOR
    assert g.dungeon.monsters[0].hp == 1


def test_mountain_has_mine_and_mine_has_ore(make_game):
    g = make_game()
    rock = g.get_location('rock')
    mine_gates = [v for v in rock.transitions.values() if v['to'] == 'mine']
    assert mine_gates
    mine = g.get_location('mine')
    ore = [rec for rec in mine.harvestables.values() if rec['kind'] == 'ore']
    assert len(ore) >= 20


def test_ore_is_harvested_by_attack(make_game):
    g = make_game()
    g.travel_to('mine')
    dungeon = g.dungeon
    spot = None
    for (x, y), rec in dungeon.harvestables.items():
        if rec['kind'] != 'ore':
            continue
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if (0 <= nx < dungeon.w and 0 <= ny < dungeon.h
                    and dungeon.tiles[ny][nx] != Tile.WALL):
                spot = (nx, ny, x, y)
                break
        if spot:
            break
    assert spot
    px, py, ox, oy = spot
    g.player.x, g.player.y = px, py
    g.player.atk = 10
    g.player_attack()
    assert g.player.inventory.get('ore', 0) >= 2
    assert not dungeon.harvestables[(ox, oy)]['has']


def test_tree_is_harvested_by_attack(make_game):
    g = make_game()
    g.travel_to('pine')
    dungeon = g.dungeon
    spot = None
    for (x, y) in ((ix, iy) for iy in range(dungeon.h)
                   for ix in range(dungeon.w)):
        if dungeon.tiles[y][x] != Tile.WALL:
            continue
        if not locations.is_tree_wall('pine', x, y):
            continue
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if (0 <= nx < dungeon.w and 0 <= ny < dungeon.h
                    and dungeon.tiles[ny][nx] != Tile.WALL
                    and (nx, ny) not in dungeon.harvestables):
                spot = (nx, ny, x, y)
                break
        if spot:
            break
    assert spot
    px, py, tx, ty = spot
    g.player.x, g.player.y = px, py
    g.player.atk = 10
    g.player_attack()
    assert g.player.inventory.get('wood', 0) >= 2
    # Удар мог выбрать любое соседнее дерево — проверяем, что срублено
    # именно дерево по соседству.
    felled = [rec for pos, rec in dungeon.harvestables.items()
              if rec['kind'] == 'tree' and rec['has'] is False
              and max(abs(pos[0] - px), abs(pos[1] - py)) <= 1]
    assert felled


def test_bushes_have_berries_and_regrow(make_game):
    g = make_game()
    dungeon = g.dungeon
    bush = next(((x, y), rec) for (x, y), rec in dungeon.harvestables.items()
                if rec['kind'] == 'bush')
    (bx, by), rec = bush
    assert rec['has'] is True
    # Встаём рядом и собираем ягоды.
    spot = None
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        nx, ny = bx + dx, by + dy
        if (0 <= nx < dungeon.w and 0 <= ny < dungeon.h
                and dungeon.tiles[ny][nx] != Tile.WALL
                and (nx, ny) not in dungeon.harvestables):
            spot = (nx, ny)
            break
    assert spot
    g.player.x, g.player.y = spot
    g.harvest_at(bx, by)
    assert g.player.inventory.get('berry', 0) >= 1
    assert rec['has'] is False
    # Время сбора прошло — ягоды снова на кусте.
    rec['ready_at'] = 0
    harvest.tick(dungeon)
    assert rec['has'] is True


def test_deer_is_passive_and_drops_no_gold(make_game):
    from core.data import MONSTERS, MONSTER_DROPS
    from types import SimpleNamespace
    assert MONSTERS['deer'].get('passive') is True
    deer = SimpleNamespace(ch='e', boss=False, max_hp=8, xp_value=8)
    reward = rules.roll_kill_reward(deer, depth=1)
    assert reward.gold == 0
    # Сама таблица дропа оленя — мясо и кости, без золота.
    assert MONSTER_DROPS['e']
    assert not any(key.startswith('gold') for key, _ in MONSTER_DROPS['e'])
    assert not any(k.startswith('gold') for k in reward.drops)
    for drops in MONSTER_DROPS.values():
        assert not any(key.startswith('gold') for key, _ in drops)


def test_fog_only_underground(make_game):
    g = make_game()
    assert g.location != 'mine'
    assert len(g.visible) == g.dungeon.w * g.dungeon.h
    g.travel_to('mine')
    assert len(g.visible) < g.dungeon.w * g.dungeon.h


def test_resolution_change_exits_fullscreen(make_game, monkeypatch):
    import core.settings as settings_mod
    monkeypatch.setattr(settings_mod, 'save_settings', lambda *a, **k: None)
    g = make_game()
    g.settings = {'resolution': [1280, 720], 'fullscreen': True}
    g.menu.selection_settings = 0
    g.menu.adjust_setting(1)
    assert g.settings['fullscreen'] is False
    assert g.pending_resolution is not None
