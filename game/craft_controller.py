"""Контроллер крафта: вкладки веток, требования навыков и мини-игра качества."""
import pygame

from core import rules, skills
from core.data import item_display_name


class CraftController:
    def __init__(self, game):
        self.game = game
        self.tab = skills.CRAFT_TABS[0]
        self.selection = 0
        self.minigame = None          # {'key','tab','pos','dir','speed'}
        self.last_quality = None

    # ── Состояние ──
    @property
    def minigame_active(self):
        return self.minigame is not None

    def open(self):
        self.game.screen_mode = 'craft'
        self.minigame = None

    def set_tab(self, tab):
        if tab in skills.CRAFT_TABS:
            self.tab = tab
            self.selection = 0
            self.minigame = None

    def next_tab(self, delta):
        index = skills.CRAFT_TABS.index(self.tab)
        self.set_tab(skills.CRAFT_TABS[
            (index + delta) % len(skills.CRAFT_TABS)])

    def list(self):
        """[(key, data, recipe, можно_ли_скрафтить)] для текущей вкладки."""
        g = self.game
        return rules.craftable_list(g.player.inventory, g.player.level,
                                    g.player, tab=self.tab)

    # ── Действия ──
    def craft(self, idx):
        g = self.game
        if self.minigame_active:
            return self.finish_minigame()
        craftable = self.list()
        if not (0 <= idx < len(craftable)):
            return False
        key, item = craftable[idx][0], craftable[idx][1]
        recipe, has_materials, allowed = rules.recipe_info(
            key, g.player.inventory, g.player.level, g.player)
        if not allowed:
            if g.player.level < item.get('level_req', 1):
                need = item.get('level_req', 1)
                g.log(f"Нужен уровень {need}! (сейчас {g.player.level})",
                      (255, 100, 100))
            else:
                tab, req = skills.recipe_requirement(item)
                g.log(f"Нужен уровень {skills.TAB_NAMES.get(tab, tab)} "
                      f"{req}.", (255, 100, 100))
            return False
        if not has_materials:
            g.log("Не хватает материалов!", (255, 100, 100))
            return False

        if skills.has_craft_minigame(g.player, self.tab):
            self.minigame = {'key': key, 'tab': self.tab, 'pos': 0.0,
                             'dir': 1, 'speed': 0.85}
            return True
        return self._finish_craft(key, None)

    def _finish_craft(self, key, quality):
        g = self.game
        result = rules.craft_item(g.player, key, quality)
        if not result.ok:
            g.log("Крафт не удался.", (255, 100, 100))
            return False
        g.total_crafts += 1
        g.quest_log.update_craft(key)
        g.log(f"Создано: {item_display_name(result.key)}", (120, 240, 120))
        if result.quality:
            g.log(f"Качество +{result.quality}!", (240, 210, 90))
        for level in result.skill_levels:
            tab_name = skills.TAB_NAMES.get(result.skill, result.skill)
            g.log(f"{tab_name}: уровень {level}!", (240, 210, 90))
        g.add_float(g.player.x, g.player.y, "КРАФТ", (240, 210, 90))
        self.last_quality = result.quality
        return True

    def update(self, dt):
        mg = self.minigame
        if not mg:
            return
        mg['pos'] += mg['dir'] * mg['speed'] * dt
        if mg['pos'] >= 1.0:
            mg['pos'] = 1.0
            mg['dir'] = -1
        elif mg['pos'] <= 0.0:
            mg['pos'] = 0.0
            mg['dir'] = 1

    def finish_minigame(self):
        mg = self.minigame
        if not mg:
            return False
        accuracy = 1.0 - abs(mg['pos'] - 0.5) * 2.0
        bonus = skills.craft_quality_bonus(self.game.player)
        quality = rules.quality_from_accuracy(accuracy, bonus)
        key = mg['key']
        self.minigame = None
        return self._finish_craft(key, quality)

    def cancel_minigame(self):
        if self.minigame:
            self.minigame = None
            self.game.log("Крафт отменён.", (180, 160, 120))
            return True
        return False

    def handle_click(self, pos):
        if self.minigame_active:
            self.finish_minigame()
            return True
        return False

    def handle_key(self, key):
        g = self.game
        if self.minigame_active:
            if key in (pygame.K_SPACE, pygame.K_RETURN, pygame.K_KP_ENTER):
                self.finish_minigame()
            elif key == pygame.K_ESCAPE:
                self.cancel_minigame()
            return
        if key in (pygame.K_k, pygame.K_ESCAPE):
            g.screen_mode = 'game'
        elif key in (pygame.K_LEFT, pygame.K_a, pygame.K_q):
            self.next_tab(-1)
        elif key in (pygame.K_RIGHT, pygame.K_d, pygame.K_e):
            self.next_tab(1)
        elif pygame.K_1 <= key <= pygame.K_9:
            self.craft(key - pygame.K_1)
