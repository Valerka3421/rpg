"""Игровое поле: карта, сущности, HUD, мини-карта, floating texts."""
from core import config
from core import overworld as ov
import pygame
import math
from core.config import (TILE, MAP_W, MAP_H, MAX_DEPTH, Tile,
                         MINIMAP_SCALE, MINIMAP_MARGIN, FOV_RADIUS)
from core.data import STATUSES, CLASSES
from render.theme import Theme as T
from render.sprites import Sprites
from render.tiles import Tiles
from render.base_renderer import BaseRenderer

from render.constants import MONSTER_SPRITES

# Глубокие тени Stoneshard — не серые, а холодные сине-зелёные.
DARK_TINT = (5, 7, 12)

# Должно совпадать с TTL в game.floats (создаётся в game/game.py).
# Вынесено из _draw_floats, чтобы не был «магическим числом».
FLOAT_TTL = 0.7

class GameRenderer(BaseRenderer):
    def __init__(self, screen, icons):
        super().__init__(screen)
        self.icons = icons
        self._shadow_sm = pygame.Surface((TILE, TILE), pygame.SRCALPHA)
        pygame.draw.ellipse(self._shadow_sm, (0, 0, 0, 105),
                            (TILE // 5, TILE - 10, TILE * 3 // 5, 6))
        self._shadow_lg = pygame.Surface((TILE, TILE), pygame.SRCALPHA)
        pygame.draw.ellipse(self._shadow_lg, (0, 0, 0, 125),
                            (TILE // 6, TILE - 10, TILE * 2 // 3, 8))
        self._tile_wall = Sprites.get('tiles/wall', (TILE, TILE))
        self._tile_floor = Sprites.get('tiles/floor', (TILE, TILE))
        self._tile_stairs = Sprites.get('tiles/stairs', (TILE, TILE))
        self._tile_exit = Sprites.get('tiles/exit', (TILE, TILE))
        self._tile_upstairs = Sprites.get('tiles/upstairs', (TILE, TILE))
        self._is_overworld = False
        # Тени у основания стен: пол рядом со стеной темнеет
        self._wall_shadow_h = pygame.Surface((TILE, 9), pygame.SRCALPHA)
        for i in range(9):
            alpha = int(58 * (1 - i / 9) ** 1.6)
            pygame.draw.line(self._wall_shadow_h, (8, 9, 13, alpha),
                             (0, i), (TILE, i))
        self._wall_shadow_v = pygame.Surface((9, TILE), pygame.SRCALPHA)
        for i in range(9):
            alpha = int(46 * (1 - i / 9) ** 1.6)
            pygame.draw.line(self._wall_shadow_v, (8, 9, 13, alpha),
                             (i, 0), (i, TILE))
        # Кэши: карта и основа миникарты перерисовываются редко
        self._map_surface = None
        self._map_key = None
        self._field_surface = None
        self._light_grad = None
        self._mini_scaled = None
        self._mini_key = None
        self._toolbar_shadow = {}

        self._light_gradient()

    # ════════════════════════ ПОЛЕ ════════════════════════
    @staticmethod
    def _rx(entity):
        """Координата для отрисовки: в MP — интерполированная."""
        return getattr(entity, 'render_x', entity.x)

    @staticmethod
    def _ry(entity):
        return getattr(entity, 'render_y', entity.y)

    def visible_remotes(self, game):
        """Другие игроки в поле зрения (миникарта показывает всех)."""
        my_id = getattr(game, 'my_id', None)
        visible = game.visible
        result = []
        for pid, player in getattr(game, 'remote_players', {}).items():
            if pid == my_id:
                continue
            if (int(self._rx(player)), int(self._ry(player))) in visible:
                result.append(player)
        return result

    def draw_field(self, game):
        px, py = self._rx(game.player), self._ry(game.player)
        field_w = getattr(game.dungeon, 'w', MAP_W)
        field_h = getattr(game.dungeon, 'h', MAP_H)
        cam_x = max(0, min(int(px) - config.VIEW_W // 2,
                           max(0, field_w - config.VIEW_W)))
        cam_y = max(0, min(int(py) - config.VIEW_H // 2,
                           max(0, field_h - config.VIEW_H)))

        size = (config.VIEW_W * TILE, config.VIEW_H * TILE)
        if self._field_surface is None or self._field_surface.get_size() != size:
            self._field_surface = pygame.Surface(size)
        previous = self.screen
        # Иконки (игрок, мобы, лут, NPC) — отдельный рендерер со своим
        # self.screen. Без синхронизации они рисовались бы на дисплее
        # и тут же затирались блитом готового поля.
        previous_icons_screen = self.icons.screen
        self.screen = self._field_surface
        self.icons.screen = self._field_surface
        try:
            self._field_surface.fill(T.BG)
            # 1) Пол, лестницы, выход — низкий слой
            self._draw_map_cached(game, cam_x, cam_y)
            # 1б) Кусты ягод стоят на полу, поверх тайла земли.
            self._draw_harvestables(game, cam_x, cam_y)
            # 2) Объекты на полу
            self._draw_loot(game, cam_x, cam_y)
            self._draw_monsters(game, cam_x, cam_y)
            self._draw_merchant(game, cam_x, cam_y)
            self._draw_elder(game, cam_x, cam_y)
            self._draw_quest_board(game, cam_x, cam_y)
            self._draw_player(game, cam_x, cam_y)
            for remote in self.visible_remotes(game):
                self._draw_remote_player(remote, cam_x, cam_y)
            # 3) Стены/деревья уже нарисованы в слое карты. Сущности
            # (игрок, мобы, лут, NPC) всегда остаются ПОВЕРХ текстур мира.
            # 4) Всплывающие цифры — поверх всего
            self._draw_floats(game, cam_x, cam_y)
        finally:
            self.screen = previous
            self.icons.screen = previous_icons_screen

        ox, oy = config.view_origin()
        self.screen.blit(self._field_surface, (ox, oy))
        self.draw_toolbar(game)


    def _draw_map_cached(self, game, cam_x, cam_y):
        """Карта (стены/пол/лестницы) меняется только при шаге, смене этажа
        или открытии новых клеток — кэшируем её в отдельную поверхность."""
        key = (id(game), cam_x, cam_y, config.VIEW_W, config.VIEW_H,
               game.world_mode, game.depth, getattr(game, 'location', 'village'),
               int(self._rx(game.player)), int(self._ry(game.player)),
               len(game.explored), len(game.visible),
               getattr(game, 'render_revision', 0))
        size = (config.VIEW_W * TILE, config.VIEW_H * TILE)
        if self._map_surface is None or self._map_surface.get_size() != size:
            self._map_surface = pygame.Surface(size)
            self._map_key = None
        if key != self._map_key:
            previous = self.screen
            self.screen = self._map_surface
            try:
                self._map_surface.fill(T.BG)
                self._draw_map(game, cam_x, cam_y)
            finally:
                self.screen = previous
            self._map_key = key
        self.screen.blit(self._map_surface, (0, 0))

    def _draw_map(self, game, cam_x, cam_y):
        """Тайлы + запечённое освещение + ореолы факелов."""
        # Шахта — подземная локация: рисуем её как пещеру и оставляем
        # туман войны, хотя формально это часть поверхности.
        self._is_overworld = (game.world_mode == 'overworld'
                              and getattr(game, 'location', 'village') != 'mine')
        self.game = game
        self._game_location = getattr(game, 'location', 'village')
        torches = []
        for sy in range(config.VIEW_H):
            for sx in range(config.VIEW_W):
                mx, my = sx + cam_x, sy + cam_y
                if (mx, my) not in game.explored:
                    continue
                px, py = sx * TILE, sy * TILE
                vis = (mx, my) in game.visible
                light = 1.0 if vis else 0.72
                tile = game.dungeon.tiles[my][mx]
                if tile == Tile.WALL:
                    record = game.dungeon.harvestables.get((mx, my))
                    if record and record.get('kind') == 'ore':
                        key = ('ore_rock' if record.get('has', True)
                               else 'ore_depleted')
                        self.screen.blit(Tiles.get(key, 0, light), (px, py))
                        continue
                    if record and record.get('kind') == 'tree' \
                            and not record.get('has', True):
                        self.screen.blit(Tiles.get('stump', 0, light), (px, py))
                        continue
                    if self._draw_wall(px, py, mx, my, light) and vis:
                        torches.append((px, py))
                    continue
                self._draw_floor(px, py, mx, my, light)
                if tile == Tile.STAIRS:
                    self._draw_stairs(px, py, light)
                elif tile == Tile.EXIT:
                    self._draw_exit(px, py, light)
                elif tile == Tile.UPSTAIRS:
                    self._draw_upstairs(px, py, light)
                elif tile == Tile.GATE:
                    self._draw_gate(game, px, py, mx, my, light)
        if not self._is_overworld:
            self._draw_darkness(game, cam_x, cam_y)
            self._draw_wall_shadows(game, cam_x, cam_y)
        if torches:
            glow = Tiles.torch_glow()
            half = glow.get_width() // 2
            for px, py in torches:
                self.screen.blit(
                    glow, (px + TILE // 2 - half, py + 18 - half),
                    special_flags=pygame.BLEND_RGB_ADD)

    def _draw_walls_layer(self, game, cam_x, cam_y):
        """Стены и деревья поверх сущностей — эффект глубины.

        Рисуем на финальном экране (не в _map_surface), потому что этот слой
        должен перекрывать игрока/мобов и «нависать» над сценой.
        """
        tiles = game.dungeon.tiles
        torches = []
        for sy in range(config.VIEW_H):
            for sx in range(config.VIEW_W):
                mx, my = sx + cam_x, sy + cam_y
                if (mx, my) not in game.explored:
                    continue
                if tiles[my][mx] != Tile.WALL:
                    continue
                px, py = sx * TILE, sy * TILE
                vis = (mx, my) in game.visible
                light = 1.0 if vis else 0.72

                if self._is_overworld:
                    # Верхний слой должен выглядеть ровно как нижний:
                    # те же варианты тайлов, но поверх сущностей.
                    self._draw_wall(px, py, mx, my, light)
                else:
                    if self._tile_wall:
                        self.screen.blit(self._tile_wall, (px, py))
                        _, kind = Tiles.variant('wall', mx, my)
                        if kind == 'wall_torch' and vis:
                            torches.append((px, py))
                    else:
                        variant, kind = Tiles.variant('wall', mx, my)
                        self.screen.blit(Tiles.get(kind, variant, light), (px, py))
                        if kind == 'wall_torch' and vis:
                            torches.append((px, py))

        if torches:
            glow = Tiles.torch_glow()
            half = glow.get_width() // 2
            for px, py in torches:
                self.screen.blit(
                    glow, (px + TILE // 2 - half, py + 18 - half),
                    special_flags=pygame.BLEND_RGB_ADD)

    def _light_gradient(self):
        """Круглый градиент тьмы: 0 в центре, максимум к радиусу.

        Строится в половинном разрешении и растягивается — так в разы
        дешевле, а на глаз разницы нет.
        """
        if self._light_grad is None:
            radius = (FOV_RADIUS + 1) * TILE
            half = max(1, radius // 2)
            small = pygame.Surface((half * 2, half * 2), pygame.SRCALPHA)
            small.fill((*DARK_TINT, 135))
            for r in range(half, 0, -2):
                t = r / half
                alpha = int(135 * t ** 1.7)
                pygame.draw.circle(small, (*DARK_TINT, alpha), (half, half), r)
            self._light_grad = pygame.transform.smoothscale(
                small, (radius * 2, radius * 2))
        return self._light_grad

    def _draw_darkness(self, game, cam_x, cam_y):
        """Плавное затемнение к краю обзора — без ступеней по тайлам."""
        size = (config.VIEW_W * TILE, config.VIEW_H * TILE)
        overlay = pygame.Surface(size, pygame.SRCALPHA)
        overlay.fill((*DARK_TINT, 135))
        grad = self._light_gradient()
        radius = grad.get_width() // 2
        sx = (int(self._rx(game.player)) - cam_x) * TILE + TILE // 2
        sy = (int(self._ry(game.player)) - cam_y) * TILE + TILE // 2
        overlay.blit(grad, (sx - radius, sy - radius),
                     special_flags=pygame.BLEND_RGBA_MIN)
        self.screen.blit(overlay, (0, 0))

    def _draw_wall_shadows(self, game, cam_x, cam_y):
        """Мягкая тень от стены на полу — стены «стоят», а не нарисованы."""
        tiles = game.dungeon.tiles
        for sy in range(config.VIEW_H):
            for sx in range(config.VIEW_W):
                mx, my = sx + cam_x, sy + cam_y
                if (mx, my) not in game.visible:
                    continue
                if tiles[my][mx] == Tile.WALL:
                    continue
                px, py = sx * TILE, sy * TILE
                if my > 0 and tiles[my - 1][mx] == Tile.WALL:
                    self.screen.blit(self._wall_shadow_h, (px, py))
                if mx > 0 and tiles[my][mx - 1] == Tile.WALL:
                    self.screen.blit(self._wall_shadow_v, (px, py))

    def _draw_wall(self, px, py, mx, my, light=1.0):
        """Возвращает True, если это стена с факелом."""
        if self._is_overworld:
            if (getattr(self, '_game_location', 'village') == 'village'
                    and ov.is_well(mx, my)):
                self.screen.blit(Tiles.get('village_well', 0, light), (px, py))
                return False
            fn = getattr(self.game, 'wall_kind', None)
            kind = fn(mx, my) if fn else ov.wall_kind(mx, my)
            variant, kind = Tiles.variant(kind, mx, my)
            self.screen.blit(Tiles.get(kind, variant, light), (px, py))
            return False
        if self._tile_wall:
            self.screen.blit(self._tile_wall, (px, py))
            _, kind = Tiles.variant('wall', mx, my)
            return kind == 'wall_torch'
        variant, kind = Tiles.variant('wall', mx, my)
        self.screen.blit(Tiles.get(kind, variant, light), (px, py))
        return kind == 'wall_torch'

    def _draw_floor(self, px, py, mx, my, light=1.0):
        if self._is_overworld:
            fn = getattr(self.game, 'ground_kind', None)
            kind = fn(mx, my) if fn else ov.ground_kind(mx, my)
            variant, kind = Tiles.variant(kind, mx, my)
            self.screen.blit(Tiles.get(kind, variant, light), (px, py))
            return
        if self._tile_floor:
            self.screen.blit(self._tile_floor, (px, py))
            return
        variant, kind = Tiles.variant('floor', mx, my)
        self.screen.blit(Tiles.get(kind, variant, light), (px, py))

    def _draw_stairs(self, px, py, light=1.0):
        if self._is_overworld:
            self.screen.blit(Tiles.get('cave', 0, light), (px, py))
            return
        if self._tile_stairs:
            self.screen.blit(self._tile_stairs, (px, py))
            return
        self.screen.blit(Tiles.get('stairs', 0, light), (px, py))

    def _draw_upstairs(self, px, py, light=1.0):
        if self._tile_upstairs:
            self.screen.blit(self._tile_upstairs, (px, py))
            return
        self.screen.blit(Tiles.get('upstairs', 0, light), (px, py))

    def _draw_exit(self, px, py, light=1.0):
        if self._tile_exit:
            self.screen.blit(self._tile_exit, (px, py))
            return
        self.screen.blit(Tiles.get('exit', 0, light), (px, py))

    def _draw_gate(self, game, px, py, mx, my, light=1.0):
        """Врата локации. Вход в шахту выглядит как пещера."""
        trans = game.dungeon.transitions.get((mx, my), {})
        kind = 'cave' if trans.get('to') == 'mine' else 'gate'
        self.screen.blit(Tiles.get(kind, 0, light), (px, py))

    def _draw_harvestables(self, game, cam_x, cam_y):
        """Кусты ягод: с ягодами или пустые, если ягоды уже собрали."""
        for (mx, my), record in game.dungeon.harvestables.items():
            if record.get('kind') != 'bush':
                continue
            if (mx, my) not in game.explored:
                continue
            if not (cam_x <= mx < cam_x + config.VIEW_W
                    and cam_y <= my < cam_y + config.VIEW_H):
                continue
            light = 1.0 if (mx, my) in game.visible else 0.72
            key = ('berry_bush' if record.get('has', True)
                   else 'berry_bush_empty')
            self.screen.blit(Tiles.get(key, 0, light),
                             ((mx - cam_x) * TILE, (my - cam_y) * TILE))

    def _draw_loot(self, game, cam_x, cam_y):
        size = 48                     # чуть меньше тайла — не «кричит»
        off = (TILE - size) // 2
        for (ix, iy), key in game.dungeon.items.items():
            if (ix, iy) not in game.visible:
                continue
            px = (ix - cam_x) * TILE + off
            py = (iy - cam_y) * TILE + off
            self.icons.draw_item(key, px, py, size=size)

    def _draw_monsters(self, game, cam_x, cam_y):
        from core.config import ANIM_IDLE_SPEED, ANIM_IDLE_AMP, ANIM_ATTACK_AMP
        for m in game.dungeon.monsters:
            if m.hp <= 0 or (m.x, m.y) not in game.visible:
                continue
            px = (self._rx(m) - cam_x) * TILE
            py = (self._ry(m) - cam_y) * TILE

            bob = math.sin(m.anim_time * ANIM_IDLE_SPEED + m.x * 0.5) * ANIM_IDLE_AMP
            tx, ty = 0, 0
            if m.attack_anim > 0:
                f = math.sin(m.attack_anim * math.pi)
                tx = m.facing[0] * f * ANIM_ATTACK_AMP
                ty = m.facing[1] * f * ANIM_ATTACK_AMP

            draw_x = int(px + tx)
            draw_y = int(py + bob + ty)

            self.screen.blit(self._shadow_sm, (px, py))
            sprite = MONSTER_SPRITES.get(m.ch, f"monster_{m.ch}")
            img = Sprites.get(sprite, (TILE, TILE), finish=True)
            if img:
                self.screen.blit(img, (draw_x, draw_y))
            elif not self.icons.draw_monster(m.ch, draw_x, draw_y, size=TILE):
                cx, cy = draw_x + TILE // 2, draw_y + TILE // 2
                r = 16 if m.boss else 12
                pygame.draw.circle(self.screen, T.shade(m.color, 0.45), (cx, cy), r)
                pygame.draw.circle(self.screen, m.color, (cx, cy), r, 1)
                self.text(m.ch, cx, cy, (255, 255, 255),
                          font=T.FONT_SMALL, center=True)

            if m.hp < m.max_hp:
                bw = TILE - 8
                pygame.draw.rect(self.screen, T.HP_BG, (px + 4, py + 4, bw, 5))
                pygame.draw.rect(self.screen, T.HP,
                                 (px + 4, py + 4, int(bw * m.hp / m.max_hp), 5))
            if m.boss:
                T.rounded_rect(self.screen, T.STAIRS,
                               (px, py, TILE - 1, TILE - 1), width=2, radius=4)
            self._draw_statuses(m, px, py)

    def _draw_elder(self, game, cam_x, cam_y):
        if game.world_mode != 'overworld' \
                or getattr(game, 'location', 'village') != 'village':
            return
        if not (game.elder and (game.elder.x, game.elder.y) in game.visible):
            return
        px = (self._rx(game.elder) - cam_x) * TILE
        py = (self._ry(game.elder) - cam_y) * TILE
        bob = math.sin(game.elder.anim_time * 4.0) * 2
        self.screen.blit(self._shadow_sm, (px, py))
        img = Sprites.get('npc/elder', (TILE, TILE), finish=True)
        if img:
            self.screen.blit(img, (px, int(py + bob)))
        else:
            self.icons.draw_elder(px, int(py + bob), size=TILE)

    def _draw_merchant(self, game, cam_x, cam_y):
        if game.world_mode != 'dungeon':
            return
        if not (game.merchant and (game.merchant.x, game.merchant.y) in game.visible):
            return
        px = (self._rx(game.merchant) - cam_x) * TILE
        py = (self._ry(game.merchant) - cam_y) * TILE
        bob = math.sin(game.merchant.anim_time * 4.0) * 2
        self.screen.blit(self._shadow_sm, (px, py))
        img = Sprites.get('npc/merchant', (TILE, TILE), finish=True)
        if img:
            self.screen.blit(img, (px, int(py + bob)))
        else:
            self.icons.draw_merchant(px, int(py + bob), size=TILE)

    def _draw_player(self, game, cam_x, cam_y):
        px = (self._rx(game.player) - cam_x) * TILE
        py = (self._ry(game.player) - cam_y) * TILE

        from core.config import ANIM_IDLE_SPEED, ANIM_IDLE_AMP, ANIM_ATTACK_AMP
        bob = math.sin(game.player.anim_time * ANIM_IDLE_SPEED) * ANIM_IDLE_AMP
        thrust_x, thrust_y = 0, 0
        if game.player.attack_anim > 0:
            f = math.sin(game.player.attack_anim * math.pi)
            thrust_x = game.player.facing[0] * f * ANIM_ATTACK_AMP
            thrust_y = game.player.facing[1] * f * ANIM_ATTACK_AMP

        draw_x = int(px + thrust_x)
        draw_y = int(py + bob + thrust_y)

        self.screen.blit(self._shadow_lg, (px, py))

        # PNG по классу, если есть
        img = Sprites.get(f'player/{game.player.class_key}', (TILE, TILE), finish=True)
        if img:
            self.screen.blit(img, (draw_x, draw_y))
        else:
            self.icons.draw_player(game.player.class_key, draw_x, draw_y, size=TILE)

        self._draw_statuses(game.player, px, py)

    def _draw_remote_player(self, player, cam_x, cam_y):
        px = (self._rx(player) - cam_x) * TILE
        py = (self._ry(player) - cam_y) * TILE
        from core.config import ANIM_IDLE_SPEED, ANIM_IDLE_AMP
        bob = math.sin(player.anim_time * ANIM_IDLE_SPEED) * ANIM_IDLE_AMP
        thrust_x = thrust_y = 0
        if player.attack_anim > 0:
            f = math.sin(player.attack_anim * math.pi)
            thrust_x = player.facing[0] * f * 8
            thrust_y = player.facing[1] * f * 8
        draw_x = int(px + thrust_x)
        draw_y = int(py + bob + thrust_y)
        self.screen.blit(self._shadow_lg, (px, py))
        self.icons.draw_player(player.class_key, draw_x, draw_y, size=TILE)
        # Ник над игроком
        self.text(player.class_name, draw_x + TILE // 2, draw_y - 6,
                  (210, 200, 178), font=T.FONT_TINY, center=True)
        self._draw_statuses(player, px, py)

    def _draw_statuses(self, entity, px, py):
        if not entity.statuses:
            return
        x = px + 3
        y = py + TILE - 6
        for key in entity.statuses:
            info = STATUSES.get(key)
            if not info:
                continue
            pygame.draw.circle(self.screen, info['color'], (x, y), 3)
            pygame.draw.circle(self.screen, (0, 0, 0), (x, y), 3, 1)
            x += 8
            if x > px + TILE - 6:
                break

    def _draw_floats(self, game, cam_x, cam_y):
        for f in game.floats:
            alpha = min(255, int(f.life / FLOAT_TTL * 255))
            surf = T.FONT_SMALL.render(f.text, True, f.color)
            surf.set_alpha(alpha)
            sx = f.x - cam_x * TILE - surf.get_width() // 2
            sy = f.y - cam_y * TILE - surf.get_height() // 2
            self.screen.blit(surf, (sx, sy))

    def _draw_quest_board(self, game, cam_x, cam_y):
        board = getattr(game, 'quest_board', None)
        if not board:
            return
        if (game.world_mode == 'overworld'
                and getattr(game, 'location', 'village') != 'village'):
            return
        if (board.x, board.y) not in game.visible:
            return
        px = (self._rx(board) - cam_x) * TILE
        py = (self._ry(board) - cam_y) * TILE
        self.screen.blit(self._shadow_sm, (px, py))
        img = Sprites.get('npc/quest_board', (TILE, TILE), finish=True)
        if not img:
            from render.tiles import Tiles
            img = Tiles.get('quest_board')
        self.screen.blit(img, (px, py))
    # ════════════════════════ HUD ════════════════════════
    def draw_hud(self, game):
        y0 = config.SCREEN_H - config.HUD_HEIGHT
        pygame.draw.rect(self.screen, T.BG_HUD, (0, y0, config.SCREEN_W, config.SCREEN_H - y0))
        pygame.draw.line(self.screen, T.PANEL_BORDER_DARK, (0, y0), (config.SCREEN_W, y0), 1)

        panel_y = y0 + 4
        panel_h = config.SCREEN_H - y0 - 8  # до низа экрана
        T.panel(self.screen, (8, panel_y, 400, panel_h))
        self._draw_stats_column(game, 20, panel_y + 10)
        log_x = 416
        log_rect = (log_x, panel_y, config.SCREEN_W - log_x - 8, panel_h)
        T.panel(self.screen, log_rect)
        self._draw_log_panel(game, log_x + 12, panel_y + 6,
                             log_rect[2] - 24, panel_h - 12)

    def _draw_stats_column(self, game, x, y):
        p = game.player
        self.text("HP", x, y + 4, T.TEXT, font=T.FONT_SMALL)
        bar_rect = (x + 30, y, 180, 22)
        ratio = p.hp / max(1, p.max_hp)
        T.bar(self.screen, bar_rect, ratio, T.HP, pulse=(ratio < 0.3))
        surf = T.render(f"{p.hp}/{p.max_hp}", (255, 255, 255), T.FONT_SMALL)
        self.screen.blit(surf, (bar_rect[0] + (bar_rect[2] - surf.get_width()) // 2,
                                bar_rect[1] + 4))
        self.text(f"${p.gold}", x + 230, y + 4, T.GOLD)
        self.text(f"Ур.{p.level}", x, y + 32, T.TEXT, font=T.FONT_BODY)
        xp_rect = (x + 70, y + 36, 160, 10)
        T.bar(self.screen, xp_rect, p.xp / max(1, p.xp_next), T.XP,
              bg_color=(20, 30, 50))
        self.text(f"{p.xp}/{p.xp_next}", x + 240, y + 32, T.XP, font=T.FONT_SMALL)
        self.text(f"ATK {p.atk}", x, y + 62, T.TEXT, font=T.FONT_BODY)
        self.text(f"DEF {p.df}", x + 100, y + 62, T.TEXT, font=T.FONT_BODY)
        if game.world_mode == 'overworld':
            fn = getattr(game, 'location_title', None)
            loc = fn() if fn else ov.location_title(game.player.x,
                                                    game.player.y)
        else:
            loc = f"Подземелье {game.depth}/{MAX_DEPTH}"
        self.text(loc, x + 200, y + 62, T.TEXT_DIM, font=T.FONT_SMALL)
        if p.free_points > 0:
            T.badge(self.screen, x, y + 94, f"● {p.free_points} очков (C)",
                    color=T.GOLD, bg=(50, 40, 15), font=T.FONT_SMALL)
        self.text(f"[F5] слот {game.current_slot}",
                  x + 380, y + 4, T.TEXT_MUTED, font=T.FONT_TINY, align_right=True)

    def _draw_log_panel(self, game, x, y, w, h):
        self.text("ЛОГ", x, y, T.TEXT_MUTED, font=T.FONT_TINY)
        if game.log_scroll > 0:
            surf = T.render(f"▲ {game.log_scroll}", T.GOLD, T.FONT_TINY)
            self.screen.blit(surf, (x + w - surf.get_width(), y))
        T.divider(self.screen, x, y + 18, x + w)
        line_h = 20
        visible_count = max(3, (h - 30) // line_h)
        total = len(game.messages)
        end = max(0, total - game.log_scroll)      # ← защита от перескролла
        start = max(0, end - visible_count)
        for i, msg in enumerate(game.messages[start:end]):
            text = self.fit_text(msg.text, w, font=T.FONT_SMALL)
            self.text(text, x, y + 24 + i * line_h, msg.color, font=T.FONT_SMALL)

    def draw_toolbar(self, game):
        labels = {
            'inventory': 'Инв',
            'craft': 'Крафт',
            'stats': 'Статы',
            'quests': 'Квесты',
        }
        mx, my = game.mouse_pos
        for name, rect in game._toolbar_rects():
            x, y, w, h = rect
            hover = x <= mx <= x + w and y <= my <= y + h

            bg = (66, 54, 38) if hover else (30, 27, 22)
            border = (206, 166, 82) if hover else (78, 66, 48)

            # Тень — одна поверхность на размер кнопки (кэш)
            shadow = self._toolbar_shadow.get((w, h))
            if shadow is None:
                shadow = pygame.Surface((w + 10, h + 10), pygame.SRCALPHA)
                pygame.draw.rect(shadow, (0, 0, 0, 110), (3, 4, w, h),
                                 border_radius=10)
                self._toolbar_shadow[(w, h)] = shadow
            self.screen.blit(shadow, (x - 3, y - 3))

            T.rounded_rect(self.screen, bg, (x, y, w, h),
                           radius=10, border_color=border)
            self.icons.draw_toolbar_icon(name, x + 6, y + 6, size=w - 12, hover=hover)
            self.text(labels[name], x + w // 2, y + h + 6, T.TEXT_DIM,
                      font=T.FONT_TINY, center=True)

    # ════════════════════════ МИНИ-КАРТА ════════════════════════
    def _minimap_scaled(self, game):
        """Основа миникарты (стены/пол/лестницы) — кэш по explored."""
        key = (id(game), game.world_mode, game.depth,
               getattr(game, 'location', 'village'),
               len(game.explored),
               getattr(game, 'render_revision', 0))
        if self._mini_key == key and self._mini_scaled is not None:
            return self._mini_scaled
        d = game.dungeon
        mini = pygame.Surface((d.w, d.h), pygame.SRCALPHA)
        mini.fill((12, 11, 9, 210))
        for my in range(d.h):
            row = game.dungeon.tiles[my]
            for mx in range(d.w):
                if (mx, my) not in game.explored:
                    continue
                tile = row[mx]
                if tile == Tile.WALL:
                    color = (82, 74, 62, 210)
                elif tile == Tile.STAIRS:
                    color = (206, 166, 82, 255)
                elif tile == Tile.EXIT:
                    color = (150, 190, 130, 255)
                elif tile == Tile.UPSTAIRS:
                    color = (110, 160, 110, 255)
                elif tile == Tile.GATE:
                    color = (206, 166, 82, 255)
                else:
                    color = (56, 52, 45, 210)
                mini.set_at((mx, my), color)
        self._mini_size = (d.w, d.h)
        self._mini_scaled = pygame.transform.scale(
            mini, (d.w * MINIMAP_SCALE, d.h * MINIMAP_SCALE))
        self._mini_key = key
        return self._mini_scaled

    def _minimap_marker(self, pos_x, pos_y, entity, color, size=2):
        mx = int(self._rx(entity)) * MINIMAP_SCALE
        my = int(self._ry(entity)) * MINIMAP_SCALE
        width, height = getattr(self, '_mini_size', (MAP_W, MAP_H))
        if not (0 <= mx < width * MINIMAP_SCALE
                and 0 <= my < height * MINIMAP_SCALE):
            return
        x = pos_x + mx - size // 2
        y = pos_y + my - size // 2
        pygame.draw.rect(self.screen, (0, 0, 0),
                         (x - 1, y - 1, size + 2, size + 2))
        pygame.draw.rect(self.screen, color, (x, y, size, size))

    def draw_minimap(self, game):
        if not game.settings.get('show_minimap', True):
            return
        scaled = self._minimap_scaled(game)
        scaled_w, scaled_h = scaled.get_size()
        pos_x = config.SCREEN_W - scaled_w - MINIMAP_MARGIN
        pos_y = MINIMAP_MARGIN
        pygame.draw.rect(self.screen, (52, 45, 34),
                         (pos_x - 2, pos_y - 2, scaled_w + 4, scaled_h + 4))
        self.screen.blit(scaled, (pos_x, pos_y))
        pygame.draw.rect(self.screen, (100, 86, 62),
                         (pos_x - 2, pos_y - 2, scaled_w + 4, scaled_h + 4), 1)

        if game.merchant:
            self._minimap_marker(pos_x, pos_y, game.merchant,
                                 (206, 170, 96), size=2)
        # Сокомандники видны на мини-карте всегда — это «радар» отряда
        my_id = getattr(game, 'my_id', None)
        my_player = getattr(game, 'player', None)
        for pid, remote in getattr(game, 'remote_players', {}).items():
            if pid == my_id or remote is my_player:
                continue
            color = CLASSES.get(getattr(remote, 'class_key', 'default'),
                                {}).get('color', (176, 170, 150))
            self._minimap_marker(pos_x, pos_y, remote, color, size=3)
        self._minimap_marker(pos_x, pos_y, game.player, (255, 255, 255), size=3)
