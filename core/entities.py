"""Сущности мира."""
from dataclasses import dataclass
from core.config import COL_PLAYER, COL_TEXT, Tile, PLAYER_MOVE_COOLDOWN, PLAYER_ATTACK_COOLDOWN
from core.data import (ITEMS, CLASSES, class_data, class_stats, parse_item_key,
                       xp_to_next, STATUS_MODS)


class Entity:
    def __init__(self, x, y, hp, max_hp, atk, df, name, ch, color,
                 xp_value=0, boss=False, ability=None):
        self.x, self.y = x, y
        self.hp, self.max_hp = hp, max_hp
        self.atk, self.df = atk, df
        self.name, self.ch, self.color = name, ch, color
        self.xp_value = xp_value
        self.boss = boss
        self.is_merchant = False
        self.is_npc = False
        self.npc_type = None
        self.ability = ability
        self.statuses = {}          # {status_key: remaining_turns}
        self.anim_time = 0.0  # накапливается в update
        self.attack_anim = 0.0  # 0..1, убывает после атаки
        self.facing = (1, 0)  # направление последнего действия
        self.move_bob = 0.0  # покачивание при движении
        # Индивидуальный таймер хода: у каждого моба свой, а не общий тик.
        self.act_timer = 0.0
        self.breath_timer = 0.0   # только для боссов

    def has_status(self, key):
        return key in self.statuses

    def add_status(self, key, duration=None):
        from core.data import STATUSES
        if key not in STATUSES:
            return
        if duration is None:
            duration = STATUSES[key]['duration']
        # Не накапливать больше max
        self.statuses[key] = max(self.statuses.get(key, 0), duration)

    def remove_status(self, key):
        self.statuses.pop(key, None)

    def clear_statuses(self):
        self.statuses.clear()


