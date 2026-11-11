"""Главное меню, сохранения, настройки, выбор класса."""
from core import config
import pygame
from render.theme import Theme as T
from core.data import CLASSES, CLASS_ORDER
from render.base_renderer import BaseRenderer


class MenuRenderer(BaseRenderer):
    def __init__(self, screen, icons):
        super().__init__(screen)
        self.icons = icons

    # ════════════════════════ ГЛАВНОЕ МЕНЮ ════════════════════════
    def draw_main(self, game):
        self.draw_gradient_bg((10, 9, 7), (30, 24, 18))
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

        btn_labels = ["Подключиться", "Назад"]
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

        self.text("Enter — подключиться · Esc — назад",
                  cx, config.SCREEN_H - 40, T.TEXT_MUTED,
                  font=T.FONT_SMALL, center=True)

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
                            game.menu.from_game, game.menu.saves_mode, game)
            if selected:
                ax = rect[0] - 22
                ay = rect[1] + rect[3] // 2
                pygame.draw.polygon(self.screen, (206, 166, 82),
                                    [(ax, ay - 7), (ax + 12, ay), (ax, ay + 7)])

    def _draw_slot(self, rect, slot, info, selected, from_game, mode, game=None):
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

        # ── Содержимое: пусто / заполнено ──
        if info is None:
            hint = ("Пусто — клик сохранит сюда" if mode == 'save'
                    else "Пусто — N начать новую игру")
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
                # Дата — левее мусорки (32 + 10 + зазор 12 = сдвиг 54)
                from core.config import MODAL_CONTENT_PAD
                del_rect = game.menu.slot_delete_rect(slot - 1) if game else None
                date_right = (del_rect[0] - 10) if del_rect else (x + w - 54)
                self.text(dt.strftime("%d.%m.%Y %H:%M"), date_right, y + 18,
                          T.TEXT_MUTED, font=T.FONT_SMALL, align_right=True)
            except Exception:
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
        x, y, size, _ = rect
        # Фон
        T.rounded_rect(self.screen, (60, 30, 30), rect,
                       radius=6, border_color=(180, 80, 80))
        cx = x + size // 2
        k = size / 32

        # Крышка
        lid_y = y + int(7 * k)
        pygame.draw.line(self.screen, (240, 120, 120),
                         (cx - int(8 * k), lid_y),
                         (cx + int(8 * k), lid_y), int(2 * k))
        # Ручка крышки (полукруг сверху)
        pygame.draw.arc(self.screen, (240, 120, 120),
                        (cx - int(4 * k), lid_y - int(4 * k),
                         int(8 * k), int(6 * k)),
                        0, 3.14, int(2 * k))

        # Корпус (трапеция: верх шире, низ уже)
        top_y = lid_y + int(3 * k)
        bot_y = y + size - int(6 * k)
        top_hw = int(7 * k)
        bot_hw = int(5 * k)
        pygame.draw.polygon(self.screen, (240, 120, 120), [
            (cx - top_hw, top_y),
            (cx + top_hw, top_y),
            (cx + bot_hw, bot_y),
            (cx - bot_hw, bot_y),
        ], int(2 * k))

        # Вертикальные полоски внутри
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

    def draw_controls(self, game):
        self.draw_gradient_bg((10, 9, 7), (30, 24, 18))
        cx = config.SCREEN_W // 2
        title = T.render("УПРАВЛЕНИЕ", (214, 178, 98), T.FONT_H1)
        self.screen.blit(title, (cx - title.get_width() // 2, 60))

        # Данные: категория → [(клавиша, описание)]
        sections = [
            ("Движение", [
                ("W A S D / ↑↓←→", "Шаг"),
                ("Пробел", "Атака (по ближайшему)"),
                ("Лестницы", "↓ вниз · ↑ наверх · выход на глубине 1"),
            ]),
            ("Окна", [
                ("I", "Инвентарь (1–9 — использовать)"),
                ("C", "Характеристики (1–5 — вложить очко)"),
                ("K", "Крафт (1–8 — создать)"),
                ("L", "Квесты (Enter — взять/сдать)"),
                ("T", "Диалог с NPC / магазин"),
                ("Tab", "Статистика забега"),
                ("Esc", "Меню / закрыть окно"),
            ]),
            ("Игровое", [
                ("H", "Быстрое зелье"),
                ("M", "Мини-карта вкл/выкл"),
                ("F5 / F9", "Сохранить / загрузить"),
                ("Магазин", "1–9 — купить · S — продать · R — обновить"),
            ]),
        ]

        col_x = 100
        y = 150
        for cat, rows in sections:
            self.text(cat, col_x, y, (214, 178, 98), font=T.FONT_H2)
            y += 32
            for key_str, desc in rows:
                self.text(key_str, col_x + 20, y, (196, 176, 138),
                          font=T.FONT_BODY)
                self.text(desc, col_x + 200, y, T.TEXT, font=T.FONT_BODY)
                y += 26
            y += 10

        self.text("Esc — назад", cx, config.SCREEN_H - 40, T.TEXT_MUTED,
                  font=T.FONT_SMALL, center=True)

    # ════════════════════════ ВЫБОР КЛАССА ════════════════════════
    def draw_class_select(self, game):
        self.draw_gradient_bg((10, 9, 7), (30, 24, 18))
        cx = config.SCREEN_W // 2
        title = T.render("ВЫБОР КЛАССА", (214, 178, 98), T.FONT_H1)
        self.screen.blit(title, (cx - title.get_width() // 2, 80))
        self.text("Каждый класс — уникальный старт", cx, 148,
                  T.TEXT_DIM, font=T.FONT_SMALL, center=True)

        for i, (idx, rect) in enumerate(game.menu.button_rects()):
            if i >= len(CLASS_ORDER):
                break
            key = CLASS_ORDER[i]
            cls = CLASSES[key]
            selected = (i == game.menu.class_selection)
            self.draw_button_bg(rect, selected)
            self._draw_class_card(rect, cls, selected)
            if selected:
                ax = rect[0] - 22
                ay = rect[1] + rect[3] // 2
                pygame.draw.polygon(self.screen, (206, 166, 82),
                                    [(ax, ay - 7), (ax + 12, ay), (ax, ay + 7)])


    def _draw_class_card(self, rect, cls, selected):
        x, y, w, h = rect
        color = cls['color']
        name_color = color if selected else T.TEXT_DIM
        self.text(cls['name'], x + 24, y + 14, name_color, font=T.FONT_H2)

        atk, df, hp, dodge, crit = self._preview_stats(cls)
        self.text(f"ATK {atk}   DEF {df}   HP {hp}   Укл {dodge}%   Крит {crit}%",
                  x + 24, y + 44, T.TEXT, font=T.FONT_SMALL)
        self.text(f"STR {cls['str']}   AGI {cls['agi']}   VIT {cls['vit']}   "
                  f"LUK {cls['luk']}   DEF {cls['df_stat']}",
                  x + 24, y + 64, T.TEXT_MUTED, font=T.FONT_TINY)

        self.text(cls['desc'], x + w - 24, y + 14, T.TEXT_DIM,
                  font=T.FONT_SMALL, align_right=True)

        gear = []
        from core.data import ITEMS
        if cls.get('start_weapon'):
            gear.append(ITEMS[cls['start_weapon']]['name'])
        if cls.get('start_armor'):
            gear.append(ITEMS[cls['start_armor']]['name'])
        for k, v in cls.get('start_items', {}).items():
            gear.append(f"{v}x {ITEMS[k]['name']}")
        self.text("Старт: " + (", ".join(gear) if gear else "ничего"),
                  x + w - 24, y + 44, T.TEXT_MUTED,
                  font=T.FONT_SMALL, align_right=True)

    def _preview_stats(self, cls):
        from core.entities import Player
        stats = Player.preview_stats({
            'str': cls['str'], 'agi': cls['agi'], 'vit': cls['vit'],
            'luk': cls['luk'], 'df_stat': cls['df_stat'],
            'weapon': cls.get('start_weapon'),
            'armor': cls.get('start_armor'),
        })
        return (stats['atk'], stats['df'], stats['max_hp'],
                int(stats['dodge'] * 100), int(stats['crit'] * 100))