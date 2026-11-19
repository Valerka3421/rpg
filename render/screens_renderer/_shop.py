"""Экран магазина: draw_shop, buy/sell панели.
"""
from render.screens_renderer._common import *  # noqa: F401,F403


class ShopMixin:
    """Экран магазина: draw_shop, buy/sell панели."""

    def draw_shop(self, game):
        self._modal_bg(game)
        cx0, cy0, cw, ch = game._modal_content_rect
        game._ui_hits['shop'] = {'rows': []}
        npc_type = getattr(game, 'active_shop_npc', None)
        city_shop = is_city_shop_npc(npc_type)
        header = CITY_NPC_NAMES.get(npc_type, 'ТОРГОВЕЦ') if city_shop \
            else "ТОРГОВЕЦ"
        self.text(header.upper(), cx0, cy0, T.MERCH, font=T.FONT_H1)
        # Переключатель режима — крупная кликабельная кнопка, а не только
        # горячая клавиша S: продажа должна быть очевидной в городе.
        buying = game.shop_submode == 'buy'
        mode = "ПОКУПКА" if buying else "ПРОДАЖА"
        mode_rect = (cx0 + (cw - 176) // 2, cy0 + 4, 176, 30)
        mx, my = getattr(game, 'mouse_pos', (-1, -1))
        hover = (mode_rect[0] <= mx <= mode_rect[0] + mode_rect[2]
                 and mode_rect[1] <= my <= mode_rect[1] + mode_rect[3])
        mode_bg = (T.MERCH if buying else T.HP_CRIT)
        if hover:
            mode_bg = T.shade(mode_bg, 1.25)
        T.rounded_rect(self.screen, mode_bg, mode_rect, radius=6,
                       border_color=T.shade(mode_bg, 1.5))
        mode_color = T.BG if buying else (255, 245, 235)
        self.text(mode, mode_rect[0] + mode_rect[2] // 2,
                  mode_rect[1] + 6, mode_color, font=T.FONT_BODY, center=True)
        game._ui_hits['shop']['mode'] = mode_rect
        where = "Город Этельгард" if city_shop else f"Глубина {game.depth}"
        self.text(where, self._modal_right(game), cy0 + 8,
                  T.TEXT_DIM, align_right=True)
        self.text(f"${game.player.gold}", self._modal_right(game), cy0 + 34,
                  T.GOLD, align_right=True)
        y = cy0 + 66
        if game.shop_submode == 'buy':
            y = T.section(self.screen, cx0, y, cw, "Товары")
            self._draw_shop_buy(game, cx0, y, cw)
        else:
            y = T.section(self.screen, cx0, y, cw, "Продать")
            self._draw_shop_sell(game, cx0, y, cw)
        action = "купить" if buying else "продать"
        hint = f"S / кнопка — режим · клик — {action} · Q/E — страницы"
        if not city_shop:
            hint += " · R — обновить"
        self.text(hint,
                  cx0 + cw // 2, cy0 + ch - 22, T.TEXT_MUTED,
                  font=T.FONT_SMALL, center=True)


    def _draw_shop_buy(self, game, x0, y, cw):
        stock = game.shop_stock
        total = max(1, (len(stock) + SHOP_PAGE_SIZE - 1) // SHOP_PAGE_SIZE)
        page = min(game.shop.page, total - 1)
        start = page * SHOP_PAGE_SIZE
        for i, entry in enumerate(stock[start:start + SHOP_PAGE_SIZE]):
            key = entry['key']
            price = entry['price']
            sold = entry.get('sold', False)
            base, _ = parse_item_key(key)
            it = ITEMS.get(base, {})
            color = item_display_color(key)
            learned = False
            if it.get('type') == 'book':
                stat = it.get('stat')
                if stat in ('double_strike', 'regen', 'vampire', 'thorns', 'greedy'):
                    learned = getattr(game.player, stat, False)
            if sold or learned:
                status = "(продано)" if sold else "(✓ изучено)"
                self.icons.draw_item(key, x0, y, size=28)
                self.text(f"{item_display_name(key)}  {status}",
                          x0 + 36, y + 4, T.TEXT_MUTED, font=T.FONT_SMALL)
                y += 34
                continue
            can_afford = game.player.gold >= price
            row_y = y
            self.icons.draw_item(key, x0, y, size=28)
            self.text(item_display_name(key), x0 + 36, y + 4,
                      color if can_afford else T.TEXT_MUTED)
            self.text(f"{price} з.", self._modal_right(game), y + 4,
                      T.GOLD if can_afford else T.TEXT_MUTED, align_right=True)
            self.text(it.get('desc', ''), x0 + 36, y + 24, T.TEXT_MUTED,
                      font=T.FONT_TINY)
            if it.get('type') in ('weapon', 'armor', 'head', 'legs', 'boots',
                                  'helmet', 'leggings'):
                deltas = self.compare_item(game, key, it['type'])
                dt, dc = self.format_delta(deltas)
                if dt:
                    self.text(dt, x0 + 36, y + 38, dc, font=T.FONT_TINY)
                    y += 10
            y += 46
            # start + i — глобальный индекс в shop_stock, а не page-local
            game._ui_hits['shop']['rows'].append(
                (start + i, (x0, row_y, cw, y - row_y - 6)))
        if total > 1:
            self.text(f"Стр. {page + 1}/{total}",
                      self._modal_right(game), y + 6, T.TEXT_DIM,
                      font=T.FONT_SMALL, align_right=True)


    def _draw_shop_sell(self, game, x0, y, cw):
        # Тот же порядок и разбиение на стаки, что и в ShopController.sell:
        # иначе клик по строке продавал бы не тот предмет.
        items = rules.inventory_stacks(game.player.inventory)
        if not items:
            self.text("  (рюкзак пуст)", x0, y, T.TEXT_MUTED, font=T.FONT_SMALL)
            return
        total = max(1, (len(items) + SHOP_PAGE_SIZE - 1) // SHOP_PAGE_SIZE)
        page = min(game.shop.page, total - 1)
        start = page * SHOP_PAGE_SIZE
        for i, (key, count) in enumerate(items[start:start + SHOP_PAGE_SIZE]):
            price = item_sell_price(key)
            row_y = y
            self.icons.draw_item(key, x0, y, size=28)
            label = item_display_name(key)
            if count > 1:
                label += f"  x{count}"
            self.text(label, x0 + 36, y + 4,
                      item_display_color(key))
            self.text(f"{price} з.", self._modal_right(game), y + 4, T.GOLD,
                      align_right=True)
            self.text(ITEMS.get(parse_item_key(key)[0], {}).get('desc', ''),
                      x0 + 36, y + 24, T.TEXT_MUTED, font=T.FONT_TINY)
            y += 46
            # глобальный индекс в player.inventory, а не page-local
            game._ui_hits['shop']['rows'].append(
                (start + i, (x0, row_y, cw, y - row_y - 6)))
        if total > 1:
            self.text(f"Стр. {page + 1}/{total}",
                      self._modal_right(game), y + 6, T.TEXT_DIM,
                      font=T.FONT_SMALL, align_right=True)

    # ════════════════════════ ДРЕВО НАВЫКОВ ════════════════════════

