"""ClientView — адаптер под Renderer + обработка кликов/клавиш."""
import time

import pygame

from core import config, rules
from core.config import INVENTORY_PAGE_SIZE, MAP_H, MAP_W, SHOP_PAGE_SIZE, Tile
from core.data import ITEMS, QUESTS, parse_item_key
from core.entities import Dungeon, Entity, Message, Player
from core.quests import QuestLog
from game.dialogue_controller import DialogueController

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
        self.dialogue = DialogueController(self)
        self.quest_log = QuestLog()
        self.quests = _MPQuestController(self)
        self.craft = _CraftStub(self)
        # Попап другого игрока: {'pid', 'name'} или None
        self.player_popup = None
        self._player_popup_hits = []
        # Активный обмен (снапшот сервера) или None
        self.trade = None
        # UI
        self._modal_content_rect = (0, 0, 0, 0)
        self._modal_close_rect = None
        self._ui_hits = {
            'inventory': {'items': [], 'equip': [], 'pages': []},
            'stats': {'plus': []},
            'craft': {'hammer': [], 'tabs': [], 'qty': []},
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

    def inv_pages_count(self):
        from core import rules
        n = len(rules.inventory_stacks(self.player.inventory))
        return max(1, (n + INVENTORY_PAGE_SIZE - 1) // INVENTORY_PAGE_SIZE)

    def inv_next_page(self):
        self.inv_page = min(self.inv_pages_count() - 1, self.inv_page + 1)

    def inv_prev_page(self):
        self.inv_page = max(0, self.inv_page - 1)

    def craftable_list(self):
        """Список рецептов — та же логика, что у сервера."""
        from core import rules
        return rules.craftable_list(self.player.inventory, self.player.level,
                                    self.player,
                                    tab=getattr(self.craft, 'tab', 'all'),
                                    unlocked_only=True)

    def craft_page_items(self):
        return self.craft.page_items()

    def craft_pages_count(self):
        return self.craft.pages_count()

    def craft_tabs(self):
        return self.craft.tabs()

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
        # Туман войны убран и в мультиплеере.
        d = self.dungeon
        self.visible = {(x, y) for y in range(d.h) for x in range(d.w)}
        self.explored |= self.visible

    # ════════════════════════ ЧАТ ════════════════════════
    def draw_chat(self, screen):
        """Строка ввода чата внизу экрана."""
        from render.theme import Theme as T
        h = 30
        y = config.SCREEN_H - h - 6
        rect = (8, y, config.SCREEN_W - 16, h)
        T.rounded_rect(screen, T.PANEL_DARK, rect, radius=6,
                       border_color=T.PANEL_BORDER)
        text = f"Чат: {self.chat_buffer}_"
        T.text(screen, text, 20, y + 6, T.TEXT, font=T.FONT_BODY)
        T.text(screen, "Enter — отправить · Esc — отмена",
               config.SCREEN_W - 20, y + 8, T.TEXT_MUTED,
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
    def _npc_adjacent(self, npc):
        return npc is not None and max(abs(self.player.x - npc.x),
                                       abs(self.player.y - npc.y)) <= 1

    def interact_npc(self):
        """Открыть диалог с NPC, стоящим рядом. True — диалог открыт."""
        if self.world_mode == 'overworld' and self._npc_adjacent(self.elder):
            self.dialogue.open('elder')
            # Разговор со старейшиной открывает его квесты всему отряду
            self.send_fn({'type': 'quest_talk'})
            return True
        if self.world_mode == 'dungeon' and self._npc_adjacent(self.merchant):
            self.dialogue.open('merchant')
            return True
        if self._npc_adjacent(self.elder):
            self.dialogue.open('elder')
            return True
        if self._npc_adjacent(self.merchant):
            self.dialogue.open('merchant')
            return True
        return False

    def handle_key(self, key):
        if self.screen_mode == 'dialogue':
            self.dialogue.handle_key(key)
            return
        if self.screen_mode == 'trade':
            if key in (pygame.K_ESCAPE, pygame.K_t):
                self.send_fn({'type': 'trade_cancel'})
                self.screen_mode = 'game'
            elif key in (pygame.K_RETURN, pygame.K_SPACE):
                self.send_fn({'type': 'trade_ready'})
            return
        if self.screen_mode == 'shop':
            self._handle_shop_key(key)
            return
        if self.screen_mode == 'inventory':
            if key == pygame.K_q:
                self.inv_prev_page()
                return
            if key == pygame.K_e:
                self.inv_next_page()
                return
        if self.screen_mode != 'game':
            if key in (pygame.K_ESCAPE, pygame.K_i, pygame.K_c,
                       pygame.K_k, pygame.K_l):
                self.screen_mode = 'game'
            return

        if self.player_popup:
            if key in (pygame.K_ESCAPE, pygame.K_t):
                self.player_popup = None
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
        elif key == pygame.K_e:
            # «E» — добыча ресурса под курсором (или смежного)
            msg = {'type': 'harvest'}
            target = self._map_tile_under(self.mouse_pos)
            if target:
                msg['x'], msg['y'] = target
            self.send_fn(msg)
        elif key in (pygame.K_t, pygame.K_RETURN):
            # Диалог с NPC в MP: старейшина на поверхности, торговец
            # в подземелье. Дальше — выбор реплик/торговли в диалоге.
            if not self.interact_npc():
                self.log("Рядом нет NPC.", (110, 110, 130))
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
            # Попап другого игрока поверх поля
            for action, rect in self._player_popup_hits:
                if self._hit(rect, pos):
                    if action == 'close':
                        self.player_popup = None
                    elif action in ('trade', 'duel'):
                        popup = self.player_popup or {}
                        pid = popup.get('pid')
                        self.player_popup = None
                        if pid is not None:
                            msg_type = ('trade_request' if action == 'trade'
                                        else 'duel_request')
                            self.send_fn({'type': msg_type, 'target': pid})
                    return True
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
            return self._click_map_player(pos)

        # Клики внутри модалок
        if self.screen_mode == 'inventory':
            return self._click_inventory(pos)
        if self.screen_mode == 'stats':
            return self._click_stats(pos)
        if self.screen_mode == 'craft':
            return self._click_craft(pos)
        if self.screen_mode == 'shop':
            return self._click_shop(pos)
        if self.screen_mode == 'trade':
            return self._click_trade(pos)
        if self.screen_mode == 'dialogue':
            return self.dialogue.handle_mouse_click(pos)
        return False

    def _click_map_player(self, pos):
        """Клик по другому игроку на поле — попап «Обмен / Дуэль»."""
        hit = self._map_tile_under(pos)
        if hit is None:
            self.player_popup = None
            return False
        tx, ty = hit
        for pid, p in self.remote_players.items():
            if pid == self.my_id:
                continue
            rx = int(getattr(p, 'render_x', p.x))
            ry = int(getattr(p, 'render_y', p.y))
            if (rx, ry) == (tx, ty):
                self.player_popup = {'pid': pid, 'name': p.class_name}
                return True
        self.player_popup = None
        return False

    def _map_tile_under(self, pos):
        """Клетка карты под курсором или None (вне игрового поля)."""
        from core.config import TILE
        ox, oy = config.view_origin()
        field_w = config.VIEW_W * TILE
        field_h = config.VIEW_H * TILE
        mx, my = pos
        if not (ox <= mx < ox + field_w and oy <= my < oy + field_h):
            return None
        p = self.player
        w_cells = getattr(self.dungeon, 'w', MAP_W)
        h_cells = getattr(self.dungeon, 'h', MAP_H)
        cam_x = max(0, min(int(getattr(p, 'render_x', p.x)) - config.VIEW_W // 2,
                           max(0, w_cells - config.VIEW_W)))
        cam_y = max(0, min(int(getattr(p, 'render_y', p.y)) - config.VIEW_H // 2,
                           max(0, h_cells - config.VIEW_H)))
        return ((mx - ox) // TILE + cam_x, (my - oy) // TILE + cam_y)

    def _click_trade(self, pos):
        for action, rect in self._ui_hits.get('trade', []):
            if not self._hit(rect, pos):
                continue
            if action == 'ready':
                self.send_fn({'type': 'trade_ready'})
            elif action == 'cancel':
                self.send_fn({'type': 'trade_cancel'})
                self.screen_mode = 'game'
            elif action == 'accept':
                self.send_fn({'type': 'trade_accept'})
            elif action == 'decline':
                self.send_fn({'type': 'trade_decline'})
                self.screen_mode = 'game'
            elif action == 'gold+10':
                self.send_fn({'type': 'trade_gold',
                              'amount': self._my_gold_offer() + 10})
            elif action == 'gold+100':
                self.send_fn({'type': 'trade_gold',
                              'amount': self._my_gold_offer() + 100})
            elif action == 'gold-10':
                self.send_fn({'type': 'trade_gold',
                              'amount': self._my_gold_offer() - 10})
            elif action == 'gold_reset':
                self.send_fn({'type': 'trade_gold', 'amount': 0})
            elif isinstance(action, tuple):
                kind, key = action
                if kind == 'add':
                    self.send_fn({'type': 'trade_offer_add', 'key': key,
                                  'count': 1})
                elif kind == 'remove':
                    self.send_fn({'type': 'trade_offer_remove', 'key': key,
                                  'count': 1})
            return True
        return False

    def _my_gold_offer(self):
        trade = self.trade or {}
        gold = trade.get('gold', {})
        return gold.get(str(self.my_id), 0)

    def _hit(self, rect, pos):
        x, y, w, h = rect
        return x <= pos[0] <= x + w and y <= pos[1] <= y + h

    def _click_inventory(self, pos):
        for action, rect in self._ui_hits['inventory'].get('pages', []):
            if self._hit(rect, pos):
                if action == 'prev':
                    self.inv_prev_page()
                else:
                    self.inv_next_page()
                return True
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
        for tab, rect in self._ui_hits['craft'].get('tabs', []):
            if self._hit(rect, pos):
                self.craft.set_tab(tab)
                return True
        if self._hit(self._ui_hits['craft'].get('prev', (0, 0, 0, 0)), pos):
            self.craft.prev_page()
            return True
        if self._hit(self._ui_hits['craft'].get('next', (0, 0, 0, 0)), pos):
            self.craft.next_page()
            return True
        for key, minus_rect, plus_rect, item in \
                self._ui_hits['craft'].get('qty', []):
            if self._hit(minus_rect, pos):
                self.craft.change_quantity_for(key, -1, item)
                return True
            if self._hit(plus_rect, pos):
                self.craft.change_quantity_for(key, 1, item)
                return True
        entries = self.craftable_list()
        for idx, rect in self._ui_hits['craft']['hammer']:
            if self._hit(rect, pos):
                if 0 <= idx < len(entries):
                    key, item, _recipe, _can, _cat, recipe_index, _n = entries[idx]
                    # Сервер крафтит по одному предмету — повторяем запрос
                    # столько раз, сколько задано в партии.
                    times = self.craft.quantity_for(key, item)
                    for _ in range(times):
                        self.send_fn({'type': 'craft', 'key': key,
                                      'recipe': recipe_index})
                else:
                    self.send_fn({'type': 'craft', 'idx': idx})
                return True
        return False

    def _click_shop(self, pos):
        mode_rect = self._ui_hits['shop'].get('mode')
        if mode_rect and self._hit(mode_rect, pos):
            self.shop_submode = 'sell' if self.shop_submode == 'buy' else 'buy'
            self.shop.page = 0
            return True
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
        p.mana = pd.get('mana', getattr(p, 'mana', 0))
        p.max_mana = pd.get('max_mana', getattr(p, 'max_mana', 0))
        p.level = pd['level']
        p.class_name = pd['name']
        p.class_key = pd.get('class', getattr(p, 'class_key', 'warrior'))
        p.class_tier = pd.get('class_tier', getattr(p, 'class_tier', 1))
        p.gold = pd.get('gold', 0)
        # Сервер присылает инвентарь только при изменении — иначе
        # сохраняем уже известный, а не обнуляем его.
        if 'inventory' in pd:
            p.inventory = dict(pd['inventory'])
        # anim_time и attack_anim тикаем локально (см. tick)
        p.attack_anim = max(p.attack_anim, pd.get('attack_anim', 0.0))
        p.facing = tuple(pd.get('facing', [1, 0]))
        p.statuses = dict(pd.get('statuses', {}))
        p.atk = pd.get('atk', p.atk)
        p.df = pd.get('df', p.df)
        p.free_points = pd.get('free_points', 0)
        p.skill_points = pd.get('skill_points', getattr(p, 'skill_points', 0))
        if 'skills' in pd:
            p.skills = dict(pd['skills'])
        p.xp = pd.get('xp', 0)
        p.xp_next = pd.get('xp_next', 10)
        p.str = pd.get('str', 5)
        p.agi = pd.get('agi', 5)
        p.vit = pd.get('vit', 5)
        p.luk = pd.get('luk', 5)
        p.df_stat = pd.get('df_stat', 5)
        p.int_stat = pd.get('int', 5)
        p.weapon = pd.get('weapon')
        p.armor = pd.get('armor')
        p.head = pd.get('head', pd.get('helmet'))
        p.legs = pd.get('legs', pd.get('leggings'))
        p.boots = pd.get('boots')
        if 'skills' in pd:
            from core import skills as sk
            from core.data import default_skills
            p.skills = default_skills()
            for skill, val in (pd.get('skills') or {}).items():
                skill = sk.canon_skill(skill)
                if skill in p.skills:
                    if isinstance(val, dict):
                        p.skills[skill]['level'] = max(1, int(val.get('level', 1)))
                        p.skills[skill]['xp'] = max(0, int(val.get('xp', 0)))
                    else:
                        p.skills[skill]['level'] = max(1, int(val))
            p.skill_xp = {k: v['xp'] for k, v in p.skills.items()}
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

        # Общие квесты отряда
        if 'quests' in snap:
            self.quest_log = QuestLog.from_dict(snap['quests'] or {})

        # Ресурсы (кусты/деревья/руда): обновляем только изменившиеся
        if 'harvestables' in snap:
            self.dungeon.harvestables = {
                (int(k.split(',')[0]), int(k.split(',')[1])):
                    {'kind': v.get('kind'), 'has': v.get('has', True),
                     'hp': v.get('hp', 0), 'max_hp': v.get('max_hp', 0)}
                for k, v in (snap['harvestables'] or {}).items()
            }

        # Обмен между игроками
        self._apply_trade(snap.get('trade'))

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

    def _apply_trade(self, trade):
        """Синхронизирует окно обмена с сервером."""
        self.trade = trade
        my = str(self.my_id)
        if trade and my in trade.get('players', []):
            if self.screen_mode not in ('trade', 'dialogue'):
                self.screen_mode = 'trade'
        elif self.screen_mode == 'trade' and not trade:
            self.screen_mode = 'game'

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

    def open(self, npc_type=None, submode='buy'):
        self.view.screen_mode = 'shop'
        self.view.shop_submode = submode if submode in ('buy', 'sell') else 'buy'
        self.page = 0

    def pages_count(self):
        view = self.view
        n = (len(view.shop_stock) if view.shop_submode == 'buy'
             else len(rules.inventory_stacks(view.player.inventory)))
        return max(1, (n + SHOP_PAGE_SIZE - 1) // SHOP_PAGE_SIZE)

    def next_page(self):
        self.page = min(self.pages_count() - 1, self.page + 1)

    def prev_page(self):
        self.page = max(0, self.page - 1)


class _MPQuestController:
    """Экран квестов в MP: журнал общий (сервер), награды персональные.

    Взять/сдать можно только у старейшины — сервер проверяет близость,
    клиент лишь отправляет запрос.
    """
    def __init__(self, view):
        self.view = view
        self.selection = 0

    def open(self):
        self.selection = 0
        self.view.screen_mode = 'quests'

    def visible_quests(self):
        ql = self.view.quest_log
        result = []
        for key in QUESTS:
            if key in ql.turned_in:
                continue
            if key in ql.available:
                state = 'available'
            elif key in ql.active:
                state = 'active'
            elif key in ql.completed:
                state = 'completed'
            else:
                continue
            result.append((key, state))
        return result

    def activate(self, idx):
        items = self.visible_quests()
        if idx < 0 or idx >= len(items):
            return
        key, state = items[idx]
        q = QUESTS.get(key, {})
        if state == 'available':
            self.view.send_fn({'type': 'quest_accept', 'key': key})
            self.view.log(f"Запрос квеста: {q.get('name', key)}",
                          (240, 220, 120))
        elif state == 'completed':
            self.view.send_fn({'type': 'quest_turn_in', 'key': key})
        self.selection = min(self.selection, max(0, len(items) - 1))

    def handle_key(self, key, scancode=0):
        v = self.view
        if key in (pygame.K_ESCAPE, pygame.K_l):
            v.screen_mode = 'game'
            return
        items = self.visible_quests()
        n = len(items)
        if n == 0:
            return
        if key in (pygame.K_UP, pygame.K_w):
            self.selection = (self.selection - 1) % n
        elif key in (pygame.K_DOWN, pygame.K_s):
            self.selection = (self.selection + 1) % n
        elif key in (pygame.K_RETURN, pygame.K_SPACE):
            self.activate(self.selection)

    def button_rects(self):
        from core.config import MODAL_CONTENT_PAD, MODAL_MARGIN_X, MODAL_TOP_PAD
        panel_x = MODAL_MARGIN_X
        panel_w = config.SCREEN_W - MODAL_MARGIN_X * 2
        bw = panel_w - MODAL_CONTENT_PAD * 2
        bh = 76
        bx = panel_x + MODAL_CONTENT_PAD
        by = MODAL_TOP_PAD + 56 + 32 + 24
        return [(i, (bx, by + i * (bh + 8), bw, bh))
                for i in range(len(self.visible_quests()))]

    def handle_mouse_click(self, pos):
        for idx, rect in self.button_rects():
            x, y, w, h = rect
            if x <= pos[0] <= x + w and y <= pos[1] <= y + h:
                self.selection = idx
                self.activate(idx)
                return True
        return False


class _CraftStub:
    """Состояние вкладок/страниц крафта для MP-клиента.

    Сами рецепты считает ``rules`` на основе снапшота игрока — так клиент
    показывает то же, что и авторитетный сервер.
    """
    def __init__(self, view=None):
        self.view = view
        self.tab = 'all'
        self.page = 0
        # Полей мини-игры у MP-клиента нет: сервер считает качество сам.
        # Атрибуты обязательны — общий ScreensRenderer.draw_craft их читает.
        self.minigame_active = False
        self.minigame = None
        # Партия для каждого предмета (как в офлайн-крафте).
        self.quantities = {}
        self.quantity = 1

    @staticmethod
    def tabs():
        from core.data import CRAFT_TABS
        return CRAFT_TABS

    def list(self):
        from core import rules
        if self.view is None:
            return []
        p = self.view.player
        return rules.craftable_list(p.inventory, p.level, tab=self.tab)

    def set_tab(self, tab):
        if tab in [key for key, _ in self.tabs()]:
            self.tab = tab
            self.page = 0

    def pages_count(self):
        from core.config import CRAFT_PAGE_SIZE
        total = len(self.list())
        return max(1, (total + CRAFT_PAGE_SIZE - 1) // CRAFT_PAGE_SIZE)

    def page_offset(self):
        from core.config import CRAFT_PAGE_SIZE
        return self.page * CRAFT_PAGE_SIZE

    def page_items(self):
        from core.config import CRAFT_PAGE_SIZE
        start = self.page_offset()
        return self.list()[start:start + CRAFT_PAGE_SIZE]

    def prev_page(self):
        self.page = max(0, self.page - 1)

    def next_page(self):
        self.page = min(self.pages_count() - 1, self.page + 1)

    def quantity_for(self, key, item=None):
        from core import rules
        if item and item.get('type') in rules.EQUIP_TYPES:
            return 1
        return max(1, min(9, int(self.quantities.get(key, 1))))

    def change_quantity_for(self, key, delta, item=None):
        from core import rules
        if item and item.get('type') in rules.EQUIP_TYPES:
            self.quantities[key] = 1
            return 1
        value = max(1, min(9, self.quantity_for(key, item) + int(delta)))
        self.quantities[key] = value
        return value

    def change_quantity(self, delta):
        self.quantity = max(1, min(9, self.quantity + int(delta)))

    def handle_click(self, pos):
        # Мини-игры в MP нет — клик по полю ничего не делает.
        return False

    def open(self):
        # Экран открывает ClientView через screen_mode.
        return None
