"""Контроллер магазина: покупка, продажа, реролл, пагинация."""
import pygame
from core import rules
from core.config import SHOP_PAGE_SIZE
from core.data import make_shop_stock, reroll_price, item_display_name


class ShopController:
    def __init__(self, game):
        self.game = game
        self.page = 0

    def open(self):
        g = self.game
        if not g.merchant:
            g.log("На этом этаже нет торговца.", (110, 110, 130))
            return
        if max(abs(g.player.x - g.merchant.x),
               abs(g.player.y - g.merchant.y)) > 1:
            g.log("Подойди ближе к торговцу.", (110, 110, 130))
            return
        g.shop_submode = 'buy'
        self.page = 0
        g.screen_mode = 'shop'

    def refresh(self):
        g = self.game
        stock = make_shop_stock(g.depth)
        if g.player.double_strike:
            stock = [s for s in stock if s['key'] != 'book_double']
        g.shop_stock = stock

    def _page_index(self, idx):
        start = self.page * SHOP_PAGE_SIZE
        return start + idx

    def buy(self, idx):
        g = self.game
        abs_idx = self._page_index(idx)
        ok, reason, price = rules.buy_from_stock(g.shop_stock, abs_idx, g.player)
        if not ok:
            if reason == 'sold':
                g.log("Уже продано.", (110, 110, 130))
            elif reason == 'learned':
                g.log("Двойной удар уже изучен.", (110, 110, 130))
            elif reason == 'gold':
                g.log(f"Не хватает {price - g.player.gold} золота.",
                      (255, 100, 100))
            return
        key = g.shop_stock[abs_idx]['key']
        g.log(f"Куплено: {item_display_name(key)} за {price} з.", (120, 240, 120))

    def sell(self, idx):
        g = self.game
        items = sorted(g.player.inventory.items())
        abs_idx = self._page_index(idx)
        if not (0 <= abs_idx < len(items)):
            return
        key = items[abs_idx][0]
        price = rules.sell_price_for(key)
        if price <= 0:
            g.log("Этот предмет нельзя продать.", (110, 110, 130))
            return
        g.consume(key)
        g.player.gold += price
        g.log(f"Продано: {item_display_name(key)} за {price} з.", (120, 240, 120))
        self.page = min(self.page, self.pages_count() - 1)

    def reroll(self):
        g = self.game
        price = reroll_price(g.depth)
        if g.player.gold < price:
            g.log(f"Обновление стоит {price} з. Не хватает.", (255, 100, 100))
            return
        g.player.gold -= price
        self.refresh()
        self.page = 0
        g.log(f"Ассортимент обновлён (-{price} з.)", (120, 240, 120))

    def pages_count(self):
        g = self.game
        n = len(g.shop_stock) if g.shop_submode == 'buy' else len(g.player.inventory)
        return max(1, (n + SHOP_PAGE_SIZE - 1) // SHOP_PAGE_SIZE)

    def next_page(self):
        self.page = min(self.pages_count() - 1, self.page + 1)

    def prev_page(self):
        self.page = max(0, self.page - 1)

    def _digit_count(self):
        g = self.game
        if g.shop_submode == 'buy':
            return len(g.shop_stock) - self.page * SHOP_PAGE_SIZE
        return len(g.player.inventory) - self.page * SHOP_PAGE_SIZE

    def handle_key(self, key):
        g = self.game
        if key in (pygame.K_t, pygame.K_ESCAPE):
            g.screen_mode = 'game'
        elif key == pygame.K_s:
            g.shop_submode = 'sell' if g.shop_submode == 'buy' else 'buy'
            self.page = 0
        elif key == pygame.K_r and g.shop_submode == 'buy':
            self.reroll()
        elif key == pygame.K_q:
            self.prev_page()
        elif key == pygame.K_e:
            self.next_page()
        elif pygame.K_1 <= key <= pygame.K_9:
            idx = key - pygame.K_1
            if idx < min(SHOP_PAGE_SIZE, max(0, self._digit_count())):
                if g.shop_submode == 'buy':
                    self.buy(idx)
                else:
                    self.sell(idx)
