"""Система квестов: лог, прогресс, награды."""


class QuestLog:
    def __init__(self):
        self.available = set()
        self.active = {}          # {quest_key: {'progress': N, 'count': N}}
        self.completed = set()
        self.turned_in = set()

    # ── Выдача ──
    def add_available(self, quest_keys):
        for k in quest_keys:
            if (k not in self.turned_in and k not in self.active
                    and k not in self.completed):
                self.available.add(k)

    def accept(self, quest_key, count, inventory=None):
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
    def update_kill(self, monster_ch):
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

    def update_collect(self, item_key, player_inv):
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

    def update_reach(self, depth):
        from core.data import QUESTS
        for q_key, state in list(self.active.items()):
            q = QUESTS.get(q_key)
            if not q or q['type'] != 'reach':
                continue
            if depth >= q['target']:
                self._complete(q_key)

    def update_craft(self, item_key):
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

    def _complete(self, q_key):
        if q_key in self.active:
            self.active.pop(q_key, None)
            self.completed.add(q_key)

    # ── Сдача ──
    def turn_in(self, q_key, player, game):
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
        player.gold += q.get('reward_gold', 0)
        for it_key, cnt in q.get('reward_items', {}).items():
            player.inventory[it_key] = player.inventory.get(it_key, 0) + cnt
        player.xp += q.get('reward_xp', 0)
        while player.xp >= player.xp_next:
            game.level_up()
        self.completed.discard(q_key)
        self.turned_in.add(q_key)
        return True

    # ── Сериализация ──
    def to_dict(self):
        return {
            'available': sorted(self.available),
            'active': {k: dict(v) for k, v in self.active.items()},
            'completed': sorted(self.completed),
            'turned_in': sorted(self.turned_in),
        }

    @classmethod
    def from_dict(cls, d):
        ql = cls()
        ql.available = set(d.get('available', []))
        ql.active = {k: dict(v) for k, v in d.get('active', {}).items()}
        ql.completed = set(d.get('completed', []))
        ql.turned_in = set(d.get('turned_in', []))
        return ql

    def accept_generated(self, quest):
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

    def turn_in_generated(self, key, player, game):
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
