"""Реестр билдеров тайлов и кэш Tiles.

Собирает все функции-билдеры из подмодулей в _BUILDERS dict,
определяет количество вариантов в _VARIANTS, предоставляет класс
Tiles — кэш готовых поверхностей с запечённым освещением.
"""

import pygame

from render import textures
from render.tiles._common import SIZE, BIOME_PALETTES
from render.tiles._dungeon import (
    _wall, _wall_moss, _wall_torch, _floor,
    _stairs_down, _stairs_up, _exit_daylight,
)
from render.tiles._nature import (
    _grass, _tree, _cave_entrance, _road,
    BIOME_GRASS_BUILDERS, BIOME_TREE_BUILDERS,
)
from render.tiles._settlement import (
    _village_wall,
    _village_statue, _city_grass, _city_path, _city_plaza, _city_wall, _city_house,
    _city_well, _city_stall, _city_statue, _city_torch,
)
from render.tiles._dungeon_ruins import (
    _dungeon_ruins_floor, _ruins_pillar, _dungeon_ground,
)
from render.tiles._terrain import (
    _sand, _water, _rock, _goblin_grass, _goblin_path,
    _goblin_plaza, _goblin_hut, _goblin_totem, _goblin_campfire,
)
from render.tiles._objects import (
    _quest_board, _stump, _berry_bush, _ore_rock, _ore_depleted, _gate,
)
from render.tiles._mine import (
    _arrow, _mine_wall, _mine_ground, _locked_door, _chest,
)
from render.tiles._tombstone import _tombstone

_BUILDERS = {
    'wall': _wall,
    'wall_moss': _wall_moss,
    'wall_torch': _wall_torch,
    'floor': _floor,
    'stairs': _stairs_down,
    'upstairs': _stairs_up,
    'exit': _exit_daylight,
    'grass': _grass,
    'tree': _tree,
    'cave': _cave_entrance,
}

# сколько вариантов каждого вида (для выбора по координате)
_VARIANTS = {
    'wall': 3, 'wall_moss': 1, 'wall_torch': 1,
    'floor': 12, 'grass': 8, 'tree': 3,
    'stairs': 1, 'upstairs': 1, 'exit': 1, 'cave': 1,
    'road': 4, 'village_grass': 8, 'village_path': 4,
    'village_plaza': 4, 'village_wall': 3, 'village_well': 1,
    'village_stall': 2, 'village_statue': 1, 'village_torch_post': 1,
    'city_grass': 8, 'city_path': 4, 'city_plaza': 3,
    'city_wall': 4, 'city_house': 3, 'city_well': 1,
    'city_stall': 2, 'city_statue': 1, 'city_torch': 1,
    'dungeon_ground': 6, 'dungeon_ruins': 4, 'rock': 3,
    'ruins_pillar': 2,
    'goblin_grass': 8, 'goblin_path': 4, 'goblin_plaza': 4,
    'goblin_hut': 3, 'goblin_totem': 1, 'goblin_campfire': 1,
    'quest_board': 1,
    'stump': 2, 'berry_bush': 2, 'berry_bush_empty': 2,
    'ore_rock': 2, 'ore_depleted': 2, 'gate': 1,
    'arrow_n': 3, 'arrow_e': 3, 'arrow_s': 3, 'arrow_w': 3,
    'mine_wall': 2, 'mine_ground': 6, 'locked_door': 1,
    'chest': 1, 'tombstone': 3,
    'sand': 4, 'water': 3,
    # ── Пляж (новый биом) ──
    'beach_sand': 4, 'beach_tree': 2,
    # ── Руины древнего города ──
    'city_ruins_wall': 3, 'city_ruins_statue': 1, 'city_ruins_floor': 6,
}


def _register_biome_tiles():
    """Добавить в кэш по паре уникальных тайлов на каждый из восьми биомов."""
    for name in BIOME_PALETTES:
        _BUILDERS[f'grass_{name}'] = BIOME_GRASS_BUILDERS[name]
        _BUILDERS[f'tree_{name}'] = BIOME_TREE_BUILDERS[name]
        _VARIANTS[f'grass_{name}'] = 6
        _VARIANTS[f'tree_{name}'] = 3


