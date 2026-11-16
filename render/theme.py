"""Тема оформления — «CSS» для pygame-интерфейса."""
import math
import pygame


class Theme:
    # Палитра Stoneshard: тёплая почти-чёрная база, бронза, пергамент.
    # Холодные синие/фиолетовые оттенки оставлены только глубоким теням.
    BG          = (14, 12, 10)
    BG_HUD      = (9, 8, 7)
    BG_MAP_DIM  = (18, 16, 13)
    PANEL       = (30, 26, 21)
    PANEL_DARK  = (18, 16, 13)
    PANEL_HEADER = (42, 36, 28)
    PANEL_BORDER = (78, 66, 48)
    PANEL_BORDER_DARK = (46, 39, 29)

    TEXT        = (222, 214, 198)
    TEXT_DIM    = (150, 140, 120)
    TEXT_MUTED  = (98, 90, 74)

    HP          = (166, 48, 42)
    HP_CRIT     = (220, 84, 64)
    HP_BG       = (44, 22, 18)
    XP          = (92, 126, 138)
    GOLD        = (206, 166, 82)
    DMG         = (214, 82, 66)
    CRIT        = (232, 178, 74)
    HEAL        = (120, 172, 104)
    DODGE       = (150, 166, 178)
    WIN         = (222, 190, 96)
    MERCH       = (206, 170, 96)
    KEY_BG      = (36, 31, 24)
    KEY_BORDER  = (72, 62, 46)
    KEY_TEXT    = (216, 206, 186)

    WALL        = (74, 68, 59)
    WALL_DIM    = (32, 29, 25)
    FLOOR       = (56, 52, 45)
    FLOOR_DIM   = (22, 20, 17)
    PLAYER      = (140, 186, 132)
    STAIRS      = (206, 166, 82)

    # Заголовки и акценты интерфейса — бронза/пергамент
    TITLE       = (214, 178, 98)
    TITLE_LIGHT = (238, 222, 188)
    ROW_HOVER   = (54, 45, 33)
    ROW_BORDER  = (110, 91, 62)

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
        # После pygame.quit()/init() старые шрифты и кэш поверхностей
        # недействительны, хотя _initialized остался True.
        if cls._initialized and pygame.font.get_init():
            return
        if not pygame.font.get_init():
            pygame.font.init()
        cls._initialized = False
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
        return tuple(max(0, min(255, int(c * factor))) for c in color[:3])

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
            pygame.draw.rect(sh, (5, 4, 3, alpha), sh.get_rect(), border_radius=radius)
        except TypeError:
            sh.fill((5, 4, 3, alpha))
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
        t = cls.render(f"  {title}  ", (190, 172, 138), f)
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