"""Переходы между локациями: travel_to, enter/exit_dungeon, ascend, new_dungeon, save, spawn_*.
"""
from game.game._common import *  # noqa: F401,F403 — все импорты


class WorldMixin:
    """Переходы между локациями: travel_to, enter/exit_dungeon, ascend, new_dungeon, save, spawn_*."""

    def get_location(self, key):
        """Ленивая генерация карты локации с кэшированием."""
        if not hasattr(self, 'locations'):
            self.locations = {}
        if key not in self.locations:
            self.locations[key] = locations.generate_location(key)
        return self.locations[key]


    def _remember_location(self):
        """Запомнить прогресс текущей карты поверхности."""
        if self.world_mode == 'overworld' and getattr(self, 'locations', None):
            self.location_explored[self.location] = set(self.explored)


    def travel_to(self, key, entry=None):
        """Переход на другую карту поверхности через врата."""
        if key not in locations.LOCATIONS:
            return False
        self._remember_location()
        target = self.get_location(key)
        self.location = key
        self.overworld = target
        self.dungeon = target
        self.world_mode = 'overworld'
        self.depth = 0
        self.explored = set(self.location_explored.get(key, set()))
        self.merchant = None
        self.shop_stock = []
        self.active_shop_npc = None
        self.quest_board = None

        if entry is None:
            entry = (target.w // 2, target.h - 8)
        spot = rules.find_free_tile(target, entry[0], entry[1],
                                    self.occupied_tiles())
        if spot:
            self.player.x, self.player.y = spot
        else:
            self.player.x, self.player.y = entry
        self.update_fov()
        self._last_fov_pos = (self.player.x, self.player.y)
        self.quest_board = self._spawn_quest_board()
        self.render_revision = getattr(self, 'render_revision', 0) + 1
        self.player_move_timer = PLAYER_MOVE_COOLDOWN
        rules.init_action_timers(target.monsters)
        self.log(f"Ты прибыл: {locations.title(key)}.", COL_GOLD)
        return True


    def _spawn_quest_board(self):
        """Доска на текущей карте или None."""
        if self.world_mode == 'overworld':
            if self.location != 'village':
                return None
            bx, by = ov.QUEST_BOARD_VILLAGE_POS
            if (0 <= bx < MAP_W and 0 <= by < MAP_H
                    and self.overworld.tiles[by][bx] != Tile.WALL):
                e = Entity(bx, by, 1, 1, 0, 0, "Доска поручений", "B",
                           (206, 170, 96), 0)
                e.is_npc = True
                e.npc_type = 'quest_board'
                e.board_depth_min, e.board_depth_max = 0, 4
                return e
            return None
        if self.depth not in (5, 10, 15, 20) or not self.dungeon.rooms:
            return None
        room = self.dungeon.rooms[0]
        bx, by = room.center()
        for dx, dy in ((0, 1), (1, 0), (-1, 0), (0, -1)):
            tx, ty = bx + dx, by + dy
            if (room.x <= tx < room.x + room.w
                    and room.y <= ty < room.y + room.h
                    and self.dungeon.tiles[ty][tx] == Tile.FLOOR
                    and not any(m.x == tx and m.y == ty for m in self.dungeon.monsters)):
                e = Entity(tx, ty, 1, 1, 0, 0, "Доска поручений", "B",
                           (206, 170, 96), 0)
                e.is_npc = True
                e.npc_type = 'quest_board'
                e.board_depth_min = self.depth
                e.board_depth_max = min(self.depth + 4, 20)
                return e
        return None


    def _spawn_city_npcs(self):
        """Расставляет горожан по площади и у ворот."""
        npcs = []
        occupied = set(self.occupied_tiles())
        for npc_type, (nx, ny) in ov.CITY_NPC_POS.items():
            name = CITY_NPC_NAMES.get(npc_type, npc_type)
            ch, color = CITY_NPC_GLYPHS.get(npc_type, ('?', (200, 200, 200)))
            tx, ty = nx, ny
            tile_ok = (0 <= tx < self.overworld.w and 0 <= ty < self.overworld.h
                       and self.overworld.tiles[ty][tx] == Tile.FLOOR)
            if not tile_ok or (tx, ty) in occupied:
                spot = rules.find_free_tile(self.overworld, nx, ny, occupied)
                if not spot:
                    continue
                tx, ty = spot
            atk = 3 if npc_type == 'guard' else 0
            e = Entity(tx, ty, 1, 1, atk, 0, name, ch, color, 0)
            e.is_npc = True
            e.npc_type = npc_type
            if npc_type == 'guard':
                # Стражник бессмертен: мобы его не убивают, а урон низкий.
                e.immortal = True
                e.hp = e.max_hp = 9999
            npcs.append(e)
            occupied.add((tx, ty))
        return npcs


    def enter_dungeon(self):
        # Сохраняем память внешнего мира перед входом
        if self.world_mode == 'overworld':
            self.surface_return = self.location
            self.overworld_explored = set(self.explored)
            self._remember_location()
        self.world_mode = 'dungeon'
        self.depth = 0
        self.new_dungeon(1)
        self.log("Ты входишь в подземелье...", COL_DMG)


    def exit_to_overworld(self):
        if self.depth > 0 and self.depth in self.dungeon_cache:
            self.dungeon_cache[self.depth]['explored'] = set(self.explored)

        self.world_mode = 'overworld'
        self.depth = 0
        # Выходим туда, откуда спускались: карта горы с входом в шахту
        # или другой поверхности.
        self.location = getattr(self, 'surface_return', 'village')
        self.overworld = self.get_location(self.location)
        self.dungeon = self.overworld
        self.explored = set(self.location_explored.get(
            self.location, self.overworld_explored))
        self.overworld_explored = set(self.explored)
        self.visible = set()
        self.merchant = None
        self.shop_stock = []
        self.active_shop_npc = None

        # Спавн рядом со входом в подземелье (если он на этой карте),
        # иначе — в центре деревни / у врат локации.
        if self.overworld.stairs_pos:
            dx, dy = self.overworld.stairs_pos
            spawn = (dx, dy + 1)
        elif self.location == 'village':
            spawn = ov.VILLAGE_CENTER
        else:
            spawn = (self.overworld.w // 2, self.overworld.h - 8)
        spot = rules.find_free_tile(self.overworld, spawn[0], spawn[1],
                                    self.occupied_tiles())
        self.player.x, self.player.y = spot if spot else spawn

        self.update_fov()
        self._last_fov_pos = (self.player.x, self.player.y)
        self.quest_board = self._spawn_quest_board()
        self.render_revision = getattr(self, 'render_revision', 0) + 1
        self.log("Ты возвращаешься на поверхность.", COL_HEAL)


    def ascend(self):
        """Подъём: с глубины 1 — на поверхность, глубже — на этаж выше."""
        from audio import Audio
        Audio.play('stairs')
        if self.depth <= 1:
            self.exit_to_overworld()
        else:
            depth = self.depth - 1
            self.new_dungeon(depth, arrive='stairs')
            self.log(f"Подъём на глубину {depth}.", COL_GOLD)


    def new_dungeon(self, depth, autosave=True, arrive='entry'):
        if self.world_mode == 'dungeon' and self.depth > 0:
            if self.depth in self.dungeon_cache:
                self.dungeon_cache[self.depth]['explored'] = set(self.explored)
                self.dungeon_cache[self.depth]['merchant'] = self.merchant
                self.dungeon_cache[self.depth]['shop_stock'] = self.shop_stock

        self.depth = depth
        self.world_mode = 'dungeon'

        if depth in self.dungeon_cache:
            cached = self.dungeon_cache[depth]
            self.dungeon = cached['dungeon']
            self.merchant = cached.get('merchant')
            self.shop_stock = cached.get('shop_stock', [])
            # Старые сейвы и кэш без лестницы назад — доставляем её
            place_entry_tile(self.dungeon, depth)
            if depth > 1:
                self.log(f"Возвращение на глубину {depth}.", COL_GOLD)
        else:
            self.dungeon = generate_dungeon(depth)
            self.merchant = None
            self.shop_stock = []

            if depth in MERCHANT_FLOORS:
                room = self.dungeon.rooms[0]
                candidates = [
                    (room.x + 1, room.y + 1),
                    (room.x + room.w - 2, room.y + 1),
                    (room.x + 1, room.y + room.h - 2),
                    (room.x + room.w - 2, room.y + room.h - 2),
                ]
                random.shuffle(candidates)
                for mx, my in candidates:
                    if not (0 <= mx < MAP_W and 0 <= my < MAP_H):
                        continue
                    if self.dungeon.tiles[my][mx] == Tile.WALL:
                        continue
                    if (mx, my) == tuple(self.dungeon.rooms[0].center()):
                        continue
                    self.merchant = Entity(mx, my, 1, 1, 0, 0,
                                           "Торговец", "M", COL_MERCH, 0)
                    self.merchant.is_merchant = True
                    self.merchant.is_npc = True
                    self.merchant.npc_type = 'merchant'
                    self.shop.refresh()
                    self.log("Торговец на этаже! Найди его (T).", COL_MERCH)
                    break

            self.dungeon_cache[depth] = {
                'dungeon': self.dungeon,
                'merchant': self.merchant,
                'shop_stock': self.shop_stock,
                'explored': set(),
            }

            if depth > 1:
                self.log(f"Спуск на глубину {depth}.", COL_GOLD)
            if depth in BOSSES:
                self.log(f"⚠ {BOSSES[depth]['name']} на глубине {depth}!", COL_DMG)

        # Точка появления: вход снизу (центр первой комнаты) или
        # лестница вниз, если поднимаемся с этажа ниже.
        if arrive == 'stairs' and self.dungeon.stairs_pos:
            cx, cy = self.dungeon.stairs_pos
        else:
            cx, cy = self.dungeon.rooms[0].center()
        self.player.x, self.player.y = cx, cy

        # Восстанавливаем explored для этой глубины
        self.explored = set(self.dungeon_cache[depth].get('explored', set()))
        self.update_fov()
        self._last_fov_pos = (self.player.x, self.player.y)
        # Синхронизируем кэш
        self.dungeon_cache[depth]['explored'] = set(self.explored)
        self.render_revision = getattr(self, 'render_revision', 0) + 1
        # Индивидуальные таймеры ходов и квест «достичь глубины»
        rules.init_action_timers(self.dungeon.monsters)
        self.quest_log.update_reach(depth)

        if autosave and depth > 1:
            self.save(silent=True)


    def save(self, path=None, silent=False):
        if path is None:
            path = save_slot_path(self.current_slot)
        if self.dead or self.victory:
            if not silent:
                self.log("Нечего сохранять.", COL_DIM)
            return
        # Текущий этаж синхронизируем в кэш и сохраняем отдельным ключом
        # `dungeon` (в кэше дубликата не будет).
        if self.world_mode == 'dungeon':
            self.dungeon_cache[self.depth] = {
                'dungeon': self.dungeon,
                'merchant': self.merchant,
                'shop_stock': self.shop_stock,
                'explored': set(self.explored),
            }
        else:
            self.surface_return = self.location
            self.location_explored[self.location] = set(self.explored)
        data = {
            'version': SAVE_VERSION,
            'world_mode': self.world_mode,
            'depth': self.depth,
            'location': self.location,
            'surface_return': self.surface_return,
            'locations': {key: dungeon_to_dict(d)
                          for key, d in self.locations.items()},
            'location_explored': {
                key: [list(p) for p in points]
                for key, points in self.location_explored.items()},
            'explored': [list(p) for p in self.explored],
            'messages': [{'text': m.text, 'color': list(m.color)}
                         for m in self.messages],
            'player': player_to_dict(self.player),
            'overworld': dungeon_to_dict(self.overworld),
            'overworld_explored': [list(p) for p in self.overworld_explored],
            # В открытом мире dungeon — это и есть overworld: не дублируем.
            'dungeon': (dungeon_to_dict(self.dungeon)
                        if self.world_mode == 'dungeon' else None),
            'dungeon_cache': dungeon_cache_to_dict(
                self.dungeon_cache, current_depth=self.depth,
                skip_depth=self.depth if self.world_mode == 'dungeon' else None),
            'merchant': entity_to_dict(self.merchant) if self.merchant else None,
            'shop_stock': self.shop_stock,
            'quests': self.quest_log.to_dict(),
            'run_stats': {
                'total_gold_earned': self.total_gold_earned,
                'total_kills_by_ch': dict(self.total_kills_by_ch),
                'total_items_collected': dict(self.total_items_collected),
                'total_potions_used': self.total_potions_used,
                'total_crafts': self.total_crafts,
                'total_quests_turned_in': self.total_quests_turned_in,
                'start_time': self.start_time,
                'kills': self.kills,
                'boss_kills': self.boss_kills,
            },
            'elder': entity_to_dict(self.elder) if self.elder else None,
            'city_npcs': [entity_to_dict(n)
                          for n in getattr(self, 'city_npcs', [])],
            'hotbar': list(getattr(self, 'hotbar', [])),
        }
        tmp = path + '.tmp'
        try:
            with open(tmp, 'w', encoding='utf-8') as f:
                # Компактно: сейв — машинные данные, отступы раздували
                # файл втрое (336 КБ против ~100 КБ).
                json.dump(data, f, ensure_ascii=False,
                          separators=(',', ':'))
            os.replace(tmp, path)
            if not silent:
                self.log(f"Сохранено в слот {self.current_slot}.", COL_HEAL)
        except Exception as e:
            self.log(f"Ошибка сохранения: {e}", COL_DMG)


