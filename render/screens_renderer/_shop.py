"""Экран магазина: сетка товаров в стиле инвентаря, подсказки при наведении.
"""
from render.screens_renderer._common import *  # noqa: F401,F403


class ShopMixin:
    """Экран магазина: draw_shop, buy/sell панели."""

    # Сетка магазина в стиле инвентаря: 3 колонки на страницу.
    SHOP_COLS = 3
    SHOP_GAP = 10

    def draw_shop(self, game):
        self._modal_bg(game)
        cx0, cy0, cw, ch = game._modal_content_rect
        game._ui_hits['shop'] = {'rows': []}
        npc_type = getattr(game, 'active_shop_npc', None)
        city_shop = is_city_shop_npc(npc_type)
        header = CITY_NPC_NAMES.get(npc_type, 'ТОРГОВЕЦ') if city_shop \
            else "ТОРГОВЕЦ"
        header_surf = T.render(header.upper(), T.MERCH, T.FONT_H1)
        self.text(header.upper(), cx0, cy0, T.MERCH, font=T.FONT_H1)

        # Переключатель режима — рядом с именем торговца, по центру кнопки.
        buying = game.shop_submode == 'buy'
        mode = "ПОКУПКА" if buying else "ПРОДАЖА"
        bw, bh = 150, 30
        mode_rect = (cx0 + header_surf.get_width() + 16, cy0 + 2, bw, bh)
        mx, my = getattr(game, 'mouse_pos', (-1, -1))
        hover = self._in_rect(mode_rect, (mx, my))
        mode_bg = (T.MERCH if buying else T.HP_CRIT)
        if hover:
            mode_bg = T.shade(mode_bg, 1.25)
        T.rounded_rect(self.screen, mode_bg, mode_rect, radius=6,
                       border_color=T.shade(mode_bg, 1.5))
        mode_color = T.BG if buying else (255, 245, 235)
        # center=True центрирует и по вертикали — без смещения «-2».
        self.text(mode, mode_rect[0] + mode_rect[2] // 2,
                  mode_rect[1] + mode_rect[3] // 2, mode_color,
                  font=T.FONT_BODY, center=True)
        game._ui_hits['shop']['mode'] = mode_rect

        where = "Город Этельгард" if city_shop else f"Глубина {game.depth}"
        self.text(where, self._modal_right(game), cy0 + 8,
                  T.TEXT_DIM, align_right=True)
        self.text(f"${game.player.gold}", self._modal_right(game), cy0 + 34,
                  T.GOLD, align_right=True)

        y = cy0 + 62
        if game.shop_submode == 'buy':
            y = T.section(self.screen, cx0, y, cw, "Товары")
            self._draw_shop_buy(game, cx0, y, cw, ch, cy0)
        else:
            y = T.section(self.screen, cx0, y, cw, "Продать")
            self._draw_shop_sell(game, cx0, y, cw, ch, cy0)
        action = "купить" if buying else "продать"
        hint = f"S / кнопка — режим · клик — {action} · Q/E — страницы"
        if not city_shop:
            hint += " · R — обновить"
        self.text(hint,
                  cx0 + cw // 2, cy0 + ch - 22, T.TEXT_MUTED,
                  font=T.FONT_SMALL, center=True)

    # ── Сетка ──
    def _shop_grid_metrics(self, x0, y, cw, ch, cy0):
        cols = self.SHOP_COLS
        gap = self.SHOP_GAP
        rows = max(1, (SHOP_PAGE_SIZE + cols - 1) // cols)
        avail_h = (cy0 + ch - 34) - y - 8
        cell_w = min(112, (cw - gap * (cols - 1)) // cols)
        cell_h = min(96, max(64, (avail_h - gap * (rows - 1)) // rows))
        grid_w = cols * cell_w + gap * (cols - 1)
        grid_x = x0 + (cw - grid_w) // 2
        return cols, rows, gap, cell_w, cell_h, grid_x

    def _shop_slot_rect(self, i, cols, gap, cell_w, cell_h, grid_x, y):
        row, col = divmod(i, cols)
        return (grid_x + col * (cell_w + gap),
                y + row * (cell_h + gap), cell_w, cell_h)

    def _draw_shop_slot(self, key, count, rect, price, game, hover,
                        dim=False, status=''):
        x, y, w, h = rect
        bg = (58, 49, 35) if hover else (32, 28, 22)
        if dim:
            bg = (26, 24, 20)
        border = T.ROW_BORDER if hover else T.PANEL_BORDER
        T.rounded_rect(self.screen, bg, rect, radius=8, border_color=border)

        icon_size = max(24, min(52, h - 34))
        self.icons.draw_item(key, x + (w - icon_size) // 2, y + 5,
                             size=icon_size)
        color = T.TEXT_MUTED if dim else item_display_color(key)
        label = self.fit_text(item_display_name(key), w - 8, T.FONT_SMALL)
        self.text(label, x + w // 2, y + h - 30, color,
                  font=T.FONT_SMALL, center=True)
        if status:
            self.text(status, x + w // 2, y + h - 14, T.HP_CRIT,
                      font=T.FONT_TINY, center=True)
        elif price is not None:
            self.text(f"{price} з.", x + w // 2, y + h - 14,
                      T.GOLD if not dim else T.TEXT_MUTED,
                      font=T.FONT_TINY, center=True)
        if count > 1:
            self.text(f"×{count}", x + w - 6, y + 3, T.TEXT,
                      font=T.FONT_TINY, align_right=True)

    def _draw_shop_buy(self, game, x0, y, cw, ch, cy0):
        stock = game.shop_stock
        total = max(1, (len(stock) + SHOP_PAGE_SIZE - 1) // SHOP_PAGE_SIZE)
        page = min(game.shop.page, total - 1)
        start = page * SHOP_PAGE_SIZE
        cols, rows, gap, cell_w, cell_h, grid_x = self._shop_grid_metrics(
            x0, y, cw, ch, cy0)
        mx, my = getattr(game, 'mouse_pos', (-1, -1))
        hovered = None
        for i, entry in enumerate(stock[start:start + SHOP_PAGE_SIZE]):
            key = entry['key']
            price = entry['price']
            sold = entry.get('sold', False)
            base, _ = parse_item_key(key)
            it = ITEMS.get(base, {})
            learned = False
            if it.get('type') == 'book':
                stat = it.get('stat')
                if stat in ('double_strike', 'regen', 'vampire', 'thorns',
                            'greedy'):
                    learned = getattr(game.player, stat, False)
            rect = self._shop_slot_rect(i, cols, gap, cell_w, cell_h,
                                        grid_x, y)
            hover = self._in_rect(rect, (mx, my))
            status = ''
            dim = sold or learned
            if sold:
                status = "продано"
            elif learned:
                status = "изучено"
            self._draw_shop_slot(key, 1, rect, price, game, hover,
                                 dim=dim, status=status)
            # page-local idx: контроллер сам добавляет смещение страницы.
            game._ui_hits['shop']['rows'].append((i, rect))
            if hover and not dim:
                hovered = (key, price)
        if hovered:
            self._draw_shop_tooltip(game, hovered[0], hovered[1], 'buy',
                                    (mx, my))
        if total > 1:
            self._draw_shop_pager(page, total, x0, cw, cy0, ch)

    def _draw_shop_sell(self, game, x0, y, cw, ch, cy0):
        # Тот же порядок и разбиение на стаки, что и в ShopController.sell.
        items = rules.inventory_stacks(game.player.inventory)
        cols, rows, gap, cell_w, cell_h, grid_x = self._shop_grid_metrics(
            x0, y, cw, ch, cy0)
        if not items:
            self.text("Рюкзак пуст", x0 + cw // 2, y + 40,
                      T.TEXT_MUTED, font=T.FONT_BODY, center=True)
            return
        total = max(1, (len(items) + SHOP_PAGE_SIZE - 1) // SHOP_PAGE_SIZE)
        page = min(game.shop.page, total - 1)
        start = page * SHOP_PAGE_SIZE
        mx, my = getattr(game, 'mouse_pos', (-1, -1))
        hovered = None
        for i, (key, count) in enumerate(items[start:start + SHOP_PAGE_SIZE]):
            price = item_sell_price(key)
            rect = self._shop_slot_rect(i, cols, gap, cell_w, cell_h,
                                        grid_x, y)
            hover = self._in_rect(rect, (mx, my))
            self._draw_shop_slot(key, count, rect, price, game, hover)
            # page-local idx: контроллер сам добавляет смещение страницы.
            game._ui_hits['shop']['rows'].append((i, rect))
            if hover:
                hovered = (key, price)
        if hovered:
            self._draw_shop_tooltip(game, hovered[0], hovered[1], 'sell',
                                    (mx, my))
        if total > 1:
            self._draw_shop_pager(page, total, x0, cw, cy0, ch)

    def _draw_shop_pager(self, page, total, x0, cw, cy0, ch):
        self.text(f"Стр. {page + 1}/{total}",
                  x0 + cw // 2, cy0 + ch - 44, T.TEXT_DIM,
                  font=T.FONT_SMALL, center=True)

    def _draw_shop_tooltip(self, game, key, price, mode, pos):
        """Подсказка магазина: все характеристики предмета и цена."""
        from core.data import class_can_use
        base, _ = parse_item_key(key)
        it = ITEMS.get(base, {})
        lines = [(item_display_name(key), T.TITLE_LIGHT, T.FONT_BODY)]
        type_names = {
            'weapon': 'Оружие', 'armor': 'Броня', 'head': 'Шлем',
            'helmet': 'Шлем', 'legs': 'Штаны', 'leggings': 'Штаны',
            'boots': 'Обувь', 'heal': 'Зелье лечения',
            'mana': 'Зелье маны', 'poison': 'Яд', 'buff': 'Эликсир',
            'resource': 'Ресурс', 'book': 'Книга', 'key': 'Ключ',
        }
        tname = type_names.get(it.get('type'))
        if tname:
            lines.append((tname, T.TEXT_DIM, T.FONT_SMALL))
        desc = it.get('desc', '')
        if desc:
            lines.append((desc, T.TEXT_MUTED, T.FONT_TINY))
        if it.get('type') in ('weapon', 'armor', 'head', 'helmet', 'legs',
                              'leggings', 'boots'):
            bonus = it.get('value', 0) + parse_item_key(key)[1]
            stat = 'ATK' if it.get('type') == 'weapon' else 'DEF'
            if bonus:
                lines.append((f"+{bonus} {stat}", T.HEAL, T.FONT_SMALL))
            deltas = self.compare_item(game, key, it['type'])
            dt, dc = self.format_delta(deltas)
            if dt:
                lines.append((dt, dc, T.FONT_SMALL))
        lvl_req = it.get('level_req', 1)
        if lvl_req > 1:
            ok = game.player.level >= lvl_req
            lines.append((f"Требуется уровень {lvl_req}",
                          T.TEXT_DIM if ok else T.HP_CRIT, T.FONT_SMALL))
        can_use, why = class_can_use(game.player.class_key, key)
        if not can_use:
            lines.append((f"Класс не может: {why}", T.HP_CRIT, T.FONT_SMALL))
        if mode == 'buy':
            afford = game.player.gold >= price
            lines.append((f"Цена: {price} з.",
                          T.GOLD if afford else T.HP_CRIT, T.FONT_BODY))
        else:
            lines.append((f"Продажа: {price} з.", T.GOLD, T.FONT_BODY))

        rendered = []
        max_w = 0
        total_h = 0
        for text, color, font in lines:
            text = self.fit_text(text, 380, font)
            surf = T.render(text, color, font)
            rendered.append((surf, total_h))
            max_w = max(max_w, surf.get_width())
            total_h += surf.get_height() + 4
        pad = 10
        w = max_w + pad * 2
        h = total_h + pad * 2 - 4
        x = min(max(8, pos[0] + 18), config.SCREEN_W - w - 8)
        y = min(max(8, pos[1] + 18), config.SCREEN_H - h - 8)
        T.rounded_rect(self.screen, (18, 16, 13), (x, y, w, h), radius=8,
                       border_color=T.ROW_BORDER)
        for surf, dy in rendered:
            self.screen.blit(surf, (x + pad, y + pad + dy))

    # ════════════════════════ ДРЕВО НАВЫКОВ ════════════════════════
