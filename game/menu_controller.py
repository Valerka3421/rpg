"""Контроллер меню: главное, сейвы, настройки, выбор класса."""
from core import config
import sys
import pygame
from core.data import CLASS_ORDER
from core.save import (save_slot_path, save_slot_exists)
from core.settings import save_settings


# Допустимые символы адреса хоста: IPv4, имя хоста и порт.
_MP_HOST_CHARS = set('abcdefghijklmnopqrstuvwxyz0123456789.-:')


def read_clipboard_text():
    """Текст из системного буфера обмена (без падения, если он недоступен)."""
    try:
        if not pygame.scrap.get_init():
            pygame.scrap.init()
        data = pygame.scrap.get(pygame.SCRAP_TEXT)
        if data:
            if isinstance(data, bytes):
                data = data.decode('utf-8', 'ignore')
            return data.replace('\x00', '')
    except Exception:
        pass
    # Запасной путь для платформ, где scrap не работает (например, Wayland).
    try:
        import tkinter
        root = tkinter.Tk()
        root.withdraw()
        try:
            return root.clipboard_get()
        finally:
            root.destroy()
    except Exception:
        return ''


def _key_to_char(key):
    """Символ по коду клавиши, когда event.unicode пуст (раскладка/тесты)."""
    if pygame.K_a <= key <= pygame.K_z:
        return chr(key)
    if pygame.K_0 <= key <= pygame.K_9:
        return chr(key)
    if key in (pygame.K_PERIOD, pygame.K_KP_PERIOD):
        return '.'
    if key == pygame.K_MINUS:
        return '-'
    if key == pygame.K_COLON:
        return ':'
    return ''


