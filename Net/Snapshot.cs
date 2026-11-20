using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Aethelgard.Net;

/// <summary>
/// Network snapshot — serialized state sent from server to clients 20 times/sec.
/// Ported from Python net/snapshot_builder.py + net/view.py.
///
/// Contains: all players' positions/stats, visible monsters, items on ground,
/// current dungeon tiles (delta-compressed in full version), events log.
/// </summary>
[Serializable]
public class Snapshot
{
    // ── Players ──
    public class PlayerState
    {
        public int Pid;
        public int X, Y;
        public int Hp, MaxHp;
        public int Mana, MaxMana;
        public int Level;
        public int Xp, XpNext;
        public int Gold;
        public string ClassKey;
        public string ClassName;
        public char Ch;
        public Color Color;
        public bool Alive;
    }

    public List<PlayerState> Players = new();

    // ── Monsters ──
    public class MonsterState
    {
        public int X, Y;
        public int Hp, MaxHp;
        public char Ch;
        public Color Color;
        public bool Boss;
        public string Name;
    }

    public List<MonsterState> Monsters = new();

    // ── Items on ground ──
    public List<(int X, int Y, string Key)> Items = new();

    // ── Dungeon info ──
    public int Depth;
    public int MapW, MapH;
    public string WorldMode;  // "overworld" or "dungeon"
    public (int X, int Y)? StairsPos;

    // ── Events (combat log) ──
    public List<string> Events = new();

    // ── Chat messages ──
    public List<(int Pid, string Text)> Chat = new();

    // ── NPCs (overworld only) ──
    public List<(int X, int Y, char Ch, Color Color, string Name)> Npcs = new();

    // ── My player ID ──
    public int MyPid;
}

/// <summary>
/// Client → server messages.
/// </summary>
[Serializable]
public class ClientMsg
{
    public string Type;  // "hello", "input", "chat", "save_world", "disconnect"

    // hello
    public string? Token;
    public string? Class;
    public string? ServerPassword;

    // input
    public int? Dx, Dy;
    public bool? Attack;
    public bool? UseStairs;
    public bool? TalkNpc;
    public bool? UsePotion;
    public int? UseItemIdx;
    public bool? NextPage;
    public bool? PrevPage;

    // chat
    public string? ChatText;
}

/// <summary>
/// Server → client messages (besides snapshots).
/// </summary>
[Serializable]
public class ServerMsg
{
    public string Type;  // "welcome", "error", "event", "chat"

    // welcome
    public int? Pid;
    public string? YourToken;

    // error
    public string? Msg;

    // chat
    public int? FromPid;
    public string? ChatText;
}
