"""Фасад рендера. Делегирует в специализированные модули."""
from render.theme import Theme as T
from render.sprites import Sprites
from render.base_renderer import BaseRenderer
from render.icon_renderer import IconRenderer
from render.menu_renderer import MenuRenderer
from render.game_renderer import GameRenderer
from render.screens_renderer import ScreensRenderer
from render.overlay_renderer import OverlayRenderer

class Renderer:
    # Алиасы для обратной совместимости (тесты зовут Renderer._compare_item)
    _compare_item = staticmethod(BaseRenderer.compare_item)
    _format_delta = staticmethod(BaseRenderer.format_delta)

    def __init__(self, screen):
        self.screen = screen
        T.init()
        Sprites.init()
        self.icons   = IconRenderer(screen)
        self.menu    = MenuRenderer(screen, self.icons)
        self.game    = GameRenderer(screen, self.icons)
        self.screens = ScreensRenderer(screen, self.icons)
        self.overlay = OverlayRenderer(screen)

    def draw(self, game):
        if game.screen_mode == 'menu':
            self.menu.draw_main(game); return
        if game.screen_mode == 'menu_saves':
            self.menu.draw_saves(game); return
        if game.screen_mode == 'menu_settings':
            self.menu.draw_settings(game); return
        if game.screen_mode == 'menu_controls':
            self.menu.draw_controls(game); return
        if game.screen_mode == 'menu_multiplayer':
            self.menu.draw_menu_multiplayer(game); return
        if game.screen_mode == 'menu_mp_join':
            self.menu.draw_menu_mp_join(game); return
        if game.screen_mode == 'class_select':
            self.menu.draw_class_select(game); return

        self.screen.fill(T.BG)
        self.game.draw_field(game)
        if game.screen_mode == 'game':
            self.game.draw_minimap(game)
            self.game.draw_hud(game)

        if game.screen_mode == 'stats':
            self.screens.draw_stats(game)
        elif game.screen_mode == 'inventory':
            self.screens.draw_inventory(game)
        elif game.screen_mode == 'shop':
            self.screens.draw_shop(game)
        elif game.screen_mode == 'craft':
            self.screens.draw_craft(game)
        elif game.screen_mode == 'dialogue':
            self.screens.draw_dialogue(game)
        elif game.screen_mode == 'quests':
            self.screens.draw_quests(game)
        elif game.screen_mode == 'stats_run':
            self.screens.draw_run_stats(game)

        if game.dead:
            self.overlay.draw_death(game)
        elif game.victory:
            self.overlay.draw_victory(game)
