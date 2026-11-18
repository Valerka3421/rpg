## Summary

Brief description of what this PR does and why.

## Type of change

- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality
      to not work as expected)
- [ ] Refactor (no functional change)
- [ ] Documentation update
- [ ] Test improvement
- [ ] Chore (deps, build, CI)

## Architecture check

- [ ] Changes in `core/` do not introduce `import pygame`
- [ ] No duplication of rules between `game.Game` and `net.core.GameCore`
      (new rules go into `core/rules.py`)
- [ ] No new magic numbers — constants in `core/config.py`
- [ ] If save format changed: bumped `SAVE_VERSION` + migration in `core/save.py`

## Tests

- [ ] New tests added for new functionality
- [ ] Regression test added for bug fix (in `tests/test_fixes.py`)
- [ ] All tests pass locally: `SDL_VIDEODRIVER=dummy pytest`
- [ ] Coverage target met: `pytest --cov` (≥80% for `core/`)

## Code quality

- [ ] `ruff check .` passes
- [ ] `black --check .` passes
- [ ] `mypy core` passes (or no new errors)
- [ ] Docstrings explain "why", not just "what"
- [ ] No `TODO` / `FIXME` left in the code

## Screenshots

If UI changes — attach before/after screenshots.

## Checklist

- [ ] Self-reviewed the code
- [ ] Comments added for complex logic
- [ ] CHANGELOG.md updated (under `[Unreleased]`)
- [ ] No new dependencies without justification
