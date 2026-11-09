"""Тесты сущностей."""
from core.entities import Player, Entity, Room, Dungeon
from core.config import Tile


def test_player_defaults():
    p = Player()
    assert p.level == 1 and p.gold == 0
    assert p.str == p.agi == p.vit == p.luk == p.df_stat == 5
    assert p.weapon is None and p.armor is None
    assert p.hp == p.max_hp


def test_recalc_str_increases_atk():
    p = Player(); b = p.atk; p.str += 4; p.recalc(); assert p.atk > b


def test_recalc_vit_increases_only_hp():
    p = Player(); b_hp, b_df = p.max_hp, p.df
    p.vit += 6; p.recalc()
    assert p.max_hp > b_hp and p.df == b_df


def test_recalc_df_stat_increases_df():
    p = Player(); b = p.df; p.df_stat += 4; p.recalc(); assert p.df == b + 2


def test_recalc_weapon_increases_atk():
    p = Player(); b = p.atk; p.weapon = 'sword'; p.recalc(); assert p.atk == b + 2


def test_recalc_rare_weapon():
    p = Player(); b = p.atk
    p.weapon = 'sword+2'   # +2 база + 2 редкость = +4
    p.recalc()
    assert p.atk == b + 4


def test_recalc_armor_increases_df():
    p = Player(); b = p.df; p.armor = 'plate'; p.recalc(); assert p.df == b + 3


def test_recalc_preserves_hp_on_stat_growth():
    p = Player(); p.hp = 10; p.vit += 2; p.recalc(); assert p.hp == 14


def test_recalc_clamps_hp():
    p = Player(); p.hp = 999; p.vit = 5; p.recalc(); assert p.hp <= p.max_hp


def test_dodge_cap():
    p = Player(); p.agi = 100
    assert p.dodge == 0.30       # было 0.40


def test_crit_cap():
    p = Player(); p.luk = 100
    assert p.crit == 0.40        # было 0.50


def test_dodge_scales():
    p = Player()
    p.agi = 5
    assert abs(p.dodge - 0.075) < 0.001
    p.agi = 10
    assert abs(p.dodge - 0.15) < 0.001


def test_dodge_bonus_from_armor():
    p = Player()
    p.agi = 5
    base = p.dodge
    p.armor = 'mithril'   # +5% уклонение
    p.recalc()
    assert p.dodge > base


def test_crit_bonus_from_weapon():
    p = Player()
    p.luk = 5
    base = p.crit
    p.weapon = 'dagger'   # +2% крит
    p.recalc()
    assert p.crit > base


def test_room_intersects():
    r1, r2, r3 = Room(0, 0, 5, 5), Room(10, 10, 5, 5), Room(3, 3, 5, 5)
    assert not r1.intersects(r2) and r1.intersects(r3)


def test_room_center():
    assert Room(10, 10, 6, 8).center() == (13, 14)


def test_dungeon_init():
    d = Dungeon(10, 10)
    assert all(t == Tile.WALL for row in d.tiles for t in row)


def test_player_perks_default_off():
    p = Player()
    assert not any([p.double_strike, p.regen, p.vampire, p.thorns, p.greedy])
    assert p.step_counter == 0


def test_entity_merchant_flag():
    e = Entity(0, 0, 1, 1, 0, 0, "T", "M", (255, 255, 255))
    assert e.is_merchant is False
    e.is_merchant = True
    assert e.is_merchant is True