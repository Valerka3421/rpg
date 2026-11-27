using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.Xna.Framework;

namespace Aethelgard.Net;

/// <summary>
/// TCP client for multiplayer.
/// Ported from Python net/client.py.
///
/// - Connects to server, валидирует хост (_validate_host)
/// - Токен игрока: ~/.ethelgard_player_id (UUID, chmod 0600)
/// - Подписывает сообщения HMAC(session_key) из welcome
/// - Receives snapshots in background thread
/// </summary>
public class GameClient
{
    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private Thread? _recvThread;
    private volatile bool _running;

    public int MyPid { get; private set; } = -1;
    public string? MyToken { get; private set; }
    public Snapshot? LatestSnapshot { get; private set; }
    public string? ErrorMessage { get; private set; }
    public bool Connected => _running && _tcp?.Connected == true;

    private byte[]? _sessionKey;
    private const int MaxMsgSize = Config.MaxMsgSize;
    private int _seq;                       // monotonic counter пакетов
    private Thread? _pingThread;
    private readonly object _snapLock = new();
    private readonly object _sendLock = new();
    private readonly StringBuilder _lineBuf = new();
    private readonly Decoder _decoder = Encoding.UTF8.GetDecoder();

    // Дельта-снапшоты: клиент сообщает серверу свою ревизию карты, а снапшоты
    // без тайлов reuse'ят кэш предыдущего (раньше каждый снапшот 20 Гц таскал
    // всю карту — десятки КБ × 20/с × игроки)
    private int _mapRev = -1;
    private List<string>? _tilesCache;

