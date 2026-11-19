"""TCP-сервер: держит GameCore, рассылает snapshot 20 раз/сек."""
import argparse
import json
import logging
import os
import socket
import threading
import time

from core import config
from core.config import DATA_DIR
from net.core import GameCore

log = logging.getLogger("ethelgard.server")

# Защита от DoS: если клиент прислал больше этого без newline — разрыв.
MAX_MSG_SIZE = 65536


class Server:
    def __init__(self, host='0.0.0.0', port=9000, max_players=2,
                 server_password=None):
        """TCP-сервер для кооператива.

        ``server_password`` — если задан, клиенты обязаны прислать его в
        ``hello`` сообщении. По умолчанию ``None`` — LAN без пароля
        (обратно совместимо со старыми клиентами). Можно также задать через
        env var ``ETHELGARD_SERVER_PASSWORD`` (см. ``core/config.py``).
        """
        self.host, self.port = host, port
        self.requested_port = port
        self.bound_port = None
        self.ready = threading.Event()
        self.max_players = max_players
        # Пароль на подключение: env var > явный аргумент > None (без пароля)
        self.server_password = (server_password
                                 or config.SERVER_PASSWORD_DEFAULT)
        self.clients = {}          # {pid: socket}
        self.next_pid = 0
        self.core = GameCore(seed=42)
        self.lock = threading.Lock()
        self.running = True
        self._last_world_serial = -1
        self._sock = None
        self.client_tokens = {}
        # IP каждого клиента: персонаж сохраняется и по токену, и по IP,
        # поэтому прогресс не теряется при смене/потере token-файла.
        self.client_ips = {}
        self.saved_players = {}
        # Сессионные ключи HMAC: {pid: bytes}. Генерируются при hello,
        # используются для подписи сообщений от клиента.
        self.session_keys = {}
        self.world_save_path = os.path.join(DATA_DIR, 'savegame_mp_world.json')
        self.players_save_path = os.path.join(DATA_DIR, 'savegame_mp_players.json')
        self._load_saved_players()

    def _load_saved_players(self):
        if not os.path.exists(self.players_save_path):
            return
        try:
            with open(self.players_save_path, encoding='utf-8') as f:
                self.saved_players = json.load(f)
            log.info("loaded %d saved players", len(self.saved_players))
        except Exception as e:
            log.warning("load players: %s", e)

    def _flush_saved_players(self):
        try:
            tmp = self.players_save_path + '.tmp'
            with open(tmp, 'w', encoding='utf-8') as f:
                json.dump(self.saved_players, f, ensure_ascii=False)
            os.replace(tmp, self.players_save_path)
        except Exception as e:
            log.warning("save players: %s", e)

    # ── Сохранение игроков по токену и по IP ──
    @staticmethod
    def _ip_key(addr):
        """Ключ сохранения ``ip:<addr>`` или None, если адрес неизвестен."""
        if isinstance(addr, (tuple, list)):
            addr = addr[0] if addr else None
        if not addr:
            return None
        return f'ip:{addr}'

    def _save_player_record(self, pid, player):
        """Пишет персонажа в оба слота: персональный токен и IP."""
        token = self.client_tokens.get(pid)
        data = self.core._player_to_dict(player)
        if token:
            self.saved_players[token] = data
        ip_key = self._ip_key(self.client_ips.get(pid))
        if ip_key:
            self.saved_players[ip_key] = data
        return bool(token or ip_key)

    def _find_saved_player(self, token, ip_key):
        """Ищет сохранение: сначала токен, затем IP. Возвращает dict|None."""
        for key in (token, ip_key):
            if key and key in self.saved_players:
                return self.saved_players[key]
        return None

    def _bind_socket(self, sock):
        """Занять порт, перебирая следующие, если запрошенный занят."""
        last_error = None
        for candidate in range(self.requested_port, self.requested_port + 20):
            try:
                sock.bind((self.host, candidate))
                self.port = candidate
                self.bound_port = candidate
                sock.listen(5)
                return True
            except OSError as e:
                last_error = e
                continue
        log.warning("Не удалось занять порт "
              f"{self.requested_port}-{self.requested_port + 19}: {last_error}")
        return False

    def start(self):
        if os.path.exists(self.world_save_path):
            if self.core.load_world(self.world_save_path):
                log.info("world loaded from %s", self.world_save_path)
        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        try:
            bind_ok = self._bind_socket(sock)
            if not bind_ok:
                self.running = False
                # ready.set() ОБЯЗАТЕЛЬНО — иначе main.py зависнет на wait().
                self.ready.set()
                return
        except Exception as e:
            log.error("bind exception: %s", e)
            self.running = False
            self.ready.set()
            return
        # bind успешен — теперь поднимаем accept loop.
        self.ready.set()
        self._sock = sock
        if self.bound_port != self.requested_port:
            log.warning("порт %s занят — использую %s",
                        self.requested_port, self.bound_port)
        log.info("%s:%s  (max %s)", self.host, self.port, self.max_players)

        threading.Thread(target=self._tick_loop, daemon=True).start()

        while self.running:
            try:
                conn, addr = sock.accept()
            except OSError:
                break                                  # сокет закрыт — выходим

            if not self.running:
                try: conn.close()
                except Exception: pass
                break

            with self.lock:
                full = len(self.clients) >= self.max_players
            if full:
                try:
                    conn.sendall(b'{"type":"error","msg":"server full"}\n')
                except Exception:
                    pass
                try: conn.close()
                except Exception: pass
                continue

            pid = self.next_pid
            self.next_pid += 1
            welcome = {
                'type': 'welcome',
                'pid': pid,
                'tiles': self.core.get_tiles(),
                'world_mode': self.core.world_mode,
                'depth': self.core.depth,
            }
            # Welcome уходит ДО регистрации клиента: иначе поток тиков может
            # вклиниться в тот же сокет и разрезать JSON-строку.
            try:
                conn.sendall((json.dumps(welcome) + '\n').encode())
            except Exception as e:
                log.warning("welcome error for %s: %s", pid, e)
                try: conn.close()
                except Exception: pass
                continue

            with self.lock:
                self.clients[pid] = conn
                self.client_ips[pid] = addr[0] if addr else ''
            log.info("player %s connected (%s)", pid, addr[0])
            threading.Thread(target=self._client_loop,
                             args=(pid, conn, addr), daemon=True).start()

        try:
            sock.close()
        except Exception:
            pass
        log.info("stopped")

    def stop(self):
        """Остановить сервер: закрыть сокет, поток завершится."""
        self.running = False
        try:
            if self._sock:
                self._sock.close()
        except Exception:
            pass
        self._sock = None
        # Последний шанс сохранить мир, чтобы «Продолжить» не теряло прогресс
        try:
            self.core.save_world(self.world_save_path)
            for qid, qp in self.core.players.items():
                self._save_player_record(qid, qp)
            self._flush_saved_players()
        except Exception as e:
            log.error("final save failed: %s", e)

    def _drop_client(self, pid, reason=''):
        with self.lock:
            conn = self.clients.pop(pid, None)
            # Сохраняем до удаления токена/IP — иначе запись уйдёт только
            # под IP и токен потеряет свой слот.
            if pid in self.core.players:
                self._save_player_record(pid, self.core.players[pid])
                self._flush_saved_players()
            self.client_tokens.pop(pid, None)
            self.client_ips.pop(pid, None)
            self.core.remove_player(pid)
            no_more_clients = not self.clients
        if conn is not None:
            try:
                conn.close()
            except Exception:
                pass
        if no_more_clients:
            # Мир сохраняем, когда вышел последний игрок
            with self.lock:
                try:
                    self.core.save_world(self.world_save_path)
                except Exception as e:
                    log.warning("world save on last disconnect: %s", e)
        if reason:
            log.info("player %s dropped (%s)", pid, reason)

    def _client_loop(self, pid, conn, addr=None):
        buf = b''
        while self.running:
            try:
                data = conn.recv(4096)
                if not data:
                    break
                buf += data
                # Защита от DoS: буфер не должен расти бесконечно без \n.
                if len(buf) > MAX_MSG_SIZE:
                    log.warning("drop %s: message too big "
                          f"({len(buf)} bytes, no newline)")
                    break
                while b'\n' in buf:
                    line, buf = buf.split(b'\n', 1)
                    if not line.strip():
                        continue
                    try:
                        msg = json.loads(line.decode())
                        if msg.get('type') == 'hello':
                            token = str(msg.get('token') or f'anon_{pid}')
                            class_key = msg.get('class', 'warrior')
                            # P3: проверка пароля сервера (если задан).
                            # hmac.compare_digest — защита от timing-атаки.
                            if self.server_password:
                                provided = msg.get('server_password') or ''
                                if not hmac.compare_digest(
                                        provided, self.server_password):
                                    log.warning("drop %s: wrong server password", pid)
                                    try:
                                        conn.sendall(b'{"type":"error",'
                                                     b'"msg":"wrong password"}\n')
                                    except Exception:
                                        pass
                                    break
                            # P3: генерируем сессионный ключ для HMAC.
                            import os as _os
                            session_key = _os.urandom(32)
                            self.session_keys[pid] = session_key
                            ip_key = self._ip_key(addr or
                                                  self.client_ips.get(pid))
                            with self.lock:
                                self.client_tokens[pid] = token
                                if addr:
                                    self.client_ips[pid] = addr[0]
                                saved = self._find_saved_player(token,
                                                                ip_key)
                                if saved is not None:
                                    p = self.core._player_from_dict(saved)
                                    # Позиция из другого сейва может оказаться
                                    # в стене — тогда переселяем в свободную клетку
                                    from core.config import Tile
                                    if (self.core.dungeon.tiles[p.y][p.x]
                                            == Tile.WALL):
                                        spot = self.core._free_spot()
                                        if spot:
                                            p.x, p.y = spot
                                    self.core.players[pid] = p
                                    self.core.move_timers[pid] = 0.0
                                    self.core.attack_timers[pid] = 0.0
                                    self.core.regen_timers[pid] = 2.0
                                    # Запоминаем и под IP, и под токеном:
                                    # так прогресс найдётся в следующий раз
                                    # даже при смене token-файла.
                                    self._save_player_record(pid, p)
                                    self._flush_saved_players()
                                    log.info("%s RESTORED (%s... ip=%s)",
                                             pid, token[:8], ip_key)
                                else:
                                    self.core.add_player(pid, class_key)
                                    log.info("%s NEW (%s...) class=%s ip=%s",
                                             pid, token[:8], class_key, ip_key)
                            continue
                        if msg.get('type') == 'save_world':
                            with self.lock:
                                try:
                                    self.core.save_world(self.world_save_path)
                                    for qid, qp in self.core.players.items():
                                        self._save_player_record(qid, qp)
                                    self._flush_saved_players()
                                    log.info("world + players saved")
                                except Exception as e:
                                    log.error("save error: %s", e)
                            continue
                        # P3: HMAC verification — если у клиента есть
                        # сессионный ключ, проверяем подпись сообщения.
                        session_key = self.session_keys.get(pid)
                        if session_key and msg.get('type') not in ('hello',):
                            sig = msg.pop('sig', None)
                            if sig and not config.verify_message_hmac(
                                    session_key, msg, sig):
                                log.warning("drop %s: bad HMAC", pid)
                                break
                        with self.lock:
                            self.core.apply_input(pid, msg)
                    except Exception as e:
                        log.warning("bad input from %s: %s", pid, e)
            except Exception as e:
                log.warning("client %s error: %s", pid, e)
                break
        self._drop_client(pid)
        log.info("player %s disconnected", pid)

    def _tick_loop(self):
        last = time.time()
        while self.running:
            now = time.time()
            dt = min(now - last, 0.1)
            last = now

            with self.lock:
                self.core.update(dt)
                snap = self.core.get_snapshot()
                world_serial = self.core.world_serial
                tiles_changed = world_serial != self._last_world_serial
                if tiles_changed:
                    self._last_world_serial = world_serial
                    snap['type'] = 'state_with_tiles'
                    snap['tiles'] = self.core.get_tiles()
                else:
                    snap['type'] = 'state'
                payload = (json.dumps(snap) + '\n').encode()
                targets = list(self.clients.items())

            # Рассылка — вне лока: медленный клиент не должен тормозить мир
            dead = []
            for pid, conn in targets:
                try:
                    conn.sendall(payload)
                except Exception:
                    dead.append(pid)

            for pid in dead:
                self._drop_client(pid, 'send failed')
            time.sleep(0.05)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--host', default='0.0.0.0')
    parser.add_argument('--port', type=int, default=9000)
    parser.add_argument('--players', type=int, default=2)
    args = parser.parse_args()
    Server(args.host, args.port, args.players).start()


if __name__ == '__main__':
    main()
