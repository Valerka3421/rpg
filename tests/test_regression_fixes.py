"""Регрессионные тесты на баги, исправленные в этой итерации.

Связанные фиксы:
- lightning projectile: NameError 'tile' в render/projectile_renderer.py
- COL_WIN undefined в net/core.py при убийстве босса в MP
- core/display.py перемещён в render/display.py (архитектурный инвариант)
- default dev console password hash удалён
- ScreenMode StrEnum для типизации screen_mode
- разбиение render/tiles.py и render/icon_renderer.py
"""
import os
import sys

import pytest


# ── Lightning projectile regression ──
def test_lightning_projectile_renders_without_crash(screen):
    """`_draw_one` для kind='lightning' не должен падать с NameError 'tile'.

    Раньше переменная `tile = config.TILE` жила только в `draw_projectiles`,
    а в `_draw_one` обращались к ней в ветке lightning — `NameError` при первой
    же молнии. Теперь `tile` прокинут параметром.
    """
    import pygame
    from render.projectile_renderer import draw_projectiles, _draw_one

    class FakeProjectile:
        kind = 'lightning'
        color = (240, 230, 110)
        x = 0.0
        y = 0.0
        tx = 5.0
        ty = 5.0

    surf = pygame.Surface((128, 128))
    # _draw_one должно успешно отрисовать молнию
    _draw_one(surf, FakeProjectile(), 64, 64, 64)

    # И через публичную функцию тоже
    draw_projectiles(surf, [FakeProjectile()], (0, 0))


@pytest.mark.parametrize('kind', ['arrow', 'fireball', 'frost', 'lightning', 'heal'])
def test_all_projectile_kinds_render(screen, kind):
    """Все 5 видов Projectile.kind должны отрисовываться без исключений."""
    import pygame
    from render.projectile_renderer import draw_projectiles

    class FakeProjectile:
        pass

    p = FakeProjectile()
    p.kind = kind
    p.color = (200, 200, 200)
    p.x = 0.0
    p.y = 0.0
    p.tx = 5.0
    p.ty = 5.0

    surf = pygame.Surface((128, 128))
    draw_projectiles(surf, [p], (0, 0))


# ── COL_WIN import regression ──
def test_net_core_imports_col_win():
    """net/core.py должен импортировать COL_WIN — используется при убийстве босса.

    Раньше `COL_WIN` использовался в `_event(...)` но не был в импортах —
    `NameError` при первом убийстве босса в кооперативе.
    """
    from core.config import COL_WIN
    # Просто проверяем что символ доступен в config
    assert COL_WIN is not None

    # И что net.core его действительно импортировал
    import net.core as nc
    assert getattr(nc, 'COL_WIN', None) is not None


# ── Architecture invariant ──
def test_core_does_not_import_pygame():
    """core/ — чистые правила мира, без pygame (инвариант из CONTRIBUTING.md)."""
    import core
    core_dir = os.path.dirname(core.__file__)
    for fname in os.listdir(core_dir):
        if not fname.endswith('.py') or fname == '__init__.py':
            continue
        path = os.path.join(core_dir, fname)
        with open(path, encoding='utf-8') as f:
            src = f.read()
        # Игнорируем комментарии и docstrings — простой grep
        for line in src.splitlines():
            stripped = line.split('#', 1)[0].strip()
            if 'import pygame' in stripped and 'noqa' not in line.lower():
                pytest.fail(
                    f"core/{fname} импортирует pygame — нарушение инварианта: {line}"
                )


def test_core_display_moved_to_render():
    """core/display.py удалён (нарушал инвариант 'core/ без pygame'),
    перемещён в render/display.py. Импорты обновлены."""
    core_display = os.path.join(
        os.path.dirname(__import__('core').__file__), 'display.py'
    )
    assert not os.path.exists(core_display), \
        "core/display.py должен быть удалён (нарушал архитектурный инвариант)"

    render_display = os.path.join(
        os.path.dirname(__import__('render').__file__), 'display.py'
    )
    assert os.path.exists(render_display), \
        "render/display.py должен существовать (после переноса из core/)"


