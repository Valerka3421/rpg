"""Константы, цвета, enum'ы. Ни от чего не зависит."""
import hashlib
import json
import os
import sys
from enum import IntEnum

# ── Пути ──
_PKG_DIR = os.path.dirname(os.path.abspath(__file__))
BASE_DIR = os.path.dirname(_PKG_DIR)

if hasattr(sys, '_MEIPASS'):
    ASSETS_DIR = os.path.join(sys._MEIPASS, 'assets')
else:
    ASSETS_DIR = os.path.join(BASE_DIR, 'assets')


def _writable_data_dir():
    """Каталог для settings.json и сейвов.

    В onefile-exe `__file__` смотрит во временную папку PyInstaller, которая
    удаляется при выходе, поэтому настройки «не сохранялись». Сначала
    пробуем папку рядом с exe (portable-режим), а если она только для
    чтения — %APPDATA%/Ethelgard.
    """
    env = os.environ.get('ETHELGARD_DATA_DIR')
    if env:
        target = os.path.abspath(os.path.expanduser(env))
    elif getattr(sys, 'frozen', False):
        target = os.path.dirname(os.path.abspath(sys.executable))
        try:
            probe = os.path.join(target, '.write_test')
            with open(probe, 'w', encoding='utf-8') as f:
                f.write('ok')
            os.remove(probe)
        except OSError:
            base = os.environ.get('APPDATA') or os.path.expanduser('~')
            target = os.path.join(base, 'Ethelgard')
    else:
        target = BASE_DIR
    try:
        os.makedirs(target, exist_ok=True)
    except OSError:
        target = BASE_DIR
    return target


DATA_DIR = _writable_data_dir()

# ── Базовые размеры ──
TILE = 64
HUD_HEIGHT = 170
# Старый мир 100×70 остаётся слева, справа добавлена Деревня гоблинов.
# Координаты восьми исходных биомов намеренно не зависят от MAP_W.
MAP_W, MAP_H = 140, 70

# ── Пресеты разрешений (макс 1920×1080) ──
RESOLUTIONS = [
    (1280, 720),
    (1366, 768),
    (1600, 900),
    (1920, 1080),
]

# ── Текущие значения (выставляются через set_resolution) ──
_DEFAULT_W, _DEFAULT_H = 1600, 900
SCREEN_W = _DEFAULT_W
SCREEN_H = _DEFAULT_H
VIEW_W = SCREEN_W // TILE
VIEW_H = (SCREEN_H - HUD_HEIGHT) // TILE


def set_resolution(width, height):
    """Изменить разрешение окна. ОБЯЗАТЕЛЬНО вызывать ДО импорта game/render."""
    global SCREEN_W, SCREEN_H, VIEW_W, VIEW_H
    SCREEN_W = width
    SCREEN_H = height
    VIEW_W = width // TILE
    VIEW_H = (height - HUD_HEIGHT) // TILE


class Tile(IntEnum):
    WALL = 0
    FLOOR = 1
    STAIRS = 2        # вниз, на следующий этаж
    EXIT = 3          # выход из подземелья на поверхность (глубина 1)
    UPSTAIRS = 4      # вверх, на предыдущий этаж (глубина 2+)
    GATE = 5          # переход на другую карту локации (дорога/вход)
    LOCKED_DOOR = 6   # запертая дверь босс-комплекса (нужен ключ босса)
    CHEST = 7         # сундук казны: открывается по «E», стоя рядом


def hud_height():
    """Высота HUD-полосы: весь остаток экрана под игровым полем.

    Поле занимает VIEW_H целых тайлов; раньше снизу оставались чёрные
    полосы высотой до TILE-1 пикселей между полем и HUD. Теперь HUD
    забирает весь остаток — полос больше нет.
    """
    return SCREEN_H - VIEW_H * TILE


