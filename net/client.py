"""pygame-клиент: ввод → сервер, отрисовка из snapshot."""
import argparse
import ipaddress
import json
import os as _os
import socket
import threading
import uuid

import pygame


def _player_token_path():
    return _os.path.join(_os.path.expanduser('~'), '.ethelgard_player_id')


def _get_or_create_player_token():
    path = _player_token_path()
    try:
        if _os.path.exists(path):
            with open(path, encoding='utf-8') as f:
                t = f.read().strip()
                if t:
                    return t
        token = str(uuid.uuid4())
        with open(path, 'w', encoding='utf-8') as f:
            f.write(token)
        # Права 0600 — только владелец может читать токен.
        try:
            _os.chmod(path, 0o600)
        except OSError:
            pass  # Windows ACL: лучшее, что можем сделать.
        return token
    except Exception:
        return str(uuid.uuid4())


def _validate_host(host):
    """Санитизирует и проверяет адрес хоста.

    Убирает ``http://``/``https://``, порт, пробелы. Возвращает чистый IP/hostname
    или ``None``, если адрес невалиден.
    """
    if not host or not isinstance(host, str):
        return None
    s = host.strip()
    if not s:
        return None
    for prefix in ('http://', 'https://'):
        if s.startswith(prefix):
            s = s[len(prefix):]
    # Убрать порт, если ввели «ip:port»
    if s.count(':') == 1:
        s = s.split(':')[0]
    # Убрать слеш в конце
    s = s.rstrip('/')
    # Проверка IP или hostname
    try:
        ipaddress.ip_address(s)
        return s
    except ValueError:
        pass
    # Возможно, hostname — попробуем resolve (с таймаутом)
    try:
        socket.gethostbyname(s)
        return s
    except (socket.gaierror, OSError):
        return None