def test_core_game_removed():
    """core/game.py удалён — был мёртвым дубликатом game/game.py (2144 LOC)."""
    core_game = os.path.join(
        os.path.dirname(__import__('core').__file__), 'game.py'
    )
    assert not os.path.exists(core_game), \
        "core/game.py должен быть удалён (мёртвый дубликат game/game.py)"


def test_pytest_ini_removed():
    """pytest.ini удалён — конфигурация живёт в pyproject.toml [tool.pytest]."""
    repo_root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    pytest_ini = os.path.join(repo_root, 'pytest.ini')
    assert not os.path.exists(pytest_ini), \
        "pytest.ini должен быть удалён (дублирует [tool.pytest.ini_options] в pyproject.toml)"


# ── Dev console password hardening ──
def test_dev_console_disabled_by_default():
    """По умолчанию (без ETHELGARD_DEV_PASSWORD) хеш пароля — None,
    консоль отключена. Раньше в исходнике был хеш слова 'ethelgard'."""
    from core import config
    # Очищаем env var на момент теста
    old_hash = os.environ.pop('ETHELGARD_DEV_PASSWORD_HASH', None)
    old_plain = os.environ.pop('ETHELGARD_DEV_PASSWORD', None)
    try:
        result = config.dev_console_password_hash()
        assert result is None, \
            "Без ETHELGARD_DEV_PASSWORD хеш должен быть None (консоль отключена)"
        # verify должен возвращать False для любого пароля, когда хеш None
        assert config.verify_dev_console_password('ethelgard', None) is False
        assert config.verify_dev_console_password('anything', '') is False
    finally:
        if old_hash:
            os.environ['ETHELGARD_DEV_PASSWORD_HASH'] = old_hash
        if old_plain:
            os.environ['ETHELGARD_DEV_PASSWORD'] = old_plain


# ── ScreenMode StrEnum regression ──
def test_screen_mode_enum_exists():
    """core.modes.ScreenMode должен существовать и быть StrEnum.

    StrEnum — обратная совместимость со строковыми литералами:
    ``mode == 'menu'`` и ``mode == ScreenMode.MENU`` эквивалентны.
    """
    from core.modes import ScreenMode, ALL_MODES, is_valid_mode

    # StrEnum: сравнение со строкой работает
    assert ScreenMode.MENU == 'menu'
    assert ScreenMode.GAME == 'game'
    assert ScreenMode.INVENTORY == 'inventory'

    # Все 20 значений определены
    assert len(ALL_MODES) >= 20
    assert is_valid_mode('menu')
    assert is_valid_mode('game')
    assert is_valid_mode('class_select')
    assert not is_valid_mode('nonexistent_mode')

    # str(ScreenMode.MENU) возвращает 'menu' (а не 'ScreenMode.MENU')
    assert str(ScreenMode.MENU) == 'menu'


def test_screen_mode_used_in_main_appstate():
    """main.AppState.MENU и AppState.EXIT должны быть ScreenMode-значениями."""
    from core.modes import ScreenMode
    # main импортирует ScreenMode, AppState.MENU == ScreenMode.MENU
    import main
    assert main.AppState.MENU == ScreenMode.MENU == 'menu'
    assert main.AppState.EXIT == ScreenMode.EXIT == 'exit'


# ── tiles.py / icon_renderer.py split regression ──
def test_tiles_split_into_package():
    """render/tiles.py разбит на render/tiles/ пакет из 10 подмодулей."""
    import render.tiles as t
    # Все публичные имена доступны из пакета
    assert hasattr(t, 'Tiles')
    assert hasattr(t, '_BUILDERS')
    assert hasattr(t, '_VARIANTS')
    assert hasattr(t, 'BIOME_PALETTES')
    assert hasattr(t, 'SIZE')
    # Подмодули существуют
    import render.tiles._common
    import render.tiles._dungeon
    import render.tiles._nature
    import render.tiles._settlement
    import render.tiles._registry
    # Оригинальный файл удалён
    assert not os.path.exists(
        os.path.join(os.path.dirname(t.__file__), '..', 'tiles.py')
    ), "render/tiles.py должен быть удалён (заменён пакетом render/tiles/)"


