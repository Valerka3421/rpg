"""Экран характеристик: draw_stats и _d_* хелперы.
"""
from render.screens_renderer._common import *  # noqa: F401,F403


class StatsMixin:
    """Экран характеристик: draw_stats и _d_* хелперы."""

    def draw_stats(self, game):
        self._modal_bg(game)
        cx0, cy0, cw, ch = game._modal_content_rect
        from core.config import PLUS_SIZE
        p = game.player
        game._ui_hits['stats'] = {'plus': []}

        class_name = getattr(p, 'class_name', 'Приключенец')
        self.text("ХАРАКТЕРИСТИКИ", cx0, cy0, (214, 178, 98), font=T.FONT_H1)
        self.text(f"{class_name} · Уровень {p.level}",
                  self._modal_right(game), cy0 + 8, T.TEXT, align_right=True)
        y = cy0 + 56
        if p.free_points > 0:
            T.badge(self.screen, cx0, y, f"● {p.free_points} очков",
                    color=T.GOLD, bg=(50, 40, 15), font=T.FONT_SMALL)
        y += 32
        y = T.section(self.screen, cx0, y, cw, "Характеристики")

        STAT_FIELDS = ['str', 'agi', 'vit', 'luk', 'df_stat', 'int']
        rows = [
            ('Сила', 'STR', p.str, '+1 ATK за 2 очка', self._d_atk(p)),
            ('Ловкость', 'AGI', p.agi, '+1.5% уклонение', self._d_dodge(p)),
            ('Выносливость', 'VIT', p.vit, '+2 HP за очко', self._d_hp(p)),
            ('Удача', 'LUK', p.luk, '+2% крит', self._d_crit(p)),
            ('Защита', 'DEF', p.df_stat, '+1 DEF за 2 очка', self._d_def(p)),
            ('Интеллект', 'INT', p.int_stat,
             '+2 маны и +1 урон магией', self._d_int(p)),
        ]
        for i, (name, abbr, val, gain, detail) in enumerate(rows):
            # ── Кнопка-значение слева ──
            btn_size = PLUS_SIZE + 4
            btn_x = cx0
            btn_y = y - 4
            can_alloc = p.free_points > 0
            bg = (40, 64, 34) if can_alloc else (45, 40, 32)
            border = (120, 170, 104) if can_alloc else (78, 66, 48)
            T.rounded_rect(self.screen, bg,
                           (btn_x, btn_y, btn_size, btn_size),
                           radius=5, border_color=border)
            self.text(str(val), btn_x + btn_size // 2, btn_y + btn_size // 2,
                      (214, 232, 196) if can_alloc else (200, 190, 170),
                      font=T.FONT_BODY, center=True)
            if can_alloc:
                game._ui_hits['stats']['plus'].append(
                    (STAT_FIELDS[i], (btn_x, btn_y, btn_size, btn_size)))

            # ── Номер + название справа от кнопки ──
            text_x = cx0 + btn_size + 16
            self.text(f"{name} ({abbr})", text_x, y, T.TEXT)
            self.text(gain, text_x + 240, y, (134, 180, 116), font=T.FONT_SMALL)
            y += 20
            self.text(f"  └ {detail}", text_x, y, T.TEXT_MUTED, font=T.FONT_TINY)
            y += 20

        y += 8
        y = T.section(self.screen, cx0, y, cw, "Перки")
        active = [f for f in PERK_INFO if getattr(p, f, False)]
        if active:
            for flag in active:
                book_key = PERK_BOOK_MAP[flag]
                name, desc, _ = PERK_INFO[flag]
                self.icons.draw_item(book_key, cx0 + 20, y - 2, size=24)
                self.text(name, cx0 + 52, y, (198, 168, 118), font=T.FONT_BODY)
                self.text(f"— {desc}", cx0 + 52 + T.FONT_BODY.size(name)[0] + 8,
                          y + 2, T.TEXT_MUTED, font=T.FONT_TINY)
                y += 28
        else:
            self.text("  (нет — покупай книги у торговца)",
                      cx0, y, T.TEXT_MUTED, font=T.FONT_SMALL)
            y += 24

        y = T.section(self.screen, cx0, y, cw, "Навыки крафта")
        from core.data import SKILL_ORDER, SKILLS, default_skills, skill_xp_to_next
        skills = getattr(p, 'skills', None) or default_skills()
        sx = cx0
        for skill_key in SKILL_ORDER:
            entry = skills.get(skill_key, {'level': 1, 'xp': 0})
            level = entry.get('level', 1)
            xp = entry.get('xp', 0)
            need = skill_xp_to_next(level)
            label = (f"{SKILLS[skill_key]['name']}: ур.{level} "
                     f"({xp}/{need})")
            w = self.text(label, sx, y + 2, T.TEXT, font=T.FONT_SMALL)
            sx += w + 34
        y += 28


    def _d_atk(self, p):
        base = 2; str_part = p.str // 2
        if p.weapon:
            b, l = parse_item_key(p.weapon)
            weapon = ITEMS.get(b, {}).get('value', 0) + l
        else:
            weapon = 0
        return f"ATK = {base} + {str_part} STR + {weapon} = {base + str_part + weapon}"


    def _d_dodge(self, p):
        raw = p.agi * 1.5; final = int(p.dodge * 100)
        cap = " (кап 30%)" if raw > 30 else ""
        bonus = int((p._w_dodge_bonus + p._a_dodge_bonus) * 100)
        extra = f" + {bonus}% экип" if bonus else ""
        return f"Уклонение = {final}% (1.5% × AGI {p.agi} = {raw:.0f}%{cap}{extra})"


    def _d_hp(self, p):
        extra = 0
        for slot in ('armor', 'head', 'legs', 'boots'):
            key = getattr(p, slot, None)
            if key:
                b, _ = parse_item_key(key)
                extra += ITEMS.get(b, {}).get('hp_bonus', 0)
        return f"HP = 15 + {p.vit * 2} VIT + {extra} = {p.max_hp}"


    def _d_crit(self, p):
        raw = p.luk * 2; final = int(p.crit * 100)
        cap = " (кап 40%)" if raw > 40 else ""
        bonus = int(p._w_crit_bonus * 100)
        extra = f" + {bonus}% оружие" if bonus else ""
        return f"Крит = {final}% (2% × LUK {p.luk} = {raw:.0f}%{cap}{extra})"


    def _d_def(self, p):
        df_part = p.df_stat // 2
        total = df_part
        for slot in ('armor', 'head', 'legs', 'boots'):
            key = getattr(p, slot, None)
            if key:
                b, l = parse_item_key(key)
                total += ITEMS.get(b, {}).get('value', 0) + l
        return f"DEF = {df_part} (df_stat) + {total - df_part} (экип.) = {total}"


    def _d_int(self, p):
        from core.rules import magic_damage
        int_stat = getattr(p, 'int_stat', 5)
        return (f"Мана = 10 + {p.level * 2} (ур.) + {int_stat * 2} (INT) "
                f"= {p.max_mana} · свиток огня 20 → {magic_damage(p, 20)}")

    # ════════════════════════ ИНВЕНТАРЬ ════════════════════════

