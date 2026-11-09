"""Экран статистики забега."""
import pygame


class StatsController:
    def __init__(self, game):
        self.game = game

    def open(self):
        self.game.screen_mode = 'stats_run'

    def handle_key(self, key, scancode=0):
        if key in (pygame.K_ESCAPE, pygame.K_TAB):
            self.game.screen_mode = 'game'