"""Тесты общего слоя правил (core.rules)."""
import random

from core import rules
from core.config import Tile, MAP_W, MAP_H, BOSS_BREATH_INTERVAL
from core.entities import Player, Entity, Dungeon
from core.data import xp_to_next


def _monster(x=0, y=0, hp=10, atk=3, df=0, ch='g', xp=5, **kw):
    return Entity(x, y, hp, hp, atk, df, "Моб", ch, (100, 200, 100), xp, **kw)


# ── Опыт и уровни ──
def test_xp_curve_is_reachable():
    """Кривая не экспоненциальная: 20-й уровень достижим."""
    assert xp_to_next(1) == 10
    assert xp_to_next(20) < 2000
    assert xp_to_next(10) < xp_to_next(11) < xp_to_next(20)


def test_level_up_once():
    p = Player()
    p.hp = 5
    level = rules.level_up_once(p)
    assert level == 2 and p.level == 2
    assert p.free_points == 5 and p.hp == p.max_hp
    assert p.xp_next == xp_to_next(2)


def test_give_xp_multiple_levels():
    p = Player()
    levels = rules.give_xp(p, xp_to_next(1) + xp_to_next(2) + 5)
    assert levels == [2, 3]
    assert p.level == 3 and p.xp == 5


# ── Бой ──
def test_resolve_attack_damage():
    p = Player()
    m = _monster(hp=50)
    random.seed(1)
    res = rules.resolve_attack(p, m)
    assert not res.dodged and res.damage >= 1
    assert m.hp == 50 - res.damage


def test_resolve_attack_player_dodge():
    p = Player()
    p.agi = 100                      # кап 30% уклонения
    m = _monster(atk=50)
    dodges = 0
    random.seed(7)
    for _ in range(400):
        hp_before = p.hp
        res = rules.resolve_attack(m, p)
        dodges += res.dodged
        if res.dodged:
            assert p.hp == hp_before
    assert 60 < dodges < 180          # ~30% из 400


def test_resolve_attack_crit_only_for_player():
    p = Player()
    p.luk = 100
    m = _monster(hp=500, df=0)
    random.seed(3)
    crits = sum(rules.resolve_attack(p, m).crit for _ in range(200))
    assert crits > 0
    m2 = _monster(hp=500)
    crits_monster = sum(rules.resolve_attack(m2, p).crit for _ in range(50))
    assert crits_monster == 0


def test_resolve_attack_applies_status():
    rat = _monster(ability='poison')
    p = Player()
    rules.resolve_attack(rat, p)
    assert p.has_status('poison')


def test_vampire_heals_attacker():
    p = Player()
    p.vampire = True
    p.hp = 10
    p.atk = 40
    m = _monster(hp=100, df=0)
    random.seed(2)
    res = rules.resolve_attack(p, m)
    assert res.vampire_heal >= 1 and p.hp > 10


def test_thorns_hurt_attacker():
    p = Player()
    p.thorns = True
    m = _monster(hp=10, atk=5)
    random.seed(2)
    res = rules.resolve_attack(m, p)
    assert res.thorns == 2 and m.hp == 8


def test_thorns_not_applied_when_defender_dies():
    p = Player()
    p.thorns = True
    p.hp = 1
    m = _monster(atk=999)
    res = rules.resolve_attack(m, p)
    assert res.defender_dead and res.thorns == 0 and m.hp == m.max_hp


# ── Награды и лут ──
def test_kill_reward_gold_and_drops():
    m = _monster(ch='r', xp=3)
    random.seed(0)
    reward = rules.roll_kill_reward(m, depth=1)
    assert reward.gold >= 2 and reward.xp == 3
    assert isinstance(reward.drops, list)


def test_boss_reward_includes_potion():
    boss = _monster(ch='G', hp=80, boss=True)
    reward = rules.roll_kill_reward(boss, depth=5)
    assert 'potion_l' in reward.drops
    assert reward.gold > 30


