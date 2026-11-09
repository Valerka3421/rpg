"""ClientView — адаптер под Renderer + обработка кликов/клавиш."""
import time

import pygame

from core import config
from core.config import MAP_W, MAP_H, Tile, SHOP_PAGE_SIZE
from core.entities import Player, Entity, Dungeon, Message
from core.world import compute_fov
from core.data import ITEMS, parse_item_key

# За сколько секунд клиент «догоняет» позицию из снапшота
INTERP_TIME = 0.06


class ClientView:
    """Эмулирует поля Game для Renderer.draw()."""

    def __init__(self, send_fn=None):
        self.send_fn = send_fn or (lambda m: None)

        self.player = Player(class_key='warrior')
        self.remote_players = {}
        self.my_id = 0
        self.dungeon = Dungeon(MAP_W, MAP_H)
        self.visible = set()
        self.explored = set()
        self.merchant = None
        self.elder = None
        self.floats = []
        self.messages = []
        self.screen_mode = 'game'
        self.dead = False
        self.victory = False
        self.depth = 0
        self.world_mode = 'overworld'
        self.log_scroll = 0
        self.current_slot = 1
        self.settings = {
            'show_fps': False, 'show_minimap': True, 'volume': 7,
            'fullscreen': False, 'resolution': [1280, 720],
        }
        # Магазин
        self.shop_stock = []
        self.shop_submode = 'buy'
        self.shop = _ShopStub(self)
        self.shop_message = ''
        # Чат
        self.chat_active = False
        self.chat_buffer = ''
        # Стабы для модалок
        self.menu = None
        self.dialogue = None
        self.quests = _QuestStub()
        self.craft = _CraftStub()
        # UI
        self._modal_content_rect = (0, 0, 0, 0)
        self._modal_close_rect = None
        self._ui_hits = {
            'inventory': {'items': [], 'equip': []},
            'stats': {'plus': []},
            'craft': {'hammer': []},
            'shop': {'rows': []},
        }
        self.mouse_pos = (-1, -1)
        self.pending_resolution = None
        self.pending_restart = False
        self._last_fov_pos = (-1, -1)
        self.dungeon_cache = {}
        self._prev_snapshot = None
        self._prev_monsters = 0
        self.inv_page = 0
        # Интерполяция: {('p', pid)/('m', mid): {...}}
        self._motion = {}
        self._monsters_by_mid = {}

    # ════════════════════════ API ДЛЯ RENDERER ════════════════════════
    def _toolbar_rects(self):
        from core.config import TOOLBAR_BTN, TOOLBAR_GAP, TOOLBAR_MARGIN
        return [(name, (TOOLBAR_MARGIN + i * (TOOLBAR_BTN + TOOLBAR_GAP),
                        TOOLBAR_MARGIN, TOOLBAR_BTN, TOOLBAR_BTN))
                for i, name in enumerate(
                    ('inventory', 'craft', 'stats', 'quests'))]

    def _modal_panel_rect(self):
        from core.config import MODAL_MARGIN_X, MODAL_MARGIN_Y
        return (MODAL_MARGIN_X, MODAL_MARGIN_Y,
                config.SCREEN_W - MODAL_MARGIN_X * 2,
                config.SCREEN_H - MODAL_MARGIN_Y * 2)

    def craftable_list(self):
        """Список рецептов — та же логика, что у сервера."""
        from core import rules
        return rules.craftable_list(self.player.inventory, self.player.level)

    def log(self, msg, color=None):
        """Лог боя в MP: сервер присылает события, клиент показывает их
        в той же панели, что и одиночная игра."""
        self.messages.append(Message(msg, color or (225, 225, 235)))
        if len(self.messages) > 50:
            self.messages = self.messages[-50:]
            self.log_scroll = min(self.log_scroll,
                                  max(0, len(self.messages) - 6))

    def scroll_log(self, delta):
        max_scroll = max(0, len(self.messages) - 6)
        self.log_scroll = max(0, min(max_scroll, self.log_scroll + delta))

    def add_float(self, *args, **kwargs):
        pass

    def update_fov(self):
        self.visible = compute_fov(self.dungeon, self.player.x, self.player.y)
        self.explored |= self.visible

    # ════════════════════════ ЧАТ ════════════════════════
    def draw_chat(self, screen):
        """Строка ввода чата внизу экрана."""
        from render.theme import Theme as T
        h = 30
        y = config.SCREEN_H - h - 6
        rect = (8, y, config.SCREEN_W - 16, h)
        T.rounded_rect(screen, (18, 18, 28), rect, radius=6,
                       border_color=(120, 120, 160))
        text = f"Чат: {self.chat_buffer}_"
        T.text(screen, text, 20, y + 6, (230, 240, 255), font=T.FONT_BODY)
        T.text(screen, "Enter — отправить · Esc — отмена",
               config.SCREEN_W - 20, y + 8, (120, 120, 140),
               font=T.FONT_SMALL, align_right=True)

    def handle_chat_key(self, key, text=''):
        if key == pygame.K_ESCAPE:
            self.chat_active = False
            self.chat_buffer = ''
        elif key in (pygame.K_RETURN, pygame.K_KP_ENTER):
            message = self.chat_buffer.strip()
            self.chat_active = False
            self.chat_buffer = ''
            if message:
                self.send_fn({'type': 'chat', 'text': message})
        elif key == pygame.K_BACKSPACE:
            self.chat_buffer = self.chat_buffer[:-1]
        elif text and text.isprintable() and len(self.chat_buffer) < 120:
            self.chat_buffer += text

    # ════════════════════════ ИНТЕРПОЛЯЦИЯ ════════════════════════
    def _track_motion(self, key, entity, tx, ty, now):
        current_x = float(getattr(entity, 'render_x', entity.x))
        current_y = float(getattr(entity, 'render_y', entity.y))
        motion = self._motion.get(key)
        if motion is None:
            entity.render_x, entity.render_y = float(tx), float(ty)
            self._motion[key] = {
                'entity': entity, 'fx': float(tx), 'fy': float(ty),
                'tx': float(tx), 'ty': float(ty), 't0': -1.0,
            }
            return
        motion['entity'] = entity
        motion['fx'], motion['fy'] = current_x, current_y
        motion['tx'], motion['ty'] = float(tx), float(ty)
        motion['t0'] = now
        entity.render_x, entity.render_y = current_x, current_y

    def tick(self, dt):
        """Плавно догоняем позиции из последнего снапшота и тикаем анимации."""
        now = time.perf_counter()
        for key, motion in list(self._motion.items()):
            entity = motion['entity']
            if motion['t0'] <= 0:
                entity.render_x, entity.render_y = motion['tx'], motion['ty']
            else:
                alpha = min(1.0, (now - motion['t0']) / INTERP_TIME)
                entity.render_x = motion['fx'] + (motion['tx'] - motion['fx']) * alpha
                entity.render_y = motion['fy'] + (motion['ty'] - motion['fy']) * alpha
            # анимации считаем локально — 20 Гц снапшотов для них мало
            entity.anim_time += dt
            if getattr(entity, 'attack_anim', 0) > 0:
                entity.attack_anim = max(0.0, entity.attack_anim - dt / 0.25)

    # ════════════════════════ ВВОД: КЛАВИШИ ════════════════════════
    def handle_key(self, key):
        if self.screen_mode == 'shop':
            self._handle_shop_key(key)
            return
        if self.screen_mode != 'game':
            if key in (pygame.K_ESCAPE, pygame.K_i, pygame.K_c,
                       pygame.K_k, pygame.K_l):
                self.screen_mode = 'game'
            return

        if key == pygame.K_i:
            self.screen_mode = 'inventory'
        elif key == pygame.K_c:
            self.screen_mode = 'stats'
        elif key == pygame.K_k:
            self.screen_mode = 'craft'
        elif key == pygame.K_l:
            self.screen_mode = 'quests'
        elif key == pygame.K_h:
            # Быстрое зелье: самое слабое из имеющихся
            potions = []
            for k, count in self.player.inventory.items():
                base, _ = parse_item_key(k)
                data = ITEMS.get(base, {})
                if count > 0 and data.get('type') == 'heal':
                    potions.append((data.get('value', 0), k))
            if potions:
                potions.sort()
                self.send_fn({'type': 'use_item', 'key': potions[0][1]})
        elif key in (pygame.K_t, pygame.K_RETURN):
            # Торговец в MP: подойти и нажать T
            if self.merchant and max(abs(self.player.x - self.merchant.x),
                                     abs(self.player.y - self.merchant.y)) <= 1:
                self.shop.page = 0
                self.shop_submode = 'buy'
                self.screen_mode = 'shop'
        elif key == pygame.K_y:
            self.chat_active = True
            self.chat_buffer = ''
        elif key == pygame.K_PAGEUP:
            self.scroll_log(3)
        elif key == pygame.K_PAGEDOWN:
            self.scroll_log(-3)
        elif key == pygame.K_HOME:
            self.scroll_log(10_000)
        elif key == pygame.K_END:
            self.log_scroll = 0

    def _handle_shop_key(self, key):
        if key in (pygame.K_t, pygame.K_ESCAPE):
            self.screen_mode = 'game'
        elif key == pygame.K_s:
            self.shop_submode = 'sell' if self.shop_submode == 'buy' else 'buy'
            self.shop.page = 0
        elif key == pygame.K_q:
            self.shop.page = max(0, self.shop.page - 1)
        elif key == pygame.K_e:
            self.shop.page = min(self.shop.pages_count() - 1, self.shop.page + 1)
        elif key == pygame.K_r and self.shop_submode == 'buy':
            self.send_fn({'type': 'reroll'})
        elif pygame.K_1 <= key <= pygame.K_9:
            idx = self.shop.page * SHOP_PAGE_SIZE + (key - pygame.K_1)
            if self.shop_submode == 'buy':
                self.send_fn({'type': 'buy', 'idx': idx})
            else:
                self.send_fn({'type': 'sell', 'idx': idx})

    # ════════════════════════ ВВОД: МЫШЬ ════════════════════════
    def handle_mouse_motion(self, pos):
        self.mouse_pos = pos

    def handle_mouse_click(self, pos):
        # Крестик закрытия модалки — первым
        if self.screen_mode != 'game' and self._modal_close_rect:
            if self._hit(self._modal_close_rect, pos):
                self.screen_mode = 'game'
                return True

        # Тулбар — только в игровом режиме
        if self.screen_mode == 'game':
            for name, rect in self._toolbar_rects():
                if self._hit(rect, pos):
                    if name == 'inventory':
                        self.screen_mode = 'inventory'
                    elif name == 'stats':
                        self.screen_mode = 'stats'
                    elif name == 'craft':
                        self.screen_mode = 'craft'
                    elif name == 'quests':
                        self.screen_mode = 'quests'
                    return True
            return False

        # Клики внутри модалок
        if self.screen_mode == 'inventory':
            return self._click_inventory(pos)
        if self.screen_mode == 'stats':
            return self._click_stats(pos)
        if self.screen_mode == 'craft':
            return self._click_craft(pos)
        if self.screen_mode == 'shop':
            return self._click_shop(pos)
        return False

    def _hit(self, rect, pos):
        x, y, w, h = rect
        return x <= pos[0] <= x + w and y <= pos[1] <= y + h

    def _click_inventory(self, pos):
        for slot, rect in self._ui_hits['inventory']['equip']:
            if self._hit(rect, pos):
                self.send_fn({'type': 'unequip', 'slot': slot})
                return True
        for key, rect in self._ui_hits['inventory']['items']:
            if self._hit(rect, pos):
                self.send_fn({'type': 'use_item', 'key': key})
                return True
        return False

    def _click_stats(self, pos):
        for stat, rect in self._ui_hits['stats']['plus']:
            if self._hit(rect, pos):
                self.send_fn({'type': 'allocate_stat', 'stat': stat})
                return True
        return False

    def _click_craft(self, pos):
        for idx, rect in self._ui_hits['craft']['hammer']:
            if self._hit(rect, pos):
                self.send_fn({'type': 'craft', 'idx': idx})
                return True
        return False

    def _click_shop(self, pos):
        for idx, rect in self._ui_hits['shop']['rows']:
            if self._hit(rect, pos):
                abs_idx = self.shop.page * SHOP_PAGE_SIZE + idx
                if self.shop_submode == 'buy':
                    self.send_fn({'type': 'buy', 'idx': abs_idx})
                else:
                    self.send_fn({'type': 'sell', 'idx': abs_idx})
                return True
        return False

    # ════════════════════════ SNAPSHOT ════════════════════════
    def _apply_player_data(self, p, pd):
        p.x, p.y = pd['x'], pd['y']
        p.hp, p.max_hp = pd['hp'], pd['max_hp']
        p.level = pd['level']
        p.class_name = pd['name']
        p.gold = pd.get('gold', 0)
        p.inventory = dict(pd.get('inventory', {}))
        # anim_time и attack_anim тикаем локально (см. tick)
        p.attack_anim = max(p.attack_anim, pd.get('attack_anim', 0.0))
        p.facing = tuple(pd.get('facing', [1, 0]))
        p.statuses = dict(pd.get('statuses', {}))
        p.atk = pd.get('atk', p.atk)
        p.df = pd.get('df', p.df)
        p.free_points = pd.get('free_points', 0)
        p.xp = pd.get('xp', 0)
        p.xp_next = pd.get('xp_next', 10)
        p.str = pd.get('str', 5)
        p.agi = pd.get('agi', 5)
        p.vit = pd.get('vit', 5)
        p.luk = pd.get('luk', 5)
        p.df_stat = pd.get('df_stat', 5)
        p.weapon = pd.get('weapon')
        p.armor = pd.get('armor')
        return p

    def update_from_snapshot(self, snap, tiles, my_pid):
        if not snap:
            return
        now = time.perf_counter()
        self.my_id = my_pid
        prev_world = (self.world_mode, self.depth)
        self.world_mode = snap.get('world_mode', 'overworld')
        self.depth = snap.get('depth', 0)
        if (self.world_mode, self.depth) != prev_world:
            self.explored = set()

        if tiles:
            self.dungeon.tiles = [[Tile(t) for t in row] for row in tiles]

        seen = set()
        for pid_str, pd in snap.get('players', {}).items():
            pid = int(pid_str)
            seen.add(pid)
            p = self.remote_players.get(pid)
            if p is None:
                p = Player(class_key=pd.get('class', 'warrior'))
            self._apply_player_data(p, pd)
            self.remote_players[pid] = p
            if pid == my_pid:
                self.player = p
            self._track_motion(('p', pid), p, pd['x'], pd['y'], now)
        for stale in [pid for pid in self.remote_players if pid not in seen]:
            self.remote_players.pop(stale, None)
            self._motion.pop(('p', stale), None)

        # Мобы: объекты переиспользуются по стабильному mid, иначе
        # интерполировать движение невозможно.
        monsters = []
        seen_mids = set()
        for md in snap.get('monsters', []):
            mid = md.get('mid', (md['x'], md['y']))
            seen_mids.add(mid)
            m = self._monsters_by_mid.get(mid)
            if m is None:
                m = Entity(md['x'], md['y'], md['hp'], md['max_hp'],
                           0, 0, '', md['ch'], tuple(md['color']), 0,
                           md.get('boss', False))
                m.anim_time = 0.0
                self._monsters_by_mid[mid] = m
            m.hp, m.max_hp = md['hp'], md['max_hp']
            m.ch = md['ch']
            m.color = tuple(md['color'])
            m.boss = md.get('boss', False)
            m.statuses = dict(md.get('statuses', {}))
            m.x, m.y = md['x'], md['y']
            m.attack_anim = max(m.attack_anim, 0.0)
            self._track_motion(('m', mid), m, md['x'], md['y'], now)
            monsters.append(m)
        for mid in list(self._monsters_by_mid):
            if mid not in seen_mids:
                del self._monsters_by_mid[mid]
                self._motion.pop(('m', mid), None)
        self.dungeon.monsters = monsters

        self.dungeon.items = {(d['x'], d['y']): d['key']
                              for d in snap.get('items', [])}
        self.shop_stock = snap.get('shop_stock', [])

        md = snap.get('merchant')
        if md:
            if self.merchant is None:
                self.merchant = Entity(md['x'], md['y'], 1, 1, 0, 0,
                                       "Торговец", "M", (240, 200, 100), 0)
            self.merchant.x, self.merchant.y = md['x'], md['y']
        else:
            self.merchant = None
        ed = snap.get('elder')
        if ed:
            if self.elder is None:
                self.elder = Entity(ed['x'], ed['y'], 1, 1, 0, 0,
                                    "Старейшина", "E", (240, 210, 100), 0)
            self.elder.x, self.elder.y = ed['x'], ed['y']
        else:
            self.elder = None

        # События сервера — в лог боя
        for event in snap.get('events', []):
            self.log(event.get('text', ''),
                     tuple(event.get('color', (225, 225, 235))))

        self.update_fov()
        self._detect_sounds(snap)

    def _detect_sounds(self, snap):
        """Сравнивает с прошлым snapshot и играет звуки при изменениях."""
        from audio import Audio
        prev = self._prev_snapshot
        self._prev_snapshot = snap
        if not prev:
            return

        my = str(self.my_id)
        prev_p = prev.get('players', {}).get(my, {})
        cur_p = snap.get('players', {}).get(my, {})
        if not cur_p or not prev_p:
            return

        # Level up
        if cur_p.get('level', 1) > prev_p.get('level', 1):
            Audio.play('level_up')

        # Свой удар
        prev_atk = prev_p.get('attack_anim', 0)
        cur_atk = cur_p.get('attack_anim', 0)
        if prev_atk <= 0 and cur_atk > 0:
            Audio.play('hit')

        # Получение урона / лечение
        prev_hp = prev_p.get('hp', 0)
        cur_hp = cur_p.get('hp', 0)
        if cur_hp < prev_hp:
            Audio.play('hit')
        elif cur_hp > prev_hp:
            Audio.play('heal')

        # Золото
        if cur_p.get('gold', 0) > prev_p.get('gold', 0):
            Audio.play('pickup')

        # Инвентарь вырос
        prev_inv = prev_p.get('inventory', {})
        cur_inv = cur_p.get('inventory', {})
        if isinstance(prev_inv, dict) and isinstance(cur_inv, dict):
            if sum(cur_inv.values()) > sum(prev_inv.values()):
                Audio.play('pickup')

        # Монстр погиб
        prev_m = len(prev.get('monsters', []))
        cur_m = len(snap.get('monsters', []))
        if cur_m < prev_m:
            Audio.play('death_monster')


class _ShopStub:
    """Локальная часть магазина для MP (сток и покупки — на сервере)."""

    def __init__(self, view):
        self.view = view
        self.page = 0

    def open(self):
        self.view.screen_mode = 'shop'
        self.page = 0

    def pages_count(self):
        view = self.view
        n = (len(view.shop_stock) if view.shop_submode == 'buy'
             else len(view.player.inventory))
        return max(1, (n + SHOP_PAGE_SIZE - 1) // SHOP_PAGE_SIZE)

    def next_page(self):
        self.page = min(self.pages_count() - 1, self.page + 1)

    def prev_page(self):
        self.page = max(0, self.page - 1)


class _QuestStub:
    """Пустой экран квестов для MP."""
    selection = 0

    def visible_quests(self):
        return []

    def button_rects(self):
        return []

    def handle_key(self, key, scancode=0):
        pass

    def handle_mouse_click(self, pos):
        return False

    def open(self):
        pass


class _CraftStub:
    """Заглушка для craft.open() в рендерере."""
    page = 0

    def open(self):
        pass
