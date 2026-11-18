"""Регрессионные тесты на баги, исправленные в этой итерации.

Связанные фиксы:
- lightning projectile: NameError 'tile' в render/projectile_renderer.py
- COL_WIN undefined в net/core.py при убийстве босса в MP
- core/display.py перемещён в render/display.py (архитектурный инвариант)
- default dev console password hash удалён
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