def test_award_kill_applies_xp_and_gold():
    p = Player()
    m = _monster(xp=xp_to_next(1))
    random.seed(0)
    reward, levels = rules.award_kill(p, m, depth=1)
    assert levels == [2]
    assert p.gold == reward.gold


# ── Статусы ──
def test_tick_statuses_damage_and_expire():
    p = Player()
    p.add_status('poison', 2)
    hp = p.hp
    events = rules.tick_statuses(p)
    assert p.hp == hp - 1
    assert events[0].damage == 1 and not events[0].expired
    events = rules.tick_statuses(p)
    assert p.statuses == {} and events[0].expired


def test_tick_regen_heals():
    p = Player()
    p.hp = 10
    p.add_status('regen', 2)
    rules.tick_statuses(p)
    assert p.hp == 12


def test_tick_statuses_unknown_key_is_dropped():
    p = Player()
    p.statuses['???'] = 3
    rules.tick_statuses(p)
    assert '???' not in p.statuses


# ── Подбор и дроп ──
def _dungeon():
    d = Dungeon(MAP_W, MAP_H)
    for y in range(5):
        for x in range(5):
            d.tiles[y][x] = Tile.FLOOR
    d.rooms = []
    return d


def test_pickup_gold():
    d = _dungeon()
    p = Player()
    d.items[(1, 1)] = 'gold_medium'
    res = rules.pickup(d, p, 1, 1)
    assert res.kind == 'gold' and p.gold == 15
    assert (1, 1) not in d.items


def test_pickup_weapon_rarity_roll():
    d = _dungeon()
    p = Player()
    d.items[(1, 1)] = 'sword'
    random.seed(1)
    rules.pickup(d, p, 1, 1)
    assert list(p.inventory)[0].startswith('sword')


def test_pickup_missing_item():
    assert rules.pickup(_dungeon(), Player(), 2, 2) is None


def test_place_loot_on_floor_and_stairs():
    d = _dungeon()
    inv = {}
    d.tiles[2][2] = Tile.STAIRS
    assert rules.place_loot(d, inv, 2, 2, 'bone') == 'inventory'
    assert inv['bone'] == 1
    assert rules.place_loot(d, inv, 1, 1, 'wood') == 'tile'
    assert d.items[(1, 1)] == 'wood'


def test_place_loot_neighbour_when_occupied():
    d = _dungeon()
    inv = {}
    d.items[(1, 1)] = 'bone'
    rules.place_loot(d, inv, 1, 1, 'wood')
    values = list(d.items.values()) + list(inv)
    assert 'wood' in values


# ── Крафт ──
def test_craft_level_gate():
    inv = {'bone': 2, 'wood': 1}
    assert rules.can_craft('bone_sword', inv, level=1) is False
    assert rules.can_craft('bone_sword', inv, level=3) is True


def test_craft_consumes_materials():
    inv = {'bone': 2, 'wood': 1}
    ok, reason = rules.craft('bone_sword', inv, level=5)
    assert ok and reason == 'ok'
    assert inv['bone_sword'] == 1 and 'bone' not in inv


def test_craft_reasons():
    assert rules.craft('bone_sword', {}, level=5)[1] == 'materials'
    assert rules.craft('dagger', {}, level=5)[1] == 'no_recipe'


# ── Предметы ──
def test_use_item_potion():
    p = Player()
    p.hp = 10
    p.inventory = {'potion_s': 1}
    res = rules.use_item(p, 'potion_s')
    assert res.ok and res.kind == 'heal' and p.hp == 20
    assert p.inventory == {}


def test_use_item_full_hp_keeps_potion():
    p = Player()
    p.inventory = {'potion_s': 1}
    res = rules.use_item(p, 'potion_s')
    assert not res.ok and p.inventory == {'potion_s': 1}


