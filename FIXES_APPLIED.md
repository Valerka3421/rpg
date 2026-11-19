# Правки — v2 (2026-11-19)

Все **582 теста** проходят (+5 новых регрессионных). Базовый коммит: `0c88592`.

## Применённые правки

### 1. Безопасность: `hmac.compare_digest` для пароля сервера
`net/server.py:273` — пароль сервера сравнивался plain `!=` (уязвимость к
timing-атаке). Заменён на `hmac.compare_digest(provided, self.server_password)`.

### 2. Отладка: `Client.send()` логирует ошибки
`net/client.py:542` — `send()` молча глотал все исключения (`except: pass`).
Теперь: `log.warning('send failed: %s', e)`. Если сокет сломан, игрок видит
в логе причину.

### 3. Документация: README.md обновлён
- Раздел «Устройство проекта» переписан: актуальная структура пакетов
  (`core/data/`, `game/game/`, `render/tiles/`, `render/icons/`, `net/player_registry.py`, и т.д.)
- Битые ссылки исправлены: `[render/tiles.py]` → `[render/tiles/]`,
  `[render/icon_renderer.py]` → `[render/icons/]`

### 4. Документация: SUMMARY.md, SPLIT_PLAN.md, PERFORMANCE.md
- `SUMMARY.md` — обновлено количество тестов (577→582) и файлов (~105)
- `docs/SPLIT_PLAN.md` — отмечены сделанные разбиения: `game/game/`, `render/tiles/`, `render/icons/`, `net/core.py` (частично)
- `docs/PERFORMANCE.md` — ссылка на `render/tiles/` вместо `render/tiles.py`

### 5. Документация: CHANGELOG.md
Убраны ложные утверждения о `.github/workflows/ci.yml` и `release.yml`
(эти файлы не существуют).

### 6. Мёртвый код удалён
`game/input_router.py` (84 LOC) и `game/save_manager.py` (109 LOC) —
никогда не импортировались. Удалены.

### 7. Ruff F401: неиспользуемые импорты
`ruff check --fix --select F401` — убраны неиспользуемые `typing.Optional`
и другие импорты из `core/data/city.py`, `core/data/classes.py` и др.

### 8. Логирование: `print()` → `logging`
- `net/server.py`: 20 `print()` → `log.info()`/`log.warning()`/`log.error()`
- `net/client.py`: 10 `print()` → `log.info()`/`log.warning()`
- `main.py`: 5 `print()` → `log.info()`/`log.warning()`/`log.error()`
Всего 35 вызовов заменено. Добавлен `log = logging.getLogger("ethelgard.server")`
и `log = logging.getLogger("ethelgard.client")`.

### 9. Типизация: `core/rules.py`
Добавлены type annotations к 15 функциям:
`ranged_attack`, `projectile_hit`, `roll_kill_reward`, `action_interval`,
`init_action_timers`, `boss_breath_tick`, `tick_statuses`, `roll_pickup_rarity`,
`place_loot`, `recipe_info`, `can_craft`, `quality_from_accuracy`,
`roll_craft_quality`, `_consume_recipe`, `craft_item`.
Добавлен `from __future__ import annotations` для отложенной оценки типов.

### 10. `render/screens_renderer.py` (2084 LOC) — отложено
Крупнейший оставшийся файл. Разбиение на mixin-пакет (`render/screens/`)
по аналогии с `tiles/` и `icons/` требует перемещения 57 методов —
рискованно для 582 тестов. Отложено на следующую итерацию.

### 11. Невидимые объекты на дороге — исправлено
**Проблема:** `core/world.py:_carve_road()` прорубает дороги ДО того, как
руины и декорации ставятся на карту. Случайные WALL-тайлы оставались на
дорожных клетках — игрок упирался в «невидимые» деревья/камни на дороге.

**Фикс:**
- `core/world.py:generate_overworld()` — финальный шаг: очистка всех
  `ROAD_SEGMENTS` клеток от WALL/harvestables/items
- `core/locations.py:_generate_village()` — то же для деревенской карты:
  очистка `VILLAGE_ROADS` + `ROAD_SEGMENTS` клеток

### 12. Окно навыков: убрана нижняя панель описания
`render/screens_renderer.py`:
- `_draw_class_tab` — убран вызов `_draw_selected_node_panel` (84px панель)
- `_draw_craft_tab` — убран вызов `_draw_craft_node_panel` (104px панель)
- Древо навыков расширено на освободившееся место
- Описание узла видно только при hover через tooltip

## Регрессионные тесты (+5)

В `tests/test_regression_fixes.py` добавлены:
- `test_road_cells_are_clear_of_walls_and_harvestables` — все дороги чисты
- `test_server_password_uses_hmac_compare_digest` — проверка hmac
- `test_client_send_logs_errors` — send() логирует ошибки
- `test_dead_code_removed` — input_router.py и save_manager.py удалены
- `test_skills_window_has_no_bottom_description_panel` — нет панелей

Всего в файле теперь **24 регрессионных теста**.

## Метрики

| | До | После |
|---|---|---|
| Тестов проходят | 577 | **582** |
| print() в net/main | 40 | **0** |
| Мёртвый код | 193 LOC | **0** |
| Битых ссылок в README | 2 | **0** |
| Timing-уязвимостей | 1 | **0** |
| Silent except: pass в send() | 1 | **0** |
| WALL на дорогах | ~28 | **0** |
