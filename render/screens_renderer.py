"""Экраны поверх игры: статы, инвентарь, магазин, крафт, диалог, квесты, статистика."""
from core import config
import pygame
from core.config import INVENTORY_PAGE_SIZE, SHOP_PAGE_SIZE
from core.data import (ITEMS, parse_item_key, item_display_name,
                       item_display_color, item_sell_price,
                       PERK_INFO, PERK_BOOK_MAP, QUESTS)
from render.theme import Theme as T
from render.base_renderer import BaseRenderer


class ScreensRenderer(BaseRenderer):
    def __init__(self, screen, icons):
        super().__init__(screen)
        self.icons = icons

    def _modal_bg(self, game):
        """Затемнение + панель + крестик + возврат rect контента."""
        from core.config import (MODAL_TOP_PAD, MODAL_CLOSE_SIZE,
                                 MODAL_CLOSE_PAD, MODAL_CONTENT_PAD)
        ov = pygame.Surface((config.SCREEN_W, config.SCREEN_H), pygame.SRCALPHA)
        ov.fill((6, 5, 4, 165))
        self.screen.blit(ov, (0, 0))

        rect = game._modal_panel_rect()
        panel = pygame.Surface((rect[2], rect[3]), pygame.SRCALPHA)
        pygame.draw.rect(panel, (30, 26, 21, 238), panel.get_rect(), border_radius=10)
        pygame.draw.rect(panel, (78, 66, 48, 255), panel.get_rect(),
                         width=2, border_radius=10)
        self.screen.blit(panel, (rect[0], rect[1]))

        # ── Крестик ──
        size = MODAL_CLOSE_SIZE
        pad = MODAL_CLOSE_PAD
        xx = rect[0] + rect[2] - size - pad
        yy = rect[1] + pad
        T.rounded_rect(self.screen, (54, 26, 22), (xx, yy, size, size),
                       radius=6, border_color=(160, 68, 58))
        cx, cy = xx + size // 2, yy + size // 2
        r = 9
        pygame.draw.line(self.screen, (200, 74, 64),
                         (cx - r, cy - r), (cx + r, cy + r), 3)
        pygame.draw.line(self.screen, (200, 74, 64),
                         (cx + r, cy - r), (cx - r, cy + r), 3)

        game._modal_close_rect = (xx, yy, size, size)
        game._modal_content_rect = (
            rect[0] + MODAL_CONTENT_PAD,
            rect[1] + MODAL_TOP_PAD,
            rect[2] - MODAL_CONTENT_PAD * 2 - size - pad,  # правее крестика не лезем
            rect[3] - MODAL_TOP_PAD - 20,
        )
        return rect

    def _modal_right(self, game):
        from core.config import (MODAL_CONTENT_PAD, MODAL_CLOSE_SIZE,
                                 MODAL_CLOSE_PAD)
        rect = game._modal_panel_rect()
        return rect[0] + rect[2] - MODAL_CONTENT_PAD - MODAL_CLOSE_SIZE - MODAL_CLOSE_PAD - 6

    # ════════════════════════ СТАТЫ ════════════════════════
    def draw_stats(self, game):
        self._modal_bg(game)
        cx0, cy0, cw, ch = game._modal_content_rect
        from core.config import PLUS_SIZE
        from core.data import PERK_INFO, PERK_BOOK_MAP
        p = game.player
        game._ui_hits['stats'] = {'plus': []}

        class_name = getattr(p, 'class_name', 'Приключенец')
        self.text("ХАРАКТЕРИСТИКИ", cx0, cy0, (214, 178, 98), font=T.FONT_H1)
        self.text(f"{class_name} · Уровень {p.level}",
                  self._modal_right(game), cy0 + 8, T.TEXT, align_right=True)
        y = cy0 + 56
        if p.free_points > 0:
            T.badge(self.screen, cx0, y, f"● {p.free_points} очков",
                    color=T.GOLD, bg=(50, 40, 15), font=T.FONT_SMALL)
        y += 32
        y = T.section(self.screen, cx0, y, cw, "Характеристики")

        STAT_FIELDS = ['str', 'agi', 'vit', 'luk', 'df_stat']
        rows = [
            ('Сила', 'STR', p.str, '+1 ATK за 2 очка', self._d_atk(p)),
            ('Ловкость', 'AGI', p.agi, '+1.5% уклонение', self._d_dodge(p)),
            ('Выносливость', 'VIT', p.vit, '+2 HP за очко', self._d_hp(p)),
            ('Удача', 'LUK', p.luk, '+2% крит', self._d_crit(p)),
            ('Защита', 'DEF', p.df_stat, '+1 DEF за 2 очка', self._d_def(p)),
        ]
        for i, (name, abbr, val, gain, detail) in enumerate(rows):
            # ── Кнопка-значение слева ──
            btn_size = PLUS_SIZE + 4
            btn_x = cx0
            btn_y = y - 4
            can_alloc = p.free_points > 0
            bg = (40, 64, 34) if can_alloc else (45, 40, 32)
            border = (120, 170, 104) if can_alloc else (78, 66, 48)
            T.rounded_rect(self.screen, bg,
                           (btn_x, btn_y, btn_size, btn_size),
                           radius=5, border_color=border)
            self.text(str(val), btn_x + btn_size // 2, btn_y + btn_size // 2,
                      (214, 232, 196) if can_alloc else (200, 190, 170),
                      font=T.FONT_BODY, center=True)
            if can_alloc:
                game._ui_hits['stats']['plus'].append(
                    (STAT_FIELDS[i], (btn_x, btn_y, btn_size, btn_size)))

            # ── Номер + название справа от кнопки ──
            text_x = cx0 + btn_size + 16
            self.text(f"{name} ({abbr})", text_x, y, T.TEXT)
            self.text(gain, text_x + 240, y, (134, 180, 116), font=T.FONT_SMALL)
            y += 22
            self.text(f"  └ {detail}", text_x, y, T.TEXT_MUTED, font=T.FONT_TINY)
            y += 26

        y += 8
        y = T.section(self.screen, cx0, y, cw, "Перки")
        active = [f for f in PERK_INFO if getattr(p, f, False)]
        if active:
            for flag in active:
                book_key = PERK_BOOK_MAP[flag]
                name, desc, _ = PERK_INFO[flag]
                self.icons.draw_item(book_key, cx0 + 20, y - 2, size=24)
                self.text(name, cx0 + 52, y, (198, 168, 118), font=T.FONT_BODY)
                self.text(f"— {desc}", cx0 + 52 + T.FONT_BODY.size(name)[0] + 8,
                          y + 2, T.TEXT_MUTED, font=T.FONT_TINY)
                y += 28
        else:
            self.text("  (нет — покупай книги у торговца)",
                      cx0, y, T.TEXT_MUTED, font=T.FONT_SMALL)
            y += 24

        y = T.section(self.screen, cx0, y, cw, "Экипировка")
        from core.data import ITEMS, parse_item_key, item_display_name, item_display_color
        for label, key, stat in (("Оружие", p.weapon, "ATK"),
                                 ("Броня", p.armor, "DEF"),
                                 ("Шлем", getattr(p, 'helmet', None), "DEF"),
                                 ("Поножи", getattr(p, 'leggings', None), "DEF")):
            if key:
                base, lvl = parse_item_key(key)
                it = ITEMS.get(base, {})
                self.icons.draw_item(key, cx0, y, size=28)
                self.text(f"{label}:  {item_display_name(key)}",
                          cx0 + 40, y + 6, item_display_color(key))
                self.text(f"+{it.get('value', 0) + lvl} {stat}",
                          self._modal_right(game), y + 6, T.TEXT_DIM,
                          font=T.FONT_SMALL, align_right=True)
            else:
                self.text(f"  {label}: —", cx0, y + 6, T.TEXT_MUTED,
                          font=T.FONT_SMALL)
            y += 34

    def _d_atk(self, p):
        base = 2; str_part = p.str // 2
        if p.weapon:
            b, l = parse_item_key(p.weapon)
            weapon = ITEMS.get(b, {}).get('value', 0) + l
        else:
            weapon = 0
        return f"ATK = {base} + {str_part} STR + {weapon} = {base + str_part + weapon}"

    def _d_dodge(self, p):
        raw = p.agi * 1.5; final = int(p.dodge * 100)
        cap = " (кап 30%)" if raw > 30 else ""
        bonus = int((p._w_dodge_bonus + p._a_dodge_bonus) * 100)
        extra = f" + {bonus}% экип" if bonus else ""
        return f"Уклонение = {final}% (1.5% × AGI {p.agi} = {raw:.0f}%{cap}{extra})"

    def _d_hp(self, p):
        extra = 0
        if p.armor:
            b, _ = parse_item_key(p.armor)
            extra = ITEMS.get(b, {}).get('hp_bonus', 0)
        return f"HP = 15 + {p.vit * 2} VIT + {extra} = {p.max_hp}"

    def _d_crit(self, p):
        raw = p.luk * 2; final = int(p.crit * 100)
        cap = " (кап 40%)" if raw > 40 else ""
        bonus = int(p._w_crit_bonus * 100)
        extra = f" + {bonus}% оружие" if bonus else ""
        return f"Крит = {final}% (2% × LUK {p.luk} = {raw:.0f}%{cap}{extra})"

    def _d_def(self, p):
        df_part = p.df_stat // 2
        if p.armor:
            b, l = parse_item_key(p.armor)
            armor = ITEMS.get(b, {}).get('value', 0) + l
        else:
            armor = 0
        return f"DEF = 0 + {df_part} DEF + {armor} = {df_part + armor}"

    # ════════════════════════ ИНВЕНТАРЬ ════════════════════════
    @staticmethod
    def _in_rect(rect, pos):
        x, y, w, h = rect
        return x <= pos[0] <= x + w and y <= pos[1] <= y + h

    def draw_inventory(self, game):
        self._modal_bg(game)
        cx0, cy0, cw, ch = game._modal_content_rect
        from core.config import INVENTORY_COLS, INVENTORY_ROWS
        p = game.player
        game._ui_hits['inventory'] = {'items': [], 'equip': [], 'pages': []}
        mx, my = getattr(game, 'mouse_pos', (-1, -1))
        hovered = None

        self.text("ИНВЕНТАРЬ", cx0, cy0, (214, 178, 98), font=T.FONT_H1)
        self.text(f"${p.gold}", self._modal_right(game), cy0 + 8, T.GOLD,
                  align_right=True)
        self.text("Q/E — страницы", self._modal_right(game), cy0 + 34,
                  T.TEXT_MUTED, font=T.FONT_SMALL, align_right=True)
        y = cy0 + 56
        y = T.section(self.screen, cx0, y, cw, "Снаряжение")

        row_w = cw
        equip_rows = (
            ('weapon', "Оружие", "U", "ATK"),
            ('armor', "Броня", "V", "DEF"),
            ('helmet', "Шлем", "B", "DEF"),
            ('leggings', "Поножи", "N", "DEF"),
        )
        for slot, label, hotkey, stat in equip_rows:
            key = getattr(p, slot, None)
            if key:
                rect = (cx0, y, row_w, 28)
                if self._in_rect(rect, (mx, my)):
                    T.rounded_rect(self.screen, T.ROW_HOVER,
                                   (cx0 - 6, y - 2, row_w + 12, 32),
                                   radius=6, border_color=T.ROW_BORDER)
                    hovered = ('equip', key)
                self._draw_equip(cx0 + 4, y, label, key, hotkey, stat)
                game._ui_hits['inventory']['equip'].append((slot, rect))
            else:
                self.text(f"  {label}: —", cx0, y + 4, T.TEXT_MUTED,
                          font=T.FONT_SMALL)
            y += 32
        y += 12

        y = T.section(self.screen, cx0, y, cw, "Рюкзак")
        items = sorted(p.inventory.items())
        total = max(1, (len(items) + INVENTORY_PAGE_SIZE - 1)
                    // INVENTORY_PAGE_SIZE)
        page = min(game.inv_page, total - 1)
        game.inv_page = page
        start = page * INVENTORY_PAGE_SIZE
        page_items = items[start:start + INVENTORY_PAGE_SIZE]

        # Сетка: 3×3 предметов на страницу. Ячейки одинакового размера,
        # поэтому предметы не «прыгают» при смене содержимого.
        gap = 12
        cols, rows = INVENTORY_COLS, INVENTORY_ROWS
        avail_h = (cy0 + ch) - y - 46
        cell_w = min(220, (cw - gap * (cols - 1)) // cols)
        cell_h = max(88, min(152, (avail_h - gap * (rows - 1)) // rows))
        grid_w = cols * cell_w + gap * (cols - 1)
        grid_x = cx0 + (cw - grid_w) // 2

        for idx in range(INVENTORY_PAGE_SIZE):
            row, col = divmod(idx, cols)
            rect = (grid_x + col * (cell_w + gap),
                    y + row * (cell_h + gap), cell_w, cell_h)
            if idx < len(page_items):
                key, count = page_items[idx]
                hover = self._in_rect(rect, (mx, my))
                self._draw_inv_slot(key, count, rect, p.level, game, hover)
                game._ui_hits['inventory']['items'].append((key, rect))
                if hover:
                    hovered = ('item', (key, count))
            else:
                T.rounded_rect(self.screen, (25, 22, 18), rect, radius=8,
                               border_color=(48, 42, 32))

        grid_bottom = y + rows * cell_h + (rows - 1) * gap
        if not page_items:
            self.text("Рюкзак пуст", grid_x + grid_w // 2,
                      y + (rows * cell_h + (rows - 1) * gap) // 2,
                      T.TEXT_MUTED, font=T.FONT_BODY, center=True)

        # ── Страницы ──
        page_y = grid_bottom + 10
        bw, bh = 38, 28
        prev_rect = (grid_x, page_y, bw, bh)
        next_rect = (grid_x + grid_w - bw, page_y, bw, bh)
        self._draw_page_button("‹", prev_rect, page > 0, page > 0 and
                               self._in_rect(prev_rect, (mx, my)))
        self._draw_page_button("›", next_rect, page < total - 1,
                               page < total - 1 and
                               self._in_rect(next_rect, (mx, my)))
        self.text(f"Стр. {page + 1}/{total}", cx0 + cw // 2, page_y + 4,
                  T.TEXT_DIM, font=T.FONT_SMALL, center=True)
        game._ui_hits['inventory']['pages'] = [
            ('prev', prev_rect), ('next', next_rect),
        ]

        if hovered:
            self._draw_inventory_tooltip(game, hovered, (mx, my))

    def _draw_inv_slot(self, key, count, rect, player_level, game, hover):
        x, y, w, h = rect
        bg = (58, 49, 35) if hover else (32, 28, 22)
        border = T.ROW_BORDER if hover else T.PANEL_BORDER
        T.rounded_rect(self.screen, bg, rect, radius=8, border_color=border)

        icon_size = max(40, min(64, h - 50))
        self.icons.draw_item(key, x + (w - icon_size) // 2, y + 8,
                             size=icon_size)

        base, _ = parse_item_key(key)
        it = ITEMS.get(base, {})
        lvl_req = it.get('level_req', 1)
        color = item_display_color(key)
        if lvl_req > 1 and player_level < lvl_req:
            color = T.HP_CRIT
        from core.data import class_can_use
        if not class_can_use(game.player.class_key, key)[0]:
            color = T.HP_CRIT
        label = self.fit_text(item_display_name(key), w - 14, T.FONT_SMALL)
        self.text(label, x + w // 2, y + h - 22, color,
                  font=T.FONT_SMALL, center=True)

        if count > 1:
            badge = T.render(f"x{count}", (255, 240, 190), T.FONT_TINY)
            bw, bh = badge.get_width() + 8, badge.get_height() + 2
            bx, by = x + w - bw - 5, y + 5
            T.rounded_rect(self.screen, (44, 36, 22), (bx, by, bw, bh),
                           radius=6, border_color=(120, 96, 52))
            self.screen.blit(badge, (bx + 4, by + 1))

    def _draw_page_button(self, label, rect, enabled, hover):
        bg = T.ROW_HOVER if enabled and hover else (30, 27, 21)
        border = T.ROW_BORDER if enabled else T.PANEL_BORDER_DARK
        T.rounded_rect(self.screen, bg, rect, radius=6, border_color=border)
        self.text(label, rect[0] + rect[2] // 2, rect[1] + rect[3] // 2 - 2,
                  T.TEXT if enabled else T.TEXT_MUTED,
                  font=T.FONT_BODY, center=True)

    def _draw_inventory_tooltip(self, game, hovered, pos):
        kind, payload = hovered
        if kind == 'equip':
            key, count = payload, 1
        else:
            key, count = payload
        base, _ = parse_item_key(key)
        it = ITEMS.get(base, {})

        lines = [(item_display_name(key), T.TITLE_LIGHT, T.FONT_BODY)]
        if count > 1:
            lines.append((f"Количество: {count}", T.TEXT_DIM, T.FONT_SMALL))
        desc = it.get('desc', '')
        if desc:
            lines.append((desc, T.TEXT_MUTED, T.FONT_TINY))
        lvl_req = it.get('level_req', 1)
        if lvl_req > 1:
            ok = game.player.level >= lvl_req
            lines.append((f"Требуется уровень {lvl_req}",
                          T.TEXT_DIM if ok else T.HP_CRIT, T.FONT_SMALL))
        from core.data import class_can_use
        can_use, why = class_can_use(game.player.class_key, key)
        if not can_use:
            lines.append((f"Класс не может: {why}", T.HP_CRIT, T.FONT_SMALL))
        if kind == 'equip':
            stat = "ATK" if it.get('type') == 'weapon' else "DEF"
            bonus = it.get('value', 0) + parse_item_key(key)[1]
            if bonus:
                lines.append((f"+{bonus} {stat}", T.HEAL, T.FONT_SMALL))
        elif it.get('type') in ('weapon', 'armor', 'helmet', 'leggings'):
            deltas = self.compare_item(game, key, it['type'])
            dt, dc = self.format_delta(deltas)
            if dt:
                lines.append((dt, dc, T.FONT_SMALL))

        rendered = []
        max_w = 0
        total_h = 0
        for text, color, font in lines:
            text = self.fit_text(text, 400, font)
            surf = T.render(text, color, font)
            rendered.append((surf, total_h))
            max_w = max(max_w, surf.get_width())
            total_h += surf.get_height() + 4

        pad = 10
        w = max_w + pad * 2
        h = total_h + pad * 2 - 4
        x = min(max(8, pos[0] + 18), config.SCREEN_W - w - 8)
        y = min(max(8, pos[1] + 18), config.SCREEN_H - h - 8)
        T.rounded_rect(self.screen, (18, 16, 13), (x, y, w, h), radius=8,
                       border_color=T.ROW_BORDER)
        for surf, dy in rendered:
            self.screen.blit(surf, (x + pad, y + pad + dy))

    def _draw_equip(self, x, y, label, key, unequip_key=None, stat=""):
        if key:
            it = ITEMS.get(parse_item_key(key)[0], {})
            self.icons.draw_item(key, x, y, size=28)
            self.text(f"{label}:  {item_display_name(key)}",
                      x + 36, y + 6, item_display_color(key))
            self.text(f"+{it.get('value', 0) + parse_item_key(key)[1]} {stat}",
                      x + 320, y + 6, T.TEXT_DIM, font=T.FONT_SMALL)
            desc = it.get('desc', '')
            if desc:
                self.text(desc, x + 36, y + 26, T.TEXT_MUTED, font=T.FONT_TINY)
        else:
            self.text(f"{label}: —", x + 4, y + 6, T.TEXT_MUTED, font=T.FONT_SMALL)

    # ════════════════════════ МАГАЗИН ════════════════════════
    def draw_shop(self, game):
        self._modal_bg(game)
        cx0, cy0, cw, ch = game._modal_content_rect
        game._ui_hits['shop'] = {'rows': []}
        self.text("ТОРГОВЕЦ", cx0, cy0, T.MERCH, font=T.FONT_H1)
        mode = "ПОКУПКА" if game.shop_submode == 'buy' else "ПРОДАЖА"
        T.badge(self.screen, cx0 + 240, cy0 + 8, mode,
                color=T.BG if game.shop_submode == 'buy' else T.TEXT,
                bg=T.MERCH if game.shop_submode == 'buy' else T.HP_CRIT,
                font=T.FONT_BODY)
        self.text(f"Глубина {game.depth}", self._modal_right(game), cy0 + 8,
                  T.TEXT_DIM, align_right=True)
        self.text(f"${game.player.gold}", self._modal_right(game), cy0 + 34,
                  T.GOLD, align_right=True)
        y = cy0 + 66
        if game.shop_submode == 'buy':
            y = T.section(self.screen, cx0, y, cw, "Товары")
            self._draw_shop_buy(game, cx0, y, cw)
        else:
            y = T.section(self.screen, cx0, y, cw, "Продать")
            self._draw_shop_sell(game, cx0, y, cw)
        self.text("S — режим · R — обновить · Q/E — страницы",
                  cx0 + cw // 2, cy0 + ch - 22, T.TEXT_MUTED,
                  font=T.FONT_SMALL, center=True)

    def _draw_shop_buy(self, game, x0, y, cw):
        from core.config import SHOP_PAGE_SIZE
        stock = game.shop_stock
        total = max(1, (len(stock) + SHOP_PAGE_SIZE - 1) // SHOP_PAGE_SIZE)
        page = min(game.shop.page, total - 1)
        start = page * SHOP_PAGE_SIZE
        for i, entry in enumerate(stock[start:start + SHOP_PAGE_SIZE]):
            key = entry['key'];
            price = entry['price'];
            sold = entry.get('sold', False)
            base, _ = parse_item_key(key)
            it = ITEMS.get(base, {})
            color = item_display_color(key)
            learned = False
            if it.get('type') == 'book':
                stat = it.get('stat')
                if stat in ('double_strike', 'regen', 'vampire', 'thorns', 'greedy'):
                    learned = getattr(game.player, stat, False)
            if sold or learned:
                status = "(продано)" if sold else "(✓ изучено)"
                self.icons.draw_item(key, x0, y, size=28)
                self.text(f"{item_display_name(key)}  {status}",
                          x0 + 36, y + 4, T.TEXT_MUTED, font=T.FONT_SMALL)
                y += 34;
                continue
            can_afford = game.player.gold >= price
            row_y = y
            self.icons.draw_item(key, x0, y, size=28)
            self.text(item_display_name(key), x0 + 36, y + 4,
                      color if can_afford else T.TEXT_MUTED)
            self.text(f"{price} з.", self._modal_right(game), y + 4,
                      T.GOLD if can_afford else T.TEXT_MUTED, align_right=True)
            self.text(it.get('desc', ''), x0 + 36, y + 24, T.TEXT_MUTED, font=T.FONT_TINY)
            if it.get('type') in ('weapon', 'armor', 'helmet', 'leggings'):
                deltas = self.compare_item(game, key, it['type'])
                dt, dc = self.format_delta(deltas)
                if dt:
                    self.text(dt, x0 + 36, y + 38, dc, font=T.FONT_TINY)
                    y += 10
            y += 46
            game._ui_hits['shop']['rows'].append((i, (x0, row_y, cw, y - row_y - 6)))
        if total > 1:
            self.text(f"Стр. {page + 1}/{total}",
                      self._modal_right(game), y + 6, T.TEXT_DIM,
                      font=T.FONT_SMALL, align_right=True)

    def _draw_shop_sell(self, game, x0, y, cw):
        from core.config import SHOP_PAGE_SIZE
        items = sorted(game.player.inventory.items())
        if not items:
            self.text("  (рюкзак пуст)", x0, y, T.TEXT_MUTED, font=T.FONT_SMALL)
            return
        total = max(1, (len(items) + SHOP_PAGE_SIZE - 1) // SHOP_PAGE_SIZE)
        page = min(game.shop.page, total - 1)
        start = page * SHOP_PAGE_SIZE
        for i, (key, count) in enumerate(items[start:start + SHOP_PAGE_SIZE]):
            price = item_sell_price(key)
            row_y = y
            self.icons.draw_item(key, x0, y, size=28)
            label = item_display_name(key)
            if count > 1:
                label += f"  x{count}"
            self.text(label, x0 + 36, y + 4,
                      item_display_color(key))
            self.text(f"{price} з.", self._modal_right(game), y + 4, T.GOLD,
                      align_right=True)
            self.text(ITEMS.get(parse_item_key(key)[0], {}).get('desc', ''),
                      x0 + 36, y + 24, T.TEXT_MUTED, font=T.FONT_TINY)
            y += 46
            game._ui_hits['shop']['rows'].append((i, (x0, row_y, cw, y - row_y - 6)))
        if total > 1:
            self.text(f"Стр. {page + 1}/{total}",
                      self._modal_right(game), y + 6, T.TEXT_DIM,
                      font=T.FONT_SMALL, align_right=True)

    # ════════════════════════ ДРЕВО НАВЫКОВ ════════════════════════
    def draw_skills(self, game):
        self._modal_bg(game)
        cx0, cy0, cw, ch = game._modal_content_rect
        from core import skills as sk
        from core.data import class_data
        p = game.player
        ctrl = game.skills_ui
        game._ui_hits['skills'] = {'tabs': [], 'rows': [], 'promotion': []}

        cls = class_data(p.class_key)
        self.text("ДРЕВО НАВЫКОВ", cx0, cy0, (214, 178, 98), font=T.FONT_H1)
        self.text(f"{p.class_name} · Уровень {p.level}",
                  self._modal_right(game), cy0 + 8, T.TEXT, align_right=True)
        if p.skill_points:
            T.badge(self.screen, cx0, cy0 + 34, f"● {p.skill_points} очков навыков",
                    color=T.GOLD, bg=(50, 40, 15), font=T.FONT_SMALL)

        y = cy0 + 66
        y, tabs = self._draw_tabs(game, sk.TABS, ctrl.tab, cx0, y, cw,
                                  'skills')
        game._ui_hits['skills']['tabs'] = tabs

        if ctrl.tab == sk.TAB_CLASS:
            self._draw_class_tab(game, cx0, y, cw, ch, cls, ctrl,
                                 sk, class_data)
        else:
            self._draw_craft_tab(game, cx0, y, cw, ch, ctrl, sk)

        hints = "↑/↓ — выбор · Enter — изучить · Q/E — вкладки · N/Esc — закрыть"
        self.text(hints, cx0 + cw // 2, cy0 + ch - 20, T.TEXT_MUTED,
                  font=T.FONT_SMALL, center=True)

    def _draw_class_tab(self, game, x, y, cw, ch, cls, ctrl, sk, class_data):
        p = game.player
        perk = cls.get('perk', '')
        if perk:
            self.text(self.fit_text(perk, cw, T.FONT_SMALL), x, y,
                      (200, 180, 130), font=T.FONT_SMALL)
            y += 26

        # ── Смена класса ──
        options = ctrl.promotion_options()
        req = ctrl.change_class_requirement()
        if options:
            ready = req is not None and p.level >= req
            title = "Смена класса"
            if req is not None and not ready:
                title += f" (с {req} уровня)"
            self.text(title, x, y, T.TITLE if ready else T.TEXT_MUTED,
                      font=T.FONT_H2)
            y += 26
            bx = x
            for i, key in enumerate(options):
                name = class_data(key).get('name', key)
                w = max(120, T.FONT_SMALL.size(name)[0] + 26)
                rect = (bx, y, w, 28)
                hover = self._in_rect(rect, getattr(game, 'mouse_pos', (-1, -1)))
                T.rounded_rect(self.screen, T.ROW_HOVER if hover else
                               (32, 28, 22), rect, radius=6,
                               border_color=T.TITLE if ready else
                               T.PANEL_BORDER_DARK)
                self.text(f"{i + 1}. {name}", bx + 10, y + 5,
                          T.TITLE_LIGHT if ready else T.TEXT_MUTED,
                          font=T.FONT_SMALL)
                game._ui_hits['skills']['promotion'].append((i, rect))
                bx += w + 8
            y += 38

        # ── Узлы ──
        nodes = ctrl.rows()
        row_h = 46
        for i, node in enumerate(nodes):
            if y + row_h > ch + 20:
                break
            rect = (x, y, cw, row_h - 6)
            selected = i == ctrl.selection
            if selected:
                T.rounded_rect(self.screen, T.ROW_HOVER, rect, radius=6,
                               border_color=T.ROW_BORDER)
            state = node.get('state')
            color = {
                'learned': (120, 220, 120),
                'available': (240, 210, 90),
                'level': T.TEXT_MUTED,
                'points': (200, 160, 120),
            }.get(state, T.TEXT)
            mark = {'learned': '✓', 'available': '+', 'level': '×',
                    'points': '●'}.get(state, '·')
            self.text(f"{mark}  {node['name']}", x + 10, y + 4, color,
                      font=T.FONT_BODY)
            self.text(f"ур. {node['level']}", x + 300, y + 6,
                      T.GOLD if p.level >= node['level'] else T.HP_CRIT,
                      font=T.FONT_SMALL)
            effects = sk.describe_effects(node.get('effects', {}))
            self.text(effects or "—", x + 390, y + 6, T.TEXT_DIM,
                      font=T.FONT_SMALL)
            game._ui_hits['skills']['rows'].append((i, rect))
            y += row_h

    def _draw_craft_tab(self, game, x, y, cw, ch, ctrl, sk):
        p = game.player
        tab = ctrl.tab
        level, need, ratio = sk.tab_progress(p, tab)
        T.section(self.screen, x, y, cw, sk.TAB_NAMES.get(tab, tab))
        y += 28
        T.bar(self.screen, (x, y, 280, 12), ratio, (120, 180, 110))
        label = f"Уровень {level} / {sk.SKILL_CAP}"
        if need:
            label += f" · {sk.craft_xp(p, tab)}/{need}"
        self.text(label, x + 294, y - 2, T.TEXT_DIM, font=T.FONT_SMALL)
        y += 26
        desc = sk.CRAFT_TREES.get(tab, {}).get('desc', '')
        if desc:
            self.text(desc, x, y, T.TEXT_MUTED, font=T.FONT_TINY)
            y += 22

        nodes = sk.craft_tree(tab)
        row_h = 40
        for i, node in enumerate(nodes):
            if y + row_h > ch + 30:
                break
            rect = (x, y, cw, row_h - 6)
            selected = i == ctrl.selection
            if selected:
                T.rounded_rect(self.screen, T.ROW_HOVER, rect, radius=6,
                               border_color=T.ROW_BORDER)
            unlocked = level >= node['level']
            mark = '✓' if unlocked else '×'
            color = (120, 220, 120) if unlocked else T.TEXT_MUTED
            self.text(f"{mark}  {node['name']}", x + 10, y + 4, color,
                      font=T.FONT_BODY)
            self.text(f"ур. {node['level']}", x + 300, y + 6,
                      T.GOLD if unlocked else T.HP_CRIT, font=T.FONT_SMALL)
            effects = sk.describe_effects(node.get('effects', {}))
            if effects:
                self.text(effects, x + 390, y + 6, T.TEXT_DIM,
                          font=T.FONT_SMALL)
            recipes = sk.recipes_for_level(tab, node['level'])
            if recipes:
                names = ', '.join(r[1].get('name', r[0]) for r in recipes)
                self.text(self.fit_text("Рецепты: " + names, cw - 520,
                                        T.FONT_TINY),
                          x + 390, y + 20, T.TEXT_MUTED, font=T.FONT_TINY)
            game._ui_hits['skills']['rows'].append((i, rect))
            y += row_h

    # ════════════════════════ КРАФТ ════════════════════════
    def _draw_tabs(self, game, tabs, active, x, y, cw, prefix):
        """Общий ряд вкладок. Возвращает (новый_y, hits)."""
        from core import skills as sk
        hits = []
        tab_w = min(200, (cw - 8 * (len(tabs) - 1)) // max(1, len(tabs)))
        tx = x
        for tab in tabs:
            rect = (tx, y, tab_w, 30)
            is_active = tab == active
            hover = self._in_rect(rect, getattr(game, 'mouse_pos', (-1, -1)))
            bg = T.ROW_HOVER if is_active or hover else (30, 27, 21)
            border = T.TITLE if is_active else T.PANEL_BORDER
            T.rounded_rect(self.screen, bg, rect, radius=6,
                           border_color=border)
            self.text(sk.TAB_SHORT.get(tab, tab), tx + tab_w // 2,
                      y + 6, T.TITLE_LIGHT if is_active else T.TEXT_DIM,
                      font=T.FONT_SMALL, center=True)
            hits.append((tab, rect))
            tx += tab_w + 8
        return y + 40, hits

    def draw_craft(self, game):
        self._modal_bg(game)
        cx0, cy0, cw, ch = game._modal_content_rect
        from core.config import HAMMER_SIZE
        from core import skills as sk
        game._ui_hits['craft'] = {'hammer': [], 'tabs': []}

        self.text("КРАФТ", cx0, cy0, (214, 178, 98), font=T.FONT_H1)
        self.text(f"Уровень {game.player.level}",
                  self._modal_right(game), cy0 + 8, T.TEXT, align_right=True)
        y = cy0 + 52
        y, tabs = self._draw_tabs(game, sk.CRAFT_TABS, game.craft.tab,
                                  cx0, y, cw, 'craft')
        game._ui_hits['craft']['tabs'] = tabs

        level, need, ratio = sk.tab_progress(game.player, game.craft.tab)
        quality_bonus = sk.craft_quality_bonus(game.player)
        T.section(self.screen, cx0, y, cw, sk.TAB_NAMES.get(
            game.craft.tab, game.craft.tab))
        y += 28
        T.bar(self.screen, (cx0, y, 260, 12), ratio, (120, 180, 110))
        progress = f"Ур. {level}"
        if need:
            progress += f" · {sk.craft_xp(game.player, game.craft.tab)}/{need}"
        self.text(progress, cx0 + 272, y - 2, T.TEXT_DIM, font=T.FONT_SMALL)
        if quality_bonus:
            self.text(f"качество +{quality_bonus}", self._modal_right(game),
                      y - 2, (240, 210, 90), font=T.FONT_SMALL,
                      align_right=True)
        y += 28

        if game.craft.minigame_active:
            self._draw_minigame(game, cx0, y, cw)
            return

        recipes = game.craftable_list()
        if not recipes:
            self.text("  (нет рецептов этой ветки)", cx0, y, T.TEXT_MUTED,
                      font=T.FONT_SMALL)
            return
        for i, (key, it, recipe, can) in enumerate(recipes[:8]):
            lvl_req = it.get('level_req', 1)
            lvl_ok = game.player.level >= lvl_req
            skill_req = int(it.get('skill_req', 1))
            skill_ok = level >= skill_req
            can_full = can and lvl_ok and skill_ok
            color = T.HEAL if can_full else T.TEXT_MUTED
            row_y = y

            self.icons.draw_item(key, cx0, row_y, size=28)
            self.text(item_display_name(key), cx0 + 36, row_y + 4, color)
            req_text = f"Ур.{lvl_req}"
            if skill_req > 1:
                req_text += f" · навык {skill_req}"
            self.text(req_text, cx0 + 280, row_y + 4,
                      (T.GOLD if lvl_ok else T.HP_CRIT) if skill_ok
                      else T.HP_CRIT, font=T.FONT_SMALL)
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
                (i, (hx, hy, HAMMER_SIZE, HAMMER_SIZE)))
            y += 26

        hints = "Q/E — вкладки · 1–8 — создать · K/Esc — закрыть"
        if sk.has_craft_minigame(game.player, game.craft.tab):
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

    # ════════════════════════ ДИАЛОГ ════════════════════════
    def draw_dialogue(self, game):
        self._modal_bg(game)
        cx0, cy0, cw, ch = game._modal_content_rect

        frame_color = (206, 166, 82) if game.dialogue.npc_type == 'elder' \
            else T.MERCH

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
        if game.dialogue.npc_type == 'elder':
            self.icons.draw_elder(portrait_x + icon_off, portrait_y + icon_off,
                                  size=icon_size)
        else:
            self.icons.draw_merchant(portrait_x + icon_off, portrait_y + icon_off,
                                     size=icon_size)

        # ── Имя в золотом бейдже ──
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
    def draw_quests(self, game):
        self._modal_bg(game)
        cx0, cy0, cw, ch = game._modal_content_rect
        from core.data import QUESTS
        self.text("КВЕСТЫ", cx0, cy0, (214, 178, 98), font=T.FONT_H1)
        y = cy0 + 56
        # Секция + отступ до карточек (синхронизировано с button_rects)
        y = T.section(self.screen, cx0, y, cw, "Список")
        y += 24

        items = game.quests.visible_quests()
        if not items:
            self.text("Нет активных квестов", cx0 + cw // 2, y + 40,
                      T.TEXT_MUTED, font=T.FONT_H2, center=True)
            return

        STATE = {
            'available': ('[ВЗЯТЬ]', (176, 186, 192)),
            'active': ('[АКТИВЕН]', (206, 190, 138)),
            'completed': ('[СДАТЬ]', (134, 190, 118)),
        }
        for i, (idx, crect) in enumerate(game.quests.button_rects()):
            if i >= len(items):
                break
            key, state = items[i]
            q = QUESTS[key]
            selected = (i == game.quests.selection)
            cx_, cy_, cw_, ch_ = crect
            if selected:
                T.rounded_rect(self.screen, (60, 60, 80), crect,
                               radius=T.R_MD, border_color=(206, 166, 82))
                T.rounded_rect(self.screen, (206, 166, 82), crect,
                               width=1, radius=T.R_MD)
            else:
                T.rounded_rect(self.screen, (32, 32, 46), crect,
                               radius=T.R_MD, border_color=(60, 60, 85))
            label, label_color = STATE[state]
            self.text(q['name'], cx_ + 16, cy_ + 12, (238, 222, 188), font=T.FONT_H2)
            self.text(label, cx_ + cw_ - 16, cy_ + 12, label_color,
                      font=T.FONT_H2, align_right=True)
            self.text(q['desc'], cx_ + 16, cy_ + 40, T.TEXT_DIM, font=T.FONT_SMALL)
            if state == 'active':
                st = game.quest_log.active.get(key, {})
                self.text(f"Прогресс: {st.get('progress', 0)}/{st.get('count', q['count'])}",
                          cx_ + 16, cy_ + 58, (196, 192, 160), font=T.FONT_SMALL)
            elif state == 'completed':
                self.text("Готово к сдаче!", cx_ + 16, cy_ + 58,
                          (134, 190, 118), font=T.FONT_SMALL)
            else:
                self.text(f"+{q['reward_gold']} з.  +{q['reward_xp']} XP",
                          cx_ + 16, cy_ + 58, (240, 210, 90), font=T.FONT_SMALL)

    # ════════════════════════ СТАТИСТИКА ════════════════════════
    def draw_run_stats(self, game):
        self._modal_bg(game)
        cx0, cy0, cw, ch = game._modal_content_rect
        self.text("СТАТИСТИКА ЗАБЕГА", cx0, cy0, (214, 178, 98), font=T.FONT_H1)
        y = cy0 + 56

        s = game.run_summary()
        mins = s['time_seconds'] // 60
        secs = s['time_seconds'] % 60

        left_x = cx0
        right_x = cx0 + cw // 2 + 20
        y_col = y
        self.text("─── ОБЩЕЕ ───", left_x, y_col, (190, 172, 138), font=T.FONT_H2)
        y_col += 40
        left = [
            ("Время", f"{mins}:{secs:02d}"), ("Убито мобов", str(s['kills'])),
            ("Боссов", str(s['boss_kills'])), ("Уровень", str(s['level'])),
            ("Глубина", str(s['depth'])),
            ("Золото сейчас", str(s['gold'])),
            ("Золото добыто", str(s['earned_gold'])),
            ("Зелий", str(s['potions_used'])), ("Крафтов", str(s['crafts'])),
            ("Квестов сдано", str(s['quests'])),
            ("Квестов активно", str(s['quests_active'])),
        ]
        for name, val in left:
            self.text(name, left_x, y_col, T.TEXT_DIM, font=T.FONT_BODY)
            self.text(val, left_x + 260, y_col, T.TEXT, font=T.FONT_BODY)
            y_col += 34

        from core.data import MONSTERS, BOSSES
        y_col = y
        self.text("─── УБИТО ПО ТИПАМ ───", right_x, y_col,
                  (190, 172, 138), font=T.FONT_H2)
        y_col += 40
        all_m = {m['ch']: m['name'] for m in MONSTERS.values()}
        all_m.update({b['ch']: b['name'] for b in BOSSES.values()})
        has = False
        for ch, count in sorted(s['kills_by_ch'].items(), key=lambda x: -x[1]):
            if count == 0:
                continue
            has = True
            self.text(f"{all_m.get(ch, ch)}:", right_x, y_col, T.TEXT_DIM, font=T.FONT_BODY)
            self.text(str(count), right_x + 260, y_col, T.TEXT, font=T.FONT_BODY)
            y_col += 34
        if not has:
            self.text("(пока никого)", right_x, y_col, T.TEXT_MUTED, font=T.FONT_SMALL)