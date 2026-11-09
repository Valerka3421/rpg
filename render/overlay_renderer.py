"""Смерть, победа, подсказка внизу."""
from core import config
import pygame
from render.theme import Theme as T
from render.base_renderer import BaseRenderer


class OverlayRenderer(BaseRenderer):
    def __init__(self, screen):
        super().__init__(screen)

    # ════════════════════════ DEATH RECAP ════════════════════════
    def draw_death(self, game):
        ov = pygame.Surface((config.SCREEN_W, config.SCREEN_H), pygame.SRCALPHA)
        ov.fill((0, 0, 0, 220)); self.screen.blit(ov, (0, 0))
        panel_w, panel_h = 700, 480
        rect = ((config.SCREEN_W - panel_w) // 2, (config.SCREEN_H - panel_h) // 2,
                panel_w, panel_h)
        T.panel(self.screen, rect, fill=T.PANEL_DARK)
        T.rounded_rect(self.screen, T.HP_CRIT, rect, width=2, radius=T.R_MD)

        self.text("ВЫ ПОГИБЛИ", config.SCREEN_W // 2, rect[1] + 28,
                  T.HP_CRIT, font=T.FONT_H1, center=True)
        T.divider(self.screen, rect[0] + 40, rect[1] + 70,
                  rect[0] + panel_w - 40, color=T.PANEL_BORDER)

        summary = game.death_summary()
        mins = summary['time_seconds'] // 60
        secs = summary['time_seconds'] % 60
        x0 = rect[0] + 50
        y = rect[1] + 90
        left = [
            (f"Убито мобов: {summary['kills']}", T.TEXT),
            (f"Боссов: {summary['boss_kills']}",
             T.GOLD if summary['boss_kills'] > 0 else T.TEXT_MUTED),
            (f"Уровень: {summary['level']}", T.TEXT),
            (f"Золото: {summary['gold']}", T.GOLD),
            (f"Время: {mins}:{secs:02d}", T.TEXT),
        ]
        for text, color in left:
            self.text(text, x0, y, color, font=T.FONT_BODY)
            y += 30

        x1 = rect[0] + 380
        y = rect[1] + 90
        self.text("Последние события:", x1, y, T.TEXT_DIM, font=T.FONT_SMALL)
        y += 24
        for msg_text in summary['last_messages']:
            color = T.TEXT_MUTED
            for m in game.messages:
                if m.text == msg_text:
                    color = m.color
                    break
            self.text(f"• {msg_text}", x1, y, color, font=T.FONT_SMALL)
            y += 20

        self.text("R — начать заново   ·   F9 — загрузить",
                  config.SCREEN_W // 2, rect[1] + panel_h - 40, T.TEXT,
                  font=T.FONT_BODY, center=True)

    # ════════════════════════ ПОБЕДА ════════════════════════
    def draw_victory(self, game):
        ov = pygame.Surface((config.SCREEN_W, config.SCREEN_H), pygame.SRCALPHA)
        ov.fill((0, 0, 0, 210)); self.screen.blit(ov, (0, 0))
        panel_w, panel_h = 520, 170
        rect = ((config.SCREEN_W - panel_w) // 2, (config.SCREEN_H - panel_h) // 2,
                panel_w, panel_h)
        T.panel(self.screen, rect, fill=T.PANEL_DARK)
        T.rounded_rect(self.screen, T.WIN, rect, width=2, radius=T.R_MD)
        self.text("ПОБЕДА!", config.SCREEN_W // 2, config.SCREEN_H // 2 - 26,
                  T.WIN, font=T.FONT_H1, center=True)
        T.divider(self.screen, rect[0] + 40, config.SCREEN_H // 2 + 14,
                  rect[0] + panel_w - 40, color=T.PANEL_BORDER)
        self.text("R — новый забег   F9 — загрузить",
                  config.SCREEN_W // 2, config.SCREEN_H // 2 + 40, T.TEXT,
                  font=T.FONT_BODY, center=True)

