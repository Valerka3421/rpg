"""Процедурные звуки через pygame.mixer. Работает без ассетов."""
import os
import math
import random
import array
import pygame
from core.config import ASSETS_DIR


class Audio:
    _sounds = {}
    _enabled = False
    _volume = 7
    _initialized = False
    _rate = 22050
    _channels = 1

    @classmethod
    def init(cls, volume=7):
        cls._volume = volume
        if cls._initialized:
            # Если pygame переинициализировали, микшер и Sound-объекты
            # мертвы — генерируем звуки заново. Если звук изначально
            # отключён, повторные попытки не делаем.
            if pygame.mixer.get_init() or not cls._enabled:
                return
            cls._sounds.clear()
            cls._initialized = False
        cls._initialized = True
        try:
            if not pygame.mixer.get_init():
                pygame.mixer.init(frequency=22050, size=-16, channels=1,
                                  buffer=512)
            # Микшер мог быть открыт раньше (pygame.init) с другими
            # параметрами — генерируем звук ровно под них, иначе моно-буфер
            # в стерео-микшере играет вдвое быстрее и с шумом.
            info = pygame.mixer.get_init()
            if info:
                cls._rate, _size, cls._channels = info
                cls._channels = max(1, cls._channels)
            cls._enabled = True
        except Exception:
            cls._enabled = False
            return
        # Сначала ищем WAV в assets/sounds/, если нет — генерируем
        cls._load_or_generate()

    @classmethod
    def set_volume(cls, vol):
        cls._volume = max(0, min(10, vol))
        for snd in cls._sounds.values():
            try:
                snd.set_volume(cls._volume / 10)
            except Exception:
                pass

    @classmethod
    def play(cls, name):
        if not cls._enabled:
            return
        snd = cls._sounds.get(name)
        if snd is None:
            return
        try:
            snd.play()
        except Exception:
            pass

    # ════════════════════════ ЗАГРУЗКА ════════════════════════
    @classmethod
    def _load_or_generate(cls):
        sounds_dir = os.path.join(ASSETS_DIR, 'sounds')
        generators = {
            'hit':         cls._gen_hit,
            'crit':        cls._gen_crit,
            'miss':        cls._gen_miss,
            'death_monster': cls._gen_death_monster,
            'death_player': cls._gen_death_player,
            'level_up':    cls._gen_level_up,
            'pickup':      cls._gen_pickup,
            'menu_open':   cls._gen_menu_open,
            'menu_select': cls._gen_menu_select,
            'fireball':    cls._gen_fireball,
            'heal':        cls._gen_heal,
            'stairs':      cls._gen_stairs,
        }
        for name, gen in generators.items():
            wav_path = os.path.join(sounds_dir, f'{name}.wav')
            if os.path.isfile(wav_path):
                try:
                    snd = pygame.mixer.Sound(wav_path)
                    snd.set_volume(cls._volume / 10)
                    cls._sounds[name] = snd
                    continue
                except Exception:
                    pass
            try:
                snd = gen()
                snd.set_volume(cls._volume / 10)
                cls._sounds[name] = snd
            except Exception:
                pass

    # ════════════════════════ УТИЛИТЫ ════════════════════════
    @classmethod
    def _buffer(cls, samples):
        """samples: list[float] -1..1 → Sound (под формат микшера)."""
        channels = max(1, cls._channels)
        buf = array.array('h', [0] * (len(samples) * channels))
        max_amp = 32767
        for i, v in enumerate(samples):
            value = max(-max_amp, min(max_amp, int(v * max_amp)))
            for ch in range(channels):
                buf[i * channels + ch] = value
        return pygame.mixer.Sound(buffer=buf.tobytes())

    @staticmethod
    def _envelope(n, attack=0.01, decay=0.3):
        """Простая AD-огибающая."""
        att = max(1, int(n * attack))
        dec = max(1, int(n * decay))
        env = [0.0] * n
        for i in range(n):
            if i < att:
                env[i] = i / att
            elif i > n - dec:
                env[i] = (n - i) / dec
            else:
                env[i] = 1.0
        return env

    @classmethod
    def _sine(cls, freq, dur, vol=0.4):
        sr = cls._rate
        n = int(sr * dur)
        env = cls._envelope(n, attack=0.005, decay=0.4)
        return [math.sin(2 * math.pi * freq * i / sr) * vol * env[i]
                for i in range(n)]

    @classmethod
    def _noise(cls, dur, vol=0.3):
        sr = cls._rate
        n = int(sr * dur)
        env = cls._envelope(n, attack=0.001, decay=0.3)
        return [random.uniform(-1, 1) * vol * env[i] for i in range(n)]

    # ════════════════════════ ГЕНЕРАТОРЫ ════════════════════════
    @classmethod
    def _gen_hit(cls):
        # Низкий тон + шум
        a = cls._sine(160, 0.08, 0.5)
        b = cls._noise(0.06, 0.4)
        n = max(len(a), len(b))
        out = a + [0.0] * (n - len(a))
        for i in range(len(b)):
            out[i] += b[i]
        return cls._buffer(out)

    @classmethod
    def _gen_crit(cls):
        a = cls._sine(300, 0.10, 0.5)
        b = cls._sine(200, 0.10, 0.4)
        return cls._buffer([a[i] + (b[i] if i < len(b) else 0)
                            for i in range(len(a))])

    @classmethod
    def _gen_miss(cls):
        return cls._buffer(cls._noise(0.08, 0.15))

    @classmethod
    def _gen_death_monster(cls):
        # Нисходящий тон
        sr = cls._rate
        n = int(sr * 0.25)
        env = cls._envelope(n, attack=0.005, decay=0.6)
        out = []
        for i in range(n):
            t = i / n
            freq = 300 * (1 - t * 0.7)
            out.append(math.sin(2 * math.pi * freq * i / sr) * 0.35 * env[i])
        return cls._buffer(out)

    @classmethod
    def _gen_death_player(cls):
        # Низкий протяжный
        sr = cls._rate
        n = int(sr * 0.8)
        env = cls._envelope(n, attack=0.01, decay=0.7)
        out = []
        for i in range(n):
            t = i / n
            freq = 120 * (1 - t * 0.5)
            out.append(math.sin(2 * math.pi * freq * i / sr) * 0.5 * env[i])
        return cls._buffer(out)

    @classmethod
    def _gen_level_up(cls):
        # Арпеджио до-мажор
        notes = [262, 330, 392, 523]
        sr = cls._rate
        out = []
        for freq in notes:
            n = int(sr * 0.08)
            env = cls._envelope(n, attack=0.01, decay=0.5)
            for i in range(n):
                out.append(math.sin(2 * math.pi * freq * i / sr) * 0.35 * env[i])
        return cls._buffer(out)

    @classmethod
    def _gen_pickup(cls):
        # Короткий высокий пик
        a = cls._sine(800, 0.06, 0.3)
        return cls._buffer(a)

    @classmethod
    def _gen_menu_open(cls):
        # Мягкий клик
        a = cls._sine(400, 0.04, 0.25)
        return cls._buffer(a)

    @classmethod
    def _gen_menu_select(cls):
        a = cls._sine(600, 0.03, 0.2)
        return cls._buffer(a)

    @classmethod
    def _gen_fireball(cls):
        # Шум + нисходящий тон
        sr = cls._rate
        n = int(sr * 0.3)
        env = cls._envelope(n, attack=0.01, decay=0.5)
        out = []
        for i in range(n):
            t = i / n
            freq = 400 * (1 - t * 0.9)
            wave = math.sin(2 * math.pi * freq * i / sr) * 0.3
            noise = random.uniform(-1, 1) * 0.15
            out.append((wave + noise) * env[i])
        return cls._buffer(out)

    @classmethod
    def _gen_heal(cls):
        # Восходящий тон
        sr = cls._rate
        n = int(sr * 0.25)
        env = cls._envelope(n, attack=0.02, decay=0.5)
        out = []
        for i in range(n):
            t = i / n
            freq = 400 + t * 400
            out.append(math.sin(2 * math.pi * freq * i / sr) * 0.3 * env[i])
        return cls._buffer(out)

    @classmethod
    def _gen_stairs(cls):
        # Ступени вниз
        sr = cls._rate
        n = int(sr * 0.15)
        env = cls._envelope(n, attack=0.01, decay=0.4)
        out = []
        for i in range(n):
            t = i / n
            freq = 200 - t * 100
            out.append(math.sin(2 * math.pi * freq * i / sr) * 0.35 * env[i])
        return cls._buffer(out)