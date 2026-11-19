"""Контроллер экрана «Древо навыков».

Держит выбранную вкладку и строку, покупает узлы классовой ветки и
выполняет смену класса. Вся математика — в ``core.skills``; здесь только
навигация, сообщения и состояние для отрисовки.
"""
import pygame

from core import config, skills
from core.data import CLASSES


class SkillsController:
    def __init__(self, game):
        self.game = game
        self.tab = skills.TAB_CLASS
        self.selection = 0
        # Смещение большого отдельного окна древа навыков. Окно можно
        # перетаскивать за заголовок (см. handle_mouse_motion).
        self.panel_offset = [0, 0]
        self.dragging = False
        self._drag_last = (0, 0)

    # ── Окно древа ──
    def panel_size(self):
        """Размер отдельного крупного фрейма древа навыков."""
        w = min(config.SCREEN_W - 24,
                max(900, int(config.SCREEN_W * 0.88)))
        h = min(config.SCREEN_H - 24,
                max(560, int(config.SCREEN_H * 0.88)))
        return w, h

    def panel_rect(self):
        """Текущий прямоугольник окна с учётом перетаскивания."""
        w, h = self.panel_size()
        x = (config.SCREEN_W - w) // 2 + self.panel_offset[0]
        y = (config.SCREEN_H - h) // 2 + self.panel_offset[1]
        x = max(8, min(x, config.SCREEN_W - w - 8))
        y = max(8, min(y, config.SCREEN_H - h - 8))
        return (x, y, w, h)

    def start_drag(self, pos):
        self.dragging = True
        self._drag_last = pos
        return True

    def handle_mouse_motion(self, pos, pressed=None):
        """Перемещение окна при зажатой ЛКМ. True — кадр обработан."""
        if not self.dragging:
            return False
        if pressed is None:
            try:
                pressed = pygame.mouse.get_pressed()[0]
            except Exception:
                pressed = True
        if not pressed:
            self.dragging = False
            return False
        dx = pos[0] - self._drag_last[0]
        dy = pos[1] - self._drag_last[1]
        self._drag_last = pos
        if dx == 0 and dy == 0:
            return True
        w, h = self.panel_size()
        x = min(max(8, self.panel_rect()[0] + dx),
                config.SCREEN_W - w - 8)
        y = min(max(8, self.panel_rect()[1] + dy),
                config.SCREEN_H - h - 8)
        self.panel_offset[0] = x - (config.SCREEN_W - w) // 2
        self.panel_offset[1] = y - (config.SCREEN_H - h) // 2
        return True

    # ── Открытие/закрытие ──
    def open(self):
        self.dragging = False
        self.game.screen_mode = 'skills'
        self.clamp()

    def close(self):
        self.dragging = False
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
