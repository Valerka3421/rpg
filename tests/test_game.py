
import os
import random
from unittest.mock import patch

import pygame

from core.config import MAX_DEPTH, Tile, INVENTORY_PAGE_SIZE, SHOP_PAGE_SIZE
from core.entities import Entity

from render import Renderer


def test_new_game_state(make_game):
    g = make_game()
    assert g.world_mode == 'overworld'
    assert g.depth == 0
    assert not g.dead and not g.victory
    assert g.player.hp == g.player.max_hp


def test_no_merchant_on_depth_1(make_game):
    assert make_game().merchant is None


def test_merchant_on_depth_3(make_game):
    g = make_game()
    g.new_dungeon(3, autosave=False)
    assert g.merchant is not None and g.merchant.is_merchant


def test_log_truncates_50(make_game):
    g = make_game()
    for i in range(100):
        g.log(f"msg {i}")
    assert len(g.messages) == 50
    assert g.messages[-1].text == "msg 99"


def test_scroll_clamps(make_game):
    g = make_game()
    g.messages = []
    for i in range(20):
        g.log(f"msg {i}")
    g.scroll_log(10_000)
    assert g.log_scroll == 14
    g.scroll_log(-10_000)
    assert g.log_scroll == 0


def test_scroll_few_messages(make_game):
    g = make_game()
    g.messages = []
    g.log("a"); g.log("b")
    g.scroll_log(5)
    assert g.log_scroll == 0


def test_pageup(make_game):
    g = make_game()
    g.messages = []
    for i in range(20):
        g.log(f"msg {i}")
    g.handle_key(pygame.K_PAGEUP)
    assert g.log_scroll == 3


def test_home_end(make_game):
    g = make_game()
    g.messages = []
    for i in range(20):
        g.log(f"msg {i}")
    g.handle_key(pygame.K_HOME)
    assert g.log_scroll == 14
    g.handle_key(pygame.K_END)
    assert g.log_scroll == 0


def test_scroll_blocked_in_modal(make_game):
    g = make_game()
    g.messages = []
    for i in range(20):
        g.log(f"msg {i}")
    g.screen_mode = 'stats'
    g.handle_key(pygame.K_PAGEUP)
    assert g.log_scroll == 0


def test_start_in_overworld(make_game):
    g = make_game()
    assert g.world_mode == 'overworld'
    assert g.depth == 0
    assert g.dungeon is g.overworld


def test_overworld_has_entrance(make_game):
    g = make_game()
    rock = g.get_location('rock')
    assert rock.stairs_pos is not None
    ex, ey = rock.stairs_pos
    assert rock.tiles[ey][ex] == Tile.STAIRS


def test_enter_dungeon(make_game):
    g = make_game()
    g.enter_dungeon()
    assert g.world_mode == 'dungeon'
    assert g.depth == 1


def test_exit_at_depth_1(make_game):
    g = make_game()
    g.enter_dungeon()
    has_exit = any(t == Tile.EXIT for row in g.dungeon.tiles for t in row)
    assert has_exit


def test_exit_returns_to_overworld(make_game):
    g = make_game()
    g.enter_dungeon()
    g.exit_to_overworld()
    assert g.world_mode == 'overworld'
    assert g.dungeon is g.overworld


def test_dungeon_cache_prevents_respawn(make_game):
    g = make_game()
    g.enter_dungeon()
    depth1 = g.dungeon
    monsters_count = len(depth1.monsters)
    g.exit_to_overworld()
    g.enter_dungeon()
    assert g.dungeon is depth1
    assert len(g.dungeon.monsters) == monsters_count


def test_move_wall(make_game):
    g = make_game()
    px = g.player.x
    g.dungeon.tiles[g.player.y][px + 1] = Tile.WALL
    assert g.move_player(1, 0) is False
    assert g.player.x == px


def test_move_attacks(make_game):
    g = make_game()
    px, py = g.player.x, g.player.y
    g.dungeon.tiles[py][px + 1] = Tile.FLOOR
    m = Entity(px + 1, py, 100, 100, 0, 0, "T", "t", (255, 255, 255), 0)
    g.dungeon.monsters = [m]
    g.move_player(1, 0)
    assert m.hp < 100


def test_move_onto_merchant_blocked(make_game):
    g = make_game()
    g.merchant = Entity(g.player.x + 1, g.player.y, 1, 1, 0, 0,
                        "T", "M", (255, 200, 0))
    assert g.move_player(1, 0) is False


def test_pickup_gold(make_game):
    g = make_game()
    g.dungeon.items[(3, 3)] = 'gold_medium'
    g.pickup(3, 3)
    assert g.player.gold == 15


def test_pickup_resource(make_game):
    g = make_game()
    g.dungeon.items[(3, 3)] = 'mushroom'
    g.pickup(3, 3)
    assert g.player.inventory.get('mushroom') == 1


def test_pickup_material(make_game):
    g = make_game()
    g.dungeon.items[(3, 3)] = 'bone'
    g.pickup(3, 3)
    assert g.player.inventory.get('bone') == 1


def test_use_potion(make_game):
    g = make_game()
    g.player.hp = 10; g.player.max_hp = 25
    g.player.inventory = {'potion_s': 1}
    g.use_item('potion_s')
    assert g.player.hp == 20


def test_use_potion_full_hp(make_game):
    g = make_game()
    g.player.hp = g.player.max_hp
    g.player.inventory = {'potion_s': 1}
    g.use_item('potion_s')
    assert g.player.inventory == {'potion_s': 1}


def test_use_mushroom_heals(make_game):
    g = make_game()
    g.player.hp = 10; g.player.max_hp = 25
    g.player.inventory = {'mushroom': 1}
    g.use_item('mushroom')
    assert g.player.hp == 15
    assert 'mushroom' not in g.player.inventory


def test_use_wood_does_nothing(make_game):
    g = make_game()
    g.player.inventory = {'wood': 1}
    g.use_item('wood')
    assert g.player.inventory == {'wood': 1}


def test_equip_weapon(make_game):
    g = make_game()
    g.player.level = 5
    base = g.player.atk
    g.player.inventory = {'sword': 1}
    g.use_item('sword')
    assert g.player.weapon == 'sword'
    assert g.player.atk == base + 2


def test_equip_rare_weapon(make_game):
    g = make_game()
    g.player.level = 5
    base = g.player.atk
    g.player.inventory = {'sword+2': 1}
    g.use_item('sword+2')
    assert g.player.weapon == 'sword+2'
    assert g.player.atk == base + 4


def test_weapon_level_requirement(make_game):
    g = make_game()
    g.player.level = 1
    g.player.inventory = {'sword': 1}
    g.use_item('sword')
    assert g.player.weapon is None


def test_armor_level_requirement(make_game):
    g = make_game()
    g.player.level = 1
    g.player.inventory = {'plate': 1}
    g.use_item('plate')
    assert g.player.armor is None