class Player(Entity):
    # Единая формула статов — используется и в recalc, и в UI превью класса.
    @staticmethod
    def preview_stats(data):
        """data: dict с str/agi/vit/luk/df_stat/weapon/armor/helmet/leggings,
        effects (пассивные бонусы класса и навыков), level."""
        str_   = data.get('str', 5)
        agi    = data.get('agi', 5)
        vit    = data.get('vit', 5)
        luk    = data.get('luk', 5)
        df_st  = data.get('df_stat', 5)
        level  = int(data.get('level', 1) or 1)
        effects = data.get('effects') or {}

        wb = ab = hp_extra = 0
        w_crit = w_dodge = a_dodge = 0.0
        w_df = 0
        mana_extra = 0
        for slot, stat in (('weapon', 'atk'), ('armor', 'df'),
                           ('helmet', 'df'), ('leggings', 'df')):
            key = data.get(slot)
            if not key:
                continue
            base, lvl = parse_item_key(key)
            it = ITEMS.get(base, {})
            value = it.get('value', 0) + lvl
            if slot == 'weapon':
                wb = value
                w_crit = it.get('crit_bonus', 0.0)
                w_dodge = it.get('dodge_bonus', 0.0)
                w_df = it.get('df_bonus', 0)
            else:
                ab += value
                a_dodge += it.get('dodge_bonus', 0.0)
            hp_extra += it.get('hp_bonus', 0)
            mana_extra += it.get('mana_bonus', 0)

        skill_dodge = effects.get('dodge', 0.0)
        skill_crit = effects.get('crit', 0.0)

        dodge_base = min(0.30, agi * 0.015)
        dodge_total = max(0.0, min(0.40, dodge_base + w_dodge + a_dodge
                                    + skill_dodge))
        crit_base = min(0.40, luk * 0.02)
        crit_total = min(0.50, crit_base + w_crit + skill_crit)

        return {
            'max_hp': 15 + vit * 2 + hp_extra + int(effects.get('hp', 0)),
            'atk': 2 + str_ // 2 + wb + int(effects.get('atk', 0)),
            'df': df_st // 2 + ab + w_df + int(effects.get('df', 0)),
            'dodge': dodge_total,
            'crit': crit_total,
            'max_mana': 10 + level * 2 + mana_extra + int(effects.get('mana', 0)),
            '_w_crit_bonus': w_crit,
            '_w_dodge_bonus': w_dodge,
            '_a_dodge_bonus': a_dodge,
            '_skill_dodge': skill_dodge,
            '_skill_crit': skill_crit,
        }

    def __init__(self, class_key='default'):
        if class_key not in CLASSES:
            class_key = 'default'
        cls = CLASSES[class_key]
        stats = class_stats(class_key)
        super().__init__(0, 0, 25, 25, 4, 1, "Герой", "@", COL_PLAYER, 0)
        self.class_key = class_key
        self.class_name = cls['name']
        self.class_tier = cls.get('tier', 1)
        self.class_base = cls.get('base', class_key)
        self.class_changes = max(0, self.class_tier - 1)
        self.level = 1
        self.xp = 0
        self.xp_next = xp_to_next(1)
        self.free_points = 0
        self.str = stats.get('str', 5)
        self.agi = stats.get('agi', 5)
        self.vit = stats.get('vit', 5)
        self.luk = stats.get('luk', 5)
        self.df_stat = stats.get('df_stat', 5)
        self.weapon = cls.get('start_weapon')
        self.armor = cls.get('start_armor')
        self.helmet = None
        self.leggings = None
        self.inventory = {}
        for k, v in cls.get('start_items', {}).items():
            self.inventory[k] = self.inventory.get(k, 0) + v
        self.gold = 0
        self.double_strike = False
        self.regen = False
        self.vampire = False
        self.thorns = False
        self.greedy = False
        self.step_counter = 0
        self._w_crit_bonus = 0.0
        self._w_dodge_bonus = 0.0
        self._a_dodge_bonus = 0.0
        self._skill_dodge = 0.0
        self._skill_crit = 0.0
        # Навыки и классовый прогресс
        self.skill_points = 1
        self.skills = {'smithing': 1, 'alchemy': 1, 'engineering': 1}
        self.skill_xp = {'smithing': 0, 'alchemy': 0, 'engineering': 0}
        self.skill_nodes = set()
        self.mana = 0
        self.max_mana = 0
        self._effects_cache = {}
        self.recalc()
        self.mana = self.max_mana

    # ── Бонусы класса и навыков ──
    def effects(self):
        """Активные пассивные бонусы (класс + узлы + статусы)."""
        from core import skills
        totals = skills.aggregate(self)
        for status in self.statuses:
            for key, value in STATUS_MODS.get(status, {}).items():
                totals[key] = totals.get(key, 0) + value
        return totals

    def effect(self, name, default=0):
        return self.effects().get(name, default)

    @property
    def lifesteal(self):
        return self.effect('lifesteal')

    @property
    def gold_pct(self):
        return self.effect('gold_pct')

    @property
    def xp_pct(self):
        return self.effect('xp_pct')

    @property
    def quest_gold_pct(self):
        return self.effect('quest_gold')

    @property
    def quest_xp_pct(self):
        return self.effect('quest_xp')

    @property
    def vision_bonus(self):
        return int(self.effect('fov'))

    def recalc(self):
        old_max = self.max_hp
        old_mana = self.max_mana
        effects = self.effects()
        stats = Player.preview_stats({
            'str': self.str, 'agi': self.agi, 'vit': self.vit,
            'luk': self.luk, 'df_stat': self.df_stat, 'level': self.level,
            'weapon': self.weapon, 'armor': self.armor,
            'helmet': self.helmet, 'leggings': self.leggings,
            'effects': effects,
        })
        self.max_hp = stats['max_hp']
        self.atk = stats['atk']
        self.df = stats['df']
        self.max_mana = stats['max_mana']
        self._w_crit_bonus = stats['_w_crit_bonus']
        self._w_dodge_bonus = stats['_w_dodge_bonus']
        self._a_dodge_bonus = stats['_a_dodge_bonus']
        self._skill_dodge = stats['_skill_dodge']
        self._skill_crit = stats['_skill_crit']
        if self.max_hp > old_max:
            self.hp += (self.max_hp - old_max)
        if self.max_mana > old_mana:
            self.mana += (self.max_mana - old_mana)
        self.hp = min(self.hp, self.max_hp)
        self.mana = min(self.mana, self.max_mana)

    @property
    def dodge(self):
        base = min(0.30, self.agi * 0.015)
        return max(0.0, min(0.40, base + self._w_dodge_bonus
                            + self._a_dodge_bonus + self._skill_dodge))

    @property
    def crit(self):
        base = min(0.40, self.luk * 0.02)
        return min(0.50, base + self._w_crit_bonus + self._skill_crit)

    # ── Тайминги с учётом навыков и статусов ──
    @property
    def attack_cooldown(self):
        speed = max(0.0, self.effect('attack_speed'))
        frenzy = max(0.0, self.effect('frenzy'))
        if frenzy:
            missing = 1.0 - (self.hp / max(1, self.max_hp))
            speed += frenzy * missing
        return PLAYER_ATTACK_COOLDOWN / (1.0 + speed)

    @property
    def move_cooldown(self):
        speed = max(0.0, self.effect('move_speed'))
        return PLAYER_MOVE_COOLDOWN / (1.0 + speed)

    @property
    def rage_bonus(self):
        rage = max(0.0, self.effect('rage'))
        if rage and self.hp < self.max_hp * 0.5:
            return 1.0 + rage
        return 1.0

    @property
    def has_companion(self):
        return bool(int(self.effect('summon')))


class Room:
    def __init__(self, x, y, w, h):
        self.x, self.y, self.w, self.h = x, y, w, h
    def center(self):
        return self.x + self.w // 2, self.y + self.h // 2
    def intersects(self, other, pad=1):
        return not (self.x + self.w + pad < other.x or
                    other.x + other.w + pad < self.x or
                    self.y + self.h + pad < other.y or
                    other.y + other.h + pad < self.y)


class Dungeon:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.tiles = [[Tile.WALL for _ in range(w)] for _ in range(h)]
        self.rooms = []
        self.monsters = []
        self.items = {}
        self.stairs_pos = None


@dataclass
class FloatText:
    x: float
    y: float
    text: str
    color: tuple
    life: float = 0.7


@dataclass
class Message:
    text: str
    color: tuple = COL_TEXT