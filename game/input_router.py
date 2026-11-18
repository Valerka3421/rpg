"""Input router — маршрутизация клавиш в game.py.

Извлечено из game/game.py для уменьшения размера файла.
Game.handle_key делегирует сюда dispatch_game_keys().
"""

import pygame
from core import config
from core.config import (COL_TEXT, COL_DIM, COL_GOLD, COL_DMG, COL_XP,
                          COL_HEAL, COL_DODGE, COL_CRIT, COL_MERCH)


def dispatch_game_keys(game, key, scancode=0, unicode='', mods=0):
    """Обработка клавиш в game screen_mode.

    Возвращает True если клавиша обработана, False если нет.
    """
    # F9 — load, F5 — save
    if key == pygame.K_F9:
        game.load()
        return True
    if key == pygame.K_F5 and not (game.dead or game.victory):
        game.save()
        return True
    if game.dead or game.victory:
        if key == pygame.K_r:
            game.new_game()
        return True

    # Навигация по экранам
    if key == pygame.K_c:
        game.screen_mode = 'stats'
        return True
    if key == pygame.K_i:
        game.screen_mode = 'inventory'
        game.inv_page = min(game.inv_page, game.inv_pages_count() - 1)
        return True
    if key == pygame.K_k:
        game.craft.open()
        return True
    if key == pygame.K_n:
        game.skills_ui.open()
        return True
    if key == pygame.K_l:
        game.quests.open()
        return True
    if key == pygame.K_t:
        game.dialogue.try_open()
        return True
    if key == pygame.K_h:
        game.quick_potion()
        game.update_fov()
        return True
    if key == pygame.K_m:
        game.settings['show_minimap'] = not game.settings.get('show_minimap', True)
        from core.settings import save_settings
        save_settings(game.settings)
        return True
    # B — bigmap
    if key == pygame.K_b:
        if game.screen_mode == 'game':
            game.screen_mode = 'bigmap'
            return True
        if game.screen_mode == 'bigmap':
            game.screen_mode = 'game'
            return True
    if key == pygame.K_TAB:
        game.stats_run.open()
        return True

    # Bigmap close
    if game.screen_mode == 'bigmap':
        game.screen_mode = 'game'
        return True

    # Quickbar 1-8
    if pygame.K_1 <= key <= pygame.K_8:
        game.use_hotbar(key - pygame.K_1)
        return True

    # Active skills F1-F10
    if pygame.K_F1 <= key <= pygame.K_F10:
        slot = key - pygame.K_F1
        game.cast_active_skill(slot)
        return True

    return False
