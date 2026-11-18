# Правки — третий проход (рефакторинг)

Дата: 2026-11-19. Применены к состоянию после второго прохода (FIXES_APPLIED.md).

Все **572 теста** проходят (+4 новых регрессионных в `tests/test_regression_fixes.py`).

## 1. Разбиение `render/tiles.py` (2132 LOC) → `render/tiles/` пакет

Было: один файл 2132 LOC с 60+ функциями-билдерами тайлов, константами,
палитрами, примитивами, реестром и классом `Tiles`.

Стало: пакет из 10 файлов по категориям:

| Файл | LOC | Содержимое |
|---|---|---|
| `__init__.py` | 82 | Реэкспорт публичного API |
| `_common.py` | 166 | Константы, палитры, примитивы (`_new`, `_clamp`, `_grime`, `_chip`, `_crack`) |
| `_dungeon.py` | 360 | Стены, пол, лестницы, выходы |
| `_nature.py` | 495 | Трава, деревья, биомы, пещеры, дороги |
| `_settlement.py` | 330 | Деревенские и городские тайлы (`_cobbles`, `_village_*`, `_city_*`) |
| `_dungeon_ruins.py` | 110 | Руины подземелья (`_dungeon_ruins_floor`, `_ruins_pillar`, `_dungeon_ground`) |
| `_terrain.py` | 183 | Песок, вода, камень, гоблинские постройки |
| `_objects.py` | 180 | Доски, пни, кусты, руда, ворота |
| `_mine.py` | 217 | Шахта, запертые двери, стрелки-переходы, сундуки |
| `_tombstone.py` | 52 | Надгробия |
| `_registry.py` | 236 | `_BUILDERS`, `_VARIANTS`, `_register_biome_tiles`, класс `Tiles` |

**Обратная совместимость**: `from render.tiles import Tiles` работает — `__init__.py`
реэкспортирует все публичные имена. Тесты не сломаны.

## 2. Разбиение `render/icon_renderer.py` (2190 LOC) → `render/icons/` пакет

Было: один класс `IconRenderer` на 2180 строк с 60+ методами рисования.

Стало: пакет из 7 файлов с **mixin-архитектурой**:

| Файл | LOC | Mixin | Содержимое |
|---|---|---|---|
| `__init__.py` | 197 | `IconRenderer` | Конструктор + диспетчер `draw_item` / `_make_item_surf` |
| `_base.py` | 11 | — | Константы `CANVAS`, `CX`, `TILE` |
| `_items.py` | 762 | `ItemMixin` | Оружие, броня, зелья, материалы, книги, свитки, руны |
| `_players.py` | 269 | `PlayerMixin` | Спрайты классов игрока (воин, разбойник, паладин, авантюрист) |
| `_class_icons.py` | 155 | `ClassIconMixin` | Маленькие иконки классов для UI |
| `_monsters.py` | 615 | `MonsterMixin` | Спрайты монстров и боссов по ключу `MONSTERS_BY_CH` |
| `_npcs.py` | 304 | `NPCMixin` | Городские NPC, торговцы, старейшина, UI-иконки |

`IconRenderer(ItemMixin, PlayerMixin, ClassIconMixin, MonsterMixin, NPCMixin, BaseRenderer)`
— все методы оказываются на одном `self`, перекрёстные вызовы (`_make_item_surf`
→ `_blade`, `draw_monster` → `_monster_dragon`) работают без изменений.

**Обратная совместимость**: `from render.icons import IconRenderer` работает.
Импорты в `render/renderer.py`, `tests/test_mp_ui.py`, `render/skill_icon_loader.py`
обновлены.

## 3. `ScreenMode` StrEnum (`core/modes.py`)

Добавлен модуль `core/modes.py` с `ScreenMode(StrEnum)` — 20 значений:
`MENU`, `MENU_SAVES`, `MENU_SETTINGS`, `MENU_CONTROLS`, `MENU_CREDITS`,
`MENU_MULTIPLAYER`, `MENU_MP_JOIN`, `CLASS_SELECT`, `GAME`, `INVENTORY`,
`STATS`, `STATS_RUN`, `CRAFT`, `SKILLS`, `QUESTS`, `SHOP`, `TRADE`,
`DIALOGUE`, `BIGMAP`, `DEV_CONSOLE`, `EXIT`.

**Обратная совместимость**: `StrEnum` сравним со строкой —
`mode == 'menu'` и `mode == ScreenMode.MENU` эквивалентны. Старый код не
нужно переписывать сразу. `main.AppState.MENU` и `AppState.EXIT` теперь
ссылаются на `ScreenMode.MENU` и `ScreenMode.EXIT`.

Также добавлены `ALL_MODES: frozenset[str]` и `is_valid_mode(value: str) -> bool`
для валидации сейвов и тестов.

