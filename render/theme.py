"""Тема оформления — «CSS» для pygame-интерфейса."""
import math
import pygame


class Theme:
    BG          = (12, 12, 18)
    BG_HUD      = (8, 8, 14)
    BG_MAP_DIM  = (20, 18, 26)
    PANEL       = (22, 22, 32)
    PANEL_DARK  = (14, 14, 22)
    PANEL_HEADER = (32, 32, 46)
    PANEL_BORDER = (55, 55, 75)
    PANEL_BORDER_DARK = (35, 35, 50)

    TEXT        = (225, 225, 235)
    TEXT_DIM    = (140, 140, 155)
    TEXT_MUTED  = (90, 90, 105)

    HP          = (200, 60, 60)
    HP_CRIT     = (255, 100, 100)
    HP_BG       = (50, 20, 20)
    XP          = (80, 160, 240)
    GOLD        = (240, 210, 90)
    DMG         = (255, 100, 100)
    CRIT        = (255, 200, 50)
    HEAL        = (120, 240, 120)
    DODGE       = (180, 180, 255)
    WIN         = (255, 220, 80)
    MERCH       = (240, 200, 100)
    KEY_BG      = (40, 40, 60)
    KEY_BORDER  = (70, 70, 100)
    KEY_TEXT    = (220, 220, 240)

    WALL        = (70, 60, 85)
    WALL_DIM    = (30, 26, 38)
    FLOOR       = (38, 33, 48)
    FLOOR_DIM   = (20, 18, 26)
    PLAYER      = (120, 230, 130)
    STAIRS      = (240, 200, 90)

    R_SM = 3
    R_MD = 6
    R_LG = 10

    FONT_H1    = None
    FONT_H2    = None
    FONT_BODY  = None
    FONT_SMALL = None
    FONT_TINY  = None
    _initialized = False

    # Кэш отрендеренного текста: pygame.font.Surface создаётся заново на
    # каждый вызов render(), а это самый дорогой шаг в кадре (десятки
    # рендеров глифов). Поверхности только блитятся — мутировать их нельзя.
    _text_cache = {}
    TEXT_CACHE_MAX = 1024

    @classmethod
    def init(cls):
        if cls._initialized:
            return
        if not pygame.font.get_init():
            pygame.font.init()
        cls.FONT_H1    = pygame.font.SysFont("consolas", 40, bold=True)
        cls.FONT_H2    = pygame.font.SysFont("consolas", 22, bold=True)
        cls.FONT_BODY  = pygame.font.SysFont("consolas", 18)
        cls.FONT_SMALL = pygame.font.SysFont("consolas", 14)
        cls.FONT_TINY  = pygame.font.SysFont("consolas", 12)
        cls._text_cache.clear()
        cls._initialized = True

    @classmethod
    def render(cls, s, color, font=None):
        """Отрендеренный текст из кэша. Результат нельзя менять на месте."""
        f = font or cls.FONT_BODY
        key = (f, s, color)
        surf = cls._text_cache.get(key)
        if surf is not None:
            return surf
        surf = f.render(s, True, color)
        if len(cls._text_cache) >= cls.TEXT_CACHE_MAX:
            # простая эвикция половины — переполнение случается редко
            for old in list(cls._text_cache)[: cls.TEXT_CACHE_MAX // 2]:
                del cls._text_cache[old]
        cls._text_cache[key] = surf
        return surf

    @classmethod
    def clear_text_cache(cls):
        cls._text_cache.clear()

    @staticmethod
    def shade(color, factor):
        return tuple(max(0, min(255, int(c * factor))) for c in color)

    @staticmethod
    def tint(color, x, y, seed=0, amount=0.10):
        h = ((x * 73856093) ^ (y * 19349663) ^ seed) & 0x7fffffff
        var = ((h % 201) - 100) / 100 * amount
        return tuple(max(0, min(255, int(c * (1 + var)))) for c in color)

    @staticmethod
    def rounded_rect(surface, color, rect, radius=6, width=0, border_color=None):
        try:
            pygame.draw.rect(surface, color, rect, width=width, border_radius=radius)
            if border_color is not None and width == 0:
                pygame.draw.rect(surface, border_color, rect, width=1,
                                 border_radius=radius)
        except TypeError:
            pygame.draw.rect(surface, color, rect, width=width)
            if border_color is not None and width == 0:
                pygame.draw.rect(surface, border_color, rect, width=1)

    @staticmethod
    def shadow(surface, rect, offset=3, alpha=120, radius=6):
        sh = pygame.Surface((rect[2], rect[3]), pygame.SRCALPHA)
        try:
            pygame.draw.rect(sh, (0, 0, 0, alpha), sh.get_rect(), border_radius=radius)
        except TypeError:
            sh.fill((0, 0, 0, alpha))
        surface.blit(sh, (rect[0] + offset, rect[1] + offset))

    @classmethod
    def panel(cls, surface, rect, fill=None, border=None, radius=None, shadow=True):
        radius = radius if radius is not None else cls.R_MD
        if shadow:
            cls.shadow(surface, rect, radius=radius)
        cls.rounded_rect(surface, fill or cls.PANEL, rect, radius=radius,
                         border_color=border or cls.PANEL_BORDER)

    @classmethod
    def text(cls, surface, s, x, y, color, font=None, center=False,
             align_right=False):
        surf = cls.render(s, color, font)
        if center:
            surface.blit(surf, surf.get_rect(center=(x, y)))
        elif align_right:
            surface.blit(surf, (x - surf.get_width(), y))
        else:
            surface.blit(surf, (x, y))
        return surf.get_width()

    @classmethod
    def section(cls, surface, x, y, width, title):
        f = cls.FONT_H2
        t = cls.render(f"  {title}  ", (180, 180, 220), f)
        cx = x + width // 2
        surface.blit(t, (cx - t.get_width() // 2, y))
        line_y = y + t.get_height() // 2
        side = (width - t.get_width()) // 2 - 6
        if side > 0:
            pygame.draw.line(surface, cls.PANEL_BORDER,
                             (x, line_y), (cx - t.get_width() // 2, line_y), 1)
            pygame.draw.line(surface, cls.PANEL_BORDER,
                             (cx + t.get_width() // 2, line_y),
                             (x + width, line_y), 1)
        return y + t.get_height() + cls.R_MD

    @classmethod
    def divider(cls, surface, x1, y, x2, color=None):
        pygame.draw.line(surface, color or cls.PANEL_BORDER_DARK,
                         (x1, y), (x2, y), 1)

    @classmethod
    def keycap(cls, surface, x, y, key, font=None):
        f = font or cls.FONT_SMALL
        surf = cls.render(key, cls.KEY_TEXT, f)
        w = surf.get_width() + 10
        h = surf.get_height() + 4
        cls.rounded_rect(surface, cls.KEY_BG, (x, y, w, h),
                         radius=cls.R_SM, border_color=cls.KEY_BORDER)
        surface.blit(surf, (x + 5, y + 1))
        return w

    @classmethod
    def bar(cls, surface, rect, ratio, color, bg_color=None, radius=None, pulse=False):
        radius = radius if radius is not None else cls.R_SM
        bg_color = bg_color or cls.HP_BG
        cls.rounded_rect(surface, bg_color, rect, radius=radius)
        fill_w = int(rect[2] * max(0.0, min(1.0, ratio)))
        if fill_w > 0:
            c = color
            if pulse:
                phase = (pygame.time.get_ticks() % 800) / 800 * 2 * math.pi
                factor = 0.7 + 0.3 * (0.5 + 0.5 * math.sin(phase))
                c = cls.shade(cls.HP_CRIT, factor)
            cls.rounded_rect(surface, c,
                             (rect[0], rect[1], fill_w, rect[3]), radius=radius)
        cls.rounded_rect(surface, cls.PANEL_BORDER, rect, width=1, radius=radius)

    @classmethod
    def icon_box(cls, surface, x, y, ch, color, size=22):
        rect = (x, y, size, size)
        cls.rounded_rect(surface, cls.PANEL_DARK, rect,
                         radius=cls.R_SM, border_color=cls.PANEL_BORDER)
        cls.text(surface, ch, x + size // 2, y + size // 2 - 1, color,
                 font=cls.FONT_BODY, center=True)

    @classmethod
    def badge(cls, surface, x, y, text, color=None, bg=None, font=None):
        f = font or cls.FONT_TINY
        color = color or cls.TEXT
        bg = bg or cls.PANEL_HEADER
        surf = cls.render(f" {text} ", color, f)
        w = surf.get_width() + 6
        h = surf.get_height() + 2
        cls.rounded_rect(surface, bg, (x, y, w, h), radius=cls.R_SM)
        surface.blit(surf, (x + 3, y + 1))
        return w