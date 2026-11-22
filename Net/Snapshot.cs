using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Aethelgard.Net;

/// <summary>
/// Network snapshot — serialized state sent from server to clients 20 times/sec.
/// Ported from Python net/snapshot_builder.py + net/view.py.
///
/// ВАЖНО: все члены — свойства, а не поля. System.Text.Json по умолчанию
/// игнорирует public-поля, из-за чего раньше снапшоты уходили как "{}".
/// Кортежи (ValueTuple) также заменены на явные DTO-типы: STJ не умеет
/// сериализовать их как контракт.
/// </summary>
[Serializable]
public sealed class Snapshot
{
    // ── Players ──
    public sealed class PlayerState
    {
        public int Pid { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public int Mana { get; set; }
        public int MaxMana { get; set; }
        public int Level { get; set; }
        public int Xp { get; set; }
        public int XpNext { get; set; }
        public int Gold { get; set; }
        public string ClassKey { get; set; } = "";
        public string ClassName { get; set; } = "";
        public char Ch { get; set; }
        public Color Color { get; set; }
        public bool Alive { get; set; }
        public int Atk { get; set; }
        public int Df { get; set; }
        public int FreePoints { get; set; }
        public int SkillPoints { get; set; }
        public int ClassTier { get; set; }
        public Dictionary<string, int> Statuses { get; set; } = new();
        public Dictionary<string, int> Inventory { get; set; } = new();

        // Характеристики (панель C — как stats_screen.py)
        public int Str { get; set; }
        public int Agi { get; set; }
        public int Vit { get; set; }
        public int Luk { get; set; }
        public int DfStat { get; set; }
        public int IntStat { get; set; }

        // Экипировка (HUD + панель инвентаря)
        public string? Weapon { get; set; }
        public string? Armor { get; set; }
        public string? Head { get; set; }
        public string? Legs { get; set; }
        public string? Boots { get; set; }

        // Хотбар (Shift+1–8 в инвентаре, 1–8 — использование)
        public string?[] Quickbar { get; set; } = new string?[8];
    }

    public List<PlayerState> Players { get; set; } = new();

    // ── Monsters ──
    public sealed class MonsterState
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public char Ch { get; set; }
        public Color Color { get; set; }
        public bool Boss { get; set; }
        public string Name { get; set; } = "";
    }

    public List<MonsterState> Monsters { get; set; } = new();

    // ── Items on ground ──
    public sealed class ItemState
    {
        public int X { get; set; }
        public int Y { get; set; }
        public string Key { get; set; } = "";
    }

    public List<ItemState> Items { get; set; } = new();

    // ── Dungeon info ──
    public int Depth { get; set; }
    public int MapW { get; set; }
    public int MapH { get; set; }
    public string WorldMode { get; set; } = "";  // "overworld" or "dungeon"
    public PointState? StairsPos { get; set; }

    /// <summary>
    /// Карта тайлов: строки символов, каждый символ — '0' + (int)Tile.
    /// Даёт клиенту рисовать стены/лестницы/ворота как в синглплеере.
    /// </summary>
    public List<string> Tiles { get; set; } = new();

    /// <summary>Ресурсы для добычи (координаты живых узлов, E — собрать).</summary>
    public List<PointState> Harvests { get; set; } = new();

    // ── Events (combat log) ──
    public List<string> Events { get; set; } = new();

    // ── Chat messages ──
    public sealed class ChatEntry
    {
        public int Pid { get; set; }
        public string Text { get; set; } = "";
    }

    public List<ChatEntry> Chat { get; set; } = new();

    // ── NPCs (overworld + dungeon merchant/board) ──
    public sealed class NpcEntry
    {
        public int X { get; set; }
        public int Y { get; set; }
        public char Ch { get; set; }
        public Color Color { get; set; }
        public string Name { get; set; } = "";
        public string NpcType { get; set; } = "";
    }

    public List<NpcEntry> Npcs { get; set; } = new();

    // ── Сток торговца подземелья (key, price) ──
    public sealed class ShopEntry
    {
        public string Key { get; set; } = "";
        public int Price { get; set; }
    }

    public List<ShopEntry> MerchantStock { get; set; } = new();

    // ── Общий журнал квестов отряда (name: progress/count) ──
    public List<string> Quests { get; set; } = new();

    /// <summary>
    /// Доска квестов для панели L: ключ, имя, прогресс, цель и состояние
    /// (available/active/completed) — чтобы клиент мог принять/сдать квест.
    /// </summary>
    public sealed class QuestBoardEntry
    {
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
        public int Progress { get; set; }
        public int Count { get; set; }
        public string State { get; set; } = "";
    }

