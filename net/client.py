"""pygame-клиент: ввод → сервер, отрисовка из snapshot."""
import socket
import threading
import json
import argparse
import pygame
import uuid
import os as _os

def _player_token_path():
    return _os.path.join(_os.path.expanduser('~'), '.ethelgard_player_id')


def _get_or_create_player_token():
    path = _player_token_path()
    try:
        if _os.path.exists(path):
            with open(path, 'r', encoding='utf-8') as f:
                t = f.read().strip()
                if t:
                    return t
        token = str(uuid.uuid4())
        with open(path, 'w', encoding='utf-8') as f:
            f.write(token)
        return token
    except Exception:
        return str(uuid.uuid4())

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
        self._build_rows()

    def _build_rows(self):
        self.rows = [
            ('Громкость', f"{self.settings.get('volume', 7)} / 10"),
            ('Показать FPS', 'Вкл' if self.settings.get('show_fps') else 'Выкл'),
            ('Мини-карта', 'Вкл' if self.settings.get('show_minimap', True) else 'Выкл'),
        ]

    def handle_key(self, key):
        if key in (pygame.K_UP, pygame.K_w):
            self.selected = (self.selected - 1) % len(self.rows)
        elif key in (pygame.K_DOWN, pygame.K_s):
            self.selected = (self.selected + 1) % len(self.rows)
        elif key in (pygame.K_LEFT, pygame.K_a):
            self._adjust(-1)
        elif key in (pygame.K_RIGHT, pygame.K_d):
            self._adjust(1)
        elif key in (pygame.K_RETURN, pygame.K_SPACE):
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
        """Экран выбора класса. Возвращает class_key или None (отмена)."""
        from core.data import CLASSES, CLASS_ORDER
        from core.entities import Player

        font_big = pygame.font.SysFont('consolas', 32, bold=True)
        font = pygame.font.SysFont('consolas', 20, bold=True)
        font_sm = pygame.font.SysFont('consolas', 14)
        selected = 0
        n = len(CLASS_ORDER)

        while True:
            screen.fill((14, 12, 10))
            title = font_big.render('ВЫБОР КЛАССА', True, (214, 178, 98))
            screen.blit(title, (sw // 2 - title.get_width() // 2, 40))

            bw = sw - 120
            bh = 110
            bx = 60
            by = 120
            gap = 14

            for i, key in enumerate(CLASS_ORDER):
                cls = CLASSES[key]
                y = by + i * (bh + gap)
                sel = (i == selected)
                bg = (96, 76, 44) if sel else (44, 39, 31)
                border = (206, 166, 82) if sel else (72, 62, 46)
                pygame.draw.rect(screen, bg, (bx, y, bw, bh), border_radius=8)
                pygame.draw.rect(screen, border, (bx, y, bw, bh), 2,
                                 border_radius=8)

                name = font.render(cls['name'], True, cls['color'])
                screen.blit(name, (bx + 20, y + 12))

                stats = Player.preview_stats({
                    'str': cls['str'], 'agi': cls['agi'], 'vit': cls['vit'],
                    'luk': cls['luk'], 'df_stat': cls['df_stat'],
                    'weapon': cls.get('start_weapon'),
                    'armor': cls.get('start_armor'),
                })
                line = (f"ATK {stats['atk']}  DEF {stats['df']}  "
                        f"HP {stats['max_hp']}  Укл {int(stats['dodge'] * 100)}%  "
                        f"Крит {int(stats['crit'] * 100)}%")
                screen.blit(font_sm.render(line, True, (222, 214, 198)),
                            (bx + 20, y + 42))
                screen.blit(font_sm.render(cls['desc'], True, (150, 140, 120)),
                            (bx + 20, y + 68))

            hint = font_sm.render(
                '↑↓ / мышь — выбор · Enter — начать · Esc — назад',
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
                        if bx <= event.pos[0] <= bx + bw and y <= event.pos[1] <= y + bh:
                            selected = i
                if event.type == pygame.MOUSEBUTTONDOWN and event.button == 1:
                    for i in range(n):
                        y = by + i * (bh + gap)
                        if bx <= event.pos[0] <= bx + bw and y <= event.pos[1] <= y + bh:
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
        from core.settings import load_settings, save_settings
        from core import config

        _s = load_settings()
        _res = _s.get('resolution', [1280, 720])
        config.set_resolution(_res[0], _res[1])
        SW, SH = config.SCREEN_W, config.SCREEN_H

        pygame.init()
        Audio.init(volume=_s.get('volume', 7))
        screen = pygame.display.set_mode((SW, SH))
        pygame.display.set_caption("Этельгард MP")

        if self.skip_class_select:
            # Класс придёт из сейва на сервере — экран не нужен
            class_key = self.class_key  # дефолт 'warrior', не используется при restore
        else:
            class_key = self._class_select_screen(screen, SW, SH)
            if class_key is None:
                # pygame принадлежит main.py: здесь только выходим из MP,
                # иначе следующее меню падает с «Library not initialized».
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
            # См. выше: не гасим общий pygame, просто возвращаемся в меню.
            return

        print(f"[CLIENT] connected as pid={self.my_pid}")
        pygame.display.set_caption(f"Этельгард MP — pid {self.my_pid}")
        clock = pygame.time.Clock()

        from render import Renderer
        from net.view import ClientView
        renderer = Renderer(screen)
        view = ClientView(send_fn=self.send)
        threading.Thread(target=self.recv_loop, daemon=True).start()

        pause = PauseMenu(SW, SH, is_host=self.is_host)
        settings_menu = SettingsMenu(SW, SH, _s, save_settings)
        mode = 'game'             # 'game' | 'pause' | 'settings'
        last_send = 0.0
        last_attack_send = 0.0
        last_snap = None
        import time

        while self.running:
            dt = clock.tick(60) / 1000.0
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
                        choices=('warrior', 'assassin', 'rogue', 'paladin'))
    args = parser.parse_args()
    Client(args.host, args.port, args.class_key).start()


if __name__ == '__main__':
    main()