class PauseMenu:
    """Пауза: Продолжить / Настройки / [Сохранить мир (host)] / Отключиться."""
    def __init__(self, sw, sh, is_host=False):
        self.sw, self.sh = sw, sh
        self.is_host = is_host
        self.selected = 0
        self.items = ['Продолжить', 'Настройки']
        if is_host:
            self.items.append('Сохранить мир')
        self.items.append('Отключиться от игры')
        self.btn_w, self.btn_h = 400, 56
        bx = sw // 2 - self.btn_w // 2
        by = sh // 2 - (len(self.items) * 76) // 2 + 20
        self.rects = [(bx, by + i * 76, self.btn_w, self.btn_h)
                      for i in range(len(self.items))]

    def handle_key(self, key):
        if key in (pygame.K_UP, pygame.K_w):
            self.selected = (self.selected - 1) % len(self.items)
        elif key in (pygame.K_DOWN, pygame.K_s):
            self.selected = (self.selected + 1) % len(self.items)
        elif key in (pygame.K_RETURN, pygame.K_SPACE):
            return self.items[self.selected]
        elif key == pygame.K_ESCAPE:
            return 'Продолжить'
        return None

    def handle_click(self, pos):
        for i, (x, y, w, h) in enumerate(self.rects):
            if x <= pos[0] <= x + w and y <= pos[1] <= y + h:
                self.selected = i
                return self.items[i]
        return None

    def draw(self, screen):
        overlay = pygame.Surface((self.sw, self.sh), pygame.SRCALPHA)
        overlay.fill((7, 6, 5, 185))
        screen.blit(overlay, (0, 0))
        font_big = pygame.font.SysFont('consolas', 36, bold=True)
        font = pygame.font.SysFont('consolas', 22)
        title = font_big.render('ПАУЗА', True, (214, 178, 98))
        screen.blit(title, (self.sw // 2 - title.get_width() // 2,
                            self.sh // 2 - (len(self.items) * 76) // 2 - 60))
        for i, (text, (x, y, w, h)) in enumerate(zip(self.items, self.rects)):
            sel = (i == self.selected)
            bg = (90, 74, 42) if sel else (44, 46, 64)
            border = (245, 215, 105) if sel else (65, 68, 92)
            pygame.draw.rect(screen, bg, (x, y, w, h), border_radius=8)
            pygame.draw.rect(screen, border, (x, y, w, h), 2, border_radius=8)
            color = (255, 240, 180) if sel else (200, 200, 215)
            t = font.render(text, True, color)
            screen.blit(t, (x + w // 2 - t.get_width() // 2,
                            y + h // 2 - t.get_height() // 2))


class SettingsMenu:
    """Настройки клиента (громкость, FPS, мини-карта)."""
    def __init__(self, sw, sh, settings, save_fn):
        self.sw, self.sh = sw, sh
        self.settings = settings
        self.save_fn = save_fn
        self.selected = 0
        self.display_changed = False
        self._build_rows()

    def _build_rows(self):
        self.rows = [
            ('Громкость', f"{self.settings.get('volume', 7)} / 10"),
            ('Показать FPS', 'Вкл' if self.settings.get('show_fps') else 'Выкл'),
            ('Мини-карта', 'Вкл' if self.settings.get('show_minimap', True) else 'Выкл'),
            ('Полный экран', 'Вкл' if self.settings.get('fullscreen') else 'Выкл'),
        ]

    def handle_key(self, key):
        if key in (pygame.K_UP, pygame.K_w):
            self.selected = (self.selected - 1) % len(self.rows)
        elif key in (pygame.K_DOWN, pygame.K_s):
            self.selected = (self.selected + 1) % len(self.rows)
        elif key in (pygame.K_LEFT, pygame.K_a):
            self._adjust(-1)
        elif key in (pygame.K_RIGHT, pygame.K_d) or key in (pygame.K_RETURN, pygame.K_SPACE):
            self._adjust(1)
        elif key == pygame.K_ESCAPE:
            return 'back'
        return None

    def _adjust(self, delta):
        if self.selected == 0:
            v = self.settings.get('volume', 7)
            self.settings['volume'] = max(0, min(10, v + delta))
            from audio import Audio
            Audio.set_volume(self.settings['volume'])
        elif self.selected == 1:
            self.settings['show_fps'] = not self.settings.get('show_fps')
        elif self.selected == 2:
            self.settings['show_minimap'] = not self.settings.get('show_minimap', True)
        elif self.selected == 3:
            self.settings['fullscreen'] = not self.settings.get('fullscreen', False)
            self.display_changed = True
        self._build_rows()
        try:
            self.save_fn(self.settings)
        except Exception:
            pass

    def draw(self, screen):
        overlay = pygame.Surface((self.sw, self.sh), pygame.SRCALPHA)
        overlay.fill((7, 6, 5, 205))
        screen.blit(overlay, (0, 0))
        font_big = pygame.font.SysFont('consolas', 36, bold=True)
        font = pygame.font.SysFont('consolas', 22)
        title = font_big.render('НАСТРОЙКИ', True, (214, 178, 98))
        screen.blit(title, (self.sw // 2 - title.get_width() // 2,
                            self.sh // 2 - 150))
        y = self.sh // 2 - 60
        for i, (label, val) in enumerate(self.rows):
            sel = (i == self.selected)
            bg = (60, 55, 80) if sel else (28, 30, 44)
            border = (245, 215, 105) if sel else (65, 68, 92)
            pygame.draw.rect(screen, bg, (self.sw // 2 - 300, y, 600, 50),
                             border_radius=8)
            pygame.draw.rect(screen, border, (self.sw // 2 - 300, y, 600, 50),
                             2, border_radius=8)
            lc = (255, 240, 180) if sel else (200, 200, 215)
            t1 = font.render(label, True, lc)
            screen.blit(t1, (self.sw // 2 - 280, y + 12))
            t2 = font.render(val, True, (245, 215, 105) if sel else (140, 140, 155))
            screen.blit(t2, (self.sw // 2 + 280 - t2.get_width(), y + 12))
            y += 60
        hint = pygame.font.SysFont('consolas', 14).render(
            '↑↓ / мышь — выбор · ←→ / Enter — изменить · Esc — назад',
            True, (110, 110, 130))
        screen.blit(hint, (self.sw // 2 - hint.get_width() // 2,
                          self.sh - 40))

class Client:
    def __init__(self, host='localhost', port=9000, class_key='warrior',
                 is_host=False, skip_class_select=False):
        self.host, self.port = host, port
        self.class_key = class_key
        self.is_host = is_host
        self.skip_class_select = skip_class_select
        self._player_token = _get_or_create_player_token()
        self.sock = None
        self.lock = threading.Lock()
        self.snap = None
        self.tiles = None
        self.my_pid = None
        self.running = True

    def connect(self):
        self.sock = socket.create_connection((self.host, self.port), timeout=5.0)
        self.sock.settimeout(10.0)
        buf = b''
        while True:
            data = self.sock.recv(65536)
            if not data:
                raise ConnectionError("server closed")
            buf += data
            while b'\n' in buf:
                line, buf = buf.split(b'\n', 1)
                if not line.strip():
                    continue
                try:
                    msg = json.loads(line.decode())
                except ValueError as e:
                    # Повреждённая строка — просто ждём следующую
                    print(f"[CLIENT] skip bad message: {e}")
                    continue
                if msg.get('type') == 'welcome':
                    self.my_pid = msg['pid']
                    self.tiles = msg['tiles']
                    # Сообщить серверу свой класс
                    self.send({'type': 'hello', 'class': self.class_key,
                               'token': self._player_token})
                    return
                if msg.get('type') == 'error':
                    # Не выходим из процесса: main.py должен вернуть игрока
                    # в меню, а не завершать игру целиком.
                    raise ConnectionError(msg.get('msg') or 'server error')
            if len(buf) > 10_000_000:
                raise ConnectionError("welcome too big")

    def _show_message(self, screen, sw, sh, line1, line2, color=(222, 214, 198)):
        """Показать сообщение с перерисовкой и event.pump."""
        font_big = pygame.font.SysFont("consolas", 24)
        font_sm = pygame.font.SysFont("consolas", 16)
        screen.fill((14, 12, 10))
        t1 = font_big.render(line1, True, color)
        t2 = font_sm.render(line2, True, (150, 140, 120))
        screen.blit(t1, (sw // 2 - t1.get_width() // 2, sh // 2 - 20))
        screen.blit(t2, (sw // 2 - t2.get_width() // 2, sh // 2 + 20))
        pygame.display.flip()
        pygame.event.pump()

    def _class_select_screen(self, screen, sw, sh):
        """Экран выбора класса. Возвращает class_key или None (отмена).

        Слева — список базовых классов, справа — крупный предпросмотр:
        спрайт персонажа, производные статы, перк и стартовый набор.
        """
        from core.data import CLASS_ORDER, CLASSES, class_stats
        from core.entities import Player
        from render.icon_renderer import IconRenderer
        from render.theme import Theme as T

        icons = IconRenderer(screen)
        font_big = T.FONT_H1
        font_h2 = T.FONT_H2
        font_sm = T.FONT_SMALL
        font_tiny = T.FONT_TINY

        roles = {
            'warrior': 'Воин ближнего боя', 'mage': 'Маг стихий',
            'rogue': 'Мастер ловкости', 'artisan': 'Мастер ремёсел',
        }
        n = len(CLASS_ORDER)
        selected = 0

        # ── Геометрия ──
        bx = 60
        bw = min(400, int(sw * 0.28))
        by = 112
        bh = 96
        gap = 12
        px = bx + bw + 28
        pw = sw - px - 60
        py = 100
        ph = sh - py - 88
        start_w, start_h = 300, 50
        start_rect = (px + (pw - start_w) // 2,
                      py + ph - start_h - 18, start_w, start_h)

        def wrap(text, max_w):
            words = str(text).split()
            lines, line = [], ''
            for word in words:
                test = f"{line} {word}".strip()
                if not line or font_sm.size(test)[0] <= max_w:
                    line = test
                else:
                    lines.append(line)
                    line = word
            if line:
                lines.append(line)
            return lines

        def gear_list(cls):
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

        while True:
            screen.fill((14, 12, 10))
            title = font_big.render('ВЫБОР КЛАССА', True, (214, 178, 98))
            screen.blit(title, (sw // 2 - title.get_width() // 2, 30))
            sub = font_sm.render(
                'Наведи или выбери класс — справа показано, как он выглядит',
                True, (150, 140, 120))
            screen.blit(sub, (sw // 2 - sub.get_width() // 2, 84))

            # ── Список классов ──
            for i, key in enumerate(CLASS_ORDER):
                cls = CLASSES[key]
                y = by + i * (bh + gap)
                sel = (i == selected)
                bg = (52, 43, 31) if sel else (34, 30, 24)
                border = (206, 166, 82) if sel else (64, 55, 42)
                pygame.draw.rect(screen, bg, (bx, y, bw, bh),
                                 border_radius=8)
                pygame.draw.rect(screen, border, (bx, y, bw, bh), 2,
                                 border_radius=8)
                name = font_h2.render(
                    cls['name'], True,
                    cls['color'] if sel else (150, 140, 120))
                screen.blit(name, (bx + 20, y + 12))
                tier = font_tiny.render(
                    f"{cls.get('tier', 1)}-я ступень", True, (98, 90, 74))
                screen.blit(tier, (bx + bw - tier.get_width() - 16, y + 16))
                desc = font_sm.render(
                    cls['desc'][:40], True,
                    (222, 214, 198) if sel else (120, 112, 96))
                screen.blit(desc, (bx + 20, y + 44))
                st = class_stats(key)
                stat_line = font_tiny.render(
                    f"STR {st.get('str', 5)} · AGI {st.get('agi', 5)} · "
                    f"VIT {st.get('vit', 5)} · LUK {st.get('luk', 5)}",
                    True, (130, 122, 104))
                screen.blit(stat_line, (bx + 20, y + 68))
                if sel:
                    ay = y + bh // 2
                    pygame.draw.polygon(screen, (206, 166, 82), [
                        (bx - 15, ay - 7), (bx - 3, ay), (bx - 15, ay + 7)])

            # ── Панель предпросмотра ──
            key = CLASS_ORDER[selected]
            cls = CLASSES[key]
            color = cls['color']
            pygame.draw.rect(screen, (26, 22, 18), (px, py, pw, ph),
                             border_radius=12)
            pygame.draw.rect(screen, color, (px, py, pw, ph), 2,
                             border_radius=12)
            icons.text(cls['name'], px + 28, py + 16, color, font=font_big)
            role = roles.get(cls.get('base', key), 'Приключенец')
            icons.text(f"{role} · {cls.get('tier', 1)}-я ступень",
                       px + 28, py + 56, (150, 140, 120), font=font_sm)

            # Портрет + стартовый набор (левая колонка)
            limit_y = start_rect[1] - 12
            portrait = min(170, max(130, int(ph * 0.26)))
            bx2, by2 = px + 24, py + 80
            pygame.draw.rect(screen, (16, 14, 11),
                             (bx2, by2, portrait, portrait), border_radius=10)
            pygame.draw.rect(screen, color,
                             (bx2, by2, portrait, portrait), 2,
                             border_radius=10)
            icons.draw_player(key, bx2 + 3, by2 + 3, size=portrait - 6)
            icons.text("как выглядит в игре", bx2 + portrait // 2,
                       by2 + portrait + 4, (98, 90, 74),
                       font=font_tiny, center=True)
            gear_y = by2 + portrait + 24
            icons.text('СТАРТОВЫЙ НАБОР', bx2, gear_y, (206, 166, 82),
                       font=font_sm)
            gy = gear_y + 22
            gear = gear_list(cls)
            for item in gear[:3]:
                icons.text(f"• {item}", bx2, gy, (150, 140, 120),
                           font=font_sm)
                gy += 17
            if len(gear) > 3:
                icons.text(f"• и ещё {len(gear) - 3}", bx2, gy,
                           (98, 90, 74), font=font_sm)

            # Производные статы
            stats = Player.preview_stats({
                **class_stats(key),
                'weapon': cls.get('start_weapon'),
                'armor': cls.get('start_armor'),
                'effects': cls.get('effects', {}),
            })
            chips = (
                ('ATK', str(stats['atk']), (222, 150, 110)),
                ('DEF', str(stats['df']), (150, 170, 200)),
                ('HP', str(stats['max_hp']), (220, 84, 64)),
                ('MP', str(stats['max_mana']), (120, 160, 255)),
                ('Уклонение', f"{int(stats['dodge'] * 100)}%",
                 (150, 166, 178)),
                ('Крит', f"{int(stats['crit'] * 100)}%", (232, 178, 74)),
            )
            sx = bx2 + portrait + 24
            available = px + pw - 24 - sx
            cgap = 8
            chip_w = max(96, (available - cgap * 2) // 3)
            chip_h = 46
            for i, (label, value, chip_color) in enumerate(chips):
                col, row = i % 3, i // 3
                cxp = sx + col * (chip_w + cgap)
                cyp = by2 + row * (chip_h + cgap)
                pygame.draw.rect(screen, (34, 29, 23),
                                 (cxp, cyp, chip_w, chip_h),
                                 border_radius=8)
                pygame.draw.rect(screen, (70, 60, 46),
                                 (cxp, cyp, chip_w, chip_h), 1,
                                 border_radius=8)
                icons.text(label, cxp + 10, cyp + 5, (150, 140, 120),
                           font=font_tiny)
                icons.text(value, cxp + chip_w - 10, cyp + 18, chip_color,
                           font=font_h2, align_right=True)

            # Базовые атрибуты
            st = class_stats(key)
            attrs = (
                ('STR', st.get('str', 5), (220, 120, 100)),
                ('AGI', st.get('agi', 5), (140, 200, 130)),
                ('VIT', st.get('vit', 5), (220, 120, 110)),
                ('LUK', st.get('luk', 5), (220, 200, 110)),
                ('DEF', st.get('df_stat', 5), (150, 170, 210)),
                ('INT', st.get('int', 5), (130, 170, 240)),
            )
            ay = by2 + 2 * (chip_h + cgap) + 6
            icons.text('БАЗОВЫЕ ХАРАКТЕРИСТИКИ', sx, ay, (206, 166, 82),
                       font=font_sm)
            ay += 22
            bar_w = px + pw - 28 - (sx + 52)
            right_w = px + pw - 24 - sx
            for label, value, bar_color in attrs:
                icons.text(label, sx, ay, (222, 214, 198), font=font_sm)
                pygame.draw.rect(screen, (20, 18, 15),
                                 (sx + 52, ay + 5, max(20, bar_w), 9),
                                 border_radius=4)
                fill = int(max(20, bar_w) * min(1.0, value / 12.0))
                if fill > 0:
                    pygame.draw.rect(screen, bar_color,
                                     (sx + 52, ay + 5, fill, 9),
                                     border_radius=4)
                icons.text(str(value), px + pw - 28, ay, bar_color,
                           font=font_sm, align_right=True)
                ay += 20

            # Перк
            perk = cls.get('perk') or cls.get('desc', '')
            icons.text('ОСОБЕННОСТЬ', sx, ay + 6, (206, 166, 82),
                       font=font_sm)
            ty = ay + 28
            for line in wrap(perk, right_w):
                if ty > limit_y:
                    break
                icons.text(line, sx, ty, (222, 214, 198), font=font_sm)
                ty += 19

            # Кнопка «Начать»
            mouse = pygame.mouse.get_pos()
            hover = (start_rect[0] <= mouse[0] <= start_rect[0] + start_w
                     and start_rect[1] <= mouse[1] <= start_rect[1] + start_h)
            sbg = T.shade(color, 1.15 if hover else 0.92)
            T.rounded_rect(screen, sbg, start_rect, radius=9,
                           border_color=T.shade(color, 1.4))
            label_color = (20, 16, 12) if sum(color) > 420 else (250, 246, 238)
            start_label = font_h2.render(
                f"НАЧАТЬ ЗА {cls['name'].upper()}", True, label_color)
            screen.blit(start_label, (
                start_rect[0] + (start_w - start_label.get_width()) // 2,
                start_rect[1] + (start_h - start_label.get_height()) // 2))

            hint = font_sm.render(
                '↑↓ / мышь — выбор · Enter / кнопка — начать · Esc — назад',
                True, (98, 90, 74))
            screen.blit(hint, (sw // 2 - hint.get_width() // 2, sh - 30))
            pygame.display.flip()

            for event in pygame.event.get():
                if event.type == pygame.QUIT:
                    return None
                if event.type == pygame.KEYDOWN:
                    if event.key in (pygame.K_UP, pygame.K_w):
                        selected = (selected - 1) % n
                    elif event.key in (pygame.K_DOWN, pygame.K_s):
                        selected = (selected + 1) % n
                    elif event.key in (pygame.K_RETURN, pygame.K_SPACE):
                        return CLASS_ORDER[selected]
                    elif event.key == pygame.K_ESCAPE:
                        return None
                if event.type == pygame.MOUSEMOTION:
                    for i in range(n):
                        y = by + i * (bh + gap)
                        if (bx <= event.pos[0] <= bx + bw
                                and y <= event.pos[1] <= y + bh):
                            selected = i
                if event.type == pygame.MOUSEBUTTONDOWN and event.button == 1:
                    if (start_rect[0] <= event.pos[0] <= start_rect[0] + start_w
                            and start_rect[1] <= event.pos[1]
                            <= start_rect[1] + start_h):
                        return CLASS_ORDER[selected]
                    for i in range(n):
                        y = by + i * (bh + gap)
                        if (bx <= event.pos[0] <= bx + bw
                                and y <= event.pos[1] <= y + bh):
                            return CLASS_ORDER[i]

    def send(self, msg):
        try:
            self.sock.sendall((json.dumps(msg) + '\n').encode())
        except Exception:
            pass

    def recv_loop(self):
        buf = b''
        while self.running:
            try:
                data = self.sock.recv(65536)
                if not data:
                    if self.running:
                        print("[CLIENT] disconnected")
                    self.running = False
                    break
                buf += data
                while b'\n' in buf:
                    line, buf = buf.split(b'\n', 1)
                    if not line.strip():
                        continue
                    try:
                        msg = json.loads(line.decode())
                        with self.lock:
                            if msg.get('type') in ('state', 'state_with_tiles'):
                                if 'tiles' in msg:
                                    self.tiles = msg.pop('tiles')
                                self.snap = msg
                    except Exception as e:
                        print(f"[CLIENT] parse error: {e}")
            except Exception as e:
                if self.running:
                    print(f"[CLIENT] recv error: {e}")
                self.running = False
                break

    def start(self):
        from audio import Audio
        from core import config
        from core.settings import load_settings, save_settings

        _s = load_settings()
        _res = _s.get('resolution', [1280, 720])
        config.set_resolution(_res[0], _res[1])
        SW, SH = config.SCREEN_W, config.SCREEN_H

        pygame.init()
        Audio.init(volume=_s.get('volume', 7))
        from render.display import set_mode as set_display_mode
        screen = set_display_mode((SW, SH), _s.get('fullscreen'), _s)
        # Fullscreen может занять весь рабочий стол — берём фактический размер
        SW, SH = screen.get_width(), screen.get_height()
        pygame.display.set_caption("Этельгард MP")
        from render.window_icon import apply_window_icon
        apply_window_icon()

        # ВАЛИДАЦИЯ ХОСТА: убираем http://, порт, проверяем IP/hostname.
        clean_host = _validate_host(self.host)
        if clean_host is None:
            self._show_message(screen, SW, SH,
                               f"Неверный адрес: {self.host}",
                               "Проверь IP или имя хоста.",
                               color=(240, 100, 100))
            pygame.time.wait(2000)
            return
        self.host = clean_host

        # Хост ВСЕГДА выбирает класс (раньше skip_class_select мог передаваться
        # ошибочно, и игрок вылетал в главное меню без выбора).
        # Только при skip_class_select=True (restore из сейва) — пропускаем.
        if self.skip_class_select and not self.is_host:
            class_key = self.class_key
        else:
            class_key = self._class_select_screen(screen, SW, SH)
            if class_key is None:
                # Esc в экране выбора — спокойно возвращаемся в меню.
                return
            self.class_key = class_key

        self._show_message(screen, SW, SH,
                           f"Подключение к {self.host}:{self.port}...",
                           "Подожди до 5 секунд")
        try:
            self.connect()
        except Exception as e:
            self._show_message(screen, SW, SH,
                               "Не удалось подключиться", f"{e}",
                               color=(240, 100, 100))
            pygame.time.wait(2000)
            return

        print(f"[CLIENT] connected as pid={self.my_pid}")
        pygame.display.set_caption(f"Этельгард MP — pid {self.my_pid}")
        clock = pygame.time.Clock()

        from net.view import ClientView
        from render import Renderer
        renderer = Renderer(screen)
        view = ClientView(send_fn=self.send)
        threading.Thread(target=self.recv_loop, daemon=True).start()

        pause = PauseMenu(SW, SH, is_host=self.is_host)
        settings_menu = SettingsMenu(SW, SH, _s, save_settings)
        fps_font = pygame.font.SysFont("consolas", 14)

        def apply_display():
            """Применить полноэкранный режим и перепривязать рендереры."""
            nonlocal screen, SW, SH
            from render.display import set_mode as set_display_mode
            screen = set_display_mode((SW, SH), _s.get('fullscreen'), _s)
            # Fullscreen (borderless) даёт размер рабочего стола
            SW, SH = screen.get_width(), screen.get_height()
            from render.window_icon import apply_window_icon
            apply_window_icon()
            renderer.screen = screen
            for sub in ('menu', 'game', 'screens', 'overlay', 'icons'):
                r = getattr(renderer, sub, None)
                if r is not None and hasattr(r, 'screen'):
                    r.screen = screen

        mode = 'game'             # 'game' | 'pause' | 'settings'
        last_send = 0.0
        last_attack_send = 0.0
        last_snap = None
        import time

        while self.running:
            dt = clock.tick(60) / 1000.0
            if settings_menu.display_changed:
                settings_menu.display_changed = False
                apply_display()
            for event in pygame.event.get():
                if event.type == pygame.QUIT:
                    self.running = False

                elif event.type == pygame.KEYDOWN:
                    if mode == 'settings':
                        r = settings_menu.handle_key(event.key)
                        if r == 'back':
                            mode = 'pause'
                    elif mode == 'pause':
                        c = pause.handle_key(event.key)
                        if c == 'Продолжить':
                            mode = 'game'
                        elif c == 'Настройки':
                            mode = 'settings'
                        elif c == 'Сохранить мир':
                            self.send({'type': 'save_world'})
                            print("[MP] world save requested")
                        elif c == 'Отключиться от игры':
                            self.running = False
                    else:
                        if view.chat_active:
                            view.handle_chat_key(event.key, event.unicode)
                        elif event.key == pygame.K_ESCAPE:
                            # Esc закрывает открытое окно, а не игру:
                            # пауза — только из игрового экрана.
                            if view.screen_mode != 'game':
                                view.handle_key(event.key)
                            else:
                                mode = 'pause'
                        else:
                            view.handle_key(event.key)

                elif event.type == pygame.MOUSEBUTTONDOWN and event.button == 1:
                    if mode == 'pause':
                        c = pause.handle_click(event.pos)
                        if c == 'Продолжить':
                            mode = 'game'
                        elif c == 'Настройки':
                            mode = 'settings'
                        elif c == 'Сохранить мир':
                            self.send({'type': 'save_world'})
                            print("[MP] world save requested")
                        elif c == 'Отключиться от игры':
                            self.running = False
                    elif mode == 'settings':
                        # клики в настройках — опционально (по кнопкам не настроено)
                        pass
                    else:
                        view.handle_mouse_click(event.pos)

                elif event.type == pygame.MOUSEMOTION and mode == 'game':
                    view.handle_mouse_motion(event.pos)

                elif event.type == pygame.MOUSEWHEEL and mode == 'game':
                    view.scroll_log(event.y * 2)

            if mode == 'game' and view.screen_mode == 'game':
                keys = pygame.key.get_pressed()
                now = time.time()
                if now - last_send >= 0.12:
                    dx = dy = 0
                    if keys[pygame.K_w] or keys[pygame.K_UP]:    dy = -1
                    elif keys[pygame.K_s] or keys[pygame.K_DOWN]: dy = 1
                    elif keys[pygame.K_a] or keys[pygame.K_LEFT]: dx = -1
                    elif keys[pygame.K_d] or keys[pygame.K_RIGHT]: dx = 1
                    if dx or dy:
                        self.send({'type': 'move', 'dx': dx, 'dy': dy})
                        last_send = now
                if keys[pygame.K_SPACE] and now - last_attack_send >= 0.40:
                    self.send({'type': 'attack'})
                    last_attack_send = now

            with self.lock:
                snap = self.snap
                tiles = self.tiles
            # Один снапшот применяем один раз: иначе события/звуки
            # срабатывают на каждом кадре до прихода следующего
            if snap is not last_snap:
                last_snap = snap
                view.update_from_snapshot(snap, tiles, self.my_pid)
            view.tick(dt)                 # интерполяция движения из снапшотов
            renderer.draw(view)
            if view.chat_active:
                view.draw_chat(screen)

            if mode == 'pause':
                pause.draw(screen)
            elif mode == 'settings':
                settings_menu.draw(screen)

            if _s.get('show_fps'):
                fps = clock.get_fps()
                surf = fps_font.render(f"FPS {fps:.0f}", True, (150, 150, 150))
                screen.blit(surf, (config.SCREEN_W - surf.get_width() - 8, 4))

            pygame.display.flip()

        try:
            self.sock.close()
        except Exception:
            pass

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--host', default='localhost')
    parser.add_argument('--port', type=int, default=9000)
    parser.add_argument('--class', dest='class_key', default='warrior',
                        choices=('warrior', 'mage', 'rogue', 'artisan'))
    args = parser.parse_args()
    Client(args.host, args.port, args.class_key).start()


if __name__ == '__main__':
    main()
