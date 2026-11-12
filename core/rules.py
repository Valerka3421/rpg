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
)
from core.entities import Player, Entity


# ════════════════════ ОПЫТ И УРОВНИ ════════════════════
def level_up_once(player):
    """Повышает уровень на 1. Возвращает новый уровень."""
    player.xp -= player.xp_next
    player.level += 1
    player.xp_next = xp_to_next(player.level)
    player.free_points += 5
    player.hp = player.max_hp
    return player.level


def give_xp(player, amount):
    """Начисляет опыт. Возвращает список полученных уровней."""
    player.xp += amount
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
    if isinstance(defender, Player) and random.random() < defender.dodge:
        return AttackResult(dodged=True)

    crit = isinstance(attacker, Player) and random.random() < attacker.crit
    damage = max(1, attacker.atk - defender.df + random.randint(-1, 1))
    if crit:
        damage = int(damage * 1.5)
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

    # Вампиризм работает только у игрока и только по живому врагу
    if isinstance(attacker, Player) and attacker.vampire and defender.hp > 0:
        heal = max(1, damage // 20)
        before = attacker.hp
        attacker.hp = min(attacker.max_hp, attacker.hp + heal)
        result.vampire_heal = attacker.hp - before

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
    gold = random.randint(2, 5 + max(1, depth))
    if monster.boss:
        gold += 30 + depth * 10
    if greedy:
        gold = int(gold * 1.5)
    drops = list(roll_monster_drops(monster.ch))
    if monster.boss:
        drops.extend(BOSS_DROPS.get(monster.ch, []))
        drops.append('potion_l')
    return KillReward(gold=gold, xp=monster.xp_value, drops=drops)


def award_kill(player, monster, depth):
    """Начисляет золото и опыт за моба. Возвращает (награда, новые уровни)."""
    reward = roll_kill_reward(monster, depth, greedy=player.greedy)
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
        if info['damage'] > 0:
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
    """При подборе оружие/броня может оказаться редким."""
    base, rarity = parse_item_key(key)
    data = ITEMS.get(base, {})
    if rarity or data.get('type') not in ('weapon', 'armor'):
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
                if (dungeon.tiles[ty][tx] == Tile.WALL
                        or (tx, ty) in dungeon.items
                        or (tx, ty) in blocked):
                    continue
                dungeon.items[(tx, ty)] = key
                return 'tile'
    inventory[key] = inventory.get(key, 0) + 1
    return 'inventory'


# ════════════════════ КРАФТ ════════════════════
def recipe_info(item_key, inventory, level):
    """(recipe, есть_материалы, хватает_уровня)."""
    item = ITEMS.get(item_key, {})
    recipe = item.get('recipe', {})
    has_materials = bool(recipe) and all(
        inventory.get(mat, 0) >= cnt for mat, cnt in recipe.items())
    level_ok = level >= item.get('level_req', 1)
    return recipe, has_materials, level_ok


def can_craft(item_key, inventory, level):
    recipe, has_materials, level_ok = recipe_info(item_key, inventory, level)
    return bool(recipe) and has_materials and level_ok


def craft(item_key, inventory, level):
    """Создаёт предмет. Возвращает (успех, причина)."""
    recipe, has_materials, level_ok = recipe_info(item_key, inventory, level)
    if not recipe:
        return False, 'no_recipe'
    if not level_ok:
        return False, 'level'
    if not has_materials:
        return False, 'materials'
    for mat, cnt in recipe.items():
        inventory[mat] -= cnt
        if inventory[mat] <= 0:
            del inventory[mat]
    inventory[item_key] = inventory.get(item_key, 0) + 1
    return True, 'ok'


def craftable_list(inventory, level):
    """[(key, data, recipe, can) ...] — как для окна крафта."""
    from core.data import get_craftable_items
    result = []
    for key, item in get_craftable_items():
        recipe, has_materials, level_ok = recipe_info(key, inventory, level)
        result.append((key, item, recipe, has_materials and level_ok))
    return result


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

    if kind in ('weapon', 'armor', 'head', 'legs'):
        slot = kind
        if getattr(player, slot) == key:
            return UseResult(False, 'equipped', 'Уже экипировано.')
        need = item.get('level_req', 1)
        if player.level < need:
            return UseResult(False, 'level',
                             f"Нужен уровень {need}! (сейчас {player.level})")
        consume(inventory, key)
        old = getattr(player, slot)
        if old:
            inventory[old] = inventory.get(old, 0) + 1
        setattr(player, slot, key)
        player.recalc()
        return UseResult(True, 'equip',
                         f"Экипировано: {item_display_name(key)}", 0,
                         item.get('color', (225, 225, 235)))

    if kind == 'scroll':
        return _use_scroll(player, key, item, monsters or [], on_hit)

    if kind == 'book':
        return _learn_book(player, key, item)

    return UseResult(False, 'no_use', f"{item['name']} нельзя использовать.")


def _use_scroll(player, key, item, monsters, on_hit):
    inventory = player.inventory
    effect = item.get('effect')
    if effect == 'heal':
        if player.hp >= player.max_hp:
            return UseResult(False, 'full', 'HP уже полное.')
        healed = min(item.get('value', 0), player.max_hp - player.hp)
        player.hp += healed
        consume(inventory, key)
        return UseResult(True, 'heal', f"{item['name']}: +{healed} HP", healed)

    if effect == 'fireball':
        target = _nearest_monster(monsters, player.x, player.y, 8)
        if target is None:
            return UseResult(False, 'no_target', 'Нет целей для огня.')
        consume(inventory, key)
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
        return UseResult(True, 'fireball',
                         f"Огненный шар: {len(victims)} целей.", len(victims),
                         (255, 120, 50), victims)

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
        consume(inventory, key)
        return UseResult(True, 'freeze',
                         f"Заморозка: {count} врагов замедлены.", count,
                         (120, 180, 255))
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
    return (0 <= x < dungeon.w and 0 <= y < dungeon.h
            and dungeon.tiles[y][x] != Tile.WALL)


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
