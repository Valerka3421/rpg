"""Экран диалога с NPC.
"""
from render.screens_renderer._common import *  # noqa: F401,F403


class DialogueMixin:
    """Экран диалога с NPC."""

    def draw_dialogue(self, game):
        self._modal_bg(game)
        cx0, cy0, cw, ch = game._modal_content_rect

        frame_color = (206, 166, 82) if game.dialogue.npc_type == 'elder' \
            else T.MERCH
        is_board = game.dialogue.npc_type == 'quest_board'
        if is_board:
            frame_color = (206, 170, 96)
        city_type = (game.dialogue.npc_type
                     if game.dialogue.npc_type in CITY_NPC_NAMES else None)
        CITY_FRAME = {
            'guard': (176, 186, 210), 'blacksmith': (206, 152, 88),
            'alchemist': (156, 126, 224), 'innkeeper': (214, 172, 112),
        }
        if city_type:
            frame_color = CITY_FRAME.get(city_type, frame_color)

        # ── Портрет ──
        portrait_size = 140
        portrait_x = cx0
        portrait_y = cy0 + 10
        T.rounded_rect(self.screen, (28, 30, 44),
                       (portrait_x, portrait_y, portrait_size, portrait_size),
                       radius=10, border_color=frame_color)
        T.rounded_rect(self.screen, (40, 42, 60),
                       (portrait_x + 4, portrait_y + 4,
                        portrait_size - 8, portrait_size - 8),
                       radius=8, border_color=(80, 80, 100))

        icon_size = 100
        icon_off = (portrait_size - icon_size) // 2
        if is_board:
            # Доска поручений — не торговец: рисуем её собственный тайл,
            # а не портрет и имя торговца.
            from render.tiles import Tiles
            board_img = pygame.transform.smoothscale(
                Tiles.get('quest_board', 0), (icon_size, icon_size))
            self.screen.blit(board_img, (portrait_x + icon_off,
                                         portrait_y + icon_off))
        elif game.dialogue.npc_type == 'elder':
            self.icons.draw_elder(portrait_x + icon_off, portrait_y + icon_off,
                                  size=icon_size)
        elif city_type:
            self.icons.draw_city_npc(city_type,
                                     portrait_x + icon_off,
                                     portrait_y + icon_off,
                                     size=icon_size)
        else:
            self.icons.draw_merchant(portrait_x + icon_off, portrait_y + icon_off,
                                     size=icon_size)

        # ── Имя в золотом бейдже ──
        if is_board:
            npc_name = "Доска поручений"
        elif city_type:
            npc_name = CITY_NPC_NAMES[city_type]
        else:
            npc_name = "Старейшина" if game.dialogue.npc_type == 'elder' else "Торговец"
        info_x = portrait_x + portrait_size + 24
        name_surf = T.render(npc_name, (28, 28, 40), T.FONT_H2)
        badge_w = name_surf.get_width() + 24
        badge_h = name_surf.get_height() + 12
        T.rounded_rect(self.screen, frame_color,
                       (info_x, portrait_y, badge_w, badge_h), radius=6)
        self.screen.blit(name_surf, (info_x + 12, portrait_y + 6))

        # ── Реплика с переносом ──
        greeting = game.dialogue.current_line or "..."
        words = greeting.split(' ')
        lines, cur = [], ''
        max_w = cx0 + cw - info_x
        for w in words:
            test = (cur + ' ' + w).strip()
            if T.FONT_BODY.size(test)[0] > max_w:
                lines.append(cur)
                cur = w
            else:
                cur = test
        if cur:
            lines.append(cur)
        text_y = portrait_y + badge_h + 14
        for line in lines:
            self.text(line, info_x, text_y, T.TEXT, font=T.FONT_BODY)
            text_y += 24

        # ── Кнопки снизу ──
        btn_h = 52
        btn_gap = 8
        n = len(game.dialogue.options)
        if n == 0:
            return
        total_h = n * btn_h + (n - 1) * btn_gap
        btn_y = cy0 + ch - total_h

        for i, opt in enumerate(game.dialogue.options):
            brect = (cx0, btn_y + i * (btn_h + btn_gap), cw, btn_h)
            selected = (i == game.dialogue.selection)
            self.draw_button_bg(brect, selected)
            color = (238, 222, 188) if selected else (200, 190, 170)
            self.text(opt[1], brect[0] + brect[2] // 2, brect[1] + brect[3] // 2,
                      color, font=T.FONT_H2, center=True)
            if selected:
                ax = brect[0] + 20
                ay = brect[1] + brect[3] // 2
                pygame.draw.polygon(self.screen, (206, 166, 82),
                                    [(ax, ay - 8), (ax + 14, ay), (ax, ay + 8)])

    # ════════════════════════ КВЕСТЫ ════════════════════════