_BUILDERS.update({
    'road': _road,
    'village_grass': _city_grass,
    'village_path': _city_path,
    'village_plaza': _city_plaza,
    'village_wall': _city_house,
    'village_well': _city_well,
    'village_stall': _city_stall,
    'village_statue': _city_statue,
    'village_torch_post': _city_torch,
    'city_grass': _city_grass,
    'city_path': _city_path,
    'city_plaza': _city_plaza,
    'city_wall': _city_wall,
    'city_house': _city_house,
    'city_well': _city_well,
    'city_stall': _city_stall,
    'city_statue': _city_statue,
    'city_torch': _city_torch,
    'dungeon_ground': _dungeon_ground,
    'dungeon_ruins': _dungeon_ruins_floor,
    'ruins_pillar': _ruins_pillar,
    'rock': _rock,
    'sand': _sand,
    'water': _water,
    'goblin_grass': _goblin_grass,
    'goblin_path': _goblin_path,
    'goblin_plaza': _goblin_plaza,
    'goblin_hut': _goblin_hut,
    'goblin_totem': _goblin_totem,
    'goblin_campfire': _goblin_campfire,
    'quest_board': _quest_board,
    'stump': _stump,
    'berry_bush': _berry_bush,
    'berry_bush_empty': lambda variant: _berry_bush(variant, berries=False),
    'ore_rock': _ore_rock,
    'ore_depleted': _ore_depleted,
    'gate': _gate,
    'arrow_n': lambda variant: _arrow(variant, 'n'),
    'arrow_e': lambda variant: _arrow(variant, 'e'),
    'arrow_s': lambda variant: _arrow(variant, 's'),
    'arrow_w': lambda variant: _arrow(variant, 'w'),
    'mine_wall': _mine_wall,
    'mine_ground': _mine_ground,
    'locked_door': _locked_door,
    'chest': _chest,
    'tombstone': _tombstone,
    # ── Пляж (новый биом) ──
    'beach_sand': lambda variant: _sand(variant),
    'beach_tree': lambda variant: _tree(variant, BIOME_PALETTES['beach']),
    # ── Руины города — повторно используют существующие builders ──
    'city_ruins_wall': lambda variant: _village_wall(variant),
    'city_ruins_statue': lambda variant: _village_statue(variant),
    'city_ruins_floor': lambda variant: _dungeon_ruins_floor(variant),
})
_register_biome_tiles()



class Tiles:
    """Кэш готовых тайлов: вид + вариант + уровень освещения."""

    _cache = {}
    _glow = None

    @classmethod
    def clear(cls):
        cls._cache.clear()
        cls._glow = None

    @classmethod
    def prewarm(cls):
        """Собрать все варианты заранее: первый кадр этажа не должен
        подтормаживать на генерации тайлов."""
        for kind, count in _VARIANTS.items():
            for variant in range(count):
                cls.get(kind, variant, 1.0)
                cls.get(kind, variant, 0.72)

    @classmethod
    def variant(cls, kind, x, y):
        count = _VARIANTS.get(kind, 1)
        if kind == 'wall':
            # редкие особые стены: факел и мох
            roll = textures.hash01(x, y, seed=41)
            if roll < 0.07:
                return 1, 'wall_torch'
            if roll < 0.16:
                return 0, 'wall_moss'
            return textures.hash_int(x, y, 41, count), 'wall'
        return textures.hash_int(x, y, 13, count), kind

    @classmethod
    def get(cls, kind, variant=0, light=1.0):
        """Тайл с запечённым освещением (квантован, чтобы кэш не пух)."""
        band = int(round(max(0.3, min(1.0, light)) * 20))
        v = variant % _VARIANTS.get(kind, 1)      # ← один v для ключа и builder
        key = (kind, v, band)
        surf = cls._cache.get(key)
        if surf is not None:
            return surf
        builder = _BUILDERS.get(kind)
        if builder is None:
            builder = _floor
        base = builder(v)
        if band < 20:
            base = textures.apply_light(base, band / 20.0)
        if len(cls._cache) >= 900:
            cls._cache.clear()
        cls._cache[key] = base
        return base

    @classmethod
    def torch_glow(cls):
        """Тёплый ореол факела: аддитивная добавка, без «выжженного» диска."""
        if cls._glow is None:
            size = SIZE * 2
            glow = pygame.Surface((size, size))
            glow.fill((0, 0, 0))
            center = size // 2
            for radius in range(center, 0, -1):
                t = radius / center
                k = int(88 * (1 - t) ** 2)
                if k <= 0:
                    continue
                pygame.draw.circle(glow, (k, int(k * 0.58), int(k * 0.25)),
                                   (center, center), radius)
            cls._glow = glow
        return cls._glow


_BUILDERS.update({

    'village_stall': _city_stall, 'village_statue': _city_statue,
    'village_torch_post': _city_torch,
    'city_stall': _city_stall, 'city_statue': _city_statue,
    'city_torch': _city_torch,
    'dungeon_ruins': _dungeon_ruins_floor, 'ruins_pillar': _ruins_pillar,
})
_VARIANTS.update({
    'village_stall': 2, 'village_statue': 1, 'village_torch_post': 1,
    'city_stall': 2, 'city_statue': 1, 'city_torch': 1,
    'dungeon_ruins': 4, 'ruins_pillar': 2,
})