def test_icon_renderer_split_into_package():
    """render/icon_renderer.py разбит на render/icons/ пакет (mixin-архитектура)."""
    from render.icons import IconRenderer
    import render.icons._items
    import render.icons._monsters
    import render.icons._players
    import render.icons._npcs
    # Все методы классов доступны через миксины
    assert hasattr(IconRenderer, 'draw_item')
    assert hasattr(IconRenderer, 'draw_player')
    assert hasattr(IconRenderer, 'draw_monster')
    assert hasattr(IconRenderer, 'draw_city_npc')
    assert hasattr(IconRenderer, 'draw_class_icon')
    # Оригинальный файл удалён
    import render.icons as pkg
    assert not os.path.exists(
        os.path.join(os.path.dirname(pkg.__file__), '..', 'icon_renderer.py')
    ), "render/icon_renderer.py должен быть удалён (заменён пакетом render/icons/)"


# ── game/game.py split regression ──
def test_game_split_into_package():
    """game/game.py (2320 LOC, 111 methods) разбит на game/game/ пакет.

    7 mixin-модулей + _common (shared imports) + __init__ (class Game).
    Все методы доступны через множественное наследование.
    """
    from game import Game
    import game.game._base
    import game.game._world
    import game.game._combat
    import game.game._inventory
    import game.game._monsters
    import game.game._input
    import game.game._menu
    import game.game._common

    # Все mixin-классы определены
    from game.game._base import GameBase
    from game.game._world import WorldMixin
    from game.game._combat import CombatMixin
    from game.game._inventory import InventoryMixin
    from game.game._monsters import MonsterMixin
    from game.game._input import InputMixin
    from game.game._menu import MenuMixin

    # Game наследует все mixin'ы
    assert issubclass(Game, GameBase)
    assert issubclass(Game, WorldMixin)
    assert issubclass(Game, CombatMixin)
    assert issubclass(Game, InventoryMixin)
    assert issubclass(Game, MonsterMixin)
    assert issubclass(Game, InputMixin)
    assert issubclass(Game, MenuMixin)

    # Ключевые методы доступны на Game через mixin'ы
    assert hasattr(Game, 'attack')          # CombatMixin
    assert hasattr(Game, 'new_game')        # GameBase
    assert hasattr(Game, 'travel_to')       # WorldMixin
    assert hasattr(Game, 'use_item')        # InventoryMixin
    assert hasattr(Game, 'monsters_turn')   # MonsterMixin
    assert hasattr(Game, 'handle_key')      # InputMixin
    assert hasattr(Game, 'open_shop')       # MenuMixin

    # Оригинальный файл удалён
    import game.game as pkg
    assert not os.path.exists(
        os.path.join(os.path.dirname(pkg.__file__), '..', 'game.py')
    ), "game/game.py должен быть удалён (заменён пакетом game/game/)"


def test_game_package_reexports_for_mock_compatibility():
    """game.game.random и game.game.save_settings доступны для mock.patch.

    Тесты патчат `game.game.random.random` и `game.game.save_settings` —
    эти имена должны быть атрибутами пакета game.game (через реэкспорт из _common).
    """
    import game.game as gg
    assert hasattr(gg, 'random'), "game.game.random должен быть доступен для mock.patch"
    assert hasattr(gg, 'save_settings'), "game.game.save_settings должен быть доступен"