    /// <summary>
    /// Токен игрока: ~/.ethelgard_player_id, при создании chmod 0600
    /// (net/client.py:16-38).
    /// </summary>
    public static string GetOrCreateToken()
    {
        try
        {
            string path = Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile), ".ethelgard_player_id");
            if (File.Exists(path))
                return File.ReadAllText(path).Trim();
            string token = Guid.NewGuid().ToString();
            File.WriteAllText(path, token);
            try
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch { /* Windows — права не применимы */ }
            return token;
        }
        catch { return $"tok_{Environment.TickCount}"; }
    }

    /// <summary>
    /// Валидация хоста: срезает http://, порт, слеши; проверяет IP/имя
    /// (net/client.py:41-71).
    /// </summary>
    public static string ValidateHost(string raw)
    {
        string host = raw.Trim();
        host = host.Replace("http://", "").Replace("https://", "");
        int slash = host.IndexOf('/');
        if (slash >= 0) host = host[..slash];
        int colon = host.LastIndexOf(':');
        if (colon > 0 && int.TryParse(host[(colon + 1)..], out _))
            host = host[..colon];
        host = host.Trim();
        if (host.Length == 0) throw new ArgumentException("Empty host");
        if (host.Contains(' ')) throw new ArgumentException("Invalid host");
        return host;
    }

    /// <summary>
    /// Connect to a server.
    /// </summary>
    public bool Connect(string host, int port, string classKey,
        string? password = null, string? token = null)
    {
        try
        {
            host = ValidateHost(host);
            _tcp = new TcpClient();
            _tcp.Connect(host, port);
            _stream = _tcp.GetStream();
            _running = true;
            _seq = 0;
            _sessionKey = null;
            _lineBuf.Clear();

            // Send hello with persistent player token
            var hello = new ClientMsg
            {
                Type = "hello",
                Class = classKey,
                Token = token ?? GetOrCreateToken(),
                ServerPassword = password,
            };
            SendMsg(hello);

            // Start receiving
            _recvThread = new Thread(RecvLoop) { IsBackground = true };
            _recvThread.Start();
            _pingThread = new Thread(PingLoop) { IsBackground = true };
            _pingThread.Start();

            return true;
        }
        catch (Exception e)
        {
            ErrorMessage = e.Message;
            return false;
        }
    }

    /// <summary>
    /// Disconnect from server.
    /// </summary>
    public void Disconnect()
    {
        _running = false;
        try
        {
            SendMsg(new ClientMsg { Type = "disconnect" });
        }
        catch { }
        try { _stream?.Close(); } catch { }
        try { _tcp?.Close(); } catch { }
    }

    /// <summary>
    /// Send input to server.
    /// </summary>
    public void SendInput(int dx, int dy)
    {
        SendMsg(new ClientMsg { Type = "input", Dx = dx, Dy = dy });
    }

    public void SendAttack()
    {
        SendMsg(new ClientMsg { Type = "input", Attack = true });
    }

    public void SendUseStairs()
    {
        SendMsg(new ClientMsg { Type = "input", UseStairs = true });
    }

    public void SendUsePotion()
    {
        SendMsg(new ClientMsg { Type = "input", UsePotion = true });
    }

    public void SendChat(string text)
    {
        SendMsg(new ClientMsg { Type = "chat", ChatText = text });
    }

    public void SendUseItem(string key) =>
        SendMsg(new ClientMsg { Type = "input", UseItemKey = key });

    public void SendUnequip(string slot) =>
        SendMsg(new ClientMsg { Type = "input", UnequipSlot = slot });

    public void SendAllocateStat(string stat) =>
        SendMsg(new ClientMsg { Type = "input", AllocateStat = stat });

    public void SendCraft(string itemKey, int qty, double accuracy) =>
        SendMsg(new ClientMsg { Type = "input", CraftKey = itemKey, CraftQty = qty,
            CraftAccuracy = accuracy });

    public void SendBuy(int idx) =>
        SendMsg(new ClientMsg { Type = "input", BuyIdx = idx });

    public void SendSell(int idx) =>
        SendMsg(new ClientMsg { Type = "input", SellIdx = idx });

    public void SendReroll() =>
        SendMsg(new ClientMsg { Type = "input", Reroll = true });

    public void SendQuest(string key, bool accept) =>
        SendMsg(new ClientMsg { Type = "input", QuestKey = key,
            QuestAccept = accept, QuestTurnIn = !accept });

    public void SendHarvest(int x, int y) =>
        SendMsg(new ClientMsg { Type = "input", HarvestX = x, HarvestY = y });

    public void SendSkill(int idx) =>
        SendMsg(new ClientMsg { Type = "input", SkillIdx = idx });

    public void SendTrade(int target, string action,
        string? itemKey = null, int count = 1, int gold = 0) =>
        SendMsg(new ClientMsg { Type = "input", TradeTarget = target,
            TradeAction = action, TradeItemKey = itemKey,
            TradeCount = count, TradeGold = gold });

    /// <summary>wait — пропуск хода (net/core.py:wait).</summary>
    public void SendWait() =>
        SendMsg(new ClientMsg { Type = "input", Wait = true });

    /// <summary>duel_request — вызов игрока на дуэль.</summary>
    public void SendDuelRequest(int target) =>
        SendMsg(new ClientMsg { Type = "input", DuelTarget = target });

    /// <summary>quest_talk — разговор с NPC/доской заданий.</summary>
    public void SendQuestTalk(string npcType) =>
        SendMsg(new ClientMsg { Type = "input", QuestTalk = npcType });

    /// <summary>save_world — запрос сохранения MP-мира (хост).</summary>
    public void SendSaveWorld() =>
        SendMsg(new ClientMsg { Type = "save_world" });

    /// <summary>Изучить узел дерева навыков по индексу (0–6, как в SP).</summary>
    public void SendLearnNode(int nodeIdx) =>
        SendMsg(new ClientMsg { Type = "input", LearnNodeIdx = nodeIdx });

    /// <summary>Повышение класса (P, change_class).</summary>
    public void SendPromote() =>
        SendMsg(new ClientMsg { Type = "input", Promote = true });

    /// <summary>Назначить предмет в слот хотбара (Shift+1–8 в SP).</summary>
    public void SendQuickbar(int slot, string? itemKey) =>
        SendMsg(new ClientMsg { Type = "input", QuickbarSlot = slot, QuickbarKey = itemKey });

    /// <summary>
    /// Get the latest snapshot (thread-safe).
    /// </summary>
    public Snapshot? GetSnapshot()
    {
        lock (_snapLock)
        {
            return LatestSnapshot;
        }
    }

    private void RecvLoop()
    {
        var buf = new byte[65536];
        var chars = new char[Encoding.UTF8.GetMaxCharCount(buf.Length)];

        while (_running && _tcp?.Connected == true)
        {
            try
            {
                int read = _stream!.Read(buf, 0, buf.Length);
                if (read == 0) break;

                // Декодируем через stateful Decoder: иначе многобайтовый
                // UTF-8 (кириллица) может разорваться между чтениями.
                _lineBuf.Append(chars, 0, _decoder.GetChars(buf, 0, read, chars, 0));

                // Сломанный/враждебный сервер шлёт мусор без \n → лимит буфера
                if (_lineBuf.Length > MaxMsgSize * 4)
                {
                    ErrorMessage = "Сервер шлёт некорректные данные — отключение";
                    _running = false;
                    break;
                }

                while (true)
                {
                    string data = _lineBuf.ToString();
                    int nl = data.IndexOf('\n');
                    if (nl < 0) break;

                    string line = data[..nl];
                    _lineBuf.Remove(0, nl + 1);

                    try
                    {
                        // Try to parse as snapshot first
                        var snap = Protocol.Deserialize<Snapshot>(line);
                        if (snap != null && snap.Players.Count > 0)
                        {
                            // Дельта-снапшот: без тайлов reuse'им кэш предыдущего
                            if (snap.Tiles != null)
                                _tilesCache = snap.Tiles;
                            else
                                snap.Tiles = _tilesCache;
                            _mapRev = snap.MapRev;
                            lock (_snapLock)
                            {
                                LatestSnapshot = snap;
                            }
                            continue;
                        }
                    }
                    catch { }

                    // Try as server message
                    try
                    {
                        var msg = Protocol.Deserialize<ServerMsg>(line);
                        if (msg == null) continue;

                        if (msg.Type == "welcome")
                        {
                            MyPid = msg.Pid ?? -1;
                            MyToken = msg.YourToken;
                            // Сессионный ключ для HMAC-подписи
                            if (!string.IsNullOrEmpty(msg.SessionKey))
                            {
                                try { _sessionKey = Convert.FromBase64String(msg.SessionKey); }
                                catch { _sessionKey = null; }
                            }
                        }
                        else if (msg.Type == "error")
                        {
                            ErrorMessage = msg.Msg;
                            _running = false;
                        }
                    }
                    catch { }
                }
            }
            catch
            {
                break;
            }
        }

        _running = false;
    }

    private void SendMsg(ClientMsg msg)
    {
        if (_stream == null) return;
        lock (_sendLock)
        {
            try
            {
                // Monotonic counter (anti-replay) + HMAC-подпись сессионным
                // ключом. hello — единственное неподписанное сообщение.
                msg.MapRev ??= _mapRev;   // дельта-снапшоты: ревизия карты в каждом пакете
                msg.Seq = ++_seq;
                if (_sessionKey != null && msg.Type != "hello")
                    msg.Sig = Protocol.Sign(Protocol.SignedPayload(msg), _sessionKey);

                var json = Protocol.Serialize(msg) + "\n";
                var bytes = Encoding.UTF8.GetBytes(json);
                _stream.Write(bytes, 0, bytes.Length);
            }
            catch { }
        }
    }

    /// <summary>Keepalive: сервер рвёт молчащие соединения (таймаут чтения),
    /// поэтому клиент периодически шлёт подписанный ping.</summary>
    private void PingLoop()
    {
        while (_running)
        {
            Thread.Sleep(15000);
            if (_running && _sessionKey != null) SendMsg(new ClientMsg { Type = "ping" });
        }
    }
}
