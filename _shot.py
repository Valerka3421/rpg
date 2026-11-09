"""Снимает эталонные PNG текущего рендера (для сравнения до/после)."""
import os, random, sys
os.environ['SDL_VIDEODRIVER'] = 'dummy'
os.environ['SDL_AUDIODRIVER'] = 'dummy'
import pygame
pygame.init()

from core import config
config.set_resolution(1280, 720)
from core.config import TILE, Tile, MAP_W, MAP_H
from core.entities import Entity
from game import Game

random.seed(5)
screen = pygame.display.set_mode((1280, 720))
g = Game(screen)
g.new_game(class_key='warrior')
g.enter_dungeon()
g.new_dungeon(3, autosave=False)
g.screen_mode = 'game'
g.update_fov()
g.draw()
os.makedirs('_shots', exist_ok=True)
pygame.image.save(screen, '_shots/full_game.png')

# Крупный план комнаты: сами нарисуем сцену через рендерер
from render.game_renderer import GameRenderer
from render.icon_renderer import IconRenderer

g.player.x, g.player.y = g.dungeon.rooms[0].center()
g.update_fov()
g.draw()
pygame.image.save(screen, '_shots/room.png')

# Сетка спрайтов: классы, монстры, боссы, NPC, предметы
canvas = pygame.Surface((1280, 720))
canvas.fill((18, 18, 26))
from render.theme import Theme as T
T.init()
from render.sprites import Sprites
Sprites.init()
icons = IconRenderer(canvas)

x0, y0 = 16, 24
T.text(canvas, "ИГРОКИ", x0, y0 - 18, (200, 200, 220), font=T.FONT_SMALL)
for i, cls in enumerate(('warrior', 'assassin', 'rogue', 'paladin', 'default')):
    icons.draw_player(cls, x0 + i * 80, y0, size=64)
    T.text(canvas, cls, x0 + i * 80, y0 + 66, (150, 150, 170), font=T.FONT_TINY)

from render.constants import MONSTER_CH_TO_KEY
y1 = y0 + 110
T.text(canvas, "МОБЫ", x0, y1 - 18, (200, 200, 220), font=T.FONT_SMALL)
for i, (ch, key) in enumerate(MONSTER_CH_TO_KEY.items()):
    icons.draw_monster(ch, x0 + i * 76, y1, size=64)
    T.text(canvas, key, x0 + i * 76, y1 + 66, (150, 150, 170), font=T.FONT_TINY)

y2 = y1 + 110
T.text(canvas, "NPC", x0, y2 - 18, (200, 200, 220), font=T.FONT_SMALL)
icons.draw_elder(x0, y2, size=64)
icons.draw_merchant(x0 + 76, y2, size=64)
T.text(canvas, 'elder', x0, y2 + 66, (150, 150, 170), font=T.FONT_TINY)
T.text(canvas, 'merchant', x0 + 76, y2 + 66, (150, 150, 170), font=T.FONT_TINY)

# предметы
from core.data import ITEMS
y3 = y2 + 110
T.text(canvas, "ПРЕДМЕТЫ", x0, y3 - 18, (200, 200, 220), font=T.FONT_SMALL)
keys = list(ITEMS.keys())
for i, key in enumerate(keys[:24]):
    cx = x0 + (i % 12) * 56
    cy = y3 + (i // 12) * 62
    icons.draw_item(key, cx, cy, size=48)
T.text(canvas, " (24 из %d)" % len(keys), x0 + 12 * 56, y3, (120, 120, 140),
       font=T.FONT_SMALL)
pygame.image.save(canvas, '_shots/sprites.png')

# Тайлы отдельно
tile_canvas = pygame.Surface((5 * 80, 100))
tile_canvas.fill((12, 12, 18))
gr = GameRenderer(tile_canvas, icons)
for i, (tile, name) in enumerate(((Tile.WALL, 'wall'), (Tile.FLOOR, 'floor'),
                                  (Tile.STAIRS, 'stairs'), (Tile.EXIT, 'exit'),
                                  (Tile.UPSTAIRS, 'upstairs'))):
    gr._is_overworld = False
    px, py = 8 + i * 80, 16
    if tile == Tile.WALL:
        gr._draw_wall(px, py, 7, 9, True)
    elif tile == Tile.FLOOR:
        gr._draw_floor(px, py, 7, 9, True)
    elif tile == Tile.STAIRS:
        gr._draw_stairs(px, py)
    elif tile == Tile.EXIT:
        gr._draw_exit(px, py)
    else:
        gr._draw_upstairs(px, py)
    T.text(tile_canvas, name, px, py + 66, (150, 150, 170), font=T.FONT_TINY)
pygame.image.save(pygame.transform.scale(tile_canvas, (5 * 160, 200)),
                  '_shots/tiles.png')
print('saved _shots/*.png')
