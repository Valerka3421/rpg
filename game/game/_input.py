"""Маршрутизация ввода: handle_key, handle_mouse_*, _handle_*_click, handle_input_continuous.
"""
from game.game._common import *  # noqa: F401,F403 — все импорты


class InputMixin:
    """Маршрутизация ввода: handle_key, handle_mouse_*, _handle_*_click, handle_input_continuous."""

    def handle_key(self, key, scancode=0, unicode='', mods=0):
        # Консоль разработчика — раньше любых экранов, чтобы её нельзя было
        # «застрять» в модалке. F12 переключает, ` открывает.
        if key == pygame.K_F12:
            if self.screen_mode == 'dev_console':
                self.dev_console.close()
            else:
                self.dev_console.open()
            return
        if key == pygame.K_BACKQUOTE and self.screen_mode != 'dev_console':
            self.dev_console.open()
            return
        if self.screen_mode == 'dev_console':
            self.dev_console.handle_key(key, unicode, mods)
            return

        # Меню и все его подэкраны
        if self.screen_mode in ('menu', 'menu_saves', 'menu_settings',
                                'class_select', 'menu_controls',
                                'menu_credits', 'menu_multiplayer',
                                'menu_mp_join'):
            self.menu.handle_key(key, scancode, unicode, mods)
            return
        if self.screen_mode == 'shop':
            self.shop.handle_key(key); return
        if self.screen_mode == 'craft':
            self.craft.handle_key(key); return
        if self.screen_mode == 'skills':
            self.skills_ui.handle_key(key, scancode); return
        if self.screen_mode == 'dialogue':
            self.dialogue.handle_key(key, scancode)
            return
        if self.screen_mode == 'quests':
            self.quests.handle_key(key, scancode)
            return
        if self.screen_mode == 'stats_run':
            self.stats_run.handle_key(key, scancode)
            return

        # Управление в игре
        if key == pygame.K_F9:
            self.load(); return
        if key == pygame.K_F5 and not (self.dead or self.victory):
            self.save(); return
        if self.dead or self.victory:
            if key == pygame.K_r:
                self.new_game()
            return

        # Скролл лога — только в игровом режиме
        if self.screen_mode == 'game':
            if scancode == 0x2F or key in (pygame.K_PAGEUP, pygame.K_LEFTBRACKET):
                self.scroll_log(3)
                return
            if scancode == 0x30 or key in (pygame.K_PAGEDOWN, pygame.K_RIGHTBRACKET):
                self.scroll_log(-3)
                return
            if key == pygame.K_HOME:
                self.scroll_log(10_000)
                return
            if key == pygame.K_END:
                self.log_scroll = 0
                return

        # Экраны статов / инвентаря
        if self.screen_mode == 'stats':
            if key in (pygame.K_c, pygame.K_ESCAPE):
                self.screen_mode = 'game'
            elif key == pygame.K_i:
                self.screen_mode = 'inventory'
            elif pygame.K_1 <= key <= pygame.K_6:
                self.allocate_stat(STAT_ORDER[key - pygame.K_1])
            return
        if self.screen_mode == 'inventory':
            if key in (pygame.K_i, pygame.K_ESCAPE):
                self.screen_mode = 'game'
            elif key == pygame.K_c:
                self.screen_mode = 'stats'
            elif key == pygame.K_u:
                self.unequip_weapon()
            elif key == pygame.K_v:
                self.unequip_armor()
            elif key == pygame.K_h:
                self.unequip_head()
            elif key == pygame.K_b:
                self.unequip_legs()
            elif key == pygame.K_n:
                self.unequip_boots()
            elif key == pygame.K_q:
                self.inv_prev_page()
            elif key == pygame.K_e:
                self.inv_next_page()
            elif pygame.K_1 <= key <= pygame.K_9:
                if (mods & pygame.KMOD_SHIFT) and key <= pygame.K_8:
                    self.assign_hotbar(key - pygame.K_1)
                else:
                    self.use_item_by_index(key - pygame.K_1)
            return

        # Быстрая панель: 1–8 используют назначенные предметы.
        if pygame.K_1 <= key <= pygame.K_8:
            self.use_hotbar(key - pygame.K_1)
            return

        # Активные навыки класса: F1..F10 кастуют.
        if pygame.K_F1 <= key <= pygame.K_F10:
            slot = key - pygame.K_F1
            self.cast_active_skill(slot)
            return

        # Открытие экранов
        if key == pygame.K_c:
            self.screen_mode = 'stats'; return
        if key == pygame.K_i:
            self.screen_mode = 'inventory'
            self.inv_page = min(self.inv_page, self.inv_pages_count() - 1)
            return
        if key == pygame.K_t:
            if (self.quest_board
                    and (self.world_mode == 'dungeon'
                         or self.location == 'village')
                    and max(abs(self.player.x - self.quest_board.x),
                            abs(self.player.y - self.quest_board.y)) <= 1):
                self.dialogue.open('quest_board')
                return
            if (self.world_mode == 'overworld' and self.location == 'village'
                    and self.elder
                    and max(abs(self.player.x - self.elder.x),
                            abs(self.player.y - self.elder.y)) <= 1):
                self.dialogue.open('elder')
                return
            city_npc = self.nearby_city_npc()
            if city_npc is not None:
                self.dialogue.open(city_npc.npc_type)
                return
            if (self.world_mode == 'dungeon' and self.merchant
                    and max(abs(self.player.x - self.merchant.x),
                            abs(self.player.y - self.merchant.y)) <= 1):
                self.dialogue.open('merchant')
                return
            self.log("Рядом нет NPC.", COL_DIM)
            return
        if key == pygame.K_e:
            self.harvest_adjacent()
            self.update_fov()
            return
        if key == pygame.K_l:
            self.quests.open()
            return
        if key == pygame.K_k:
            self.craft.open(); return
        if key == pygame.K_n:
            self.skills_ui.open(); return
        if key == pygame.K_h:
            self.quick_potion()
            self.update_fov()
            return
        if key == pygame.K_m:
            self.settings['show_minimap'] = not self.settings.get('show_minimap', True)
            save_settings(self.settings)
            return
        # B в game-режиме открывает большую карту; в bigmap — закрывает.
        if key == pygame.K_b:
            if self.screen_mode == 'game':
                self.screen_mode = 'bigmap'
                return
            if self.screen_mode == 'bigmap':
                self.screen_mode = 'game'
                return
        if key == pygame.K_TAB:
            self.stats_run.open()
            return

        # Bigmap закрывается по Esc и по любой необработанной клавише.
        if self.screen_mode == 'bigmap' and key == pygame.K_ESCAPE:
            self.screen_mode = 'game'
            return


    def handle_mouse_motion(self, pos):
        self.mouse_pos = pos
        if self.screen_mode in ('menu', 'menu_saves', 'menu_settings',
                                'class_select', 'menu_controls',
                                'menu_credits', 'menu_multiplayer',
                                'menu_mp_join'):
            self.menu.handle_mouse_motion(pos)


    def handle_mouse_click(self, pos):
        # Меню
        if self.screen_mode in ('menu', 'menu_saves', 'menu_settings',
                                'class_select', 'menu_controls',
                                'menu_credits', 'menu_multiplayer',
                                'menu_mp_join'):
            return self.menu.handle_mouse_click(pos)

        # Крестик — ПЕРВЫМ, до любых других обработчиков
        close = getattr(self, '_modal_close_rect', None)
        if close and self.screen_mode == 'dev_console':
            if self._hit(close, pos):
                self.dev_console.close()
                return True
        if close and self.screen_mode in ('stats', 'inventory', 'craft',
                                          'quests', 'shop', 'stats_run',
                                          'dialogue', 'skills'):
            if self._hit(close, pos):
                self.screen_mode = 'game'
                return True

        # Диалог — после крестика
        if self.screen_mode == 'dialogue':
            return self.dialogue.handle_mouse_click(pos)

        if self.screen_mode == 'inventory':
            return self._handle_inventory_click(pos)
        if self.screen_mode == 'stats':
            return self._handle_stats_click(pos)
        if self.screen_mode == 'craft':
            return self._handle_craft_click(pos)
        if self.screen_mode == 'skills':
            return self._handle_skills_click(pos)
        if self.screen_mode == 'shop':
            return self._handle_shop_click(pos)
        if self.screen_mode == 'quests':
            return self.quests.handle_mouse_click(pos)
        if self.screen_mode != 'game':
            return False

        mx, my = pos

        # Панель иконок сверху
        for name, rect in self._toolbar_rects():
            x, y, w, h = rect
            if x <= mx <= x + w and y <= my <= y + h:
                if name == 'inventory':
                    self.screen_mode = 'inventory'
                    self.inv_page = min(self.inv_page, self.inv_pages_count() - 1)
                elif name == 'craft':
                    self.craft.open()
                elif name == 'skills':
                    self.skills_ui.open()
                elif name == 'stats':
                    self.screen_mode = 'stats'
                elif name == 'quests':
                    self.quests.open()
                return True

        # Клик по NPC на карте
        from core.config import MAP_H, MAP_W, TILE
        cam_x = max(0, min(self.player.x - config.VIEW_W // 2, MAP_W - config.VIEW_W))
        cam_y = max(0, min(self.player.y - config.VIEW_H // 2, MAP_H - config.VIEW_H))
        ox, oy = config.view_origin()
        field_w = config.VIEW_W * TILE
        field_h = config.VIEW_H * TILE
        if not (ox <= mx < ox + field_w and oy <= my < oy + field_h):
            return False
        tx = (mx - ox) // TILE + cam_x
        ty = (my - oy) // TILE + cam_y

        if (self.world_mode == 'overworld' and self.location == 'village'
                and self.elder
                and (tx, ty) == (self.elder.x, self.elder.y)
                and (tx, ty) in self.visible):
            if max(abs(self.player.x - tx), abs(self.player.y - ty)) <= 1:
                self.dialogue.open('elder')
            else:
                self.log("Подойди ближе.", COL_DIM)
            return True
        if (self.world_mode == 'overworld' and self.location == 'village'):
            clicked = next((n for n in getattr(self, 'city_npcs', ())
                            if (n.x, n.y) == (tx, ty)), None)
            if clicked is not None and (tx, ty) in self.visible:
                if max(abs(self.player.x - tx), abs(self.player.y - ty)) <= 1:
                    self.dialogue.open(clicked.npc_type)
                else:
                    self.log("Подойди ближе.", COL_DIM)
                return True
        if (self.world_mode == 'dungeon' and self.merchant
                and (tx, ty) == (self.merchant.x, self.merchant.y)
                and (tx, ty) in self.visible):
            if max(abs(self.player.x - tx), abs(self.player.y - ty)) <= 1:
                self.dialogue.open('merchant')
            else:
                self.log("Подойди ближе.", COL_DIM)
            return True

        # Клик по доске поручений открывает её.
        if (self.quest_board
                and (tx, ty) == (self.quest_board.x, self.quest_board.y)
                and (tx, ty) in self.visible):
            if max(abs(self.player.x - tx), abs(self.player.y - ty)) <= 1:
                self.dialogue.open('quest_board')
            else:
                self.log("Подойди ближе.", COL_DIM)
            return True

        return False


    def _hit(self, rect, pos):
        x, y, w, h = rect
        return x <= pos[0] <= x + w and y <= pos[1] <= y + h


    def _handle_inventory_click(self, pos):
        hits = self._ui_hits.get('inventory', {})
        for action, rect in hits.get('pages', []):
            if self._hit(rect, pos):
                if action == 'prev':
                    self.inv_prev_page()
                else:
                    self.inv_next_page()
                return True
        for slot, rect in hits.get('equip', []):
            if self._hit(rect, pos):
                self.unequip_slot(slot)
                return True
        for key, rect in hits.get('items', []):
            if self._hit(rect, pos):
                self.use_item(key)
                self.inv_page = min(self.inv_page, self.inv_pages_count() - 1)
                return True
        return False


    def _handle_stats_click(self, pos):
        hits = self._ui_hits.get('stats', {})
        for stat, rect in hits.get('plus', []):
            if self._hit(rect, pos):
                self.allocate_stat(stat)
                return True
        return False


    def _handle_craft_click(self, pos):
        hits = self._ui_hits.get('craft', {})
        if self.craft.minigame_active:
            return self.craft.handle_click(pos)
        for tab, rect in hits.get('tabs', []):
            if self._hit(rect, pos):
                self.craft.set_tab(tab)
                return True
        if self._hit(hits.get('prev', (0, 0, 0, 0)), pos):
            self.craft.prev_page()
            return True
        if self._hit(hits.get('next', (0, 0, 0, 0)), pos):
            self.craft.next_page()
            return True
        if self._hit(hits.get('qty_minus', (0, 0, 0, 0)), pos):
            self.craft.change_quantity(-1)
            return True
        if self._hit(hits.get('qty_plus', (0, 0, 0, 0)), pos):
            self.craft.change_quantity(1)
            return True
        for idx, rect in hits.get('hammer', []):
            if self._hit(rect, pos):
                self.craft.craft(idx)
                return True
        return False


    def _handle_skills_click(self, pos):
        hits = self._ui_hits.get('skills', {})
        for tab, rect in hits.get('tabs', []):
            if self._hit(rect, pos):
                self.skills_ui.set_tab(tab)
                return True
        for idx, rect in hits.get('rows', []):
            if self._hit(rect, pos):
                if self.skills_ui.selection == idx:
                    self.skills_ui.activate()
                else:
                    self.skills_ui.selection = idx
                return True
        for idx, rect in hits.get('promotion', []):
            if self._hit(rect, pos):
                self.skills_ui.change_class(idx)
                return True
        return False


    def _handle_shop_click(self, pos):
        """Клик по строке товара/предмета в магазине."""
        hits = self._ui_hits.get('shop', {})
        for idx, rect in hits.get('rows', []):
            if self._hit(rect, pos):
                if self.shop_submode == 'buy':
                    self.buy_item(idx)
                else:
                    self.sell_item(idx)
                return True
        return False


    def _toolbar_rects(self):
        from core.config import TOOLBAR_BTN, TOOLBAR_GAP, TOOLBAR_MARGIN
        result = []
        for i, name in enumerate(('inventory', 'craft', 'skills',
                                  'stats', 'quests')):
            x = TOOLBAR_MARGIN + i * (TOOLBAR_BTN + TOOLBAR_GAP)
            y = TOOLBAR_MARGIN
            result.append((name, (x, y, TOOLBAR_BTN, TOOLBAR_BTN)))
        return result


    def handle_input_continuous(self, keys):
        """Вызывается каждый кадр из main. Движение и атака по зажатым клавишам."""
        if self.dead or self.victory or self.screen_mode != 'game':
            return
        if self.player_move_timer > 0 and self.player_attack_timer > 0:
            return

        # ── Движение (приоритет: вертикаль, потом горизонталь) ──
        if self.player_move_timer <= 0:
            dx = dy = 0
            if keys[pygame.K_w] or keys[pygame.K_UP]:
                dy = -1
            elif keys[pygame.K_s] or keys[pygame.K_DOWN]:
                dy = 1
            elif keys[pygame.K_a] or keys[pygame.K_LEFT]:
                dx = -1
            elif keys[pygame.K_d] or keys[pygame.K_RIGHT]:
                dx = 1
            if dx or dy:
                if self.move_player(dx, dy):
                    self.player_move_timer = self.player.move_cooldown
                    self.update_fov()

        # ── Атака пробелом ──
        if keys[pygame.K_SPACE] and self.player_attack_timer <= 0:
            self.player_attack()
            self.player_attack_timer = self.player.attack_cooldown


