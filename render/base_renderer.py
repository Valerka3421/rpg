"""Общие примитивы для всех рендереров: текст, градиент, кнопки, сравнение."""
from core import config
import pygame
from render.theme import Theme as T


class BaseRenderer:
    _fit_cache = {}
    FIT_CACHE_MAX = 256

    def __init__(self, screen):
        self.screen = screen

    # ── Текст ──
    def text(self, s, x, y, color, font=None, center=False, align_right=False):
        return T.text(self.screen, s, x, y, color, font=font,
                      center=center, align_right=align_right)

    def fit_text(self, s, max_w, font=None):
        """Обрезает строку под ширину. Результат кэшируется: лог рисуется
        каждый кадр, а текст в нём меняется редко."""
        f = font or T.FONT_BODY
        key = (f, s, max_w)
        cached = self._fit_cache.get(key)
        if cached is not None:
            return cached
        if f.size(s)[0] <= max_w:
            result = s
        else:
            cut = s.rstrip()
            while cut and f.size(cut + "…")[0] > max_w:
                cut = cut[:-1]
            result = cut + "…"
        if len(self._fit_cache) >= self.FIT_CACHE_MAX:
            for old in list(self._fit_cache)[: self.FIT_CACHE_MAX // 2]:
                del self._fit_cache[old]
        self._fit_cache[key] = result
        return result

    # ── Фон ──
    _gradient_cache = {}

    def draw_gradient_bg(self, top, bottom):
        """Фон меню. Раньше рисовал SCREEN_H линий каждый кадр — теперь
        градиент строится один раз на размер/цвета и блитится."""
        key = (tuple(top), tuple(bottom), config.SCREEN_W, config.SCREEN_H)
        surf = self._gradient_cache.get(key)
        if surf is None:
            surf = pygame.Surface((config.SCREEN_W, config.SCREEN_H))
            for y in range(config.SCREEN_H):
                t = y / max(1, config.SCREEN_H - 1)
                c = tuple(int(top[j] * (1 - t) + bottom[j] * t)
                          for j in range(3))
                pygame.draw.line(surf, c, (0, y), (config.SCREEN_W, y))
            if len(self._gradient_cache) > 4:
                self._gradient_cache.clear()
            self._gradient_cache[key] = surf
        self.screen.blit(surf, (0, 0))

    # ── Кнопка ──
    _button_cache = {}

    def _button_parts(self, w, h, selected, radius):
        key = (w, h, selected, radius)
        cached = self._button_cache.get(key)
        if cached is not None:
            return cached
        shadow = pygame.Surface((w + 12, h + 16), pygame.SRCALPHA)
        pygame.draw.rect(shadow, (0, 0, 0, 110), (6, 8, w, h),
                         border_radius=radius)

        if selected:
            top_c, bottom_c = (96, 76, 44), (56, 44, 25)
            border = (206, 166, 82)
            glow = True
        else:
            top_c, bottom_c = (44, 39, 31), (24, 21, 17)
            border = (72, 62, 46)
            glow = False

        grad = pygame.Surface((w, h), pygame.SRCALPHA)
        for i in range(h):
            t = i / max(1, h - 1)
            c = tuple(int(top_c[j] * (1 - t) + bottom_c[j] * t)
                      for j in range(3))
            pygame.draw.line(grad, c, (0, i), (w, i))
        mask = pygame.Surface((w, h), pygame.SRCALPHA)
        pygame.draw.rect(mask, (255, 255, 255, 255), (0, 0, w, h),
                         border_radius=radius)
        grad.blit(mask, (0, 0), special_flags=pygame.BLEND_RGBA_MULT)
        pygame.draw.rect(grad, border, (0, 0, w, h), width=2,
                         border_radius=radius)
        if glow:
            pygame.draw.line(grad, (236, 206, 142),
                             (10, 1), (w - 10, 1), 1)
            inner = pygame.Surface((w, h), pygame.SRCALPHA)
            pygame.draw.rect(inner, (206, 166, 82, 24), (0, 0, w, h),
                             border_radius=radius)
            grad.blit(inner, (0, 0))
        parts = (shadow, grad)
        if len(self._button_cache) > 64:
            self._button_cache.clear()
        self._button_cache[key] = parts
        return parts

    def draw_button_bg(self, rect, selected, radius=10):
        x, y, w, h = rect
        shadow, body = self._button_parts(w, h, selected, radius)
        self.screen.blit(shadow, (x - 6, y - 6))
        self.screen.blit(body, (x, y))

    # ── Сравнение предметов ──
    # ── Сравнение предметов ──
    @staticmethod
    def compare_item(game, new_key, slot):
        from core.entities import Player
        p = game.player
        current_key = p.weapon if slot == 'weapon' else p.armor
        if current_key == new_key:
            return []
        base = {
            'str': p.str, 'agi': p.agi, 'vit': p.vit,
            'luk': p.luk, 'df_stat': p.df_stat,
        }
        cur = Player.preview_stats({
            **base,
            'weapon': current_key if slot == 'weapon' else p.weapon,
            'armor': current_key if slot == 'armor' else p.armor,
        })
        new = Player.preview_stats({
            **base,
            'weapon': new_key if slot == 'weapon' else p.weapon,
            'armor': new_key if slot == 'armor' else p.armor,
        })
        deltas = []
        da = new['atk'] - cur['atk']
        dd = new['df'] - cur['df']
        dh = new['max_hp'] - cur['max_hp']
        dodge = (new['dodge'] - cur['dodge']) * 100
        crit = (new['crit'] - cur['crit']) * 100
        if da != 0: deltas.append(('ATK', da, False))
        if dd != 0: deltas.append(('DEF', dd, False))
        if dh != 0: deltas.append(('HP', dh, False))
        if abs(dodge) >= 1: deltas.append(('Укл', int(round(dodge)), True))
        if abs(crit) >= 1: deltas.append(('Крит', int(round(crit)), True))
        return deltas

    @staticmethod
    def format_delta(deltas):
        if not deltas:
            return None, None
        numeric = [d for d in deltas if not d[2]] or deltas
        total = sum(d[1] for d in numeric)
        positive = total > 0
        color = (140, 212, 116) if positive else (210, 92, 74)
        arrow = "▲" if positive else "▼"
        parts = []
        for name, val, is_pct in deltas:
            sign = "+" if val > 0 else "−"
            suffix = "%" if is_pct else ""
            parts.append(f"{name} {sign}{abs(val)}{suffix}")
        return f"{arrow} {', '.join(parts)}", color