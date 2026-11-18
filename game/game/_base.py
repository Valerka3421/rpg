"""Базовое состояние Game: конструктор, new_game, NPC, лог, FOV, save/load, update/draw.
"""
from game.game._common import *  # noqa: F401,F403 — все импорты


class GameBase:
    """Базовое состояние Game: конструктор, new_game, NPC, лог, FOV, save/load, update/draw."""

    def __init__(self, screen):
        self.renderer = Renderer(screen)
        self.settings = load_settings()

        from audio import Audio
        Audio.init(volume=self.settings.get('volume', 7))

        # Контроллеры
        self.menu = MenuController(self)
        self.shop = ShopController(self)
        self.craft = CraftController(self)
        self.skills_ui = SkillsController(self)
        self.dev_console = DevConsoleController(self)

        self.dialogue = DialogueController(self)
        self.quests = QuestController(self)
        self.stats_run = StatsController(self)

        # Состояние
        self.pending_class_key = 'warrior'
        self.current_slot = 1
        self.mouse_pos = (-1, -1)
        self.new_game()
        self.screen_mode = 'menu'

    # ════════════════════════ СОСТОЯНИЕ ════════════════════════

    def new_game(self, class_key=None):
        if class_key is None:
            class_key = getattr(self, 'pending_class_key', 'default') or 'default'
        self.pending_class_key = class_key

        # Инициализация контроллеров — для тестов через Game.__new__
        if not hasattr(self, 'menu'):
            self.menu = MenuController(self)
        if not hasattr(self, 'shop'):
            self.shop = ShopController(self)
        if not hasattr(self, 'craft'):
            self.craft = CraftController(self)
        if not hasattr(self, 'skills_ui'):
            self.skills_ui = SkillsController(self)
        if not hasattr(self, 'dev_console'):
            self.dev_console = DevConsoleController(self)
        else:
            self.dev_console.return_mode = 'game'
            self.dev_console.buffer = ''
            self.dev_console.error = ''
        if not hasattr(self, 'dialogue'):
            self.dialogue = DialogueController(self)
        if not hasattr(self, 'quests'):
            self.quests = QuestController(self)
        if not hasattr(self, 'stats_run'):
            self.stats_run = StatsController(self)
        if not hasattr(self, 'current_slot'):
            self.current_slot = 1
        if not hasattr(self, 'settings'):
            self.settings = load_settings()
        if not hasattr(self, 'mouse_pos'):
            self.mouse_pos = (-1, -1)

        self.player = Player(class_key=class_key)
        self.messages = []
        self.log_scroll = 0
        self._lvl_float_idx = 0
        self.dead = False
        self.victory = False
        self.visible = set()
        self.explored = set()
        self.overworld_explored = set()
        self.screen_mode = 'game'
        self.shop_submode = 'buy'
        self.inv_page = 0
        # Быстрая панель: 8 слотов, клавиши 1–8.
        self.hotbar = [None] * 8
        # Летящие снаряды (стрелы, магические шары).
        self.projectiles = []
        self.floats = []
        self.merchant = None
        self.shop_stock = []
        self.dungeon_cache = {}
        self.world_mode = 'overworld'
        self.depth = 0
        # Каждая локация — отдельная карта. Генерируются лениво и живут в
        # self.locations; сейв сохраняет их целиком, поэтому карта не
        # пересоздаётся при каждом визите.
        self.locations = {}
        self.location = 'village'
        self.surface_return = 'village'
        self.location_explored = {}
        self.overworld = self.get_location('village')
        self.dungeon = self.overworld
        sx, sy = ov.VILLAGE_CENTER
        if self.overworld.tiles[sy][sx] == Tile.FLOOR:
            self.player.x, self.player.y = sx, sy
        else:
            spot = rules.find_free_tile(self.overworld, sx, sy)
            if spot:
                self.player.x, self.player.y = spot
        self.shop.page = 0

        # Квесты — выдаём все доступные от обоих NPC
        self.quest_log = QuestLog()


        # Счётчики забега
        self.start_time = time.time()
        self.kills = 0
        self.boss_kills = 0
        self.total_gold_earned = 0
        self.total_kills_by_ch = {}
        self.total_items_collected = {}
        self.total_potions_used = 0
        self.total_crafts = 0
        self.total_quests_turned_in = 0
        self.quest_board = None

        self._modal_close_rect = None
        self.pending_resolution = None
        self.pending_multiplayer = None
        self._modal_content_rect = (0, 0, 0, 0)
        self._ui_hits = {}

        # Старейшина живёт в городе, на центральной площади
        self.city_npcs = []
        self.active_shop_npc = None
        self.elder = None
        ex, ey = ov.ELDER_POS
        if (0 <= ex < MAP_W and 0 <= ey < MAP_H
                and self.overworld.tiles[ey][ex] == Tile.FLOOR):
            self.elder = Entity(ex, ey, 1, 1, 0, 0,
                                "Старейшина", "E", (240, 210, 100), 0)
            self.elder.is_npc = True
            self.elder.npc_type = 'elder'
        else:
            spot = rules.find_free_tile(
                self.overworld, ov.VILLAGE_CENTER[0], ov.VILLAGE_CENTER[1],
                self.occupied_tiles())
            if spot:
                tx, ty = spot
                self.elder = Entity(tx, ty, 1, 1, 0, 0,
                                    "Старейшина", "E", (240, 210, 100), 0)
                self.elder.is_npc = True
                self.elder.npc_type = 'elder'

        if self.elder is None:
            self.log("Старейшине не нашлось места — поговори с ним позже.", COL_DIM)

        self.quest_board = self._spawn_quest_board()
        self.city_npcs = self._spawn_city_npcs()
        self._guard_timer = 0.0
        self.update_fov()
        self.log("Добро пожаловать в город Этельгард!", COL_TEXT)
        self.log("Кузнец, алхимик и трактирщик ждут покупателей на площади.",
                 COL_GOLD)
        self.log("Восемь лесных краёв ждут исследователя. Ищи дороги.", COL_GOLD)
        self.log("Вход в подземелье — на Каменном перевале (северо-восток).",
                 COL_GOLD)
        self.log(f"Цель: победить Древнего дракона на глубине {MAX_DEPTH}.", COL_GOLD)
        # Real-time таймеры
        self.player_move_timer = 0.0
        self.player_attack_timer = 0.0
        self.status_tick_timer = STATUS_TICK_INTERVAL
        self.regen_timer = REGEN_TICK_INTERVAL
        self._last_fov_pos = (self.player.x, self.player.y)
        # Счётчик для кэшей рендера: карта сменилась
        self.render_revision = getattr(self, 'render_revision', 0) + 1
        rules.init_action_timers(self.overworld.monsters)


    def occupied_tiles(self):
        """Занятые клетки: игрок, NPC, мобы, торговец."""
        occupied = {(self.player.x, self.player.y)}
        occupied.update((m.x, m.y) for m in self.dungeon.monsters if m.hp > 0)
        for npc in self._all_npcs():
            occupied.add((npc.x, npc.y))
        return occupied


    def _all_npcs(self):
        npcs = []
        if self.merchant is not None:
            npcs.append(self.merchant)
        if self.elder is not None:
            npcs.append(self.elder)
        # Горожане существуют только на карте города.
        if (self.world_mode == 'overworld'
                and getattr(self, 'location', 'village') == 'village'):
            npcs.extend(getattr(self, 'city_npcs', ()) or ())
        return npcs


    def npc_blocking_at(self, x, y):
        for npc in self._all_npcs():
            if npc.x == x and npc.y == y:
                return npc
        return None


    def nearby_city_npc(self):
        """Горожанин на соседней клетке (для разговора по T)."""
        if (self.world_mode != 'overworld'
                or getattr(self, 'location', 'village') != 'village'):
            return None
        for npc in getattr(self, 'city_npcs', ()) or ():
            if max(abs(self.player.x - npc.x),
                   abs(self.player.y - npc.y)) <= 1:
                return npc
        return None

    # ════════════════════════ ЛОКАЦИИ ════════════════════════

    def ground_kind(self, x, y):
        return locations.ground_kind(self.location, x, y)


    def wall_kind(self, x, y):
        return locations.wall_kind(self.location, x, y)


    def location_title(self):
        return locations.title(self.location)


    def log(self, msg, color=COL_TEXT):
        self.messages.append(Message(msg, color))
        if len(self.messages) > 50:
            self.messages = self.messages[-50:]


    def scroll_log(self, delta):
        max_scroll = max(0, len(self.messages) - 6)
        self.log_scroll = max(0, min(max_scroll, self.log_scroll + delta))


    def add_float(self, x, y, text, color):
        self.floats.append(
            FloatText(x * TILE + TILE // 2, y * TILE + TILE // 2, text, color))

    # ════════════════════════ ПЕРЕХОДЫ МЕЖДУ МИРАМИ ════════════════════════

    def update_fov(self):
        # Туман войны убран везде: игрок видит всю карту локации/этажа.
        d = self.dungeon
        self.visible = {(x, y) for y in range(d.h) for x in range(d.w)}
        self.explored |= self.visible

    # ════════════════════════ SAVE / LOAD ════════════════════════

    def load(self, path=None):
        if path is None:
            path = save_slot_path(self.current_slot)
        if not os.path.exists(path):
            self.log("Сохранения нет. Сначала F5.", COL_DMG)
            return False
        try:
            with open(path, encoding='utf-8') as f:
                data = json.load(f)

            version = int(data.get('version', 5))
            if version > SAVE_VERSION:
                raise ValueError(f"сейв новее игры (v{version} > v{SAVE_VERSION})")

            self.player = player_from_dict(data['player'])
            self.location = data.get('location', 'village')
            self.surface_return = data.get('surface_return', self.location)
            # Новый формат хранит все посещённые карты; старый — одну
            # карту поверхности в ключе `overworld`.
            self.locations = {}
            if data.get('locations'):
                self.locations = {
                    key: dungeon_from_dict(raw, is_location=True)
                    for key, raw in data['locations'].items()}
            elif data.get('overworld'):
                self.locations['village'] = dungeon_from_dict(
                    data['overworld'], is_overworld=True)
            self.location_explored = {
                key: {tuple(p) for p in points}
                for key, points in (data.get('location_explored') or {}).items()}
            if self.location not in self.locations:
                self.locations[self.location] = locations.generate_location(
                    self.location)
            if self.surface_return not in self.locations:
                self.locations[self.surface_return] = locations.generate_location(
                    self.surface_return)
            self.overworld = self.locations[self.surface_return]
            self.world_mode = data.get('world_mode', 'overworld')
            self.depth = int(data.get('depth', 0))
            self.dungeon_cache = dungeon_cache_from_dict(
                data.get('dungeon_cache', {}))
            self.explored = {tuple(p) for p in data.get('explored', [])}
            self.overworld_explored = {
                tuple(p) for p in data.get('overworld_explored', [])}

            if self.world_mode == 'dungeon':
                raw = data.get('dungeon')
                if raw:
                    self.dungeon = dungeon_from_dict(raw)
                elif self.depth in self.dungeon_cache:
                    self.dungeon = self.dungeon_cache[self.depth]['dungeon']
                else:                                   # повреждённый сейв
                    self.dungeon = generate_dungeon(self.depth)
                place_entry_tile(self.dungeon, self.depth)
                cached = self.dungeon_cache.get(self.depth, {})
                self.merchant = (entity_from_dict(data['merchant'])
                                 if data.get('merchant') else cached.get('merchant'))
                self.shop_stock = data.get('shop_stock') or cached.get('shop_stock', [])
                # Приводим кэш к единому виду: текущий этаж = self.dungeon
                self.dungeon_cache[self.depth] = {
                    'dungeon': self.dungeon,
                    'merchant': self.merchant,
                    'shop_stock': self.shop_stock,
                    'explored': set(self.explored),
                }
            else:
                self.overworld = self.locations[self.location]
                self.surface_return = self.location
                self.dungeon = self.overworld
                self.explored = self.location_explored.get(
                    self.location, self.explored)
                self.merchant = None
                self.shop_stock = []
                self.active_shop_npc = None
                self.overworld_explored = set(self.explored)

            if self.elder is None:
                spot = rules.find_free_tile(
                    self.overworld, ov.VILLAGE_CENTER[0], ov.VILLAGE_CENTER[1],
                    self.occupied_tiles())
                if spot:
                    ex, ey = spot
                    self.elder = Entity(ex, ey, 1, 1, 0, 0,
                                        "Старейшина", "E", (240, 210, 100), 0)
                    self.elder.is_npc = True
                    self.elder.npc_type = 'elder'

            self.messages = [Message(m['text'], tuple(m['color']))
                             for m in data.get('messages', [])]
            self.dead = False
            self.victory = False
            self.screen_mode = 'game'
            self.shop_submode = 'buy'
            self.shop.page = 0
            self.inv_page = 0
            self.hotbar = list(data.get('hotbar') or [])[:8]
            self.hotbar += [None] * (8 - len(self.hotbar))
            self.log_scroll = 0
            self.floats = []
            self._lvl_float_idx = 0
            self.quest_log = QuestLog.from_dict(data.get('quests', {}))
            rs = data.get('run_stats', {})
            self.total_gold_earned = rs.get('total_gold_earned', 0)
            self.total_kills_by_ch = dict(rs.get('total_kills_by_ch', {}))
            self.total_items_collected = dict(rs.get('total_items_collected', {}))
            self.total_potions_used = rs.get('total_potions_used', 0)
            self.total_crafts = rs.get('total_crafts', 0)
            self.total_quests_turned_in = rs.get('total_quests_turned_in', 0)
            self.start_time = rs.get('start_time', time.time())
            self.kills = rs.get('kills', 0)
            self.boss_kills = rs.get('boss_kills', 0)
            if data.get('elder'):
                self.elder = entity_from_dict(data['elder'])

            if data.get('city_npcs') is not None:
                self.city_npcs = [entity_from_dict(n)
                                  for n in data['city_npcs']]
                for npc in self.city_npcs:
                    if npc.npc_type == 'guard':
                        npc.immortal = True
                        npc.hp = npc.max_hp = 9999
            else:
                self.city_npcs = self._spawn_city_npcs()
            self.active_shop_npc = None
            self._guard_timer = 0.0

            self.player_move_timer = 0.0
            self.player_attack_timer = 0.0
            self.status_tick_timer = STATUS_TICK_INTERVAL
            self.regen_timer = REGEN_TICK_INTERVAL
            rules.init_action_timers(self.dungeon.monsters)
            self.update_fov()
            self._last_fov_pos = (self.player.x, self.player.y)
            self.render_revision = getattr(self, 'render_revision', 0) + 1
            self.log(f"Загружено из слота {self.current_slot}.", COL_HEAL)
            return True
        except Exception as e:
            self.log(f"Ошибка загрузки: {e}", COL_DMG)
            return False

    # ════════════════════════ ДВИЖЕНИЕ ════════════════════════

    def level_up(self):
        """Повышает уровень на 1 (совместимость с квестами и тестами)."""
        level = rules.level_up_once(self.player)
        self._on_level_up(level)


    def _on_level_up(self, level):
        from audio import Audio
        Audio.play('level_up')
        p = self.player
        self.log(f"Уровень {level}! +5 очков, +1 очко навыка, HP восстановлено.",
                 COL_GOLD)
        n = self._lvl_float_idx % 4
        self._lvl_float_idx = n + 1
        self.add_float(p.x, p.y - n * 0.8, f"LVL {level}", COL_GOLD)

    # ════════════════════════ AI ════════════════════════

    def death_summary(self):
        """Сводка для death recap."""
        import time
        elapsed = 0
        if getattr(self, 'start_time', None):
            end = getattr(self, 'death_time', time.time())
            elapsed = int(end - self.start_time)
        return {
            'kills': getattr(self, 'kills', 0),
            'boss_kills': getattr(self, 'boss_kills', 0),
            'depth': self.depth,
            'world_mode': self.world_mode,
            'level': self.player.level,
            'gold': self.player.gold,
            'time_seconds': elapsed,
            'last_messages': [m.text for m in self.messages[-5:]],
        }


    def run_summary(self):
        import time as _t
        elapsed = int(_t.time() - getattr(self, 'start_time', _t.time()))
        return {
            'kills': self.kills,
            'boss_kills': self.boss_kills,
            'gold': self.player.gold,  # ← текущее
            'earned_gold': self.total_gold_earned,  # ← добыто за забег
            'depth': self.depth,
            'level': self.player.level,
            'time_seconds': elapsed,
            'kills_by_ch': dict(self.total_kills_by_ch),
            'items_collected': dict(self.total_items_collected),
            'potions_used': self.total_potions_used,
            'crafts': self.total_crafts,
            'quests': self.total_quests_turned_in,
            'quests_active': len(self.quest_log.active),
        }

    # ════════════════════════ ОБЁРТКИ (для renderer и тестов) ════════════════════════

    def update(self, dt):
        # ── Анимации (работают даже в паузе, чтобы idle не замирал) ──
        self.player.anim_time += dt
        if self.player.attack_anim > 0:
            self.player.attack_anim = max(0.0, self.player.attack_anim - dt / 0.25)
        # ── Кулдауны активных навыков класса ──
        cd_map = getattr(self.player, 'active_skill_cd', {})
        if cd_map:
            for key in list(cd_map.keys()):
                cd_map[key] = max(0.0, cd_map[key] - dt)
        if self.merchant:
            self.merchant.anim_time += dt
        if self.elder:
            self.elder.anim_time += dt
        for npc in getattr(self, 'city_npcs', ()) or ():
            npc.anim_time += dt
            if npc.attack_anim > 0:
                npc.attack_anim = max(0.0, npc.attack_anim - dt / 0.25)
        for m in self.dungeon.monsters:
            m.anim_time += dt
            if m.attack_anim > 0:
                m.attack_anim = max(0.0, m.attack_anim - dt / 0.25)

        # Floating texts
        for f in self.floats:
            f.life -= dt
            f.y -= 40 * dt
        self.floats = [f for f in self.floats if f.life > 0]

        self.update_projectiles(dt)

        # Пауза: смерть/победа/меню
        if self.dead or self.victory:
            return
        if self.screen_mode == 'craft':
            self.craft.update(dt)
        if self.screen_mode != 'game':
            self.player_move_timer = 0.0
            self.player_attack_timer = 0.0
            self.status_tick_timer = STATUS_TICK_INTERVAL
            self.regen_timer = REGEN_TICK_INTERVAL
            return

        # Тикаем таймеры
        self.player_move_timer = max(0.0, self.player_move_timer - dt)
        self.player_attack_timer = max(0.0, self.player_attack_timer - dt)
        self.status_tick_timer -= dt
        self.regen_timer -= dt

        # Кусты ягод и деревья/руда восстанавливаются по реальному времени.
        from core import harvest as harvest_mod
        if harvest_mod.tick(self.dungeon, time.time()):
            self.render_revision = getattr(self, 'render_revision', 0) + 1

        # ── Защита: FOV пересчитывается сразу при смене позиции ──
        if (self.player.x, self.player.y) != self._last_fov_pos:
            self.update_fov()
            self._last_fov_pos = (self.player.x, self.player.y)

        # Реген — теперь использует config.REGEN_HP_PER_TICK/MANA_PER_TICK
        # (замедленный: было +1 HP каждые 2 сек, стало +1 HP каждые 5 сек).
        if self.regen_timer <= 0:
            self.regen_timer += REGEN_TICK_INTERVAL
            from core import config as _cfg
            if self.player.regen and self.player.hp < self.player.max_hp:
                self.player.hp = min(self.player.max_hp,
                                     self.player.hp + _cfg.REGEN_HP_PER_TICK)
            # Мана регенерируется всегда, но медленно
            if self.player.mana < self.player.max_mana:
                self.player.mana = min(self.player.max_mana,
                                       self.player.mana + _cfg.REGEN_MANA_PER_TICK)

        # Тик статусов
        if self.status_tick_timer <= 0:
            self.status_tick_timer += STATUS_TICK_INTERVAL
            self._tick_statuses()
            if self.dead:
                return

        # Ходы мобов: у каждого свой таймер (без синхронного «залпа»)
        self.monsters_update(dt)
        if self.dead:
            return
        # Стражник города бьёт мобов, подошедших к воротам.
        self._update_city_guard(dt)
        if self.dead:
            return
        # FOV уже пересчитан выше при смене позиции игрока. Мобы и статусы
        # не меняют геометрию обзора, поэтому второй вызов каждый кадр
        # был чистой потерей ~0.4 мс.


    def draw(self):
        self.renderer.draw(self)


    def _modal_panel_rect(self):
        from core.config import MODAL_MARGIN_X, MODAL_MARGIN_Y
        return (MODAL_MARGIN_X, MODAL_MARGIN_Y,
                config.SCREEN_W - MODAL_MARGIN_X * 2,
                config.SCREEN_H - MODAL_MARGIN_Y * 2)

