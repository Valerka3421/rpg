"""Контроллер крафта: вкладки навыков, страницы, мини-игра качества."""
import pygame

from core import rules, skills
from core.config import CRAFT_PAGE_SIZE
from core.data import CRAFT_TABS, ITEMS, item_display_name


class CraftController:
    def __init__(self, game):
        self.game = game
        self.tab = 'all'
        self.page = 0
        self.selection = 0
        self.minigame = None          # {'key','tab','recipe_index','pos',...}
        self.last_quality = None
        self.quantity = 1             # сколько предметов создать за раз

    # ── Состояние ──
    @property
    def minigame_active(self):
        return self.minigame is not None

    def open(self):
        self.game.screen_mode = 'craft'
        self.minigame = None

    @staticmethod
    def tabs():
        return CRAFT_TABS

    def set_tab(self, tab):
        if tab not in [key for key, _ in CRAFT_TABS]:
            return
        self.tab = tab
        self.page = 0
        self.selection = 0
        self.minigame = None

    def next_tab(self, delta):
        keys = [key for key, _ in CRAFT_TABS]
        if self.tab not in keys:
            self.set_tab(keys[0])
            return
        index = keys.index(self.tab)
        self.set_tab(keys[(index + delta) % len(keys)])

    # ── Рецепты и страницы ──
    def list(self):
        """[(key, data, recipe, can, category, recipe_index, count)].

        Показываем только открытые рецепты: остальные появляются по мере
        прокачки навыка.
        """
        g = self.game
        return rules.craftable_list(g.player.inventory, g.player.level,
                                    g.player, tab=self.tab,
                                    unlocked_only=True)

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

    # ── Крафт ──
    def craft(self, idx):
        g = self.game
        if self.minigame_active:
            return self.finish_minigame()
        craftable = self.list()
        if not (0 <= idx < len(craftable)):
            return False
        key, item = craftable[idx][0], craftable[idx][1]
        category = craftable[idx][4]
        recipe_index = craftable[idx][5]
        recipe, has_materials, allowed = rules.recipe_info(
            key, g.player.inventory, g.player.level, g.player, recipe_index)
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

        quantity = self.effective_quantity(item)
        if skills.has_craft_minigame(g.player, category):
            skill_req = int(item.get('skill_req', 1))
            # Мини-игра сложнее для дорогих рецептов и больших партий.
            speed = 0.85 + skill_req * 0.02 + (quantity - 1) * 0.04
            self.minigame = {'key': key, 'tab': category,
                             'recipe_index': recipe_index,
                             'quantity': quantity,
                             'pos': 0.0, 'dir': 1,
                             'speed': min(2.4, speed)}
            return True
        return self._finish_craft(key, None, recipe_index, quantity)

    def effective_quantity(self, item=None):
        """Сколько предметов реально создастся: снаряжение — по 1."""
        if item and item.get('type') in rules.EQUIP_TYPES:
            return 1
        return max(1, int(self.quantity))

    def change_quantity(self, delta):
        item = None
        craftable = self.list()
        if 0 <= self.selection < len(craftable):
            item = craftable[self.selection][1]
        if item and item.get('type') in rules.EQUIP_TYPES:
            self.quantity = 1
            return self.quantity
        self.quantity = max(1, min(99, self.quantity + (1 if delta > 0
                                                        else -1)))
        return self.quantity

    def _finish_craft(self, key, quality, recipe_index=None, quantity=1):
        g = self.game
        results = rules.craft_many(g.player, key, quantity, quality,
                                   recipe_index)
        ok = [r for r in results if r.ok]
        if not ok:
            last = results[-1] if results else None
            if last is not None and last.reason == 'materials':
                g.log("Не хватает материалов!", (255, 100, 100))
            else:
                g.log("Крафт не удался.", (255, 100, 100))
            return False
        total = sum(r.count for r in ok)
        result = ok[-1]
        g.total_crafts += len(ok)
        g.quest_log.update_craft(key)
        name = item_display_name(result.key)
        if total > 1:
            name += f" ×{total}"
        g.log(f"Создано: {name}", (120, 240, 120))
        if result.quality:
            g.log(f"Качество +{result.quality}!", (240, 210, 90))
        seen_levels = set()
        for res in ok:
            for level in res.skill_levels:
                if level not in seen_levels:
                    seen_levels.add(level)
                    tab_name = skills.TAB_NAMES.get(res.skill, res.skill)
                    g.log(f"{tab_name}: уровень {level}!", (240, 210, 90))
        g.add_float(g.player.x, g.player.y, "КРАФТ", (240, 210, 90))
        self.last_quality = result.quality
        self.page = min(self.page, self.pages_count() - 1)
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
        item = ITEMS.get(mg['key'], {})
        skill_req = int(item.get('skill_req', 1))
        quantity = max(1, int(mg.get('quantity', 1)))
        # Чем сложнее рецепт и больше партия — тем труднее удержать
        # высокое качество.
        accuracy -= skill_req * 0.008
        accuracy -= min(0.30, (quantity - 1) * 0.03)
        accuracy = max(0.0, accuracy)
        bonus = skills.craft_quality_bonus(self.game.player)
        quality = rules.quality_from_accuracy(accuracy, bonus)
        key = mg['key']
        recipe_index = mg.get('recipe_index')
        self.minigame = None
        return self._finish_craft(key, quality, recipe_index, quantity)

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
        elif key in (pygame.K_q, pygame.K_a):
            self.next_tab(-1)
        elif key in (pygame.K_e, pygame.K_d):
            self.next_tab(1)
        elif key in (pygame.K_LEFT, pygame.K_PAGEUP):
            self.prev_page()
        elif key in (pygame.K_RIGHT, pygame.K_PAGEDOWN):
            self.next_page()
        elif pygame.K_1 <= key <= pygame.K_8:
            self.craft(self.page_offset() + (key - pygame.K_1))
        elif key in (pygame.K_LEFTBRACKET, pygame.K_MINUS,
                     pygame.K_KP_MINUS):
            self.change_quantity(-1)
        elif key in (pygame.K_RIGHTBRACKET, pygame.K_EQUALS,
                     pygame.K_PLUS, pygame.K_KP_PLUS):
            self.change_quantity(1)
