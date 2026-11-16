"""Общие правила мира — без pygame и без UI.

Модуль существует ради одной цели: одиночная игра (``game.Game``) и
авторитетный сервер мультиплеера (``net.core.GameCore``) должны считать
урон, лут, статусы, опыт, крафт и подбор предметов **одинаково**.
Раньше это были две независимые копии, которые успели разойтись
(в MP не было статусов, уворота, шипов, дропа и свитков).

Правила только меняют состояние сущностей/инвентаря и возвращают
описание того, что произошло; логи, звук, всплывающий текст и анимации
остаются в UI-слое.
"""
import random
from dataclasses import dataclass, field

from core.config import (
    Tile, MONSTER_ACTION_INTERVAL, MONSTER_ACTION_JITTER,
    BOSS_ACTION_INTERVAL, BOSS_BREATH_INTERVAL, BOSS_BREATH_RANGE,
    BOSS_BREATH_POWER,
)
from core.data import (
    ITEMS, STATUSES, ABILITY_TO_STATUS, BOSS_DROPS,
    roll_monster_drops, roll_rarity, make_loot_key, parse_item_key,
    item_display_name, item_display_color, item_sell_price, xp_to_next,
    item_category, select_recipe, roll_mana_crystal, MONSTERS_BY_CH,
    SKILLS, default_skills, skill_xp_to_next,
)
from core.entities import Player, Entity
from core import skills


# ════════════════════ ОПЫТ И УРОВНИ ════════════════════
def level_up_once(player):
    """Повышает уровень на 1. Возвращает новый уровень."""
    player.xp -= player.xp_next
    player.level += 1
    player.xp_next = xp_to_next(player.level)
    player.free_points += 5
    # Очко навыка — на покупку узлов классовой ветки.
    player.skill_points = getattr(player, 'skill_points', 0) + 1
    player.recalc()
    player.hp = player.max_hp
    player.mana = player.max_mana
    return player.level


def give_xp(player, amount, apply_bonus=True):
    """Начисляет опыт. Возвращает список полученных уровней."""
    if apply_bonus:
        pct = float(getattr(player, 'xp_pct', 0.0) or 0.0)
        amount = int(round(amount * (1.0 + pct)))
    player.xp += max(0, amount)
    gained = []
    guard = 0
    while player.xp >= player.xp_next and guard < 200:
        gained.append(level_up_once(player))
        guard += 1
    return gained


# ════════════════════ БОЙ ════════════════════
@dataclass
class AttackResult:
    dodged: bool = False
    crit: bool = False
    damage: int = 0
    status: str = ''
    vampire_heal: int = 0
    thorns: int = 0
    defender_dead: bool = False
    attacker_dead: bool = False


def face_towards(attacker, defender):
    attacker.facing = (
        1 if defender.x > attacker.x else (-1 if defender.x < attacker.x else 0),
        1 if defender.y > attacker.y else (-1 if defender.y < attacker.y else 0),
    )


def resolve_attack(attacker, defender):
    """Один удар. Меняет HP/статусы и возвращает факты о бое."""
    god = isinstance(defender, Player) and getattr(defender, 'god_mode',
                                                    False)
    if isinstance(defender, Player) and random.random() < defender.dodge:
        return AttackResult(dodged=True)

    crit = isinstance(attacker, Player) and random.random() < attacker.crit
    atk = attacker.atk
    if isinstance(attacker, Player):
        atk = int(atk * attacker.rage_bonus)
    damage = max(1, atk - defender.df + random.randint(-1, 1))
    if crit:
        damage = int(damage * 1.5)
    # Консоль разработчика: режим бессмертия.
    if god:
        damage = 0
    defender.hp -= damage

    attacker.attack_anim = 1.0
    face_towards(attacker, defender)

    result = AttackResult(crit=crit, damage=damage)

    # Способность атакующего (яд/горение/кровотечение/замедление)
    ability = getattr(attacker, 'ability', None)
    if ability in ABILITY_TO_STATUS:
        status = ABILITY_TO_STATUS[ability]
        defender.add_status(status)
        result.status = status

    # Вампиризм/похищение жизни работает только у игрока и только по живому врагу
    if isinstance(attacker, Player) and defender.hp > 0:
        pct = attacker.lifesteal
        if attacker.vampire:
            pct = max(pct, 0.05)
        if pct > 0:
            heal = max(1, int(damage * pct))
            before = attacker.hp
            attacker.hp = min(attacker.max_hp, attacker.hp + heal)
            result.vampire_heal = attacker.hp - before

    # «Замедление удара» (маг воды): шанс повесить slow на цель.
    if isinstance(attacker, Player) and defender.hp > 0:
        slow_chance = float(attacker.effect('slow_hit') or 0.0)
        if slow_chance and random.random() < slow_chance:
            defender.add_status('slow')
            result.status = 'slow'

    result.defender_dead = defender.hp <= 0
    if result.defender_dead:
        return result

    # Шипы — ответный урон игрока
    if isinstance(defender, Player) and defender.thorns and isinstance(attacker, Entity):
        attacker.hp -= 2
        result.thorns = 2
        result.attacker_dead = attacker.hp <= 0
    return result