    public List<QuestBoardEntry> QuestBoard { get; set; } = new();

    /// <summary>Узлы дерева навыков игрока (панель N): имя, уровень, изучен.</summary>
    public sealed class SkillEntry
    {
        public string Name { get; set; } = "";
        public int Level { get; set; }
        public bool Learned { get; set; }
    }

    public List<SkillEntry> Skills { get; set; } = new();

    /// <summary>Активные навыки игрока (панель N, режим каста): имя, мана.</summary>
    public sealed class ActiveSkillEntry
    {
        public string Name { get; set; } = "";
        public int Mana { get; set; }
    }

    public List<ActiveSkillEntry> ActiveSkills { get; set; } = new();

    /// <summary>Доступные повышения класса (панель N, клавиша P).</summary>
    public List<string> Promotions { get; set; } = new();

    // ── Запрос обмена (для UI-попапа) ──
    public string? TradeEvent { get; set; }

    // ── My player ID ──
    public int MyPid { get; set; }
}

/// <summary>Точка на карте (X, Y) для сетевого протокола.</summary>
public sealed class PointState
{
    public int X { get; set; }
    public int Y { get; set; }
}

/// <summary>
/// Client → server messages.
/// </summary>
[Serializable]
public sealed class ClientMsg
{
    public int ProtocolVersion { get; set; } = Protocol.Version;
    public string Type { get; set; } = "";  // hello/input/chat/trade/quest/harvest/craft/shop/skill/disconnect

    // hello
    public string? Token { get; set; }
    public string? Class { get; set; }
    public string? ServerPassword { get; set; }
    // HMAC-подпись сообщения (session key из welcome)
    public string? Sig { get; set; }

    // input
    public int? Dx { get; set; }
    public int? Dy { get; set; }
    public bool? Attack { get; set; }
    public bool? UseStairs { get; set; }
    public bool? TalkNpc { get; set; }
    public bool? UsePotion { get; set; }
    public int? UseItemIdx { get; set; }
    public bool? NextPage { get; set; }
    public bool? PrevPage { get; set; }

    // item / equipment / stats
    public string? UseItemKey { get; set; }
    public string? UnequipSlot { get; set; }
    public string? AllocateStat { get; set; }

    // craft / shop
    public string? CraftKey { get; set; }
    public int? CraftQty { get; set; }
    public double? CraftAccuracy { get; set; }
    public int? BuyIdx { get; set; }
    public int? SellIdx { get; set; }
    public bool? Reroll { get; set; }

    // quests
    public string? QuestKey { get; set; }
    public bool? QuestAccept { get; set; }
    public bool? QuestTurnIn { get; set; }
    // поговорить с NPC/доской заданий (net/core.py:quest_talk)
    public string? QuestTalk { get; set; }

    // ожидание хода / дуэль (net/core.py:wait, duel_request)
    public bool? Wait { get; set; }
    public int? DuelTarget { get; set; }

    // сохранение мира хостом (net/core.py:save_world)
    public bool? SaveWorld { get; set; }

    // harvest
    public int? HarvestX { get; set; }
    public int? HarvestY { get; set; }

    // active skill
    public int? SkillIdx { get; set; }

    // дерево навыков: изучить узел по индексу (0–6) — как TryLearnNode в SP
    public int? LearnNodeIdx { get; set; }

    // повышение класса (change_class, SP-клавиша P)
    public bool? Promote { get; set; }

    // хотбар: назначить предмет в слот 0–7 (Shift+1–8 в SP)
    public int? QuickbarSlot { get; set; }
    public string? QuickbarKey { get; set; }

    // trade
    public int? TradeTarget { get; set; }
    public string? TradeAction { get; set; }   // request/accept/decline/cancel/ready/add/remove/gold
    public string? TradeItemKey { get; set; }
    public int? TradeCount { get; set; }
    public int? TradeGold { get; set; }

    // chat
    public string? ChatText { get; set; }
}

/// <summary>
/// Server → client messages (besides snapshots).
/// </summary>
[Serializable]
public sealed class ServerMsg
{
    public string Type { get; set; } = "";  // "welcome", "error", "event", "chat"

    // welcome
    public int? Pid { get; set; }
    public string? YourToken { get; set; }
    /// <summary>Сессионный ключ для HMAC-подписи сообщений (base64).</summary>
    public string? SessionKey { get; set; }

    // error
    public string? Msg { get; set; }

    // chat
    public int? FromPid { get; set; }
    public string? ChatText { get; set; }
}