def test_swap_weapon(make_game):
    g = make_game()
    g.player.level = 5
    g.player.weapon = 'dagger'
    g.player.recalc()
    g.player.inventory = {'sword': 1}
    g.use_item('sword')
    assert g.player.weapon == 'sword'
    assert g.player.inventory.get('dagger') == 1


def test_unequip_weapon(make_game):
    g = make_game()
    g.player.weapon = 'sword'; g.player.recalc()
    base = g.player.atk; g.player.inventory = {}
    g.unequip_weapon()
    assert g.player.weapon is None
    assert g.player.inventory.get('sword') == 1
    assert g.player.atk < base


def test_unequip_armor(make_game):
    g = make_game()
    g.player.armor = 'plate'; g.player.recalc()
    base = g.player.df; g.player.inventory = {}
    g.unequip_armor()
    assert g.player.armor is None and g.player.df < base


def test_unequip_no_weapon(make_game):
    g = make_game()
    g.unequip_weapon()
    assert g.player.weapon is None


def test_unequip_via_key_u(make_game):
    g = make_game()
    g.player.weapon = 'sword'; g.player.recalc()
    g.player.inventory = {}
    g.screen_mode = 'inventory'
    g.handle_key(pygame.K_u)
    assert g.player.weapon is None


def test_unequip_via_key_v(make_game):
    g = make_game()
    g.player.armor = 'chain'; g.player.recalc()
    g.player.inventory = {}
    g.screen_mode = 'inventory'
    g.handle_key(pygame.K_v)
    assert g.player.armor is None


def test_use_book_stat(make_game):
    g = make_game()
    base = g.player.str
    g.player.inventory = {'book_str': 1}
    g.use_item('book_str')
    assert g.player.str == base + 5


def test_use_book_def(make_game):
    g = make_game()
    base = g.player.df_stat
    g.player.inventory = {'book_def': 1}
    g.use_item('book_def')
    assert g.player.df_stat == base + 5


def test_use_book_perk(make_game):
    g = make_game()
    g.player.inventory = {'book_double': 1}
    g.use_item('book_double')
    assert g.player.double_strike is True


def test_use_book_perk_duplicate(make_game):
    g = make_game()
    g.player.double_strike = True
    g.player.inventory = {'book_double': 1}
    g.use_item('book_double')
    assert g.player.inventory == {'book_double': 1}


def test_quick_potion_weakest(make_game):
    g = make_game()
    g.player.hp = 10; g.player.max_hp = 25
    g.player.inventory = {'potion_l': 1, 'potion_s': 1}
    g.quick_potion()
    assert g.player.hp == 20
    assert 'potion_s' not in g.player.inventory
    assert 'potion_l' in g.player.inventory


def test_allocate_str(make_game):
    g = make_game()
    g.player.free_points = 5
    base = g.player.str
    g.allocate_stat('str')
    assert g.player.str == base + 1
    assert g.player.free_points == 4


def test_allocate_no_points(make_game):
    g = make_game()
    g.player.free_points = 0
    base = g.player.str
    g.allocate_stat('str')
    assert g.player.str == base


def test_allocate_df_stat(make_game):
    g = make_game()
    g.player.free_points = 5
    base = g.player.df_stat
    g.allocate_stat('df_stat')
    assert g.player.df_stat == base + 1


def test_allocate_df_stat_direct(make_game):
    g = make_game()
    g.player.free_points = 5
    base = g.player.df_stat
    g.allocate_stat('df_stat')
    assert g.player.df_stat == base + 1


def test_allocate_via_click(make_game):
    """Клик по кнопке стата в окне статов."""
    from core.config import PLUS_SIZE
    g = make_game()
    g.player.free_points = 5
    base = g.player.df_stat
    g.screen_mode = 'stats'
    # Рендер не вызывается в тестах, поэтому эмулируем hit вручную:
    # добавляем в _ui_hits вручную
    g._ui_hits = {'stats': {'plus': [
        ('df_stat', (100, 100, PLUS_SIZE + 4, PLUS_SIZE + 4))
    ]}}
    g.handle_mouse_click((110, 110))
    assert g.player.df_stat == base + 1


def test_level_up(make_game):
    g = make_game()
    g.player.xp = g.player.xp_next
    g.player.free_points = 0
    g.player.hp = 5
    old = g.player.level
    g.level_up()
    assert g.player.level == old + 1
    assert g.player.free_points == 5
    assert g.player.hp == g.player.max_hp


def test_rarity_parse():
    from core.data import parse_item_key, make_loot_key, item_display_name
    assert parse_item_key('sword') == ('sword', 0)
    assert parse_item_key('sword+2') == ('sword', 2)
    assert make_loot_key('sword', 0) == 'sword'
    assert make_loot_key('sword', 2) == 'sword+2'
    assert item_display_name('sword+2') == 'Меч +2'


def test_rarity_color():
    from core.data import item_display_color, RARITY_COLORS, ITEMS
    assert item_display_color('sword+1') == RARITY_COLORS[1]
    assert item_display_color('sword+2') == RARITY_COLORS[2]
    assert item_display_color('sword+3') == RARITY_COLORS[3]
    assert item_display_color('sword') == ITEMS['sword']['color']


def test_rarity_sell_price():
    from core.data import item_sell_price, ITEMS
    assert item_sell_price('sword') == ITEMS['sword']['sell']
    assert item_sell_price('sword+2') == ITEMS['sword']['sell'] + 20


def test_open_shop_adjacent(make_game):
    g = make_game()
    g.merchant = Entity(g.player.x + 1, g.player.y, 1, 1, 0, 0,
                        "T", "M", (255, 200, 0))
    g.open_shop()
    assert g.screen_mode == 'shop'


def test_open_shop_too_far(make_game):
    g = make_game()
    g.merchant = Entity(50, 50, 1, 1, 0, 0, "T", "M", (255, 200, 0))
    g.player.x, g.player.y = 0, 0
    g.open_shop()
    assert g.screen_mode == 'game'


def test_open_shop_no_merchant(make_game):
    g = make_game()
    g.open_shop()
    assert g.screen_mode == 'game'


def test_buy_item(make_game):
    g = make_game()
    g.player.gold = 1000
    g.shop_stock = [{'key': 'potion_s', 'price': 5, 'sold': False}]
    g.buy_item(0)
    assert g.player.gold == 995
    assert g.player.inventory['potion_s'] == 1
    assert g.shop_stock[0]['sold'] is True


def test_buy_item_not_enough_gold(make_game):
    g = make_game()
    g.player.gold = 1
    g.shop_stock = [{'key': 'sword', 'price': 100, 'sold': False}]
    g.buy_item(0)
    assert g.player.gold == 1
    assert 'sword' not in g.player.inventory


def test_buy_sold_item(make_game):
    g = make_game()
    g.player.gold = 1000
    g.shop_stock = [{'key': 'sword', 'price': 100, 'sold': True}]
    g.buy_item(0)
    assert g.player.gold == 1000


