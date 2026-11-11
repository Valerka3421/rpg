"""Точка входа. Запуск: python main.py"""
import sys
import time
import threading

# 1) Настройки — ДО импорта game/render
from core.settings import load_settings
_settings = load_settings()

from core import config
_res = _settings.get('resolution', [1280, 720])
config.set_resolution(_res[0], _res[1])

import pygame


# ════════════════════════ СМЕНА РАЗРЕШЕНИЯ ════════════════════════
def _apply_resolution(game, new_w, new_h, fullscreen):
    """Меняет размер окна и обновляет единый источник истины — core.config.

    Рендереры читают config.SCREEN_W/H и config.VIEW_W/H в момент отрисовки,
    поэтому никакой синхронизации копий констант не нужно.
    """
    flags = pygame.FULLSCREEN if fullscreen else 0
    screen = pygame.display.set_mode((new_w, new_h), flags)
    pygame.display.set_caption(f"Этельгард — {new_w}×{new_h}")

    config.set_resolution(new_w, new_h)

    game.renderer.screen = screen
    for sub in ('menu', 'game', 'screens', 'overlay', 'icons'):
        r = getattr(game.renderer, sub, None)
        if r is not None and hasattr(r, 'screen'):
            r.screen = screen

    game._modal_content_rect = (0, 0, 0, 0)
    game._modal_close_rect = None
    return screen


# ════════════════════════ ГЛАВНЫЙ ЦИКЛ SINGLE-PLAYER ════════════════════════
def _run_game_loop(game, screen, clock):
    """Возвращает None при выходе из игры, или dict для перехода в MP."""
    pygame.init()
    if not pygame.font.get_init():
        pygame.font.init()

    fps_font = pygame.font.SysFont("consolas", 14)

    current_fullscreen = game.settings.get('fullscreen', False)
    if current_fullscreen:
        screen = pygame.display.set_mode(
            (config.SCREEN_W, config.SCREEN_H), pygame.FULLSCREEN)
        game.renderer.screen = screen

    while True:
        dt = clock.tick(60) / 1000.0

        # ── Переход в мультиплеер? ──
        if getattr(game, 'pending_multiplayer', None):
            result = game.pending_multiplayer
            game.pending_multiplayer = None
            # Автосейв, если игрок был в партии
            if game.screen_mode not in (
                'menu', 'menu_saves', 'menu_settings', 'menu_controls',
                'menu_multiplayer', 'menu_mp_join', 'class_select',
            ):
                try:
                    game.save(silent=True)
                except Exception:
                    pass
            return result

        # ── Смена разрешения ──
        if getattr(game, 'pending_resolution', None):
            new_w, new_h = game.pending_resolution
            game.pending_resolution = None
            screen = _apply_resolution(game, new_w, new_h, current_fullscreen)

        # ── Fullscreen toggle ──
        desired = game.settings.get('fullscreen', False)
        if desired != current_fullscreen:
            current_fullscreen = desired
            screen = _apply_resolution(game, config.SCREEN_W,
                                       config.SCREEN_H, desired)

        # ── События ──
        for event in pygame.event.get():
            if event.type == pygame.QUIT:
                return None
            if event.type == pygame.MOUSEWHEEL:
                game.scroll_log(event.y * 2)
            if event.type == pygame.MOUSEMOTION:
                game.handle_mouse_motion(event.pos)
            if event.type == pygame.MOUSEBUTTONDOWN and event.button == 1:
                game.handle_mouse_click(event.pos)
            if event.type == pygame.KEYDOWN:
                if (event.key == pygame.K_ESCAPE
                        and game.screen_mode == 'game'
                        and not game.dead and not game.victory):
                    game.open_menu(from_game=True)
                    continue
                game.handle_key(event.key, event.scancode,
                                event.unicode, event.mod)

        keys = pygame.key.get_pressed()
        game.handle_input_continuous(keys)
        game.update(dt)
        game.draw()

        if game.settings.get('show_fps'):
            fps = clock.get_fps()
            surf = fps_font.render(f"FPS {fps:.0f}", True, (150, 150, 150))
            screen.blit(surf, (config.SCREEN_W - surf.get_width() - 8, 4))

        pygame.display.flip()


# ════════════════════════ МУЛЬТИПЛЕЕР ════════════════════════
def _run_multiplayer(as_host, host_addr='localhost', port=9000,
                     class_key='warrior', skip_class_select=False):
    server = None
    if as_host:
        from net.server import Server
        server = Server(host='', port=port, max_players=4)
        threading.Thread(target=server.start, daemon=True).start()
        time.sleep(0.5)          # дать серверу bind-нуться
        host_addr = 'localhost'
        print(f"[MP] Host started on port {port}, connecting as localhost")

    try:
        from net.client import Client
        client = Client(host=host_addr, port=port, class_key=class_key,
                        is_host=as_host,
                        skip_class_select=skip_class_select)
        client.start()
    except Exception as e:
        print(f"[MP] error: {e}")
        time.sleep(2)
    finally:
        if server:
            try:
                server.stop()
            except Exception:
                pass


# ════════════════════════ MAIN ════════════════════════
def main():
    pygame.init()
    if not pygame.font.get_init():
        pygame.font.init()

    while True:
        # Клиент мультиплеера мог завершить pygame (например, старые
        # сборки звали pygame.quit()) — перед новым меню поднимаем его.
        if not pygame.get_init():
            pygame.init()
        if not pygame.font.get_init():
            pygame.font.init()
        settings_now = load_settings()
        flags = pygame.FULLSCREEN if settings_now.get('fullscreen') else 0
        screen = pygame.display.set_mode(
            (config.SCREEN_W, config.SCREEN_H), flags)
        pygame.display.set_caption(f"Этельгард — {config.SCREEN_W}×{config.SCREEN_H}")
        clock = pygame.time.Clock()

        # Пересоздать Game при каждой итерации (возврат из MP → новое меню)
        from game import Game
        game = Game(screen)

        result = _run_game_loop(game, screen, clock)

        if result is None:
            break

        mode = result.get('mode')
        if mode == 'mp_host':
            if result.get('fresh_start'):
                import os as _os
                _base = config.DATA_DIR
                for fname in ('savegame_mp_world.json',
                              'savegame_mp_players.json'):
                    try:
                        _os.remove(_os.path.join(_base, fname))
                    except FileNotFoundError:
                        pass
                    except Exception as e:
                        print(f"[MP] fresh_start remove {fname}: {e}")
                print("[MP] fresh start — старый сейв удалён")
            _run_multiplayer(as_host=True,
                             port=result.get('port', 9000),
                             skip_class_select=result.get('skip_class', False))
        elif mode == 'mp_join':
            _run_multiplayer(as_host=False,
                             host_addr=result.get('host', 'localhost'),
                             port=result.get('port', 9000),
                             skip_class_select=False)
        # цикл повторяется → новое меню

    pygame.quit()
    sys.exit(0)


if __name__ == "__main__":
    main()