@dataclass
class KillReward:
    gold: int = 0
    xp: int = 0
    drops: list = field(default_factory=list)


def roll_kill_reward(monster, depth, greedy=False):
    """Золото/опыт/дроп за убитого моба (не применяет к игроку)."""
    profile = MONSTERS_BY_CH.get(getattr(monster, 'ch', ''), {})
    # Животные не носят монет — с них падают только трофеи/материалы.
    # Боссы и разумные монстры дают золото.
    gold = 0
    if profile.get('gold', True) or monster.boss:
        gold = random.randint(2, 5 + max(1, depth))
        if monster.boss:
            gold += 30 + depth * 10
        if greedy:
            gold = int(gold * 1.5)
    drops = list(roll_monster_drops(monster.ch))
    if monster.boss:
        drops.extend(BOSS_DROPS.get(monster.ch, []))
        drops.append('potion_l')
        # Ключ босса выпадает всегда: без него не попасть в казну
        # (сундук + лестница вниз), а из арены не выйти, пока он жив.
        drops.append('boss_key')
    # Кристалл маны может выпасть с любого моба. Размер зависит от моба.
    crystal = roll_mana_crystal(monster)
    if crystal:
        drops.append(crystal)
    return KillReward(gold=gold, xp=monster.xp_value, drops=drops)


def award_kill(player, monster, depth):
    """Начисляет золото и опыт за моба. Возвращает (награда, новые уровни)."""
    reward = roll_kill_reward(monster, depth, greedy=player.greedy)
    pct = float(getattr(player, 'gold_pct', 0.0) or 0.0)
    reward.gold = int(round(reward.gold * (1.0 + pct)))
    player.gold += reward.gold
    levels = give_xp(player, reward.xp)
    return reward, levels


# ════════════════════ ТАЙМИНГИ И АТАКИ БОССОВ ════════════════════
def action_interval(monster):
    """Индивидуальный интервал хода моба с небольшим разбросом."""
    jitter = random.uniform(-MONSTER_ACTION_JITTER, MONSTER_ACTION_JITTER)
    base = BOSS_ACTION_INTERVAL if monster.boss else MONSTER_ACTION_INTERVAL
    return max(0.12, base * (1 + jitter))


def init_action_timers(monsters):
    for m in monsters:
        if m.act_timer <= 0:
            m.act_timer = action_interval(m)


def boss_breath_tick(boss, target, distance, dt):
    """Дальняя атака босса (анти-кайтинг). Возвращает нанесённый урон."""
    if not boss.boss or dt <= 0:
        return 0
    boss.breath_timer = max(0.0, boss.breath_timer - dt)
    if boss.breath_timer > 0:
        return 0
    lo, hi = BOSS_BREATH_RANGE
    if not (lo <= distance <= hi):
        return 0
    boss.breath_timer = BOSS_BREATH_INTERVAL
    damage = max(1, int(boss.atk * BOSS_BREATH_POWER) - target.df)
    if isinstance(target, Player) and getattr(target, 'god_mode', False):
        damage = 0
    target.hp -= damage
    return damage


# ════════════════════ СТАТУСЫ ════════════════════
@dataclass
class StatusEvent:
    key: str
    name: str
    color: tuple
    damage: int = 0
    heal: int = 0
    expired: bool = False