def test_sell_item(make_game):
    g = make_game()
    g.player.gold = 0
    g.player.inventory = {'potion_s': 1}
    g.sell_item(0)
    assert g.player.gold == 2


def test_sell_last_copy_removes(make_game):
    g = make_game()
    g.player.gold = 0
    g.player.inventory = {'potion_s': 2}
    g.sell_item(0)
    assert g.player.inventory['potion_s'] == 1


def test_sell_rare_item(make_game):
    from core.data import item_sell_price
    g = make_game()
    g.player.gold = 0
    g.player.inventory = {'sword+2': 1}
    expected = item_sell_price('sword+2')
    g.sell_item(0)
    assert g.player.gold == expected


def test_reroll_shop(make_game):
    g = make_game()
    g.depth = 3
    g.player.gold = 100
    g.shop_stock = [{'key': 'potion_s', 'price': 5, 'sold': True}]
    g.reroll_shop()
    assert g.player.gold == 76
    assert len(g.shop_stock) >= 4


def test_reroll_not_enough_gold(make_game):
    g = make_game()
    g.depth = 3
    g.player.gold = 5
    old = [{'key': 'potion_s', 'price': 5, 'sold': False}]
    g.shop_stock = old
    g.reroll_shop()
    assert g.player.gold == 5
    assert g.shop_stock is old


def test_inventory_pagination(make_game):
    g = make_game()
    g.player.inventory = {f'item_{i}': 1 for i in range(30)}
    assert g.inv_pages_count() == 2
    g.inv_page = 0
    g.inv_next_page();
    assert g.inv_page == 1
    g.inv_next_page();
    assert g.inv_page == 1
    g.inv_prev_page();
    assert g.inv_page == 0
    g.inv_prev_page();
    assert g.inv_page == 0


def test_shop_pagination(make_game):
    g = make_game()
    g.shop_stock = [{'key': 'potion_s', 'price': 5, 'sold': False}
                    for _ in range(20)]
    g.shop_submode = 'buy'
    g.shop.page = 0
    g.screen_mode = 'shop'
    expected = (20 + SHOP_PAGE_SIZE - 1) // SHOP_PAGE_SIZE
    assert g.shop.pages_count() == expected
    g.handle_key(pygame.K_e)
    assert g.shop.page == 1
    g.handle_key(pygame.K_q)
    assert g.shop.page == 0


def test_sell_on_second_page(make_game):
    from core.config import SHOP_PAGE_SIZE
    g = make_game()
    g.player.inventory = {
        'potion_s': 1, 'potion_m': 1, 'potion_l': 1,
        'dagger': 1, 'shortsword': 1, 'sword': 1,
        'axe': 1, 'greatsword': 1, 'warhammer': 1,
        'padded': 1, 'leather': 1, 'chain': 1,
    }
    assert len(g.player.inventory) == 12
    g.screen_mode = 'shop'
    g.shop_submode = 'sell'
    g.shop.page = 1
    sorted_items = sorted(g.player.inventory.items())
    first_on_page2 = sorted_items[SHOP_PAGE_SIZE][0]
    gold_before = g.player.gold
    g.sell_item(0)          # первый на текущей (2-й) странице
    assert g.player.inventory.get(first_on_page2, 0) == 0
    assert g.player.gold > gold_before


def test_inventory_page_size_is_25():
    assert INVENTORY_PAGE_SIZE == 25


def test_craft_screen_opens(make_game):
    g = make_game()
    g.handle_key(pygame.K_k)
    assert g.screen_mode == 'craft'


def test_craft_screen_closes_escape(make_game):
    g = make_game()
    g.screen_mode = 'craft'
    g.handle_key(pygame.K_ESCAPE)
    assert g.screen_mode == 'game'


def test_craft_screen_closes_k(make_game):
    g = make_game()
    g.screen_mode = 'craft'
    g.handle_key(pygame.K_k)
    assert g.screen_mode == 'game'


def test_craftable_list_returns_recipes(make_game):
    g = make_game()
    craftable = g.craftable_list()
    assert len(craftable) >= 3
    keys = [c[0] for c in craftable]
    assert 'bone_sword' in keys
    assert 'leather_armor' in keys


def test_craft_bone_sword(make_game):
    g = make_game()
    g.player.level = 5                  # костяной меч требует 3-й уровень
    g.player.inventory = {'bone': 2, 'wood': 1}
    craftable = g.craftable_list()
    bone_idx = next((i for i, c in enumerate(craftable) if c[0] == 'bone_sword'), None)
    assert bone_idx is not None
    assert craftable[bone_idx][3] is True
    g.craft_item(bone_idx)
    assert g.player.inventory.get('bone_sword') == 1
    assert 'bone' not in g.player.inventory
    assert 'wood' not in g.player.inventory


def test_craft_requires_level(make_game):
    """Крафт уважает level_req — как показывает окно крафта."""
    g = make_game()
    g.player.level = 1
    g.player.inventory = {'bone': 2, 'wood': 1}
    craftable = g.craftable_list()
    bone_idx = next(i for i, c in enumerate(craftable) if c[0] == 'bone_sword')
    assert craftable[bone_idx][3] is False
    g.craft_item(bone_idx)
    assert 'bone_sword' not in g.player.inventory
    assert g.player.inventory == {'bone': 2, 'wood': 1}


def test_craft_not_enough_materials(make_game):
    g = make_game()
    g.player.inventory = {'bone': 1}
    craftable = g.craftable_list()
    bone_idx = next((i for i, c in enumerate(craftable) if c[0] == 'bone_sword'), None)
    assert craftable[bone_idx][3] is False
    g.craft_item(bone_idx)
    assert 'bone_sword' not in g.player.inventory


def test_craft_via_click(make_game):
    """Клик по молоточку в окне крафта."""
    from core.config import HAMMER_SIZE
    g = make_game()
    g.player.level = 5
    g.player.inventory = {'bone': 2, 'wood': 1}
    g.screen_mode = 'craft'
    craftable = g.craftable_list()
    bone_idx = next(i for i, c in enumerate(craftable) if c[0] == 'bone_sword')
    # Эмулируем hit-rect для молоточка
    g._ui_hits = {'craft': {'hammer': [
        (bone_idx, (100, 100, HAMMER_SIZE, HAMMER_SIZE))
    ]}}
    g.handle_mouse_click((110, 110))
    assert g.player.inventory.get('bone_sword') == 1


def test_craft_missing_materials_keeps_inventory(make_game):
    g = make_game()
    g.player.level = 5
    g.player.inventory = {'bone': 1}
    craftable = g.craftable_list()
    bone_idx = next(i for i, c in enumerate(craftable) if c[0] == 'bone_sword')
    g.craft_item(bone_idx)
    assert g.player.inventory == {'bone': 1}


def test_material_drops_from_skeleton():
    from core.data import MONSTER_DROPS
    assert 'bone' in [k for k, _ in MONSTER_DROPS['s']]


