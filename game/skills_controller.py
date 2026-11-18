"""Контроллер экрана «Древо навыков».

Держит выбранную вкладку и строку, покупает узлы классовой ветки и
выполняет смену класса. Вся математика — в ``core.skills``; здесь только
навигация, сообщения и состояние для отрисовки.
"""
import pygame

from core import skills
from core.data import CLASSES


class SkillsController:
    def __init__(self, game):
        self.game = game
        self.tab = skills.TAB_CLASS
        self.selection = 0

    # ── Открытие/закрытие ──
    def open(self):
        self.game.screen_mode = 'skills'
        self.clamp()

    def close(self):
        self.game.screen_mode = 'game'

    # ── Навигация ──
    def rows(self):
        p = self.game.player
        if self.tab == skills.TAB_CLASS:
            return skills.available_nodes(p)
        return skills.craft_tree(self.tab)

    def clamp(self):
        count = len(self.rows())
        self.selection = max(0, min(self.selection, max(0, count - 1)))

    def move(self, delta):
        count = len(self.rows())
        if count:
            self.selection = (self.selection + delta) % count

    def set_tab(self, tab):
        if tab in skills.TABS:
            self.tab = tab
            self.selection = 0

    def next_tab(self, delta):
        index = skills.TABS.index(self.tab)
        self.tab = skills.TABS[(index + delta) % len(skills.TABS)]
        self.selection = 0

    # ── Действия ──
    def activate(self):
        """Enter: купить узел класса. В ремесленных вкладках — подсказка."""
        if self.tab != skills.TAB_CLASS:
            row = self.rows()[self.selection] if self.rows() else None
            if row:
                self.game.log(
                    f"{row['name']}: уровень {self.craft_level()} / {row['level']}.",
                    (200, 180, 130))
            return
        row = self.rows()[self.selection] if self.rows() else None
        if not row:
            return
        if row.get('learned'):
            self.game.log(f"«{row['name']}» уже изучен.", (180, 160, 120))
            return
        ok, reason = skills.learn_node(self.game.player, row['key'])
        if ok:
            self.game.log(f"Изучено: {row['name']}!", (240, 210, 90))
        else:
            messages = {
                'level': f"Нужен уровень {row['level']}.",
                'points': 'Нет очков навыков.',
                'class': 'Узел другого класса.',
                'learned': 'Уже изучен.',
            }
            self.game.log(messages.get(reason, 'Нельзя изучить.'),
                          (255, 120, 120))

    def craft_level(self, tab=None):
        return skills.craft_level(self.game.player, tab or self.tab)

    # ── Смена класса ──
    def promotion_options(self):
        return skills.promotion_options(self.game.player)

    def change_class_requirement(self):
        return skills.class_change_requirement(self.game.player)

    def change_class(self, index):
        options = self.promotion_options()
        if not (0 <= index < len(options)):
            return False
        key = options[index]
        p = self.game.player
        old_nodes = set(getattr(p, 'skill_nodes', set()))
        ok, reason = skills.change_class(p, key)
        if ok:
            name = CLASSES.get(key, {}).get('name', key)
            self.game.log(f"Новый класс: {name}!", (240, 210, 90))
            dropped = old_nodes - set(getattr(p, 'skill_nodes', set()))
            if dropped:
                self.game.log(
                    f"Ветка прежнего класса сброшена: возвращено "
                    f"{len(dropped)} очк. навыков.", (200, 184, 140))
            self.selection = 0
            self.tab = skills.TAB_CLASS
            # Смена класса могла запретить текущую экипировку.
            for slot, item_key, why in skills.disallowed_equipment(p):
                setattr(p, slot, None)
                p.inventory[item_key] = p.inventory.get(item_key, 0) + 1
                self.game.log(f"Снято ({why}).", (255, 160, 120))
            p.recalc()
        else:
            messages = {
                'level': f"Нужен уровень {skills.CLASS_CHANGE_LEVELS[0]}.",
                'path': 'Этот класс недоступен.',
                'max': 'Достигнута вершина ветки.',
            }
            self.game.log(messages.get(reason, 'Смена класса невозможна.'),
                          (255, 120, 120))
        return ok

    # ── Ввод ──
    def handle_key(self, key, scancode=0, unicode='', mods=0):
        if key in (pygame.K_ESCAPE, pygame.K_n):
            self.close()
            return
        if key in (pygame.K_UP, pygame.K_w):
            self.move(-1)
        elif key in (pygame.K_DOWN, pygame.K_s):
            self.move(1)
        elif key in (pygame.K_LEFT, pygame.K_a, pygame.K_q):
            self.next_tab(-1)
        elif key in (pygame.K_RIGHT, pygame.K_d, pygame.K_e):
            self.next_tab(1)
        elif key in (pygame.K_RETURN, pygame.K_KP_ENTER, pygame.K_SPACE):
            self.activate()
        elif pygame.K_1 <= key <= pygame.K_9:
            self.change_class(key - pygame.K_1)
        elif key == pygame.K_TAB:
            self.next_tab(1)
