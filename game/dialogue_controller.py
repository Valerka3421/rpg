"""Диалоги с NPC: Старейшина, Торговец."""
from core import config
import pygame


# Циклические реплики при выборе «Поговорить о мире»
LORE_ELDER = [
    "Приветствую, путник. Тёмная бездна внизу ждёт героя.",
    "Говорят, на глубине 5 сидит Король гоблинов. Его голова стоит дорого.",
    "На 10-й глубине — Лич. Он крадёт души у тех, кто не готов.",
    "Древний дракон на дне бездны. Только герой сможет его одолеть.",
    "Собирай кости, кожу, чешую — из них кузнецы делают оружие.",
    "Крысы в лесу ядовиты. Осторожнее с ними.",
]

LORE_MERCHANT = [
    "Свежий товар! Лучшие цены в подземелье!",
    "Книги — наше всё. Двойной удар — лучшая инвестиция.",
    "Зелья дешевеют с глубиной. Копи на большие.",
    "Редкие предметы сверкают цветом. Красный — эпический!",
    "Продавай мне ненужное — я дам честную цену.",
]


class DialogueController:
    def __init__(self, game):
        self.game = game
        self.npc_type = None
        self.options = []
        self.selection = 0
        self.current_line = ""
        self.lore_index = 0

    def open(self, npc_type):
        self.npc_type = npc_type
        self.selection = 0
        self.lore_index = 0

        # Квесты этого NPC становятся доступны при первом разговоре.
        # В мультиплеере квестов нет: quest_log отсутствует, и пункт
        # «Квесты» просто не показывается.
        quest_log = getattr(self.game, 'quest_log', None)
        if quest_log is not None:
            from core.data import quests_by_giver
            quest_log.add_available(quests_by_giver(npc_type))

        if npc_type == 'elder':
            self.options = [('talk', 'Поговорить о мире')]
            if quest_log is not None:
                self.options.append(('quests', 'Квесты'))
            self.options.append(('close', 'Уйти'))
            self.current_line = LORE_ELDER[0]
        elif npc_type == 'merchant':
            self.options = [('shop', 'Торговля')]
            if quest_log is not None:
                self.options.append(('quests', 'Квесты'))
            self.options.append(('close', 'Уйти'))
            self.current_line = LORE_MERCHANT[0]
        elif npc_type == 'quest_board':
            self.board_quests = []
            self._refresh_board()
            self._rebuild_options()
            self.current_line = "Доска поручений. Выберите задание."
        self.game.screen_mode = 'dialogue'

    def activate(self):
        if not self.options:
            return
        choice = self.options[self.selection][0]
        if choice == 'close':
            self.game.screen_mode = 'game'
        elif choice == 'shop':
            self.game.shop.open()
        elif choice == 'quests':
            self.game.quests.open()
        elif choice == 'talk':
            self._talk_lore()
        elif choice.startswith('board_accept_'):
            idx = int(choice.split('_')[-1])
            if idx < len(self.board_quests):
                q = self.board_quests[idx]
                if self.game.quest_log.accept_generated(q):
                    self.game.log(f"Взято: {q['name']}", (240, 220, 120))
                    self._refresh_board()
                    self._rebuild_options()
                else:
                    self.game.log("Уже взято.", (110, 110, 130))

    def _refresh_board(self):
        from core.data import generate_board_quests
        board = getattr(self.game, 'quest_board', None)
        if board is None:
            self.board_quests = []
            return
        qlog = self.game.quest_log
        dmin = getattr(board, 'board_depth_min', 0)
        dmax = getattr(board, 'board_depth_max', 4)
        seed = (hash((dmin, dmax, board.x, board.y)) & 0xFFFF)
        all_q = generate_board_quests(dmin, dmax, count=3, base_seed=seed)
        self.board_quests = [q for q in all_q
                             if q['id'] not in qlog.active
                             and q['id'] not in qlog.turned_in]

    def _rebuild_options(self):
        self.options = []
        for i, q in enumerate(self.board_quests):
            self.options.append((f'board_accept_{i}', f"Взять: {q['name']}"))
        self.options.append(('close', 'Уйти'))
        self.selection = min(self.selection, max(0, len(self.options) - 1))


    def _talk_lore(self):
        """Прокрутить реплику в панели диалога."""
        if self.npc_type == 'elder':
            lines = LORE_ELDER
        else:
            lines = LORE_MERCHANT
        self.lore_index = (self.lore_index + 1) % len(lines)
        self.current_line = lines[self.lore_index]

    def handle_key(self, key, scancode=0):
        if not self.options:
            return
        n = len(self.options)
        if key in (pygame.K_UP, pygame.K_w):
            self.selection = (self.selection - 1) % n
        elif key in (pygame.K_DOWN, pygame.K_s):
            self.selection = (self.selection + 1) % n
        elif key in (pygame.K_RETURN, pygame.K_SPACE):
            self.activate()
        elif key == pygame.K_ESCAPE:
            self.game.screen_mode = 'game'

    def button_rects(self):
        from core.config import (MODAL_MARGIN_X, MODAL_MARGIN_Y,
                                 MODAL_CONTENT_PAD, MODAL_TOP_PAD)
        panel_x = MODAL_MARGIN_X
        panel_w = config.SCREEN_W - MODAL_MARGIN_X * 2
        panel_h = config.SCREEN_H - MODAL_MARGIN_Y * 2
        content_x = panel_x + MODAL_CONTENT_PAD
        content_w = panel_w - MODAL_CONTENT_PAD * 2
        content_top = MODAL_MARGIN_Y + MODAL_TOP_PAD
        content_h = panel_h - MODAL_TOP_PAD - 20

        btn_h = 52
        btn_gap = 8
        n = len(self.options)
        if n == 0:
            return []
        total_h = n * btn_h + (n - 1) * btn_gap
        btn_y = content_top + content_h - total_h
        return [(i, (content_x, btn_y + i * (btn_h + btn_gap),
                     content_w, btn_h)) for i in range(n)]

    def handle_mouse_click(self, pos):
        for idx, rect in self.button_rects():
            x, y, w, h = rect
            if x <= pos[0] <= x + w and y <= pos[1] <= y + h:
                self.selection = idx
                self.activate()
                return True
        return False