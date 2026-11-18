"""Auto-generated from render/tiles.py — see git history."""

import pygame

from render import textures
from render.tiles._common import (
    SIZE, BIOME_PALETTES, _grime,
)

from render.tiles._nature import _grass


# ════════════════════════ НАДГРОБИЯ ════════════════════════
def _tombstone(variant):
    """Надгробие: плита на выцветшей траве мёртвого леса."""
    s = _grass(variant, BIOME_PALETTES['dead'])
    rng = textures.rng_for(variant, 97, seed=281)
    stone = (104, 100, 92)
    shadow = pygame.Surface((SIZE, SIZE), pygame.SRCALPHA)
    pygame.draw.ellipse(shadow, (0, 0, 0, 105), (16, 50, 32, 10))
    s.blit(shadow, (0, 0))
    # плита: слегка наклонена по варианту
    tilt = (variant % 3 - 1) * 3
    body = pygame.Surface((24, 40), pygame.SRCALPHA)
    pygame.draw.rect(body, (36, 34, 30), (2, 6, 20, 34), border_radius=9)
    pygame.draw.rect(body, stone, (3, 7, 18, 32), border_radius=9)
    pygame.draw.line(body, textures.shade(stone, 1.25), (5, 9), (18, 9), 2)
    pygame.draw.line(body, textures.shade(stone, 0.7), (5, 38), (18, 38), 1)
    # высеченная надпись
    for ly in (16, 22, 28):
        pygame.draw.line(body, (58, 56, 50), (7, ly), (16, ly), 1)
    pygame.draw.line(body, (58, 56, 50), (7, 32), (12, 32), 1)
    body = pygame.transform.rotate(body, tilt)
    s.blit(body, (SIZE // 2 - body.get_width() // 2,
                  12 - (body.get_height() - 40) // 2 - 4))
    # холмик земли и увядшие цветы
    pygame.draw.ellipse(s, (52, 46, 36), (18, 44, 28, 8))
    for _ in range(3):
        x = rng.randint(6, SIZE - 8)
        y = rng.randint(42, SIZE - 4)
        pygame.draw.circle(s, (86, 74, 44), (x, y), 1)
    _grime(s, rng, 110, 8)
    return s


# ════════════════════════ КЭШ ════════════════════════
