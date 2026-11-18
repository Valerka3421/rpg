# Этельгард — Aethelgard

[English](README.en.md) | [Русский](README.md)

A tile-based RPG built with Python + pygame-ce: open world, 20-floor procedural
dungeon, four bosses, class tree with crafting branches, and 4-player LAN
cooperative mode.

All graphics are procedural (PNGs in `assets/sprites/` are optional but go
through the same color grading). Sounds are synthesized on the fly. The
palette is in the spirit of **Stoneshard**: warm near-black stone, muted olive,
cold blue-green gloom, and amber torchlight.

## Quick start

```bat
python -m pip install -r requirements.txt
python main.py
```

Or with the launcher scripts:

- **Windows:** `run.bat` (finds `Aethelgard.exe`, venv, or system Python automatically)
- **Build standalone:** `build.bat` (requires PyInstaller, produces `dist\Aethelgard.exe`)

## Building from source

```bash
git clone https://github.com/Valerka3421/rpg.git
cd rpg
python -m venv .venv
source .venv/bin/activate    # Linux/macOS
# or: .venv\Scripts\activate  # Windows

pip install -r requirements-dev.txt
pip install -e .

# Run tests (headless)
SDL_VIDEODRIVER=dummy pytest

# Run the game
python main.py
```

## Project layout

```
main.py            entry point, game loop, resolution switching
core/              world rules — no pygame dependency
  config.py        constants, colors, balance
  data.py          monsters, items, classes, drops, XP curve
  rules.py         SHARED rules: combat, loot, statuses, XP, crafting
  skills.py        skill tree, class branches, class change
  entities.py      Player / Entity / Dungeon / Projectile
  world.py         world generation, FOV, BFS
  overworld.py     village layout, biomes, roads, ruins
  save.py          serialization, slots, dungeon cache
  quests.py        quest journal
game/              single-player game
  game.py          state, movement, combat, saves, input
  *_controller.py  menu, dialogue, quests, shop, craft, stats
render/            drawing
  textures.py      shared primitives: noise, outlines, shadows, light
  tiles.py         procedural tiles and their cache
  theme/sprites/…  theme, renderers, procedural icons
net/               cooperative multiplayer
  core.py          authoritative server state (uses core.rules)
  server.py        TCP server, 20 Hz snapshots
  client.py        pygame client
  view.py          snapshot adapter for Renderer
audio/             procedural sounds
tests/             pytest
```

### Key architectural rule

Single-player (`game.Game`) and the multiplayer server (`net.core.GameCore`)
**do not duplicate rules**: damage, crit, dodge, statuses, kill rewards, loot
pickup, crafting, XP — all live in `core/rules.py`. UI layer (logs, sound,
floating text) stays in `game/` and `net/`.

## Controls

| Keys | Action |
|---|---|
| `W A S D` / arrows | step |
| `Space` | attack nearest enemy |
| `E` | harvest resource / open chest |
| `I` | inventory grid |
| `C` | character stats |
| `K` | crafting |
| `N` | skill tree |
| `L` | quests |
| `T` | NPC dialogue / shop |
| `H` | quick potion |
| `M` | big map |
| `Tab` | run statistics |
| `F5` / `F9` | save / load |
| `F12` / `` ` `` | developer console (password-protected) |
| `Esc` | menu / close |

See [README.md](README.md) (Russian) for the full controls list and details.

## Development

- **Tests:** `pytest` — 522 tests, run with `SDL_VIDEODRIVER=dummy pytest`.
- **Linting:** `ruff check . && black --check . && mypy core`.
- **Coverage:** `pytest --cov` — targets: core 80%, game 60%, render 50%.
- **Pre-commit hooks:** `pre-commit install`.
- **CI:** GitHub Actions runs lint + tests on every PR (Win/Linux/macOS × Py 3.10/3.11/3.12).

See [CONTRIBUTING.md](CONTRIBUTING.md) for rules and conventions.

## Save format

JSON, `version: 7`. Tiles are compact digit strings. Mid-dungeon save is
~100 KB instead of ~580 KB. Saves version 5/6 are auto-migrated; too-new
saves are rejected.

## Cooperative multiplayer

Host: menu → «Мультиплеер» → «Создать игру» (port 9000). Client: «Присоединиться»
to host's IP. Server is authoritative — client only sends input and renders
snapshots. Player is bound to a token file in the home directory, so progress
is restored on reconnect. Auto-saves on last player disconnect.

Auto-scaling: monster HP/ATK scale with player count (+20% HP, +10% ATK per
extra player).

## License

MIT — see [LICENSE](LICENSE).

## Credits

- **Game design & code:** Valerka3421
- **Art direction:** Лизабет еБатори
- **Contributors:** see [CHANGELOG.md](CHANGELOG.md)

## Links

- [Repository](https://github.com/Valerka3421/rpg)
- [Issues](https://github.com/Valerka3421/rpg/issues)
- [Discussions](https://github.com/Valerka3421/rpg/discussions)