def test_material_drops_from_troll():
    from core.data import MONSTER_DROPS
    assert 'ogre_leather' in [k for k, _ in MONSTER_DROPS['T']]


def test_boss_drops_dragon_scale():
    from core.data import BOSS_DROPS
    assert 'dragon_scale' in BOSS_DROPS['A']


def test_boss_drops_vampire_fang():
    from core.data import BOSS_DROPS
    assert 'vampire_fang' in BOSS_DROPS['V']


def test_roll_monster_drops_returns_list():
    from core.data import roll_monster_drops
    assert isinstance(roll_monster_drops('r'), list)


def test_drop_on_stairs_goes_to_inventory(make_game):
    g = make_game()
    g.enter_dungeon()
    sx, sy = g.dungeon.stairs_pos
    m = Entity(sx, sy, 1, 1, 0, 0, "РљСЂС‹СЃР°", "r", (170, 150, 130), 3)
    g.dungeon.monsters = [m]
    random.seed(0)
    g._kill_monster(m)
    assert (sx, sy) not in g.dungeon.items


def test_drop_on_floor_goes_to_tile(make_game):
    g = make_game()
    g.enter_dungeon()
    mx = my = None
    for y in range(5, 30):
        for x in range(5, 30):
            if g.dungeon.tiles[y][x] == Tile.FLOOR:
                mx, my = x, y
                break
        if mx is not None:
            break
    m = Entity(mx, my, 1, 1, 0, 0, "РљСЂС‹СЃР°", "r", (170, 150, 130), 3)
    g.dungeon.monsters = [m]
    random.seed(0)
    g._kill_monster(m)


def test_place_drop_on_stairs_goes_to_inventory(make_game):
    g = make_game()
    g.enter_dungeon()
    sx, sy = g.dungeon.stairs_pos
    before = g.player.inventory.get('potion_s', 0)
    g._place_drop(sx, sy, 'potion_s')
    assert g.player.inventory.get('potion_s', 0) == before + 1
    assert (sx, sy) not in g.dungeon.items


def test_place_drop_on_free_tile(make_game):
    g = make_game()
    g.enter_dungeon()
    for y in range(5, 30):
        for x in range(5, 30):
            if (g.dungeon.tiles[y][x] == Tile.FLOOR
                    and (x, y) not in g.dungeon.items):
                g._place_drop(x, y, 'mushroom')
                assert g.dungeon.items.get((x, y)) == 'mushroom'
                return
    raise AssertionError("РќРµ РЅР°С€Р»Рё СЃРІРѕР±РѕРґРЅСѓСЋ РєР»РµС‚РєСѓ")


def test_place_drop_on_occupied_finds_neighbor(make_game):
    g = make_game()
    g.enter_dungeon()
    for y in range(5, 30):
        for x in range(5, 30):
            if g.dungeon.tiles[y][x] == Tile.FLOOR:
                g.dungeon.items[(x, y)] = 'potion_s'
                inv_before = g.player.inventory.get('mushroom', 0)
                g._place_drop(x, y, 'mushroom')
                on_map = any(v == 'mushroom' for v in g.dungeon.items.values())
                in_inv = g.player.inventory.get('mushroom', 0) > inv_before
                assert on_map or in_inv
                return


def test_place_drop_doesnt_overwrite_potion_l(make_game):
    g = make_game()
    g.enter_dungeon()
    for y in range(5, 30):
        for x in range(5, 30):
            if g.dungeon.tiles[y][x] == Tile.FLOOR:
                g._place_drop(x, y, 'dragon_scale')
                g._place_drop(x, y, 'potion_l')
                values = list(g.dungeon.items.values()) + list(g.player.inventory.keys())
                assert 'dragon_scale' in values
                assert 'potion_l' in values
                return


def test_kill_monster_drops_all_items(make_game):
    g = make_game()
    g.enter_dungeon()
    sx, sy = g.dungeon.stairs_pos
    m = Entity(sx - 1, sy, 1, 1, 0, 0, "РљСЂС‹СЃР°", "r", (170, 150, 130), 3)
    g.dungeon.monsters = [m]
    g.dungeon.items[(sx - 1, sy)] = 'potion_s'
    random.seed(1)
    inv_before = sum(g.player.inventory.values())
    items_before = len(g.dungeon.items)
    g._kill_monster(m)
    inv_after = sum(g.player.inventory.values())
    items_after = len(g.dungeon.items)
    assert inv_after + items_after >= inv_before + items_before


def test_death_when_hp_zero(make_game):
    g = make_game()
    g.player.hp = 1
    m = Entity(0, 0, 100, 100, 999, 0, "T", "t", (255, 255, 255), 0)
    with patch('game.game.random.random', return_value=0.5):
        g._single_attack(m, g.player)
    assert g.dead is True


def test_victory_on_dragon(make_game):
    g = make_game()
    g.depth = MAX_DEPTH
    dragon = Entity(0, 0, 1, 1, 0, 0, "Р”СЂРµРІРЅРёР№ РґСЂР°РєРѕРЅ", "A",
                    (255, 255, 255), 1200, boss=True)
    g.dungeon.monsters = [dragon]
    with patch('game.game.random.random', return_value=0.5):
        g.attack(g.player, dragon)
    assert g.victory is True


def test_double_strike(make_game):
    g = make_game()
    g.player.double_strike = True
    m = Entity(0, 0, 200, 200, 0, 0, "T", "t", (255, 255, 255), 0)
    calls = {'n': 0}
    orig = g._single_attack
    def counter(a, b):
        calls['n'] += 1
        orig(a, b)
    g._single_attack = counter
    with patch('game.game.random.random', return_value=0.0):
        g.attack(g.player, m)
    assert calls['n'] == 2


def test_vampire_heals(make_game):
    g = make_game()
    g.player.hp = 10; g.player.vampire = True; g.player.atk = 20
    m = Entity(0, 0, 100, 100, 0, 0, "T", "t", (255, 255, 255), 0)
    with patch('game.game.random.random', return_value=0.5):
        g._single_attack(g.player, m)
    assert g.player.hp > 10


def test_thorns_damage(make_game):
    g = make_game()
    g.player.thorns = True
    m = Entity(0, 0, 50, 50, 5, 0, "T", "t", (255, 255, 255), 0)
    with patch('game.game.random.random', return_value=0.5):
        g._single_attack(m, g.player)
    assert m.hp == 48


def test_greedy_more_gold(make_game):
    random.seed(42)
    g = make_game()
    g.player.greedy = True
    m = Entity(0, 0, 1, 1, 0, 0, "T", "t", (255, 255, 255), 0)
    gold_before = g.player.gold
    g._kill_monster(m)
    assert g.player.gold - gold_before >= 3


def test_has_los_strict_diagonal():
    from core.entities import Dungeon
    from core.world import has_los
    from core.config import MAP_W, MAP_H
    d = Dungeon(MAP_W, MAP_H)
    for y in range(5):
        for x in range(5):
            d.tiles[y][x] = Tile.FLOOR
    d.tiles[1][0] = Tile.WALL
    d.tiles[0][1] = Tile.WALL
    assert not has_los(d, 0, 0, 1, 1)


