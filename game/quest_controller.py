"""UI для списка квестов: принять, сдать."""
from core import config
import pygame
from core.data import QUESTS


class QuestController:
    def __init__(self, game):
        self.game = game
        self.selection = 0

    def open(self):
        self.selection = 0
        self.game.screen_mode = 'quests'

    def visible_quests(self):
        """[(key, state)] для отображения."""
        ql = self.game.quest_log
        result = []
        for key in QUESTS:
            if key in ql.turned_in:
                continue
            if key in ql.available:
                state = 'available'
            elif key in ql.active:
                state = 'active'
            elif key in ql.completed:
                state = 'completed'
            else:
                continue
            result.append((key, state))
        return result

    def activate(self, idx):
        items = self.visible_quests()
        if idx < 0 or idx >= len(items):
            return
        key, state = items[idx]
        if state == 'available':
            self.game.quest_log.accept(key, QUESTS[key]['count'])
            self.game.log(f"Квест взят: {QUESTS[key]['name']}",
                          (240, 220, 120))
        elif state == 'completed':
            if self.game.quest_log.turn_in(key, self.game.player, self.game):
                from audio import Audio
                Audio.play('level_up')  # звук сдачи
                q = QUESTS[key]
                self.game.log(
                    f"Квест сдан: {q['name']} (+{q['reward_gold']} з., "
                    f"+{q['reward_xp']} XP)", (120, 240, 120))
                self.game.total_quests_turned_in += 1
            self.selection = min(self.selection, len(self.visible_quests()) - 1)

    def handle_key(self, key, scancode=0):
        g = self.game
        if key in (pygame.K_ESCAPE, pygame.K_l):
            g.screen_mode = 'game'
            return
        items = self.visible_quests()
        n = len(items)
        if n == 0:
            return
        if key in (pygame.K_UP, pygame.K_w):
            self.selection = (self.selection - 1) % n
        elif key in (pygame.K_DOWN, pygame.K_s):
            self.selection = (self.selection + 1) % n
        elif key in (pygame.K_RETURN, pygame.K_SPACE):
            self.activate(self.selection)

    def button_rects(self):
        from core.config import (MODAL_MARGIN_X, MODAL_CONTENT_PAD,
                                 MODAL_TOP_PAD)
        panel_x = MODAL_MARGIN_X
        panel_w = config.SCREEN_W - MODAL_MARGIN_X * 2
        bw = panel_w - MODAL_CONTENT_PAD * 2
        bh = 76
        bx = panel_x + MODAL_CONTENT_PAD
        # Заголовок (56) + секция «Квесты» (32) + отступ (24) = 112 от верха панели
        by = MODAL_TOP_PAD + 56 + 32 + 24
        return [(i, (bx, by + i * (bh + 8), bw, bh))
                for i in range(len(self.visible_quests()))]

    def handle_mouse_click(self, pos):
        for idx, rect in self.button_rects():
            x, y, w, h = rect
            if x <= pos[0] <= x + w and y <= pos[1] <= y + h:
                self.selection = idx
                self.activate(idx)
                return True
        return False
