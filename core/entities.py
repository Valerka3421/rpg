"""Сущности мира."""
from dataclasses import dataclass
from core.config import COL_PLAYER, COL_TEXT, Tile
from core.data import (ITEMS, CLASSES, parse_item_key, xp_to_next,
                       default_skills)


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
        """data: dict с str/agi/vit/luk/df_stat/weapon/armor/head/legs."""
        str_ = data.get('str', 5)
        agi = data.get('agi', 5)
        vit = data.get('vit', 5)
        luk = data.get('luk', 5)
        df_st = data.get('df_stat', 5)
        weapon = data.get('weapon')
        armor = data.get('armor')
        head = data.get('head')
        legs = data.get('legs')

        base_w, w_lvl = parse_item_key(weapon) if weapon else (None, 0)
        base_a, a_lvl = parse_item_key(armor) if armor else (None, 0)
        base_h, h_lvl = parse_item_key(head) if head else (None, 0)
        base_l, l_lvl = parse_item_key(legs) if legs else (None, 0)
        w_data = ITEMS.get(base_w, {}) if base_w else {}
        a_data = ITEMS.get(base_a, {}) if base_a else {}
        h_data = ITEMS.get(base_h, {}) if base_h else {}
        l_data = ITEMS.get(base_l, {}) if base_l else {}

        wb = w_data.get('value', 0) + w_lvl
        ab = a_data.get('value', 0) + a_lvl
        hb = h_data.get('value', 0) + h_lvl
        lb = l_data.get('value', 0) + l_lvl
        hp_extra = (a_data.get('hp_bonus', 0) + h_data.get('hp_bonus', 0)
                    + l_data.get('hp_bonus', 0))

        w_crit = w_data.get('crit_bonus', 0.0)
        w_dodge = w_data.get('dodge_bonus', 0.0)
        a_dodge = a_data.get('dodge_bonus', 0.0)
        h_dodge = h_data.get('dodge_bonus', 0.0)
        l_dodge = l_data.get('dodge_bonus', 0.0)

        dodge_base = min(0.30, agi * 0.015)
        dodge_total = max(0.0, min(0.40, dodge_base + w_dodge + a_dodge
                                   + h_dodge + l_dodge))
        crit_base = min(0.40, luk * 0.02)
        crit_total = min(0.50, crit_base + w_crit)

        return {
            'max_hp': 15 + vit * 2 + hp_extra,
            'atk': 2 + str_ // 2 + wb,
            'df': df_st // 2 + ab + hb + lb,
            'dodge': dodge_total,
            'crit': crit_total,
            '_w_crit_bonus': w_crit,
            '_w_dodge_bonus': w_dodge + h_dodge + l_dodge,
            '_a_dodge_bonus': a_dodge,
        }

    def __init__(self, class_key='default'):
        cls = CLASSES.get(class_key, CLASSES['default'])
        super().__init__(0, 0, 25, 25, 4, 1, "Герой", "@", COL_PLAYER, 0)
        self.class_key = class_key
        self.class_name = cls['name']
        self.level = 1
        self.xp = 0
        self.xp_next = xp_to_next(1)
        self.free_points = 0
        self.str = cls['str']
        self.agi = cls['agi']
        self.vit = cls['vit']
        self.luk = cls['luk']
        self.df_stat = cls['df_stat']
        self.weapon = cls.get('start_weapon')
        self.armor = cls.get('start_armor')
        self.head = None
        self.legs = None
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
        self.skills = default_skills()   # {навык: {'level': 1, 'xp': 0}}
        self._w_crit_bonus = 0.0
        self._w_dodge_bonus = 0.0
        self._a_dodge_bonus = 0.0
        self.recalc()

    def recalc(self):
        old_max = self.max_hp
        stats = Player.preview_stats({
            'str': self.str, 'agi': self.agi, 'vit': self.vit,
            'luk': self.luk, 'df_stat': self.df_stat,
            'weapon': self.weapon, 'armor': self.armor,
            'head': self.head, 'legs': self.legs,
        })
        self.max_hp = stats['max_hp']
        self.atk = stats['atk']
        self.df = stats['df']
        self._w_crit_bonus = stats['_w_crit_bonus']
        self._w_dodge_bonus = stats['_w_dodge_bonus']
        self._a_dodge_bonus = stats['_a_dodge_bonus']
        if self.max_hp > old_max:
            self.hp += (self.max_hp - old_max)
        self.hp = min(self.hp, self.max_hp)

    @property
    def dodge(self):
        base = min(0.30, self.agi * 0.015)
        return max(0.0, min(0.40, base + self._w_dodge_bonus + self._a_dodge_bonus))

    @property
    def crit(self):
        base = min(0.40, self.luk * 0.02)
        return min(0.50, base + self._w_crit_bonus)


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
        # Переходы между локациями: (x, y) -> {'to': key, 'entry': (x, y)}
        self.transitions = {}
        # Добываемые ресурсы: (x, y) -> запись core.harvest
        self.harvestables = {}


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