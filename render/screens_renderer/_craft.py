"""Экран крафта: draw_craft, мини-игра качества.
"""
from render.screens_renderer._common import *  # noqa: F401,F403


class CraftMixin:
    """Экран крафта: draw_craft, мини-игра качества."""

    def draw_craft(self, game):
        self._modal_bg(game)
        cx0, cy0, cw, ch = game._modal_content_rect
        from core import skills as sk
        from core.config import HAMMER_SIZE
        game._ui_hits['craft'] = {'hammer': [], 'tabs': [], 'qty': []}

        self.text("КРАФТ", cx0, cy0, (214, 178, 98), font=T.FONT_H1)
        self.text(f"Уровень {game.player.level}",
                  self._modal_right(game), cy0 + 8, T.TEXT, align_right=True)

        # ── Вкладки: все крафты и три навыка ──
        tab_y = cy0 + 44
        tx = cx0
        current_tab = getattr(game.craft, 'tab', 'all')
        for tab_key, label in game.craft_tabs():
            active = current_tab == tab_key
            tw = T.render(label, T.TEXT, T.FONT_SMALL).get_width() + 22
            rect = (tx, tab_y, tw, 28)
            bg = (66, 54, 38) if active else (30, 27, 22)
            border = (206, 166, 82) if active else (78, 66, 48)
            T.rounded_rect(self.screen, bg, rect, radius=6,
                           border_color=border)
            self.text(label, tx + 11, tab_y + 5,
                      T.GOLD if active else T.TEXT_DIM, font=T.FONT_SMALL)
            game._ui_hits['craft']['tabs'].append((tab_key, rect))
            tx += tw + 6

        y = tab_y + 38

        # ── Прогресс ветки и бонус качества ──
        skill_tab = current_tab if current_tab != 'all' else None
        if skill_tab:
            level, need, ratio = sk.tab_progress(game.player, skill_tab)
            quality_bonus = sk.craft_quality_bonus(game.player)
            T.section(self.screen, cx0, y, cw,
                      sk.TAB_NAMES.get(skill_tab, skill_tab))
            y += 28
            T.bar(self.screen, (cx0, y, 260, 12), ratio, (120, 180, 110))
            progress = f"Ур. {level}"
            if need:
                progress += f" · {sk.craft_xp(game.player, skill_tab)}/{need}"
            self.text(progress, cx0 + 272, y - 2, T.TEXT_DIM,
                      font=T.FONT_SMALL)
            if quality_bonus:
                self.text(f"качество +{quality_bonus}",
                          self._modal_right(game), y - 2, (240, 210, 90),
                          font=T.FONT_SMALL, align_right=True)
            y += 28
        else:
            y = T.section(self.screen, cx0, y, cw, "Все рецепты")
            y += 6

        if getattr(game.craft, 'minigame_active', False):
            self._draw_minigame(game, cx0, y, cw)
            return

        recipes = game.craft_page_items()
        offset = game.craft.page_offset()
        if not recipes:
            self.text("  (нет рецептов)", cx0, y, T.TEXT_MUTED,
                      font=T.FONT_SMALL)
        for i, entry in enumerate(recipes):
            key, it, recipe, can, category, _ri, out_count = entry
            lvl_req = it.get('level_req', 1)
            lvl_ok = game.player.level >= lvl_req
            skill_req = int(it.get('skill_req', 1))
            skill_ok = sk.craft_level(game.player, category) >= skill_req
            can_full = can and lvl_ok and skill_ok
            color = T.HEAL if can_full else T.TEXT_MUTED
            row_y = y

            self.icons.draw_item(key, cx0, row_y, size=28)
            name = item_display_name(key)
            if out_count > 1:
                name += f" ×{out_count}"
            self.text(name, cx0 + 36, row_y + 4, color)
            req_text = f"Ур.{lvl_req}"
            if skill_req > 1:
                req_text += f" · навык {skill_req}"
            self.text(req_text, cx0 + 300, row_y + 4,
                      T.GOLD if (lvl_ok and skill_ok) else T.HP_CRIT,
                      font=T.FONT_SMALL)
            y += 22

            ix = cx0 + 36
            for mat, cnt in recipe.items():
                have = game.player.inventory.get(mat, 0)
                ok = have >= cnt
                mat_name = ITEMS.get(mat, {}).get('name', mat)
                w = self.text(f"{mat_name} {have}/{cnt}", ix, y,
                              T.HEAL if ok else T.HP_CRIT, font=T.FONT_TINY)
                ix += w + 14

            # Молоточек — правый край, не под крестиком
            hx = self._modal_right(game) - HAMMER_SIZE + 4
            hy = row_y
            mx, my = game.mouse_pos

            # ── Партия конкретного предмета: [-] ×N [+] слева от молота ──
            if it.get('type') not in rules.EQUIP_TYPES:
                qty = game.craft.quantity_for(key, it)
                ctrl_w, btn_w = 96, 24
                qx = hx - 10 - ctrl_w
                qy = row_y + (HAMMER_SIZE - 26) // 2
                minus_rect = (qx, qy, btn_w, 26)
                plus_rect = (qx + ctrl_w - btn_w, qy, btn_w, 26)
                for rect, label in ((minus_rect, '-'), (plus_rect, '+')):
                    qhover = (rect[0] <= mx <= rect[0] + rect[2]
                              and rect[1] <= my <= rect[1] + rect[3])
                    bg_q = (52, 45, 33) if qhover else (30, 27, 22)
                    T.rounded_rect(self.screen, bg_q, rect, radius=5,
                                   border_color=(78, 66, 48))
                    self.text(label, rect[0] + rect[2] // 2,
                              rect[1] + rect[3] // 2 - 1,
                              T.GOLD, font=T.FONT_BODY, center=True)
                self.text(f"×{qty}", qx + ctrl_w // 2, qy + 5, T.TEXT,
                          font=T.FONT_SMALL, center=True)
                game._ui_hits['craft']['qty'].append(
                    (key, minus_rect, plus_rect, it))

            hover = (hx <= mx <= hx + HAMMER_SIZE
                     and hy <= my <= hy + HAMMER_SIZE)
            bg_h = (60, 90, 60) if (can_full and hover) else \
                (40, 70, 40) if can_full else (40, 40, 50)
            border_h = (120, 170, 104) if can_full else (78, 66, 48)
            T.rounded_rect(self.screen, bg_h, (hx, hy, HAMMER_SIZE, HAMMER_SIZE),
                           radius=6, border_color=border_h)
            self.icons.draw_toolbar_icon('craft', hx + 2, hy + 2,
                                         size=HAMMER_SIZE - 4,
                                         hover=can_full and hover)
            game._ui_hits['craft']['hammer'].append(
                (offset + i, (hx, hy, HAMMER_SIZE, HAMMER_SIZE)))
            y += 26

        # ── Страницы ──
        total = game.craft_pages_count()
        if total > 1:
            page = game.craft.page
            py = cy0 + ch - 34
            prev_rect = (cx0, py, 70, 26)
            next_rect = (cx0 + 84, py, 70, 26)
            # Треугольники вместо «◀/▶»: в шрифте нет этих глифов,
            # и они рисовались пустыми квадратами.
            for rect, enabled, direction in (
                    (prev_rect, page > 0, -1),
                    (next_rect, page < total - 1, 1)):
                T.rounded_rect(self.screen, (30, 27, 22), rect,
                               radius=5, border_color=(78, 66, 48))
                cx = rect[0] + rect[2] // 2
                cyy = rect[1] + rect[3] // 2
                col = T.GOLD if enabled else T.TEXT_MUTED
                if direction < 0:
                    pts = [(cx + 4, cyy - 6), (cx + 4, cyy + 6), (cx - 5, cyy)]
                else:
                    pts = [(cx - 4, cyy - 6), (cx - 4, cyy + 6), (cx + 5, cyy)]
                pygame.draw.polygon(self.screen, col, pts)
            self.text(f"Стр. {page + 1}/{total}", cx0 + 170, py + 4,
                      T.TEXT_DIM, font=T.FONT_SMALL)
            game._ui_hits['craft']['prev'] = prev_rect
            game._ui_hits['craft']['next'] = next_rect

        hints = "Q/E — вкладки · ←/→ — страницы · 1–8 — создать · K/Esc — закрыть"
        if getattr(game.craft, 'minigame_active', False):
            hints = "Создание запускает мини-игру качества. " + hints
        self.text(hints, cx0 + cw // 2, cy0 + ch - 20, T.TEXT_MUTED,
                  font=T.FONT_SMALL, center=True)


    def _draw_minigame(self, game, x, y, cw):
        mg = game.craft.minigame
        item_key = mg['key'] if mg else ''
        self.text("МИНИ-ИГРА КАЧЕСТВА", x, y, T.TITLE, font=T.FONT_H2)
        y += 30
        self.text(f"Создаётся: {item_display_name(item_key)}", x, y,
                  T.TEXT, font=T.FONT_BODY)
        qty = int(mg.get('quantity', 1)) if mg else 1
        if qty > 1:
            self.text(f"Партия: ×{qty}", x + cw - 20, y, T.GOLD,
                      font=T.FONT_BODY, align_right=True)
        y += 30
        bar = (x, y, cw - 20, 26)
        T.rounded_rect(self.screen, (28, 26, 20), bar, radius=6,
                       border_color=T.PANEL_BORDER)
        # Зелёная зона точности по центру
        zx = x + int((cw - 20) * 0.35)
        zw = int((cw - 20) * 0.30)
        T.rounded_rect(self.screen, (52, 96, 52), (zx, y + 2, zw, 22),
                       radius=4, border_color=(120, 200, 120))
        pos = x + int((cw - 20) * mg['pos']) if mg else x
        pygame.draw.line(self.screen, (250, 240, 180),
                         (pos, y - 6), (pos, y + 32), 3)
        y += 40
        self.text("Пробел / клик — остановить в зелёной зоне. Esc — отмена.",
                  x + cw // 2, y, T.TEXT_DIM, font=T.FONT_SMALL, center=True)

    # ════════════════════════ КОНСОЛЬ РАЗРАБОТЧИКА ════════════════════════

