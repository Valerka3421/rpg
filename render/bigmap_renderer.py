"""Полноэкранная карта (открывается по «B»).

Карта всегда строится по РЕАЛЬНЫМ размерам текущей локации
(140×70 подземелье, 120×70 биом, 100×70 деревня), а не по
захардкоженным константам. Поэтому правая часть новых этажей
больше не обрезается, а маркер игрока всегда стоит на своей клетке.
"""
import pygame

from core import config
from core import locations
from core.config import Tile

# Палитра биомов для фона карты поверхности.
BIOME_COLORS = {
    'pine':   (52, 74, 46),
    'birch':  (92, 112, 64),
    'rock':   (96, 92, 84),
    'autumn': (126, 88, 48),
    'swamp':  (64, 84, 54),
    'dark':   (34, 44, 34),
    'meadow': (108, 148, 76),
    'dead':   (74, 70, 60),
    'beach':  (222, 204, 152),
}

# Цвета напольных покрытий (ground_kind).
GROUND_COLORS = {
    'road': (132, 112, 78),
    'beach_sand': (224, 206, 154),
    'mine_ground': (74, 68, 60),
    'city_ruins_floor': (122, 112, 98),
    'dungeon_ruins': (104, 96, 84),
    'grass_dead': (86, 92, 64),
    'goblin_plaza': (118, 94, 60),
    'goblin_path': (116, 88, 54),
    'goblin_grass': (66, 88, 54),
}

WALL_COLORS = {
    'water': (38, 74, 116),
    'beach_tree': (38, 78, 44),
    'mine_wall': (48, 44, 42),
    'city_ruins_wall': (104, 94, 82),
    'city_ruins_statue': (150, 142, 124),
    'tombstone': (104, 104, 108),
}

DUNGEON_FLOOR = (96, 86, 70)
DUNGEON_WALL = (46, 42, 38)

# Кэш готового слоя карты: перестраиваем только при смене локации/этажа
# или после изменения тайлов. Иначе 10 000 прямоугольников каждый кадр.
_MAP_CACHE = {'key': None, 'surface': None}


def _location_color(game, tile, x, y):
    """Цвет клетки для большой карты."""
    mode = game.world_mode
    if mode == 'dungeon':
        if tile == Tile.WALL:
            return DUNGEON_WALL
        if tile == Tile.STAIRS:
            return (255, 200, 90)
        if tile in (Tile.EXIT, Tile.UPSTAIRS):
            return (220, 190, 104)
        if tile == Tile.LOCKED_DOOR:
            return (190, 80, 80)
        if tile == Tile.CHEST:
            return (244, 186, 64)
        if tile == Tile.GATE:
            return (255, 215, 100)
        return DUNGEON_FLOOR

    # Поверхность: стены/вода/деревья и настоящий тип земли.
    if tile == Tile.WALL:
        try:
            fn = getattr(game, 'wall_kind', None)
            kind = fn(x, y) if fn else locations.wall_kind(game.location, x, y)
        except Exception:
            kind = 'tree_pine'
        if kind in WALL_COLORS:
            return WALL_COLORS[kind]
        if kind.startswith('tree'):
            biome = kind[5:] if kind.startswith('tree_') else 'pine'
            base = BIOME_COLORS.get(biome, (52, 74, 46))
            return tuple(max(0, c - 18) for c in base)
        return (60, 56, 50)
    if tile == Tile.STAIRS:
        return (232, 132, 58)
    if tile == Tile.GATE:
        return (255, 215, 100)
    if tile == Tile.CHEST:
        return (244, 186, 64)
    try:
        fn = getattr(game, 'ground_kind', None)
        kind = (fn(x, y) if fn
                else locations.ground_kind(game.location, x, y))
    except Exception:
        kind = 'grass_pine'
    if kind in GROUND_COLORS:
        return GROUND_COLORS[kind]
    if kind.startswith('grass_'):
        return BIOME_COLORS.get(kind[6:], (92, 120, 68))
    return (92, 112, 72)