# ── mypy annotations regression ──
def test_core_functions_have_type_annotations():
    """Все функции в core/ должны иметь type annotations (mypy --strict).

    Проверяем выборочно ключевые модули, аннотированные в этой итерации.
    """
    import ast
    from pathlib import Path

    core_dir = Path(__import__('core').__file__).parent
    modules_to_check = [
        'save.py', 'harvest.py', 'quests.py', 'modes.py',
        'world.py', 'overworld.py', 'locations.py', 'skills.py',
    ]
    untyped = []
    for modname in modules_to_check:
        path = core_dir / modname
        if not path.exists():
            continue
        tree = ast.parse(path.read_text(encoding='utf-8'))
        for node in ast.walk(tree):
            if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)):
                if node.returns is None:
                    untyped.append(f"{modname}:{node.lineno} def {node.name}")
    assert not untyped, \
        "Функции без return annotation: " + ", ".join(untyped[:5])


# ── Road invisible blockers regression (#11) ──
def test_road_cells_are_clear_of_walls_and_harvestables():
    """Дороги должны быть свободны от WALL-тайлов, harvestables и items.

    Раньше лес мог быть посажен до прорубания дорог, оставляя невидимые
    WALL-тайлы на дороге. Теперь generate_overworld() финализирует
    очисткой всех road cells.
    """
    import pygame
    pygame.init()
    pygame.display.set_mode((100, 100))
    from game import Game
    from core import overworld as ov
    from core.config import Tile

    g = Game.__new__(Game)
    g.new_game()
    d = g.overworld

    blockers = []
    for x0, y0, x1, y1 in ov.ROAD_SEGMENTS:
        if y0 == y1:
            for x in range(min(x0, x1), max(x0, x1) + 1):
                for y in range(y0 - 1, y0 + 2):
                    if 0 <= x < d.w and 0 <= y < d.h:
                        t = d.tiles[y][x]
                        if t == Tile.WALL:
                            blockers.append((x, y, 'WALL'))
                        if (x, y) in d.harvestables:
                            blockers.append((x, y, 'harvestable'))
        elif x0 == x1:
            for y in range(min(y0, y1), max(y0, y1) + 1):
                for x in range(x0 - 1, x0 + 2):
                    if 0 <= x < d.w and 0 <= y < d.h:
                        t = d.tiles[y][x]
                        if t == Tile.WALL:
                            blockers.append((x, y, 'WALL'))
                        if (x, y) in d.harvestables:
                            blockers.append((x, y, 'harvestable'))
    assert not blockers, \
        f"На дороге найдены блокирующие объекты: {blockers[:5]}"


# ── Server password constant-time comparison regression (#1) ──
def test_server_password_uses_hmac_compare_digest():
    """net/server.py должен использовать hmac.compare_digest для пароля."""
    import inspect
    from net import server as srv
    src = inspect.getsource(srv)
    # hmac.compare_digest должен быть в исходнике
    assert 'hmac.compare_digest' in src, \
        "Server password check должен использовать hmac.compare_digest"
    # Не должно быть plain != для пароля
    assert 'provided != self.server_password' not in src, \
        "Server password check не должен использовать plain !="


# ── Client send() logging regression (#2) ──
def test_client_send_logs_errors():
    """net/client.py Client.send() должен логировать ошибки, а не глотать их."""
    import inspect
    from net.client import Client
    src = inspect.getsource(Client.send)
    assert 'log.warning' in src or 'log.error' in src, \
        "Client.send() должен логировать ошибки через log.warning/error"


# ── Dead code removed regression (#6) ──
def test_dead_code_removed():
    """game/input_router.py и game/save_manager.py удалены (мёртвый код)."""
    import os
    game_dir = os.path.dirname(__import__('game').__file__)
    assert not os.path.exists(os.path.join(game_dir, 'input_router.py')), \
        "game/input_router.py должен быть удалён (мёртвый код)"
    assert not os.path.exists(os.path.join(game_dir, 'save_manager.py')), \
        "game/save_manager.py должен быть удалён (мёртвый код)"