def test_use_item_equip_and_swap():
    p = Player()
    p.level = 5
    p.inventory = {'sword': 1}
    rules.use_item(p, 'sword')
    assert p.weapon == 'sword'
    p.inventory['axe'] = 1
    rules.use_item(p, 'axe')
    assert p.weapon == 'axe' and p.inventory.get('sword') == 1


def test_use_item_scroll_fireball_hits_area():
    p = Player()
    p.inventory = {'scroll_fireball': 1}
    monsters = [_monster(x=p.x + 3, y=p.y, hp=30),
                _monster(x=p.x + 4, y=p.y + 1, hp=30)]
    res = rules.use_item(p, 'scroll_fireball', monsters)
    assert res.ok and res.kind == 'fireball'
    assert all(m.hp < 30 for m in monsters)
    assert all(m.has_status('burning') for m in monsters)


def test_use_item_scroll_without_target_is_not_consumed():
    p = Player()
    p.inventory = {'scroll_fireball': 1}
    res = rules.use_item(p, 'scroll_fireball', [])
    assert not res.ok and 'scroll_fireball' in p.inventory


def test_best_heal_key_picks_weakest():
    p = Player()
    p.inventory = {'potion_l': 1, 'potion_s': 2, 'mushroom': 1}
    assert rules.best_heal_key(p.inventory) == 'potion_s'


# ── Магазин ──
def test_buy_from_stock():
    p = Player()
    p.gold = 50
    stock = [{'key': 'potion_s', 'price': 5, 'sold': False}]
    ok, reason, price = rules.buy_from_stock(stock, 0, p)
    assert ok and reason == 'ok' and price == 5
    assert p.gold == 45 and p.inventory['potion_s'] == 1
    assert stock[0]['sold'] is True
    assert rules.buy_from_stock(stock, 0, p)[1] == 'sold'


def test_buy_without_gold():
    p = Player()
    p.gold = 1
    stock = [{'key': 'sword', 'price': 100, 'sold': False}]
    ok, reason, _ = rules.buy_from_stock(stock, 0, p)
    assert not ok and reason == 'gold'


# ── Спавн ──
def test_find_free_tile_avoids_walls_and_occupancy():
    d = _dungeon()
    d.tiles[2][2] = Tile.WALL
    assert rules.find_free_tile(d, 2, 2) is not None
    x, y = rules.find_free_tile(d, 1, 1, occupied={(1, 1)})
    assert (x, y) != (1, 1) and d.tiles[y][x] != Tile.WALL


def test_find_free_tile_prefers_floor_over_stairs():
    d = _dungeon()
    d.tiles[2][2] = Tile.STAIRS
    x, y = rules.find_free_tile(d, 2, 2)
    assert d.tiles[y][x] == Tile.FLOOR


# ── Босс ──
def test_boss_breath_hits_from_range():
    p = Player()
    boss = _monster(x=p.x + 3, y=p.y, hp=500, atk=20, boss=True)
    boss.breath_timer = 0.0
    hp = p.hp
    damage = rules.boss_breath_tick(boss, p, distance=3, dt=0.1)
    assert damage > 0 and p.hp == hp - damage
    assert boss.breath_timer == BOSS_BREATH_INTERVAL
    # таймер не прошёл — второго удара нет
    assert rules.boss_breath_tick(boss, p, distance=3, dt=0.1) == 0


def test_boss_breath_out_of_range():
    p = Player()
    boss = _monster(x=p.x + 1, y=p.y, hp=500, atk=20, boss=True)
    boss.breath_timer = 0.0
    assert rules.boss_breath_tick(boss, p, distance=1, dt=0.1) == 0


def test_action_interval_boss_slower():
    boss = _monster(boss=True)
    normal = _monster()
    random.seed(1)
    intervals = [rules.action_interval(boss) for _ in range(50)]
    assert min(intervals) > 0.4
    assert rules.action_interval(normal) < 0.45
