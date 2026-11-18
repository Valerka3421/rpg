"""Консоль разработчика: пароль, команды, блокировка и отрисовка."""
import hashlib
import time

import pygame
import pytest

from core import config, rules
from core.entities import Entity

PASSWORD = 'test-secret'


@pytest.fixture(autouse=True)
def _console_password(monkeypatch):
    monkeypatch.delenv('ETHELGARD_DEV_PASSWORD', raising=False)
    monkeypatch.delenv('ETHELGARD_DEV_PASSWORD_HASH', raising=False)
    monkeypatch.setattr(config, 'DEV_CONSOLE_PASSWORD_HASH',
                        hashlib.sha256(PASSWORD.encode()).hexdigest())


def type_text(game, text):
    for ch in text:
        game.handle_key(ord(ch), 0, ch, 0)


def unlock(game, password=PASSWORD):
    game.screen_mode = 'game'
    game.dev_console.open()
    type_text(game, password)
    game.handle_key(pygame.K_RETURN)
    assert game.dev_console.unlocked, game.dev_console.error


def test_f12_toggles_console_and_restores_mode(make_game):
    g = make_game()
    g.screen_mode = 'inventory'

    g.handle_key(pygame.K_F12)
    assert g.screen_mode == 'dev_console'
    assert not g.dev_console.unlocked

    g.handle_key(pygame.K_F12)
    assert g.screen_mode == 'inventory'


def test_backquote_opens_console(make_game):
    g = make_game()
    g.screen_mode = 'game'
    g.handle_key(pygame.K_BACKQUOTE)
    assert g.screen_mode == 'dev_console'


def test_password_required_and_accepted(make_game):
    g = make_game()
    g.screen_mode = 'game'
    g.dev_console.open()

    type_text(g, 'wrong')
    g.handle_key(pygame.K_RETURN)
    assert not g.dev_console.unlocked
    assert g.dev_console.error

    g.dev_console.buffer = ''
    type_text(g, PASSWORD)
    g.handle_key(pygame.K_RETURN)
    assert g.dev_console.unlocked
    assert g.dev_console.error == ''


def test_three_failures_lock_console(make_game):
    g = make_game()
    g.screen_mode = 'game'
    g.dev_console.open()
    for _ in range(config.DEV_CONSOLE_MAX_ATTEMPTS):
        g.dev_console.buffer = ''
        type_text(g, 'nope')
        g.handle_key(pygame.K_RETURN)
    assert not g.dev_console.unlocked
    assert g.dev_console.locked_for() > 0
    assert g.screen_mode == 'game'      # закрылась при блокировке


def test_give_gold_and_level_commands(make_game):
    g = make_game()
    unlock(g)
    console = g.dev_console

    console.execute('give bone_sword 2')
    assert g.player.inventory.get('bone_sword') == 2

    console.execute('give no_such_item')
    assert 'no_such_item' not in g.player.inventory

    console.execute('gold 250')
    assert g.player.gold == 250
    console.execute('gold -50')
    assert g.player.gold == 200

    console.execute('xp 50')
    assert g.player.xp > 0 or g.player.level > 1

    console.execute('level 10')
    assert g.player.level == 10

    console.execute('stat str 20')
    assert g.player.str == 20
    console.execute('skill blacksmith 5')
    assert g.player.skills['blacksmith']['level'] == 5


def test_heal_and_god_mode(make_game, monkeypatch):
    g = make_game()
    unlock(g)
    p = g.player
    p.hp = 1
    p.add_status('poison', 5)
    g.dev_console.execute('heal')
    assert p.hp == p.max_hp
    assert not p.statuses

    # Без бессмертия удар снимает HP.
    monkeypatch.setattr(rules.random, 'random', lambda: 0.99)
    attacker = Entity(p.x + 1, p.y, 10, 10, 100, 0, 'Тест', 'X',
                      (255, 255, 255), 0)
    before = p.hp
    rules.resolve_attack(attacker, p)
    assert p.hp < before

    g.dev_console.execute('god')
    assert p.god_mode
    before = p.hp
    rules.resolve_attack(attacker, p)
    assert p.hp == before


def test_teleport_spawn_boss_and_killall(make_game):
    g = make_game()
    unlock(g)
    p = g.player
    console = g.dev_console

    spot = next(((x, y) for y in range(1, g.dungeon.h - 1)
                 for x in range(1, g.dungeon.w - 1)
                 if (x, y) != (p.x, p.y)
                 and rules.tile_walkable(g.dungeon, x, y)), None)
    assert spot is not None
    console.execute(f'tp {spot[0]} {spot[1]}')
    assert (p.x, p.y) == spot

    wolves_before = sum(1 for m in g.dungeon.monsters if m.name == 'Волк')
    console.execute('spawn wolf 3')
    wolves_after = sum(1 for m in g.dungeon.monsters if m.name == 'Волк')
    assert wolves_after - wolves_before == 3

    console.execute('boss 5')
    assert any(m.boss and m.name == 'Король гоблинов'
               for m in g.dungeon.monsters)

    console.execute('killall')
    assert g.dungeon.monsters == []


def test_goto_and_depth(make_game):
    g = make_game()
    unlock(g)

    g.dev_console.execute('depth 3')
    assert g.world_mode == 'dungeon'
    assert g.depth == 3

    g.dev_console.execute('goto village')
    assert g.world_mode == 'overworld'
    assert g.location == 'village'

    g.dev_console.execute('goto dungeon')
    assert g.world_mode == 'dungeon'
    assert g.depth == 1


def test_lock_and_unknown_command(make_game):
    g = make_game()
    unlock(g)
    g.dev_console.execute('definitely-not-a-command')
    assert any('Неизвестная команда' in text for text, _ in g.dev_console.lines)

    g.dev_console.execute('lock')
    assert not g.dev_console.unlocked
    assert g.dev_console.error


def test_console_draws(renderer, make_game):
    g = make_game()
    g.renderer = renderer

    g.screen_mode = 'game'
    g.dev_console.open()
    renderer.draw(g)                    # экран пароля

    g.dev_console.buffer = 'abc'
    renderer.draw(g)
    g.dev_console.error = 'Неверный пароль.'
    renderer.draw(g)

    assert g.dev_console.locked_for() == 0
    g.dev_console.unlocked = True
    g.dev_console.lines = [('Доступ разрешён. ' * 20, config.COL_HEAL)]
    renderer.draw(g)                    # длинная строка обёрнута

    g.dev_console.execute('help')
    renderer.draw(g)
