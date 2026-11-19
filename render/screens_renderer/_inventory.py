"""Экран инвентаря: draw_inventory, equipment panel, slots, tooltips.
"""
from render.screens_renderer._common import *  # noqa: F401,F403


class InventoryMixin:
    """Экран инвентаря: draw_inventory, equipment panel, slots, tooltips."""

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
        self.text("Q/E — страницы   U/V/H/B/N — снять снаряжение",
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
        items = rules.inventory_stacks(p.inventory)
        total = max(1, (len(items) + INVENTORY_PAGE_SIZE - 1)
                    // INVENTORY_PAGE_SIZE)
        page = min(game.inv_page, total - 1)
        game.inv_page = page
        start = page * INVENTORY_PAGE_SIZE
        page_items = items[start:start + INVENTORY_PAGE_SIZE]

        gap_cell = 4
        cols, rows = INVENTORY_COLS, INVENTORY_ROWS
        avail_h = (cy0 + ch) - y2 - 46
        cell_w = min(64, (inv_w - gap_cell * (cols - 1)) // cols)
        cell_h = min(64, max(38, (avail_h - gap_cell * (rows - 1)) // rows))
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
        """Левая панель: силуэт, ячейки экипировки и подсказка класса."""
        p = game.player

        # ── Что может носить этот класс ──
        # Подсказка живёт внизу панели и переносится по словам, чтобы
        # длинные списки брони/оружия были видны целиком.
        from core.data import ARMOR_TYPE_NAMES, WCLASS_NAMES, class_data
        cls = class_data(p.class_key)
        weapons = ', '.join(WCLASS_NAMES.get(w, w)
                            for w in cls.get('weapons', ())) or '—'
        armor = ', '.join(ARMOR_TYPE_NAMES.get(a, a)
                          for a in cls.get('armor_types', ())) or '—'
        hint = f"Оружие: {weapons}. Броня: {armor}."
        hint_lines = self._wrap_lines(hint, T.FONT_TINY, w - 12)
        hint_h = len(hint_lines) * 14 + 6
        hint_y = y + h - hint_h

        # ── Силуэт персонажа ──
        char_size = min(120, max(80, int(h * 0.22)), w - 40)
        char_x = x + (w - char_size) // 2
        char_y = y + 20

        # Подложка под силуэт
        T.rounded_rect(self.screen, (26, 24, 32),
                       (char_x - 8, char_y - 8, char_size + 16, char_size + 16),
                       radius=10, border_color=(60, 55, 70))

        # Персонаж (используем тот же draw_player, что и на поле)
        self.icons.draw_player(p.class_key, char_x, char_y, size=char_size)

        # ── Ячейки: голова, тело, ноги, обувь — вертикально;
        #    руки (оружие) — сбоку от тела. ──
        slots_y = char_y + char_size + 24
        avail = max(120, hint_y - slots_y - 6)
        slot_size = max(30, min(48, avail // 4 - 34, (w - 40) // 2 - 8))
        cell_gap = 10
        row_step = slot_size + 34
        group_w = 2 * slot_size + cell_gap
        gx = x + (w - group_w) // 2

        slots = [
            ('head', 'Голова', 0, 0),
            ('armor', 'Тело', 0, 1),
            ('weapon', 'Руки', 1, 1),
            ('legs', 'Ноги', 0, 2),
            ('boots', 'Обувь', 0, 3),
        ]

        for slot_key, label, gcol, grow in slots:
            sx = gx + gcol * (slot_size + cell_gap)
            sy = slots_y + grow * row_step
            rect = (sx, sy, slot_size, slot_size)
            item_key = getattr(p, slot_key, None)
            hover = self._in_rect(rect, (mx, my))

            # Подпись над ячейкой
            self.text(label, sx + slot_size // 2, sy - 14, T.TEXT_DIM,
                      font=T.FONT_TINY, center=True)

            # Фон ячейки
            bg = (58, 49, 35) if hover else (32, 28, 22)
            border = T.ROW_BORDER if hover else T.PANEL_BORDER
            T.rounded_rect(self.screen, bg, rect, radius=8, border_color=border)

            if item_key:
                icon_size = slot_size - 10
                self.icons.draw_item(item_key, sx + 5, sy + 5, size=icon_size)
                name = self.fit_text(item_display_name(item_key),
                                     slot_size + 8, T.FONT_TINY)
                self.text(name, sx + slot_size // 2, sy + slot_size + 2,
                          item_display_color(item_key),
                          font=T.FONT_TINY, center=True)
                game._ui_hits['inventory']['equip'].append((slot_key, rect))
                if hover:
                    hovered[0] = ('equip', item_key)
            else:
                # Пунктирная подсказка «пусто»
                self.text("—", sx + slot_size // 2,
                          sy + slot_size // 2 - 8,
                          T.TEXT_MUTED, font=T.FONT_SMALL, center=True)

        # ── Подсказка класса — внизу секции «Снаряжение» ──
        for i, line in enumerate(hint_lines):
            self.text(line, x + w // 2, hint_y + i * 14, (170, 180, 160),
                      font=T.FONT_TINY, center=True)


    def _draw_inv_slot(self, key, count, rect, player_level, game, hover):
        x, y, w, h = rect
        bg = (58, 49, 35) if hover else (32, 28, 22)
        border = T.ROW_BORDER if hover else T.PANEL_BORDER
        T.rounded_rect(self.screen, bg, rect, radius=8, border_color=border)

        icon_size = max(20, min(56, h - 24))
        self.icons.draw_item(key, x + (w - icon_size) // 2, y + 3,
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
        label = self.fit_text(item_display_name(key), w - 6,
                              T.FONT_TINY if h < 70 else T.FONT_SMALL)
        self.text(label, x + w // 2, y + h - 17, color,
                  font=T.FONT_TINY if h < 70 else T.FONT_SMALL,
                  center=True)

        # Бейдж количества — в правом верхнем углу ячейки
        if count > 1:
            self._draw_count_badge(x, y, w, h, count)


    def _draw_count_badge(self, x, y, w, h, count):
        """Счётчик предметов: золотая капсула в правом верхнем углу."""
        txt = "×99+" if count >= 100 else f"×{count}"
        font = T.FONT_SMALL
        surf = font.render(txt, True, (32, 24, 12))

        if count < 10:
            bg, border = (240, 195, 105), (150, 110, 40)
        elif count < 50:
            bg, border = (240, 160, 80), (150, 90, 30)
        else:
            bg, border = (240, 120, 100), (150, 60, 40)

        tw, th = surf.get_size()
        bw = max(tw + 14, 30)
        bh = max(th + 6, 24)
        bx = x + w - bw - 3
        by = y + 3

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
        return (bx, by, bw, bh)


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

