"""Экран торговли между игроками (MP).
"""
from render.screens_renderer._common import *  # noqa: F401,F403


class TradeMixin:
    """Экран торговли между игроками (MP)."""

    def draw_trade(self, game):
        """Окно обмена: предложения сторон, золото, подтверждение."""
        self._modal_bg(game)
        cx0, cy0, cw, ch = game._modal_content_rect
        self.text("ОБМЕН", cx0, cy0, (214, 178, 98), font=T.FONT_H1)
        hits = []
        game._ui_hits['trade'] = hits
        trade = getattr(game, 'trade', None)
        if not trade:
            self.text("Обмен не активен", cx0 + cw // 2, cy0 + 120,
                      T.TEXT_MUTED, font=T.FONT_H2, center=True)
            return

        my = str(game.my_id)
        players = trade.get('players', [])
        partner_id = None
        if my in players:
            partner_id = players[1] if players[0] == my else players[0]
        partner = game.remote_players.get(int(partner_id)) \
            if partner_id is not None else None

        # Фаза запроса: второй игрок ещё не принял
        if my not in trade.get('accepted', []):
            name = partner.class_name if partner else 'Игрок'
            self.text(f"{name} предлагает обмен", cx0 + cw // 2, cy0 + 110,
                      (238, 222, 188), font=T.FONT_H2, center=True)
            bw, bh = 220, 46
            for i, (action, label, color) in enumerate(
                    (('accept', 'Принять', (134, 190, 118)),
                     ('decline', 'Отказаться', (230, 120, 90)))):
                rect = (cx0 + cw // 2 - bw + i * (bw + 20), cy0 + 170, bw, bh)
                self._trade_button(label, rect, color)
                hits.append((action, rect))
            return

        my_offer = trade.get('offers', {}).get(my, {})
        other_offer = trade.get('offers', {}).get(partner_id, {})
        my_gold = trade.get('gold', {}).get(my, 0)
        other_gold = trade.get('gold', {}).get(partner_id, 0)
        ready = trade.get('ready', [])

        col_w = (cw - 60) // 2
        col_h = 320
        cols = (
            (cx0, 'Вы выкладываете', my_offer, my_gold, my in ready),
            (cx0 + col_w + 60, partner.class_name if partner else 'Соперник',
             other_offer, other_gold, partner_id in ready),
        )
        for col_x, title, offer, gold, is_ready in cols:
            T.rounded_rect(self.screen, (26, 22, 18), (col_x, cy0 + 56,
                                                       col_w, col_h),
                           radius=8, border_color=(90, 74, 48))
            self.text(title, col_x + 12, cy0 + 64, (238, 222, 188),
                      font=T.FONT_H2)
            self.text(f"Золото: {gold}", col_x + 12, cy0 + 96,
                      (240, 210, 90), font=T.FONT_BODY)
            y = cy0 + 128
            if is_ready:
                self.text("✔ Подтверждено", col_x + col_w - 12, cy0 + 96,
                          (134, 190, 118), font=T.FONT_BODY, align_right=True)
            if not offer:
                self.text("(пусто)", col_x + 12, y, T.TEXT_MUTED,
                          font=T.FONT_SMALL)
                y += 24
            for key, count in sorted(offer.items()):
                self.text(f"{item_display_name(key)} ×{count}",
                          col_x + 12, y, T.TEXT, font=T.FONT_SMALL)
                if col_x == cx0:      # свои предметы можно убрать
                    rect = (col_x + col_w - 32, y - 2, 26, 22)
                    self._trade_button('−', rect, (230, 120, 90))
                    hits.append((('remove', key), rect))
                y += 26
            if col_x == cx0 and y < cy0 + 56 + col_h - 30:
                # Кнопки золота
                by = cy0 + 56 + col_h - 96
                for i, (action, label) in enumerate(
                        (('gold-10', '-10'), ('gold+10', '+10'),
                         ('gold+100', '+100'), ('gold_reset', 'Сброс'))):
                    rect = (col_x + 12 + i * 78, by, 70, 26)
                    self._trade_button(label, rect, (206, 166, 82))
                    hits.append((action, rect))

        # Свои предметы: добавить по клику (до 12 позиций)
        self.text("Инвентарь (клик — добавить):", cx0, cy0 + 56 + col_h + 16,
                  (190, 172, 138), font=T.FONT_SMALL)
        inv = [(k, c) for k, c in sorted(game.player.inventory.items()) if c > 0]
        per_col = 6
        for i, (key, count) in enumerate(inv[:per_col * 2]):
            col = i // per_col
            row = i % per_col
            ix = cx0 + col * (col_w + 60)
            iy = cy0 + 56 + col_h + 40 + row * 26
            label = self.fit_text(f"{item_display_name(key)} ×{count}",
                                  col_w - 40, T.FONT_SMALL)
            self.text(label, ix, iy, T.TEXT, font=T.FONT_SMALL)
            rect = (ix + col_w - 34, iy - 3, 28, 22)
            self._trade_button('+', rect, (134, 190, 118))
            hits.append((('add', key), rect))

        # Кнопки внизу
        bw, bh = 220, 46
        bottom = cy0 + ch - bh - 8
        is_ready = my in ready
        ready_label = 'Готов ✔' if is_ready else 'Подтвердить'
        rect = (cx0, bottom, bw, bh)
        self._trade_button(ready_label, rect, (134, 190, 118))
        hits.append(('ready', rect))
        rect = (cx0 + cw - bw, bottom, bw, bh)
        self._trade_button('Отменить', rect, (230, 120, 90))
        hits.append(('cancel', rect))


    def _trade_button(self, label, rect, color):
        mx, my = pygame.mouse.get_pos()
        x, y, w, h = rect
        hover = x <= mx <= x + w and y <= my <= y + h
        bg = (66, 54, 38) if hover else (40, 34, 26)
        T.rounded_rect(self.screen, bg, rect, radius=6, border_color=color)
        self.text(label, x + w // 2, y + h // 2 - 2, color,
                  font=T.FONT_BODY, center=True)

