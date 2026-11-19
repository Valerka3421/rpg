"""Экран древа навыков: draw_skills, class/craft trees, tooltips, tabs.
"""
from render.screens_renderer._common import *  # noqa: F401,F403


class SkillsMixin:
    """Экран древа навыков: draw_skills, class/craft trees, tooltips, tabs."""

    def draw_skills(self, game):
        # Древо живёт в собственном крупном окне, которое можно двигать
        # мышью за заголовок. Обычная модалка тут не используется.
        ctrl = game.skills_ui
        panel = ctrl.panel_rect()
        self._modal_bg(game, panel)
        cx0, cy0, cw, ch = game._modal_content_rect
        from core import skills as sk
        from core.data import class_data
        p = game.player
        game._ui_hits['skills'] = {
            'tabs': [], 'rows': [], 'promotion': [],
            # Вся верхняя полоса с заголовком — «ручка» перетаскивания.
            'drag': (panel[0], panel[1], panel[2], 96),
        }

        # Ручка-«гриф» по центру заголовка: подсказывает, что окно можно
        # тащить. Крестик обрабатывается раньше и остаётся кликабельным.
        gx = panel[0] + panel[2] // 2 - 14
        gy = panel[1] + 16
        for row in range(2):
            for col in range(3):
                pygame.draw.circle(self.screen, (126, 108, 76),
                                   (gx + col * 8, gy + row * 7), 2)

        cls = class_data(p.class_key)
        self.text("ДРЕВО НАВЫКОВ", cx0, cy0, (214, 178, 98), font=T.FONT_H1)
        self.text(f"{p.class_name} · Уровень {p.level}",
                  self._modal_right(game), cy0 + 8, T.TEXT, align_right=True)
        if p.skill_points:
            T.badge(self.screen, cx0, cy0 + 34, f"● {p.skill_points} очков навыков",
                    color=T.GOLD, bg=(50, 40, 15), font=T.FONT_SMALL)

        y = cy0 + 66
        y, tabs = self._draw_tabs(game, sk.TABS, ctrl.tab, cx0, y, cw,
                                  'skills')
        game._ui_hits['skills']['tabs'] = tabs

        if ctrl.tab == sk.TAB_CLASS:
            self._draw_class_tab(game, cx0, y, cw, ch, cls, ctrl,
                                 sk, class_data)
        else:
            self._draw_craft_tab(game, cx0, y, cw, ch, ctrl, sk)

    # ── Графическая ветка класса ──
    # Цвет значка по главному эффекту узла — как цветные иконки на референсе.
    _GLYPH_COLORS = {
        'atk': (230, 124, 88), 'crit': (238, 192, 92),
        'hp': (214, 98, 98), 'df': (148, 176, 204),
        'dodge': (166, 202, 176), 'mana': (108, 154, 232),
        'lifesteal': (204, 92, 124), 'attack_speed': (242, 212, 108),
        'move_speed': (146, 208, 188), 'gold_pct': (238, 196, 88),
        'xp_pct': (148, 196, 242), 'craft_xp': (208, 170, 116),
        'craft_quality': (224, 188, 126), 'quest_gold': (228, 200, 128),
        'quest_xp': (168, 200, 236), 'summon': (188, 166, 222),
        'rage': (230, 110, 92), 'frenzy': (232, 136, 96),
    }


    @staticmethod
    def _hex_points(cx, cy, r):
        """Точки шестиугольника «вершиной вверх»."""
        return [(cx + r * math.cos(math.radians(60 * i - 90)),
                 cy + r * math.sin(math.radians(60 * i - 90)))
                for i in range(6)]


    def _dotted_line(self, a, b, color, width=2, dash=6, gap=5):
        ax, ay = a
        bx, by = b
        dx, dy = bx - ax, by - ay
        dist = math.hypot(dx, dy)
        if dist <= 0.1:
            return
        ux, uy = dx / dist, dy / dist
        pos = 0.0
        while pos < dist:
            end = min(pos + dash, dist)
            pygame.draw.line(self.screen, color,
                             (ax + ux * pos, ay + uy * pos),
                             (ax + ux * end, ay + uy * end), width)
            pos = end + gap


    def _draw_skill_glyph(self, effects, cx, cy, r, color, strength=1.0):
        """Процедурный значок узла по его главному эффекту."""
        key = self._primary_effect(effects)
        color = T.shade(self._GLYPH_COLORS.get(key, color), strength)
        s = max(0.55, r / 26.0)
        w = max(1, int(round(2 * s)))
        if key == 'atk':
            pygame.draw.line(self.screen, color, (cx, cy - 11 * s), (cx, cy + 5 * s), w)
            pygame.draw.line(self.screen, color, (cx - 7 * s, cy + 2 * s),
                             (cx + 7 * s, cy + 2 * s), w)
            pygame.draw.line(self.screen, color, (cx, cy + 5 * s), (cx, cy + 10 * s), w)
            pygame.draw.circle(self.screen, color, (int(cx), int(cy + 11 * s)), max(1, int(2 * s)))
        elif key == 'crit':
            pygame.draw.circle(self.screen, color, (int(cx), int(cy)), int(9 * s), w)
            pygame.draw.line(self.screen, color, (cx - 12 * s, cy), (cx + 12 * s, cy), w)
            pygame.draw.line(self.screen, color, (cx, cy - 12 * s), (cx, cy + 12 * s), w)
            pygame.draw.circle(self.screen, color, (int(cx), int(cy)), max(1, int(2 * s)))
        elif key in ('lifesteal', 'mana'):
            pygame.draw.polygon(self.screen, color, [
                (cx, cy - 11 * s), (cx + 8 * s, cy + 1 * s),
                (cx + 4 * s, cy + 9 * s), (cx - 4 * s, cy + 9 * s),
                (cx - 8 * s, cy + 1 * s)])
            if key == 'lifesteal':
                pygame.draw.line(self.screen, (24, 20, 18),
                                 (cx - 3 * s, cy + 2 * s), (cx + 3 * s, cy + 2 * s), w)
        elif key == 'attack_speed':
            pygame.draw.polygon(self.screen, color, [
                (cx + 3 * s, cy - 11 * s), (cx - 7 * s, cy + 1 * s),
                (cx - 1 * s, cy + 1 * s), (cx - 3 * s, cy + 11 * s),
                (cx + 8 * s, cy - 2 * s), (cx + 2 * s, cy - 2 * s)])
        elif key == 'hp':
            rr = max(2, int(5 * s))
            pygame.draw.circle(self.screen, color, (int(cx - 4 * s), int(cy - 3 * s)), rr)
            pygame.draw.circle(self.screen, color, (int(cx + 4 * s), int(cy - 3 * s)), rr)
            pygame.draw.polygon(self.screen, color, [
                (cx - 9 * s, cy - 1 * s), (cx + 9 * s, cy - 1 * s),
                (cx, cy + 10 * s)])
        elif key == 'df':
            shield = [(cx, cy - 11 * s), (cx + 9 * s, cy - 6 * s),
                      (cx + 7 * s, cy + 5 * s), (cx, cy + 11 * s),
                      (cx - 7 * s, cy + 5 * s), (cx - 9 * s, cy - 6 * s)]
            pygame.draw.polygon(self.screen, color, shield, w)
            pygame.draw.line(self.screen, color, (cx, cy - 7 * s), (cx, cy + 7 * s), 1)
        elif key == 'dodge':
            rect = pygame.Rect(int(cx - 10 * s), int(cy - 10 * s),
                               int(20 * s), int(20 * s))
            pygame.draw.arc(self.screen, color, rect,
                            math.radians(25), math.radians(300), w)
            pygame.draw.circle(self.screen, color, (int(cx), int(cy)),
                               max(1, int(2.5 * s)))
        elif key == 'move_speed':
            for off in (-4, 5):
                pygame.draw.lines(self.screen, color, False, [
                    (cx + off * s - 4 * s, cy - 7 * s),
                    (cx + off * s + 3 * s, cy),
                    (cx + off * s - 4 * s, cy + 7 * s)], w)
        elif key == 'gold_pct':
            pygame.draw.circle(self.screen, color, (int(cx), int(cy)), int(10 * s), w)
            pygame.draw.circle(self.screen, color, (int(cx), int(cy)), int(4 * s), w)
            pygame.draw.line(self.screen, color, (cx, cy - 10 * s), (cx, cy - 13 * s), w)
        elif key == 'xp_pct':
            pts = []
            for i in range(10):
                ang = math.radians(-90 + i * 36)
                rad = r * (0.72 if i % 2 == 0 else 0.3)
                pts.append((cx + rad * math.cos(ang), cy + rad * math.sin(ang)))
            pygame.draw.polygon(self.screen, color, pts)
        elif key in ('craft_xp', 'craft_quality'):
            pygame.draw.polygon(self.screen, color, [
                (cx - 11 * s, cy - 5 * s), (cx + 11 * s, cy - 5 * s),
                (cx + 6 * s, cy + 1 * s), (cx + 3 * s, cy + 1 * s),
                (cx + 4 * s, cy + 6 * s), (cx + 8 * s, cy + 10 * s),
                (cx - 8 * s, cy + 10 * s), (cx - 4 * s, cy + 6 * s),
                (cx - 3 * s, cy + 1 * s), (cx - 6 * s, cy + 1 * s)])
        elif key in ('quest_gold', 'quest_xp'):
            pygame.draw.rect(self.screen, color, (int(cx - 8 * s), int(cy - 9 * s),
                                                  int(16 * s), int(18 * s)), w)
            pygame.draw.line(self.screen, color, (cx - 4 * s, cy - 3 * s),
                             (cx + 4 * s, cy - 3 * s), 1)
            pygame.draw.line(self.screen, color, (cx - 4 * s, cy + 2 * s),
                             (cx + 4 * s, cy + 2 * s), 1)
        elif key == 'summon':
            pygame.draw.circle(self.screen, color, (int(cx), int(cy - 2 * s)), int(8 * s), w)
            pygame.draw.rect(self.screen, color, (int(cx - 4 * s), int(cy + 4 * s),
                                                  int(8 * s), int(5 * s)), w)
            pygame.draw.circle(self.screen, color, (int(cx - 3 * s), int(cy - 3 * s)),
                               max(1, int(2 * s)))
            pygame.draw.circle(self.screen, color, (int(cx + 3 * s), int(cy - 3 * s)),
                               max(1, int(2 * s)))
        else:
            pygame.draw.polygon(self.screen, color, [
                (cx, cy - 11 * s), (cx + 9 * s, cy),
                (cx, cy + 11 * s), (cx - 9 * s, cy)], w)


    @staticmethod
    def _primary_effect(effects):
        return next((k for k in (
            'atk', 'crit', 'lifesteal', 'attack_speed', 'mana', 'hp', 'df',
            'dodge', 'move_speed', 'gold_pct', 'xp_pct', 'craft_xp',
            'craft_quality', 'quest_gold', 'quest_xp', 'summon', 'rage',
            'frenzy', 'extra_stats', 'plain_weapons')
            if effects.get(k)), None)


    def _draw_node_icon(self, node, cx, cy, r, color, strength=1.0):
        """Иконка из assets/icons, а при её отсутствии — процедурный значок."""
        from render.assets_icons import IconAssets, node_icon_candidates
        effects = node.get('effects') or {}
        effect_key = self._primary_effect(effects)
        candidates = node_icon_candidates(node.get('key'), effect_key,
                                         node.get('tab'))
        size = int(r * 1.9)
        if IconAssets.draw(self.screen, candidates, cx, cy, size,
                           alpha=255 * min(1.0, max(0.25, strength))):
            return
        self._draw_skill_glyph(effects, cx, cy, r, color, strength)


    def _draw_class_icon(self, class_key, effects, cx, cy, r, color,
                         strength=1.0):
        from render.assets_icons import IconAssets, class_icon_candidates
        effect_key = self._primary_effect(effects or {})
        size = int(r * 1.9)
        if IconAssets.draw(self.screen,
                           class_icon_candidates(class_key, effect_key),
                           cx, cy, size,
                           alpha=255 * min(1.0, max(0.25, strength))):
            return
        self._draw_skill_glyph(effects or {'hp': 1}, cx, cy, r, color,
                               strength)


    def _draw_craft_hub_icon(self, tab, effects, cx, cy, r, color,
                             strength=1.0):
        from render.assets_icons import IconAssets, node_icon_candidates
        effect_key = self._primary_effect(effects or {})
        candidates = node_icon_candidates(None, effect_key, tab)
        size = int(r * 1.9)
        if IconAssets.draw(self.screen, candidates, cx, cy, size,
                           alpha=255 * min(1.0, max(0.25, strength))):
            return
        self._draw_skill_glyph(effects or {'craft_quality': 1}, cx, cy, r,
                               color, strength)


    def _draw_node_counter(self, nx, ny, count, total, learned):
        label = f"{count}/{total}"
        color = (126, 166, 226) if learned else (196, 184, 158)
        fw = T.FONT_TINY.size(label)[0]
        box = (nx - fw // 2 - 5, ny, fw + 10, 15)
        T.rounded_rect(self.screen, (14, 12, 10), box, radius=3,
                       border_color=(64, 56, 42))
        self.text(label, nx, ny + 7, color, font=T.FONT_TINY, center=True)


    def _draw_skill_node(self, nx, ny, r, node, selected, mouse):
        state = node.get('state')
        learned = bool(node.get('learned'))
        if learned:
            fill, border, glyph = (30, 38, 27), (208, 172, 86), (238, 216, 152)
        elif state == 'available':
            fill, border, glyph = (32, 29, 22), (170, 140, 70), (224, 202, 148)
        else:
            fill, border, glyph = (22, 20, 18), (66, 58, 45), (108, 98, 82)
        rect = (nx - r - 7, ny - r - 17, r * 2 + 14, r * 2 + 40)
        hover = self._in_rect(rect, mouse)
        if selected:
            border = (246, 228, 172)
        elif hover:
            border = T.shade(border, 1.3)
        pygame.draw.polygon(self.screen, (10, 9, 8), self._hex_points(nx, ny, r + 6))
        pygame.draw.polygon(self.screen, fill, self._hex_points(nx, ny, r))
        pygame.draw.polygon(self.screen, T.shade(fill, 1.6),
                            self._hex_points(nx, ny, r - 6), 1)
        pygame.draw.polygon(self.screen, border, self._hex_points(nx, ny, r + 3), 3)
        if selected:
            pygame.draw.polygon(self.screen, (252, 238, 196),
                                self._hex_points(nx, ny, r + 6), 1)
        strength = 1.0 if learned else (0.82 if state == 'available' else 0.5)
        self._draw_node_icon(node, nx, ny, r * 0.74, glyph, strength)
        self._draw_node_counter(nx, ny + r + 8, 1 if learned else 0, 1, learned)
        return rect


    def _draw_class_hub(self, cls, class_key, cx, cy, r, learned, total):
        border = T.GOLD
        pygame.draw.polygon(self.screen, (9, 8, 7), self._hex_points(cx, cy, r + 8))
        pygame.draw.polygon(self.screen, (44, 36, 23), self._hex_points(cx, cy, r + 3))
        pygame.draw.polygon(self.screen, (28, 25, 19), self._hex_points(cx, cy, r))
        pygame.draw.polygon(self.screen, border, self._hex_points(cx, cy, r + 3), 3)
        pygame.draw.polygon(self.screen, T.shade(border, 0.55),
                            self._hex_points(cx, cy, r - 6), 1)
        pygame.draw.circle(self.screen, T.shade(border, 0.4),
                           (int(cx), int(cy)), int(r * 0.64), 1)
        self._draw_class_icon(class_key, cls.get('effects'),
                              cx, cy - 2, r * 0.6, (232, 208, 154))
        self._draw_node_counter(cx, cy + r + 8, learned, total, True)


    def _draw_class_tree(self, game, nodes, cls, ctrl, x, y, cw, tree_bottom):
        """Единое древо: узлы текущего класса и ветки его эволюции.

        Ветки эволюции — не отдельный список под деревом, а нижний ярус
        того же древа: от центрального узла вниз идёт «ствол», который
        расходится на доступные классы следующей ступени.
        """
        top = y
        mouse = getattr(game, 'mouse_pos', (-1, -1))
        total_h = max(240, tree_bottom - top)

        # Нижний ярус под ветки эволюции.
        pr = max(15, min(22, int(total_h * 0.058)))
        promo_y = tree_bottom - pr - 34
        tree_h = max(150, promo_y - 64 - top)

        cx = x + cw // 2
        cy = top + int(tree_h * 0.55)
        r = max(20, min(28, int(tree_h * 0.085)))
        dx = min(152, int(cw * 0.13))
        dy = min(92, int(tree_h * 0.26))
        # Крылья поднимаются от класса вверх, венец — над центральным узлом.
        positions = [
            (cx - dx * 0.85, cy + dy * 0.78),
            (cx - dx * 1.68, cy + dy * 0.04),
            (cx - dx * 2.45, cy - dy * 0.88),
            (cx + dx * 0.85, cy + dy * 0.78),
            (cx + dx * 1.68, cy + dy * 0.04),
            (cx + dx * 2.45, cy - dy * 0.88),
            (cx, top + r + 12),
        ]
        pos = [positions[i % len(positions)] for i in range(len(nodes))]
        hub = (cx, cy)
        learned_count = sum(1 for n in nodes if n.get('learned'))

        # Тусклый декоративный круг под древом — как фон-орнамент.
        ring = int(min(tree_h * 0.46, cw * 0.3))
        pygame.draw.circle(self.screen, (44, 38, 29), (int(cx), int(cy)), ring, 1)
        pygame.draw.circle(self.screen, (33, 29, 23), (int(cx), int(cy)),
                           int(ring * 0.72), 1)

        # Связи: золотые к изученным узлам, тусклые пунктирные — к прочим.
        pairs = ((None, 0), (0, 1), (1, 2), (None, 3), (3, 4), (4, 5),
                 (None, 6))
        for a, b in pairs:
            if b >= len(pos) or (a is not None and a >= len(pos)):
                continue
            pa = hub if a is None else pos[a]
            pb = pos[b]
            done = bool(nodes[b].get('learned')) and (
                a is None or nodes[a].get('learned'))
            if done:
                pygame.draw.line(self.screen, (198, 162, 74), pa, pb, 3)
                pygame.draw.circle(self.screen, (236, 208, 132),
                                   (int(pb[0]), int(pb[1])), 2)
            else:
                self._dotted_line(pa, pb, (92, 79, 56), 2, 7, 6)

        game._ui_hits['skills']['rows'] = []
        hovered = None
        for i, node in enumerate(nodes):
            nx, ny = pos[i]
            rect = self._draw_skill_node(int(nx), int(ny), r, node,
                                         i == ctrl.selection, mouse)
            game._ui_hits['skills']['rows'].append((i, rect))
            if self._in_rect(rect, mouse):
                hovered = node
        self._draw_class_hub(cls, game.player.class_key, cx, cy, r + 12,
                             learned_count, len(nodes))

        # Подпись яруса эволюции — слева, чтобы не мешать стволу.
        self.text("ЭВОЛЮЦИЯ", x + 6, promo_y - 6, (150, 136, 108),
                  font=T.FONT_TINY)

        # Смена класса — такие же узлы древа, связанные с центром.
        promo_hover = self._draw_promotion_nodes(
            game, ctrl, cx, promo_y, cw, r, mouse,
            hub=(cx, cy + r + 14))
        if promo_hover is not None:
            key = promo_hover
            from core.data import class_data as _class_data
            target = _class_data(key)
            req = ctrl.change_class_requirement()
            ready = req is not None and game.player.level >= req
            lines = [(target.get('desc', ''), T.TEXT_DIM, T.FONT_SMALL)]
            if target.get('perk'):
                lines.append((target['perk'], (200, 180, 130), T.FONT_TINY))
            lines.append(("Клик — сменить класс; ветка прежнего класса "
                          "сбросится с возвратом очков.",
                          T.TEXT_MUTED, T.FONT_TINY))
            if not ready:
                if req is None:
                    lines.insert(0, ("Вершина ветки — смена недоступна.",
                                     T.TEXT_MUTED, T.FONT_SMALL))
                else:
                    lines.insert(0, (f"Нужен {req}-й уровень.", T.HP_CRIT,
                                     T.FONT_SMALL))
            else:
                lines.insert(0, ("Доступно для смены класса.", (240, 210, 96),
                                 T.FONT_SMALL))
            self._draw_tooltip(game, target.get('name', key), lines, mouse)
        elif hovered is not None:
            self._draw_node_tooltip(game, hovered, mouse)


    def _promotion_positions(self, ctrl, cx, cw, cy):
        options = ctrl.promotion_options()
        if not options:
            return []
        spacing = min(160, max(104, int(cw / (len(options) + 1))))
        total = spacing * (len(options) - 1)
        left = cx - total / 2
        return [(left + i * spacing, cy) for i in range(len(options))]


    def _draw_promotion_nodes(self, game, ctrl, cx, cy, cw, r, mouse,
                              hub=None):
        """Узлы эволюции класса — нижний ярус единого древа."""
        from core.data import class_data
        options = ctrl.promotion_options()
        if not options:
            return None
        req = ctrl.change_class_requirement()
        ready = req is not None and game.player.level >= req
        positions = self._promotion_positions(ctrl, cx, cw, cy)
        pr = max(16, r - 6)
        hovered = None
        line_color = (198, 162, 74) if ready else (92, 79, 56)

        # Ствол от центрального узла вниз и общая ветвь-шина, от которой
        # отходят узлы эволюции. Так дерево читается как одно целое.
        bus_y = cy - pr - 24
        if hub is not None:
            if ready:
                pygame.draw.line(self.screen, line_color, hub,
                                 (cx, bus_y), 3)
            else:
                self._dotted_line(hub, (cx, bus_y), line_color, 2, 6, 6)
        if len(positions) > 1:
            left_x = positions[0][0]
            right_x = positions[-1][0]
            if ready:
                pygame.draw.line(self.screen, line_color,
                                 (left_x, bus_y), (right_x, bus_y), 2)
            else:
                self._dotted_line((left_x, bus_y), (right_x, bus_y),
                                  line_color, 2, 6, 6)

        for i, (key, (nx, ny)) in enumerate(zip(options, positions)):
            cls = class_data(key)
            rect = (nx - (positions[1][0] - positions[0][0]) // 2 + 3
                    if len(positions) > 1 else nx - 60,
                    ny - pr - 8,
                    (positions[1][0] - positions[0][0]) - 6
                    if len(positions) > 1 else 120,
                    pr * 2 + 36)
            hover = self._in_rect(rect, mouse)
            if hover:
                hovered = key
            # Ветвь от шины к узлу эволюции.
            if ready:
                pygame.draw.line(self.screen, line_color,
                                 (nx, bus_y), (nx, ny - pr - 2), 2)
            else:
                self._dotted_line((nx, bus_y), (nx, ny - pr - 2),
                                  line_color, 1, 5, 5)
            border = T.GOLD if ready else (92, 82, 64)
            if hover:
                border = (252, 232, 172)
            pygame.draw.polygon(self.screen, (10, 9, 8),
                                self._hex_points(nx, ny, pr + 4))
            pygame.draw.polygon(self.screen, (30, 27, 21),
                                self._hex_points(nx, ny, pr))
            pygame.draw.polygon(self.screen, border,
                                self._hex_points(nx, ny, pr + 2), 2)
            self._draw_class_icon(key, cls.get('effects'),
                                  nx, ny, pr * 0.72,
                                  tuple(cls.get('color', (200, 200, 200))),
                                  1.0 if ready else 0.6)
            label = self.fit_text(cls.get('name', key),
                                  rect[2] - 8, T.FONT_TINY)
            self.text(label, nx, ny + pr + 5,
                      T.GOLD if ready else T.TEXT_MUTED,
                      font=T.FONT_TINY, center=True)
            if not ready and req is not None:
                self.text(f"с {req} ур.", nx, ny + pr + 19,
                          T.HP_CRIT, font=T.FONT_TINY, center=True)
            game._ui_hits['skills']['promotion'].append((i, rect))
        return hovered


    def _node_status(self, node):
        state = node.get('state')
        need = node.get('level')
        return {
            'learned': ("✓ Изучено", (120, 208, 120)),
            'available': ("Доступно · Enter или повторный клик — изучить",
                          (240, 210, 96)),
            'level': (f"Откроется на {need} уровне", (176, 162, 132)),
            'points': ("Нет очков навыков", (212, 158, 112)),
        }.get(state, ("", T.TEXT_DIM))


    def _draw_node_tooltip(self, game, node, mouse):
        """Подсказка узла при наведении — без выбора и клика."""
        from core import skills as sk
        status, status_color = self._node_status(node)
        need = node.get('level', 1)
        p = game.player
        effects = sk.describe_effects(node.get('effects', {}))
        lines = [
            (f"Уровень {need}", T.GOLD if p.level >= need else T.HP_CRIT,
             T.FONT_SMALL),
            (effects or "Пассивных бонусов нет.", T.TEXT, T.FONT_SMALL),
        ]
        if status:
            lines.append((status, status_color, T.FONT_TINY))
        self._draw_tooltip(game, node.get('name', 'Узел'), lines, mouse)


    def _draw_tooltip(self, game, title, lines, mouse):
        """Небольшая панель у курсора, прижатая к границам окна."""
        pad = 10
        inner = 320
        w = T.FONT_H2.size(title)[0]
        for text, _color, font in lines:
            w = max(w, min(inner, font.size(text)[0]))
        w = min(360, w + pad * 2)
        line_h = 18
        h = 30 + len(lines) * line_h + pad
        cx0, cy0, cw, ch = game._modal_content_rect
        mx, my = mouse
        x = mx + 18
        y = my + 16
        x = max(cx0 + 4, min(x, cx0 + cw - w - 4))
        y = max(cy0 + 4, min(y, cy0 + ch - h - 4))
        T.rounded_rect(self.screen, (18, 16, 13), (x, y, w, h), radius=8,
                       border_color=T.TITLE)
        self.text(self.fit_text(title, w - pad * 2, T.FONT_H2),
                  x + pad, y + 7, T.TITLE_LIGHT, font=T.FONT_H2)
        T.divider(self.screen, x + pad, y + 27, x + w - pad)
        for i, (text, color, font) in enumerate(lines):
            self.text(self.fit_text(text, w - pad * 2, font),
                      x + pad, y + 33 + i * line_h, color, font=font)


    def _draw_selected_node_panel(self, sk, nodes, ctrl, x, y, cw, ch, p):
        panel = (x, y, cw, ch)
        T.rounded_rect(self.screen, (24, 21, 17), panel, radius=8,
                       border_color=T.PANEL_BORDER)
        node = nodes[ctrl.selection] if nodes else None
        if not node:
            self.text("Нет узлов в этой ветке.", x + 16, y + 12,
                      T.TEXT_MUTED, font=T.FONT_SMALL)
            return
        state = node.get('state')
        self.text(self.fit_text(node['name'], cw - 200, T.FONT_H2),
                  x + 16, y + 8, T.TITLE_LIGHT, font=T.FONT_H2)
        need = node['level']
        self.text(f"Уровень {need}", x + cw - 16, y + 12,
                  T.GOLD if p.level >= need else T.HP_CRIT,
                  font=T.FONT_SMALL, align_right=True)
        effects = sk.describe_effects(node.get('effects', {}))
        self.text(self.fit_text(effects or "Пассивных бонусов нет.",
                                cw - 32, T.FONT_SMALL),
                  x + 16, y + 37, T.TEXT, font=T.FONT_SMALL)
        status = {
            'learned': ("✓ Изучено", (120, 208, 120)),
            'available': ("Доступно · Enter или повторный клик — изучить за 1 очко",
                          (240, 210, 96)),
            'level': (f"Откроется на {need} уровне", (176, 162, 132)),
            'points': ("Нет очков навыков", (212, 158, 112)),
        }.get(state, ("", T.TEXT_DIM))
        self.text(status[0], x + 16, y + 60, status[1], font=T.FONT_SMALL)


    def _draw_class_tab(self, game, x, y, cw, ch, cls, ctrl, sk, class_data):
        p = game.player
        bottom = game._modal_content_rect[1] + ch
        perk = cls.get('perk', '')
        if perk:
            self.text(self.fit_text(perk, cw, T.FONT_SMALL), x, y,
                      (200, 180, 130), font=T.FONT_SMALL)
            y += 24

        nodes = ctrl.rows()
        # Нижняя панель описания убрана — только hover-tooltip.
        # Древо занимает всё доступное место.
        box = (x, y + 2, cw, max(140, bottom - 30 - (y + 2)))
        T.rounded_rect(self.screen, (21, 19, 15), box, radius=10,
                       border_color=(74, 62, 45))
        T.rounded_rect(self.screen, (17, 15, 12),
                       (box[0] + 4, box[1] + 4, box[2] - 8, box[3] - 8),
                       radius=8, border_color=(48, 42, 33))
        self.text("ДРЕВО КЛАССА", box[0] + 16, box[1] + 8, T.TEXT_DIM,
                  font=T.FONT_TINY)

        self._draw_class_tree(game, nodes, cls, ctrl,
                              box[0] + 16, box[1] + 28,
                              box[2] - 32, box[1] + box[3] - 8)


    def _draw_craft_tab(self, game, x, y, cw, ch, ctrl, sk):
        p = game.player
        tab = ctrl.tab
        level, need, ratio = sk.tab_progress(p, tab)
        T.section(self.screen, x, y, cw, sk.TAB_NAMES.get(tab, tab))
        y += 28
        T.bar(self.screen, (x, y, 280, 12), ratio, (120, 180, 110))
        label = f"Уровень {level} / {sk.SKILL_CAP}"
        if need:
            label += f" · {sk.craft_xp(p, tab)}/{need}"
        self.text(label, x + 294, y - 2, T.TEXT_DIM, font=T.FONT_SMALL)
        y += 26
        desc = sk.CRAFT_TREES.get(tab, {}).get('desc', '')
        if desc:
            self.text(desc, x, y, T.TEXT_MUTED, font=T.FONT_TINY)
            y += 22

        nodes = sk.craft_tree(tab)
        bottom = game._modal_content_rect[1] + ch
        # Нижняя панель описания убрана — только hover-tooltip.
        box = (x, y + 2, cw, max(140, bottom - 30 - (y + 2)))
        T.rounded_rect(self.screen, (21, 19, 15), box, radius=10,
                       border_color=(74, 62, 45))
        T.rounded_rect(self.screen, (17, 15, 12),
                       (box[0] + 4, box[1] + 4, box[2] - 8, box[3] - 8),
                       radius=8, border_color=(48, 42, 33))

        hovered = self._draw_craft_tree(game, nodes, ctrl, tab, level,
                                        box[0] + 16, box[1] + 14,
                                        box[2] - 32, box[1] + box[3] - 8)

    # Цвет и значок по умолчанию для ремесленных веток.
    _CRAFT_GLYPH = {
        'blacksmith': ({'craft_quality': 1}, (224, 188, 126)),
        'alchemy': ({'mana': 1}, (120, 190, 150)),
        'magic_engineering': ({'attack_speed': 1}, (170, 190, 240)),
    }


    def _draw_craft_hub(self, tab, cx, cy, r, level):
        effects, color = self._CRAFT_GLYPH.get(
            tab, ({'craft_quality': 1}, T.GOLD))
        pygame.draw.polygon(self.screen, (9, 8, 7),
                            self._hex_points(cx, cy, r + 8))
        pygame.draw.polygon(self.screen, (38, 32, 22),
                            self._hex_points(cx, cy, r + 3))
        pygame.draw.polygon(self.screen, (26, 23, 18),
                            self._hex_points(cx, cy, r))
        pygame.draw.polygon(self.screen, T.GOLD,
                            self._hex_points(cx, cy, r + 3), 3)
        self._draw_craft_hub_icon(tab, effects, cx, cy - 2, r * 0.6, color)
        self._draw_node_counter(cx, cy + r + 8, level, 25, True)


    def _craft_tree_positions(self, cx, cy, cw, tree_h, count):
        r = max(20, min(28, int(tree_h * 0.075)))
        dx = min(150, int(cw * 0.12))
        dy = min(88, int(tree_h * 0.24))
        left = [
            (cx - dx * 0.85, cy + dy * 0.85),
            (cx - dx * 1.55, cy + dy * 0.20),
            (cx - dx * 2.10, cy - dy * 0.55),
            (cx - dx * 2.45, cy - dy * 1.35),
        ]
        right = [(2 * cx - px, py) for px, py in left]
        pos = (left + right)[:count]
        return pos, r


    def _draw_craft_tree(self, game, nodes, ctrl, tab, level,
                         x, y, cw, tree_bottom):
        """Ремесленная ветка в том же шестиугольном формате, что и класс."""
        top = y
        tree_h = max(200, tree_bottom - top)
        cx = x + cw // 2
        cy = top + int(tree_h * 0.55)
        pos, r = self._craft_tree_positions(cx, cy, cw, tree_h, len(nodes))
        if not pos:
            return None
        self._draw_craft_hub(tab, cx, cy, r + 12, level)

        # Связи: центр → первые узлы крыльев, затем цепочка в каждом крыле.
        pairs = [(None, 0), (None, 4)]
        for a, b in ((0, 1), (1, 2), (2, 3), (4, 5), (5, 6), (6, 7)):
            if b < len(nodes):
                pairs.append((a, b))
        for a, b in pairs:
            if b >= len(pos) or (a is not None and a >= len(pos)):
                continue
            pa = (cx, cy) if a is None else pos[a]
            pb = pos[b]
            done = level >= nodes[b]['level'] and (
                a is None or level >= nodes[a]['level'])
            if done:
                pygame.draw.line(self.screen, (198, 162, 74), pa, pb, 3)
            else:
                self._dotted_line(pa, pb, (92, 79, 56), 2, 6, 6)

        effects, _color = self._CRAFT_GLYPH.get(
            tab, ({'craft_quality': 1}, T.GOLD))
        mouse = getattr(game, 'mouse_pos', (-1, -1))
        hovered = None
        game._ui_hits['skills']['rows'] = []
        for i, node in enumerate(nodes):
            nx, ny = pos[i]
            unlocked = level >= node['level']
            view = {
                **node,
                'state': 'learned' if unlocked else 'level',
                'learned': unlocked,
                'effects': node.get('effects') or effects,
            }
            rect = self._draw_skill_node(int(nx), int(ny), r, view,
                                         i == ctrl.selection, mouse)
            game._ui_hits['skills']['rows'].append((i, rect))
            if self._in_rect(rect, mouse):
                hovered = i
        if hovered is not None:
            node = nodes[hovered]
            unlocked = level >= node['level']
            need = node['level']
            effects_text = None
            from core import skills as _sk
            effects_text = _sk.describe_effects(node.get('effects', {}))
            recipes = _sk.recipes_for_level(tab, need)
            lines = [
                (f"Уровень {need}", T.GOLD if unlocked else T.HP_CRIT,
                 T.FONT_SMALL),
                (effects_text or "Новый уровень навыка.", T.TEXT,
                 T.FONT_SMALL),
            ]
            if recipes:
                names = ', '.join(r[1].get('name', r[0]) for r in recipes)
                lines.append((self.fit_text("Рецепты: " + names, 300,
                                            T.FONT_TINY),
                              T.TEXT_MUTED, T.FONT_TINY))
            lines.append(("Доступно" if unlocked
                          else f"Откроется на {need} уровне",
                          (120, 208, 120) if unlocked
                          else (176, 162, 132), T.FONT_TINY))
            self._draw_tooltip(game, node['name'], lines, mouse)
        return hovered


    def _draw_craft_node_panel(self, sk, tab, nodes, ctrl, level, hovered,
                               x, y, cw, ch):
        panel = (x, y, cw, ch)
        T.rounded_rect(self.screen, (24, 21, 17), panel, radius=8,
                       border_color=T.PANEL_BORDER)
        index = hovered if hovered is not None else ctrl.selection
        node = nodes[index] if nodes and 0 <= index < len(nodes) else None
        if not node:
            self.text("Нет узлов в этой ветке.", x + 16, y + 12,
                      T.TEXT_MUTED, font=T.FONT_SMALL)
            return
        need = node['level']
        unlocked = level >= need
        self.text(self.fit_text(node['name'], cw - 200, T.FONT_H2),
                  x + 16, y + 8, T.TITLE_LIGHT, font=T.FONT_H2)
        self.text(f"Уровень {need}", x + cw - 16, y + 12,
                  T.GOLD if unlocked else T.HP_CRIT,
                  font=T.FONT_SMALL, align_right=True)
        effects = sk.describe_effects(node.get('effects', {}))
        self.text(self.fit_text(effects or "Новый уровень навыка.",
                                cw - 32, T.FONT_SMALL),
                  x + 16, y + 37, T.TEXT, font=T.FONT_SMALL)
        recipes = sk.recipes_for_level(tab, need)
        if recipes:
            names = ', '.join(r[1].get('name', r[0]) for r in recipes)
            self.text(self.fit_text("Рецепты: " + names, cw - 32, T.FONT_TINY),
                      x + 16, y + 58, T.TEXT_MUTED, font=T.FONT_TINY)
        status = ("Доступно" if unlocked else
                  f"Откроется на уровне навыка {need}")
        self.text(status, x + 16, y + 78,
                  (120, 208, 120) if unlocked else (176, 162, 132),
                  font=T.FONT_SMALL)

    # ════════════════════════ КРАФТ ════════════════════════

    def _draw_tabs(self, game, tabs, active, x, y, cw, prefix):
        """Общий ряд вкладок по центру. Возвращает (новый_y, hits)."""
        from core import skills as sk
        hits = []
        labels = [sk.TAB_SHORT.get(tab, tab) for tab in tabs]
        max_w = (cw - 8 * (len(tabs) - 1)) // max(1, len(tabs))
        widths = [min(max_w, max(96, T.FONT_SMALL.size(label)[0] + 44))
                  for label in labels]
        total = sum(widths) + 8 * (len(tabs) - 1)
        tx = x + max(0, (cw - total) // 2)
        for tab, label, tab_w in zip(tabs, labels, widths):
            rect = (tx, y, tab_w, 30)
            is_active = tab == active
            hover = self._in_rect(rect, getattr(game, 'mouse_pos', (-1, -1)))
            bg = T.ROW_HOVER if is_active or hover else (30, 27, 21)
            border = T.TITLE if is_active else T.PANEL_BORDER
            T.rounded_rect(self.screen, bg, rect, radius=6,
                           border_color=border)
            self.text(label, tx + tab_w // 2, y + 15,
                      T.TITLE_LIGHT if is_active else T.TEXT_DIM,
                      font=T.FONT_SMALL, center=True)
            hits.append((tab, rect))
            tx += tab_w + 8
        return y + 40, hits