def tick_statuses(entity, heal=True):
    """Тик всех статусов сущности. Возвращает список событий."""
    events = []
    if not entity.statuses:
        return events
    for key, turns in list(entity.statuses.items()):
        info = STATUSES.get(key)
        if not info:
            entity.statuses.pop(key, None)
            continue
        damage = 0
        healed = 0
        god = isinstance(entity, Player) and getattr(entity, 'god_mode', False)
        if info['damage'] > 0 and not god:
            damage = info['damage']
            entity.hp -= damage
        if heal and info['heal'] > 0:
            healed = min(info['heal'], entity.max_hp - entity.hp)
            if healed > 0:
                entity.hp += healed
        entity.statuses[key] = turns - 1
        expired = entity.statuses[key] <= 0
        if expired:
            entity.statuses.pop(key, None)
        events.append(StatusEvent(key=key, name=info['name'],
                                  color=info['color'], damage=damage,
                                  heal=healed, expired=expired))
    # Временные баффы меняют характеристики — пересчитываем.
    if isinstance(entity, Player) and hasattr(entity, 'recalc'):
        entity.recalc()
    return events


# ════════════════════ ЛУТ ════════════════════
@dataclass
class PickupResult:
    kind: str          # 'gold' | 'resource' | 'item'
    key: str = ''
    name: str = ''
    gold: int = 0
    color: tuple = (225, 225, 235)


def roll_pickup_rarity(key):
    """При подборе снаряжение может оказаться редким."""
    base, rarity = parse_item_key(key)
    data = ITEMS.get(base, {})
    if rarity or data.get('type') not in EQUIP_TYPES:
        return key
    rarity = roll_rarity()
    return make_loot_key(key, rarity) if rarity > 0 else key


def pickup(dungeon, player, x, y):
    """Снимает предмет с тайла. Возвращает PickupResult или None."""
    if (x, y) not in dungeon.items:
        return None
    key = dungeon.items.pop((x, y))
    key = roll_pickup_rarity(key)
    base, _ = parse_item_key(key)
    data = ITEMS.get(base, {})
    if data.get('type') == 'gold':
        player.gold += data.get('value', 0)
        return PickupResult('gold', key, data.get('name', ''),
                            data.get('value', 0),
                            data.get('color', (240, 210, 90)))
    player.inventory[key] = player.inventory.get(key, 0) + 1
    kind = 'resource' if data.get('type') == 'resource' else 'item'
    return PickupResult(kind, key, item_display_name(key), 0,
                        item_display_color(key))


def place_loot(dungeon, inventory, x, y, key, avoid=()):
    """Кладёт дроп на тайл или в инвентарь. Возвращает 'tile'|'inventory'."""
    blocked = set(avoid)
    if dungeon.tiles[y][x] in (Tile.STAIRS, Tile.EXIT, Tile.UPSTAIRS):
        inventory[key] = inventory.get(key, 0) + 1
        return 'inventory'
    if (x, y) not in dungeon.items and (x, y) not in blocked:
        dungeon.items[(x, y)] = key
        return 'tile'
    for radius in range(1, 4):
        for dx in range(-radius, radius + 1):
            for dy in range(-radius, radius + 1):
                if abs(dx) != radius and abs(dy) != radius:
                    continue
                tx, ty = x + dx, y + dy
                if not (0 <= tx < dungeon.w and 0 <= ty < dungeon.h):
                    continue
                if (dungeon.tiles[ty][tx] in (Tile.WALL, Tile.LOCKED_DOOR,
                                              Tile.CHEST)
                        or (tx, ty) in dungeon.items
                        or (tx, ty) in blocked):
                    continue
                dungeon.items[(tx, ty)] = key
                return 'tile'
    inventory[key] = inventory.get(key, 0) + 1
    return 'inventory'


# ════════════════════ КРАФТ ════════════════════
# Качество (+N) имеет смысл только для снаряжения.
EQUIP_TYPES = ('weapon', 'armor', 'head', 'legs')


@dataclass
class CraftResult:
    ok: bool
    reason: str = 'ok'
    key: str = ''            # итоговый ключ (может быть с «+N»)
    quality: int = 0
    skill: str = ''
    skill_levels: list = field(default_factory=list)
    item: dict = field(default_factory=dict)
    count: int = 1


