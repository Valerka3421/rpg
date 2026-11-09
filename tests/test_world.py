"""Тесты генерации, LOS, FOV, BFS."""
from core.config import Tile, MAP_W, MAP_H
from core.entities import Dungeon
from core.world import generate_dungeon, has_los, compute_fov, bfs_toward_player


def _make_open(size=20):
    d = Dungeon(MAP_W, MAP_H)
    for y in range(size):
        for x in range(size):
            d.tiles[y][x] = Tile.FLOOR
    return d


def test_generate_dungeon_returns_valid():
    d = generate_dungeon(1)
    assert len(d.rooms) >= 2
    assert d.stairs_pos is not None
    sx, sy = d.stairs_pos
    assert d.tiles[sy][sx] == Tile.STAIRS


def test_generate_dungeon_has_monsters():
    assert len(generate_dungeon(1).monsters) >= 1


def test_generate_dungeon_no_boss_on_depth_1():
    assert not any(m.boss for m in generate_dungeon(1).monsters)


def test_boss_on_depth_5():
    bosses = [m for m in generate_dungeon(5).monsters if m.boss]
    assert len(bosses) == 1 and bosses[0].name == 'Король гоблинов'


def test_boss_on_depth_10():
    bosses = [m for m in generate_dungeon(10).monsters if m.boss]
    assert len(bosses) == 1 and bosses[0].name == 'Лич'


def test_boss_on_depth_20():
    bosses = [m for m in generate_dungeon(20).monsters if m.boss]
    assert len(bosses) == 1 and bosses[0].name == 'Древний дракон'


def test_monsters_on_floor():
    d = generate_dungeon(3)
    for m in d.monsters:
        assert d.tiles[m.y][m.x] == Tile.FLOOR


def test_monsters_scale():
    d1, d5 = generate_dungeon(1), generate_dungeon(5)
    a1 = sum(m.max_hp for m in d1.monsters) / max(1, len(d1.monsters))
    a5 = sum(m.max_hp for m in d5.monsters) / max(1, len(d5.monsters))
    assert a5 > a1


def test_los_open():
    assert has_los(_make_open(), 0, 0, 10, 10)


def test_los_blocked():
    d = _make_open(); d.tiles[5][5] = Tile.WALL
    assert not has_los(d, 0, 0, 10, 10)


def test_los_self():
    assert has_los(_make_open(), 5, 5, 5, 5)


def test_fov_radius():
    vis = compute_fov(_make_open(), 10, 10, radius=5)
    assert (10, 10) in vis and (20, 20) not in vis


def test_fov_walls():
    d = _make_open()
    for x in range(4, 16):
        d.tiles[4][x] = Tile.WALL; d.tiles[16][x] = Tile.WALL
    for y in range(4, 16):
        d.tiles[y][4] = Tile.WALL; d.tiles[y][16] = Tile.WALL
    vis = compute_fov(d, 10, 10, radius=10)
    assert (5, 5) in vis and (6, 6) in vis and (5, 20) not in vis


def test_bfs_first_step():
    nxt = bfs_toward_player(_make_open(), (0, 0), (5, 5), set())
    assert nxt is not None and abs(nxt[0]) + abs(nxt[1]) == 1


def test_bfs_blocked():
    d = _make_open(); d.tiles[0][1] = Tile.WALL; d.tiles[1][0] = Tile.WALL
    assert bfs_toward_player(d, (0, 0), (5, 5), set()) is None


def test_bfs_adjacent():
    assert bfs_toward_player(_make_open(), (5, 7), (5, 5), set()) == (5, 6)

def test_generate_overworld():
    from core.world import generate_overworld
    d = generate_overworld()
    assert d.stairs_pos is not None
    ex, ey = d.stairs_pos
    assert d.tiles[ey][ex] == Tile.STAIRS
    assert len(d.items) >= 10
    assert len(d.monsters) >= 5


def test_overworld_has_resources():
    from core.world import generate_overworld
    d = generate_overworld()
    resource_keys = {'mushroom', 'berry', 'wood', 'stone', 'ore'}
    found = set(d.items.values())
    assert found & resource_keys

def test_fov_blocks_wall_gap():
    """Игрок в (5,5), стена в форме угла, моб за углом — невидим."""
    from core.entities import Dungeon
    from core.world import compute_fov
    from core.config import Tile, MAP_W, MAP_H
    d = Dungeon(MAP_W, MAP_H)
    for y in range(MAP_H):
        for x in range(MAP_W):
            d.tiles[y][x] = Tile.FLOOR
    # Стена-угол: горизонтальная стена справа, вертикальная сверху
    for x in range(5, 20):
        d.tiles[3][x] = Tile.WALL          # стена сверху
    for y in range(0, 4):
        d.tiles[y][10] = Tile.WALL         # стена слева от коридора
    # Игрок в (5,5), цель в (12,2) — за углом
    vis = compute_fov(d, 5, 5, radius=10)
    assert (12, 2) not in vis, "Видно за угол — FOV дырявый"
    # Но прямая цель — видна
    assert (8, 5) in vis


def test_fov_sees_adjacent_wall():
    """Стена в 1 клетке — видна."""
    from core.entities import Dungeon
    from core.world import compute_fov
    from core.config import Tile, MAP_W, MAP_H
    d = Dungeon(MAP_W, MAP_H)
    for y in range(MAP_H):
        for x in range(MAP_W):
            d.tiles[y][x] = Tile.FLOOR
    d.tiles[5][6] = Tile.WALL
    vis = compute_fov(d, 5, 5, radius=5)
    assert (6, 5) in vis