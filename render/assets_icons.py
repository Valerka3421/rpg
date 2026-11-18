"""Заглушка модуля assets_icons для рендера древа навыков.

В upstream этот модуль отсутствует, но screens_renderer.py пытается его
импортировать. Здесь — минимальный fallback, который использует
``render.skill_icon_loader.load_skill_icon`` для загрузки PNG из
assets/skills/, либо процедурную заглушку.

Интерфейс (восстановлен по вызовам в screens_renderer.py):
    node_icon_candidates(node_key, effect_key, tab) -> list[Surface]
    class_icon_candidates(class_key, effect_key) -> list[Surface]
    IconAssets.draw(screen, candidates, cx, cy, size, alpha=255) -> bool
"""
import pygame

from render.skill_icon_loader import load_skill_icon


class IconAssets:
    """Менеджер иконок навыков с статическим методом draw."""

    @staticmethod
    def draw(screen, candidates, cx, cy, size, alpha=255):
        """Отрисовать первую иконку из candidates в (cx, cy) размером size.

        Возвращает True, если отрисовано (candidates непустой и валиден),
        False — если нужно использовать fallback (процедурный glyph).
        """
        if not candidates or size is None or size <= 0:
            return False
        try:
            surf = candidates[0]
            if surf is None:
                return False
            if surf.get_width() != size or surf.get_height() != size:
                surf = pygame.transform.smoothscale(surf, (size, size))
            if alpha < 255:
                # Применить alpha через set_alpha (не блокирует исходную поверхность)
                surf = surf.copy()
                surf.set_alpha(int(alpha))
            screen.blit(surf, (cx - size // 2, cy - size // 2))
            return True
        except Exception:
            return False


def node_icon_candidates(node_key, effect_key=None, tab=None, count=3):
    """Список поверхностей-кандидатов для узла навыка.

    ``node_key`` может быть None (для крафт-hub) — тогда используем tab.
    ``effect_key`` — основной эффект узла (например 'atk', 'hp'); сейчас
    не используется для выбора иконки, но может в будущем.
    """
    key = node_key
    if key is None:
        # Для craft hub: конвертируем tab в имя иконки
        if tab:
            key = f'{tab}:0'
        else:
            return []
    try:
        return [load_skill_icon(key, size=48) for _ in range(count)]
    except Exception:
        return []


def class_icon_candidates(class_key, effect_key=None, count=3):
    """Аналогично для иконок классов."""
    if not class_key:
        return []
    try:
        return [load_skill_icon(f'{class_key}:0', size=48)
                for _ in range(count)]
    except Exception:
        return []
