"""Контроллер крафта."""
import pygame
from core import rules
from core.data import item_display_name


class CraftController:
    def __init__(self, game):
        self.game = game

    def open(self):
        self.game.screen_mode = 'craft'

    def list(self):
        """[(key, data, recipe, можно_ли_скрафтить)] с учётом уровня."""
        g = self.game
        return rules.craftable_list(g.player.inventory, g.player.level)

    def craft(self, idx):
        g = self.game
        craftable = self.list()
        if idx < 0 or idx >= len(craftable):
            return False
        key, item = craftable[idx][0], craftable[idx][1]
        ok, reason = rules.craft(key, g.player.inventory, g.player.level)
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
        g.log(f"Создано: {item_display_name(key)}", (120, 240, 120))
        g.add_float(g.player.x, g.player.y, "КРАФТ", (240, 210, 90))
        return True

    def handle_key(self, key):
        g = self.game
        if key in (pygame.K_k, pygame.K_ESCAPE):
            g.screen_mode = 'game'
        elif pygame.K_1 <= key <= pygame.K_8:
            self.craft(key - pygame.K_1)
