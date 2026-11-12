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
        slots = [
            ("Голова", p.head, "DEF"),
            ("Руки", p.weapon, "ATK"),
            ("Тело", p.armor, "DEF"),
            ("Ноги", p.legs, "DEF"),
        ]
        for label, key, stat in slots:
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
        for slot in ('armor', 'head', 'legs'):
            key = getattr(p, slot, None)
            if key:
                b, _ = parse_item_key(key)
                extra += ITEMS.get(b, {}).get('hp_bonus', 0)
        return f"HP = 15 + {p.vit * 2} VIT + {extra} = {p.max_hp}"

    def _d_crit(self, p):
        raw = p.luk * 2; final = int(p.crit * 100)
        cap = " (кап 40%)" if raw > 40 else ""
        bonus = int(p._w_crit_bonus * 100)
        extra = f" + {bonus}% оружие" if bonus else ""
        return f"Крит = {final}% (2% × LUK {p.luk} = {raw:.0f}%{cap}{extra})"

    def _d_def(self, p):
        df_part = p.df_stat // 2
        total = df_part
        for slot in ('armor', 'head', 'legs'):
            key = getattr(p, slot, None)
            if key:
                b, l = parse_item_key(key)
                total += ITEMS.get(b, {}).get('value', 0) + l
        return f"DEF = {df_part} (df_stat) + {total - df_part} (экип.) = {total}"

    # ════════════════════════ ИНВЕНТАРЬ ════════════════════════
    @staticmethod
    def _in_rect(rect, pos):
        x, y, w, h = rect
        return x <= pos[0] <= x + w and y <= pos[1] <= y + h

    def draw_inventory(self, game):
        self._modal_bg(game)
        cx0, cy0, cw, ch = game._modal_content_rect
        p = game.player
        game._ui_hits['inventory'] = {'items': [], 'equip': [], 'pages': []}
        mx, my = getattr(game, 'mouse_pos', (-1, -1))
        hovered = [None]  # nonlocal-обёртка

        self.text("ИНВЕНТАРЬ", cx0, cy0, (214, 178, 98), font=T.FONT_H1)
        self.text(f"${p.gold}", self._modal_right(game), cy0 + 8, T.GOLD,
                  align_right=True)
        self.text("Q/E — страницы   U/V/H/B — снять экипировку",
                  self._modal_right(game), cy0 + 34, T.TEXT_MUTED,
                  font=T.FONT_SMALL, align_right=True)

        y = cy0 + 56

        # ── Разделение на 2 колонки ──
        equip_w = int(cw * 0.36)
        gap = 20
        inv_w = cw - equip_w - gap
        equip_x = cx0
        inv_x = cx0 + equip_w + gap

        # ═══════ ЛЕВАЯ КОЛОНКА: силуэт + ячейки экипировки ═══════
        y1 = T.section(self.screen, equip_x, y, equip_w, "Снаряжение")
        panel_h = (cy0 + ch) - y1 - 20
        self._draw_equipment_panel(game, equip_x, y1, equip_w, panel_h,
                                   mx, my, hovered)

        # ═══════ ПРАВАЯ КОЛОНКА: сетка 5×5 ═══════
        y2 = T.section(self.screen, inv_x, y, inv_w, "Рюкзак")

        from core.config import INVENTORY_COLS, INVENTORY_ROWS
        items = sorted(p.inventory.items())
        total = max(1, (len(items) + INVENTORY_PAGE_SIZE - 1)
                    // INVENTORY_PAGE_SIZE)
        page = min(game.inv_page, total - 1)
        game.inv_page = page
        start = page * INVENTORY_PAGE_SIZE
        page_items = items[start:start + INVENTORY_PAGE_SIZE]

        gap_cell = 6
        cols, rows = INVENTORY_COLS, INVENTORY_ROWS
        avail_h = (cy0 + ch) - y2 - 46
        cell_w = min(120, (inv_w - gap_cell * (cols - 1)) // cols)
        cell_h = max(80, min(120, (avail_h - gap_cell * (rows - 1)) // rows))
        grid_w = cols * cell_w + gap_cell * (cols - 1)
        grid_x = inv_x + (inv_w - grid_w) // 2

        for idx in range(INVENTORY_PAGE_SIZE):
            row, col = divmod(idx, cols)
            rect = (grid_x + col * (cell_w + gap_cell),
                    y2 + row * (cell_h + gap_cell), cell_w, cell_h)
            if idx < len(page_items):
                key, count = page_items[idx]
                hover = self._in_rect(rect, (mx, my))
                self._draw_inv_slot(key, count, rect, p.level, game, hover)
                game._ui_hits['inventory']['items'].append((key, rect))
                if hover:
                    hovered[0] = ('item', (key, count))
            else:
                T.rounded_rect(self.screen, (25, 22, 18), rect, radius=8,
                               border_color=(48, 42, 32))

        grid_bottom = y2 + rows * cell_h + (rows - 1) * gap_cell
        if not page_items:
            self.text("Рюкзак пуст", grid_x + grid_w // 2,
                      y2 + (rows * cell_h + (rows - 1) * gap_cell) // 2,
                      T.TEXT_MUTED, font=T.FONT_BODY, center=True)

        # ── Страницы ──
        page_y = grid_bottom + 10
        bw, bh = 38, 28
        prev_rect = (grid_x, page_y, bw, bh)
        next_rect = (grid_x + grid_w - bw, page_y, bw, bh)
        self._draw_page_button("‹", prev_rect, page > 0,
                               page > 0 and self._in_rect(prev_rect, (mx, my)))
        self._draw_page_button("›", next_rect, page < total - 1,
                               page < total - 1 and
                               self._in_rect(next_rect, (mx, my)))
        self.text(f"Стр. {page + 1}/{total}",
                  inv_x + inv_w // 2, page_y + 4, T.TEXT_DIM,
                  font=T.FONT_SMALL, center=True)
        game._ui_hits['inventory']['pages'] = [
            ('prev', prev_rect), ('next', next_rect),
        ]

        if hovered[0]:
            self._draw_inventory_tooltip(game, hovered[0], (mx, my))

    def _draw_equipment_panel(self, game, x, y, w, h, mx, my, hovered):
        """Левая панель: силуэт игрока + 4 ячейки (голова, руки, тело, ноги)."""
        p = game.player

        # ── Силуэт персонажа ──
        char_size = min(h - 260, 200, w - 40)
        char_size = max(char_size, 120)
        char_x = x + (w - char_size) // 2
        char_y = y + 10

        # Подложка под силуэт
        T.rounded_rect(self.screen, (26, 24, 32),
                       (char_x - 8, char_y - 8, char_size + 16, char_size + 16),
                       radius=10, border_color=(60, 55, 70))

        # Персонаж (используем тот же draw_player, что и на поле)
        self.icons.draw_player(p.class_key, char_x, char_y, size=char_size)

        # ── 4 ячейки: голова, руки, тело, ноги ──
        slot_size = min(72, (w - 40) // 2 - 8)
        slots_y = char_y + char_size + 20

        # Сетка 2×2
        slots = [
            ('head', 'Голова', 0, 0),
            ('weapon', 'Руки', 1, 0),
            ('armor', 'Тело', 0, 1),
            ('legs', 'Ноги', 1, 1),
        ]
        cell_gap = 10
        grid_w = 2 * slot_size + cell_gap
        gx = x + (w - grid_w) // 2

        for slot_key, label, gcol, grow in slots:
            sx = gx + gcol * (slot_size + cell_gap)
            sy = slots_y + grow * (slot_size + cell_gap + 22)
            rect = (sx, sy, slot_size, slot_size)
            item_key = getattr(p, slot_key, None)
            hover = self._in_rect(rect, (mx, my))

            # Подпись над ячейкой
            self.text(label, sx + slot_size // 2, sy - 18, T.TEXT_DIM,
                      font=T.FONT_SMALL, center=True)

            # Фон ячейки
            bg = (58, 49, 35) if hover else (32, 28, 22)
            border = T.ROW_BORDER if hover else T.PANEL_BORDER
            T.rounded_rect(self.screen, bg, rect, radius=8, border_color=border)

            if item_key:
                icon_size = slot_size - 12
                self.icons.draw_item(item_key, sx + 6, sy + 6, size=icon_size)
                # Название снизу под ячейкой
                name = self.fit_text(item_display_name(item_key),
                                     slot_size + 10, T.FONT_TINY)
                self.text(name, sx + slot_size // 2, sy + slot_size + 2,
                          item_display_color(item_key),
                          font=T.FONT_TINY, center=True)
                game._ui_hits['inventory']['equip'].append((slot_key, rect))
                if hover:
                    hovered[0] = ('equip', item_key)
            else:
                # Пунктирная подсказка «пусто»
                self.text("—", sx + slot_size // 2, sy + slot_size // 2,
                          T.TEXT_MUTED, font=T.FONT_BODY, center=True)

    def _draw_inv_slot(self, key, count, rect, player_level, game, hover):
        x, y, w, h = rect
        bg = (58, 49, 35) if hover else (32, 28, 22)
        border = T.ROW_BORDER if hover else T.PANEL_BORDER
        T.rounded_rect(self.screen, bg, rect, radius=8, border_color=border)

        icon_size = max(40, min(64, h - 26))
        self.icons.draw_item(key, x + (w - icon_size) // 2, y + 4,
                             size=icon_size)

        base, _ = parse_item_key(key)
        it = ITEMS.get(base, {})
        lvl_req = it.get('level_req', 1)
        color = item_display_color(key)
        if lvl_req > 1 and player_level < lvl_req:
            color = T.HP_CRIT
        label = self.fit_text(item_display_name(key), w - 8, T.FONT_TINY)
        self.text(label, x + w // 2, y + h - 16, color,
                  font=T.FONT_TINY, center=True)

        # Улучшенный бейдж количества — в правом нижнем углу
        if count > 1:
            self._draw_count_badge(x, y, w, h, count)

    def _draw_count_badge(self, x, y, w, h, count):
        """Красивый бейдж количества в правом нижнем углу ячейки."""
        txt = "99+" if count >= 100 else str(count)
        font = T.FONT_SMALL
        surf = font.render(txt, True, (255, 250, 240))

        if count < 10:
            bg, border = (52, 44, 22), (206, 166, 82)
        elif count < 50:
            bg, border = (90, 62, 22), (240, 195, 105)
        else:
            bg, border = (105, 38, 28), (240, 130, 100)

        tw, th = surf.get_size()
        bw = max(tw + 16, 30)
        bh = max(th + 6, 24)
        bx = x + w - bw - 3
        by = y + h - bh - 3

        # Тень
        shadow = pygame.Surface((bw, bh), pygame.SRCALPHA)
        pygame.draw.rect(shadow, (0, 0, 0, 140), shadow.get_rect(),
                         border_radius=bh // 2)
        self.screen.blit(shadow, (bx + 1, by + 2))

        # Капсула
        pygame.draw.rect(self.screen, bg, (bx, by, bw, bh),
                         border_radius=bh // 2)
        pygame.draw.rect(self.screen, border, (bx, by, bw, bh), 2,
                         border_radius=bh // 2)

        # Блик сверху
        pygame.draw.line(self.screen, T.shade(border, 1.4),
                         (bx + 8, by + 2), (bx + bw - 8, by + 2), 1)

        # Текст
        self.screen.blit(surf, (bx + (bw - tw) // 2,
                                by + (bh - th) // 2 - 1))

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
        if kind == 'equip':
            stat = "ATK" if it.get('type') == 'weapon' else "DEF"
            bonus = it.get('value', 0) + parse_item_key(key)[1]
            if bonus:
                lines.append((f"+{bonus} {stat}", T.HEAL, T.FONT_SMALL))
        elif it.get('type') in ('weapon', 'armor'):
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
            key = entry['key']
            price = entry['price']
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
                y += 34
                continue
            can_afford = game.player.gold >= price
            row_y = y
            self.icons.draw_item(key, x0, y, size=28)
            self.text(item_display_name(key), x0 + 36, y + 4,
                      color if can_afford else T.TEXT_MUTED)
            self.text(f"{price} з.", self._modal_right(game), y + 4,
                      T.GOLD if can_afford else T.TEXT_MUTED, align_right=True)
            self.text(it.get('desc', ''), x0 + 36, y + 24, T.TEXT_MUTED,
                      font=T.FONT_TINY)
            if it.get('type') in ('weapon', 'armor'):
                deltas = self.compare_item(game, key, it['type'])
                dt, dc = self.format_delta(deltas)
                if dt:
                    self.text(dt, x0 + 36, y + 38, dc, font=T.FONT_TINY)
                    y += 10
            y += 46
            # start + i — глобальный индекс в shop_stock, а не page-local
            game._ui_hits['shop']['rows'].append(
                (start + i, (x0, row_y, cw, y - row_y - 6)))
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
            # глобальный индекс в player.inventory, а не page-local
            game._ui_hits['shop']['rows'].append(
                (start + i, (x0, row_y, cw, y - row_y - 6)))
        if total > 1:
            self.text(f"Стр. {page + 1}/{total}",
                      self._modal_right(game), y + 6, T.TEXT_DIM,
                      font=T.FONT_SMALL, align_right=True)

    # ════════════════════════ КРАФТ ════════════════════════
    def draw_craft(self, game):
        self._modal_bg(game)
        cx0, cy0, cw, ch = game._modal_content_rect
        from core.config import HAMMER_SIZE
        game._ui_hits['craft'] = {'hammer': []}

        self.text("КРАФТ", cx0, cy0, (214, 178, 98), font=T.FONT_H1)
        self.text(f"Уровень {game.player.level}",
                  self._modal_right(game), cy0 + 8, T.TEXT, align_right=True)
        y = cy0 + 56
        y = T.section(self.screen, cx0, y, cw, "Рецепты")

        recipes = game.craftable_list()
        if not recipes:
            self.text("  (нет рецептов)", cx0, y, T.TEXT_MUTED, font=T.FONT_SMALL)
            return
        for i, (key, it, recipe, can) in enumerate(recipes[:8]):
            lvl_req = it.get('level_req', 1)
            lvl_ok = game.player.level >= lvl_req
            can_full = can and lvl_ok
            color = T.HEAL if can_full else T.TEXT_MUTED
            row_y = y

            self.icons.draw_item(key, cx0, row_y, size=28)
            self.text(item_display_name(key), cx0 + 36, row_y + 4, color)
            self.text(f"Ур.{lvl_req}", cx0 + 300, row_y + 4,
                      T.GOLD if lvl_ok else T.HP_CRIT, font=T.FONT_SMALL)
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
            hover = (hx <= mx <= hx + HAMMER_SIZE and hy <= my <= hy + HAMMER_SIZE)
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
            if i >= len(items): break
            key, state = items[i]
            if key in QUESTS:
                q = QUESTS[key]
            else:
                q = game.quest_log.active.get(key, {})
            q_name = q.get('name', '?')
            q_desc = q.get('desc', '')
            q_gold = q.get('reward_gold', 0)
            q_xp = q.get('reward_xp', 0)
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