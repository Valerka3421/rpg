"""Меню и магазин: open_menu/shop, buy/sell/reroll, craft_*, menu_start_new, class_select_confirm.
"""
from game.game._common import *  # noqa: F401,F403 — все импорты


class MenuMixin:
    """Меню и магазин: open_menu/shop, buy/sell/reroll, craft_*, menu_start_new, class_select_confirm."""

    def open_menu(self, from_game=False):
        from audio import Audio
        Audio.play('menu_open')
        self.menu.open(from_game)


    def menu_button_rects(self):
        return self.menu.button_rects()


    def menu_return_to_game(self):
        self.menu.return_to_game()


    def open_shop(self):
        if not self.merchant:
            self.log("На этом этаже нет торговца.", COL_DIM)
            return
        if max(abs(self.player.x - self.merchant.x),
               abs(self.player.y - self.merchant.y)) > 1:
            self.log("Подойди ближе к торговцу.", COL_DIM)
            return
        self.shop.open()


    def open_city_shop(self, npc_type=None):
        """Открывает магазин горожанина (кузнец/алхимик/трактирщик)."""
        if npc_type is None:
            npc_type = getattr(self, 'active_shop_npc', None)
        if not is_city_shop_npc(npc_type):
            self.shop.open()
            return
        npc = next((n for n in getattr(self, 'city_npcs', ())
                    if n.npc_type == npc_type), None)
        if npc is None:
            self.log("Торговца нет на месте.", COL_DIM)
            return
        if (self.world_mode != 'overworld' or self.location != 'village'
                or max(abs(self.player.x - npc.x),
                       abs(self.player.y - npc.y)) > 1):
            self.log("Подойди ближе к торговцу.", COL_DIM)
            return
        self.shop.open(npc_type)


    def buy_item(self, idx):
        self.shop.buy(idx)


    def sell_item(self, idx):
        self.shop.sell(idx)


    def reroll_shop(self):
        self.shop.reroll()


    def refresh_shop(self):
        self.shop.refresh()


    def shop_pages_count(self):
        return self.shop.pages_count()


    def craftable_list(self):
        return self.craft.list()


    def craft_page_items(self):
        return self.craft.page_items()


    def craft_pages_count(self):
        return self.craft.pages_count()


    def craft_tabs(self):
        return self.craft.tabs()


    def craft_item(self, idx):
        self.craft.craft(idx)


    def menu_start_new(self):
        self.menu.start_new()


    def menu_load_slot(self, slot):
        self.menu.load_slot(slot)


    def class_select_confirm(self):
        self.menu.confirm_class()


    def _main_menu_labels(self):
        return self.menu.labels()

    @property

    def menu_selection(self):
        return self.menu.selection

    @menu_selection.setter

    def menu_selection(self, v):
        self.menu.selection = v

    # ════════════════════════ UPDATE / DRAW ════════════════════════

