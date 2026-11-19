"""Консоль разработчика.
"""
from render.screens_renderer._common import *  # noqa: F401,F403


class DevConsoleMixin:
    """Консоль разработчика."""

    def draw_dev_console(self, game):
        self._modal_bg(game)
        cx0, cy0, cw, ch = game._modal_content_rect
        ctrl = game.dev_console
        accent = (224, 120, 88)
        self.text("КОНСОЛЬ РАЗРАБОТЧИКА", cx0, cy0, accent, font=T.FONT_H1)
        self.text("F12 / ` — закрыть", self._modal_right(game), cy0 + 8,
                  T.TEXT_MUTED, font=T.FONT_SMALL, align_right=True)
        y = cy0 + 56

        locked = ctrl.locked_for()
        if locked > 0:
            self.text("Доступ временно заблокирован.", cx0, y, T.HP_CRIT,
                      font=T.FONT_BODY)
            self.text(f"Осталось: {int(locked) + 1} с.", cx0, y + 28,
                      T.TEXT_DIM, font=T.FONT_SMALL)
            self.text("Esc — закрыть.", cx0, y + 58, T.TEXT_MUTED,
                      font=T.FONT_SMALL)
            return

        if not ctrl.unlocked:
            self.text("Введите пароль разработчика.", cx0, y, T.TEXT,
                      font=T.FONT_BODY)
            box = (cx0, y + 30, min(cw, 560), 38)
            T.rounded_rect(self.screen, (18, 16, 13), box, radius=6,
                           border_color=(110, 86, 58))
            masked = '*' * len(ctrl.buffer)
            shown = self.fit_text(masked, box[2] - 56, T.FONT_BODY)
            self.text(shown, box[0] + 12, box[1] + 9,
                      T.TEXT, font=T.FONT_BODY)
            caret_x = box[0] + 12 + T.FONT_BODY.size(shown)[0]
            if caret_x < box[0] + box[2] - 20:
                self.text("_", caret_x + 2, box[1] + 8, T.GOLD,
                          font=T.FONT_BODY)
            if ctrl.error:
                self.text(ctrl.error, cx0, box[1] + box[3] + 10, T.HP_CRIT,
                          font=T.FONT_SMALL)
            self.text("Enter — проверить · Esc — закрыть",
                      cx0 + cw // 2, cy0 + ch - 20, T.TEXT_MUTED,
                      font=T.FONT_SMALL, center=True)
            return

        # ── Вывод консоли ──
        pad = 10
        out_rect = (cx0, y - 6, cw, (cy0 + ch) - (y - 6) - 46)
        T.rounded_rect(self.screen, (14, 13, 11), out_rect, radius=6,
                       border_color=(76, 64, 46))
        text_w = out_rect[2] - pad * 2
        wrapped = []
        for text, color in ctrl.lines:
            for part in self._wrap_console(text, T.FONT_SMALL, text_w):
                wrapped.append((part, color))
        max_lines = max(1, (out_rect[3] - pad * 2) // 18)
        visible = wrapped[-max_lines:]
        ly = out_rect[1] + pad
        for text, color in visible:
            self.text(self.fit_text(text, text_w, T.FONT_SMALL),
                      out_rect[0] + pad, ly, color, font=T.FONT_SMALL)
            ly += 18

        # ── Строка ввода ──
        input_rect = (cx0, cy0 + ch - 34, cw, 30)
        T.rounded_rect(self.screen, (22, 20, 16), input_rect, radius=6,
                       border_color=(110, 86, 58))
        prompt = ctrl.prompt
        self.text(prompt, input_rect[0] + 8, input_rect[1] + 6,
                  T.GOLD, font=T.FONT_BODY)
        prompt_w = T.FONT_BODY.size(prompt)[0]
        shown = self.fit_text(ctrl.buffer, cw - prompt_w - 40, T.FONT_BODY)
        self.text(shown, input_rect[0] + 8 + prompt_w, input_rect[1] + 6,
                  T.TEXT, font=T.FONT_BODY)
        caret_x = input_rect[0] + 10 + prompt_w + T.FONT_BODY.size(shown)[0]
        if caret_x < input_rect[0] + cw - 12:
            self.text("_", caret_x, input_rect[1] + 6, T.GOLD,
                      font=T.FONT_BODY)
        self.text("Enter — выполнить · ↑/↓ — история · Tab — дописать",
                  cx0 + cw // 2, cy0 + ch - 46, T.TEXT_MUTED,
                  font=T.FONT_TINY, center=True)

    # ════════════════════════ ДИАЛОГ ════════════════════════

