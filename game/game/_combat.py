"""Бой и движение: move_player, attack, _single_attack, _kill_monster, projectiles, статусы.
"""
from game.game._common import *  # noqa: F401,F403 — все импорты


class CombatMixin:
    """Бой и движение: move_player, attack, _single_attack, _kill_monster, projectiles, статусы."""

    def move_player(self, dx, dy):
        nx, ny = self.player.x + dx, self.player.y + dy
        if not (0 <= nx < self.dungeon.w and 0 <= ny < self.dungeon.h):
            return False
        if self.npc_blocking_at(nx, ny):
            return False
        target = next((m for m in self.dungeon.monsters
                       if m.x == nx and m.y == ny and m.hp > 0), None)
        if target:
            self.attack(self.player, target)
            return True
        # Ресурсы добываются только клавишей «E» — идти в клетку ресурса
        # нельзя. Деревья/кусты/руда стоят на месте.
        if (nx, ny) in self.dungeon.harvestables:
            return False
        tile = self.dungeon.tiles[ny][nx]
        if tile in (Tile.WALL, Tile.LOCKED_DOOR, Tile.CHEST):
            if tile == Tile.LOCKED_DOOR:
                # Запертая дверь босс-комплекса: отпирается ключом босса.
                return self._try_unlock_door(nx, ny)
            return False
        self.player.x, self.player.y = nx, ny
        if (nx, ny) in self.dungeon.items:
            self.pickup(nx, ny)
        trans = self.dungeon.transitions.get((nx, ny))
        if trans:
            self.travel_to(trans['to'], trans.get('entry'))
            return False
        if tile == Tile.STAIRS:
            if self.world_mode == 'overworld':
                self.enter_dungeon()
            else:
                from audio import Audio
                Audio.play('stairs')
                self.new_dungeon(self.depth + 1)
            # Игрок появится на входной лестнице: без кулдауна удержание
            # направления сразу же отправляет его обратно.
            self.player_move_timer = PLAYER_MOVE_COOLDOWN
            return False
        elif tile == Tile.EXIT:
            self.exit_to_overworld()
            self.player_move_timer = PLAYER_MOVE_COOLDOWN
            return False
        elif tile == Tile.UPSTAIRS:
            self.ascend()
            self.player_move_timer = PLAYER_MOVE_COOLDOWN
            return False
        return True


    def player_attack(self):
        """Атака по пробелу: ближайший моб, иначе — добываемый ресурс.

        Если оружие дальнобойное (bow/staff/book) — стреляем в ближайшего
        видимого моба в радиусе 5. Иначе — melee как раньше.
        """
        from core.data import get_item_data
        weapon_key = self.player.weapon
        weapon = get_item_data(weapon_key) if weapon_key else {}
        wtype = weapon.get('wtype', '')
        # Дальнобойное оружие: bow, staff, book
        if wtype in ('bow', 'staff', 'book'):
            self._ranged_player_attack(weapon, wtype)
            return
        # Melee: ближайший моб в радиусе 1
        target = None
        best_dist = 999
        for m in self.dungeon.monsters:
            if m.hp <= 0:
                continue
            dist = max(abs(m.x - self.player.x), abs(m.y - self.player.y))
            if dist <= 1 and dist < best_dist:
                target = m
                best_dist = dist
        if target:
            self.attack(self.player, target)
            return
        # Ресурсы добываются только клавишей «E» (harvest_adjacent):
        # пробел бьёт только по монстрам.


    def _ranged_player_attack(self, weapon, wtype):
        """Дальнобойная атака игрока: создаёт Projectile в self.projectiles.

        Урон applied при попадании (в update_projectiles).
        Тип снаряда зависит от оружия:
        - bow → arrow
        - staff → fireball (магия стихий)
        - book → frost (магическая книга)
        """
        from core import rules
        from core.config import PLAYER_RANGED_COOLDOWN
        # Найти ближайшего видимого моба в радиусе 5
        target = None
        best_dist = 999
        for m in self.dungeon.monsters:
            if m.hp <= 0:
                continue
            dist = max(abs(m.x - self.player.x), abs(m.y - self.player.y))
            if dist <= 5 and dist < best_dist:
                target = m
                best_dist = dist
        if not target:
            self.log("Нет цели для дальнобойной атаки.", COL_DIM)
            return
        kind_map = {'bow': 'arrow', 'staff': 'fireball', 'book': 'frost'}
        kind = kind_map.get(wtype, 'arrow')
        # Создать Projectile через rules.ranged_attack
        proj = rules.ranged_attack(self.player, target, kind=kind)
        # self.projectiles — list of dict (upstream format). Конвертируем.
        # upstream projectile dict: {'x','y','tx','ty','kind','color','speed',...}
        self.projectiles.append({
            'x': proj.x, 'y': proj.y,
            'tx': proj.tx, 'ty': proj.ty,
            'kind': proj.kind, 'color': proj.color,
            'speed': proj.speed, 'damage': proj.damage,
            'target_ch': proj.target_ch, 'target': target,
            'life': proj.life,
        })
        self.player.attack_anim = 1.0
        from audio import Audio
        Audio.play('attack') if hasattr(Audio, 'play') else None
        self.player_attack_timer = PLAYER_RANGED_COOLDOWN


    def attack(self, a, b):
        self._single_attack(a, b)
        if (isinstance(a, Player) and a.double_strike
                and b.hp > 0 and not self.dead
                and random.random() < 0.15):
            self.log("Двойной удар!", COL_CRIT)
            self._single_attack(a, b)


    def _single_attack(self, a, b):
        """Один удар. Правила — в core.rules, здесь только звук/лог/смерть."""
        from audio import Audio
        from core.data import STATUSES

        result = rules.resolve_attack(a, b)
        if result.dodged:
            Audio.play('miss')
            self.log(f"{b.name} уклоняется от {a.name}!", COL_DODGE)
            self.add_float(b.x, b.y, "MISS", COL_DODGE)
            return

        Audio.play('crit' if result.crit else 'hit')
        color = COL_CRIT if result.crit else COL_DMG
        prefix = "КРИТ! " if result.crit else ""
        self.log(f"{prefix}{a.name} → {b.name}: {result.damage}", color)
        self.add_float(b.x, b.y, str(result.damage), color)

        if result.status:
            info = STATUSES[result.status]
            self.log(f"{b.name}: {info['name']}!", info['color'])
        if result.vampire_heal:
            self.add_float(a.x, a.y, f"+{result.vampire_heal}", COL_HEAL)

        if result.defender_dead:
            if b is self.player:
                Audio.play('death_player')
                self.dead = True
                self.death_time = time.time()
                self.log("Вы погибли. R — рестарт, F9 — загрузка.", COL_DMG)
            else:
                self._kill_monster(b)
            return

        if result.thorns:
            self.add_float(a.x, a.y, "ШИПЫ", (200, 200, 80))
            if result.attacker_dead:
                self._kill_monster(a)


    def _kill_monster(self, monster):
        from audio import Audio
        self.kills += 1
        if monster.boss:
            self.boss_kills += 1
        Audio.play('death_monster')
        self.log(f"{monster.name} погиб (+{monster.xp_value} XP)", COL_XP)
        self.total_kills_by_ch[monster.ch] = (
                self.total_kills_by_ch.get(monster.ch, 0) + 1)
        self.quest_log.update_kill(monster.ch)
        self.add_float(monster.x, monster.y, f"+{monster.xp_value} XP", COL_XP)

        reward = rules.roll_kill_reward(monster, self.depth, self.player.greedy)
        gold_pct = float(getattr(self.player, 'gold_pct', 0.0) or 0.0)
        reward.gold = int(round(reward.gold * (1.0 + gold_pct)))
        self.player.gold += reward.gold
        self.total_gold_earned += reward.gold
        self.log(f"+{reward.gold} золота", COL_GOLD)
        self.add_float(monster.x, monster.y - 0.6, f"+{reward.gold}g", COL_GOLD)
        for item_key in reward.drops:
            # Добыча сразу попадает в инвентарь — не нужно бегать по тайлам.
            # add_item учитывает стаки и не-стакаемое снаряжение.
            rules.add_item(self.player.inventory, item_key, 1)
            self.quest_log.update_collect(item_key, self.player.inventory)
            self.log(f"Добыча: {item_display_name(item_key)}", COL_GOLD)
            self.add_float(monster.x, monster.y - 0.4,
                           f"+{item_display_name(item_key)}", COL_HEAL)

        if monster.boss:
            # Погиб босс: замурованные выходы его арены открываются.
            from core.world import clear_boss_barriers
            if clear_boss_barriers(self.dungeon):
                self.render_revision = getattr(self, 'render_revision', 0) + 1
                self.log("Выходы арены открыты.", COL_WIN)

        if monster.boss and self.depth == MAX_DEPTH:
            self.victory = True
            self.log("ДРЕВНИЙ ДРАКОН ПОБЕЖДЁН! ВЫ ПОБЕДИЛИ!", COL_WIN)

        self.dungeon.monsters = [m for m in self.dungeon.monsters if m.hp > 0]
        for level in rules.give_xp(self.player, monster.xp_value):
            self._on_level_up(level)


    def _place_drop(self, x, y, item_key):
        rules.place_loot(self.dungeon, self.player.inventory, x, y, item_key,
                         avoid={(self.player.x, self.player.y)})


    def _player_death(self):
        from audio import Audio
        Audio.play('death_player')
        self.dead = True
        self.death_time = time.time()
        self.log("Вы погибли. R — рестарт, F9 — загрузка.", COL_DMG)


    def _monster_act(self, m):
        if m.hp <= 0:
            return
        if m.has_status('slow'):
            return  # замедлен — пропускает ход

        ability = getattr(m, 'ability', None)
        dx = self.player.x - m.x
        dy = self.player.y - m.y
        dist = max(abs(dx), abs(dy))

        # Мирные животные (олень): никогда не нападают. После первого
        # полученного удара убегают от игрока.
        profile = MONSTERS_BY_CH.get(m.ch, {})
        if profile.get('passive'):
            if m.hp < m.max_hp or dist <= 3:
                self._flee_monster(m)
            return

        if ability == 'healer':
            if self._healer_action(m):
                return
        if ability == 'ranged' and 2 <= dist <= 4:
            if has_los(self.dungeon, m.x, m.y, self.player.x, self.player.y):
                self._launch_projectile(m, self.player)
                return
        if dist <= 1:
            self.attack(m, self.player)
        elif dist <= 8 and has_los(self.dungeon, m.x, m.y,
                                   self.player.x, self.player.y):
            mx = (dx > 0) - (dx < 0)
            my = (dy > 0) - (dy < 0)
            if abs(dx) > abs(dy):
                if not self.try_move(m, m.x + mx, m.y):
                    self.try_move(m, m.x, m.y + my)
            else:
                if not self.try_move(m, m.x, m.y + my):
                    self.try_move(m, m.x + mx, m.y)
        elif m.boss and dist <= 15:
            blocked = {(o.x, o.y) for o in self.dungeon.monsters
                       if o is not m and o.hp > 0}
            nxt = bfs_toward_player(self.dungeon, (m.x, m.y),
                                    (self.player.x, self.player.y), blocked)
            if nxt:
                m.x, m.y = nxt

    # ── Снаряды дальних атак ──
    PROJECTILE_STYLES = {
        'a': ('arrow', (210, 190, 120)),      # гоблин-лучник
        'L': ('orb', (200, 120, 255)),        # лич
        'S': ('orb', (180, 140, 220)),        # шаман
        'W': ('spirit', (190, 200, 255)),     # призрак
    }


    def _launch_projectile(self, source, target, kind=None, color=None,
                           speed=9.0):
        """Запускает видимый снаряд; урон применяется при попадании."""
        style, default_color = self.PROJECTILE_STYLES.get(
            getattr(source, 'ch', ''), ('orb', (200, 160, 255)))
        self.projectiles.append({
            'x': float(source.x), 'y': float(source.y),
            'kind': kind or style,
            'color': color or default_color,
            'speed': speed,
            'source': source, 'target': target,
        })


    def update_projectiles(self, dt):
        if not self.projectiles:
            return
        remaining = []
        for pr in self.projectiles:
            target = pr.get('target')
            if target is None or getattr(target, 'hp', 0) <= 0:
                continue
            dx = target.x - pr['x']
            dy = target.y - pr['y']
            dist = (dx * dx + dy * dy) ** 0.5
            step = max(0.01, pr['speed'] * dt)
            if dist <= step:
                source = pr.get('source')
                if source is not None and getattr(source, 'hp', 0) > 0:
                    self.attack(source, target)
                continue
            pr['x'] += dx / dist * step
            pr['y'] += dy / dist * step
            remaining.append(pr)
        self.projectiles = remaining


    def _flee_monster(self, m):
        """Шаг в сторону, увеличивающую расстояние до игрока."""
        options = []
        # Только ортогональные шаги: персонаж не ходит по диагонали и не
        # должен видеть, как монстры «срезают» между стенами.
        for fx, fy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = m.x + fx, m.y + fy
            if not (0 <= nx < self.dungeon.w and 0 <= ny < self.dungeon.h):
                continue
            if self.dungeon.tiles[ny][nx] in (Tile.WALL, Tile.LOCKED_DOOR,
                                              Tile.CHEST):
                continue
            if any(o is not m and o.hp > 0 and o.x == nx and o.y == ny
                   for o in self.dungeon.monsters):
                continue
            score = (nx - self.player.x) ** 2 + (ny - self.player.y) ** 2
            options.append((score, nx, ny))
        if not options:
            return False
        best = max(s for s, _, _ in options)
        candidates = [(x, y) for s, x, y in options if s == best]
        nx, ny = random.choice(candidates)
        return self.try_move(m, nx, ny)


    def _tick_player_statuses(self):
        """Игрок получает урон/лечение от статусов в начале хода монстров."""
        for event in rules.tick_statuses(self.player):
            if event.damage:
                self.log(f"{event.name}: -{event.damage} HP", event.color)
                self.add_float(self.player.x, self.player.y,
                               f"-{event.damage}", event.color)
            if event.heal:
                self.log(f"{event.name}: +{event.heal} HP", event.color)
                self.add_float(self.player.x, self.player.y,
                               f"+{event.heal}", event.color)
        if self.player.hp <= 0 and not self.dead:
            self.dead = True
            self.log("Вы погибли от статус-эффекта. R — рестарт.", COL_DMG)


    def _tick_monster_statuses(self, m):
        """Монстр получает урон/лечение от статусов."""
        for event in rules.tick_statuses(m):
            if event.damage:
                self.log(f"{m.name}: {event.name} -{event.damage} HP",
                         event.color)
                self.add_float(m.x, m.y, f"-{event.damage}", event.color)
            if event.heal:
                self.add_float(m.x, m.y, f"+{event.heal}", event.color)
        if m.hp <= 0 and m in self.dungeon.monsters:
            self._kill_monster(m)


    def _tick_statuses(self):
        """Тик всех статусов — по таймеру."""
        self._tick_player_statuses()
        if self.dead:
            return
        for m in list(self.dungeon.monsters):
            if m.hp > 0:
                self._tick_monster_statuses(m)


    def _healer_action(self, healer):
        """Шаман лечит раненого союзника. Возвращает True, если походил."""
        target = None
        best_ratio = 1.0
        for m in self.dungeon.monsters:
            if m is healer or m.hp <= 0 or m.hp >= m.max_hp:
                continue
            dist = max(abs(m.x - healer.x), abs(m.y - healer.y))
            if dist > 5:
                continue
            ratio = m.hp / m.max_hp
            if ratio < best_ratio and has_los(self.dungeon, healer.x, healer.y,
                                              m.x, m.y):
                target = m
                best_ratio = ratio
        if not target:
            return False
        heal = min(8, target.max_hp - target.hp)
        target.hp += heal
        self.log(f"{healer.name} лечит {target.name}: +{heal}",
                 (120, 240, 120))
        self.add_float(target.x, target.y, f"+{heal}", (120, 240, 120))
        return True


    def _boss_breath_fx(self, boss, damage):
        from audio import Audio
        Audio.play('fireball')
        self.log(f"{boss.name}: дыхание! {damage} урона", COL_DMG)
        self.add_float(self.player.x, self.player.y, f"-{damage}", COL_DMG)
        if self.player.hp <= 0 and not self.dead:
            self._player_death()


