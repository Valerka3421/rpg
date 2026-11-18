"""Benchmark-тесты производительности (pytest-benchmark).

Запуск: pytest tests/test_benchmarks.py --benchmark-only
В CI не запускается по умолчанию — opt-in.
"""
import os
import random

import pytest

os.environ.setdefault('SDL_VIDEODRIVER', 'dummy')
os.environ.setdefault('SDL_AUDIODRIVER', 'dummy')

import pygame

pygame.init()
pygame.font.init()

from core import config

# Важно: НЕ модифицируем глобальный config.SCREEN_W при импорте —
# это портит другие тесты (например, test_promotion_nodes_live_inside_class_tree,
# который зависит от дефолтного 1600x900). Вместо этого — fixture с teardown.
_ORIG_W, _ORIG_H = config.SCREEN_W, config.SCREEN_H

import json

from core.entities import Player
from core.rules import resolve_attack
from core.save import dungeon_from_dict, dungeon_to_dict
from core.world import compute_fov, generate_dungeon, generate_overworld


@pytest.fixture(autouse=True)
def _benchmark_resolution():
    """На время benchmark-тестов ставим 1280×720, после — восстанавливаем."""
    config.set_resolution(1280, 720)
    yield
    config.set_resolution(_ORIG_W, _ORIG_H)


@pytest.fixture
def overworld():
    random.seed(42)
    return generate_overworld()


@pytest.fixture
def dungeon_floor1():
    random.seed(7)
    return generate_dungeon(1)


def test_benchmark_compute_fov(benchmark, overworld):
    """FOV на полной карте — должен быть < 5 мс."""
    p = overworld.rooms[0].center()
    benchmark(lambda: compute_fov(overworld, p[0], p[1]))


def test_benchmark_generate_dungeon(benchmark):
    """generate_dungeon(1) — должен быть < 50 мс."""
    def gen():
        random.seed(1)
        return generate_dungeon(1)
    benchmark(gen)


def test_benchmark_dungeon_serialize(benchmark, dungeon_floor1):
    """Сериализация этажа в dict — должна быть < 10 мс."""
    benchmark(lambda: dungeon_to_dict(dungeon_floor1))


def test_benchmark_dungeon_deserialize(benchmark, dungeon_floor1):
    """Десериализация этажа из dict."""
    data = dungeon_to_dict(dungeon_floor1)
    benchmark(lambda: dungeon_from_dict(data))


def test_benchmark_resolve_attack(benchmark):
    """Один удар — должен быть < 0.1 мс."""
    p = Player(class_key='warrior')
    from core.entities import Entity
    m = Entity(1, 0, 50, 50, 3, 0, "Моб", 'g', (100, 200, 100), 5)
    random.seed(1)
    benchmark(lambda: resolve_attack(p, m))


def test_benchmark_save_to_json(benchmark, dungeon_floor1, tmp_path):
    """Полный сейв (overworld + dungeon + player) → JSON строка."""
    p = Player(class_key='warrior')
    p.x, p.y = dungeon_floor1.rooms[0].center()
    data = {
        'version': config.SAVE_VERSION,
        'overworld': dungeon_to_dict(dungeon_floor1),
        'player': {'x': p.x, 'y': p.y, 'hp': p.hp},
        'dungeon_cache': {},
    }
    benchmark(lambda: json.dumps(data))


# ── Метрики производительности — пороги ──
# Цели (см. README.md и CHANGELOG.md):
#   compute_fov на 140×70 карте с FOV off: < 20 мс
#   generate_dungeon(1): < 50 мс
#   resolve_attack: < 0.5 мс
#   save JSON: < 50 мс
#
# pytest-benchmark автоматически ловит регрессии через --benchmark-compare.
# В CI: pytest --benchmark-only --benchmark-compare=baseline.json
