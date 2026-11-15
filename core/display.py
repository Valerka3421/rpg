"""Безопасная смена режима окна.

На Windows ``pygame.display.set_mode(..., FULLSCREEN)`` иногда падает с
``ChangeDisplaySettingsEx() failed: DISP_CHANGE_FAILED`` — например, когда
драйвер не даёт повторно переключить тот же режим или монитор не
поддерживает выбранное разрешение. Игра не должна из-за этого падать:
пробуем полноэкранный режим, а при ошибке откатываемся в окно и
запоминаем это в настройках.
"""
from __future__ import annotations

import pygame


def _current_surface():
    try:
        if not pygame.display.get_init():
            return None
        return pygame.display.get_surface()
    except pygame.error:
        return None


def _is_fullscreen(surface):
    try:
        return bool(surface.get_flags() & pygame.FULLSCREEN)
    except pygame.error:
        return False


def set_mode(size, fullscreen, settings=None):
    """Ставит окно нужного размера, не падая на неподдерживаемом режиме.

    Если текущая поверхность уже имеет нужный размер и режим, она
    возвращается как есть: повторный ``set_mode`` без необходимости как
    раз и вызывает ошибку драйвера при возврате из мультиплеера.
    """
    size = (int(size[0]), int(size[1]))
    desired_fullscreen = bool(fullscreen)

    current = _current_surface()
    if current is not None:
        try:
            same_size = tuple(current.get_size()) == size
        except pygame.error:
            same_size = False
        if same_size and _is_fullscreen(current) == desired_fullscreen:
            return current

    flags = pygame.FULLSCREEN if desired_fullscreen else pygame.RESIZABLE
    try:
        return pygame.display.set_mode(size, flags)
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
            return pygame.display.set_mode(size, pygame.RESIZABLE)
        except pygame.error:
            # Последний шанс: обычное окно без флагов.
            return pygame.display.set_mode(size)
