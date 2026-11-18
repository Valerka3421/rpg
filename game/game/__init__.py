"""Пакет одиночной игры.

class Game собирается из нескольких mixin-модулей по категориям:
- GameBase: конструктор, new_game, NPC, лог, FOV, update/draw
- WorldMixin: переходы между локациями, save
- CombatMixin: бой, движение, снаряды, статусы
- InventoryMixin: предметы, добыча, экипировка
- MonsterMixin: поведение мобов
- InputMixin: маршрутизация ввода
- MenuMixin: меню и магазин

Все методы оказываются на одном `self`, поэтому перекрёстные вызовы
(например _monster_act -> _single_attack -> _kill_monster -> _on_level_up)
работают без изменений.

Обратная совместимость: `from game import Game` работает как раньше.
Тесты, патчащие `game.game.random.random` или `game.game.save_settings`,
продолжают работать — эти имена реэкспортированы из _common.
"""
# Реэкспорт имён из _common для обратной совместимости с тестами,
# которые патчат `game.game.random` или `game.game.save_settings`.
from game.game._common import *  # noqa: F401,F403
from game.game._common import random, save_settings  # noqa: F401 — явно для mock.patch
from game.game._base import GameBase
from game.game._world import WorldMixin
from game.game._combat import CombatMixin
from game.game._inventory import InventoryMixin
from game.game._monsters import MonsterMixin
from game.game._input import InputMixin
from game.game._menu import MenuMixin


class Game(
    CombatMixin,
    WorldMixin,
    InventoryMixin,
    MonsterMixin,
    InputMixin,
    MenuMixin,
    GameBase,
):
    """Главная игровая логика: 111 методов разбиты по 7 mixin-модулям."""
    pass