def test_has_los_clear_path():
    from core.entities import Dungeon
    from core.world import has_los
    from core.config import MAP_W, MAP_H
    d = Dungeon(MAP_W, MAP_H)
    for y in range(5):
        for x in range(5):
            d.tiles[y][x] = Tile.FLOOR
    assert has_los(d, 0, 0, 3, 3)


def test_save_load_roundtrip(make_game, tmp_path):
    g = make_game()
    g.player.gold = 123
    g.player.str = 99
    g.player.inventory = {'potion_s': 5, 'sword+2': 1, 'bone': 3}
    g.depth = 3
    path = str(tmp_path / "save.json")
    g.save(path=path)
    g.player.gold = 0; g.player.str = 1; g.player.inventory = {}
    g.depth = 1
    g.load(path=path)
    assert g.player.gold == 123
    assert g.player.str == 99
    assert g.player.inventory == {'potion_s': 5, 'sword+2': 1, 'bone': 3}


def test_load_missing_file(make_game, tmp_path):
    assert make_game().load(path=str(tmp_path / "nope.json")) is False


def test_save_dead_does_nothing(make_game, tmp_path):
    g = make_game()
    g.dead = True
    path = str(tmp_path / "save.json")
    g.save(path=path)
    assert not os.path.exists(path)


def test_save_load_two_worlds(make_game, tmp_path):
    g = make_game()
    g.enter_dungeon()
    path = str(tmp_path / "save.json")
    g.save(path=path)
    g.world_mode = 'overworld'
    g.dungeon = g.overworld
    g.load(path=path)
    assert g.world_mode == 'dungeon'
    assert g.depth == 1


def test_load_resets_screen_mode(make_game, tmp_path):
    g = make_game()
    path = str(tmp_path / "save.json")
    g.save(path=path)
    g.screen_mode = 'shop'; g.shop_submode = 'sell'
    g.load(path=path)
    assert g.screen_mode == 'game'
    assert g.shop_submode == 'buy'


def test_dungeon_cache_saved(make_game, tmp_path):
    g = make_game()
    g.enter_dungeon()
    g.new_dungeon(2, autosave=False)
    g.current_slot = 1
    path = str(tmp_path / "save.json")
    g.save(path=path)
    g.dungeon_cache = {}
    g.load(path=path)
    assert 1 in g.dungeon_cache
    assert 2 in g.dungeon_cache


def test_dungeon_cache_limited_in_save(make_game, tmp_path):
    import json
    g = make_game()
    g.enter_dungeon()
    for depth in range(2, 11):
        g.new_dungeon(depth, autosave=False)
    assert len(g.dungeon_cache) >= 5
    g.current_slot = 1
    path = str(tmp_path / "save.json")
    g.save(path=path)
    with open(path, 'r', encoding='utf-8') as f:
        data = json.load(f)
    assert len(data['dungeon_cache']) <= 3


def test_class_warrior(make_game):
    g = make_game()
    g.new_game(class_key='warrior')
    assert g.player.class_key == 'warrior'
    assert g.player.str == 8
    assert g.player.armor == 'padded'
    assert g.player.weapon is None


def test_class_assassin(make_game):
    g = make_game()
    g.new_game(class_key='assassin')
    assert g.player.class_key == 'assassin'
    assert g.player.luk == 9
    assert g.player.weapon == 'dagger'


def test_class_rogue(make_game):
    g = make_game()
    g.new_game(class_key='rogue')
    assert g.player.class_key == 'rogue'
    assert g.player.weapon == 'dagger'
    assert g.player.agi == 9


def test_class_selection_screen(make_game):
    g = make_game()
    g.menu_start_new()
    assert g.screen_mode == 'class_select'
    g.menu.class_selection = 1
    g.class_select_confirm()
    assert g.screen_mode == 'game'
    assert g.player.class_key == 'assassin'


def test_class_saved_and_loaded(make_game, tmp_path):
    g = make_game()
    g.new_game(class_key='paladin')
    path = str(tmp_path / "save.json")
    g.save(path=path)
    g.new_game(class_key='warrior')
    g.load(path=path)
    assert g.player.class_key == 'paladin'


def test_preview_stats_matches_recalc(make_game):
    from core.entities import Player
    for class_key in ('warrior', 'assassin', 'rogue', 'paladin', 'default'):
        p = Player(class_key=class_key)
        stats = Player.preview_stats({
            'str': p.str, 'agi': p.agi, 'vit': p.vit,
            'luk': p.luk, 'df_stat': p.df_stat,
            'weapon': p.weapon, 'armor': p.armor,
        })
        assert stats['max_hp'] == p.max_hp
        assert stats['atk'] == p.atk
        assert stats['df'] == p.df
        assert abs(stats['dodge'] - p.dodge) < 1e-6
        assert abs(stats['crit'] - p.crit) < 1e-6


def test_open_stats_screen(make_game):
    g = make_game()
    g.handle_key(pygame.K_c)
    assert g.screen_mode == 'stats'


def test_close_stats_screen(make_game):
    g = make_game()
    g.screen_mode = 'stats'
    g.handle_key(pygame.K_ESCAPE)
    assert g.screen_mode == 'game'


def test_open_inventory_screen(make_game):
    g = make_game()
    g.handle_key(pygame.K_i)
    assert g.screen_mode == 'inventory'


def test_shop_toggle_buy_sell(make_game):
    g = make_game()
    g.screen_mode = 'shop'; g.shop_submode = 'buy'
    g.handle_key(pygame.K_s)
    assert g.shop_submode == 'sell'
    g.handle_key(pygame.K_s)
    assert g.shop_submode == 'buy'


def test_craft_key_in_game(make_game):
    g = make_game()
    g.handle_key(pygame.K_k)
    assert g.screen_mode == 'craft'


def test_save_slot_info(make_game, tmp_path, monkeypatch):
    from core import save as save_mod
    monkeypatch.setattr(save_mod, 'BASE_DIR', str(tmp_path))
    g = make_game()
    g.new_game(class_key='warrior')
    g.current_slot = 1
    g.save()
    info = save_mod.save_slot_info(1)
    assert info is not None
    assert info['class_key'] == 'warrior'


def test_update_clears_expired_floats(make_game):
    g = make_game()
    g.add_float(0, 0, "+1", (255, 255, 255))
    assert len(g.floats) == 1
    g.update(1.0)
    assert len(g.floats) == 0


def test_update_keeps_fresh_floats(make_game):
    g = make_game()
    g.add_float(0, 0, "+1", (255, 255, 255))
    g.update(0.3)
    assert len(g.floats) == 1

def test_entity_add_status(make_game):
    g = make_game()
    g.player.add_status('poison')
    assert g.player.has_status('poison')
    assert g.player.statuses['poison'] == 3


