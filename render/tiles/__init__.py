"""Пакет процедурных тайлов 64×64 в духе Stoneshard.

Разбит на подмодули по категориям:
- _common: константы, палитры, примитивы
- _dungeon: стены, пол, лестницы, выходы
- _nature: трава, деревья, биомы, пещеры, дороги
- _settlement: деревенские и городские тайлы
- _dungeon_ruins: руины подземелья
- _terrain: песок, вода, камень, гоблинские постройки
- _objects: доски, пни, кусты, руда, ворота, стрелки, сундуки
- _mine: шахта, запертые двери
- _registry: _BUILDERS, _VARIANTS, класс Tiles

Все публичные имена реэкспортированы отсюда для обратной совместимости
с тем кодом, который импортирует `from render.tiles import Tiles`.
"""
from render.tiles._common import (
    SIZE, WALL_BASE, MORTAR, FLOOR_BASE, FLOOR_GROUT, TORCH_GLOW,
    MOSS, MOSS_DARK, RUST, BRONZE, BONE,
    GRASS_BASE, GRASS_DARK, GRASS_DRY, TREE_BASE, TREE_DARK, TRUNK,
    STONE, STONE_DARK,
    BIOME_PALETTES, VILLAGE_PALETTE, CITY_PALETTE,
)
from render.tiles._dungeon import (
    _wall, _wall_moss, _wall_torch, _floor,
    _stairs_down, _stairs_up, _exit_daylight,
    _wall_blocks,
)
from render.tiles._nature import (
    _grass, _tree, _cave_entrance, _road,
    _decor_pine, _decor_birch, _decor_rock, _decor_autumn,
    _decor_swamp, _decor_dark, _decor_meadow, _decor_dead, _decor_beach,
    _pine_tree, _birch_tree, _rock_cluster, _bare_tree,
    _autumn_tree, _swamp_tree, _dark_tree, _meadow_tree,
    _biome_grass,
    BIOME_LEAF_COLORS, BIOME_DECOR,
    BIOME_GRASS_BUILDERS, BIOME_TREE_BUILDERS,
)
from render.tiles._settlement import (
    _cobbles,
    _village_path, _village_plaza, _village_grass, _village_wall,
    _village_well, _village_stall, _village_statue, _village_torch_post,
    _city_grass, _city_path, _city_plaza, _city_wall, _city_house,
    _city_well, _city_stall, _city_statue, _city_torch,
)
from render.tiles._dungeon_ruins import (
    _dungeon_ruins_floor, _ruins_pillar, _dungeon_ground,
)
from render.tiles._terrain import (
    _sand, _water, _rock,
    _goblin_grass, _goblin_path, _goblin_plaza, _goblin_hut,
    _goblin_totem, _goblin_campfire,
)
from render.tiles._objects import (
    _quest_board, _stump, _berry_bush, _ore_rock, _ore_depleted, _gate,
)
from render.tiles._mine import (
    _arrow, _mine_wall, _mine_ground, _locked_door, _chest,
)
from render.tiles._tombstone import _tombstone

from render.tiles._registry import (
    _BUILDERS, _VARIANTS, _register_biome_tiles, Tiles,
)

__all__ = [
    'SIZE', 'BIOME_PALETTES', 'VILLAGE_PALETTE', 'CITY_PALETTE',
    '_BUILDERS', '_VARIANTS', '_register_biome_tiles', 'Tiles',
    # builders (для тестов)
    '_wall', '_floor', '_grass', '_tree', '_stairs_down', '_stairs_up',
    '_exit_daylight', '_cave_entrance', '_road', '_cobbles',
    '_village_path', '_village_plaza', '_village_grass', '_village_wall',
    '_village_well', '_village_stall', '_village_statue', '_village_torch_post',
    '_city_grass', '_city_path', '_city_plaza', '_city_wall', '_city_house',
    '_city_well', '_city_stall', '_city_statue', '_city_torch',
    '_dungeon_ruins_floor', '_ruins_pillar', '_dungeon_ground',
    '_sand', '_water', '_rock',
    '_goblin_grass', '_goblin_path', '_goblin_plaza', '_goblin_hut',
    '_goblin_totem', '_goblin_campfire',
    '_quest_board', '_stump', '_berry_bush', '_ore_rock', '_ore_depleted',
    '_gate', '_arrow', '_chest', '_tombstone',
    '_mine_wall', '_mine_ground', '_locked_door',
]
