# CHANGELOG

Все заметные изменения проекта «Этельгард» документируются здесь.

Формат основан на [Keep a Changelog](https://keepachangelog.com/ru/1.1.0/),
версионирование — [Semantic Versioning](https://semver.org/lang/ru/).

## [Unreleased]

### Добавлено
- **Лицензия MIT** — проект теперь официально open-source (`LICENSE`).
- **`pyproject.toml`** — единая конфигурация сборки, lint, type-check, тестов.
- **`CONTRIBUTING.md`** — правила для контрибьюторов: тесты обязательны,
  docstrings «почему», разделение по слоям, Conventional Commits.
- **`CODE_OF_CONDUCT.md`** — стандарт поведения в сообществе.
- **`README.en.md`** — английская версия README.
- **`.editorconfig`, `.pre-commit-config.yaml`** — настройка редакторов и
  pre-commit хуков.
- **Issue/PR templates** в `.github/ISSUE_TEMPLATE/`.
- **`py.typed` маркер** — пакет `core/` объявлен типизированным.
- **Типизация `core/`** — аннотации на публичных функциях `rules.py`,
  `entities.py`, `data.py`, `save.py`, `world.py`.
- **Магические числа вынесены в `core/config.py`**: `DMG_SPREAD`, `CRIT_MULT`,
  `XP_LEVEL_GUARD`, `DODGE_CAP`.
- **`Player._compute_stats`** — устранено дублирование формулы статов
  между `preview_stats` и `recalc`.
- **Конечный автомат в `main.py`** — `AppState` вместо `while True`.
- **`core/data/`** — пакет с подмодулями `monsters.py`, `items.py`, `classes.py`,
  `quests.py`, `recipes.py` + `__init__.py` re-export для обратной совместимости.
- **`coverage` + `.coveragerc`** — измерение покрытия, `fail_under=70`.
- **`pytest-benchmark`** — тесты производительности рендера/сейва.
- **Тесты на malformed-сообщения** в `tests/test_net_security.py`.
- **Тест блокировки dev console в MP** в `tests/test_dev_console_mp.py`.
- **`scrypt` для пароля dev console** — заменён `hashlib.sha256` на
  `hashlib.scrypt(n=2**14, r=8, p=1)` с солью.
- **`MAX_MSG_SIZE = 65536`** в `net/server.py` — защита от DoS через буфер.
- **`os.chmod(path, 0o600)`** на файл токена игрока в `net/client.py`.
- **`server_password` опция** в `Server.__init__` — пароль на подключение.
- **HMAC на сетевых сообщениях** — подпись каждого сообщения сессионным ключом.
- **`_validate_host`** — санитизация и проверка IP/hostname при подключении.
- **Auto-scale монстров в MP** — HP/ATK масштабируются под количество игроков.
- **Пляж моря** — новый биом (0,2) вместо «Дремучего ельника».
- **Руины древнего города** — в биоме `dead`, с 25% шансом сундука с ресурсами.
- **4 кардинальных GATE-перехода** — север/юг/запад/восток на краях карты.
- **Отключаемый туман войны** — `FOG_OF_WAR_ENABLED = False` по умолчанию.
- **Замедленная регенерация** — `REGEN_TICK_INTERVAL = 5.0` (было 2.0).
- **`Projectile` dataclass + `ranged_attack`/`projectile_hit`** в `core/rules.py`.
- **`render/projectile_renderer.py`** — анимация стрел/шаров/молний.
- **`render/bigmap_renderer.py`** — полноэкранная карта на «M».
- **`render/skill_icon_loader.py`** — загрузка PNG иконок навыков из `assets/skills/`.
- **`rules.add_item`** — корректные стаки с суффиксом `#N` при превышении `MAX_STACK`.
- **Дроп сразу в инвентарь** — `_kill_monster` использует `add_item`.
- **Quickbar** — `Player.quickbar`, 10 слотов для быстрого доступа к предметам.
- **Активные навыки** — `CLASSES['mage'].active_skills` (firebolt, frost_bolt, heal).
- **Явная `category: 'alchemy'`** для `mana_s`, `mana_m`, `elixir_life`.
- **`beach` палитра** в `render/tiles.py:BIOME_PALETTES`.
- **`city_ruins_*` тайлы** зарегистрированы в `_VARIANTS` и `_BUILDERS`.

### Изменено
- `REGEN_TICK_INTERVAL`: 2.0 → 5.0 (медленнее восстановление).
- `ROAD_SEGMENTS`: 11 сегментов → 4 кардинальных + дорога к подземелью.
- `BIOME_NAMES[(0,2)]`: `'dark'` → `'beach'`.
- `compute_fov`: учитывает `FOG_OF_WAR_ENABLED`.
- `generate_dungeon`: переписана — узкие L-коридоры, BFS-связность, арена =
  самая большая комната.
- `net/server.py:start`: fix race condition `ready.set()` до успешного bind.
- `main.py:_run_multiplayer`: 10 сек таймаут вместо 5; хост всегда выбирает класс.

### Удалено
- `pytest.ini` — конфигурация перенесена в `pyproject.toml`.
- Упоминание пароля «ethelgard» в `README.md` — заменено на инструкцию
  задать свой через `ETHELGARD_DEV_PASSWORD` env var.

## [0.0.1] — 2026-11-17

### Добавлено
- Первоначальный релиз «Этельгард» от Valerka3421.
- Тайловая RPG на Python + pygame-ce с открытым миром, 20 этажами
  подземелья, 4 боссами, 4 классами, крафтом, кооперативом до 4 игроков.
- 475 тестов, процедурная графика, процедурный звук.
