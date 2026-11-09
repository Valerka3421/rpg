"""Общие фикстуры."""
import itertools
import os
import pathlib
import shutil
import random

# Видео/аудио-заглушки — тесты рисуют в обычную Surface, окно не нужно.
os.environ.setdefault('SDL_VIDEODRIVER', 'dummy')
os.environ.setdefault('SDL_AUDIODRIVER', 'dummy')

import pytest

import sys
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

_TMP_COUNTER = itertools.count()


@pytest.fixture
def make_game():
    from game import Game

    def _make(seed=0):
        random.seed(seed)
        g = Game.__new__(Game)
        g.new_game()
        return g

    return _make


@pytest.fixture(autouse=True)
def _isolate_saves(tmp_path, monkeypatch):
    """Любой save() из тестов пишет в песочницу, а не в корень проекта.

    Иначе автосейв (например, при подъёме по лестнице) создаёт
    savegame_1.json рядом с исходниками.
    """
    import core.save as save_mod
    saves = tmp_path / 'saves'
    saves.mkdir(exist_ok=True)
    monkeypatch.setattr(save_mod, 'BASE_DIR', str(saves))


@pytest.fixture(scope='session')
def screen():
    """Один общий экран (dummy-драйвер) для тестов рендера."""
    import pygame
    pygame.init()
    return pygame.display.set_mode((1280, 720))


@pytest.fixture
def renderer(screen):
    from render import Renderer
    return Renderer(screen)


@pytest.fixture
def tmp_path():
    """Своя замена pytest.tmp_path.

    Стандартная фикстура создаёт каталог с правами 0o700 и в системном
    %TEMP%, а в песочницах/ограниченных окружениях такой каталог потом
    недоступен (WinError 5). Здесь — обычный каталог в проекте.
    """
    base = pathlib.Path(__file__).resolve().parent.parent / '.test_tmp'
    base.mkdir(parents=True, exist_ok=True)
    path = base / f't{next(_TMP_COUNTER)}'
    path.mkdir(exist_ok=True)
    yield path
    shutil.rmtree(path, ignore_errors=True)
