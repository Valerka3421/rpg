"""TCP-сервер: держит GameCore, рассылает snapshot 20 раз/сек."""
import socket
import threading
import json
import time
import os
import argparse
from core.config import DATA_DIR
from net.core import GameCore


class Server:
    def __init__(self, host='0.0.0.0', port=9000, max_players=2):
        self.host, self.port = host, port
        self.max_players = max_players
        self.clients = {}          # {pid: socket}
        self.next_pid = 0
        self.core = GameCore(seed=42)
        self.lock = threading.Lock()
        self.running = True
        self._last_world_serial = -1
        self._sock = None
        self.client_tokens = {}
        self.saved_players = {}
        self.world_save_path = os.path.join(DATA_DIR, 'savegame_mp_world.json')
        self.players_save_path = os.path.join(DATA_DIR, 'savegame_mp_players.json')
        self._load_saved_players()

    def _load_saved_players(self):
        if not os.path.exists(self.players_save_path):
            return
        try:
            with open(self.players_save_path, 'r', encoding='utf-8') as f:
                self.saved_players = json.load(f)
            print(f"[SERVER] loaded {len(self.saved_players)} saved players")
        except Exception as e:
            print(f"[SERVER] load players: {e}")

    def _flush_saved_players(self):
        try:
            tmp = self.players_save_path + '.tmp'
            with open(tmp, 'w', encoding='utf-8') as f:
                json.dump(self.saved_players, f, ensure_ascii=False)
            os.replace(tmp, self.players_save_path)
        except Exception as e:
            print(f"[SERVER] save players: {e}")

    def start(self):
        if os.path.exists(self.world_save_path):
            if self.core.load_world(self.world_save_path):
                print(f"[SERVER] world loaded from {self.world_save_path}")
        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        try:
            sock.bind((self.host, self.port))
            sock.listen(5)
        except OSError as e:
            print(f"[SERVER] Не удалось занять порт {self.port}: {e}")
            self.running = False
            return
        self._sock = sock
        print(f"[SERVER] {self.host}:{self.port}  (max {self.max_players})")

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
                print(f"[SERVER] welcome error for {pid}: {e}")
                try: conn.close()
                except Exception: pass
                continue

            with self.lock:
                self.clients[pid] = conn
            print(f"[SERVER] player {pid} connected ({addr[0]})")
            threading.Thread(target=self._client_loop,
                             args=(pid, conn), daemon=True).start()

        try:
            sock.close()
        except Exception:
            pass
        print("[SERVER] stopped")

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
                t = self.client_tokens.get(qid)
                if t:
                    self.saved_players[t] = self.core._player_to_dict(qp)
            self._flush_saved_players()
        except Exception as e:
            print(f"[SERVER] final save failed: {e}")

    def _drop_client(self, pid, reason=''):
        with self.lock:
            conn = self.clients.pop(pid, None)
            token = self.client_tokens.pop(pid, None)
            if token and pid in self.core.players:
                self.saved_players[token] = \
                    self.core._player_to_dict(self.core.players[pid])
                self._flush_saved_players()
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
                    print(f"[SERVER] world save on last disconnect: {e}")
        if reason:
            print(f"[SERVER] player {pid} dropped ({reason})")

    def _client_loop(self, pid, conn):
        buf = b''
        while self.running:
            try:
                data = conn.recv(4096)
                if not data:
                    break
                buf += data
                while b'\n' in buf:
                    line, buf = buf.split(b'\n', 1)
                    if not line.strip():
                        continue
                    try:
                        msg = json.loads(line.decode())
                        if msg.get('type') == 'hello':
                            token = msg.get('token', f'anon_{pid}')
                            class_key = msg.get('class', 'warrior')
                            with self.lock:
                                self.client_tokens[pid] = token
                                if token in self.saved_players:
                                    p = self.core._player_from_dict(
                                        self.saved_players[token])
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
                                    print(f"[SERVER] {pid} RESTORED ({token[:8]}...)")
                                else:
                                    self.core.add_player(pid, class_key)
                                    print(f"[SERVER] {pid} NEW ({token[:8]}...) "
                                          f"class={class_key}")
                            continue
                        if msg.get('type') == 'save_world':
                            with self.lock:
                                try:
                                    self.core.save_world(self.world_save_path)
                                    for qid, qp in self.core.players.items():
                                        t = self.client_tokens.get(qid)
                                        if t:
                                            self.saved_players[t] = \
                                                self.core._player_to_dict(qp)
                                    self._flush_saved_players()
                                    print(f"[SERVER] world + players saved")
                                except Exception as e:
                                    print(f"[SERVER] save error: {e}")
                            continue
                        with self.lock:
                            self.core.apply_input(pid, msg)
                    except Exception as e:
                        print(f"[SERVER] bad input from {pid}: {e}")
            except Exception as e:
                print(f"[SERVER] client {pid} error: {e}")
                break
        self._drop_client(pid)
        print(f"[SERVER] player {pid} disconnected")

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
