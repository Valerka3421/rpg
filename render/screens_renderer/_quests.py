"""Журнал квестов и статистика забега.
"""
from render.screens_renderer._common import *  # noqa: F401,F403


class QuestsMixin:
    """Журнал квестов и статистика забега."""

    def draw_quests(self, game):
        self._modal_bg(game)
        cx0, cy0, cw, ch = game._modal_content_rect
        self.text("КВЕСТЫ", cx0, cy0, (214, 178, 98), font=T.FONT_H1)
        y = cy0 + 56
        # Секция + отступ до карточек (синхронизировано с button_rects)
        y = T.section(self.screen, cx0, y, cw, "Список")
        y += 24

        items = game.quests.visible_quests()
        if not items:
            self.text("Нет активных квестов", cx0 + cw // 2, y + 40,
                      T.TEXT_MUTED, font=T.FONT_H2, center=True)
            return

        STATE = {
            'available': ('[ВЗЯТЬ]', (176, 186, 192)),
            'active': ('[АКТИВЕН]', (206, 190, 138)),
            'completed': ('[СДАТЬ]', (134, 190, 118)),
        }
        for i, (idx, crect) in enumerate(game.quests.button_rects()):
            if i >= len(items): break
            key, state = items[i]
            if key in QUESTS:
                q = QUESTS[key]
            else:
                q = game.quest_log.active.get(key, {})
            q_name = q.get('name', '?')
            q_desc = q.get('desc', '')
            q_gold = q.get('reward_gold', 0)
            q_xp = q.get('reward_xp', 0)
            selected = (i == game.quests.selection)
            cx_, cy_, cw_, ch_ = crect
            if selected:
                T.rounded_rect(self.screen, (60, 60, 80), crect,
                               radius=T.R_MD, border_color=(206, 166, 82))
                T.rounded_rect(self.screen, (206, 166, 82), crect,
                               width=1, radius=T.R_MD)
            else:
                T.rounded_rect(self.screen, (32, 32, 46), crect,
                               radius=T.R_MD, border_color=(60, 60, 85))
            label, label_color = STATE[state]
            self.text(q['name'], cx_ + 16, cy_ + 12, (238, 222, 188), font=T.FONT_H2)
            self.text(label, cx_ + cw_ - 16, cy_ + 12, label_color,
                      font=T.FONT_H2, align_right=True)
            self.text(q['desc'], cx_ + 16, cy_ + 40, T.TEXT_DIM, font=T.FONT_SMALL)
            if state == 'active':
                st = game.quest_log.active.get(key, {})
                self.text(f"Прогресс: {st.get('progress', 0)}/{st.get('count', q['count'])}",
                          cx_ + 16, cy_ + 58, (196, 192, 160), font=T.FONT_SMALL)
            elif state == 'completed':
                self.text("Готово к сдаче!", cx_ + 16, cy_ + 58,
                          (134, 190, 118), font=T.FONT_SMALL)
            else:
                self.text(f"+{q['reward_gold']} з.  +{q['reward_xp']} XP",
                          cx_ + 16, cy_ + 58, (240, 210, 90), font=T.FONT_SMALL)

    # ════════════════════════ СТАТИСТИКА ════════════════════════

    def draw_run_stats(self, game):
        self._modal_bg(game)
        cx0, cy0, cw, ch = game._modal_content_rect
        self.text("СТАТИСТИКА ЗАБЕГА", cx0, cy0, (214, 178, 98), font=T.FONT_H1)
        y = cy0 + 56

        s = game.run_summary()
        mins = s['time_seconds'] // 60
        secs = s['time_seconds'] % 60

        left_x = cx0
        right_x = cx0 + cw // 2 + 20
        y_col = y
        self.text("─── ОБЩЕЕ ───", left_x, y_col, (190, 172, 138), font=T.FONT_H2)
        y_col += 40
        left = [
            ("Время", f"{mins}:{secs:02d}"), ("Убито мобов", str(s['kills'])),
            ("Боссов", str(s['boss_kills'])), ("Уровень", str(s['level'])),
            ("Глубина", str(s['depth'])),
            ("Золото сейчас", str(s['gold'])),
            ("Золото добыто", str(s['earned_gold'])),
            ("Зелий", str(s['potions_used'])), ("Крафтов", str(s['crafts'])),
            ("Квестов сдано", str(s['quests'])),
            ("Квестов активно", str(s['quests_active'])),
        ]
        for name, val in left:
            self.text(name, left_x, y_col, T.TEXT_DIM, font=T.FONT_BODY)
            self.text(val, left_x + 260, y_col, T.TEXT, font=T.FONT_BODY)
            y_col += 34

        from core.data import BOSSES, MONSTERS
        y_col = y
        self.text("─── УБИТО ПО ТИПАМ ───", right_x, y_col,
                  (190, 172, 138), font=T.FONT_H2)
        y_col += 40
        all_m = {m['ch']: m['name'] for m in MONSTERS.values()}
        all_m.update({b['ch']: b['name'] for b in BOSSES.values()})
        has = False
        for ch, count in sorted(s['kills_by_ch'].items(), key=lambda x: -x[1]):
            if count == 0:
                continue
            has = True
            self.text(f"{all_m.get(ch, ch)}:", right_x, y_col, T.TEXT_DIM, font=T.FONT_BODY)
            self.text(str(count), right_x + 260, y_col, T.TEXT, font=T.FONT_BODY)
            y_col += 34
        if not has:
            self.text("(пока никого)", right_x, y_col, T.TEXT_MUTED, font=T.FONT_SMALL)

    # ════════════════════════ ОБМЕН МЕЖДУ ИГРОКАМИ ════════════════════════

