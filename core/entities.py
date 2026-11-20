"""Сущности мира."""
from dataclasses import dataclass

from core.config import COL_PLAYER, COL_TEXT, PLAYER_ATTACK_COOLDOWN, PLAYER_MOVE_COOLDOWN, Tile
from core.data import (
    CLASSES,
    ITEMS,
    STATUS_MODS,
    class_stats,
    default_skills,
    parse_item_key,
    xp_to_next,
)


class Entity:
    def __init__(self, x: int, y: int, hp: int, max_hp: int,
                 atk: int, df: int, name: str, ch: str, color: tuple,
                 xp_value: int = 0, boss: bool = False,
                 ability: str | None = None) -> None:
        self.x, self.y = x, y
        self.hp, self.max_hp = hp, max_hp
        self.atk, self.df = atk, df
        self.name, self.ch, self.color = name, ch, color
        self.xp_value = xp_value
        self.boss = boss
        self.is_merchant = False
        self.is_npc = False
        self.npc_type: str | None = None
        self.ability = ability
        self.immortal = False
        self.statuses: dict[str, int] = {}      # {status_key: remaining_turns}
        self.anim_time = 0.0  # накапливается в update
        self.attack_anim = 0.0  # 0..1, убывает после атаки
        self.facing = (1, 0)  # направление последнего действия
        self.move_bob = 0.0  # покачивание при движении
        # Индивидуальный таймер хода: у каждого моба свой, а не общий тик.
        self.act_timer = 0.0
        self.breath_timer = 0.0   # только для боссов

    def has_status(self, key: str) -> bool:
        return key in self.statuses

    def add_status(self, key: str, duration: int | None = None) -> None:
        from core.data import STATUSES
        if key not in STATUSES:
            return
        if duration is None:
            duration = STATUSES[key]['duration']
        # Не накапливать больше max
        self.statuses[key] = max(self.statuses.get(key, 0), duration)

    def remove_status(self, key: str) -> None:
        self.statuses.pop(key, None)

    def clear_statuses(self) -> None:
        self.statuses.clear()


