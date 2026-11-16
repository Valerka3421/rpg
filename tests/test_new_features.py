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


def test_ore_is_harvested_by_e_key(make_game):
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
    # Возобновляемые ресурсы добываются только клавишей «E»
    g.harvest_adjacent()
    assert g.player.inventory.get('ore', 0) >= 2
    assert not dungeon.harvestables[(ox, oy)]['has']


def test_tree_is_harvested_by_e_key(make_game):
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
    g.harvest_adjacent()
    assert g.player.inventory.get('wood', 0) >= 2
    # «E» могло выбрать любое соседнее дерево — проверяем, что срублено
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


# ── Счётчик предметов и кнопка «Назад» ──
def test_inventory_count_badge_is_top_right(renderer):
    rect = renderer.screens._draw_count_badge(100, 60, 120, 100, 5)
    bx, by, bw, bh = rect
    # Бейдж «×N» — в правом верхнем углу ячейки
    assert (bx, by) == (100 + 120 - bw - 3, 63)
    # Бейдж не вылезает за ячейку.
    assert bx + bw <= 100 + 120
    assert by + bh <= 60 + 100


def test_back_button_exists_in_every_submenu(make_game):
    g = make_game()
    for mode in ('menu_saves', 'menu_settings', 'menu_controls',
                 'class_select', 'menu_multiplayer', 'menu_mp_join'):
        g.screen_mode = mode
        assert g.menu.back_button_rect() is not None, mode
    # У главного меню родителя нет — кнопка «Назад» не нужна.
    g.screen_mode = 'menu'
    assert g.menu.back_button_rect() is None


def test_back_button_returns_from_submenus(make_game, monkeypatch):
    import core.settings as settings_mod
    monkeypatch.setattr(settings_mod, 'save_settings', lambda *a, **k: None)
    g = make_game()

    def click_back(mode):
        g.screen_mode = mode
        rect = g.menu.back_button_rect()
        assert rect
        return g.menu.handle_mouse_click(rect[:2])

    assert click_back('menu_saves') is True
    assert g.screen_mode == 'menu'

    assert click_back('menu_settings') is True
    assert g.screen_mode == 'menu'

    assert click_back('menu_controls') is True
    assert g.screen_mode == 'menu_settings'

    assert click_back('menu_multiplayer') is True
    assert g.screen_mode == 'menu'

    assert click_back('menu_mp_join') is True
    assert g.screen_mode == 'menu_multiplayer'

    assert click_back('class_select') is True
    assert g.screen_mode == 'menu'
    assert g.current_slot == 1


def test_submenus_render_back_button_without_errors(make_game, renderer):
    g = make_game()
    for mode in ('menu_saves', 'menu_settings', 'menu_controls',
                 'class_select', 'menu_multiplayer', 'menu_mp_join'):
        g.screen_mode = mode
        renderer.draw(g)


# ── Полноэкранный режим мультиплеера не должен ронять игру ──
def test_display_falls_back_to_window_on_error(monkeypatch):
    import pygame
    import core.display as display_mod
    import core.settings as settings_mod

    class FakeSurface:
        def get_size(self):
            return (100, 100)

        def get_flags(self):
            return 0

    def fake_get_surface():
        return FakeSurface()

    def fake_set_mode(size, flags=0):
        # Полный экран ломается и в видеорежиме, и в borderless-окне
        if flags & (pygame.FULLSCREEN | pygame.NOFRAME):
            raise pygame.error("ChangeDisplaySettingsEx() failed")
        return FakeSurface()

    monkeypatch.setattr(display_mod.pygame.display, 'get_surface',
                        fake_get_surface)
    monkeypatch.setattr(display_mod.pygame.display, 'set_mode', fake_set_mode)
    saved = []
    monkeypatch.setattr(settings_mod, 'save_settings',
                        lambda s: saved.append(dict(s)))

    settings = {'fullscreen': True}
    surf = display_mod.set_mode((800, 600), True, settings)
    assert surf is not None
    # Настройки честно поправлены: следующий запуск не повторит ошибку.
    assert settings['fullscreen'] is False
    assert saved and saved[-1]['fullscreen'] is False