def recipe_info(item_key, inventory, level, player=None, recipe_index=None):
    """(материалы, есть_материалы, всё_ли_выполнено).

    ``player`` проверяется на требования навыка. Для совместимости со
    старым вызовом ``recipe_info(key, inv, level, recipe_index)``
    четвёртым позиционным аргументом может быть индекс рецепта.
    """
    if isinstance(player, int) and not isinstance(player, bool):
        player, recipe_index = None, player
    item = ITEMS.get(item_key, {})
    recipe, _count, _idx = select_recipe(item_key, inventory, recipe_index)
    has_materials = bool(recipe) and all(
        inventory.get(mat, 0) >= cnt for mat, cnt in recipe.items())
    level_ok = level >= item.get('level_req', 1)
    skill_ok = True
    if player is not None:
        skill_ok = skills.recipe_unlocked(player, item)
    return recipe, has_materials, level_ok and skill_ok


def can_craft(item_key, inventory, level, player=None, recipe_index=None):
    recipe, has_materials, allowed = recipe_info(
        item_key, inventory, level, player, recipe_index)
    return bool(recipe) and has_materials and allowed


def quality_from_accuracy(accuracy, bonus=0):
    """Точность мини-игры (0..1) → качество 0..3 с бонусом навыков."""
    if accuracy >= 0.93:
        quality = 3
    elif accuracy >= 0.72:
        quality = 2
    elif accuracy >= 0.42:
        quality = 1
    else:
        quality = 0
    return max(0, min(3, quality + int(bonus)))


def roll_craft_quality(player):
    """Качество без мини-игры: редкий шанс бонуса от узлов ветки."""
    bonus = skills.craft_quality_bonus(player)
    roll = random.random()
    quality = 0
    if roll < 0.05 + 0.05 * bonus:
        quality = 1
    if roll < 0.015 + 0.01 * bonus:
        quality = 2
    if roll < 0.004:
        quality = 3
    return max(0, min(3, quality + bonus))


def _consume_recipe(inventory, recipe):
    for mat, cnt in recipe.items():
        inventory[mat] -= cnt
        if inventory[mat] <= 0:
            del inventory[mat]


def craft_item(player, item_key, quality=None, recipe_index=None):
    """Создаёт предмет с опытом навыка и качеством. Возвращает CraftResult."""
    inventory = player.inventory
    item = ITEMS.get(item_key, {})
    if not item or not ('recipe' in item or 'recipes' in item):
        return CraftResult(False, 'no_recipe', item=item)
    recipe, _count, _idx = select_recipe(item_key, inventory, recipe_index)
    recipe, has_materials, allowed = recipe_info(
        item_key, inventory, player.level, player, recipe_index)
    if not allowed:
        need_level = player.level < item.get('level_req', 1)
        return CraftResult(False, 'level' if need_level else 'skill',
                           item=item)
    if not has_materials:
        return CraftResult(False, 'materials', item=item)

    _mats, count, _variant = select_recipe(item_key, inventory, recipe_index)
    count = max(1, int(count))
    _consume_recipe(inventory, recipe)

    if quality is None:
        quality = roll_craft_quality(player)
    quality = max(0, min(3, int(quality)))
    if item.get('type') not in EQUIP_TYPES:
        quality = 0
    full_key = make_loot_key(item_key, quality)
    inventory[full_key] = inventory.get(full_key, 0) + count

    category = item_category(item_key)
    levels = []
    if category in skills.CRAFT_TABS:
        base = (6 + int(item.get('tier', 1)) * 4
                + int(item.get('level_req', 1)) * 2) * count
        levels = skills.gain_craft_xp(player, category, base)
        # Совместимость: плоский словарь опыта навыков.
        player.skill_xp = dict(getattr(player, 'skill_xp', {}) or {})
        player.skill_xp[category] = skills.craft_xp(player, category)
    return CraftResult(True, 'ok', full_key, quality, category, levels,
                       item, count)


def craft(item_key, inventory, level, player=None, recipe_index=None,
          quality=None):
    """Создаёт предмет. Возвращает (успех, причина).

    Без ``player`` работает «сырой» режим (тесты/консоль): требования
    навыков не проверяются. С игроком — полный цикл через craft_item.
    """
    if player is not None:
        res = craft_item(player, item_key, quality, recipe_index)
        return res.ok, res.reason
    item = ITEMS.get(item_key, {})
    recipe, count, _idx = select_recipe(item_key, inventory, recipe_index)
    if not recipe:
        return False, 'no_recipe'
    if level < item.get('level_req', 1):
        return False, 'level'
    if not all(inventory.get(mat, 0) >= cnt for mat, cnt in recipe.items()):
        return False, 'materials'
    _consume_recipe(inventory, recipe)
    inventory[item_key] = inventory.get(item_key, 0) + max(1, int(count))
    return True, 'ok'


