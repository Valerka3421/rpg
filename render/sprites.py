"""Загрузчик спрайтов с кэшем, рекурсивным поиском и fallback."""
import os
import pygame

from core.config import TILE, ASSETS_DIR


class Sprites:
    _cache = {}
    _root = None
    _index = {}

    @classmethod
    def init(cls, root=None):
        if root is None:
            root = os.path.join(ASSETS_DIR, 'sprites')
        cls._root = root
        cls._cache.clear()
        cls._index.clear()
        if not os.path.isdir(root):
            return
        for dirpath, _dirnames, filenames in os.walk(root):
            rel = os.path.relpath(dirpath, root).replace('\\', '/')
            rel = '' if rel == '.' else rel
            for fname in filenames:
                if not fname.lower().endswith('.png'):
                    continue
                stem = os.path.splitext(fname)[0]
                full = f"{rel}/{stem}" if rel else stem
                cls._index[full] = full
                if stem not in cls._index:
                    cls._index[stem] = full

    @classmethod
    def has(cls, name):
        return name in cls._index

    @classmethod
    def get(cls, name, size=(TILE, TILE), finish=False):
        """PNG-спрайт (если есть). `finish` — для сущностей: чёткое
        масштабирование пиксель-арта + обводка/тень как у процедурных."""
        if not cls._index:
            return None
        key = (name, size, finish)
        if key in cls._cache:
            return cls._cache[key]
        resolved = cls._index.get(name)
        if resolved is None:
            base = name.rsplit('/', 1)[-1]
            resolved = cls._index.get(base)
        if resolved is None:
            cls._cache[key] = None
            return None
        path = os.path.join(cls._root, resolved + '.png')
        try:
            img = pygame.image.load(path).convert_alpha()
            if img.get_size() != size:
                if finish:
                    img = pygame.transform.scale(img, size)   # nearest: пиксель-арт
                else:
                    img = pygame.transform.smoothscale(img, size)
            if finish:
                from render import textures
                img = textures.finish(img, shadow=True)
            cls._cache[key] = img
            return img
        except Exception:
            cls._cache[key] = None
            return None

    @classmethod
    def debug_list(cls):
        return sorted(set(cls._index.values()))