"""Настройки игры — сохранение/загрузка settings.json."""
import os
import json
from core.config import DATA_DIR


SETTINGS_FILE = os.path.join(DATA_DIR, 'settings.json')

DEFAULTS = {
    'volume': 7,
    'fullscreen': False,
    'show_fps': False,
    'show_minimap': True,
    'resolution': [1600, 900],
}

def load_settings():
    if not os.path.exists(SETTINGS_FILE):
        return dict(DEFAULTS)
    try:
        with open(SETTINGS_FILE, 'r', encoding='utf-8') as f:
            data = json.load(f)
        merged = dict(DEFAULTS)
        merged.update(data)
        return merged
    except Exception:
        return dict(DEFAULTS)


def save_settings(settings):
    """Атомарная запись: при сбое старый settings.json остаётся целым."""
    tmp = SETTINGS_FILE + '.tmp'
    try:
        with open(tmp, 'w', encoding='utf-8') as f:
            json.dump(settings, f, indent=2)
        os.replace(tmp, SETTINGS_FILE)
    except Exception:
        try:
            os.remove(tmp)
        except OSError:
            pass