def view_origin():
    """Левый верхний угол игрового поля.

    Поле прижато к верхней кромке экрана, а весь остаток высоты отдан
    HUD (см. :func:`hud_height`): чёрные полосы сверху/снизу исчезли.
    По горизонтали поле центрируется, если ширина не делится на тайлы.
    """
    return ((SCREEN_W - VIEW_W * TILE) // 2, 0)

# ── Игровые параметры ──
FOV_RADIUS = 9
# Туман войны: True = классический FOV, False = вся карта видна.
# По умолчанию выключен — игрок видит весь мир сразу.
FOG_OF_WAR_ENABLED = False
MAX_DEPTH = 20
MERCHANT_FLOORS = (3, 6, 9, 12, 15, 18)
# Запасной пул лесных мобов. Гоблины и скелеты в лесу больше не спавнятся:
# они живут в Деревне гоблинов и подземелье соответственно.
OVERWORLD_MONSTERS = ('rat', 'wolf', 'boar')
INVENTORY_COLS = 10
INVENTORY_ROWS = 10
INVENTORY_PAGE_SIZE = INVENTORY_COLS * INVENTORY_ROWS
# Максимум предметов в одном стаке. Оружие и броня не стакаются вовсе.
MAX_STACK = 99
SHOP_PAGE_SIZE = 9
# Сколько рецептов показывать на одной странице окна крафта.
CRAFT_PAGE_SIZE = 8
MINIMAP_SCALE = 2
MINIMAP_MARGIN = 8

SAVE_VERSION = 8

# ── Консоль разработчика ──
# Пароль хранится хешем scrypt (с солью), чтобы он не лежал в сейвах/настройках
# открытым текстом и был устойчив к радужным таблицам.
# Замена sha256 на scrypt — P3 правка безопасности.
# Пароль по умолчанию — НЕ публикуется в README. Переопределяется через
# переменную окружения ETHELGARD_DEV_PASSWORD (или сразу хешем через
# ETHELGARD_DEV_PASSWORD_HASH).
DEV_CONSOLE_PASSWORD_HASH = None  # по умолчанию консоль отключена;
                                       # задайте ETHELGARD_DEV_PASSWORD env var
DEV_CONSOLE_MAX_ATTEMPTS = 3
DEV_CONSOLE_LOCK_SECONDS = 30

# Параметры scrypt для хеширования пароля.
# n=2**14 — memory-hard (16 MB), r=8, p=1 — CPU-hard.
# Устойчив к brute-force на GPU: одна итерация ~100 мс.
DEV_CONSOLE_SCRYPT_N = 2 ** 14
DEV_CONSOLE_SCRYPT_R = 8
DEV_CONSOLE_SCRYPT_P = 1
DEV_CONSOLE_SCRYPT_DKLEN = 32
# Соль генерится один раз при первом запуске и хранится в settings.json.
# Для backward compatibility: если salt не задан — используется этот дефолт.
DEV_CONSOLE_SALT_DEFAULT = b'ethelgard_salt_v1_2026_11'


def dev_console_password_hash():
    """Хеш пароля консоли с учётом переменных окружения.

    Приоритет:
    1. ``ETHELGARD_DEV_PASSWORD_HASH`` env var — прямой hex-хеш (для CI).
    2. ``ETHELGARD_DEV_PASSWORD`` env var — plaintext, хешируется через
       scrypt с солью из settings.json (или дефолтной).
    3. ``DEV_CONSOLE_PASSWORD_HASH`` — ``None`` по умолчанию (консоль
       отключена, пока не задан ``ETHELGARD_DEV_PASSWORD``).

    Возвращает hex-строку хеша. Хеши scrypt помечаются префиксом ``scrypt:``,
    чтобы ``verify_dev_console_password`` мог отличить их от sha256.
    """
    # 1. Явный хеш из env var — высший приоритет.
    explicit = os.environ.get('ETHELGARD_DEV_PASSWORD_HASH')
    if explicit:
        return explicit

    # 2. Plaintext пароль — хешируем через scrypt.
    plain = os.environ.get('ETHELGARD_DEV_PASSWORD')
    if plain:
        salt = _load_dev_console_salt()
        dk = hashlib.scrypt(
            plain.encode('utf-8'),
            salt=salt,
            n=DEV_CONSOLE_SCRYPT_N,
            r=DEV_CONSOLE_SCRYPT_R,
            p=DEV_CONSOLE_SCRYPT_P,
            dklen=DEV_CONSOLE_SCRYPT_DKLEN,
        )
        return 'scrypt:' + dk.hex()

    # 3. Дефолт: None — консоль отключена, пока не задан ETHELGARD_DEV_PASSWORD.
    #    Раньше здесь лежал хеш слова 'ethelgard' — любой мог сбрутфорсить.
    return DEV_CONSOLE_PASSWORD_HASH


def _load_dev_console_salt() -> bytes:
    """Соль для scrypt — из settings.json или дефолтная.

    При первом запуске salt генерируется случайно и сохраняется в
    settings.json. Это prevents радужные таблицы.
    """
    try:
        from core.settings import load_settings, save_settings
        s = load_settings()
        salt_hex = s.get('dev_console_salt')
        if salt_hex:
            salt = bytes.fromhex(salt_hex)
            if len(salt) >= 16:
                return salt
        # Сгенерировать новый salt
        salt = os.urandom(16)
        s['dev_console_salt'] = salt.hex()
        save_settings(s)
        return salt
    except Exception:
        # Фолбэк на дефолтную соль — лучше, чем ничего
        return DEV_CONSOLE_SALT_DEFAULT


def verify_dev_console_password(entered: str, expected_hash: str) -> bool:
    """Проверить пароль dev console через scrypt + сравнение constant-time.

    ``entered`` — plaintext от пользователя.
    ``expected_hash`` — hex-хеш (результат ``dev_console_password_hash``).

    Если хеш начинается с ``scrypt:`` — используется scrypt с солью.
    Иначе — fallback на sha256 (backward compat со старыми сейвами).

    Возвращает True/False. Использует ``hmac.compare_digest`` для
    защиты от timing-атаки.
    """
    import hmac
    if not expected_hash or not entered:
        return False  # консоль отключена, пока не задан ETHELGARD_DEV_PASSWORD
    # scrypt-хеш — имеет префикс 'scrypt:'
    if expected_hash.startswith('scrypt:'):
        salt = _load_dev_console_salt()
        try:
            dk = hashlib.scrypt(
                entered.encode('utf-8'),
                salt=salt,
                n=DEV_CONSOLE_SCRYPT_N,
                r=DEV_CONSOLE_SCRYPT_R,
                p=DEV_CONSOLE_SCRYPT_P,
                dklen=DEV_CONSOLE_SCRYPT_DKLEN,
            )
            return hmac.compare_digest('scrypt:' + dk.hex(), expected_hash)
        except (ValueError, OSError):
            return False
    # Fallback: старый sha256-хеш (backward compat)
    sha = hashlib.sha256(entered.encode('utf-8')).hexdigest()
    return hmac.compare_digest(sha, expected_hash)


# ── Сетевая безопасность: HMAC на сообщениях ──
# Сессионный ключ генерится сервером при hello и отправляется клиенту
# в welcome. Каждое сообщение от клиента подписывается
# hmac.new(session_key, json.dumps(msg).encode(), sha256).hexdigest()
# в поле 'sig'. Сервер проверяет подпись — защита от подмены токена.
NET_HMAC_ALGORITHM = 'sha256'


def compute_message_hmac(session_key: bytes, msg: dict) -> str:
    """Вычислить HMAC сообщения для сетевой защиты."""
    import hmac
    payload = json.dumps(msg, sort_keys=True).encode('utf-8')
    return hmac.new(session_key, payload, NET_HMAC_ALGORITHM).hexdigest()


def verify_message_hmac(session_key: bytes, msg: dict,
                         expected_sig: str) -> bool:
    """Проверить HMAC сообщения. ``msg`` должен БЕЗ поля 'sig'."""
    import hmac
    if not expected_sig or not session_key:
        return False
    msg_copy = {k: v for k, v in msg.items() if k != 'sig'}
    actual = compute_message_hmac(session_key, msg_copy)
    return hmac.compare_digest(actual, expected_sig)


# Опция: пароль на подключение к серверу (LAN-only по умолчанию)
# Если SERVER_PASSWORD env var задан — сервер требует его в hello.
SERVER_PASSWORD_DEFAULT = os.environ.get('ETHELGARD_SERVER_PASSWORD', None)

# ── Real-time таймеры ──
PLAYER_MOVE_COOLDOWN   = 0.12
PLAYER_ATTACK_COOLDOWN = 0.40
PLAYER_RANGED_COOLDOWN = 0.55      # дальнобойная атака — чуть медленнее
MONSTER_BASE_COOLDOWN  = 0.35      # базовый интервал хода обычного моба
STATUS_TICK_INTERVAL   = 0.50
# Регенерация HP/маны замедлена: было 2 сек, стало 5 сек.
# Игрок реже автоматически восстанавливается — расходуем зелья.
REGEN_TICK_INTERVAL    = 5.00
REGEN_HP_PER_TICK      = 1
REGEN_MANA_PER_TICK    = 1

# ── Восстановление добываемых ресурсов (кусты, деревья) ──
# После сбора ресурсов куст/дерево «вырастает» снова через это время.
HARVEST_RESPAWN_TIME   = 120.0     # 2 минуты на восстановление

# ── БАЛАНС — боевые константы ──
# Раньше были магическими числами в core/rules.py; вынесены сюда, чтобы
# их можно было тюнить без grep по коду и писать параметризованные тесты.
DMG_SPREAD = 1            # ±random.randint(-1, 1) к урону (см. resolve_attack)
CRIT_MULT = 1.5           # множитель критического урона
XP_LEVEL_GUARD = 200      # защита от зацикливания при give_xp (макс уровней за один вызов)
DODGE_CAP = 0.30          # кап уклонения от AGI (dodge_base = min(DODGE_CAP, agi * 0.015))
DODGE_TOTAL_CAP = 0.40    # абсолютный кап dodge (со всеми бонусами)
CRIT_BASE_CAP = 0.40      # кап крита от LUK (crit_base = min(CRIT_BASE_CAP, luk * 0.02))
CRIT_TOTAL_CAP = 0.50     # абсолютный кап crit
THORNS_DAMAGE = 2         # ответный урон шипов игрока
# Кривая опыта: xp_next(level) = XP_BASE * level ** XP_EXPONENT.
# Раньше опыт рос как 10 * 1.6**n — после 14-го уровня это миллионы XP,
# то есть уровни становились недостижимыми.
XP_BASE = 10
XP_EXPONENT = 1.35

# Каждый моб ходит по своему таймеру (а не все одним залпом).
MONSTER_ACTION_INTERVAL = 0.35
MONSTER_ACTION_JITTER = 0.20       # ±20% разброса
BOSS_ACTION_INTERVAL = 0.55        # боссы бьют реже, но больнее
BOSS_BREATH_INTERVAL = 4.5         # «дыхание» босса — антикайтинг
BOSS_BREATH_RANGE = (2, 5)
BOSS_BREATH_POWER = 0.6            # доля ATK в уроне от дыхания

# ── Тайминги анимаций ──
ANIM_IDLE_SPEED = 4.0      # скорость покачивания (рад/сек)
ANIM_IDLE_AMP   = 2.5      # амплитуда покачивания, px
ANIM_ATTACK_DUR = 0.25     # длительность анимации удара, сек
ANIM_ATTACK_AMP = 12       # амплитуда выпада при атаке, px

# Скорость полёта снарядов (тайлов в секунду)
PROJECTILE_SPEED_ARROW    = 12.0
PROJECTILE_SPEED_FIREBALL = 8.0
PROJECTILE_SPEED_FROST    = 9.0
PROJECTILE_SPEED_LIGHTNING = 16.0  # молния — мгновенно, но с визуальным эффектом
PROJECTILE_SPEED_HEAL     = 10.0

# ── UI ──
MODAL_MARGIN_X   = 40
MODAL_MARGIN_Y   = 20
MODAL_CONTENT_PAD = 20
MODAL_TOP_PAD = 56         # зона под крестик + заголовок
MODAL_CLOSE_SIZE = 34
MODAL_CLOSE_PAD = 10

TOOLBAR_BTN      = 56
TOOLBAR_GAP      = 8
TOOLBAR_MARGIN   = 12

PLUS_SIZE        = 26
HAMMER_SIZE      = 32

# ── Быстрая панель (quickbar) ──
QUICKBAR_SLOTS   = 10
QUICKBAR_SLOT_SIZE = 48
QUICKBAR_GAP     = 4
QUICKBAR_Y_OFFSET = 8          # от низа экрана

# ── Большая карта (M) ──
BIGMAP_MARGIN    = 80         # отступ от краёв экрана
BIGMAP_TITLE_H   = 40         # полоса под заголовок
BIGMAP_PAD_COLOR = (12, 10, 8)
BIGMAP_BORDER    = (180, 160, 100)

# ── Цвета ──
COL_BG      = (12, 12, 18)
COL_WALL    = (70, 60, 85)
COL_WALL_D  = (30, 26, 38)
COL_FLOOR   = (38, 33, 48)
COL_FLOOR_D = (20, 18, 26)
COL_TEXT    = (225, 225, 235)
COL_DIM     = (110, 110, 130)
COL_PLAYER  = (120, 230, 130)
COL_STAIRS  = (240, 200, 90)

COL_HP      = (200, 60, 60)
COL_HP_BG   = (50, 20, 20)
COL_XP      = (80, 160, 240)
COL_GOLD    = (240, 210, 90)
COL_DMG     = (255, 100, 100)
COL_CRIT    = (255, 200, 50)
COL_HEAL    = (120, 240, 120)
COL_DODGE   = (180, 180, 255)
COL_WIN     = (255, 220, 80)
COL_MERCH   = (240, 200, 100)