def test_display_reuses_matching_surface(monkeypatch):
    import pygame
    import core.display as display_mod

    class FakeSurface:
        def get_size(self):
            return (800, 600)

        def get_flags(self):
            return pygame.FULLSCREEN

    def fail(*args, **kwargs):
        raise AssertionError("set_mode не должен вызываться лишний раз")

    monkeypatch.setattr(display_mod.pygame.display, 'get_surface',
                        lambda: FakeSurface())
    monkeypatch.setattr(display_mod.pygame.display, 'set_mode', fail)
    assert display_mod.set_mode((800, 600), True) is not None


# ════════════════════ Босс-комплекс: арена, казна, дверь ════════════════════
def test_boss_complex_has_locked_treasury():
    from core.world import generate_dungeon
    d = generate_dungeon(3)
    locked = sum(1 for row in d.tiles for t in row if t == Tile.LOCKED_DOOR)
    chests = sum(1 for row in d.tiles for t in row if t == Tile.CHEST)
    assert locked == 2 and chests == 1
    assert d.barrier_tiles and d.boss_door_cells and d.treasury_pos
    # Сундук в казне рядом с лестницей вниз (казна ведёт глубже)
    tx, ty = d.treasury_pos
    assert d.tiles[ty][tx] == Tile.STAIRS
    radius = 2
    chest_near = any(
        d.tiles[ty + dy][tx + dx] == Tile.CHEST
        for dy in range(-radius, radius + 1)
        for dx in range(-radius, radius + 1)
        if 0 <= tx + dx < d.w and 0 <= ty + dy < d.h)
    assert chest_near
    # Босс ждёт в центре арены на максимальной глубине
    deep = generate_dungeon(5)
    assert any(m.boss for m in deep.monsters)


def test_boss_key_drops_always_and_unlocks_door(make_game):
    g = make_game()
    g.enter_dungeon()
    g.depth = 3
    monster = SimpleNamespace(ch='X', boss=True, name='Босс', xp_value=10)
    reward = rules.roll_kill_reward(monster, 3)
    assert 'boss_key' in reward.drops
    # Дверь заперта без ключа и открывается с ним
    for (x, y) in g.dungeon.boss_door_cells:
        if g.dungeon.tiles[y][x] != Tile.LOCKED_DOOR:
            continue
        assert g.move_player(x - g.player.x, y - g.player.y) is False
        g.player.inventory['boss_key'] = 1
        assert g.move_player(x - g.player.x, y - g.player.y) is True
        assert g.dungeon.tiles[y][x] == Tile.FLOOR
        break
    else:
        assert False, 'не нашли запертую дверь'


def test_boss_barriers_open_after_kill():
    from core.world import generate_dungeon, clear_boss_barriers
    d = generate_dungeon(3)
    assert clear_boss_barriers(d) is True
    assert not d.barrier_tiles
    assert clear_boss_barriers(d) is False


# ════════════════════ Кладбище и сетка локаций ════════════════════
def test_graveyard_location_and_walkable_grid(make_game):
    g = make_game()
    dead = g.get_location('dead')
    gates = {t['to']: t for t in dead.transitions.values()}
    assert 'graveyard' in gates
    gy = g.get_location('graveyard')
    assert gy.stairs_pos is not None
    # У каждой локации все переходы имеют сторону появления
    for key in ('pine', 'dead', 'graveyard', 'village'):
        loc = g.get_location(key)
        for tr in loc.transitions.values():
            assert tr.get('side') in ('n', 'e', 's', 'w'), (key, tr)


