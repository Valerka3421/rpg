"""Безопасная смена режима окна.

На Windows ``pygame.display.set_mode(..., FULLSCREEN)`` иногда падает с
``ChangeDisplaySettingsEx() failed: DISP_CHANGE_FAILED`` — например, когда
драйвер не даёт повторно переключить тот же режим или монитор не
поддерживает выбранное разрешение. Игра не должна из-за этого падать:
пробуем полноэкранный режим, а при ошибке откатываемся в окно и
запоминаем это в настройках.
"""
from __future__ import annotations

import os

import pygame

# Центрировать окно при каждом создании/смене размера (SDL читает hint
# в момент создания окна).
os.environ.setdefault('SDL_VIDEO_CENTERED', '1')


def _current_surface():
    try:
        if not pygame.display.get_init():
            return None
        return pygame.display.get_surface()
    except pygame.error:
        return None


def _is_fullscreen(surface):
    """Полноэкранный = видеорежим FULLSCREEN или borderless-окно."""
    try:
        flags = surface.get_flags()
        if flags & pygame.FULLSCREEN:
            return True
        return bool(flags & pygame.NOFRAME) and not flags & pygame.RESIZABLE
    except pygame.error:
        return False


def _desktop_size():
    """Размер рабочего стола (для borderless-полноэкранного режима)."""
    try:
        info = pygame.display.Info()
        return (int(info.current_w or 0), int(info.current_h or 0))
    except pygame.error:
        return None


def _remember_actual_size(screen):
    """config читают рендереры в момент отрисовки — держим его честным."""
    try:
        from core import config
        config.set_resolution(screen.get_width(), screen.get_height())
    except Exception:
        pass
    return screen


def set_mode(size, fullscreen, settings=None):
    """Ставит окно нужного размера, не падая на неподдерживаемом режиме.

    Если текущая поверхность уже имеет нужный размер и режим, она
    возвращается как есть: повторный ``set_mode`` без необходимости как
    раз и вызывает ошибку драйвера при возврате из мультиплеера.

    Полный экран делаем borderless-окном (NOFRAME поверх рабочего стола):
    переключение видеорежима на Windows часто даёт DISP_CHANGE_FAILED.
    """
    size = (int(size[0]), int(size[1]))
    desired_fullscreen = bool(fullscreen)

    current = _current_surface()
    if current is not None:
        try:
            full = _is_fullscreen(current)
            if desired_fullscreen and full:
                return current          # уже на весь экран — не трогаем
            if (not desired_fullscreen and not full
                    and tuple(current.get_size()) == size):
                return current          # уже окно нужного размера
        except pygame.error:
            pass

    if desired_fullscreen:
        desktop = _desktop_size()
        if desktop and desktop[0] > 0 and desktop[1] > 0:
            try:
                return _remember_actual_size(
                    pygame.display.set_mode(desktop, pygame.NOFRAME))
            except pygame.error:
                pass
        try:
            return _remember_actual_size(
                pygame.display.set_mode(size, pygame.FULLSCREEN))
        except pygame.error as exc:
            print(f"[DISPLAY] {exc}; переключаюсь в оконный режим")
            if settings is not None:
                settings['fullscreen'] = False
                try:
                    from core.settings import save_settings
                    save_settings(settings)
                except Exception:
                    pass
        try:
            return _remember_actual_size(
                pygame.display.set_mode(size, pygame.RESIZABLE))
        except pygame.error:
            # Последний шанс: обычное окно без флагов.
            return _remember_actual_size(pygame.display.set_mode(size))
    return _remember_actual_size(
        pygame.display.set_mode(size, pygame.RESIZABLE))