def craftable_list(inventory, level, player=None, tab='all'):
    """[(key, data, recipe, can, category, recipe_index, count) ...].

    `tab` — ключ вкладки окна крафта: 'all' или категория навыка.
    """
    from core.data import get_craftable_items
    result = []
    for key, item in get_craftable_items():
        category = item_category(key)
        if tab not in (None, 'all') and category != tab:
            continue
        recipe, count, recipe_index = select_recipe(key, inventory)
        has_materials = bool(recipe) and all(
            inventory.get(mat, 0) >= cnt for mat, cnt in recipe.items())
        level_ok = level >= item.get('level_req', 1)
        allowed = level_ok and (player is None
                                or skills.recipe_unlocked(player, item))
        result.append((key, item, recipe, has_materials and allowed,
                       category, recipe_index, count))
    return result


def give_skill_xp(player, skill, amount):
    """Начисляет опыт навыка крафта. Возвращает число новых уровней."""
    if amount <= 0 or skills.canon_skill(skill) not in SKILLS:
        return 0
    levels = skills.gain_craft_xp(player, skill, amount)
    player.skill_xp = dict(getattr(player, 'skill_xp', {}) or {})
    player.skill_xp[skills.canon_skill(skill)] = skills.craft_xp(player, skill)
    return len(levels)

# ════════════════════ ПРЕДМЕТЫ ════════════════════
def consume(inventory, key):
    if inventory.get(key, 0) <= 0:
        return False
    inventory[key] -= 1
    if inventory[key] <= 0:
        del inventory[key]
    return True


@dataclass
class UseResult:
    ok: bool
    kind: str = ''
    message: str = ''
    amount: int = 0
    color: tuple = (225, 225, 235)
    victims: list = field(default_factory=list)   # [(monster, damage)]


# Тип предмета → слот экипировки.
SLOT_FOR_TYPE = {
    'weapon': 'weapon',
    'armor': 'armor',
    'head': 'head',
    'legs': 'legs',
    # Старое имя типа предмета из ранних сейвов/данных.
    'helmet': 'head',
    'leggings': 'legs',
}


def best_heal_key(inventory):
    """Самое слабое лечебное зелье в инвентаре."""
    potions = []
    for key, count in inventory.items():
        if count <= 0:
            continue
        base, _ = parse_item_key(key)
        data = ITEMS.get(base, {})
        if data.get('type') == 'heal':
            potions.append((data.get('value', 0), key))
    if not potions:
        return None
    potions.sort()
    return potions[0][1]


def _nearest_monster(monsters, x, y, max_dist):
    candidates = [(m, abs(m.x - x) + abs(m.y - y))
                  for m in monsters if m.hp > 0]
    candidates = [(m, d) for m, d in candidates if d <= max_dist]
    if not candidates:
        return None
    candidates.sort(key=lambda pair: pair[1])
    return candidates[0][0]


