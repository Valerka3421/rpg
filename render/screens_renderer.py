"""Экраны поверх игры: статы, инвентарь, магазин, крафт, диалог, квесты, статистика."""
import math

import pygame

from core import config, rules
from core.config import INVENTORY_PAGE_SIZE, SHOP_PAGE_SIZE
from core.data import (
    CITY_NPC_NAMES,
    ITEMS,
    PERK_BOOK_MAP,
    PERK_INFO,
    QUESTS,
    is_city_shop_npc,
    item_display_color,
    item_display_name,
    item_sell_price,
    parse_item_key,
)
from render.base_renderer import BaseRenderer
from render.theme import Theme as T


class ScreensRenderer(BaseRenderer):
    def __init__(self, screen, icons):
        super().__init__(screen)
        self.icons = icons

    def _modal_bg(self, game, rect=None):
        """Затемнение + панель + крестик + возврат rect контента."""
        from core.config import MODAL_CLOSE_PAD, MODAL_CLOSE_SIZE, MODAL_CONTENT_PAD, MODAL_TOP_PAD
        ov = pygame.Surface((config.SCREEN_W, config.SCREEN_H), pygame.SRCALPHA)
        ov.fill((6, 5, 4, 165))
        self.screen.blit(ov, (0, 0))

        rect = rect or game._modal_panel_rect()
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
        game._modal_panel_rect_current = rect
        game._modal_content_rect = (
            rect[0] + MODAL_CONTENT_PAD,
            rect[1] + MODAL_TOP_PAD,
            rect[2] - MODAL_CONTENT_PAD * 2 - size - pad,  # правее крестика не лезем
            rect[3] - MODAL_TOP_PAD - 20,
        )
        return rect

    def _modal_right(self, game):
        from core.config import MODAL_CLOSE_PAD, MODAL_CLOSE_SIZE, MODAL_CONTENT_PAD
        rect = (getattr(game, '_modal_panel_rect_current', None)
                or game._modal_panel_rect())
        return rect[0] + rect[2] - MODAL_CONTENT_PAD - MODAL_CLOSE_SIZE - MODAL_CLOSE_PAD - 6

    # ════════════════════════ СТАТЫ ════════════════════════
    def draw_stats(self, game):
        self._modal_bg(game)
        cx0, cy0, cw, ch = game._modal_content_rect
        from core.config import PLUS_SIZE
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

        STAT_FIELDS = ['str', 'agi', 'vit', 'luk', 'df_stat', 'int']
        rows = [
            ('Сила', 'STR', p.str, '+1 ATK за 2 очка', self._d_atk(p)),
            ('Ловкость', 'AGI', p.agi, '+1.5% уклонение', self._d_dodge(p)),
            ('Выносливость', 'VIT', p.vit, '+2 HP за очко', self._d_hp(p)),
            ('Удача', 'LUK', p.luk, '+2% крит', self._d_crit(p)),
            ('Защита', 'DEF', p.df_stat, '+1 DEF за 2 очка', self._d_def(p)),
            ('Интеллект', 'INT', p.int_stat,
             '+2 маны и +1 урон магией', self._d_int(p)),
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
            y += 20
            self.text(f"  └ {detail}", text_x, y, T.TEXT_MUTED, font=T.FONT_TINY)
            y += 20

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

        y = T.section(self.screen, cx0, y, cw, "Навыки крафта")
        from core.data import SKILL_ORDER, SKILLS, default_skills, skill_xp_to_next
        skills = getattr(p, 'skills', None) or default_skills()
        sx = cx0
        for skill_key in SKILL_ORDER:
            entry = skills.get(skill_key, {'level': 1, 'xp': 0})
            level = entry.get('level', 1)
            xp = entry.get('xp', 0)
            need = skill_xp_to_next(level)
            label = (f"{SKILLS[skill_key]['name']}: ур.{level} "
                     f"({xp}/{need})")
            w = self.text(label, sx, y + 2, T.TEXT, font=T.FONT_SMALL)
            sx += w + 34
        y += 28

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
        for slot in ('armor', 'head', 'legs', 'boots'):
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
        for slot in ('armor', 'head', 'legs', 'boots'):
            key = getattr(p, slot, None)
            if key:
                b, l = parse_item_key(key)
                total += ITEMS.get(b, {}).get('value', 0) + l
        return f"DEF = {df_part} (df_stat) + {total - df_part} (экип.) = {total}"

    def _d_int(self, p):
        from core.rules import magic_damage
        int_stat = getattr(p, 'int_stat', 5)
        return (f"Мана = 10 + {p.level * 2} (ур.) + {int_stat * 2} (INT) "
                f"= {p.max_mana} · свиток огня 20 → {magic_damage(p, 20)}")

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

    @staticmethod
    def _wrap_lines(text, font, max_w):
        """Разбивает текст на строки по словам под заданную ширину."""
        lines = []
        current = ''
        for word in str(text).split():
            candidate = word if not current else f"{current} {word}"
            if not current or font.size(candidate)[0] <= max_w:
                current = candidate
            else:
                lines.append(current)
                current = word
        if current:
            lines.append(current)
        return lines or ['']

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
    def draw_shop(self, game):
        self._modal_bg(game)
        cx0, cy0, cw, ch = game._modal_content_rect
        game._ui_hits['shop'] = {'rows': []}
        npc_type = getattr(game, 'active_shop_npc', None)
        city_shop = is_city_shop_npc(npc_type)
        header = CITY_NPC_NAMES.get(npc_type, 'ТОРГОВЕЦ') if city_shop \
            else "ТОРГОВЕЦ"
        self.text(header.upper(), cx0, cy0, T.MERCH, font=T.FONT_H1)
        # Переключатель режима — крупная кликабельная кнопка, а не только
        # горячая клавиша S: продажа должна быть очевидной в городе.
        buying = game.shop_submode == 'buy'
        mode = "ПОКУПКА" if buying else "ПРОДАЖА"
        mode_rect = (cx0 + (cw - 176) // 2, cy0 + 4, 176, 30)
        mx, my = getattr(game, 'mouse_pos', (-1, -1))
        hover = (mode_rect[0] <= mx <= mode_rect[0] + mode_rect[2]
                 and mode_rect[1] <= my <= mode_rect[1] + mode_rect[3])
        mode_bg = (T.MERCH if buying else T.HP_CRIT)
        if hover:
            mode_bg = T.shade(mode_bg, 1.25)
        T.rounded_rect(self.screen, mode_bg, mode_rect, radius=6,
                       border_color=T.shade(mode_bg, 1.5))
        mode_color = T.BG if buying else (255, 245, 235)
        self.text(mode, mode_rect[0] + mode_rect[2] // 2,
                  mode_rect[1] + 6, mode_color, font=T.FONT_BODY, center=True)
        game._ui_hits['shop']['mode'] = mode_rect
        where = "Город Этельгард" if city_shop else f"Глубина {game.depth}"
        self.text(where, self._modal_right(game), cy0 + 8,
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
        action = "купить" if buying else "продать"
        hint = f"S / кнопка — режим · клик — {action} · Q/E — страницы"
        if not city_shop:
            hint += " · R — обновить"
        self.text(hint,
                  cx0 + cw // 2, cy0 + ch - 22, T.TEXT_MUTED,
                  font=T.FONT_SMALL, center=True)

    def _draw_shop_buy(self, game, x0, y, cw):
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
            if it.get('type') in ('weapon', 'armor', 'head', 'legs', 'boots',
                                  'helmet', 'leggings'):
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
        # Тот же порядок и разбиение на стаки, что и в ShopController.sell:
        # иначе клик по строке продавал бы не тот предмет.
        items = rules.inventory_stacks(game.player.inventory)
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

    # ════════════════════════ ДРЕВО НАВЫКОВ ════════════════════════
    def draw_skills(self, game):
        # Древо живёт в собственном крупном окне, которое можно двигать
        # мышью за заголовок. Обычная модалка тут не используется.
        ctrl = game.skills_ui
        panel = ctrl.panel_rect()
        self._modal_bg(game, panel)
        cx0, cy0, cw, ch = game._modal_content_rect
        from core import skills as sk
        from core.data import class_data
        p = game.player
        game._ui_hits['skills'] = {
            'tabs': [], 'rows': [], 'promotion': [],
            # Вся верхняя полоса с заголовком — «ручка» перетаскивания.
            'drag': (panel[0], panel[1], panel[2], 96),
        }

        # Ручка-«гриф» по центру заголовка: подсказывает, что окно можно
        # тащить. Крестик обрабатывается раньше и остаётся кликабельным.
        gx = panel[0] + panel[2] // 2 - 14
        gy = panel[1] + 16
        for row in range(2):
            for col in range(3):
                pygame.draw.circle(self.screen, (126, 108, 76),
                                   (gx + col * 8, gy + row * 7), 2)

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

    # ── Графическая ветка класса ──
    # Цвет значка по главному эффекту узла — как цветные иконки на референсе.
    _GLYPH_COLORS = {
        'atk': (230, 124, 88), 'crit': (238, 192, 92),
        'hp': (214, 98, 98), 'df': (148, 176, 204),
        'dodge': (166, 202, 176), 'mana': (108, 154, 232),
        'lifesteal': (204, 92, 124), 'attack_speed': (242, 212, 108),
        'move_speed': (146, 208, 188), 'gold_pct': (238, 196, 88),
        'xp_pct': (148, 196, 242), 'craft_xp': (208, 170, 116),
        'craft_quality': (224, 188, 126), 'quest_gold': (228, 200, 128),
        'quest_xp': (168, 200, 236), 'summon': (188, 166, 222),
        'rage': (230, 110, 92), 'frenzy': (232, 136, 96),
    }

    @staticmethod
    def _hex_points(cx, cy, r):
        """Точки шестиугольника «вершиной вверх»."""
        return [(cx + r * math.cos(math.radians(60 * i - 90)),
                 cy + r * math.sin(math.radians(60 * i - 90)))
                for i in range(6)]

    def _dotted_line(self, a, b, color, width=2, dash=6, gap=5):
        ax, ay = a
        bx, by = b
        dx, dy = bx - ax, by - ay
        dist = math.hypot(dx, dy)
        if dist <= 0.1:
            return
        ux, uy = dx / dist, dy / dist
        pos = 0.0
        while pos < dist:
            end = min(pos + dash, dist)
            pygame.draw.line(self.screen, color,
                             (ax + ux * pos, ay + uy * pos),
                             (ax + ux * end, ay + uy * end), width)
            pos = end + gap

    def _draw_skill_glyph(self, effects, cx, cy, r, color, strength=1.0):
        """Процедурный значок узла по его главному эффекту."""
        key = self._primary_effect(effects)
        color = T.shade(self._GLYPH_COLORS.get(key, color), strength)
        s = max(0.55, r / 26.0)
        w = max(1, int(round(2 * s)))
        if key == 'atk':
            pygame.draw.line(self.screen, color, (cx, cy - 11 * s), (cx, cy + 5 * s), w)
            pygame.draw.line(self.screen, color, (cx - 7 * s, cy + 2 * s),
                             (cx + 7 * s, cy + 2 * s), w)
            pygame.draw.line(self.screen, color, (cx, cy + 5 * s), (cx, cy + 10 * s), w)
            pygame.draw.circle(self.screen, color, (int(cx), int(cy + 11 * s)), max(1, int(2 * s)))
        elif key == 'crit':
            pygame.draw.circle(self.screen, color, (int(cx), int(cy)), int(9 * s), w)
            pygame.draw.line(self.screen, color, (cx - 12 * s, cy), (cx + 12 * s, cy), w)
            pygame.draw.line(self.screen, color, (cx, cy - 12 * s), (cx, cy + 12 * s), w)
            pygame.draw.circle(self.screen, color, (int(cx), int(cy)), max(1, int(2 * s)))
        elif key in ('lifesteal', 'mana'):
            pygame.draw.polygon(self.screen, color, [
                (cx, cy - 11 * s), (cx + 8 * s, cy + 1 * s),
                (cx + 4 * s, cy + 9 * s), (cx - 4 * s, cy + 9 * s),
                (cx - 8 * s, cy + 1 * s)])
            if key == 'lifesteal':
                pygame.draw.line(self.screen, (24, 20, 18),
                                 (cx - 3 * s, cy + 2 * s), (cx + 3 * s, cy + 2 * s), w)
        elif key == 'attack_speed':
            pygame.draw.polygon(self.screen, color, [
                (cx + 3 * s, cy - 11 * s), (cx - 7 * s, cy + 1 * s),
                (cx - 1 * s, cy + 1 * s), (cx - 3 * s, cy + 11 * s),
                (cx + 8 * s, cy - 2 * s), (cx + 2 * s, cy - 2 * s)])
        elif key == 'hp':
            rr = max(2, int(5 * s))
            pygame.draw.circle(self.screen, color, (int(cx - 4 * s), int(cy - 3 * s)), rr)
            pygame.draw.circle(self.screen, color, (int(cx + 4 * s), int(cy - 3 * s)), rr)
            pygame.draw.polygon(self.screen, color, [
                (cx - 9 * s, cy - 1 * s), (cx + 9 * s, cy - 1 * s),
                (cx, cy + 10 * s)])
        elif key == 'df':
            shield = [(cx, cy - 11 * s), (cx + 9 * s, cy - 6 * s),
                      (cx + 7 * s, cy + 5 * s), (cx, cy + 11 * s),
                      (cx - 7 * s, cy + 5 * s), (cx - 9 * s, cy - 6 * s)]
            pygame.draw.polygon(self.screen, color, shield, w)
            pygame.draw.line(self.screen, color, (cx, cy - 7 * s), (cx, cy + 7 * s), 1)
        elif key == 'dodge':
            rect = pygame.Rect(int(cx - 10 * s), int(cy - 10 * s),
                               int(20 * s), int(20 * s))
            pygame.draw.arc(self.screen, color, rect,
                            math.radians(25), math.radians(300), w)
            pygame.draw.circle(self.screen, color, (int(cx), int(cy)),
                               max(1, int(2.5 * s)))
        elif key == 'move_speed':
            for off in (-4, 5):
                pygame.draw.lines(self.screen, color, False, [
                    (cx + off * s - 4 * s, cy - 7 * s),
                    (cx + off * s + 3 * s, cy),
                    (cx + off * s - 4 * s, cy + 7 * s)], w)
        elif key == 'gold_pct':
            pygame.draw.circle(self.screen, color, (int(cx), int(cy)), int(10 * s), w)
            pygame.draw.circle(self.screen, color, (int(cx), int(cy)), int(4 * s), w)
            pygame.draw.line(self.screen, color, (cx, cy - 10 * s), (cx, cy - 13 * s), w)
        elif key == 'xp_pct':
            pts = []
            for i in range(10):
                ang = math.radians(-90 + i * 36)
                rad = r * (0.72 if i % 2 == 0 else 0.3)
                pts.append((cx + rad * math.cos(ang), cy + rad * math.sin(ang)))
            pygame.draw.polygon(self.screen, color, pts)
        elif key in ('craft_xp', 'craft_quality'):
            pygame.draw.polygon(self.screen, color, [
                (cx - 11 * s, cy - 5 * s), (cx + 11 * s, cy - 5 * s),
                (cx + 6 * s, cy + 1 * s), (cx + 3 * s, cy + 1 * s),
                (cx + 4 * s, cy + 6 * s), (cx + 8 * s, cy + 10 * s),
                (cx - 8 * s, cy + 10 * s), (cx - 4 * s, cy + 6 * s),
                (cx - 3 * s, cy + 1 * s), (cx - 6 * s, cy + 1 * s)])
        elif key in ('quest_gold', 'quest_xp'):
            pygame.draw.rect(self.screen, color, (int(cx - 8 * s), int(cy - 9 * s),
                                                  int(16 * s), int(18 * s)), w)
            pygame.draw.line(self.screen, color, (cx - 4 * s, cy - 3 * s),
                             (cx + 4 * s, cy - 3 * s), 1)
            pygame.draw.line(self.screen, color, (cx - 4 * s, cy + 2 * s),
                             (cx + 4 * s, cy + 2 * s), 1)
        elif key == 'summon':
            pygame.draw.circle(self.screen, color, (int(cx), int(cy - 2 * s)), int(8 * s), w)
            pygame.draw.rect(self.screen, color, (int(cx - 4 * s), int(cy + 4 * s),
                                                  int(8 * s), int(5 * s)), w)
            pygame.draw.circle(self.screen, color, (int(cx - 3 * s), int(cy - 3 * s)),
                               max(1, int(2 * s)))
            pygame.draw.circle(self.screen, color, (int(cx + 3 * s), int(cy - 3 * s)),
                               max(1, int(2 * s)))
        else:
            pygame.draw.polygon(self.screen, color, [
                (cx, cy - 11 * s), (cx + 9 * s, cy),
                (cx, cy + 11 * s), (cx - 9 * s, cy)], w)

    @staticmethod
    def _primary_effect(effects):
        return next((k for k in (
            'atk', 'crit', 'lifesteal', 'attack_speed', 'mana', 'hp', 'df',
            'dodge', 'move_speed', 'gold_pct', 'xp_pct', 'craft_xp',
            'craft_quality', 'quest_gold', 'quest_xp', 'summon', 'rage',
            'frenzy', 'extra_stats', 'plain_weapons')
            if effects.get(k)), None)

    def _draw_node_icon(self, node, cx, cy, r, color, strength=1.0):
        """Иконка из assets/icons, а при её отсутствии — процедурный значок."""
        from render.assets_icons import IconAssets, node_icon_candidates
        effects = node.get('effects') or {}
        effect_key = self._primary_effect(effects)
        candidates = node_icon_candidates(node.get('key'), effect_key,
                                         node.get('tab'))
        size = int(r * 1.9)
        if IconAssets.draw(self.screen, candidates, cx, cy, size,
                           alpha=255 * min(1.0, max(0.25, strength))):
            return
        self._draw_skill_glyph(effects, cx, cy, r, color, strength)

    def _draw_class_icon(self, class_key, effects, cx, cy, r, color,
                         strength=1.0):
        from render.assets_icons import IconAssets, class_icon_candidates
        effect_key = self._primary_effect(effects or {})
        size = int(r * 1.9)
        if IconAssets.draw(self.screen,
                           class_icon_candidates(class_key, effect_key),
                           cx, cy, size,
                           alpha=255 * min(1.0, max(0.25, strength))):
            return
        self._draw_skill_glyph(effects or {'hp': 1}, cx, cy, r, color,
                               strength)

    def _draw_craft_hub_icon(self, tab, effects, cx, cy, r, color,
                             strength=1.0):
        from render.assets_icons import IconAssets, node_icon_candidates
        effect_key = self._primary_effect(effects or {})
        candidates = node_icon_candidates(None, effect_key, tab)
        size = int(r * 1.9)
        if IconAssets.draw(self.screen, candidates, cx, cy, size,
                           alpha=255 * min(1.0, max(0.25, strength))):
            return
        self._draw_skill_glyph(effects or {'craft_quality': 1}, cx, cy, r,
                               color, strength)

    def _draw_node_counter(self, nx, ny, count, total, learned):
        label = f"{count}/{total}"
        color = (126, 166, 226) if learned else (196, 184, 158)
        fw = T.FONT_TINY.size(label)[0]
        box = (nx - fw // 2 - 5, ny, fw + 10, 15)
        T.rounded_rect(self.screen, (14, 12, 10), box, radius=3,
                       border_color=(64, 56, 42))
        self.text(label, nx, ny + 7, color, font=T.FONT_TINY, center=True)

    def _draw_skill_node(self, nx, ny, r, node, selected, mouse):
        state = node.get('state')
        learned = bool(node.get('learned'))
        if learned:
            fill, border, glyph = (30, 38, 27), (208, 172, 86), (238, 216, 152)
        elif state == 'available':
            fill, border, glyph = (32, 29, 22), (170, 140, 70), (224, 202, 148)
        else:
            fill, border, glyph = (22, 20, 18), (66, 58, 45), (108, 98, 82)
        rect = (nx - r - 7, ny - r - 17, r * 2 + 14, r * 2 + 40)
        hover = self._in_rect(rect, mouse)
        if selected:
            border = (246, 228, 172)
        elif hover:
            border = T.shade(border, 1.3)
        pygame.draw.polygon(self.screen, (10, 9, 8), self._hex_points(nx, ny, r + 6))
        pygame.draw.polygon(self.screen, fill, self._hex_points(nx, ny, r))
        pygame.draw.polygon(self.screen, T.shade(fill, 1.6),
                            self._hex_points(nx, ny, r - 6), 1)
        pygame.draw.polygon(self.screen, border, self._hex_points(nx, ny, r + 3), 3)
        if selected:
            pygame.draw.polygon(self.screen, (252, 238, 196),
                                self._hex_points(nx, ny, r + 6), 1)
        strength = 1.0 if learned else (0.82 if state == 'available' else 0.5)
        self._draw_node_icon(node, nx, ny, r * 0.74, glyph, strength)
        self._draw_node_counter(nx, ny + r + 8, 1 if learned else 0, 1, learned)
        return rect

    def _draw_class_hub(self, cls, class_key, cx, cy, r, learned, total):
        border = T.GOLD
        pygame.draw.polygon(self.screen, (9, 8, 7), self._hex_points(cx, cy, r + 8))
        pygame.draw.polygon(self.screen, (44, 36, 23), self._hex_points(cx, cy, r + 3))
        pygame.draw.polygon(self.screen, (28, 25, 19), self._hex_points(cx, cy, r))
        pygame.draw.polygon(self.screen, border, self._hex_points(cx, cy, r + 3), 3)
        pygame.draw.polygon(self.screen, T.shade(border, 0.55),
                            self._hex_points(cx, cy, r - 6), 1)
        pygame.draw.circle(self.screen, T.shade(border, 0.4),
                           (int(cx), int(cy)), int(r * 0.64), 1)
        self._draw_class_icon(class_key, cls.get('effects'),
                              cx, cy - 2, r * 0.6, (232, 208, 154))
        self._draw_node_counter(cx, cy + r + 8, learned, total, True)

    def _draw_class_tree(self, game, nodes, cls, ctrl, x, y, cw, tree_bottom):
        """Единое древо: узлы текущего класса и ветки его эволюции.

        Ветки эволюции — не отдельный список под деревом, а нижний ярус
        того же древа: от центрального узла вниз идёт «ствол», который
        расходится на доступные классы следующей ступени.
        """
        top = y
        mouse = getattr(game, 'mouse_pos', (-1, -1))
        total_h = max(240, tree_bottom - top)

        # Нижний ярус под ветки эволюции.
        pr = max(15, min(22, int(total_h * 0.058)))
        promo_y = tree_bottom - pr - 34
        tree_h = max(150, promo_y - 64 - top)

        cx = x + cw // 2
        cy = top + int(tree_h * 0.55)
        r = max(20, min(28, int(tree_h * 0.085)))
        dx = min(152, int(cw * 0.13))
        dy = min(92, int(tree_h * 0.26))
        # Крылья поднимаются от класса вверх, венец — над центральным узлом.
        positions = [
            (cx - dx * 0.85, cy + dy * 0.78),
            (cx - dx * 1.68, cy + dy * 0.04),
            (cx - dx * 2.45, cy - dy * 0.88),
            (cx + dx * 0.85, cy + dy * 0.78),
            (cx + dx * 1.68, cy + dy * 0.04),
            (cx + dx * 2.45, cy - dy * 0.88),
            (cx, top + r + 12),
        ]
        pos = [positions[i % len(positions)] for i in range(len(nodes))]
        hub = (cx, cy)
        learned_count = sum(1 for n in nodes if n.get('learned'))

        # Тусклый декоративный круг под древом — как фон-орнамент.
        ring = int(min(tree_h * 0.46, cw * 0.3))
        pygame.draw.circle(self.screen, (44, 38, 29), (int(cx), int(cy)), ring, 1)
        pygame.draw.circle(self.screen, (33, 29, 23), (int(cx), int(cy)),
                           int(ring * 0.72), 1)

        # Связи: золотые к изученным узлам, тусклые пунктирные — к прочим.
        pairs = ((None, 0), (0, 1), (1, 2), (None, 3), (3, 4), (4, 5),
                 (None, 6))
        for a, b in pairs:
            if b >= len(pos) or (a is not None and a >= len(pos)):
                continue
            pa = hub if a is None else pos[a]
            pb = pos[b]
            done = bool(nodes[b].get('learned')) and (
                a is None or nodes[a].get('learned'))
            if done:
                pygame.draw.line(self.screen, (198, 162, 74), pa, pb, 3)
                pygame.draw.circle(self.screen, (236, 208, 132),
                                   (int(pb[0]), int(pb[1])), 2)
            else:
                self._dotted_line(pa, pb, (92, 79, 56), 2, 7, 6)

        game._ui_hits['skills']['rows'] = []
        hovered = None
        for i, node in enumerate(nodes):
            nx, ny = pos[i]
            rect = self._draw_skill_node(int(nx), int(ny), r, node,
                                         i == ctrl.selection, mouse)
            game._ui_hits['skills']['rows'].append((i, rect))
            if self._in_rect(rect, mouse):
                hovered = node
        self._draw_class_hub(cls, game.player.class_key, cx, cy, r + 12,
                             learned_count, len(nodes))

        # Подпись яруса эволюции — слева, чтобы не мешать стволу.
        self.text("ЭВОЛЮЦИЯ", x + 6, promo_y - 6, (150, 136, 108),
                  font=T.FONT_TINY)

        # Смена класса — такие же узлы древа, связанные с центром.
        promo_hover = self._draw_promotion_nodes(
            game, ctrl, cx, promo_y, cw, r, mouse,
            hub=(cx, cy + r + 14))
        if promo_hover is not None:
            key = promo_hover
            from core.data import class_data as _class_data
            target = _class_data(key)
            req = ctrl.change_class_requirement()
            ready = req is not None and game.player.level >= req
            lines = [(target.get('desc', ''), T.TEXT_DIM, T.FONT_SMALL)]
            if target.get('perk'):
                lines.append((target['perk'], (200, 180, 130), T.FONT_TINY))
            lines.append(("Клик — сменить класс; ветка прежнего класса "
                          "сбросится с возвратом очков.",
                          T.TEXT_MUTED, T.FONT_TINY))
            if not ready:
                if req is None:
                    lines.insert(0, ("Вершина ветки — смена недоступна.",
                                     T.TEXT_MUTED, T.FONT_SMALL))
                else:
                    lines.insert(0, (f"Нужен {req}-й уровень.", T.HP_CRIT,
                                     T.FONT_SMALL))
            else:
                lines.insert(0, ("Доступно для смены класса.", (240, 210, 96),
                                 T.FONT_SMALL))
            self._draw_tooltip(game, target.get('name', key), lines, mouse)
        elif hovered is not None:
            self._draw_node_tooltip(game, hovered, mouse)

    def _promotion_positions(self, ctrl, cx, cw, cy):
        options = ctrl.promotion_options()
        if not options:
            return []
        spacing = min(160, max(104, int(cw / (len(options) + 1))))
        total = spacing * (len(options) - 1)
        left = cx - total / 2
        return [(left + i * spacing, cy) for i in range(len(options))]

    def _draw_promotion_nodes(self, game, ctrl, cx, cy, cw, r, mouse,
                              hub=None):
        """Узлы эволюции класса — нижний ярус единого древа."""
        from core.data import class_data
        options = ctrl.promotion_options()
        if not options:
            return None
        req = ctrl.change_class_requirement()
        ready = req is not None and game.player.level >= req
        positions = self._promotion_positions(ctrl, cx, cw, cy)
        pr = max(16, r - 6)
        hovered = None
        line_color = (198, 162, 74) if ready else (92, 79, 56)

        # Ствол от центрального узла вниз и общая ветвь-шина, от которой
        # отходят узлы эволюции. Так дерево читается как одно целое.
        bus_y = cy - pr - 24
        if hub is not None:
            if ready:
                pygame.draw.line(self.screen, line_color, hub,
                                 (cx, bus_y), 3)
            else:
                self._dotted_line(hub, (cx, bus_y), line_color, 2, 6, 6)
        if len(positions) > 1:
            left_x = positions[0][0]
            right_x = positions[-1][0]
            if ready:
                pygame.draw.line(self.screen, line_color,
                                 (left_x, bus_y), (right_x, bus_y), 2)
            else:
                self._dotted_line((left_x, bus_y), (right_x, bus_y),
                                  line_color, 2, 6, 6)

        for i, (key, (nx, ny)) in enumerate(zip(options, positions)):
            cls = class_data(key)
            rect = (nx - (positions[1][0] - positions[0][0]) // 2 + 3
                    if len(positions) > 1 else nx - 60,
                    ny - pr - 8,
                    (positions[1][0] - positions[0][0]) - 6
                    if len(positions) > 1 else 120,
                    pr * 2 + 36)
            hover = self._in_rect(rect, mouse)
            if hover:
                hovered = key
            # Ветвь от шины к узлу эволюции.
            if ready:
                pygame.draw.line(self.screen, line_color,
                                 (nx, bus_y), (nx, ny - pr - 2), 2)
            else:
                self._dotted_line((nx, bus_y), (nx, ny - pr - 2),
                                  line_color, 1, 5, 5)
            border = T.GOLD if ready else (92, 82, 64)
            if hover:
                border = (252, 232, 172)
            pygame.draw.polygon(self.screen, (10, 9, 8),
                                self._hex_points(nx, ny, pr + 4))
            pygame.draw.polygon(self.screen, (30, 27, 21),
                                self._hex_points(nx, ny, pr))
            pygame.draw.polygon(self.screen, border,
                                self._hex_points(nx, ny, pr + 2), 2)
            self._draw_class_icon(key, cls.get('effects'),
                                  nx, ny, pr * 0.72,
                                  tuple(cls.get('color', (200, 200, 200))),
                                  1.0 if ready else 0.6)
            label = self.fit_text(cls.get('name', key),
                                  rect[2] - 8, T.FONT_TINY)
            self.text(label, nx, ny + pr + 5,
                      T.GOLD if ready else T.TEXT_MUTED,
                      font=T.FONT_TINY, center=True)
            if not ready and req is not None:
                self.text(f"с {req} ур.", nx, ny + pr + 19,
                          T.HP_CRIT, font=T.FONT_TINY, center=True)
            game._ui_hits['skills']['promotion'].append((i, rect))
        return hovered

    def _node_status(self, node):
        state = node.get('state')
        need = node.get('level')
        return {
            'learned': ("✓ Изучено", (120, 208, 120)),
            'available': ("Доступно · Enter или повторный клик — изучить",
                          (240, 210, 96)),
            'level': (f"Откроется на {need} уровне", (176, 162, 132)),
            'points': ("Нет очков навыков", (212, 158, 112)),
        }.get(state, ("", T.TEXT_DIM))

    def _draw_node_tooltip(self, game, node, mouse):
        """Подсказка узла при наведении — без выбора и клика."""
        from core import skills as sk
        status, status_color = self._node_status(node)
        need = node.get('level', 1)
        p = game.player
        effects = sk.describe_effects(node.get('effects', {}))
        lines = [
            (f"Уровень {need}", T.GOLD if p.level >= need else T.HP_CRIT,
             T.FONT_SMALL),
            (effects or "Пассивных бонусов нет.", T.TEXT, T.FONT_SMALL),
        ]
        if status:
            lines.append((status, status_color, T.FONT_TINY))
        self._draw_tooltip(game, node.get('name', 'Узел'), lines, mouse)

    def _draw_tooltip(self, game, title, lines, mouse):
        """Небольшая панель у курсора, прижатая к границам окна."""
        pad = 10
        inner = 320
        w = T.FONT_H2.size(title)[0]
        for text, _color, font in lines:
            w = max(w, min(inner, font.size(text)[0]))
        w = min(360, w + pad * 2)
        line_h = 18
        h = 30 + len(lines) * line_h + pad
        cx0, cy0, cw, ch = game._modal_content_rect
        mx, my = mouse
        x = mx + 18
        y = my + 16
        x = max(cx0 + 4, min(x, cx0 + cw - w - 4))
        y = max(cy0 + 4, min(y, cy0 + ch - h - 4))
        T.rounded_rect(self.screen, (18, 16, 13), (x, y, w, h), radius=8,
                       border_color=T.TITLE)
        self.text(self.fit_text(title, w - pad * 2, T.FONT_H2),
                  x + pad, y + 7, T.TITLE_LIGHT, font=T.FONT_H2)
        T.divider(self.screen, x + pad, y + 27, x + w - pad)
        for i, (text, color, font) in enumerate(lines):
            self.text(self.fit_text(text, w - pad * 2, font),
                      x + pad, y + 33 + i * line_h, color, font=font)

    def _draw_selected_node_panel(self, sk, nodes, ctrl, x, y, cw, ch, p):
        panel = (x, y, cw, ch)
        T.rounded_rect(self.screen, (24, 21, 17), panel, radius=8,
                       border_color=T.PANEL_BORDER)
        node = nodes[ctrl.selection] if nodes else None
        if not node:
            self.text("Нет узлов в этой ветке.", x + 16, y + 12,
                      T.TEXT_MUTED, font=T.FONT_SMALL)
            return
        state = node.get('state')
        self.text(self.fit_text(node['name'], cw - 200, T.FONT_H2),
                  x + 16, y + 8, T.TITLE_LIGHT, font=T.FONT_H2)
        need = node['level']
        self.text(f"Уровень {need}", x + cw - 16, y + 12,
                  T.GOLD if p.level >= need else T.HP_CRIT,
                  font=T.FONT_SMALL, align_right=True)
        effects = sk.describe_effects(node.get('effects', {}))
        self.text(self.fit_text(effects or "Пассивных бонусов нет.",
                                cw - 32, T.FONT_SMALL),
                  x + 16, y + 37, T.TEXT, font=T.FONT_SMALL)
        status = {
            'learned': ("✓ Изучено", (120, 208, 120)),
            'available': ("Доступно · Enter или повторный клик — изучить за 1 очко",
                          (240, 210, 96)),
            'level': (f"Откроется на {need} уровне", (176, 162, 132)),
            'points': ("Нет очков навыков", (212, 158, 112)),
        }.get(state, ("", T.TEXT_DIM))
        self.text(status[0], x + 16, y + 60, status[1], font=T.FONT_SMALL)

    def _draw_class_tab(self, game, x, y, cw, ch, cls, ctrl, sk, class_data):
        p = game.player
        bottom = game._modal_content_rect[1] + ch
        perk = cls.get('perk', '')
        if perk:
            self.text(self.fit_text(perk, cw, T.FONT_SMALL), x, y,
                      (200, 180, 130), font=T.FONT_SMALL)
            y += 24

        nodes = ctrl.rows()
        panel_h = 84
        panel_y = bottom - panel_h - 30

        # Древо — в собственном внутреннем фрейме, отдельно от подсказки.
        box = (x, y + 2, cw, max(140, panel_y - 12 - (y + 2)))
        T.rounded_rect(self.screen, (21, 19, 15), box, radius=10,
                       border_color=(74, 62, 45))
        T.rounded_rect(self.screen, (17, 15, 12),
                       (box[0] + 4, box[1] + 4, box[2] - 8, box[3] - 8),
                       radius=8, border_color=(48, 42, 33))
        self.text("ДРЕВО КЛАССА", box[0] + 16, box[1] + 8, T.TEXT_DIM,
                  font=T.FONT_TINY)

        self._draw_class_tree(game, nodes, cls, ctrl,
                              box[0] + 16, box[1] + 28,
                              box[2] - 32, box[1] + box[3] - 8)
        self._draw_selected_node_panel(sk, nodes, ctrl, x, panel_y, cw,
                                       panel_h, p)

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
        bottom = game._modal_content_rect[1] + ch
        panel_h = 104
        panel_y = bottom - panel_h - 30

        # Отдельный внутренний фрейм вокруг ремесленного древа.
        box = (x, y + 2, cw, max(140, panel_y - 12 - (y + 2)))
        T.rounded_rect(self.screen, (21, 19, 15), box, radius=10,
                       border_color=(74, 62, 45))
        T.rounded_rect(self.screen, (17, 15, 12),
                       (box[0] + 4, box[1] + 4, box[2] - 8, box[3] - 8),
                       radius=8, border_color=(48, 42, 33))

        hovered = self._draw_craft_tree(game, nodes, ctrl, tab, level,
                                        box[0] + 16, box[1] + 14,
                                        box[2] - 32, box[1] + box[3] - 8)
        self._draw_craft_node_panel(sk, tab, nodes, ctrl, level,
                                    hovered, x, panel_y, cw, panel_h)

    # Цвет и значок по умолчанию для ремесленных веток.
    _CRAFT_GLYPH = {
        'blacksmith': ({'craft_quality': 1}, (224, 188, 126)),
        'alchemy': ({'mana': 1}, (120, 190, 150)),
        'magic_engineering': ({'attack_speed': 1}, (170, 190, 240)),
    }

    def _draw_craft_hub(self, tab, cx, cy, r, level):
        effects, color = self._CRAFT_GLYPH.get(
            tab, ({'craft_quality': 1}, T.GOLD))
        pygame.draw.polygon(self.screen, (9, 8, 7),
                            self._hex_points(cx, cy, r + 8))
        pygame.draw.polygon(self.screen, (38, 32, 22),
                            self._hex_points(cx, cy, r + 3))
        pygame.draw.polygon(self.screen, (26, 23, 18),
                            self._hex_points(cx, cy, r))
        pygame.draw.polygon(self.screen, T.GOLD,
                            self._hex_points(cx, cy, r + 3), 3)
        self._draw_craft_hub_icon(tab, effects, cx, cy - 2, r * 0.6, color)
        self._draw_node_counter(cx, cy + r + 8, level, 25, True)

    def _craft_tree_positions(self, cx, cy, cw, tree_h, count):
        r = max(20, min(28, int(tree_h * 0.075)))
        dx = min(150, int(cw * 0.12))
        dy = min(88, int(tree_h * 0.24))
        left = [
            (cx - dx * 0.85, cy + dy * 0.85),
            (cx - dx * 1.55, cy + dy * 0.20),
            (cx - dx * 2.10, cy - dy * 0.55),
            (cx - dx * 2.45, cy - dy * 1.35),
        ]
        right = [(2 * cx - px, py) for px, py in left]
        pos = (left + right)[:count]
        return pos, r

    def _draw_craft_tree(self, game, nodes, ctrl, tab, level,
                         x, y, cw, tree_bottom):
        """Ремесленная ветка в том же шестиугольном формате, что и класс."""
        top = y
        tree_h = max(200, tree_bottom - top)
        cx = x + cw // 2
        cy = top + int(tree_h * 0.55)
        pos, r = self._craft_tree_positions(cx, cy, cw, tree_h, len(nodes))
        if not pos:
            return None
        self._draw_craft_hub(tab, cx, cy, r + 12, level)

        # Связи: центр → первые узлы крыльев, затем цепочка в каждом крыле.
        pairs = [(None, 0), (None, 4)]
        for a, b in ((0, 1), (1, 2), (2, 3), (4, 5), (5, 6), (6, 7)):
            if b < len(nodes):
                pairs.append((a, b))
        for a, b in pairs:
            if b >= len(pos) or (a is not None and a >= len(pos)):
                continue
            pa = (cx, cy) if a is None else pos[a]
            pb = pos[b]
            done = level >= nodes[b]['level'] and (
                a is None or level >= nodes[a]['level'])
            if done:
                pygame.draw.line(self.screen, (198, 162, 74), pa, pb, 3)
            else:
                self._dotted_line(pa, pb, (92, 79, 56), 2, 6, 6)

        effects, _color = self._CRAFT_GLYPH.get(
            tab, ({'craft_quality': 1}, T.GOLD))
        mouse = getattr(game, 'mouse_pos', (-1, -1))
        hovered = None
        game._ui_hits['skills']['rows'] = []
        for i, node in enumerate(nodes):
            nx, ny = pos[i]
            unlocked = level >= node['level']
            view = {
                **node,
                'state': 'learned' if unlocked else 'level',
                'learned': unlocked,
                'effects': node.get('effects') or effects,
            }
            rect = self._draw_skill_node(int(nx), int(ny), r, view,
                                         i == ctrl.selection, mouse)
            game._ui_hits['skills']['rows'].append((i, rect))
            if self._in_rect(rect, mouse):
                hovered = i
        if hovered is not None:
            node = nodes[hovered]
            unlocked = level >= node['level']
            need = node['level']
            effects_text = None
            from core import skills as _sk
            effects_text = _sk.describe_effects(node.get('effects', {}))
            recipes = _sk.recipes_for_level(tab, need)
            lines = [
                (f"Уровень {need}", T.GOLD if unlocked else T.HP_CRIT,
                 T.FONT_SMALL),
                (effects_text or "Новый уровень навыка.", T.TEXT,
                 T.FONT_SMALL),
            ]
            if recipes:
                names = ', '.join(r[1].get('name', r[0]) for r in recipes)
                lines.append((self.fit_text("Рецепты: " + names, 300,
                                            T.FONT_TINY),
                              T.TEXT_MUTED, T.FONT_TINY))
            lines.append(("Доступно" if unlocked
                          else f"Откроется на {need} уровне",
                          (120, 208, 120) if unlocked
                          else (176, 162, 132), T.FONT_TINY))
            self._draw_tooltip(game, node['name'], lines, mouse)
        return hovered

    def _draw_craft_node_panel(self, sk, tab, nodes, ctrl, level, hovered,
                               x, y, cw, ch):
        panel = (x, y, cw, ch)
        T.rounded_rect(self.screen, (24, 21, 17), panel, radius=8,
                       border_color=T.PANEL_BORDER)
        index = hovered if hovered is not None else ctrl.selection
        node = nodes[index] if nodes and 0 <= index < len(nodes) else None
        if not node:
            self.text("Нет узлов в этой ветке.", x + 16, y + 12,
                      T.TEXT_MUTED, font=T.FONT_SMALL)
            return
        need = node['level']
        unlocked = level >= need
        self.text(self.fit_text(node['name'], cw - 200, T.FONT_H2),
                  x + 16, y + 8, T.TITLE_LIGHT, font=T.FONT_H2)
        self.text(f"Уровень {need}", x + cw - 16, y + 12,
                  T.GOLD if unlocked else T.HP_CRIT,
                  font=T.FONT_SMALL, align_right=True)
        effects = sk.describe_effects(node.get('effects', {}))
        self.text(self.fit_text(effects or "Новый уровень навыка.",
                                cw - 32, T.FONT_SMALL),
                  x + 16, y + 37, T.TEXT, font=T.FONT_SMALL)
        recipes = sk.recipes_for_level(tab, need)
        if recipes:
            names = ', '.join(r[1].get('name', r[0]) for r in recipes)
            self.text(self.fit_text("Рецепты: " + names, cw - 32, T.FONT_TINY),
                      x + 16, y + 58, T.TEXT_MUTED, font=T.FONT_TINY)
        status = ("Доступно" if unlocked else
                  f"Откроется на уровне навыка {need}")
        self.text(status, x + 16, y + 78,
                  (120, 208, 120) if unlocked else (176, 162, 132),
                  font=T.FONT_SMALL)

    # ════════════════════════ КРАФТ ════════════════════════
    def _draw_tabs(self, game, tabs, active, x, y, cw, prefix):
        """Общий ряд вкладок по центру. Возвращает (новый_y, hits)."""
        from core import skills as sk
        hits = []
        labels = [sk.TAB_SHORT.get(tab, tab) for tab in tabs]
        max_w = (cw - 8 * (len(tabs) - 1)) // max(1, len(tabs))
        widths = [min(max_w, max(96, T.FONT_SMALL.size(label)[0] + 44))
                  for label in labels]
        total = sum(widths) + 8 * (len(tabs) - 1)
        tx = x + max(0, (cw - total) // 2)
        for tab, label, tab_w in zip(tabs, labels, widths):
            rect = (tx, y, tab_w, 30)
            is_active = tab == active
            hover = self._in_rect(rect, getattr(game, 'mouse_pos', (-1, -1)))
            bg = T.ROW_HOVER if is_active or hover else (30, 27, 21)
            border = T.TITLE if is_active else T.PANEL_BORDER
            T.rounded_rect(self.screen, bg, rect, radius=6,
                           border_color=border)
            self.text(label, tx + tab_w // 2, y + 15,
                      T.TITLE_LIGHT if is_active else T.TEXT_DIM,
                      font=T.FONT_SMALL, center=True)
            hits.append((tab, rect))
            tx += tab_w + 8
        return y + 40, hits

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
    @staticmethod
    def _wrap_console(text, font, max_w):
        lines = []
        for raw in str(text).split('\n'):
            words = raw.split(' ')
            current = ''
            for word in words:
                candidate = word if not current else current + ' ' + word
                if not current or font.size(candidate)[0] <= max_w:
                    current = candidate
                else:
                    lines.append(current)
                    current = word
            lines.append(current)
        return lines or ['']

    def draw_dev_console(self, game):
        self._modal_bg(game)
        cx0, cy0, cw, ch = game._modal_content_rect
        ctrl = game.dev_console
        accent = (224, 120, 88)
        self.text("КОНСОЛЬ РАЗРАБОТЧИКА", cx0, cy0, accent, font=T.FONT_H1)
        self.text("F12 / ` — закрыть", self._modal_right(game), cy0 + 8,
                  T.TEXT_MUTED, font=T.FONT_SMALL, align_right=True)
        y = cy0 + 56

        locked = ctrl.locked_for()
        if locked > 0:
            self.text("Доступ временно заблокирован.", cx0, y, T.HP_CRIT,
                      font=T.FONT_BODY)
            self.text(f"Осталось: {int(locked) + 1} с.", cx0, y + 28,
                      T.TEXT_DIM, font=T.FONT_SMALL)
            self.text("Esc — закрыть.", cx0, y + 58, T.TEXT_MUTED,
                      font=T.FONT_SMALL)
            return

        if not ctrl.unlocked:
            self.text("Введите пароль разработчика.", cx0, y, T.TEXT,
                      font=T.FONT_BODY)
            box = (cx0, y + 30, min(cw, 560), 38)
            T.rounded_rect(self.screen, (18, 16, 13), box, radius=6,
                           border_color=(110, 86, 58))
            masked = '*' * len(ctrl.buffer)
            shown = self.fit_text(masked, box[2] - 56, T.FONT_BODY)
            self.text(shown, box[0] + 12, box[1] + 9,
                      T.TEXT, font=T.FONT_BODY)
            caret_x = box[0] + 12 + T.FONT_BODY.size(shown)[0]
            if caret_x < box[0] + box[2] - 20:
                self.text("_", caret_x + 2, box[1] + 8, T.GOLD,
                          font=T.FONT_BODY)
            if ctrl.error:
                self.text(ctrl.error, cx0, box[1] + box[3] + 10, T.HP_CRIT,
                          font=T.FONT_SMALL)
            self.text("Enter — проверить · Esc — закрыть",
                      cx0 + cw // 2, cy0 + ch - 20, T.TEXT_MUTED,
                      font=T.FONT_SMALL, center=True)
            return

        # ── Вывод консоли ──
        pad = 10
        out_rect = (cx0, y - 6, cw, (cy0 + ch) - (y - 6) - 46)
        T.rounded_rect(self.screen, (14, 13, 11), out_rect, radius=6,
                       border_color=(76, 64, 46))
        text_w = out_rect[2] - pad * 2
        wrapped = []
        for text, color in ctrl.lines:
            for part in self._wrap_console(text, T.FONT_SMALL, text_w):
                wrapped.append((part, color))
        max_lines = max(1, (out_rect[3] - pad * 2) // 18)
        visible = wrapped[-max_lines:]
        ly = out_rect[1] + pad
        for text, color in visible:
            self.text(self.fit_text(text, text_w, T.FONT_SMALL),
                      out_rect[0] + pad, ly, color, font=T.FONT_SMALL)
            ly += 18

        # ── Строка ввода ──
        input_rect = (cx0, cy0 + ch - 34, cw, 30)
        T.rounded_rect(self.screen, (22, 20, 16), input_rect, radius=6,
                       border_color=(110, 86, 58))
        prompt = ctrl.prompt
        self.text(prompt, input_rect[0] + 8, input_rect[1] + 6,
                  T.GOLD, font=T.FONT_BODY)
        prompt_w = T.FONT_BODY.size(prompt)[0]
        shown = self.fit_text(ctrl.buffer, cw - prompt_w - 40, T.FONT_BODY)
        self.text(shown, input_rect[0] + 8 + prompt_w, input_rect[1] + 6,
                  T.TEXT, font=T.FONT_BODY)
        caret_x = input_rect[0] + 10 + prompt_w + T.FONT_BODY.size(shown)[0]
        if caret_x < input_rect[0] + cw - 12:
            self.text("_", caret_x, input_rect[1] + 6, T.GOLD,
                      font=T.FONT_BODY)
        self.text("Enter — выполнить · ↑/↓ — история · Tab — дописать",
                  cx0 + cw // 2, cy0 + ch - 46, T.TEXT_MUTED,
                  font=T.FONT_TINY, center=True)

    # ════════════════════════ ДИАЛОГ ════════════════════════
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
    def draw_quests(self, game):
        self._modal_bg(game)
        cx0, cy0, cw, ch = game._modal_content_rect
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

        from core.data import BOSSES, MONSTERS
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

    # ════════════════════════ ОБМЕН МЕЖДУ ИГРОКАМИ ════════════════════════
    def draw_trade(self, game):
        """Окно обмена: предложения сторон, золото, подтверждение."""
        self._modal_bg(game)
        cx0, cy0, cw, ch = game._modal_content_rect
        self.text("ОБМЕН", cx0, cy0, (214, 178, 98), font=T.FONT_H1)
        hits = []
        game._ui_hits['trade'] = hits
        trade = getattr(game, 'trade', None)
        if not trade:
            self.text("Обмен не активен", cx0 + cw // 2, cy0 + 120,
                      T.TEXT_MUTED, font=T.FONT_H2, center=True)
            return

        my = str(game.my_id)
        players = trade.get('players', [])
        partner_id = None
        if my in players:
            partner_id = players[1] if players[0] == my else players[0]
        partner = game.remote_players.get(int(partner_id)) \
            if partner_id is not None else None

        # Фаза запроса: второй игрок ещё не принял
        if my not in trade.get('accepted', []):
            name = partner.class_name if partner else 'Игрок'
            self.text(f"{name} предлагает обмен", cx0 + cw // 2, cy0 + 110,
                      (238, 222, 188), font=T.FONT_H2, center=True)
            bw, bh = 220, 46
            for i, (action, label, color) in enumerate(
                    (('accept', 'Принять', (134, 190, 118)),
                     ('decline', 'Отказаться', (230, 120, 90)))):
                rect = (cx0 + cw // 2 - bw + i * (bw + 20), cy0 + 170, bw, bh)
                self._trade_button(label, rect, color)
                hits.append((action, rect))
            return

        my_offer = trade.get('offers', {}).get(my, {})
        other_offer = trade.get('offers', {}).get(partner_id, {})
        my_gold = trade.get('gold', {}).get(my, 0)
        other_gold = trade.get('gold', {}).get(partner_id, 0)
        ready = trade.get('ready', [])

        col_w = (cw - 60) // 2
        col_h = 320
        cols = (
            (cx0, 'Вы выкладываете', my_offer, my_gold, my in ready),
            (cx0 + col_w + 60, partner.class_name if partner else 'Соперник',
             other_offer, other_gold, partner_id in ready),
        )
        for col_x, title, offer, gold, is_ready in cols:
            T.rounded_rect(self.screen, (26, 22, 18), (col_x, cy0 + 56,
                                                       col_w, col_h),
                           radius=8, border_color=(90, 74, 48))
            self.text(title, col_x + 12, cy0 + 64, (238, 222, 188),
                      font=T.FONT_H2)
            self.text(f"Золото: {gold}", col_x + 12, cy0 + 96,
                      (240, 210, 90), font=T.FONT_BODY)
            y = cy0 + 128
            if is_ready:
                self.text("✔ Подтверждено", col_x + col_w - 12, cy0 + 96,
                          (134, 190, 118), font=T.FONT_BODY, align_right=True)
            if not offer:
                self.text("(пусто)", col_x + 12, y, T.TEXT_MUTED,
                          font=T.FONT_SMALL)
                y += 24
            for key, count in sorted(offer.items()):
                self.text(f"{item_display_name(key)} ×{count}",
                          col_x + 12, y, T.TEXT, font=T.FONT_SMALL)
                if col_x == cx0:      # свои предметы можно убрать
                    rect = (col_x + col_w - 32, y - 2, 26, 22)
                    self._trade_button('−', rect, (230, 120, 90))
                    hits.append((('remove', key), rect))
                y += 26
            if col_x == cx0 and y < cy0 + 56 + col_h - 30:
                # Кнопки золота
                by = cy0 + 56 + col_h - 96
                for i, (action, label) in enumerate(
                        (('gold-10', '-10'), ('gold+10', '+10'),
                         ('gold+100', '+100'), ('gold_reset', 'Сброс'))):
                    rect = (col_x + 12 + i * 78, by, 70, 26)
                    self._trade_button(label, rect, (206, 166, 82))
                    hits.append((action, rect))

        # Свои предметы: добавить по клику (до 12 позиций)
        self.text("Инвентарь (клик — добавить):", cx0, cy0 + 56 + col_h + 16,
                  (190, 172, 138), font=T.FONT_SMALL)
        inv = [(k, c) for k, c in sorted(game.player.inventory.items()) if c > 0]
        per_col = 6
        for i, (key, count) in enumerate(inv[:per_col * 2]):
            col = i // per_col
            row = i % per_col
            ix = cx0 + col * (col_w + 60)
            iy = cy0 + 56 + col_h + 40 + row * 26
            label = self.fit_text(f"{item_display_name(key)} ×{count}",
                                  col_w - 40, T.FONT_SMALL)
            self.text(label, ix, iy, T.TEXT, font=T.FONT_SMALL)
            rect = (ix + col_w - 34, iy - 3, 28, 22)
            self._trade_button('+', rect, (134, 190, 118))
            hits.append((('add', key), rect))

        # Кнопки внизу
        bw, bh = 220, 46
        bottom = cy0 + ch - bh - 8
        is_ready = my in ready
        ready_label = 'Готов ✔' if is_ready else 'Подтвердить'
        rect = (cx0, bottom, bw, bh)
        self._trade_button(ready_label, rect, (134, 190, 118))
        hits.append(('ready', rect))
        rect = (cx0 + cw - bw, bottom, bw, bh)
        self._trade_button('Отменить', rect, (230, 120, 90))
        hits.append(('cancel', rect))

    def _trade_button(self, label, rect, color):
        mx, my = pygame.mouse.get_pos()
        x, y, w, h = rect
        hover = x <= mx <= x + w and y <= my <= y + h
        bg = (66, 54, 38) if hover else (40, 34, 26)
        T.rounded_rect(self.screen, bg, rect, radius=6, border_color=color)
        self.text(label, x + w // 2, y + h // 2 - 2, color,
                  font=T.FONT_BODY, center=True)
