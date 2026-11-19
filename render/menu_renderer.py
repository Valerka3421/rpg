"""Главное меню, сохранения, настройки, выбор класса."""
import pygame

from core import config
from core.data import CLASS_ORDER, CLASSES, class_stats
from render.base_renderer import BaseRenderer
from render.theme import Theme as T


class MenuRenderer(BaseRenderer):
    def __init__(self, screen, icons):
        super().__init__(screen)
        self.icons = icons

    # ════════════════════════ ГЛАВНОЕ МЕНЮ ════════════════════════
    def draw_main(self, game):
        from render.menu_background import MenuBackground
        MenuBackground.draw(self.screen)
        overlay = pygame.Surface((config.SCREEN_W, config.SCREEN_H),
                                 pygame.SRCALPHA)
        overlay.fill((6, 5, 4, 70))
        self.screen.blit(overlay, (0, 0))
        cx = config.SCREEN_W // 2
        title_text = "ЭТЕЛЬГАРД"
        title_surf = T.render(title_text, (214, 178, 98), T.FONT_H1)
        shadow_surf = T.render(title_text, (0, 0, 0), T.FONT_H1)
        tx = cx - title_surf.get_width() // 2
        self.screen.blit(shadow_surf, (tx + 3, 103))
        self.screen.blit(title_surf, (tx, 100))

        line_y = 155
        pygame.draw.line(self.screen, (96, 78, 46),
                         (cx - 220, line_y), (cx + 220, line_y), 1)
        pygame.draw.line(self.screen, (176, 142, 82),
                         (cx - 150, line_y + 3), (cx + 150, line_y + 3), 1)
        self.text("Открытый мир · Подземелья · Дракон", cx, 172,
                  T.TEXT_DIM, font=T.FONT_SMALL, center=True)

        labels = game.menu.labels()
        for i, (idx, rect) in enumerate(game.menu.button_rects()):
            if i >= len(labels):
                break
            selected = (i == game.menu.selection)
            self.draw_button_bg(rect, selected)
            color = (238, 222, 188) if selected else (200, 190, 170)
            self.text(labels[i], rect[0] + rect[2] // 2, rect[1] + rect[3] // 2,
                      color, font=T.FONT_H2, center=True)
            if selected:
                ax = rect[0] - 22
                ay = rect[1] + rect[3] // 2
                pygame.draw.polygon(self.screen, (206, 166, 82),
                                    [(ax, ay - 7), (ax + 12, ay), (ax, ay + 7)])

    # ════════════════════════ КНОПКА «НАЗАД» ════════════════════════
    def draw_back_button(self, game):
        """Единая кнопка возврата на шаг вверх для всех подменю (стрелка)."""
        rect = game.menu.back_button_rect()
        if not rect:
            return
        x, y, w, h = rect
        mx, my = getattr(game, 'mouse_pos', (-1, -1))
        hover = x <= mx <= x + w and y <= my <= y + h
        self.draw_button_bg(rect, hover, radius=8)
        color = (238, 222, 188) if hover else (200, 190, 170)
        cx, cy = x + w // 2, y + h // 2
        shaft = max(8, min(w, h) // 4)
        head = max(6, min(w, h) // 5)
        tip = cx - shaft - head // 2
        pygame.draw.line(self.screen, color, (cx + shaft, cy),
                         (tip + head // 2, cy), 3)
        pygame.draw.polygon(self.screen, color, [
            (tip, cy),
            (tip + head, cy - head),
            (tip + head, cy + head),
        ])

    # ════════════════════════ Мультиплеер ════════════════════════
    def draw_menu_multiplayer(self, game):
        self.draw_gradient_bg((10, 9, 7), (30, 24, 18))
        cx = config.SCREEN_W // 2
        title = T.render("МУЛЬТИПЛЕЕР", (214, 178, 98), T.FONT_H1)
        self.screen.blit(title, (cx - title.get_width() // 2, 100))
        self.text("Играй с другом по локальной сети",
                  cx, 160, T.TEXT_DIM, font=T.FONT_SMALL, center=True)

        labels = game.menu._mp_labels()
        for i, (idx, rect) in enumerate(game.menu.button_rects()):
            if i >= len(labels):
                break
            selected = (i == game.menu.mp_selection)
            self.draw_button_bg(rect, selected)
            color = (238, 222, 188) if selected else (200, 190, 170)
            self.text(labels[i], rect[0] + rect[2] // 2,
                      rect[1] + rect[3] // 2, color,
                      font=T.FONT_H2, center=True)
            if selected:
                ax = rect[0] - 22
                ay = rect[1] + rect[3] // 2
                pygame.draw.polygon(self.screen, (206, 166, 82),
                                    [(ax, ay - 7), (ax + 12, ay), (ax, ay + 7)])

        # Пометка про сохранение
        if game.menu._has_mp_save():
            self.text("● Есть сохранение мультиплеера",
                      cx, 180, (134, 190, 118),
                      font=T.FONT_TINY, center=True)
        else:
            self.text("Сохранения нет",
                      cx, 180, T.TEXT_MUTED,
                      font=T.FONT_TINY, center=True)

        self.text("Esc — назад", cx, config.SCREEN_H - 40, T.TEXT_MUTED,
                  font=T.FONT_SMALL, center=True)
        self.draw_back_button(game)

    def draw_menu_mp_join(self, game):
        self.draw_gradient_bg((10, 9, 7), (30, 24, 18))
        cx = config.SCREEN_W // 2
        title = T.render("ПРИСОЕДИНИТЬСЯ", (214, 178, 98), T.FONT_H1)
        self.screen.blit(title, (cx - title.get_width() // 2, 100))

        self.text("Введи IP-адрес хоста:", cx, 200, T.TEXT,
                  font=T.FONT_BODY, center=True)

        # Поле ввода
        field_w, field_h = 460, 56
        field_x = cx - field_w // 2
        field_y = 240
        T.rounded_rect(self.screen, (24, 21, 17),
                       (field_x, field_y, field_w, field_h),
                       radius=8, border_color=(110, 91, 62))
        cursor = "_" if (pygame.time.get_ticks() // 500) % 2 else ""
        self.text(game.menu.mp_ip + cursor,
                  field_x + field_w // 2, field_y + field_h // 2,
                  (228, 220, 200), font=T.FONT_H2, center=True)

        self.text("Например: localhost или 192.168.1.5",
                  cx, field_y + field_h + 14,
                  T.TEXT_MUTED, font=T.FONT_SMALL, center=True)

        btn_labels = ["Подключиться"]
        for i, (idx, rect) in enumerate(game.menu.button_rects()):
            if i >= len(btn_labels):
                break
            # Первая кнопка всегда "selected" для визуальной привязки
            selected = (i == 0)
            self.draw_button_bg(rect, selected)
            color = (238, 222, 188) if selected else (200, 190, 170)
            self.text(btn_labels[i], rect[0] + rect[2] // 2,
                      rect[1] + rect[3] // 2, color,
                      font=T.FONT_H2, center=True)

        self.text("Enter — подключиться · Ctrl+V — вставить · Esc — назад",
                  cx, config.SCREEN_H - 40, T.TEXT_MUTED,
                  font=T.FONT_SMALL, center=True)
        self.draw_back_button(game)

    # ════════════════════════ СОХРАНЕНИЯ ════════════════════════
    def draw_saves(self, game):
        from core.save import save_slot_info
        self.draw_gradient_bg((10, 9, 7), (30, 24, 18))
        cx = config.SCREEN_W // 2
        title = "ЗАГРУЗИТЬ ИГРУ" if game.menu.saves_mode == 'load' else "СОХРАНИТЬ ИГРУ"
        title_surf = T.render(title, (214, 178, 98), T.FONT_H1)
        self.screen.blit(title_surf, (cx - title_surf.get_width() // 2, 80))

        for slot in (1, 2, 3):
            info = save_slot_info(slot)
            selected = (slot - 1 == game.menu.selection_saves)
            rects = game.menu.button_rects()
            if slot - 1 >= len(rects):
                break
            rect = rects[slot - 1][1]
            self.draw_button_bg(rect, selected)
            self._draw_slot(rect, slot, info, selected,
                             game.menu.saves_mode, game)
            if selected:
                ax = rect[0] - 22
                ay = rect[1] + rect[3] // 2
                pygame.draw.polygon(self.screen, (206, 166, 82),
                                    [(ax, ay - 7), (ax + 12, ay), (ax, ay + 7)])
        self.draw_back_button(game)

    def _draw_slot(self, rect, slot, info, selected, mode, game=None):
        from datetime import datetime

        from core.data import CLASSES

        x, y, w, h = rect
        icon_size = 44
        ix = x + 16
        iy = y + (h - icon_size) // 2

        # ── Иконка слева ──
        if info is None:
            T.rounded_rect(self.screen, (34, 29, 23),
                           (ix, iy, icon_size, icon_size),
                           radius=T.R_SM, border_color=(72, 62, 46))
            self.text("—", ix + icon_size // 2, iy + icon_size // 2,
                      T.TEXT_MUTED, font=T.FONT_H2, center=True)
        else:
            cls = CLASSES.get(info.get('class_key', 'default'),
                              CLASSES['default'])
            T.rounded_rect(self.screen, (30, 26, 21),
                           (ix, iy, icon_size, icon_size),
                           radius=T.R_SM, border_color=cls['color'])
            self.icons.draw_class_icon(info.get('class_key', 'default'),
                                       ix, iy, size=icon_size)

        # ── Заголовок слота ──
        slot_color = (238, 222, 188) if selected else (200, 190, 170)
        self.text(f"Слот {slot}", x + 76, y + 16, slot_color, font=T.FONT_H2)

        # ── Содержимое ──
        if info is None:
            hint = ("Пусто — клик сохранит сюда" if mode == 'save'
                    else "Пусто — начните новую игру")
            self.text(hint, x + 76, y + 54, T.TEXT_MUTED, font=T.FONT_BODY)
        else:
            cls = CLASSES.get(info.get('class_key', 'default'), CLASSES['default'])
            mode_name = "Открытый мир" if info['world_mode'] == 'overworld' \
                else f"Подземелье {info['depth']}"
            class_w = self.text(cls['name'], x + 76, y + 54, cls['color'],
                                font=T.FONT_BODY)
            self.text(f"   Ур.{info['level']}   ·   {mode_name}   ·   ${info['gold']}",
                      x + 76 + class_w, y + 54, T.TEXT, font=T.FONT_BODY)

            try:
                dt = datetime.fromtimestamp(info['mtime'])
                del_rect = game.menu.slot_delete_rect(slot - 1) if game else None
                date_right = (del_rect[0] - 10) if del_rect else (x + w - 54)
                self.text(dt.strftime("%d.%m.%Y %H:%M"), date_right, y + 18,
                          T.TEXT_MUTED, font=T.FONT_SMALL, align_right=True)
            except (KeyError, ValueError, OSError):
                pass

            if mode == 'save':
                self.text("клик перезапишет", x + w - 20, y + h - 24,
                          (208, 158, 82), font=T.FONT_TINY, align_right=True)

        # ── Мусорка — ТОЛЬКО для заполненного слота ──
        if game is not None and info is not None:
            del_rect = game.menu.slot_delete_rect(slot - 1)
            if del_rect:
                self._draw_trash_bin(del_rect)

    def _draw_trash_bin(self, rect):
        x, y, w, h = rect
        size = min(w, h)  # опорный размер для масштаба
        T.rounded_rect(self.screen, (60, 30, 30), rect,
                       radius=6, border_color=(180, 80, 80))
        cx = x + w // 2
        k = size / 32

        # Крышка
        lid_y = y + int(7 * k)
        pygame.draw.line(self.screen, (240, 120, 120),
                         (cx - int(8 * k), lid_y),
                         (cx + int(8 * k), lid_y), int(2 * k))
        pygame.draw.arc(self.screen, (240, 120, 120),
                        (cx - int(4 * k), lid_y - int(4 * k),
                         int(8 * k), int(6 * k)),
                        0, 3.14, int(2 * k))

        # Корпус (трапеция)
        top_y = lid_y + int(3 * k)
        bot_y = y + h - int(6 * k)  # ← h, не w
        top_hw = int(7 * k)
        bot_hw = int(5 * k)
        pygame.draw.polygon(self.screen, (240, 120, 120), [
            (cx - top_hw, top_y),
            (cx + top_hw, top_y),
            (cx + bot_hw, bot_y),
            (cx - bot_hw, bot_y),
        ], int(2 * k))

        for dx in (-3, 0, 3):
            pygame.draw.line(self.screen, (180, 80, 80),
                             (cx + int(dx * k), top_y + int(3 * k)),
                             (cx + int(dx * k * 0.85), bot_y - int(3 * k)),
                             int(1 * k))

    # ════════════════════════ НАСТРОЙКИ ════════════════════════
    def draw_settings(self, game):
        self.draw_gradient_bg((10, 9, 7), (30, 24, 18))
        cx = config.SCREEN_W // 2
        title = T.render("НАСТРОЙКИ", (214, 178, 98), T.FONT_H1)
        self.screen.blit(title, (cx - title.get_width() // 2, 100))

        current_res = tuple(game.settings.get('resolution', [1280, 720]))
        res_str = f"{current_res[0]}×{current_res[1]}"

        rows = [
            ("Разрешение", res_str),
            ("Полный экран", "Вкл" if game.settings['fullscreen'] else "Выкл"),
            ("Громкость", f"{game.settings['volume']} / 10"),
            ("Показывать FPS", "Вкл" if game.settings['show_fps'] else "Выкл"),
            ("Мини-карта", "Вкл" if game.settings.get('show_minimap', True) else "Выкл"),
            ("Управление", "→"),
        ]
        for i, (label, val) in enumerate(rows):
            rects = game.menu.button_rects()
            if i >= len(rects):
                break
            rect = rects[i][1]
            selected = (i == game.menu.selection_settings)
            self.draw_button_bg(rect, selected)
            x, y, w, h = rect

            cy = y + h // 2 - 11

            label_color = (238, 222, 188) if selected else (200, 190, 170)
            self.text(label, x + 28, cy, label_color, font=T.FONT_H2)

            val_zone_center = x + w - 100
            val_w = T.FONT_H2.size(val)[0]
            val_color = (206, 166, 82) if selected else T.TEXT_DIM
            self.text(val, val_zone_center, y + h // 2, val_color,
                      font=T.FONT_H2, center=True)

            if selected and i != 5:
                val_left = val_zone_center - val_w // 2
                val_right = val_zone_center + val_w // 2
                ay = y + h // 2
                lb = val_left - 16
                pygame.draw.polygon(self.screen, (176, 142, 82),
                                    [(lb, ay - 7), (lb - 10, ay), (lb, ay + 7)])
                rb = val_right + 16
                pygame.draw.polygon(self.screen, (206, 166, 82),
                                    [(rb, ay - 7), (rb + 10, ay), (rb, ay + 7)])
        self.draw_back_button(game)

    def draw_controls(self, game):
        self.draw_gradient_bg((10, 9, 7), (30, 24, 18))
        cx = config.SCREEN_W // 2
        title = T.render("УПРАВЛЕНИЕ", (214, 178, 98), T.FONT_H1)
        self.screen.blit(title, (cx - title.get_width() // 2, 60))

        # Данные: категория → [(клавиша, описание)]
        sections = [
            ("Движение", [
                ("W A S D / ↑↓←→", "Шаг"),
                ("Пробел", "Атака (по ближайшему врагу)"),
                ("E", "Собрать ресурс рядом"),
                ("T", "Говорить с NPC / магазин / доска"),
                ("Лестницы", "↓ вниз · ↑ наверх · выход на глубине 1"),
            ]),
            ("Окна", [
                ("I", "Инвентарь (1–9 — использовать)"),
                ("Shift+1–8", "Назначить предмет на быструю панель"),
                ("U / V", "Снять оружие / броню"),
                ("H / B / N", "Снять шлем / поножи / обувь"),
                ("C", "Характеристики (1–6 — вложить очко)"),
                ("K", "Крафт ([ / ] — размер партии)"),
                ("N", "Древо навыков"),
                ("L", "Квесты (Enter — взять/сдать)"),
                ("Tab", "Статистика забега"),
            ]),
            ("Карта и бой", [
                ("M", "Мини-карта вкл/выкл"),
                ("B", "Большая карта мира"),
                ("1–8", "Быстрая панель предметов"),
                ("F1–F10", "Активные навыки класса"),
                ("H", "Быстрое зелье лечения"),
            ]),
            ("Прочее", [
                ("F5 / F9", "Сохранить / загрузить"),
                ("PgUp/PgDn · [ ]", "Прокрутка журнала событий"),
                ("Home / End", "Журнал в начало / в конец"),
                ("Магазин", "1–9 — купить · S — продать"),
                ("", "R — обновить ассортимент"),
                ("F12 / `", "Консоль разработчика (по паролю)"),
                ("Esc", "Меню / закрыть окно"),
            ]),
        ]

        # Две колонки: слева движение и окна, справа карта/бой и прочее.
        columns = (sections[:2], sections[2:])
        col_starts = (100, 700)
        for column, col_x in zip(columns, col_starts):
            y = 132
            for cat, rows in column:
                self.text(cat, col_x, y, (214, 178, 98), font=T.FONT_H2)
                y += 32
                for key_str, desc in rows:
                    if key_str.strip():
                        self.text(key_str, col_x + 20, y, (196, 176, 138),
                                  font=T.FONT_BODY)
                    self.text(desc, col_x + 200, y, T.TEXT,
                              font=T.FONT_BODY)
                    y += 26
                y += 10

        self.text("Esc — назад", cx, config.SCREEN_H - 40, T.TEXT_MUTED,
                  font=T.FONT_SMALL, center=True)
        self.draw_back_button(game)

    # ════════════════════════ СОЗДАТЕЛИ ════════════════════════
    def draw_credits(self, game):
        self.draw_gradient_bg((10, 9, 7), (30, 24, 18))
        cx = config.SCREEN_W // 2
        title = T.render("СОЗДАТЕЛИ", (214, 178, 98), T.FONT_H1)
        self.screen.blit(title, (cx - title.get_width() // 2, 56))

        self.text("Игра разработана командой EVA", cx, 128,
                  T.TEXT_DIM, font=T.FONT_BODY, center=True)

        sections = [
            ("Создатели", ["Dodik", "Don't talk to people", "And don’t write"]),
            ("Художник", ["Лизабет еБатори"]),
        ]
        y = 190
        for cat, names in sections:
            self.text(cat, cx, y, (214, 178, 98), font=T.FONT_H2, center=True)
            y += 40
            for name in names:
                self.text(name, cx, y, T.TEXT, font=T.FONT_BODY, center=True)
                y += 32
            y += 16

        self.text("Все права на изображения принадлежат ей", cx, y + 8,
                  T.TEXT_MUTED, font=T.FONT_SMALL, center=True)

        self.text("от 11.2026", cx, config.SCREEN_H - 84, (206, 166, 82),
                  font=T.FONT_BODY, center=True)
        self.text("Esc — назад", cx, config.SCREEN_H - 40, T.TEXT_MUTED,
                  font=T.FONT_SMALL, center=True)
        self.draw_back_button(game)

    # ════════════════════════ ВЫБОР КЛАССА ════════════════════════
    # Роли базовых классов — для подписи в предпросмотре.
    _CLASS_ROLES = {
        'warrior': 'Воин ближнего боя',
        'mage': 'Маг стихий',
        'rogue': 'Мастер ловкости',
        'artisan': 'Мастер ремёсел',
    }

    def draw_class_select(self, game):
        """Слева — список классов, справа — крупный живой предпросмотр."""
        self.draw_gradient_bg((10, 9, 7), (30, 24, 18))
        cx = config.SCREEN_W // 2
        title = T.render("ВЫБОР КЛАССА", (214, 178, 98), T.FONT_H1)
        self.screen.blit(title, (cx - title.get_width() // 2, 30))
        self.text("Наведи или выбери класс — справа показано, как он выглядит "
                  "и с чем начинает", cx, 80, T.TEXT_DIM,
                  font=T.FONT_SMALL, center=True)

        rects = game.menu.button_rects()
        for i, (idx, rect) in enumerate(rects):
            if i >= len(CLASS_ORDER):
                break
            key = CLASS_ORDER[i]
            cls = CLASSES[key]
            selected = (i == game.menu.class_selection)
            self.draw_button_bg(rect, selected)
            self._draw_class_card(rect, cls, selected, key)
            if selected:
                ax = rect[0] - 16
                ay = rect[1] + rect[3] // 2
                pygame.draw.polygon(self.screen, (206, 166, 82),
                                    [(ax, ay - 7), (ax + 12, ay), (ax, ay + 7)])

        key = CLASS_ORDER[game.menu.class_selection]
        self._draw_class_preview(game, key, CLASSES[key])

        self.text("↑/↓ — выбор · Enter — начать · Esc — назад",
                  config.SCREEN_W - 70, config.SCREEN_H - 46, T.TEXT_MUTED,
                  font=T.FONT_SMALL, align_right=True)
        self.draw_back_button(game)

    def _draw_class_card(self, rect, cls, selected, class_key='default'):
        """Компактная карточка класса в левом списке."""
        x, y, w, h = rect
        color = cls['color']
        name_color = color if selected else T.TEXT_DIM
        self.text(cls['name'], x + 22, y + 12, name_color, font=T.FONT_H2)
        tier = cls.get('tier', 1)
        tier_label = f"{tier}-я ступень"
        self.text(tier_label, x + w - 20, y + 16, T.TEXT_MUTED,
                  font=T.FONT_TINY, align_right=True)
        self.text(self.fit_text(cls['desc'], w - 44, T.FONT_SMALL),
                  x + 22, y + 44, T.TEXT if selected else T.TEXT_MUTED,
                  font=T.FONT_SMALL)
        st = class_stats(class_key)
        self.text(f"STR {st.get('str', 5)} · AGI {st.get('agi', 5)} · "
                  f"VIT {st.get('vit', 5)} · LUK {st.get('luk', 5)}",
                  x + 22, y + 66, T.TEXT_MUTED, font=T.FONT_TINY)

    def _draw_class_preview(self, game, class_key, cls):
        """Крупная панель: спрайт, характеристики, перк и стартовый набор."""
        px, py, pw, ph = game.menu.class_preview_rect()
        color = cls['color']
        panel = pygame.Surface((pw, ph), pygame.SRCALPHA)
        panel.fill((26, 22, 18, 236))
        pygame.draw.rect(panel, (*color, 90), panel.get_rect(),
                         width=2, border_radius=12)
        self.screen.blit(panel, (px, py))

        # ── Заголовок ──
        self.text(cls['name'], px + 28, py + 18, color, font=T.FONT_H1)
        role = self._CLASS_ROLES.get(cls.get('base', class_key),
                                     'Приключенец')
        self.text(f"{role} · {cls.get('tier', 1)}-я ступень",
                  px + 28, py + 58, T.TEXT_DIM, font=T.FONT_SMALL)

        # ── Краткое описание класса ──
        desc_lines = self._wrap_text(cls.get('desc', ''),
                                     pw - 56, T.FONT_SMALL)[:2]
        desc_y = py + 78
        for line in desc_lines:
            self.text(line, px + 28, desc_y, T.TEXT, font=T.FONT_SMALL)
            desc_y += 18

        # ── Портрет персонажа ──
        start = game.menu.class_start_rect()
        limit_y = start[1] - 14
        portrait = min(180, max(140, int(ph * 0.26)))
        bx, by = px + 28, desc_y + 8
        pygame.draw.rect(self.screen, (16, 14, 11),
                         (bx, by, portrait, portrait), border_radius=10)
        pygame.draw.rect(self.screen, (*color, 120),
                         (bx, by, portrait, portrait), width=2,
                         border_radius=10)
        if self.icons is not None:
            self.icons.draw_player(class_key, bx + 4, by + 4,
                                   size=portrait - 8)
        self.text("как выглядит в игре", bx + portrait // 2, by + portrait + 6,
                  T.TEXT_MUTED, font=T.FONT_TINY, center=True)

        # ── Стартовый набор под портретом (левая колонка) ──
        gear_y = by + portrait + 26
        self.text("СТАРТОВЫЙ НАБОР", bx, gear_y, T.GOLD, font=T.FONT_SMALL)
        gear = self._start_gear(cls)
        gy = gear_y + 24
        for item in gear[:3]:
            self.text(f"• {item}", bx, gy, T.TEXT_DIM, font=T.FONT_SMALL)
            gy += 17
        if len(gear) > 3:
            self.text(f"• и ещё {len(gear) - 3}", bx, gy,
                      T.TEXT_MUTED, font=T.FONT_SMALL)

        # ── Характеристики в правой колонке ──
        sx = bx + portrait + 34
        stats = self._preview_stats(cls, class_key)
        chips = (
            ("ATK", str(stats['atk']), (222, 150, 110)),
            ("DEF", str(stats['df']), (150, 170, 200)),
            ("HP", str(stats['max_hp']), T.HP_CRIT),
            ("MP", str(stats['max_mana']), (120, 160, 255)),
            ("Уклонение", f"{int(stats['dodge'] * 100)}%", T.DODGE),
            ("Крит", f"{int(stats['crit'] * 100)}%", T.CRIT),
        )
        right_w = px + pw - 28 - sx
        gap = 8
        chip_w = max(96, (right_w - gap * 2) // 3)
        chip_h = 48
        for i, (label, value, chip_color) in enumerate(chips):
            col, row = i % 3, i // 3
            cxp = sx + col * (chip_w + gap)
            cyp = by + row * (chip_h + gap)
            T.rounded_rect(self.screen, (34, 29, 23),
                           (cxp, cyp, chip_w, chip_h), radius=8,
                           border_color=(70, 60, 46))
            self.text(label, cxp + 12, cyp + 5, T.TEXT_MUTED,
                      font=T.FONT_TINY)
            self.text(value, cxp + chip_w - 12, cyp + 18, chip_color,
                      font=T.FONT_H2, align_right=True)

        # ── Базовые атрибуты с полосками ──
        st = class_stats(class_key)
        attrs = (
            ("STR", st.get('str', 5), (220, 120, 100)),
            ("AGI", st.get('agi', 5), (140, 200, 130)),
            ("VIT", st.get('vit', 5), (220, 120, 110)),
            ("LUK", st.get('luk', 5), (220, 200, 110)),
            ("DEF", st.get('df_stat', 5), (150, 170, 210)),
            ("INT", st.get('int', 5), (130, 170, 240)),
        )
        ay = by + 2 * (chip_h + gap) + 8
        self.text("БАЗОВЫЕ ХАРАКТЕРИСТИКИ", sx, ay, T.GOLD,
                  font=T.FONT_SMALL)
        ay += 22
        max_val = 12
        for label, value, bar_color in attrs:
            self.text(label, sx, ay - 1, T.TEXT, font=T.FONT_SMALL)
            track = (sx + 52, ay + 4, max(24, right_w - 80), 10)
            T.bar(self.screen, track, min(1.0, value / max_val), bar_color,
                  bg_color=(20, 18, 15))
            self.text(str(value), px + pw - 28, ay - 1, bar_color,
                      font=T.FONT_SMALL, align_right=True)
            ay += 20

        # ── Перк ──
        perk = cls.get('perk') or cls.get('desc', '')
        self.text("ОСОБЕННОСТЬ", sx, ay + 6, T.GOLD, font=T.FONT_SMALL)
        ty = ay + 28
        for line in self._wrap_text(perk, right_w, T.FONT_SMALL):
            if ty > limit_y:
                break
            self.text(line, sx, ty, T.TEXT, font=T.FONT_SMALL)
            ty += 19

        # ── Кнопка «Начать» ──
        start = game.menu.class_start_rect()
        mx, my = getattr(game, 'mouse_pos', (-1, -1))
        hover = (start[0] <= mx <= start[0] + start[2]
                 and start[1] <= my <= start[1] + start[3])
        bg = T.shade(color, 1.15 if hover else 0.92)
        T.rounded_rect(self.screen, bg, start, radius=9,
                       border_color=T.shade(color, 1.4))
        label_color = (20, 16, 12) if sum(color) > 420 else (250, 246, 238)
        label = self.fit_text(f"ИГРАТЬ ЗА {cls['name'].upper()}",
                              start[2] - 20, T.FONT_H2)
        self.text(label, start[0] + start[2] // 2,
                  start[1] + 13, label_color, font=T.FONT_H2, center=True)

    def _start_gear(self, cls):
        from core.data import ITEMS
        gear = []
        if cls.get('start_weapon') and cls['start_weapon'] in ITEMS:
            gear.append(ITEMS[cls['start_weapon']]['name'])
        if cls.get('start_armor') and cls['start_armor'] in ITEMS:
            gear.append(ITEMS[cls['start_armor']]['name'])
        for k, v in (cls.get('start_items') or {}).items():
            if k in ITEMS:
                gear.append(f"{v}× {ITEMS[k]['name']}")
        return gear or ["Пустые руки — всё впереди"]

    def _wrap_text(self, text, max_w, font):
        """Разбивает строку по словам под заданную ширину."""
        words = str(text).split()
        lines, line = [], ''
        for word in words:
            test = f"{line} {word}".strip()
            if not line or font.size(test)[0] <= max_w:
                line = test
            else:
                lines.append(line)
                line = word
        if line:
            lines.append(line)
        return lines

    def _preview_stats(self, cls, class_key='default'):
        """Производные статы для предпросмотра класса (одна формула — Player)."""
        from core.entities import Player
        st = class_stats(class_key)
        stats = Player.preview_stats({
            'str': st.get('str', 5), 'agi': st.get('agi', 5),
            'vit': st.get('vit', 5), 'luk': st.get('luk', 5),
            'df_stat': st.get('df_stat', 5), 'level': 1,
            'int': st.get('int', 5),
            'weapon': cls.get('start_weapon'),
            'armor': cls.get('start_armor'),
            'effects': cls.get('effects', {}),
        })
        return stats

