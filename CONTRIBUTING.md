# Contributing to Этельгард

Спасибо за интерес к проекту! Этот документ описывает, как вносить изменения.

## Быстрый старт

```bash
git clone https://github.com/Valerka3421/rpg.git
cd rpg
python -m venv .venv
# Windows
.venv\Scripts\activate
# Linux/macOS
source .venv/bin/activate

pip install -r requirements-dev.txt
pip install -e .

# Запуск тестов
SDL_VIDEODRIVER=dummy pytest

# Запуск игры
python main.py
```

## Архитектура — главное правило

Проект **строго слоистый**:

```
core/      — чистые правила мира. НЕ зависит от pygame.
             Импортируется и одиночной игрой, и сервером мультиплеера.
game/      — single-player: состояние + контроллеры UI.
render/    — pygame-отрисовка. Только UI, не игровая логика.
net/       — кооператив: TCP-сервер + клиент. Использует core.rules.
audio/     — процедурный синтез звуков через pygame.mixer.
```

**Критический инвариант:** одиночная игра (`game.Game`) и сервер мультиплеера
(`net.core.GameCore`) **не дублируют правила**. Урон, крит, уворот, статусы,
награды за убийство, подбор лута, крафт, опыт — всё в `core/rules.py`. UI-слой
(логи, звук, всплывающие цифры) остаётся в `game/` и `net/`.

Если добавляешь новое правило боя/лута/опыта — кладёшь его в `core/rules.py`,
вызываешь из обоих мест. **Не пиши отдельную версию для мультиплеера.**

## Перед PR

1. **Тесты обязательны.** Каждый новый функционал — с тестом в `tests/`.
   Багфикс — с регрессионным тестом в `tests/test_fixes.py`.
   Запуск: `pytest --cov`. Цель покрытия — 80% на `core/`.

2. **Docstrings объясняют «почему», а не «что».** Хороший пример:
   ```python
   def _writable_data_dir():
       """Каталог для settings.json и сейвов.

       В onefile-exe `__file__` смотрит во временную папку PyInstaller,
       которая удаляется при выходе, поэтому настройки «не сохранялись».
       Сначала пробуем папку рядом с exe, а если она только для чтения —
       %APPDATA%/Ethelgard.
       """
   ```
   Плохой пример: `"""Возвращает путь."""` — это и так видно из имени.

3. **Линтинг.** Перед коммитом:
   ```bash
   ruff check .
   black .
   isort .
   mypy core
   ```
   или просто `pre-commit run --all-files`.

4. **Коммиты — Conventional Commits:**
   ```
   feat(core): add scrypt password hashing for dev console
   fix(net): block class select bypass for MP host
   docs(readme): add English version
   test(rules): parametrized dodge cap test
   refactor(game): extract SaveManager from Game class
   chore(deps): bump pytest to 7.4
   ```

## Правила кода

- **`core/` без pygame.** Если в `core/` появился `import pygame` — это баг.
- **Никаких магических чисел в логике.** Константы — в `core/config.py`:
  `MAX_STACK = 99`, `DMG_SPREAD = 1`, `CRIT_MULT = 1.5`, и т.д.
- **Сохранения — версионные.** При изменении формата сейва — bump
  `SAVE_VERSION` в `core/config.py` и добавь миграцию в `core/save.py`.
  Слишком новый сейв должен отклоняться, а не ломать игру.
- **`py.typed` маркер** — пакет объявлен как типизированный. Новые функции в
  `core/` — с аннотациями типов. Начни с сигнатуры, потом mypy подскажет.

## Структура PR

- Один PR — одна фича или один багфикс. Микс — сложно ревьюить.
- Заголовок по Conventional Commits.
- В описании: что делает, зачем, как тестировать.
- Скриншоты для UI-изменений.

## Code review

- Ревьюер проверяет: тесты есть, docstrings объясняют «почему», слои не
  нарушены (core без pygame), нет дублирования правил single/multiplayer.
- Не мёрджим PR с красным CI.

## Стиль коммуникации

- Уважительно. Не опускаемся до «это очевидно» — объясняем.
- Критика кода, не автора.
- Если не уверен — спрашивай в Discussions или Issues.

## Лицензия

Вклады принимаются под MIT (см. `LICENSE`). Отправляя PR, ты соглашаешься
с тем, что твой код будет распространяться под этой же лицензией.
