"""Перечисление экранов приложения.

Строго типизированная замена строковым литералам ``screen_mode``.
Обратная совместимость: ``ScreenMode`` — это ``StrEnum``, поэтому
``mode == 'menu'`` и ``mode == ScreenMode.MENU`` эквивалентны.
Старый код не нужно переписывать сразу — миграция постепенная.

Использование в новом коде::

    from core.modes import ScreenMode

    if game.screen_mode == ScreenMode.GAME:
        ...
    game.screen_mode = ScreenMode.MENU

Использование в main.py (AppState)::

    from core.modes import ScreenMode
    state = ScreenMode.MENU
    while state != ScreenMode.EXIT:
        ...
"""
from __future__ import annotations

import sys

if sys.version_info >= (3, 11):
    from enum import StrEnum
else:  # pragma: no cover
    from enum import Enum

    class StrEnum(str, Enum):
        """Бэкпорт StrEnum для Python 3.10."""

        def __str__(self) -> str:
            return self.value


class ScreenMode(StrEnum):
    """Все возможные экраны (состояния) приложения."""

    # ── Главное меню и его подпункты ──
    MENU = 'menu'
    MENU_SAVES = 'menu_saves'
    MENU_SETTINGS = 'menu_settings'
    MENU_CONTROLS = 'menu_controls'
    MENU_CREDITS = 'menu_credits'
    MENU_MULTIPLAYER = 'menu_multiplayer'
    MENU_MP_JOIN = 'menu_mp_join'

    # ── Игровые экраны ──
    CLASS_SELECT = 'class_select'
    GAME = 'game'
    INVENTORY = 'inventory'
    STATS = 'stats'
    STATS_RUN = 'stats_run'
    CRAFT = 'craft'
    SKILLS = 'skills'
    QUESTS = 'quests'
    SHOP = 'shop'
    TRADE = 'trade'
    DIALOGUE = 'dialogue'
    BIGMAP = 'bigmap'

    # ── Разработческие ──
    DEV_CONSOLE = 'dev_console'

    # ── Прочие ──
    EXIT = 'exit'


# Все легитимные значения screen_mode — для валидации сейвов и тестов.
ALL_MODES: frozenset[str] = frozenset(str(m.value) for m in ScreenMode)


def is_valid_mode(value: str) -> bool:
    """Проверить, что строка — легитимное значение screen_mode."""
    return value in ALL_MODES
