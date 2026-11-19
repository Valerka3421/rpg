"""Пакет рендера модальных экранов.

class ScreensRenderer собирается из нескольких mixin-модулей по категориям:
- ScreensBase: __init__, modal bg, утилиты
- StatsMixin: экран характеристик
- InventoryMixin: инвентарь, экипировка
- ShopMixin: магазин
- SkillsMixin: древо навыков (самый большой модуль)
- CraftMixin: крафт
- DevConsoleMixin: консоль разработчика
- DialogueMixin: диалоги с NPC
- QuestsMixin: журнал квестов, статистика
- TradeMixin: торговля между игроками

Все методы на одном `self` — перекрёстные вызовы работают без изменений.
"""
from render.base_renderer import BaseRenderer
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


class ScreensRenderer(ScreensBase, StatsMixin, InventoryMixin, ShopMixin, SkillsMixin, CraftMixin, DevConsoleMixin, DialogueMixin, QuestsMixin, TradeMixin, BaseRenderer):
    """Рендер модальных экранов: статы, инвентарь, крафт, навыки, квесты."""
    pass