def test_entity_add_status_extends(make_game):
    g = make_game()
    g.player.add_status('poison', 2)
    g.player.add_status('poison', 5)
    assert g.player.statuses['poison'] == 5


def test_tick_player_poison(make_game):
    g = make_game()
    g.player.hp = 20
    g.player.add_status('poison', 3)
    g._tick_player_statuses()
    assert g.player.hp == 19
    assert g.player.statuses['poison'] == 2


def test_tick_status_expires(make_game):
    g = make_game()
    g.player.hp = 20
    g.player.add_status('poison', 1)
    g._tick_player_statuses()
    assert g.player.hp == 19
    assert not g.player.has_status('poison')


def test_tick_burning_damage(make_game):
    g = make_game()
    g.player.hp = 20
    g.player.add_status('burning', 2)
    g._tick_player_statuses()
    assert g.player.hp == 17


def test_tick_regen_heals(make_game):
    g = make_game()
    g.player.hp = 10
    g.player.add_status('regen', 3)
    g._tick_player_statuses()
    assert g.player.hp == 12


def test_player_dies_from_poison(make_game):
    g = make_game()
    g.player.hp = 1
    g.player.add_status('burning', 1)
    g._tick_player_statuses()
    assert g.dead is True


def test_monster_status_ticks(make_game):
    from core.entities import Entity
    g = make_game()
    g.enter_dungeon()
    m = Entity(5, 5, 10, 10, 0, 0, "T", "t", (255,255,255), 0)
    m.add_status('burning', 2)
    g.dungeon.monsters = [m]
    g._tick_monster_statuses(m)
    assert m.hp == 7
    assert m.statuses['burning'] == 1


def test_poison_ability_applied(make_game):
    from core.entities import Entity
    g = make_game()
    m = Entity(g.player.x + 1, g.player.y, 10, 10, 5, 0,
               "РљСЂС‹СЃР°", "r", (170,150,130), 3, ability='poison')
    g.dungeon.monsters = [m]
    with patch('game.game.random.random', return_value=0.5):
        g.attack(m, g.player)
    assert g.player.has_status('poison')


def test_ranged_monster_attacks_from_distance(make_game):
    from core.entities import Entity
    g = make_game()
    g.enter_dungeon()
    px, py = g.player.x, g.player.y
    for x in (px + 1, px + 2, px + 3):
        g.dungeon.tiles[py][x] = Tile.FLOOR
    archer = Entity(px + 3, py, 10, 10, 3, 0,
                    "Р›СѓС‡РЅРёРє", "a", (150,200,100), 5, ability='ranged')
    g.dungeon.monsters = [archer]
    hp_before = g.player.hp
    with patch('game.game.random.random', return_value=0.5):
        g.monsters_turn()
    assert g.player.hp < hp_before


def test_healer_heals_ally(make_game):
    from core.entities import Entity
    g = make_game()
    g.enter_dungeon()
    px, py = g.player.x, g.player.y
    healer = Entity(px + 2, py, 10, 10, 2, 1,
                    "РЁР°РјР°РЅ", "S", (180,140,220), 5, ability='healer')
    wounded = Entity(px + 2, py + 1, 5, 20, 0, 0,
                     "Р“РѕР±Р»РёРЅ", "g", (120,190,100), 6)
    g.dungeon.monsters = [healer, wounded]
    hp_before = wounded.hp
    with patch('game.game.random.random', return_value=0.5):
        g.monsters_turn()
    assert wounded.hp > hp_before


def test_slow_skips_monster_turn(make_game):
    from core.entities import Entity
    g = make_game()
    g.enter_dungeon()
    px, py = g.player.x, g.player.y
    m = Entity(px + 1, py, 100, 100, 10, 0,
               "T", "t", (255,255,255), 0)
    m.add_status('slow', 2)
    g.dungeon.monsters = [m]
    hp_before = g.player.hp
    g.monsters_turn()
    assert g.player.hp == hp_before


def test_scroll_heal(make_game):
    g = make_game()
    g.player.hp = 10
    g.player.max_hp = 25
    g.player.inventory = {'scroll_heal': 1}
    g.use_item('scroll_heal')
    assert g.player.hp == 25
    assert 'scroll_heal' not in g.player.inventory


def test_scroll_heal_full_hp_noop(make_game):
    g = make_game()
    g.player.hp = g.player.max_hp
    g.player.inventory = {'scroll_heal': 1}
    g.use_item('scroll_heal')
    assert g.player.inventory == {'scroll_heal': 1}


def test_scroll_fireball_hits_target(make_game):
    from core.entities import Entity
    g = make_game()
    g.enter_dungeon()
    px, py = g.player.x, g.player.y
    m = Entity(px + 3, py, 30, 30, 0, 0,
               "T", "t", (255,255,255), 0)
    g.dungeon.monsters = [m]
    g.player.inventory = {'scroll_fireball': 1}
    g.use_item('scroll_fireball')
    assert m.hp < 30
    assert 'scroll_fireball' not in g.player.inventory


def test_scroll_fireball_aoe(make_game):
    from core.entities import Entity
    g = make_game()
    g.enter_dungeon()
    px, py = g.player.x, g.player.y
    m1 = Entity(px + 3, py, 30, 30, 0, 0, "T1", "t", (255,255,255), 0)
    m2 = Entity(px + 4, py + 1, 30, 30, 0, 0, "T2", "t", (255,255,255), 0)
    g.dungeon.monsters = [m1, m2]
    g.player.inventory = {'scroll_fireball': 1}
    g.use_item('scroll_fireball')
    assert m1.hp < 30
    assert m2.hp < 30


def test_scroll_fireball_applies_burning(make_game):
    from core.entities import Entity
    g = make_game()
    g.enter_dungeon()
    px, py = g.player.x, g.player.y
    m = Entity(px + 3, py, 30, 30, 0, 0, "T", "t", (255,255,255), 0)
    g.dungeon.monsters = [m]
    g.player.inventory = {'scroll_fireball': 1}
    g.use_item('scroll_fireball')
    assert m.has_status('burning')


def test_scroll_freeze_slows_nearby(make_game):
    from core.entities import Entity
    g = make_game()
    g.enter_dungeon()
    px, py = g.player.x, g.player.y
    m1 = Entity(px + 2, py, 30, 30, 0, 0, "T1", "t", (255,255,255), 0)
    m2 = Entity(px + 10, py, 30, 30, 0, 0, "T2", "t", (255,255,255), 0)
    g.dungeon.monsters = [m1, m2]
    g.player.inventory = {'scroll_freeze': 1}
    g.use_item('scroll_freeze')
    assert m1.has_status('slow')
    assert not m2.has_status('slow')


def test_scroll_fireball_no_target_keeps_scroll(make_game):
    g = make_game()
    g.enter_dungeon()
    g.dungeon.monsters = []
    g.player.inventory = {'scroll_fireball': 1}
    g.use_item('scroll_fireball')
    assert 'scroll_fireball' in g.player.inventory

