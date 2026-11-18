"""Поведение мобов: monsters_turn/update, _update_city_guard, try_move.
"""
from game.game._common import *  # noqa: F401,F403 — все импорты


class MonsterMixin:
    """Поведение мобов: monsters_turn/update, _update_city_guard, try_move."""

    def _update_city_guard(self, dt):
        """Стражник у ворот бьёт мобов в радиусе 5 клеток. Без XP/лута."""
        if (self.world_mode != 'overworld'
                or getattr(self, 'location', 'village') != 'village'):
            return
        guard = next((n for n in getattr(self, 'city_npcs', ())
                      if n.npc_type == 'guard'), None)
        if guard is None:
            return
        self._guard_timer = max(0.0, getattr(self, '_guard_timer', 0.0) - dt)
        if self._guard_timer > 0:
            return
        target, best = None, 99
        for m in self.dungeon.monsters:
            if m.hp <= 0:
                continue
            dist = max(abs(m.x - guard.x), abs(m.y - guard.y))
            if dist <= 5 and dist < best:
                target, best = m, dist
        if target is None:
            return
        self._guard_timer = 0.8
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

