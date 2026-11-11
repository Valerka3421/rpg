"""Иконка окна игры (favicon) — assets/icon.png."""
import os

import pygame

from core.config import ASSETS_DIR


ICON_PATH = os.path.join(ASSETS_DIR, 'icon.png')


def apply_window_icon():
    """Поставить иконку текущему окну. Возвращает True при успехе.

    Вызывать после каждого pygame.display.set_mode(): смена режима
    создаёт новое окно, и иконку нужно назначить заново.
    """
    try:
        icon = pygame.image.load(ICON_PATH)
        pygame.display.set_icon(icon)
        return True
    except Exception:
        return False
