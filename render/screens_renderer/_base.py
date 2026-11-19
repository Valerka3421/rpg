"""Базовые методы: __init__, modal bg, утилиты (_in_rect, _wrap_lines, _wrap_console).
"""
from render.screens_renderer._common import *  # noqa: F401,F403


class ScreensBase:
    """Базовые методы: __init__, modal bg, утилиты (_in_rect, _wrap_lines, _wrap_console)."""

    def __init__(self, screen, icons):
        super().__init__(screen)
        self.icons = icons


    def _modal_bg(self, game, rect=None):
        """Затемнение + панель + крестик + возврат rect контента."""
        from core.config import MODAL_CLOSE_PAD, MODAL_CLOSE_SIZE, MODAL_CONTENT_PAD, MODAL_TOP_PAD
        ov = pygame.Surface((config.SCREEN_W, config.SCREEN_H), pygame.SRCALPHA)
        ov.fill((6, 5, 4, 165))
        self.screen.blit(ov, (0, 0))

        rect = rect or game._modal_panel_rect()
        panel = pygame.Surface((rect[2], rect[3]), pygame.SRCALPHA)
        pygame.draw.rect(panel, (30, 26, 21, 238), panel.get_rect(), border_radius=10)
        pygame.draw.rect(panel, (78, 66, 48, 255), panel.get_rect(),
                         width=2, border_radius=10)
        self.screen.blit(panel, (rect[0], rect[1]))

        # ── Крестик ──
        size = MODAL_CLOSE_SIZE
        pad = MODAL_CLOSE_PAD
        xx = rect[0] + rect[2] - size - pad
        yy = rect[1] + pad
        T.rounded_rect(self.screen, (54, 26, 22), (xx, yy, size, size),
                       radius=6, border_color=(160, 68, 58))
        cx, cy = xx + size // 2, yy + size // 2
        r = 9
        pygame.draw.line(self.screen, (200, 74, 64),
                         (cx - r, cy - r), (cx + r, cy + r), 3)
        pygame.draw.line(self.screen, (200, 74, 64),
                         (cx + r, cy - r), (cx - r, cy + r), 3)

        game._modal_close_rect = (xx, yy, size, size)
        game._modal_panel_rect_current = rect
        game._modal_content_rect = (
            rect[0] + MODAL_CONTENT_PAD,
            rect[1] + MODAL_TOP_PAD,
            rect[2] - MODAL_CONTENT_PAD * 2 - size - pad,  # правее крестика не лезем
            rect[3] - MODAL_TOP_PAD - 20,
        )
        return rect


    def _modal_right(self, game):
        from core.config import MODAL_CLOSE_PAD, MODAL_CLOSE_SIZE, MODAL_CONTENT_PAD
        rect = (getattr(game, '_modal_panel_rect_current', None)
                or game._modal_panel_rect())
        return rect[0] + rect[2] - MODAL_CONTENT_PAD - MODAL_CLOSE_SIZE - MODAL_CLOSE_PAD - 6

    # ════════════════════════ СТАТЫ ════════════════════════

    @staticmethod
    def _in_rect(rect, pos):
        x, y, w, h = rect
        return x <= pos[0] <= x + w and y <= pos[1] <= y + h


    @staticmethod
    def _wrap_lines(text, font, max_w):
        """Разбивает текст на строки по словам под заданную ширину."""
        lines = []
        current = ''
        for word in str(text).split():
            candidate = word if not current else f"{current} {word}"
            if not current or font.size(candidate)[0] <= max_w:
                current = candidate
            else:
                lines.append(current)
                current = word
        if current:
            lines.append(current)
        return lines or ['']


    @staticmethod
    def _wrap_console(text, font, max_w):
        lines = []
        for raw in str(text).split('\n'):
            words = raw.split(' ')
            current = ''
            for word in words:
                candidate = word if not current else current + ' ' + word
                if not current or font.size(candidate)[0] <= max_w:
                    current = candidate
                else:
                    lines.append(current)
                    current = word
            lines.append(current)
        return lines or ['']