def use_item(player, key, monsters=None, on_hit=None):
    """Применяет предмет из инвентаря игрока.

    ``monsters`` нужен свиткам, ``on_hit(monster, damage)`` вызывается для
    каждой задетой цели (UI рисует цифры). Смерть монстров обрабатывает
    вызывающий код: в ``victims`` приходят цели с ``hp <= 0``.
    """
    inventory = player.inventory
    if inventory.get(key, 0) <= 0:
        return None
    base, _ = parse_item_key(key)
    item = ITEMS.get(base, {})
    if not item:
        return None
    kind = item.get('type')

    if kind == 'resource':
        healing = item.get('heal', 0)
        if healing <= 0:
            return UseResult(False, 'no_use', f"{item['name']} нельзя использовать.")
        if player.hp >= player.max_hp:
            return UseResult(False, 'full', 'HP уже полное.')
        healed = min(healing, player.max_hp - player.hp)
        player.hp += healed
        consume(inventory, key)
        return UseResult(True, 'heal', f"{item['name']}: +{healed} HP", healed)

    if kind == 'heal':
        if player.hp >= player.max_hp:
            return UseResult(False, 'full', 'HP уже полное.')
        healed = min(item.get('value', 0), player.max_hp - player.hp)
        player.hp += healed
        consume(inventory, key)
        return UseResult(True, 'heal', f"{item['name']}: +{healed} HP", healed)

    if kind == 'mana':
        if player.mana >= player.max_mana:
            return UseResult(False, 'full', 'Мана уже полная.')
        restored = min(item.get('value', 0), player.max_mana - player.mana)
        player.mana += restored
        consume(inventory, key)
        return UseResult(True, 'mana', f"{item['name']}: +{restored} маны",
                         restored, item.get('color', (120, 160, 255)))

    if kind == 'buff':
        status = item.get('status')
        if not status:
            return UseResult(False, 'no_use', 'Зелье без эффекта.')
        player.add_status(status)
        player.recalc()
        consume(inventory, key)
        from core.data import STATUSES
        label = STATUSES.get(status, {}).get('name', status)
        return UseResult(True, 'buff', f"{item['name']}: {label}",
                         0, item.get('color', (225, 225, 235)))

    if kind == 'poison':
        target = _nearest_monster(monsters or [], player.x, player.y, 8)
        if target is None:
            return UseResult(False, 'no_target', 'Нет цели для яда.')
        damage = item.get('value', 0)
        target.hp -= damage
        target.add_status('poison', 4)
        consume(inventory, key)
        if on_hit:
            on_hit(target, damage)
        return UseResult(True, 'poison',
                         f"{item['name']}: {target.name} отравлен.", damage,
                         item.get('color', (120, 200, 90)), [(target, damage)])

    slot = SLOT_FOR_TYPE.get(kind)
    if slot:
        if getattr(player, slot) == key:
            return UseResult(False, 'equipped', 'Уже экипировано.')
        need = item.get('level_req', 1)
        if player.level < need:
            return UseResult(False, 'level',
                             f"Нужен уровень {need}! (сейчас {player.level})")
        from core.data import class_can_use
        can_use, reason = class_can_use(player.class_key, key)
        if not can_use:
            return UseResult(False, 'class',
                             f"Класс не может носить ({reason}).")
        consume(inventory, key)
        old = getattr(player, slot)
        if old:
            inventory[old] = inventory.get(old, 0) + 1
        setattr(player, slot, key)
        player.recalc()
        return UseResult(True, 'equip',
                         f"Экипировано: {item_display_name(key)}", 0,
                         item.get('color', (225, 225, 235)))

    if kind in ('scroll', 'rune'):
        return _use_scroll(player, key, item, monsters or [], on_hit)

    if kind == 'book':
        return _learn_book(player, key, item)

    return UseResult(False, 'no_use', f"{item['name']} нельзя использовать.")


def _use_scroll(player, key, item, monsters, on_hit):
    inventory = player.inventory
    effect = item.get('effect')
    cost = int(item.get('mana_cost', 0) or 0)
    if cost and player.mana < cost:
        return UseResult(False, 'mana',
                         f"Не хватает маны ({player.mana}/{cost}).")

    def finish(result):
        if result.ok:
            consume(inventory, key)
            if cost:
                player.mana = max(0, player.mana - cost)
        return result

    if effect == 'heal':
        if player.hp >= player.max_hp:
            return UseResult(False, 'full', 'HP уже полное.')
        healed = min(item.get('value', 0), player.max_hp - player.hp)
        player.hp += healed
        return finish(UseResult(True, 'heal',
                                f"{item['name']}: +{healed} HP", healed))

    if effect == 'ward':
        player.add_status('stone_skin', 20)
        player.recalc()
        return finish(UseResult(True, 'ward',
                                f"{item['name']}: каменная кожа.", 0,
                                item.get('color', (200, 200, 160))))

    if effect == 'lightning':
        target = _nearest_monster(monsters, player.x, player.y, 8)
        if target is None:
            return UseResult(False, 'no_target', 'Нет целей для молнии.')
        damage = item.get('value', 0)
        target.hp -= damage
        target.add_status('slow', 2)
        if on_hit:
            on_hit(target, damage)
        return finish(UseResult(True, 'lightning',
                                f"Молния: {target.name} −{damage}.", damage,
                                item.get('color', (240, 230, 110)),
                                [(target, damage)]))

    if effect == 'fireball':
        target = _nearest_monster(monsters, player.x, player.y, 8)
        if target is None:
            return UseResult(False, 'no_target', 'Нет целей для огня.')
        victims = []
        for monster in list(monsters):
            if monster.hp <= 0:
                continue
            if abs(monster.x - target.x) <= 1 and abs(monster.y - target.y) <= 1:
                monster.hp -= item.get('value', 0)
                monster.add_status('burning', 3)
                victims.append((monster, item.get('value', 0)))
                if on_hit:
                    on_hit(monster, item.get('value', 0))
        return finish(UseResult(True, 'fireball',
                                f"Огненный шар: {len(victims)} целей.",
                                len(victims), (255, 120, 50), victims))

    if effect == 'freeze':
        count = 0
        for monster in list(monsters):
            if monster.hp <= 0:
                continue
            if abs(monster.x - player.x) <= 5 and abs(monster.y - player.y) <= 5:
                monster.add_status('slow', item.get('value', 0))
                count += 1
        if count == 0:
            return UseResult(False, 'no_target', 'Рядом нет врагов.')
        return finish(UseResult(True, 'freeze',
                                f"Заморозка: {count} врагов замедлены.", count,
                                (120, 180, 255)))
    return UseResult(False, 'no_use', 'Свиток не сработал.')


