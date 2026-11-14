"""Контроллер крафта: вкладки навыков, страницы и начисление опыта."""
import pygame

from core import rules
from core.config import CRAFT_PAGE_SIZE
from core.data import (item_display_name, craft_skill_reward, SKILLS,
                       CRAFT_TABS)


class CraftController:
    def __init__(self, game):
        self.game = game
        self.tab = 'all'
        self.page = 0

    def open(self):
        self.game.screen_mode = 'craft'

    # ── вкладки и страницы ──
    @staticmethod
    def tabs():
        return CRAFT_TABS

    def set_tab(self, tab):
        if tab not in [key for key, _ in CRAFT_TABS]:
            return
        self.tab = tab
        self.page = 0

    def list(self):
        """[(key, data, recipe, can, category, recipe_index, count)]."""
        g = self.game
        return rules.craftable_list(g.player.inventory, g.player.level,
                                    tab=self.tab)

    def pages_count(self):
        total = len(self.list())
        return max(1, (total + CRAFT_PAGE_SIZE - 1) // CRAFT_PAGE_SIZE)

    def page_offset(self):
        return self.page * CRAFT_PAGE_SIZE

    def page_items(self):
        items = self.list()
        start = self.page_offset()
        return items[start:start + CRAFT_PAGE_SIZE]

    def prev_page(self):
        self.page = max(0, self.page - 1)

    def next_page(self):
        self.page = min(self.pages_count() - 1, self.page + 1)

    # ── крафт ──
    def craft(self, idx):
        g = self.game
        craftable = self.list()
        if idx < 0 or idx >= len(craftable):
            return False
        key, item = craftable[idx][0], craftable[idx][1]
        recipe_index = craftable[idx][5]
        count = craftable[idx][6]
        ok, reason = rules.craft(key, g.player.inventory, g.player.level,
                                 recipe_index)
        if not ok:
            if reason == 'level':
                need = item.get('level_req', 1)
                g.log(f"Нужен уровень {need}! (сейчас {g.player.level})",
                      (255, 100, 100))
            else:
                g.log("Не хватает материалов!", (255, 100, 100))
            return False
        g.total_crafts += 1
        g.quest_log.update_craft(key)
        g.log(f"Создано: {item_display_name(key)}"
              + (f" ×{count}" if count > 1 else ""), (120, 240, 120))

        category = craftable[idx][4]
        xp = craft_skill_reward(key, count)
        levels = rules.give_skill_xp(g.player, category, xp)
        if levels:
            g.log(f"{SKILLS[category]['name']}: новый уровень "
                  f"{g.player.skills[category]['level']}!", (240, 210, 90))

        g.add_float(g.player.x, g.player.y, "КРАФТ", (240, 210, 90))
        return True

    def handle_key(self, key):
        g = self.game
        if key in (pygame.K_k, pygame.K_ESCAPE):
            g.screen_mode = 'game'
        elif key in (pygame.K_LEFT, pygame.K_PAGEUP):
            self.prev_page()
        elif key in (pygame.K_RIGHT, pygame.K_PAGEDOWN):
            self.next_page()
        elif pygame.K_1 <= key <= pygame.K_8:
            self.craft(self.page_offset() + (key - pygame.K_1))