# ── Skills window no bottom panel regression (#12) ──
def test_skills_window_has_no_bottom_description_panel():
    """Окно навыков не должно иметь нижней панели описания — только hover."""
    import inspect
    from render.screens_renderer import ScreensRenderer
    # _draw_class_tab не должен вызывать _draw_selected_node_panel
    src_class = inspect.getsource(ScreensRenderer._draw_class_tab)
    assert '_draw_selected_node_panel' not in src_class, \
        "_draw_class_tab не должен вызывать _draw_selected_node_panel"
    # _draw_craft_tab не должен вызывать _draw_craft_node_panel
    src_craft = inspect.getsource(ScreensRenderer._draw_craft_tab)
    assert '_draw_craft_node_panel' not in src_craft, \
        "_draw_craft_tab не должен вызывать _draw_craft_node_panel"


# ── screens_renderer.py split regression (#10) ──
def test_screens_renderer_split_into_package():
    """render/screens_renderer.py разбит на render/screens_renderer/ пакет.

    10 mixin-модулей + _common (shared imports) + __init__ (class ScreensRenderer).
    Все методы доступны через множественное наследование.
    """
    from render.screens_renderer import ScreensRenderer
    import render.screens_renderer._base
    import render.screens_renderer._stats
    import render.screens_renderer._inventory
    import render.screens_renderer._shop
    import render.screens_renderer._skills
    import render.screens_renderer._craft
    import render.screens_renderer._dev_console
    import render.screens_renderer._dialogue
    import render.screens_renderer._quests
    import render.screens_renderer._trade

    # Все mixin-классы определены
    from render.screens_renderer._base import ScreensBase
    from render.screens_renderer._stats import StatsMixin
    from render.screens_renderer._inventory import InventoryMixin
    from render.screens_renderer._shop import ShopMixin
    from render.screens_renderer._skills import SkillsMixin
    from render.screens_renderer._craft import CraftMixin
    from render.screens_renderer._dev_console import DevConsoleMixin
    from render.screens_renderer._dialogue import DialogueMixin
    from render.screens_renderer._quests import QuestsMixin
    from render.screens_renderer._trade import TradeMixin

    # ScreensRenderer наследует все mixin'ы + BaseRenderer
    from render.base_renderer import BaseRenderer
    assert issubclass(ScreensRenderer, ScreensBase)
    assert issubclass(ScreensRenderer, StatsMixin)
    assert issubclass(ScreensRenderer, InventoryMixin)
    assert issubclass(ScreensRenderer, ShopMixin)
    assert issubclass(ScreensRenderer, SkillsMixin)
    assert issubclass(ScreensRenderer, CraftMixin)
    assert issubclass(ScreensRenderer, DevConsoleMixin)
    assert issubclass(ScreensRenderer, DialogueMixin)
    assert issubclass(ScreensRenderer, QuestsMixin)
    assert issubclass(ScreensRenderer, TradeMixin)
    assert issubclass(ScreensRenderer, BaseRenderer)

    # Ключевые методы доступны через mixin'ы
    assert hasattr(ScreensRenderer, 'draw_stats')       # StatsMixin
    assert hasattr(ScreensRenderer, 'draw_inventory')   # InventoryMixin
    assert hasattr(ScreensRenderer, 'draw_shop')        # ShopMixin
    assert hasattr(ScreensRenderer, 'draw_skills')      # SkillsMixin
    assert hasattr(ScreensRenderer, 'draw_craft')       # CraftMixin
    assert hasattr(ScreensRenderer, 'draw_dev_console') # DevConsoleMixin
    assert hasattr(ScreensRenderer, 'draw_dialogue')    # DialogueMixin
    assert hasattr(ScreensRenderer, 'draw_quests')      # QuestsMixin
    assert hasattr(ScreensRenderer, 'draw_trade')       # TradeMixin

    # Оригинальный файл удалён
    import render.screens_renderer as pkg
    assert not os.path.exists(
        os.path.join(os.path.dirname(pkg.__file__), '..', 'screens_renderer.py')
    ), "render/screens_renderer.py должен быть удалён (заменён пакетом)"