def test_death_summary_populated(make_game):
    g = make_game()
    g.kills = 5
    g.boss_kills = 1
    g.player.gold = 250
    g.death_time = g.start_time + 60
    s = g.death_summary()
    assert s['kills'] == 5
    assert s['boss_kills'] == 1
    assert s['gold'] == 250
    assert 55 <= s['time_seconds'] <= 65


def test_kill_increments_counter(make_game):
    from core.entities import Entity
    g = make_game()
    m = Entity(0, 0, 1, 1, 0, 0, "T", "t", (255,255,255), 0)
    initial = g.kills
    g._kill_monster(m)
    assert g.kills == initial + 1


def test_boss_kill_counter(make_game):
    from core.entities import Entity
    g = make_game()
    g.depth = 5
    boss = Entity(0, 0, 1, 1, 0, 0, "Р‘РѕСЃСЃ", "G", (255,255,255), 100, boss=True)
    initial = g.boss_kills
    g._kill_monster(boss)
    assert g.boss_kills == initial + 1


def test_compare_weapon_returns_delta(make_game):
    from render.renderer import Renderer
    g = make_game()
    g.player.weapon = None
    g.player.recalc()
    r = Renderer.__new__(Renderer)
    deltas = r._compare_item(g, 'sword', 'weapon')
    atk_delta = next((d for d in deltas if d[0] == 'ATK'), None)
    assert atk_delta is not None
    assert atk_delta[1] > 0


def test_compare_same_item_returns_empty(make_game):
    from render.renderer import Renderer
    g = make_game()
    g.player.weapon = 'sword'
    r = Renderer.__new__(Renderer)
    deltas = r._compare_item(g, 'sword', 'weapon')
    assert deltas == []


def test_format_delta_positive(make_game):
    from render.renderer import Renderer
    r = Renderer.__new__(Renderer)
    text, color = r._format_delta([('ATK', 2, False), ('DEF', 1, False)])
    assert text.startswith("▲")
    assert "ATK +2" in text
    assert "DEF +1" in text
    assert color[1] > 200


def test_format_delta_negative(make_game):
    from render.renderer import Renderer
    r = Renderer.__new__(Renderer)
    text, color = r._format_delta([('ATK', -1, False)])
    assert text.startswith("▼")
    assert color[0] > 200


def test_format_delta_mixed(make_game):
    from render.renderer import Renderer
    r = Renderer.__new__(Renderer)
    text, color = r._format_delta([('ATK', 2, False), ('DEF', -1, False)])
    assert text.startswith("▲")
    assert color[1] > 200


def test_minimap_toggle(make_game, monkeypatch):
    g = make_game()
    monkeypatch.setattr('game.game.save_settings', lambda s: None)
    initial = g.settings.get('show_minimap', True)
    g.handle_key(pygame.K_m)
    assert g.settings['show_minimap'] != initial

def test_quest_log_init(make_game):
    """Старт — никаких квестов, пока не поговорил с NPC."""
    g = make_game()
    assert len(g.quest_log.available) == 0
    assert len(g.quest_log.active) == 0


def test_quests_available_after_elder_dialogue(make_game):
    """Поговорил со старейшиной — его квесты в списке доступных."""
    g = make_game()
    g.dialogue.open('elder')
    assert 'q_rats' in g.quest_log.available
    assert 'q_bones' in g.quest_log.available
    assert 'q_first_step' in g.quest_log.available


def test_quests_available_after_merchant_dialogue(make_game):
    """Поговорил с торговцем — его квесты в списке доступных."""
    g = make_game()
    g.dialogue.open('merchant')
    assert 'q_goblin_king' in g.quest_log.available
    assert 'q_craft_bone' in g.quest_log.available
    assert 'q_dragon' in g.quest_log.available


def test_merchant_quests_not_available_without_dialogue(make_game):
    g = make_game()
    assert 'q_goblin_king' not in g.quest_log.available
    assert 'q_craft_bone' not in g.quest_log.available


def test_quest_accept(make_game):
    g = make_game()
    g.dialogue.open('elder')
    g.quest_log.accept('q_rats', 5)
    assert 'q_rats' not in g.quest_log.available
    assert 'q_rats' in g.quest_log.active


def test_quest_kill_progress(make_game):
    from core.entities import Entity
    g = make_game()
    g.dialogue.open('elder')
    g.quest_log.accept('q_rats', 5)
    for _ in range(5):
        m = Entity(0, 0, 1, 1, 0, 0, "РљСЂС‹СЃР°", "r", (170,150,130), 3)
        g._kill_monster(m)
    assert 'q_rats' in g.quest_log.completed


def test_quest_collect_progress(make_game):
    g = make_game()
    g.dialogue.open('elder')
    g.quest_log.accept('q_bones', 3)
    for _ in range(3):
        g.player.inventory['bone'] = g.player.inventory.get('bone', 0) + 1
        g.quest_log.update_collect('bone', g.player.inventory)
    assert 'q_bones' in g.quest_log.completed


def test_quest_reach_progress(make_game):
    g = make_game()
    g.dialogue.open('elder')
    g.quest_log.accept('q_first_step', 1)
    g.quest_log.update_reach(5)
    assert 'q_first_step' in g.quest_log.completed


def test_quest_craft_progress(make_game):
    g = make_game()
    g.dialogue.open('merchant')
    g.quest_log.accept('q_craft_bone', 1)
    g.quest_log.update_craft('bone_sword')
    assert 'q_craft_bone' in g.quest_log.completed


def test_quest_turn_in_gives_gold(make_game):
    g = make_game()
    g.dialogue.open('elder')
    g.quest_log.accept('q_rats', 5)
    g.quest_log.active['q_rats']['progress'] = 5
    g.quest_log._complete('q_rats')
    gold_before = g.player.gold
    assert g.quest_log.turn_in('q_rats', g.player, g) is True
    assert g.player.gold == gold_before + 20
    assert 'q_rats' in g.quest_log.turned_in


def test_quest_turn_in_collect_removes_items(make_game):
    g = make_game()
    g.dialogue.open('elder')
    g.quest_log.accept('q_bones', 3)
    g.player.inventory['bone'] = 5
    g.quest_log.active['q_bones']['progress'] = 3
    g.quest_log._complete('q_bones')
    g.quest_log.turn_in('q_bones', g.player, g)
    assert g.player.inventory.get('bone', 0) == 2


def test_elder_exists_in_overworld(make_game):
    g = make_game()
    assert g.elder is not None
    assert g.elder.npc_type == 'elder'


def test_new_game_spawns_in_village(make_game):
    from core import overworld as ov
    g = make_game()
    assert (g.player.x, g.player.y) == ov.VILLAGE_CENTER
    assert ov.in_village(g.player.x, g.player.y)
    assert g.overworld.tiles[g.player.y][g.player.x] == Tile.FLOOR