def draw_bigmap(screen, game):
    """Отрисовать полноэкранную карту поверх игры."""
    sw, sh = config.SCREEN_W, config.SCREEN_H
    overlay = pygame.Surface((sw, sh), pygame.SRCALPHA)
    overlay.fill((7, 6, 5, 228))
    screen.blit(overlay, (0, 0))

    cx = sw // 2
    d = game.dungeon
    mw, mh = d.w, d.h

    # Заголовок: название локации и, для подземелья, глубина.
    if game.world_mode == 'dungeon':
        title_text = f'ПОДЗЕМЕЛЬЕ — ГЛУБИНА {game.depth}'
        accent = (214, 178, 98)
    else:
        title_text = locations.title(getattr(game, 'location', 'village'))
        accent = (214, 178, 98)
    title_font = pygame.font.SysFont('consolas', 30, bold=True)
    title = title_font.render(title_text, True, accent)
    screen.blit(title, (cx - title.get_width() // 2, 18))

    # Масштаб: вписываем АКТУАЛЬНЫЕ размеры карты.
    avail_w = sw - 2 * config.BIGMAP_MARGIN
    avail_h = sh - 2 * config.BIGMAP_MARGIN - config.BIGMAP_TITLE_H
    s = max(1, min(avail_w // mw, avail_h // mh))
    map_w, map_h = mw * s, mh * s
    ox = (sw - map_w) // 2
    oy = config.BIGMAP_TITLE_H + (sh - config.BIGMAP_TITLE_H - map_h) // 2

    pygame.draw.rect(screen, config.BIGMAP_PAD_COLOR,
                     (ox - 6, oy - 6, map_w + 12, map_h + 12),
                     border_radius=6)
    pygame.draw.rect(screen, config.BIGMAP_BORDER,
                     (ox - 6, oy - 6, map_w + 12, map_h + 12), 2,
                     border_radius=6)

    # Карта: каждый тайл квадратом (масштаб 1..N). Слой кэшируется.
    key = (id(d), game.world_mode, getattr(game, 'depth', 0),
           getattr(game, 'location', ''), mw, mh, s,
           getattr(game, 'render_revision', 0))
    if _MAP_CACHE['key'] != key:
        layer = pygame.Surface((map_w, map_h))
        tiles = d.tiles
        for y in range(mh):
            row = tiles[y]
            py = y * s
            for x in range(mw):
                pygame.draw.rect(layer, _location_color(game, row[x], x, y),
                                 (x * s, py, s, s))
        _MAP_CACHE['key'] = key
        _MAP_CACHE['surface'] = layer
    screen.blit(_MAP_CACHE['surface'], (ox, oy))

    # Переходы/врата на поверхности — заметные ромбы.
    if game.world_mode != 'dungeon':
        for (tx, ty), trans in getattr(d, 'transitions', {}).items():
            px = ox + tx * s + s // 2
            py = oy + ty * s + s // 2
            r = max(4, s)
            pygame.draw.polygon(screen, (255, 220, 110),
                                [(px, py - r), (px + r, py),
                                 (px, py + r), (px - r, py)])

    # Маркер игрока.
    px = ox + game.player.x * s + s // 2
    py = oy + game.player.y * s + s // 2
    radius = max(5, s + 2)
    pygame.draw.circle(screen, (250, 250, 120), (px, py), radius)
    pygame.draw.circle(screen, (40, 30, 0), (px, py), radius, 2)

    # Сокомандники в мультиплеере.
    if hasattr(game, 'players') and isinstance(game.players, dict):
        for pid, p in game.players.items():
            if p is game.player:
                continue
            mx = ox + p.x * s + s // 2
            my = oy + p.y * s + s // 2
            pygame.draw.circle(screen, (120, 230, 130), (mx, my),
                               max(4, s))

    # Легенда снизу.
    legend_font = pygame.font.SysFont('consolas', 14)
    legend = [
        ('игрок', (250, 250, 120)),
        ('переход', (255, 220, 110)),
        ('лестница', (255, 200, 90)),
        ('сундук', (244, 186, 64)),
    ]
    lx = ox
    ly = min(sh - 26, oy + map_h + 14)
    for label, color in legend:
        pygame.draw.rect(screen, color, (lx, ly + 2, 12, 12))
        text = legend_font.render(label, True, (190, 182, 158))
        screen.blit(text, (lx + 18, ly - 2))
        lx += 18 + text.get_width() + 22

    hint = legend_font.render('B / Esc — закрыть карту', True,
                              (170, 162, 138))
    screen.blit(hint, (cx - hint.get_width() // 2, sh - 22))
