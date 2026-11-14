"""Константы, цвета, enum'ы. Ни от чего не зависит."""
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


def view_origin():
    """Левый верхний угол игрового поля.

    Поле — целое число тайлов, поэтому при некоторых разрешениях остаётся
    несколько пикселей. Центрируем его в области над HUD, иначе карта
    прижимается к левому верхнему углу и смотрится «съехавшей».
    """
    return ((SCREEN_W - VIEW_W * TILE) // 2,
            (SCREEN_H - HUD_HEIGHT - VIEW_H * TILE) // 2)


# ── Игровые параметры ──
FOV_RADIUS = 9
MAX_DEPTH = 20
MERCHANT_FLOORS = (3, 6, 9, 12, 15, 18)
# Запасной пул лесных мобов. Гоблины и скелеты в лесу больше не спавнятся:
# они живут в Деревне гоблинов и подземелье соответственно.
OVERWORLD_MONSTERS = ('rat', 'wolf', 'boar')
INVENTORY_COLS = 5
INVENTORY_ROWS = 5
INVENTORY_PAGE_SIZE = INVENTORY_COLS * INVENTORY_ROWS
SHOP_PAGE_SIZE = 9
# Сколько рецептов показывать на одной странице окна крафта.
CRAFT_PAGE_SIZE = 8
MINIMAP_SCALE = 2
MINIMAP_MARGIN = 8

SAVE_VERSION = 7

# ── Real-time таймеры ──
PLAYER_MOVE_COOLDOWN   = 0.12
PLAYER_ATTACK_COOLDOWN = 0.40
MONSTER_BASE_COOLDOWN  = 0.35      # базовый интервал хода обычного моба
STATUS_TICK_INTERVAL   = 0.50
REGEN_TICK_INTERVAL    = 2.00

# ── БАЛАНС ──
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


class Tile(IntEnum):
    WALL = 0
    FLOOR = 1
    STAIRS = 2        # вниз, на следующий этаж
    EXIT = 3          # выход из подземелья на поверхность (глубина 1)
    UPSTAIRS = 4      # вверх, на предыдущий этаж (глубина 2+)
    GATE = 5          # переход на другую карту локации (дорога/вход)