## 4. Аннотации типов в `core/` (mypy --strict)

Полностью аннотированы:
- **`core/harvest.py`** — 8 функций: `make_harvestable`, `regrow_time`,
  `is_blocking`, `can_harvest`, `hit`, `tick`, `serialize`, `deserialize`.
- **`core/quests.py`** — 13 методов `QuestLog`: `add_available`, `accept`,
  `update_kill`, `update_collect`, `update_reach`, `update_craft`,
  `_complete`, `turn_in`, `to_dict`, `from_dict`, `accept_generated`,
  `turn_in_generated`.
- **`core/modes.py`** — новый модуль полностью типизирован.

Частично аннотированы (в предыдущей итерации): `core/config.py`,
`core/rules.py`, `core/entities.py`.

**Что осталось**: `core/skills.py` (43 функции без аннотаций), `core/overworld.py`
(27), `core/locations.py` (24), `core/world.py` (20), `core/save.py` (6).
Аннотирование этих модулей требует понимания динамических типов данных
(`dict[str, Any]` для монстров, предметов, квестов) и выходит за рамки
безопасного рефакторинга — это задача для отдельной итерации.

## 5. `game/game.py` (2320 LOC) — НЕ разбит

**Сознательное решение**. В `game/game.py` 111 методов, все ссылаются на `self`
(атрибуты `dungeon`, `player`, `log`, `add_float`, `_ui_hits` и т.д.).
Извлечение даже одного модуля (например, боёвки `attack` / `_single_attack` /
`_kill_monster` / `_launch_projectile` / `update_projectiles`) требует:

1. Вынесения методов в mixin-класс (как в `render/icons/`).
2. Перемещения ~250 строк кода.
3. Обновления импортов в `game/__init__.py`.
4. Рисков: методы используют приватные атрибуты `_kill_monster` (вызывается из
   `_monster_act`, `update_projectiles`, `_single_attack`) — все вызовы должны
   оставаться валидны.

В репозитории уже есть `game/input_router.py` (87 LOC) и `game/save_manager.py`
(104 LOC) — но они **не подключены** (`game/game.py` их не использует). Это
заготовки под будущий рефакторинг. Полноценное разбиение `game/game.py` на
6-8 mixin-модулей (CombatMixin, WorldMixin, InventoryMixin, InputMixin,
MonsterMixin, SaveMixin) — задача на 1-2 дня отдельной итерации.

## Финальные метрики

| Метрика | До (2-й проход) | После (3-й проход) | Дельта |
|---|---|---|---|
| Тестов проходят | 568 | **572** | +4 |
| Python-файлов в `render/tiles/` | 1 (2132 LOC) | 10 (файлы ≤495 LOC) | +9 |
| Python-файлов в `render/icons/` | 1 (2190 LOC) | 7 (файлы ≤762 LOC) | +6 |
| Самый большой файл в `render/` | 2190 LOC | 1980 (`screens_renderer.py`) | −210 |
| `core/` файлов с типизацией | 5 | 7 (+modes, +harvest, +quests полностью) | +2 |
| StrEnum для `screen_mode` | нет | да (20 значений) | +1 модуль |
| `ScreenMode` тестов | 0 | 2 | +2 |

## Регрессионные тесты (новые в `tests/test_regression_fixes.py`)

Добавлены 4 теста (всего в файле теперь 16):
- `test_screen_mode_enum_exists` — StrEnum, 20+ значений, `is_valid_mode()`
- `test_screen_mode_used_in_main_appstate` — `AppState.MENU == ScreenMode.MENU`
- `test_tiles_split_into_package` — пакет `render/tiles/`, оригинальный файл удалён
- `test_icon_renderer_split_into_package` — пакет `render/icons/`, mixin-архитектура

## Структура архива

```
rpg/
├── core/
│   ├── modes.py           ← НОВЫЙ: ScreenMode StrEnum
│   ├── harvest.py         ← типизирован
│   ├── quests.py          ← типизирован
│   ├── config.py, rules.py, entities.py, ... (без изменений)
├── render/
│   ├── tiles/             ← НОВЫЙ ПАКЕТ (10 файлов)
│   ├── icons/             ← НОВЫЙ ПАКЕТ (7 файлов, mixin-архитектура)
│   ├── display.py, projectile_renderer.py, ... (без изменений)
├── game/
│   ├── game.py            (без изменений, 2320 LOC)
│   ├── input_router.py, save_manager.py  (не подключены)
├── tests/
│   ├── test_regression_fixes.py  ← +4 теста (всего 16)
│   ├── test_*.py
├── main.py                ← AppState использует ScreenMode
├── FIXES_APPLIED.md       ← этот файл
└── ...
```
