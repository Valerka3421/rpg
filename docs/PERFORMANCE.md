# Performance roadmap (опциональные улучшения)

Эти улучшения не обязательны для релиза, но помогут при масштабировании.
См. `CHANGELOG.md` для уже сделанных оптимизаций.

## Lazy world generation

**Проблема.** `generate_overworld()` генерирует всю поверхность 140×70 за
~200 мс при каждом `new_game()`. При рестарте игры это заметная пауза.

**Решение.** Хранить seed в сейве, генерировать этаж при первом входе,
не все 20 сразу:

```python
# core/world.py
def generate_dungeon_lazy(depth, seed, cache):
    if depth in cache:
        return cache[depth]
    random.seed(seed + depth)
    d = generate_dungeon(depth)
    cache[depth] = d
    return d
```

**Эффект:** старт игры ~50 мс вместо ~200 мс.

## numpy для тайлов

**Проблема.** При картах больше 256×256 или реалтайм-освещении чистый
Python в `render/tiles.py` становится узким местом.

**Решение.** Использовать `pygame.surfarray` + numpy для векторизованных
операций с пикселями:

```python
import numpy as np
arr = pygame.surfarray.pixels3d(surface)
# Векторизованная операция — в 10-50× быстрее Python-циклов
arr[:, :, 0] = np.clip(arr[:, :, 0] * 0.8, 0, 255).astype(np.uint8)
del arr  # освободить pixels-array
```

**Когда делать:** только если профайлинг покажет узкое место в рендере
тайлов. Сейчас 64×64 чистым Python — нормально.

## Per-player input queue в net/core.py

**Проблема.** `net/core.py:GameCore.update()` использует глобальный
`self.lock` для всех игроков. При 4 игроках — OK, при 16+ — контентион.

**Решение.** Per-player input queue:

```python
class GameCore:
    def __init__(self, ...):
        self.input_queues = {}    # {pid: deque}
        self.input_locks = {}     # {pid: threading.Lock}

    def apply_input(self, pid, msg):
        with self.input_locks.setdefault(pid, threading.Lock()):
            self.input_queues.setdefault(pid, deque()).append(msg)

    def update(self, dt):
        # Обработать все очереди — без глобального lock
        for pid, q in self.input_queues.items():
            while q:
                msg = q.popleft()
                self._apply_one(pid, msg)
        # World update — под одним lock
        with self.lock:
            self._world_update(dt)
```

**Когда делать:** если планируется публичный сервер с большим числом
игроков. Для LAN-кооператива до 4 игроков — overkill.

## Профайлинг

В dev console есть команда `fps` — переключает счётчик FPS. Можно добавить
`profile <frames>` для дампа cProfile:

```python
import cProfile, pstats
def cmd_profile(game, args):
    n = int(args[0]) if args else 60
    pr = cProfile.Profile()
    pr.enable()
    for _ in range(n):
        game.update(1/60)
        game.draw()
    pr.disable()
    ps = pstats.Stats(pr).sort_stats('cumulative')
    ps.print_stats(20)
```

## Benchmarks

`tests/test_benchmarks.py` — pytest-benchmark тесты на ключевые операции:

```bash
pytest tests/test_benchmarks.py --benchmark-only
```

Запускать в CI с `--benchmark-compare=baseline.json` для ловли регрессий.
