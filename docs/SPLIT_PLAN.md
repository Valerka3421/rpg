# План разбиения больших файлов

## ✅ Сделано: `core/data.py` → `core/data/` пакет

**1805 строк → 7 файлов:**

| Файл | Строк | Содержимое |
|---|---|---|
| `core/data/__init__.py` | 70 | re-export всех имён для обратной совместимости |
| `core/data/monsters.py` | 105 | MONSTERS, BOSSES, BOSS_DROPS, MONSTER_DROPS, scale_*, mana_crystal_* |
| `core/data/items.py` | 730 | ITEMS, MATERIAL_KEYS, RARITY_CHANCES, RARITY_COLORS, item_*, parse_item_key, PERK_INFO |
| `core/data/classes.py` | 530 | WCLASS_NAMES, ARMOR_TYPE_NAMES, _cls, CLASSES, CLASS_ORDER, class_* |
| `core/data/recipes.py` | 150 | SKILLS, SKILL_ORDER, CRAFT_TABS, craft_*, get_craftable_items, make_shop_stock, xp_to_next |
| `core/data/statuses.py` | 105 | STATUSES, STATUS_MODS, ABILITY_TO_STATUS |
| `core/data/quests.py` | 195 | QUESTS, quests_by_giver, ItemInstance, generate_board_quests |

**Все импорты `from core.data import X` продолжают работать** через
`__init__.py`, который делает re-export. Тесты не сломаны: 533 проходят,
2 pre-existing failures остались.

Circular imports решены через lazy imports в `items.py:item_category`
и `items.py:parse_item_key` (они используют `SKILLS` и `ItemInstance`,
которые в `recipes.py` и `quests.py` соответственно).

## ✅ Сделано: `game/game.py` → `game/game/` пакет (mixin-архитектура)

**2320 строк → 8 файлов:**

| Файл | LOC | Mixin | Методов |
|---|---|---|---|
| `__init__.py` | 43 | `Game` | 0 (наследует все mixins) |
| `_common.py` | 83 | — | shared imports + STAT_ORDER |
| `_base.py` | 558 | `GameBase` | 23 |
| `_world.py` | 343 | `WorldMixin` | 10 |
| `_combat.py` | 429 | `CombatMixin` | 17 |
| `_inventory.py` | 423 | `InventoryMixin` | 26 |
| `_monsters.py` | 93 | `MonsterMixin` | 4 |
| `_input.py` | 442 | `InputMixin` | 11 |
| `_menu.py` | 120 | `MenuMixin` | 20 |

`class Game(CombatMixin, WorldMixin, InventoryMixin, MonsterMixin, InputMixin, MenuMixin, GameBase)`
— все методы на одном `self`, перекрёстные вызовы работают без изменений.

## ✅ Сделано: `render/tiles.py` → `render/tiles/` пакет

**2132 строки → 10 файлов** по категориям: `_common`, `_dungeon`, `_nature`,
`_settlement`, `_dungeon_ruins`, `_terrain`, `_objects`, `_mine`, `_tombstone`, `_registry`.

## ✅ Сделано: `render/icon_renderer.py` → `render/icons/` пакет

**2190 строк → 7 файлов** (mixin): `_base`, `_items`, `_players`, `_class_icons`,
`_monsters`, `_npcs`, `__init__`.

## ✅ Сделано: `net/core.py` частично разбит

Вынесены `net/player_registry.py` (146 LOC) и `net/snapshot_builder.py` (81 LOC).
Ядро `net/core.py` (1292 LOC) осталось — требует дальнейшего разбиения.

## ✅ Сделано: `render/screens_renderer.py` → `render/screens_renderer/` пакет

**2076 строк → 12 файлов** (mixin-архитектура):

| Файл | LOC | Mixin | Методов |
|---|---|---|---|
| `__init__.py` | 32 | `ScreensRenderer` | 0 (наследует все mixins + BaseRenderer) |
| `_common.py` | 27 | — | shared imports |
| `_base.py` | 100 | `ScreensBase` | 6 (__init__, _modal_bg, утилиты) |
| `_stats.py` | 150 | `StatsMixin` | 7 (draw_stats, _d_*) |
| `_inventory.py` | 340 | `InventoryMixin` | 7 (draw_inventory, equipment, slots) |
| `_shop.py` | 139 | `ShopMixin` | 3 (draw_shop, buy/sell) |
| `_skills.py` | 799 | `SkillsMixin` | 26 (draw_skills, trees, tooltips, tabs) |
| `_craft.py` | 206 | `CraftMixin` | 2 (draw_craft, minigame) |
| `_dev_console.py` | 89 | `DevConsoleMixin` | 1 |
| `_dialogue.py` | 118 | `DialogueMixin` | 1 |
| `_quests.py` | 117 | `QuestsMixin` | 2 (quests, run_stats) |
| `_trade.py` | 129 | `TradeMixin` | 2 (trade, button) |

### `net/core.py` (1376 строк)

**План был:** разбить на
- `net/world_authority.py` — авторитетное состояние мира
- `net/snapshot_builder.py` — построение снапшотов
- `net/player_registry.py` — управление игроками

**Почему отложено:** `GameCore.get_snapshot()` (70 строк) напрямую читает
~20 полей `self`. Вынос в отдельный класс потребует либо передавать
`GameCore` целиком (что не уменьшает coupling), либо дублировать поля.
`add_player`/`remove_player` используют `self.players`, `self.lock`,
`self.dungeon`, `self.regen_timers`, `self._inventory_cache` —
перемещение в `PlayerRegistry` потребует передавать все эти структуры.

Та же проблема: API `GameCore` остаётся, реализация переезжает.
Чистая выгода — 150-200 строк из `net/core.py`. Но риск сломать MP-тесты
значительный.

### `render/icon_renderer.py` (2055 строк) и `render/screens_renderer.py` (1957 строк)

**План был:** разбить по экранам
- `render/screens/inventory.py`, `skills.py`, `craft.py`, `dev_console.py`, `shop.py`

**Почему отложено:** `ScreensRenderer` имеет общий кэш шрифтов, общие
методы рисования кнопок/окон, общие импорты. Каждый экран — это 5-10
методов, использующих `self.screen`, `self.font_*`, `self.text()`,
`self.draw_button_bg()`. Разбиение потребует либо:
1. Класс-родитель `BaseScreen` с общими методами (но тогда каждый экран
   наследует и не уменьшает общий объём кода)
2. Передача renderer-а в каждую функцию экрана (но это меняет API)

Тесты на рендер (`test_new_features.py`, `test_textures.py`) хрупкие и
зависят от конкретных методов `ScreensRenderer`. Разбиение сломало бы
30+ тестов.

## Резюме

**Сделано безопасно:** `core/data.py` → пакет с 7 файлами + re-export.
533/535 тестов проходят, импорты не изменились.

**Отложено обоснованно:** разбиение классов с глубокой связностью
(`Game`, `GameCore`, `ScreensRenderer`) требует изменения API и
переписывания 200+ тестов — это отдельная рефакторинг-итерация после
стабилизации P0–P5 правок. Текущая структура (большие файлы с чёткими
секциями «═══») приемлема для продолжения разработки.

**Рекомендация:** перед разбиением `game/game.py`:
1. Сначала стабилизировать P0–P5 (что сделано в этой итерации)
2. Добавить интеграционные тесты на Game API (`g.save()`, `g.attack()`,
   `g.handle_key()` — без проверки внутренней реализации)
3. Только потом выносить логику в helpers, оставив `Game` тонкой
   фасадной обёрткой