def test_elder_and_quest_board_live_in_village(make_game):
    from core import overworld as ov
    g = make_game()
    assert (g.elder.x, g.elder.y) == ov.ELDER_POS
    assert ov.in_village(g.elder.x, g.elder.y)
    assert g.quest_board is not None
    assert ov.in_village(g.quest_board.x, g.quest_board.y)


def test_elder_blocks_movement(make_game):
    g = make_game()
    g.player.x, g.player.y = g.elder.x - 1, g.elder.y
    g.overworld.tiles[g.player.y][g.player.x] = Tile.FLOOR
    moved = g.move_player(1, 0)
    assert moved is False


def test_open_dialogue_elder(make_game):
    g = make_game()
    g.player.x, g.player.y = g.elder.x - 1, g.elder.y
    g.dialogue.open('elder')
    assert g.screen_mode == 'dialogue'
    assert g.dialogue.npc_type == 'elder'


def test_dialogue_navigate(make_game):
    g = make_game()
    g.dialogue.open('elder')
    g.dialogue.selection = 0
    g.handle_key(pygame.K_DOWN)
    assert g.dialogue.selection == 1
    g.handle_key(pygame.K_UP)
    assert g.dialogue.selection == 0


def test_dialogue_close(make_game):
    g = make_game()
    g.dialogue.open('elder')
    g.dialogue.selection = len(g.dialogue.options) - 1
    g.handle_key(pygame.K_RETURN)
    assert g.screen_mode == 'game'


def test_dialogue_opens_quests(make_game):
    g = make_game()
    g.dialogue.open('elder')
    g.dialogue.selection = 1
    g.handle_key(pygame.K_RETURN)
    assert g.screen_mode == 'quests'


def test_quests_screen_opens(make_game):
    g = make_game()
    g.handle_key(pygame.K_l)
    assert g.screen_mode == 'quests'


def test_quests_screen_closes(make_game):
    g = make_game()
    g.screen_mode = 'quests'
    g.handle_key(pygame.K_ESCAPE)
    assert g.screen_mode == 'game'


def test_quest_controller_accepts(make_game):
    g = make_game()
    g.dialogue.open('elder')
    g.quests.open()
    items = g.quests.visible_quests()
    idx = next(i for i, (k, s) in enumerate(items) if k == 'q_rats')
    g.quests.activate(idx)
    assert 'q_rats' in g.quest_log.active


def test_stats_screen_opens(make_game):
    g = make_game()
    g.handle_key(pygame.K_TAB)
    assert g.screen_mode == 'stats_run'


def test_stats_screen_closes(make_game):
    g = make_game()
    g.screen_mode = 'stats_run'
    g.handle_key(pygame.K_TAB)
    assert g.screen_mode == 'game'


def test_run_summary_returns_dict(make_game):
    g = make_game()
    g.kills = 10
    g.boss_kills = 2
    s = g.run_summary()
    assert s['kills'] == 10
    assert s['boss_kills'] == 2
    assert 'time_seconds' in s


def test_quests_saved_and_loaded(make_game, tmp_path):
    g = make_game()
    g.dialogue.open('elder')
    g.quest_log.accept('q_rats', 5)
    g.quest_log.active['q_rats']['progress'] = 3
    path = str(tmp_path / "save.json")
    g.save(path=path)
    from core.quests import QuestLog
    g.quest_log = QuestLog()
    g.load(path=path)
    assert 'q_rats' in g.quest_log.active
    assert g.quest_log.active['q_rats']['progress'] == 3


def test_run_stats_saved_and_loaded(make_game, tmp_path):
    g = make_game()
    g.kills = 42
    g.total_crafts = 7
    path = str(tmp_path / "save.json")
    g.save(path=path)
    g.kills = 0
    g.total_crafts = 0
    g.load(path=path)
    assert g.kills == 42
    assert g.total_crafts == 7

def test_overworld_explored_preserved_on_enter(make_game):
    g = make_game()
    for y in range(5):
        for x in range(5):
            g.explored.add((x, y))
    before = set(g.explored)
    g.enter_dungeon()
    assert g.overworld_explored == before


def test_overworld_explored_restored_on_exit(make_game):
    g = make_game()
    g.explored.add((10, 10))
    before = set(g.explored)
    g.enter_dungeon()
    g.exit_to_overworld()
    assert (10, 10) in g.explored


def test_overworld_memory_preserved_at_depth_2(make_game):
    """С появлением подъёма наверх память поверхности больше не стирается."""
    g = make_game()
    g.explored.add((5, 5))
    g.enter_dungeon()
    assert (5, 5) in g.overworld_explored
    g.new_dungeon(2, autosave=False)
    assert (5, 5) in g.overworld_explored


def test_depth_1_explored_preserved_on_reentry(make_game):
    g = make_game()
    g.enter_dungeon()
    custom = (g.player.x + 5, g.player.y + 5)
    g.explored.add(custom)
    g.exit_to_overworld()
    g.enter_dungeon()
    assert custom in g.explored


def test_explored_saved_and_loaded(make_game, tmp_path):
    g = make_game()
    g.enter_dungeon()
    custom = (g.player.x + 3, g.player.y + 3)
    g.explored.add(custom)
    g.explored_snapshot = set(g.explored)
    path = str(tmp_path / "save.json")
    g.save(path=path)
    g.load(path=path)
    assert custom in g.explored


def test_overworld_explored_saved_and_loaded(make_game, tmp_path):
    g = make_game()
    g.explored.add((20, 20))
    g.enter_dungeon()
    path = str(tmp_path / "save.json")
    g.save(path=path)
    g.overworld_explored = set()
    g.load(path=path)
    assert (20, 20) in g.overworld_explored

def test_item_instance_basic():
    from core.data import ItemInstance
    i = ItemInstance('sword', 2)
    assert i.key() == 'sword+2'
    assert str(i) == 'sword+2'
    assert i.rarity == 2
    assert i.base == 'sword'


def test_item_instance_from_key():
    from core.data import ItemInstance
    assert ItemInstance.from_key('sword+2') == ItemInstance('sword', 2)
    assert ItemInstance.from_key('sword') == ItemInstance('sword', 0)


def test_item_instance_equality():
    from core.data import ItemInstance
    assert ItemInstance('sword', 2) == ItemInstance('sword', 2)
    assert ItemInstance('sword', 2) != ItemInstance('sword', 1)
    d = {ItemInstance('sword', 2): 'a', ItemInstance('axe', 1): 'b'}
    assert ItemInstance('sword', 2) in d


def test_item_instance_parses_via_parse_item_key():
    from core.data import ItemInstance, parse_item_key
    inst = ItemInstance('axe', 3)
    assert parse_item_key(inst) == ('axe', 3)
    assert parse_item_key('axe+3') == ('axe', 3)
    assert parse_item_key('axe') == ('axe', 0)


def test_item_instance_display():
    from core.data import ItemInstance
    i = ItemInstance('sword', 2)
    assert i.display_name() == 'Меч +2'
    assert i.data()['value'] == 2

