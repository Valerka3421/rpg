"""Тесты процедурных текстур: тайлы, обводка, свет, шум."""
import pygame
import pytest

from render import textures
from render.tiles import Tiles, SIZE


@pytest.fixture(autouse=True)
def _fresh_tiles():
    Tiles.clear()
    yield
    Tiles.clear()


# ── Хэш и варианты ──
def test_hash_is_deterministic():
    assert textures.hash01(3, 7, 1) == textures.hash01(3, 7, 1)
    assert textures.hash01(3, 7, 1) != textures.hash01(3, 7, 2)


def test_hash_is_spatially_mixed():
    """Соседние клетки не должны попадать в один вариант — иначе видны
    прямоугольные пятна тайлов."""
    picks = [textures.hash_int(x, y, 13, 8) for y in range(20) for x in range(20)]
    same_neighbours = 0
    for y in range(20):
        for x in range(19):
            if picks[y * 20 + x] == picks[y * 20 + x + 1]:
                same_neighbours += 1
    # при 8 вариантах ожидаем ~1/8 совпадений, допускаем до 30%
    assert same_neighbours < 20 * 19 * 0.30


def test_variant_is_stable_and_valid():
    for kind in ('wall', 'floor', 'grass', 'tree'):
        a = Tiles.variant(kind, 5, 9)
        b = Tiles.variant(kind, 5, 9)
        assert a == b
        variant, real_kind = a
        assert isinstance(variant, int) and variant >= 0
        assert real_kind in (kind, 'wall_moss', 'wall_torch')


def test_wall_special_variants_exist():
    kinds = {Tiles.variant('wall', x, y)[1]
             for x in range(40) for y in range(40)}
    assert 'wall_torch' in kinds
    assert 'wall_moss' in kinds


# ── Кэш тайлов ──
def test_tile_cached_and_sized(renderer):
    for kind in ('wall', 'floor', 'stairs', 'upstairs', 'exit',
                 'grass', 'tree', 'cave'):
        surf = Tiles.get(kind, 0, 1.0)
        assert surf.get_size() == (SIZE, SIZE)
        assert Tiles.get(kind, 0, 1.0) is surf


def test_tile_light_bands(renderer):
    full = Tiles.get('floor', 2, 1.0)
    dark = Tiles.get('floor', 2, 0.6)
    assert dark is not full
    # середина тёмного тайла темнее светлого
    def brightness(surf):
        r, g, b, _ = surf.get_at((16, 16))
        return r + g + b
    assert brightness(dark) < brightness(full)


def test_tile_is_not_empty(renderer):
    surf = Tiles.get('wall', 0, 1.0)
    assert surf.get_bounding_rect().width == SIZE
    assert surf.get_at((4, 4))[3] > 0


def test_torch_glow_cached(renderer):
    glow = Tiles.torch_glow()
    assert Tiles.torch_glow() is glow
    assert glow.get_width() == SIZE * 2


# ── Обводка и свет ──
def test_finish_adds_outline_and_shadow(renderer):
    src = pygame.Surface((32, 32), pygame.SRCALPHA)
    pygame.draw.rect(src, (200, 120, 80), (8, 8, 16, 16))
    result = textures.finish(src)
    assert result.get_size() == (32, 32)
    assert result is not src
    # исходник не тронут
    assert src.get_at((0, 0))[3] == 0
    # контур: пиксель сразу за краем фигуры стал непрозрачным
    assert result.get_at((7, 16))[3] > 0
    assert result.get_at((16, 7))[3] > 0
    # тень ниже фигуры
    assert result.get_at((10, 25))[3] > 0


def test_finish_keeps_inside_bounds(renderer):
    src = pygame.Surface((16, 16), pygame.SRCALPHA)
    pygame.draw.rect(src, (255, 255, 255), (0, 0, 16, 16))
    result = textures.finish(src)
    assert result.get_size() == (16, 16)


def test_apply_light_returns_copy(renderer):
    src = pygame.Surface((8, 8))
    src.fill((100, 100, 100))
    dark = textures.apply_light(src, 0.5)
    assert dark is not src
    assert src.get_at((0, 0))[:3] == (100, 100, 100)
    # тень не нейтрально-серая: холодный сдвиг (R гаснет сильнее B)
    r, g, b = dark.get_at((0, 0))[:3]
    assert r < g < b
    assert r < 80 and b < 90


def test_light_factor_monotonic():
    near = textures.light_factor(0, 9, inner=0.7)
    far = textures.light_factor(9, 9, inner=0.7)
    assert near > far
    assert near >= 0.99
    assert abs(far - 0.7) < 0.01


def test_shade_and_mix():
    assert textures.shade((100, 100, 100), 0.5) == (50, 50, 50)
    assert textures.shade((100, 100, 100), 3) == (255, 255, 255)
    assert textures.mix((0, 0, 0), (100, 100, 100), 0.5) == (50, 50, 50)


# ── PNG-спрайты ──
def test_sprite_finish_creates_new_surface(renderer):
    from render.sprites import Sprites
    Sprites.init()
    img = Sprites.get('monsters/rat', (64, 64), finish=True)
    if img is None:                       # ассетов может не быть
        pytest.skip("нет PNG-ассетов")
    assert img.get_size() == (64, 64)
    assert img.get_bounding_rect().width >= 64 or img.get_bounding_rect().height > 0


def test_missing_sprite_returns_none(renderer):
    from render.sprites import Sprites
    Sprites.init()
    assert Sprites.get('definitely/not/here', (32, 32), finish=True) is None