class Player(Entity):
    # Единая формула статов — используется и в recalc, и в UI превью класса.
    @staticmethod
    def preview_stats(data: dict) -> dict:
        """Превью статов из словаря данных (для UI выбора класса).

        Делегирует в ``_compute_stats`` — единая формула для ``recalc`` и
        ``preview_stats``, чтобы они не разошлись.

        data: dict с str/agi/vit/luk/df_stat/weapon/armor/head/legs/boots,
        effects (пассивные бонусы класса и навыков), level.
        """
        return Player._compute_stats(data)

    @staticmethod
    def _compute_stats(data: dict) -> dict:
        """Единая формула пересчёта статов из экипировки/эффектов.

        Вызывается из ``preview_stats`` (для UI) и из ``recalc`` (для
        актуального игрока). Любое изменение формулы — в одном месте.

        Возвращает dict с max_hp, atk, df, dodge, crit, max_mana и
        внутренними _w_crit_bonus / _a_dodge_bonus / и т.д. для
        использования в property dodge/crit.
        """
        from core.config import CRIT_BASE_CAP, CRIT_TOTAL_CAP, DODGE_CAP, DODGE_TOTAL_CAP
        str_   = data.get('str', 5)
        agi    = data.get('agi', 5)
        vit    = data.get('vit', 5)
        luk    = data.get('luk', 5)
        df_st  = data.get('df_stat', 5)
        int_   = data.get('int', 5)
        level  = int(data.get('level', 1) or 1)
        effects = data.get('effects') or {}
        head = data.get('head', data.get('helmet'))
        legs = data.get('legs', data.get('leggings'))
        boots = data.get('boots')

        wb = ab = hp_extra = 0
        w_crit = w_dodge = a_dodge = 0.0
        w_df = 0
        mana_extra = 0
        for slot, key in (('weapon', data.get('weapon')),
                          ('armor', data.get('armor')),
                          ('head', head), ('legs', legs), ('boots', boots)):
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

        dodge_base = min(DODGE_CAP, agi * 0.015)
        dodge_total = max(0.0, min(DODGE_TOTAL_CAP, dodge_base + w_dodge + a_dodge
                                    + skill_dodge))
        crit_base = min(CRIT_BASE_CAP, luk * 0.02)
        crit_total = min(CRIT_TOTAL_CAP, crit_base + w_crit + skill_crit)

        return {
            'max_hp': 15 + vit * 2 + hp_extra + int(effects.get('hp', 0)),
            'atk': 2 + str_ // 2 + wb + int(effects.get('atk', 0)),
            'df': df_st // 2 + ab + w_df + int(effects.get('df', 0)),
            'dodge': dodge_total,
            'crit': crit_total,
            'max_mana': (10 + level * 2 + int_ * 2 + mana_extra
                         + int(effects.get('mana', 0))),
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
        self.int_stat = stats.get('int', 5)
        self.weapon = cls.get('start_weapon')
        self.armor = cls.get('start_armor')
        self.head = None
        self.legs = None
        self.boots = None
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
        self._skill_dodge = 0.0
        self._skill_crit = 0.0
        # Навыки и классовый прогресс
        self.skill_points = 1
        self.skill_xp = {k: v['xp'] for k, v in self.skills.items()}
        self.skill_nodes = set()
        self.blueprints = set()        # прочитанные чертежи особых крафтов
        # Мана: ресурс для свитков и рун. Новый герой начинает с полным
        # запасом, чтобы первый же свиток сработал.
        self.mana = 0
        self.max_mana = 0
        self._effects_cache = {}
        # Быстрая панель: 10 слотов под предметы. None — пусто.
        # Привязка — Ctrl+<номер> в инвентаре, использование — <номер> в игре.
        from core.config import QUICKBAR_SLOTS
        self.quickbar = [None] * QUICKBAR_SLOTS
        # Активные навыки класса (заполняется из CLASSES[class]['active_skills'])
        self.active_skill_cd = {}      # {skill_key: remaining_seconds}
        self.recalc()
        self.mana = self.max_mana

    # ── Совместимость слотов: старые имена helmet/leggings ──
    @property
    def helmet(self):
        return self.head

    @helmet.setter
    def helmet(self, value):
        self.head = value

    @property
    def leggings(self):
        return self.legs

    @leggings.setter
    def leggings(self, value):
        self.legs = value

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
            'int': self.int_stat,
            'weapon': self.weapon, 'armor': self.armor,
            'head': self.head, 'legs': self.legs, 'boots': self.boots,
            'effects': effects,
        })
        self.max_hp = stats['max_hp']
        self.max_mana = stats['max_mana']
        self.atk = stats['atk']
        self.df = stats['df']
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

    # ── Быстрая панель ──
    def assign_quickbar(self, slot_idx, item_key):
        """Привязать предмет к слоту быстрой панели (0..QUICKBAR_SLOTS-1)."""
        from core.config import QUICKBAR_SLOTS
        if 0 <= slot_idx < QUICKBAR_SLOTS:
            self.quickbar[slot_idx] = item_key
            return True
        return False

    def clear_quickbar_slot(self, slot_idx):
        from core.config import QUICKBAR_SLOTS
        if 0 <= slot_idx < QUICKBAR_SLOTS:
            self.quickbar[slot_idx] = None
            return True
        return False

    def active_skills(self):
        """Активные навыки класса из CLASSES (магия, способности)."""
        cls = CLASSES.get(self.class_key, {})
        return cls.get('active_skills', []) or []


class Room:
    def __init__(self, x, y, w, h):
        self.x, self.y, self.w, self.h = x, y, w, h
    def center(self):
        return self.x + self.w // 2, self.y + self.h // 2
    def contains(self, x, y):
        return (self.x <= x < self.x + self.w
                and self.y <= y < self.y + self.h)
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
        # Замурованные выходы арены босса: (x, y). После победы над боссом
        # эти тайлы возвращаются в FLOOR и сет пустеет.
        self.barrier_tiles = set()
        # Босс-комплекс: центр казны и клетки запертой двери
        self.treasury_pos = None
        self.boss_door_cells = set()
        # Содержимое сундуков, заданное генерацией: (x, y) -> {ключ: кол-во}
        self.chest_loot = {}


@dataclass
class FloatText:
    x: float
    y: float
    text: str
    color: tuple
    life: float = 0.7


@dataclass
class Projectile:
    """Летящий снаряд: стрела, магический шар, молния и т.д.

    Создаётся в rules.ranged_attack и обновляется в game.update_projectiles.
    Урон применяется при достижении цели. Рендер — в game_renderer.draw_projectiles.
    """
    x: float                 # текущая позиция в тайлах
    y: float
    tx: float                # координаты цели
    ty: float
    kind: str = 'arrow'      # 'arrow' | 'fireball' | 'frost' | 'lightning' | 'heal'
    speed: float = 12.0      # тайлов в секунду
    damage: int = 0
    color: tuple = (255, 255, 255)
    alive: bool = True
    source_pid: int = -1     # pid в MP, для single-player = -1
    target_ch: str = ''      # символ цели (для её поиска в monsters)
    life: float = 2.0        # защита от бесконечного полёта


@dataclass
class Message:
    text: str
    color: tuple = COL_TEXT
