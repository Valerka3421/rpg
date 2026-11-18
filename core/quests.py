"""Система квестов: лог, прогресс, награды."""
from __future__ import annotations

from typing import Any, Optional


class QuestLog:
    def __init__(self) -> None:
        self.available: set[str] = set()
        self.active: dict[str, dict[str, Any]] = {}
        self.completed: set[str] = set()
        self.turned_in: set[str] = set()

    # ── Выдача ──
    def add_available(self, quest_keys: list[str]) -> None:
        for k in quest_keys:
            if (k not in self.turned_in and k not in self.active
                    and k not in self.completed):
                self.available.add(k)

    def accept(
        self,
        quest_key: str,
        count: int,
        inventory: Optional[dict[str, int]] = None,
    ) -> bool:
        """Взять квест. Если передан инвентарь, сразу считаем прогресс
        «принеси N предметов» — иначе уже собранные предметы не засчитывались."""
        if quest_key not in self.available:
            return False
        self.available.discard(quest_key)
        self.active[quest_key] = {'progress': 0, 'count': count}
        if inventory is not None:
            from core.data import QUESTS
            quest = QUESTS.get(quest_key, {})
            if quest.get('type') == 'collect':
                have = inventory.get(quest['target'], 0)
                self.active[quest_key]['progress'] = min(have, count)
                if have >= count:
                    self._complete(quest_key)
        return True

    # ── Обновления ──
    def update_kill(self, monster_ch: str) -> None:
        from core.data import QUESTS
        for q_key, state in list(self.active.items()):
            q = QUESTS.get(q_key)
            if not q or q['type'] != 'kill':
                continue
            if q['target'] != monster_ch:
                continue
            state['progress'] += 1
            if state['progress'] >= state['count']:
                self._complete(q_key)

    def update_collect(self, item_key: str, player_inv: dict[str, int]) -> None:
        from core.data import QUESTS
        for q_key, state in list(self.active.items()):
            q = QUESTS.get(q_key)
            if not q or q['type'] != 'collect':
                continue
            if q['target'] != item_key:
                continue
            have = player_inv.get(item_key, 0)
            state['progress'] = min(have, state['count'])
            if state['progress'] >= state['count']:
                self._complete(q_key)

    def update_reach(self, depth: int) -> None:
        from core.data import QUESTS
        for q_key, state in list(self.active.items()):
            q = QUESTS.get(q_key)
            if not q or q['type'] != 'reach':
                continue
            if depth >= q['target']:
                self._complete(q_key)

    def update_craft(self, item_key: str) -> None:
        from core.data import QUESTS
        for q_key, state in list(self.active.items()):
            q = QUESTS.get(q_key)
            if not q or q['type'] != 'craft':
                continue
            if q['target'] != item_key:
                continue
            state['progress'] += 1
            if state['progress'] >= state['count']:
                self._complete(q_key)

    def _complete(self, q_key: str) -> None:
        if q_key in self.active:
            self.active.pop(q_key, None)
            self.completed.add(q_key)

    # ── Сдача ──
    def turn_in(self, q_key: str, player: Any, game: Any) -> bool:
        if q_key not in self.completed:
            return False
        from core.data import QUESTS
        q = QUESTS.get(q_key)
        if not q:
            return False
        if q['type'] == 'collect':
            mat = q['target']
            need = q['count']
            if player.inventory.get(mat, 0) < need:
                return False
            player.inventory[mat] -= need
            if player.inventory[mat] <= 0:
                del player.inventory[mat]
        gold = q.get('reward_gold', 0)
        gold_pct = float(getattr(player, 'quest_gold_pct', 0.0) or 0.0)
        player.gold += int(round(gold * (1.0 + gold_pct)))
        for it_key, cnt in q.get('reward_items', {}).items():
            player.inventory[it_key] = player.inventory.get(it_key, 0) + cnt
        xp = q.get('reward_xp', 0)
        xp_pct = float(getattr(player, 'quest_xp_pct', 0.0) or 0.0)
        from core import rules
        levels = rules.give_xp(player, int(round(xp * (1.0 + xp_pct))),
                               apply_bonus=False)
        for level in levels:
            if hasattr(game, '_on_level_up'):
                game._on_level_up(level)
        self.completed.discard(q_key)
        self.turned_in.add(q_key)
        return True

    # ── Сериализация ──
    def to_dict(self) -> dict[str, Any]:
        return {
            'available': sorted(self.available),
            'active': {k: dict(v) for k, v in self.active.items()},
            'completed': sorted(self.completed),
            'turned_in': sorted(self.turned_in),
        }

    @classmethod
    def from_dict(cls, d: dict[str, Any]) -> 'QuestLog':
        ql = cls()
        ql.available = set(d.get('available', []))
        ql.active = {k: dict(v) for k, v in d.get('active', {}).items()}
        ql.completed = set(d.get('completed', []))
        ql.turned_in = set(d.get('turned_in', []))
        return ql

    def accept_generated(self, quest: dict[str, Any]) -> bool:
        """Взять сгенерированный квест с доски."""
        key = quest['id']
        if key in self.active or key in self.turned_in:
            return False
        self.active[key] = {
            'progress': 0, 'count': quest['count'], 'generated': True,
            'type': quest['type'], 'target': quest['target'],
            'name': quest['name'], 'desc': quest['desc'],
            'reward_gold': quest['reward_gold'],
            'reward_xp': quest['reward_xp'],
        }
        return True

    def turn_in_generated(self, key: str, player: Any, game: Any) -> bool:
        data = self.active.get(key)
        if not data or not data.get('generated'):
            return False
        if data['progress'] < data['count']:
            return False
        if data['type'] == 'collect':
            need = data['count']
            have = player.inventory.get(data['target'], 0)
            if have < need:
                return False
            player.inventory[data['target']] -= need
            if player.inventory[data['target']] <= 0:
                del player.inventory[data['target']]
        player.gold += data['reward_gold']
        player.xp += data['reward_xp']
        while player.xp >= player.xp_next:
            game.level_up()
        self.active.pop(key)
        self.turned_in.add(key)
        return True
