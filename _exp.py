"""Эксперимент: варианты пост-обработки спрайтов (обводка, пикселизация)."""
import os
os.environ['SDL_VIDEODRIVER'] = 'dummy'
os.environ['SDL_AUDIODRIVER'] = 'dummy'
import pygame
pygame.init()
pygame.display.set_mode((1280, 720))

from core import config
config.set_resolution(1280, 720)
from render.theme import Theme as T
T.init()
from render.icon_renderer import IconRenderer
from render.sprites import Sprites
Sprites.init()

canvas = pygame.Surface((1280, 720))
canvas.fill((22, 22, 30))
icons = IconRenderer(canvas)


def silhouette(surf, color, width):
    mask = pygame.mask.from_surface(surf, 40)
    sil = mask.to_surface(setcolor=color, unsetcolor=(0, 0, 0, 0))
    out = pygame.Surface(surf.get_size(), pygame.SRCALPHA)
    for dx in range(-width, width + 1):
        for dy in range(-width, width + 1):
            if dx == 0 and dy == 0:
                continue
            if dx * dx + dy * dy > width * width + 0.1:
                continue
            out.blit(sil, (dx, dy))
    return out


def finish(surf, outline=(6, 6, 10), width=1, pixelate=False, shadow=True):
    w, h = surf.get_size()
    work = pygame.Surface((w, h), pygame.SRCALPHA)
    work.blit(surf, (0, 0))
    if pixelate:
        small = pygame.transform.scale(work, (w // 2, h // 2))
        work = pygame.transform.scale(small, (w, h))
    out = pygame.Surface((w, h), pygame.SRCALPHA)
    sil = silhouette(work, outline, width)
    out.blit(sil, (0, 0))
    if shadow:
        sh = pygame.Surface((w, h), pygame.SRCALPHA)
        sh.blit(silhouette(work, (0, 0, 0), 2), (1, 2))
        sh.set_alpha(70)
        out.blit(sh, (0, 0))
    out.blit(work, (0, 0))
    return out


classes = ('warrior', 'assassin', 'rogue', 'paladin', 'default')
monsters = ('r', 'g', 'a', 's', 'S', 'O', 'T', 'D', 'v', 'd', 'Y', 'W')
rows = [
    ('сейчас', lambda s: s),
    ('обводка', lambda s: finish(s)),
    ('пикселизация+обводка', lambda s: finish(s, pixelate=True)),
]
y = 10
T.text(canvas, 'КЛАССЫ', 10, y, (220, 220, 240), font=T.FONT_SMALL)
y += 20
for label, fn in rows:
    T.text(canvas, label, 10, y + 24, (150, 150, 170), font=T.FONT_TINY)
    for i, cls in enumerate(classes):
        s = pygame.Surface((64, 64), pygame.SRCALPHA)
        icons._make_player_surf(cls, 64)
        base = pygame.Surface((64, 64), pygame.SRCALPHA)
        base.blit(icons._get_item_surf('potion_s', 64) or pygame.Surface((64, 64)), (0, 0))
        # просто перерисуем игрока в отдельную поверхность
        tmp = IconRenderer(base)
        tmp.draw_player(cls, 0, 0, size=64)
        canvas.blit(fn(base), (150 + i * 80, y))
    y += 80

T.text(canvas, 'МОБЫ', 10, y, (220, 220, 240), font=T.FONT_SMALL)
y += 20
for label, fn in rows:
    T.text(canvas, label, 10, y + 24, (150, 150, 170), font=T.FONT_TINY)
    for i, ch in enumerate(monsters):
        tmp = pygame.Surface((64, 64), pygame.SRCALPHA)
        icon = IconRenderer(tmp)
        icon.draw_monster(ch, 0, 0, size=64)
        canvas.blit(fn(tmp), (150 + i * 70, y))
    y += 80

pygame.image.save(canvas, '_shots/sprite_styles.png')
print('saved')