def test_gate_side_spawn_matches_side(make_game):
    g = make_game()
    # Нижний переход в деревню → появление в биоме у его «северных» врат
    g.travel_to('pine')
    entry = g.dungeon.transitions[(60, 66)]['entry']
    # врата деревни pine (8,8): сторона w → появление у восточных врат pine
    assert entry in (locations.SIDE_ENTRY['e'], locations.VILLAGE_INWARD['pine'])


def test_side_gate_walk_between_adjacent_locations(make_game):
    g = make_game()
    g.travel_to('pine')
    gate = locations.BIOME_GATES['pine'][0][0]      # восточные врата → birch
    assert g.dungeon.transitions[gate]['to'] == 'birch'
    g.player.x, g.player.y = gate[0] - 1, gate[1]
    g.move_player(1, 0)                      # шаг на врата = переход
    assert g.location == 'birch'
    # Вошли с запада — появились у западного края
    assert g.player.x <= 4 and g.player.y == locations.SIDE_ENTRY['w'][1]


def test_roads_have_no_items_harvestables_or_monsters(make_game):
    g = make_game()
    for key in ('pine', 'birch', 'dead', 'mine', 'village',
                'goblin_village'):
        loc = g.get_location(key)
        roads = locations.road_cells(key)
        assert not (set(loc.items) & roads), key
        assert not (set(loc.harvestables) & roads), key
        assert all((m.x, m.y) not in roads for m in loc.monsters), key


def test_new_visual_tiles_registered():
    from render import tiles
    for kind in ('arrow_n', 'arrow_e', 'arrow_s', 'arrow_w',
                 'mine_wall', 'mine_ground', 'locked_door', 'chest',
                 'tombstone'):
        assert kind in tiles._BUILDERS, kind
        assert tiles._VARIANTS.get(kind, 0) >= 1, kind


# ════════════════════ MP: обмен и общие квесты ════════════════════
def _mp_core(players=2):
    from net.core import GameCore
    core = GameCore(seed=42)
    for pid in range(players):
        core.add_player(pid, 'warrior' if pid == 0 else 'rogue')
    return core


def test_trade_executes_offers_and_gold():
    core = _mp_core()
    a, b = core.players[0], core.players[1]
    a.x = b.x + 1            # встать рядом
    a.inventory['potion_s'] = 2
    b.inventory['wood'] = 3
    b.gold = 50
    core.trade_request(0, 1)
    core.trade_accept(1)
    core.trade_offer_add(0, 'potion_s', 2)
    core.trade_offer_add(1, 'wood', 2)
    core.trade_gold(1, 40)
    core.trade_ready(0)
    core.trade_ready(1)
    assert core.trade is None
    assert a.inventory['wood'] == 2 and 'potion_s' not in a.inventory
    assert b.inventory['potion_s'] == 2 and b.inventory['wood'] == 1
    assert b.gold == 10 and a.gold >= 40


def test_trade_snapshot_serializes_pids():
    core = _mp_core()
    a, b = core.players[0], core.players[1]
    a.x = b.x + 1
    core.trade_request(0, 1)
    snap = core.get_snapshot()
    assert snap['trade']['players'] == ['0', '1']


def test_duel_request_only_logs():
    core = _mp_core()
    core.duel_request(0, 1)
    snap = core.get_snapshot()
    texts = [e['text'] for e in snap['events']]
    assert any('дуэль' in t for t in texts)


def test_mp_quests_shared_progress():
    core = _mp_core()
    log = core.quest_log
    p = core.players[0]
    # Разговор со старейшиной открывает его квесты (нужно стоять рядом)
    assert core.elder is not None
    p.x, p.y = core.elder.x + 1, core.elder.y
    core.quest_talk(0)
    assert log.available, 'старейшина не открыл квесты'
    key = next(iter(log.available))
    core.quest_accept(0, key)
    assert key in log.active
    # Прогресс общий: журнал в снапшоте у всего отряда
    assert key in core.get_snapshot()['quests']['active']