def _learn_book(player, key, item):
    inventory = player.inventory
    stat = item.get('stat')
    if stat in ('double_strike', 'regen', 'vampire', 'thorns', 'greedy'):
        if getattr(player, stat, False):
            return UseResult(False, 'learned', 'Уже изучено.')
        setattr(player, stat, True)
        consume(inventory, key)
        return UseResult(True, 'book', f"Изучено: {item['name']}!", 0,
                         (240, 210, 90))
    if stat in ('str', 'agi', 'vit', 'luk', 'df_stat'):
        setattr(player, stat, getattr(player, stat) + item['value'])
        player.recalc()
        consume(inventory, key)
        label = {'df_stat': 'DEF'}.get(stat, stat.upper())
        return UseResult(True, 'book',
                         f"Изучено: +{item['value']} {label}", item['value'],
                         (240, 210, 90))
    return UseResult(False, 'no_use', 'Книга не даёт эффекта.')


# ════════════════════ МАГАЗИН ════════════════════
def buy_price(stock_entry):
    return stock_entry.get('price', 0)


def buy_from_stock(stock, index, player):
    """Покупка товара из стока. Возвращает (успех, причина, цена)."""
    if not (0 <= index < len(stock)):
        return False, 'index', 0
    entry = stock[index]
    if entry.get('sold'):
        return False, 'sold', 0
    key, price = entry['key'], entry.get('price', 0)
    base, _ = parse_item_key(key)
    if base == 'book_double' and player.double_strike:
        return False, 'learned', price
    if player.gold < price:
        return False, 'gold', price
    player.gold -= price
    player.inventory[key] = player.inventory.get(key, 0) + 1
    entry['sold'] = True
    return True, 'ok', price


def sell_price_for(key):
    return item_sell_price(key)


# ════════════════════ СПАВН ════════════════════
def tile_walkable(dungeon, x, y):
    if not (0 <= x < dungeon.w and 0 <= y < dungeon.h):
        return False
    tile = dungeon.tiles[y][x]
    # WALL и LOCKED_DOOR (заперта до ключа босса) непроходимы.
    return tile != Tile.WALL and tile != Tile.LOCKED_DOOR


def find_free_tile(dungeon, x, y, occupied=(), max_radius=8):
    """Ближайшая проходимая и незанятая клетка (для спавна).

    Обычный пол предпочитается лестницам: иначе игрок/старейшина может
    появиться прямо на входе в подземелье.
    """
    blocked = set(occupied)
    if tile_walkable(dungeon, x, y) and (x, y) not in blocked \
            and dungeon.tiles[y][x] == Tile.FLOOR \
            and (x, y) not in dungeon.items:
        return x, y
    fallback = None
    for radius in range(1, max_radius + 1):
        for dx in range(-radius, radius + 1):
            for dy in range(-radius, radius + 1):
                if max(abs(dx), abs(dy)) != radius:
                    continue
                tx, ty = x + dx, y + dy
                if not tile_walkable(dungeon, tx, ty):
                    continue
                if (tx, ty) in blocked or (tx, ty) in dungeon.items:
                    continue
                if dungeon.tiles[ty][tx] == Tile.FLOOR:
                    return tx, ty
                if fallback is None:
                    fallback = (tx, ty)
        if fallback is not None:
            # в этом радиусе есть только лестницы — лучше они, чем ничего
            return fallback
    return None
