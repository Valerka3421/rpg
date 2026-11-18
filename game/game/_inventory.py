"""Инвентарь и предметы: use_item, harvest_*, pickup, unequip_*, cast_active_skill, allocate_stat.
"""
from game.game._common import *  # noqa: F401,F403 — все импорты


class InventoryMixin:
    """Инвентарь и предметы: use_item, harvest_*, pickup, unequip_*, cast_active_skill, allocate_stat."""

    def map_tile_under(self, pos):
        """Клетка карты под курсором или None (курсор вне игрового поля)."""
        from core.config import MAP_H, MAP_W, TILE
        ox, oy = config.view_origin()
        mx, my = pos
        if not (ox <= mx < ox + config.VIEW_W * TILE
                and oy <= my < oy + config.VIEW_H * TILE):
            return None
        w_cells = getattr(self.dungeon, 'w', MAP_W)
        h_cells = getattr(self.dungeon, 'h', MAP_H)
        cam_x = max(0, min(self.player.x - config.VIEW_W // 2,
                           max(0, w_cells - config.VIEW_W)))
        cam_y = max(0, min(self.player.y - config.VIEW_H // 2,
                           max(0, h_cells - config.VIEW_H)))
        return ((mx - ox) // TILE + cam_x, (my - oy) // TILE + cam_y)


    def harvest_adjacent(self):
        """Добыча ресурса или открытие сундука («E»).

        Приоритет — клетка под курсором, если она в пределах шага; иначе
        выбирается ближайший смежный ресурс (как раньше).
        """
        px, py = self.player.x, self.player.y
        hovered = self.map_tile_under(getattr(self, 'mouse_pos', (-1, -1)))
        if hovered is not None and max(abs(hovered[0] - px),
                                       abs(hovered[1] - py)) > 1:
            hovered = None

        def is_chest(x, y):
            return (0 <= x < self.dungeon.w and 0 <= y < self.dungeon.h
                    and self.dungeon.tiles[y][x] == Tile.CHEST)

        if hovered and is_chest(*hovered):
            self._open_chest(*hovered)
            return True
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            tx, ty = px + dx, py + dy
            if is_chest(tx, ty):
                self._open_chest(tx, ty)
                return True
        if self.world_mode != 'overworld':
            return False
        # Ресурс под курсором — даже если рядом есть другие.
        if hovered and self.harvestable_kind_at(*hovered) is not None:
            return self.harvest_at(*hovered)
        best = None
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                if dx == 0 and dy == 0:
                    continue
                pos = (px + dx, py + dy)
                if self.harvestable_kind_at(*pos) is None:
                    continue
                dist = max(abs(dx), abs(dy))
                if best is None or dist < best[0]:
                    best = (dist, pos)
        if best:
            return self.harvest_at(*best[1])
        return False


    def _open_chest(self, x, y):
        """Сундук: заранее заданные ресурсы или золото + предметы."""
        from audio import Audio
        Audio.play('pickup')
        loot = getattr(self.dungeon, 'chest_loot', {}).pop((x, y), None)
        if loot:
            names = []
            for key, count in loot.items():
                self.player.inventory[key] = (
                    self.player.inventory.get(key, 0) + count)
                self.quest_log.update_collect(key, self.player.inventory)
                names.append(f"{item_display_name(key)} ×{count}")
            self.log("Сундук открыт: " + ", ".join(names), COL_GOLD)
            self.add_float(x, y, "СУНДУК", COL_GOLD)
            self.dungeon.tiles[y][x] = Tile.FLOOR
            self.render_revision = getattr(self, 'render_revision', 0) + 1
            return
        depth = max(1, self.depth)
        gold = 20 + depth * 8
        self.player.gold += gold
        self.total_gold_earned += gold
        self.log(f"Сундук открыт: +{gold} золота", COL_GOLD)
        self.add_float(x, y, f"+{gold}g", COL_GOLD)
        tier = min(3, max(1, (depth + 1) // 3))
        pool = [k for k, v in ITEMS.items()
                if v.get('tier', 1) <= tier
                and v.get('type') not in ('book', 'resource', 'key')]
        for _ in range(random.randint(2, 3)):
            self._place_drop(x, y, random.choice(pool))
        self.dungeon.tiles[y][x] = Tile.FLOOR
        self.render_revision = getattr(self, 'render_revision', 0) + 1


    def _try_unlock_door(self, x, y):
        """Запертая дверь: тратит ключ босса и открывается."""
        if self.player.inventory.get('boss_key', 0) > 0:
            rules.consume(self.player.inventory, 'boss_key')
            self.dungeon.tiles[y][x] = Tile.FLOOR
            self.render_revision = getattr(self, 'render_revision', 0) + 1
            self.log("Ключ босса отпирает дверь.", COL_GOLD)
            return True
        self.log("Дверь заперта: нужен ключ босса.", COL_DIM)
        return False


    def harvestable_kind_at(self, x, y):
        """'bush'/'tree'/'ore' для клетки или None."""
        record = self.dungeon.harvestables.get((x, y))
        if record is not None:
            return record.get('kind')
        if not (0 <= x < self.dungeon.w and 0 <= y < self.dungeon.h):
            return None
        if self.dungeon.tiles[y][x] == Tile.WALL \
                and locations.is_tree_wall(self.location, x, y):
            return 'tree'
        return None


    def harvest_at(self, x, y):
        """Удар по добываемому объекту (дерево/руда/куст)."""
        from core import harvest as harvest_mod
        record = self.dungeon.harvestables.get((x, y))
        if record is None:
            # Обычное дерево становится ресурсом при первом ударе.
            if self.harvestable_kind_at(x, y) == 'tree':
                record = harvest_mod.make_harvestable('tree')
                self.dungeon.harvestables[(x, y)] = record
            else:
                return False
        if not record.get('has', True):
            self.log(f"{harvest_mod.HARVEST_NAMES.get(record['kind'], 'Ресурс')} "
                     "ещё не восстановился.", COL_DIM)
            return True
        result = harvest_mod.hit(self.dungeon, self.player, x, y,
                                 self.player.atk, time.time())
        if result is None:
            return True
        self.render_revision = getattr(self, 'render_revision', 0) + 1
        if not result['done']:
            self.log(f"{result['name']}: {result['hp']}/{result['max_hp']}",
                     COL_DIM)
            self.add_float(x, y, str(result['hp']), COL_DIM)
            return True
        from core.data import item_display_name
        names = []
        for key, count in result['items']:
            self.total_items_collected[key] = (
                self.total_items_collected.get(key, 0) + count)
            self.quest_log.update_collect(key, self.player.inventory)
            names.append(f"{item_display_name(key)}×{count}")
        text = ', '.join(names)
        self.log(f"Добыто: {text}", COL_HEAL)
        self.add_float(x, y, '+', COL_HEAL)
        return True


    def pickup(self, x, y):
        from audio import Audio
        result = rules.pickup(self.dungeon, self.player, x, y)
        if result is None:
            return
        Audio.play('pickup')
        self.total_items_collected[result.key] = (
            self.total_items_collected.get(result.key, 0) + 1)
        if result.kind == 'gold':
            self.total_gold_earned += result.gold
            self.log(f"Подобрано: {result.gold} золота", COL_GOLD)
            self.add_float(x, y, f"+{result.gold}g", COL_GOLD)
        elif result.kind == 'resource':
            self.log(f"Собрано: {result.name}", COL_HEAL)
        else:
            self.log(f"Подобрано: {result.name}", result.color)
        self.quest_log.update_collect(result.key, self.player.inventory)


    def consume(self, key):
        rules.consume(self.player.inventory, key)

    # ════════════════════════ ПРЕДМЕТЫ ════════════════════════

    def use_item(self, key):
        from audio import Audio
        result = rules.use_item(self.player, key, self.dungeon.monsters)
        if result is None:
            return
        if not result.ok:
            self.log(result.message, COL_DIM)
            return
        if result.kind == 'heal':
            Audio.play('heal')
            if parse_item_key(key)[0].startswith('potion'):
                self.total_potions_used += 1
            self.log(result.message, COL_HEAL)
            self.add_float(self.player.x, self.player.y, f"+{result.amount}", COL_HEAL)
        elif result.kind == 'equip':
            self.log(result.message, result.color)
        elif result.kind in ('book', 'blueprint'):
            self.log(result.message, COL_GOLD)
            if result.amount:
                self.add_float(self.player.x, self.player.y,
                               f"+{result.amount}", COL_GOLD)
        elif result.kind == 'mana':
            Audio.play('heal')
            self.log(result.message, result.color)
            self.add_float(self.player.x, self.player.y,
                           f"+{result.amount}", result.color)
        elif result.kind == 'buff':
            Audio.play('heal')
            self.log(result.message, result.color)
        elif result.kind in ('poison', 'lightning'):
            Audio.play('fireball' if result.kind == 'lightning' else 'hit')
            for monster, damage in result.victims:
                self.log(f"{result.message}", result.color)
                self.add_float(monster.x, monster.y, str(damage), result.color)
                if monster.hp <= 0:
                    self._kill_monster(monster)
        elif result.kind == 'ward':
            Audio.play('heal')
            self.log(result.message, result.color)
        elif result.kind == 'fireball':
            Audio.play('fireball')
            for monster, damage in result.victims:
                self.log(f"Огонь → {monster.name}: {damage}", (255, 120, 50))
                self.add_float(monster.x, monster.y, str(damage), (255, 120, 50))
                if monster.hp <= 0:
                    self._kill_monster(monster)
            self.log(result.message, COL_GOLD)
        elif result.kind == 'freeze':
            self.log(result.message, result.color)


    def use_item_by_index(self, idx):
        items = rules.inventory_stacks(self.player.inventory)
        start = self.inv_page * INVENTORY_PAGE_SIZE
        page_items = items[start:start + INVENTORY_PAGE_SIZE]
        if 0 <= idx < len(page_items):
            self.use_item(page_items[idx][0])
            self.inv_page = min(self.inv_page, self.inv_pages_count() - 1)


    def use_hotbar(self, idx):
        """Использовать предмет/свиток из быстрой панели."""
        if not (0 <= idx < len(self.hotbar)):
            return
        key = self.hotbar[idx]
        if not key:
            self.log(f"Слот {idx + 1} пуст. Shift+цифра в инвентаре — назначить.",
                     COL_DIM)
            return
        if self.player.inventory.get(key, 0) <= 0:
            self.log(f"В слоте {idx + 1} закончился предмет.", COL_DIM)
            return
        self.use_item(key)


    def assign_hotbar(self, idx):
        """Назначить предмет под курсором в инвентаре на слот панели."""
        mx, my = self.mouse_pos
        for key, rect in self._ui_hits.get('inventory', {}).get('items', []):
            if self._hit(rect, (mx, my)):
                self.hotbar[idx] = key
                self.log(f"Слот {idx + 1}: {item_display_name(key)}", COL_GOLD)
                return
        self.log("Наведи курсор на предмет и нажми Shift+цифру.", COL_DIM)


    def inv_pages_count(self):
        n = len(rules.inventory_stacks(self.player.inventory))
        return max(1, (n + INVENTORY_PAGE_SIZE - 1) // INVENTORY_PAGE_SIZE)


    def inv_next_page(self):
        self.inv_page = min(self.inv_pages_count() - 1, self.inv_page + 1)


    def inv_prev_page(self):
        self.inv_page = max(0, self.inv_page - 1)


    def quick_potion(self):
        p = self.player
        if p.hp >= p.max_hp:
            self.log("HP полное.", COL_DIM); return
        key = rules.best_heal_key(p.inventory)
        if not key:
            self.log("Нет зелий!", COL_DMG); return
        self.use_item(key)


    def cast_active_skill(self, slot):
        """Каст активного навыка класса по F1..F10.

        Проверяет ману и кулдаун. Создаёт Projectile для боевых навыков
        (firebolt/frost_bolt) или применяет heal для heal_self.
        """
        p = self.player
        skills_list = p.active_skills()
        if not (0 <= slot < len(skills_list)):
            return
        skill = skills_list[slot]
        skill_key = skill['key']
        cd_map = getattr(p, 'active_skill_cd', {})
        if cd_map.get(skill_key, 0) > 0:
            self.log(f"{skill['name']}: кулдаун {cd_map[skill_key]:.1f}с",
                     COL_DIM)
            return
        mana_cost = skill.get('mana', 0)
        if p.mana < mana_cost:
            self.log(f"Недостаточно маны: нужно {mana_cost}, есть {p.mana}",
                     COL_DMG)
            return
        p.mana -= mana_cost
        cd_map[skill_key] = skill.get('cooldown', 1.5)
        p.active_skill_cd = cd_map
        kind = skill.get('kind', 'fireball')
        damage = skill.get('damage', 0)
        if kind == 'heal':
            # Отрицательный damage = heal
            heal_amount = -damage if damage < 0 else damage
            p.hp = min(p.max_hp, p.hp + heal_amount)
            self.log(f"{skill['name']}: +{heal_amount} HP", COL_HEAL)
            self.add_float(p.x, p.y - 0.6, f"+{heal_amount}", COL_HEAL)
            return
        # Боевые навыки — ищем ближайшего врага в радиусе 6
        target = None
        best_dist = 999
        for m in self.dungeon.monsters:
            if m.hp <= 0:
                continue
            dist = max(abs(m.x - p.x), abs(m.y - p.y))
            if dist <= 6 and dist < best_dist:
                target = m
                best_dist = dist
        if not target:
            self.log(f"{skill['name']}: нет цели в радиусе 6", COL_DIM)
            # Ману возвращаем, кулдаун не запускаем
            p.mana += mana_cost
            cd_map[skill_key] = 0
            return
        # Создаём Projectile через rules.ranged_attack
        from core import rules as _rules
        proj = _rules.ranged_attack(p, target, kind=kind, damage=damage)
        self.projectiles.append({
            'x': proj.x, 'y': proj.y,
            'tx': proj.tx, 'ty': proj.ty,
            'kind': proj.kind, 'color': proj.color,
            'speed': proj.speed, 'damage': proj.damage,
            'target_ch': proj.target_ch, 'target': target,
            'life': proj.life,
        })
        self.log(f"{skill['name']}! → {target.name}", COL_XP)
        self.add_float(p.x, p.y - 0.6, skill['name'][:6], (200, 150, 255))


    def allocate_stat(self, name):
        p = self.player
        if p.free_points <= 0:
            self.log("Нет свободных очков.", COL_DIM); return
        if name == 'int':
            p.int_stat += 1
        else:
            setattr(p, name, getattr(p, name) + 1)
        p.free_points -= 1
        p.recalc()
        names = {'str': 'Сила', 'agi': 'Ловкость', 'vit': 'Выносливость',
                 'luk': 'Удача', 'df_stat': 'Защита', 'int': 'Интеллект'}
        self.log(f"{names[name]} +1 ({p.free_points} очков)", COL_GOLD)


    def unequip_slot(self, slot):
        p = self.player
        slot = {'helmet': 'head', 'leggings': 'legs'}.get(slot, slot)
        key = getattr(p, slot, None)
        if not key:
            names = {'weapon': 'Оружие', 'armor': 'Броня',
                     'head': 'Шлем', 'legs': 'Поножи', 'boots': 'Обувь'}
            self.log(f"{names.get(slot, 'Слот')}: пусто.", COL_DIM)
            return
        setattr(p, slot, None)
        p.inventory[key] = p.inventory.get(key, 0) + 1
        p.recalc()
        self.log(f"Снято: {item_display_name(key)}", COL_DIM)


    def unequip_weapon(self):
        self.unequip_slot('weapon')


    def unequip_armor(self):
        self.unequip_slot('armor')


    def unequip_helmet(self):
        self.unequip_slot('head')


    def unequip_leggings(self):
        self.unequip_slot('legs')


    def unequip_head(self):
        self.unequip_slot('head')


    def unequip_legs(self):
        self.unequip_slot('legs')


    def unequip_boots(self):
        self.unequip_slot('boots')

    # ════════════════════════ БОЙ ════════════════════════

