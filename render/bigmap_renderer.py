"""Полноэкранная карта мира (открывается по «M»).

Показывает всю поверхность с локациями-квадратами: деревня в центре,
8 биомов вокруг, руины города, Деревня гоблинов на востоке. Текущая
позиция игрока — маркером. Tile.WALL (включая воду и стены зданий)
затемнены, Tile.FLOOR — светлые, лестницы/выходы/GATE — цветом.
"""
import pygame

from core import config
from core import overworld as ov
from core.config import Tile

# Палитра биомов для квадратов на большой карте.
BIOME_COLORS = {
    'pine':   (40, 60, 35),
    'birch':  (90, 110, 60),
    'rock':   (90, 86, 78),
    'autumn': (120, 80, 40),
    'swamp':  (60, 80, 50),
    'dark':   (30, 40, 30),
    'meadow': (100, 140, 70),
    'dead':   (70, 65, 55),
    'beach':  (220, 200, 150),
}


def draw_bigmap(screen, game):
    """Отрисовать полноэкранную карту мира поверх игры."""
    sw, sh = config.SCREEN_W, config.SCREEN_H
    # Полупрозрачная подложка
    overlay = pygame.Surface((sw, sh), pygame.SRCALPHA)
    overlay.fill((7, 6, 5, 220))
    screen.blit(overlay, (0, 0))

    cx = sw // 2
    title = pygame.font.SysFont('consolas', 32, bold=True).render(
        'КАРТА МИРА', True, (214, 178, 98))
    screen.blit(title, (cx - title.get_width() // 2, 24))

    d = game.dungeon
    # Масштаб: вписать всю карту MAP_W×MAP_H в окно с отступами.
    avail_w = sw - 2 * config.BIGMAP_MARGIN
    avail_h = sh - 2 * config.BIGMAP_MARGIN - config.BIGMAP_TITLE_H
    sx = avail_w // config.MAP_W
    sy = avail_h // config.MAP_H
    s = max(2, min(sx, sy))
    ox = (sw - config.MAP_W * s) // 2
    oy = config.BIGMAP_TITLE_H + (sh - config.BIGMAP_TITLE_H
                                   - config.MAP_H * s) // 2

    # Локации-квадраты: только для поверхности (ov.LEGACY_MAP_W × MAP_H).
    if game.world_mode == 'overworld':
        for (rx, ry), biome in ov.BIOME_NAMES.items():
            bx = ox + ov.REGION_X[rx] * s
            by = oy + ov.REGION_Y[ry] * s
            bw = (ov.REGION_X[rx + 1] - ov.REGION_X[rx]) * s
            bh = (ov.REGION_Y[ry + 1] - ov.REGION_Y[ry]) * s
            color = BIOME_COLORS.get(biome, (60, 80, 60))
            pygame.draw.rect(screen, color, (bx, by, bw, bh),
                             border_radius=4)
            pygame.draw.rect(screen, config.BIGMAP_BORDER,
                             (bx, by, bw, bh), 2, border_radius=4)
            # Название биома (только если квадрат достаточно велик)
            if bw >= 80 and bh >= 40:
                font = pygame.font.SysFont('consolas', 12, bold=True)
                t = font.render(ov.BIOME_TITLES.get(biome, biome),
                                True, (240, 230, 200))
                screen.blit(t, (bx + bw // 2 - t.get_width() // 2,
                                 by + bh // 2 - t.get_height() // 2))

        # Деревня Этельгард
        vx, vy, vw, vh = ov.VILLAGE_RECT
        pygame.draw.rect(screen, (200, 180, 120),
                         (ox + vx * s, oy + vy * s, vw * s, vh * s),
                         border_radius=3)
        # Деревня гоблинов
        gx, gy, gw, gh = ov.GOBLIN_VILLAGE_RECT
        pygame.draw.rect(screen, (120, 90, 60),
                         (ox + gx * s, oy + gy * s, gw * s, gh * s),
                         border_radius=3)
        # Руины города
        rx, ry, rw, rh = ov.CITY_RUINS_RECT
        pygame.draw.rect(screen, (160, 130, 90),
                         (ox + rx * s, oy + ry * s, rw * s, rh * s),
                         border_radius=3)
        # Кардинальные GATE-переходы на краях
        for direction, (gx, gy) in ov.GATE_POSITIONS.items():
            mx = ox + gx * s
            my = oy + gy * s
            pygame.draw.circle(screen, (255, 215, 100), (mx, my),
                               max(4, s * 2))
        # Вход в подземелье
        dx, dy = ov.DUNGEON_POS
        pygame.draw.circle(screen, (255, 100, 50),
                           (ox + dx * s, oy + dy * s), max(5, s * 2))

    # Карта подземелья: каждый тайл одним пикселем/квадратом
    if game.world_mode != 'overworld':
        for y in range(config.MAP_H):
            for x in range(config.MAP_W):
                if x >= ov.LEGACY_MAP_W:
                    continue
                t = d.tiles[y][x]
                if t == Tile.WALL:
                    color = (40, 36, 32)
                elif t == Tile.FLOOR:
                    color = (90, 80, 65)
                elif t == Tile.STAIRS:
                    color = (255, 200, 90)
                elif t in (Tile.EXIT, Tile.UPSTAIRS):
                    color = (200, 180, 100)
                elif t == Tile.GATE:
                    color = (255, 215, 100)
                elif t == Tile.LOCKED_DOOR:
                    color = (180, 80, 80)
                elif t == Tile.CHEST:
                    color = (240, 180, 60)
                else:
                    color = (20, 20, 20)
                pygame.draw.rect(screen, color,
                                 (ox + x * s, oy + y * s, s, s))

    # Текущая позиция игрока — яркий маркер
    px = ox + game.player.x * s + s // 2
    py = oy + game.player.y * s + s // 2
    pygame.draw.circle(screen, (250, 250, 100), (px, py), max(5, s * 2))
    pygame.draw.circle(screen, (40, 30, 0), (px, py), max(5, s * 2), 1)

    # Сокомандники в мультиплеере (если есть)
    if hasattr(game, 'players') and isinstance(game.players, dict):
        for pid, p in game.players.items():
            if p is game.player:
                continue
            mx = ox + p.x * s + s // 2
            my = oy + p.y * s + s // 2
            pygame.draw.circle(screen, (120, 230, 130), (mx, my),
                               max(4, s * 2))

    # Подсказка внизу
    hint_font = pygame.font.SysFont('consolas', 14)
    hint = hint_font.render('B / Esc — закрыть карту',
                             True, (180, 170, 140))
    screen.blit(hint, (cx - hint.get_width() // 2, sh - 30))