class MenuController:
    MENU_START = ('start', 'load', 'multiplayer', 'settings', 'exit')
    MENU_PAUSE = ('resume', 'save', 'load', 'settings', 'exit_to_menu')

    def __init__(self, game):
        self.game = game
        self.selection = 0
        self.selection_saves = 0
        self.selection_settings = 0
        self.from_game = False
        self.saves_mode = 'load'
        self.class_selection = 0
        self.pending_class_key = 'warrior'
        self.mp_selection = 0
        self.mp_ip = "localhost"

    # ── Состояние ──
    def items(self):
        return self.MENU_PAUSE if self.from_game else self.MENU_START

    def labels(self):
        if self.from_game:
            return ["Вернуться в игру", "Сохранить игру", "Загрузить игру",
                    "Настройки", "Выйти в главное меню"]
        return ["Начать игру", "Загрузить игру", "Мультиплеер", "Настройки", "Выйти"]

    def open(self, from_game=False):
        self.game.screen_mode = 'menu'
        self.selection = 0
        self.from_game = from_game

    def back(self):
        self.game.screen_mode = 'menu'
        self.selection = 0

    def return_to_game(self):
        self.game.screen_mode = 'game'
        self.from_game = False

    def exit_game(self):
        save_settings(self.game.settings)
        pygame.quit()
        sys.exit()

    def exit_to_main_menu(self):
        self.game.save(silent=True)
        self.from_game = False
        self.saves_mode = 'load'
        self.selection = 0
        self.game.screen_mode = 'menu'

    # ── Активация ──
    def activate(self):
        items = self.items()
        if self.selection >= len(items):
            return
        choice = items[self.selection]
        if choice == 'resume':
            self.return_to_game()
        elif choice == 'start':
            self.start_new()
        elif choice == 'save':
            self.saves_mode = 'save'
            self.game.screen_mode = 'menu_saves'
            self.selection_saves = 0
        elif choice == 'load':
            self.saves_mode = 'load'
            self.game.screen_mode = 'menu_saves'
            self.selection_saves = 0
        elif choice == 'multiplayer':
            self.mp_selection = 0
            self.game.screen_mode = 'menu_multiplayer'
        elif choice == 'settings':
            self.game.screen_mode = 'menu_settings'
            self.selection_settings = 0
        elif choice == 'exit':
            self.exit_game()
        elif choice == 'exit_to_menu':
            self.exit_to_main_menu()

    def activate_index(self, idx):
        items = self.items()
        if 0 <= idx < len(items):
            self.selection = idx
            self.activate()

    def start_new(self):
        self.game.current_slot = 1
        self.pending_class_key = 'warrior'
        self.class_selection = 0
        self.game.screen_mode = 'class_select'

    def load_slot(self, slot):
        path = save_slot_path(slot)
        exists = save_slot_exists(slot)
        if self.saves_mode == 'save':
            self.game.current_slot = slot
            self.game.save(path=path, silent=True)
            self.game.log(f"Сохранено в слот {slot}.", (120, 240, 120))
            self.return_to_game()
            return
        if not exists:
            self.game.log(f"Слот {slot} пуст.", (110, 110, 130))
            return
        self.game.current_slot = slot
        self.game.load(path=path)
        self.from_game = False
        self.saves_mode = 'load'

    def new_in_slot(self, slot):
        if save_slot_exists(slot):
            return
        self.game.current_slot = slot
        self.pending_class_key = 'warrior'
        self.class_selection = 0
        self.game.screen_mode = 'class_select'

    def delete_slot(self, slot):
        from core.save import delete_slot as _delete_slot
        try:
            if _delete_slot(slot):
                self.game.log(f"Слот {slot} удалён.", (110, 110, 130))
        except Exception as e:
            self.game.log(f"Ошибка удаления: {e}", (255, 100, 100))
        # Сбросить выделение, если выбранный слот был удалён
        if self.selection_saves >= 3:
            self.selection_saves = 0

    def confirm_class(self):
        key = CLASS_ORDER[self.class_selection]
        self.pending_class_key = key
        self.game.new_game(class_key=key)
        self.from_game = False
        self.saves_mode = 'load'

    # ── Настройки ──
    def adjust_setting(self, delta):
        idx = self.selection_settings
        if idx == 0:  # Разрешение
            from core.config import RESOLUTIONS
            from core.settings import save_settings
            current = tuple(self.game.settings.get('resolution', [1280, 720]))
            try:
                i = RESOLUTIONS.index(current)
            except ValueError:
                i = 0
            i = (i + delta) % len(RESOLUTIONS)
            new_res = RESOLUTIONS[i]
            if tuple(new_res) != current:
                self.game.settings['resolution'] = list(new_res)
                # Смена разрешения — это смена размера ОКНА. В полноэкранном
                # режиме размер окна не виден, поэтому выходим из него.
                self.game.settings['fullscreen'] = False
                save_settings(self.game.settings)
                self.game.pending_resolution = tuple(new_res)
        elif idx == 1:  # Полный экран
            self.game.settings['fullscreen'] = not self.game.settings['fullscreen']
        elif idx == 2:  # Громкость
            self.game.settings['volume'] = max(0, min(10,
                                                      self.game.settings['volume'] + delta))
            from audio import Audio
            Audio.set_volume(self.game.settings['volume'])
        elif idx == 3:  # FPS
            self.game.settings['show_fps'] = not self.game.settings['show_fps']
        elif idx == 4:  # Мини-карта
            self.game.settings['show_minimap'] = not self.game.settings.get('show_minimap', True)
        elif idx == 5:  # Управление
            self.game.screen_mode = 'menu_controls'

    def save_settings_and_back(self):
        save_settings(self.game.settings)
        self.back()

    # ── Ректы кнопок ──
    def button_rects(self):
        m = self.game.screen_mode
        if m == 'menu':
            n = len(self.items())
            bw, bh = 400, 56
            bx = (config.SCREEN_W - bw) // 2
            by = 200
            return [(i, (bx, by + i * 80, bw, bh)) for i in range(n)]
        elif m == 'menu_saves':
            bw, bh = 700, 110
            bx = (config.SCREEN_W - bw) // 2
            by = 180
            return [(i, (bx, by + i * (bh + 20), bw, bh)) for i in range(3)]
        elif m == 'menu_settings':
            bw, bh = 700, 46
            bx = (config.SCREEN_W - bw) // 2
            by = 160
            return [(i, (bx, by + i * 60, bw, bh)) for i in range(6)]
        elif m == 'class_select':
            bw, bh = 800, 92
            bx = (config.SCREEN_W - bw) // 2
            by = 160
            return [(i, (bx, by + i * (bh + 14), bw, bh))
                    for i in range(len(CLASS_ORDER))]
        if m == 'menu_multiplayer':
            n = len(self._mp_items())
            bw, bh = 460, 60
            bx = (config.SCREEN_W - bw) // 2
            # Центр по вертикали: 2 кнопки → 240, 3 кнопки → 200
            by = 240 if n == 2 else 200
            return [(i, (bx, by + i * 80, bw, bh)) for i in range(n)]
        elif m == 'menu_mp_join':
            bw, bh = 460, 56
            bx = (config.SCREEN_W - bw) // 2
            by = 380
            return [
                (0, (bx, by, bw, bh)),          # Подключиться
                (1, (bx, by + 76, bw, bh)),     # Назад
            ]
        return []

    def _has_mp_save(self):
        """Есть ли сохранение мультиплеера (мир или игроки)."""
        import os
        from core.config import DATA_DIR
        for fname in ('savegame_mp_world.json', 'savegame_mp_players.json'):
            if os.path.exists(os.path.join(DATA_DIR, fname)):
                return True
        return False

    def _mp_items(self):
        """Ключи кнопок меню мультиплеера."""
        if self._has_mp_save():
            return ['continue', 'new', 'join']
        return ['create', 'join']

    def _mp_labels(self):
        """Тексты кнопок (для рендера)."""
        if self._has_mp_save():
            return ['Продолжить игру', 'Начать новую игру', 'Присоединиться']
        return ['Создать игру', 'Присоединиться к игре']

    def _activate_mp(self):
        items = self._mp_items()
        if self.mp_selection >= len(items):
            self.mp_selection = 0
            return
        choice = items[self.mp_selection]

        if choice == 'continue':
            self.game.pending_multiplayer = {
                'mode': 'mp_host',
                'port': 9000,
                'skip_class': True,       # класс восстанавливается из сейва
            }
        elif choice == 'create':
            self.game.pending_multiplayer = {
                'mode': 'mp_host',
                'port': 9000,
                'skip_class': False,
            }
        elif choice == 'new':
            self.game.pending_multiplayer = {
                'mode': 'mp_host',
                'port': 9000,
                'fresh_start': True,
                'skip_class': False,
            }
        elif choice == 'join':
            self.game.screen_mode = 'menu_mp_join'

    def _paste_mp_ip(self):
        """Вставить адрес из буфера обмена, отбросив лишние символы."""
        text = read_clipboard_text()
        if not text:
            return
        cleaned = ''.join(ch for ch in text.strip().lower()
                          if ch in _MP_HOST_CHARS)
        if not cleaned:
            return
        room = max(0, 40 - len(self.mp_ip))
        self.mp_ip = (self.mp_ip + cleaned[:room])[:40]

    def _connect_mp(self):
        host = self.mp_ip.strip() or 'localhost'
        self.game.pending_multiplayer = {
            'mode': 'mp_join',
            'host': host,
            'port': 9000,
        }

    # ── Input ──
    def handle_key(self, key, scancode=0, unicode='', mods=0):
        from audio import Audio
        m = self.game.screen_mode
        # ─────────── Мультиплеер: выбор ───────────
        if m == 'menu_multiplayer':
            n = len(self._mp_items())
            if key in (pygame.K_UP, pygame.K_w):
                self.mp_selection = (self.mp_selection - 1) % n
                Audio.play('menu_select')
            elif key in (pygame.K_DOWN, pygame.K_s):
                self.mp_selection = (self.mp_selection + 1) % n
                Audio.play('menu_select')
            elif key in (pygame.K_RETURN, pygame.K_SPACE):
                self._activate_mp()
            elif key == pygame.K_ESCAPE:
                self.back()
            return

        # ─────────── Мультиплеер: ввод IP ───────────
        if m == 'menu_mp_join':
            ctrl = bool(mods & pygame.KMOD_CTRL) if pygame.KMOD_CTRL else False
            # На нелатинской раскладке Ctrl+V приходит другим кодом,
            # поэтому ловим ещё и управляющий символ \x16.
            if ctrl and (key == pygame.K_v or unicode == '\x16'):
                self._paste_mp_ip()
                return
            if key == pygame.K_BACKSPACE:
                self.mp_ip = self.mp_ip[:-1]
            elif key in (pygame.K_RETURN, pygame.K_KP_ENTER):
                self._connect_mp()
            elif key == pygame.K_ESCAPE:
                self.game.screen_mode = 'menu_multiplayer'
            elif not ctrl and len(self.mp_ip) < 40:
                # event.unicode не зависит от раскладки: на русской
                # клавиатуре «.» приходит сюда, а не как K_PERIOD.
                ch = (unicode or _key_to_char(key)).lower()
                if len(ch) == 1 and ch in _MP_HOST_CHARS:
                    self.mp_ip += ch
            return

        if m == 'menu':
            n = len(self.items())
            if key in (pygame.K_UP, pygame.K_w):
                self.selection = (self.selection - 1) % n
            elif key in (pygame.K_DOWN, pygame.K_s):
                self.selection = (self.selection + 1) % n
            elif key in (pygame.K_RETURN, pygame.K_SPACE):
                self.activate()
            elif key == pygame.K_ESCAPE:
                if self.from_game:
                    self.return_to_game()
                else:
                    self.exit_game()
            return

        if m == 'menu_saves':
            from audio import Audio
            if key in (pygame.K_UP, pygame.K_w):
                self.selection_saves = (self.selection_saves - 1) % 3
                Audio.play('menu_select')
            elif key in (pygame.K_DOWN, pygame.K_s):
                self.selection_saves = (self.selection_saves + 1) % 3
                Audio.play('menu_select')
            elif key in (pygame.K_RETURN, pygame.K_SPACE):
                self.load_slot(self.selection_saves + 1)
            elif key == pygame.K_n and self.saves_mode == 'load':
                self.new_in_slot(self.selection_saves + 1)
            elif key == pygame.K_d:
                self.delete_slot(self.selection_saves + 1)
            elif key == pygame.K_ESCAPE:
                self.back()
            return

        if m == 'menu_settings':
            from audio import Audio
            if key in (pygame.K_UP, pygame.K_w):
                self.selection_settings = (self.selection_settings - 1) % 6
                Audio.play('menu_select')
            elif key in (pygame.K_DOWN, pygame.K_s):
                self.selection_settings = (self.selection_settings + 1) % 6
                Audio.play('menu_select')
            elif key in (pygame.K_LEFT, pygame.K_a):
                self.adjust_setting(-1)
            elif key in (pygame.K_RIGHT, pygame.K_d):
                self.adjust_setting(1)
            elif key in (pygame.K_RETURN, pygame.K_SPACE):
                self.adjust_setting(1)
            elif key == pygame.K_ESCAPE:
                self.save_settings_and_back()
            return

        if m == 'menu_controls':
            if key in (pygame.K_ESCAPE, pygame.K_RETURN, pygame.K_SPACE):
                self.game.screen_mode = 'menu_settings'
            return

        if m == 'class_select':
            if key in (pygame.K_UP, pygame.K_w):
                self.class_selection = (self.class_selection - 1) % len(CLASS_ORDER)
            elif key in (pygame.K_DOWN, pygame.K_s):
                self.class_selection = (self.class_selection + 1) % len(CLASS_ORDER)
            elif key in (pygame.K_RETURN, pygame.K_SPACE):
                self.confirm_class()
            elif key == pygame.K_ESCAPE:
                self.game.current_slot = 1
                self.back()
            return

    def handle_mouse_motion(self, pos):
        for idx, rect in self.button_rects():
            x, y, w, h = rect
            if x <= pos[0] <= x + w and y <= pos[1] <= y + h:
                m = self.game.screen_mode
                if m == 'menu':
                    self.selection = idx
                elif m == 'menu_saves':
                    self.selection_saves = idx
                elif m == 'menu_settings':
                    self.selection_settings = idx
                elif m == 'menu_multiplayer':
                    if idx < len(self._mp_items()):
                        self.mp_selection = idx
                elif m == 'class_select':
                    self.class_selection = idx
                return

    def handle_mouse_click(self, pos):
        mx, my = pos

        # ── Стрелки в настройках ──
        if self.game.screen_mode == 'menu_settings':
            for row, direction, rect in self.settings_arrow_rects():
                x, y, w, h = rect
                if x <= mx <= x + w and y <= my <= y + h:
                    self.selection_settings = row
                    self.adjust_setting(direction)
                    return True

        # Крестики удаления слотов
        if self.game.screen_mode == 'menu_saves':
            from core.save import save_slot_exists
            for slot_idx in range(3):
                slot_num = slot_idx + 1
                rect = self.slot_delete_rect(slot_idx)
                if rect:
                    x, y, w, h = rect
                    if x <= mx <= x + w and y <= my <= y + h:
                        if save_slot_exists(slot_num):
                            self.delete_slot(slot_num)
                        return True

        # ── Кнопки ──
        for idx, rect in self.button_rects():
            x, y, w, h = rect
            if x <= pos[0] <= x + w and y <= pos[1] <= y + h:
                m = self.game.screen_mode
                # ── Крестик удаления слота ──
                if self.game.screen_mode == 'menu_saves':
                    for slot_idx in range(3):
                        rect = self.slot_delete_rect(slot_idx)
                        if rect:
                            x, y, w, h = rect
                            if x <= mx <= x + w and y <= my <= y + h:
                                self.delete_slot(slot_idx + 1)
                                return True
                if m == 'menu':
                    self.selection = idx
                    self.activate()
                elif m == 'menu_saves':
                    self.selection_saves = idx
                    self.load_slot(idx + 1)
                elif m == 'menu_settings':
                    self.selection_settings = idx
                    if idx == 5:
                        self.game.screen_mode = 'menu_controls'
                elif m == 'class_select':
                    self.class_selection = idx
                    self.confirm_class()
                elif m == 'menu_multiplayer':
                    self.mp_selection = idx
                    self._activate_mp()
                elif m == 'menu_mp_join':
                    if idx == 0:
                        self._connect_mp()
                    else:
                        self.game.screen_mode = 'menu_multiplayer'
                return True
        return False

    def settings_arrow_rects(self):
        """[(row_idx, direction, rect), ...] — кликабельные стрелки в настройках."""
        rects = self.button_rects()  # строки настроек
        result = []
        rows = [
            ("Разрешение", f"{self.game.settings.get('resolution', [1280, 720])[0]}×"
                           f"{self.game.settings.get('resolution', [1280, 720])[1]}"),
            ("Полный экран", "Вкл" if self.game.settings['fullscreen'] else "Выкл"),
            ("Громкость", f"{self.game.settings['volume']} / 10"),
            ("Показывать FPS", "Вкл" if self.game.settings['show_fps'] else "Выкл"),
            ("Мини-карта", "Вкл" if self.game.settings.get('show_minimap', True) else "Выкл"),
        ]
        from render.theme import Theme as T
        for i, (label, val) in enumerate(rows):
            if i >= len(rects):
                break
            x, y, w, h = rects[i][1]
            val_zone_center = x + w - 100
            val_w = T.FONT_H2.size(val)[0]
            val_left = val_zone_center - val_w // 2
            val_right = val_zone_center + val_w // 2
            ay = y + h // 2
            # левая стрелка
            lb = val_left - 16
            result.append((i, -1, (lb - 14, ay - 10, 16, 20)))
            # правая стрелка
            rb = val_right + 16
            result.append((i, 1, (rb - 2, ay - 10, 16, 20)))
        return result

    def slot_delete_rect(self, slot_idx):
        """Rect мусорного ведра для слота 0..2."""
        rects = self.button_rects()
        if slot_idx >= len(rects):
            return None
        x, y, w, h = rects[slot_idx][1]
        size = 32
        pad = 10
        return (x + w - size - pad, y + pad, size, size)