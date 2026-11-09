"""Round-trip сериализации."""
from core.entities import Player, Entity, Room, Dungeon
from core.config import MAP_W, MAP_H, Tile
from core.save import (entity_to_dict, entity_from_dict,
                       player_to_dict, player_from_dict,
                       dungeon_to_dict, dungeon_from_dict)


def test_entity_roundtrip():
    e = Entity(5, 7, 10, 15, 4, 2, "Гоблин", "g", (100, 200, 100), 6, False)
    e2 = entity_from_dict(entity_to_dict(e))
    assert (e2.x, e2.y) == (5, 7) and (e2.hp, e2.max_hp) == (10, 15)
    assert e2.name == "Гоблин" and e2.color == (100, 200, 100)


def test_merchant_roundtrip():
    e = Entity(3, 4, 1, 1, 0, 0, "T", "M", (255, 200, 0))
    e.is_merchant = True
    assert entity_from_dict(entity_to_dict(e)).is_merchant is True


def test_player_roundtrip():
    p = Player()
    p.hp = 20; p.level = 3; p.xp = 15
    p.str = 8; p.df_stat = 9
    p.weapon = 'sword'; p.armor = 'leather'
    p.inventory = {'potion_s': 2, 'book_str': 1}
    p.gold = 100
    p.double_strike = p.regen = p.vampire = p.thorns = p.greedy = True
    p.step_counter = 17
    p2 = player_from_dict(player_to_dict(p))
    assert p2.hp == p.hp and p2.level == p.level
    assert p2.str == p.str and p2.df_stat == 9
    assert p2.weapon == 'sword' and p2.armor == 'leather'
    assert p2.inventory == {'potion_s': 2, 'book_str': 1}
    assert p2.gold == 100 and p2.double_strike and p2.greedy


def test_player_backward_compat():
    d = {'x': 0, 'y': 0, 'hp': 20,
         'level': 2, 'xp': 5, 'xp_next': 16, 'free_points': 5,
         'str': 6, 'agi': 5, 'vit': 5, 'luk': 5,
         'weapon': None, 'armor': None, 'inventory': {}}
    p = player_from_dict(d)
    assert p.gold == 0 and p.df_stat == 5
    assert p.double_strike is False and p.step_counter == 0


def test_dungeon_roundtrip():
    d = Dungeon(MAP_W, MAP_H)
    for y in range(5):
        for x in range(5):
            d.tiles[y][x] = Tile.FLOOR
    d.tiles[3][3] = Tile.STAIRS
    d.stairs_pos = (3, 3)
    d.rooms = [Room(0, 0, 5, 5)]
    d.monsters = [Entity(1, 1, 5, 5, 2, 0, "Крыса", "r", (170, 150, 130), 3)]
    d.items = {(2, 2): 'potion_s'}
    d2 = dungeon_from_dict(dungeon_to_dict(d))
    assert d2.tiles[3][3] == Tile.STAIRS
    assert d2.stairs_pos == (3, 3)
    assert d2.items == {(2, 2): 'potion_s'}