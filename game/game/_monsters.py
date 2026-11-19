"""Поведение мобов: monsters_turn/update, _update_city_guard, try_move.
"""
from game.game._common import *  # noqa: F401,F403 — все импорты


class MonsterMixin:
    """Поведение мобов: monsters_turn/update, _update_city_guard, try_move."""

    # Радиус, в котором стражник замечает монстров вокруг своего поста.
    GUARD_RADIUS = 5
    # Один шаг/удар стражи за этот интервал (сек).
    GUARD_STEP_INTERVAL = 0.35

    def _guard_can_enter(self, guard, x, y):
        """Может ли стражник встать на клетку (без стен, NPC и мобов)."""
        if not (0 <= x < self.dungeon.w and 0 <= y < self.dungeon.h):
            return False
        if self.dungeon.tiles[y][x] in (Tile.WALL, Tile.LOCKED_DOOR,
                                        Tile.CHEST):
            return False
        if (x, y) in self.dungeon.harvestables:
            return False
        if (x, y) == (self.player.x, self.player.y):
            return False
        if self.npc_blocking_at(x, y) is not None:
            return False
        for m in self.dungeon.monsters:
            if m.hp > 0 and (m.x, m.y) == (x, y):
                return False
        return True

    def _guard_step_toward(self, guard, tx, ty):
        """Один жадный шаг к цели. Возвращает True, если шаг сделан."""
        dx = (tx > guard.x) - (tx < guard.x)
        dy = (ty > guard.y) - (ty < guard.y)
        if dx == 0 and dy == 0:
            return False
        # Сначала сокращаем большую разницу, затем меньшую — так стражник
        # не «застревает» о выступы стен.
        if abs(tx - guard.x) >= abs(ty - guard.y):
            options = ((dx, dy), (dx, 0), (0, dy))
        else:
            options = ((dx, dy), (0, dy), (dx, 0))
        for sx, sy in options:
            if sx == 0 and sy == 0:
                continue
            nx, ny = guard.x + sx, guard.y + sy
            if self._guard_can_enter(guard, nx, ny):
                guard.x, guard.y = nx, ny
                return True
        return False

    def _update_city_guard(self, dt):
        """Стражники у всех ворот: подходят к мобам и бьют вплотную.

        Цель ищется в радиусе ``GUARD_RADIUS`` от клетки спавна. Урон
        наносится только на соседней клетке; после убийства стражник
        возвращается на свой пост. Опыт и лут игроку не начисляются.
        """
        if (self.world_mode != 'overworld'
                or getattr(self, 'location', 'village') != 'village'):
            return
        guards = [n for n in getattr(self, 'city_npcs', ())
                  if n.npc_type == 'guard' and n.hp > 0]
        if not guards:
            return
        self._guard_timer = max(0.0, getattr(self, '_guard_timer', 0.0) - dt)
        if self._guard_timer > 0:
            return
        self._guard_timer = self.GUARD_STEP_INTERVAL

        for guard in guards:
            hx = getattr(guard, 'home_x', guard.x)
            hy = getattr(guard, 'home_y', guard.y)
            # После убийства стражник сначала возвращается на пост, даже
            # если рядом есть другие мобы. Дома снова готов к бою.
            if getattr(guard, '_returning_home', False):
                if (guard.x, guard.y) == (hx, hy):
                    guard._returning_home = False
                else:
                    self._guard_step_toward(guard, hx, hy)
                    continue
            # Ближайший моб именно к посту, а не к текущей позиции:
            # стражник не уходит погоней за пределы своего радиуса.
            target, best = None, self.GUARD_RADIUS + 1
            for m in self.dungeon.monsters:
                if m.hp <= 0:
                    continue
                home_dist = max(abs(m.x - hx), abs(m.y - hy))
                if home_dist <= self.GUARD_RADIUS and home_dist < best:
                    target, best = m, home_dist
            if target is None:
                # Пост опустел — возвращаемся на исходную клетку.
                if (guard.x, guard.y) != (hx, hy):
                    self._guard_step_toward(guard, hx, hy)
                continue

            dist = max(abs(target.x - guard.x), abs(target.y - guard.y))
            if dist <= 1:
                guard.attack_anim = 1.0
                rules.face_towards(guard, target)
                damage = max(1, guard.atk)
                target.hp -= damage
                self.add_float(target.x, target.y, f"-{damage}", COL_DMG)
                if target.hp <= 0:
                    if target in self.dungeon.monsters:
                        self.dungeon.monsters.remove(target)
                    self.log(f"Стражник сразил {target.name}.", COL_DIM)
                    self.render_revision = getattr(self, 'render_revision', 0) + 1
                    # Следующий тик начнёт с возвращения на пост.
                    guard._returning_home = True
            else:
                self._guard_step_toward(guard, target.x, target.y)


    def monsters_turn(self):
        """Все мобы действуют (используется тестами и как «полный ход»)."""
        for m in list(self.dungeon.monsters):
            if self.dead:
                return
            self._monster_act(m)


    def monsters_update(self, dt):
        """Real-time ходы: у каждого моба свой таймер, без общего залпа."""
        for m in list(self.dungeon.monsters):
            if self.dead:
                return
            if m.hp <= 0:
                continue
            # Дыхание босса — отдельный таймер, вне обычного хода
            if m.boss:
                distance = max(abs(self.player.x - m.x),
                               abs(self.player.y - m.y))
                damage = rules.boss_breath_tick(m, self.player, distance, dt)
                if damage:
                    self._boss_breath_fx(m, damage)
            m.act_timer -= dt
            if m.act_timer > 0:
                continue
            m.act_timer = rules.action_interval(m)
            self._monster_act(m)


    def try_move(self, m, x, y):
        from core.config import Tile
        if not (0 <= x < self.dungeon.w and 0 <= y < self.dungeon.h):
            return False
        if self.dungeon.tiles[y][x] in (Tile.WALL, Tile.LOCKED_DOOR,
                                        Tile.CHEST):
            return False
        if (x, y) in self.dungeon.harvestables:
            return False
        if x == self.player.x and y == self.player.y:
            return False
        if self.npc_blocking_at(x, y):
            return False
        # Другие мобы
        for o in self.dungeon.monsters:
            if o is not m and o.hp > 0 and o.x == x and o.y == y:
                return False
        m.x, m.y = x, y
        return True

    # ════════════════════════ МАРШРУТИЗАЦИЯ INPUT ════════════════════════

