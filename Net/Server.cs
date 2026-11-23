using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
namespace Aethelgard.Net;

/// <summary>
/// TCP server for cooperative multiplayer.
/// Ported from Python net/server.py.
///
/// - Accepts connections on port 9000 (+20 при занятости)
/// - Assigns player IDs, максимум MaxPlayers (5-й получает error)
/// - Receives input from clients (MAX_MSG_SIZE: буфер без \n больше 64К → разрыв)
/// - Sends snapshots 20 times/sec
/// - Пароль сервера — constant-time сравнение (hmac.compare_digest)
/// - HMAC-подпись сообщений сессионным ключом (os.urandom(32))
/// </summary>
public class GameServer
{
    public const int DefaultPort = 9000;
    public const int MaxPlayers = 4;
    public const int MaxMsgSize = Config.MaxMsgSize;  // 65536
    public const float SnapshotInterval = 0.05f;  // 20 Hz

    private TcpListener? _listener;
    private Thread? _acceptThread;
    private Thread? _updateThread;
    private volatile bool _running;
    private int _nextPid = 1;
    private readonly Dictionary<int, TcpClient> _clients = new();
    private readonly Dictionary<int, byte[]> _sessionKeys = new();
    private readonly Dictionary<int, bool> _authenticated = new();
    private readonly object _lock = new();

    public GameCore Core { get; } = new();
    public int Port { get; private set; }

    // Хранится только хеш. Setter принимает либо готовый PBKDF2-хеш,
    // либо открытый пароль (тогда он сразу хешируется).
    private string? _serverPasswordHash;
    public string? ServerPassword
    {
        get => _serverPasswordHash;
        set => _serverPasswordHash = string.IsNullOrEmpty(value)
            ? null
            : value.StartsWith("pbkdf2_sha256$", StringComparison.Ordinal)
                ? value
                : PasswordHasher.Hash(value);
    }

    // Saved players by token and IP (net/server.py:86-102)
    private readonly Dictionary<string, string> _savedPlayers = new();

    public void Start(int port = DefaultPort)
    {
        Port = port;
        _running = true;

        // Bind
        for (int attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, Port);
                _listener.Start();
                break;
            }
            catch
            {
                Port++;
            }
        }

        if (_listener == null)
            throw new Exception("Could not bind to any port");

        _acceptThread = new Thread(AcceptLoop) { IsBackground = true };
        _acceptThread.Start();

        _updateThread = new Thread(UpdateLoop) { IsBackground = true };
        _updateThread.Start();
    }

    public void Stop()
    {
        _running = false;
        _listener?.Stop();
        lock (_lock)
        {
            foreach (var client in _clients.Values)
            {
                try { client.Close(); } catch { }
            }
            _clients.Clear();
        }
    }

    private void AcceptLoop()
    {
        while (_running)
        {
            try
            {
                var client = _listener!.AcceptTcpClient();
                var pid = _nextPid++;
                lock (_lock)
                {
                    _clients[pid] = client;
                }
                var thread = new Thread(() => HandleClient(pid, client))
                {
                    IsBackground = true
                };
                thread.Start();
            }
            catch
            {
                break;
            }
        }
    }

    private void HandleClient(int pid, TcpClient client)
    {
        var remoteIp = (client.Client.RemoteEndPoint as IPEndPoint)?.Address?.ToString() ?? "";
        try
        {
            var stream = client.GetStream();
            var buf = new byte[8192];
            var lineBuf = new StringBuilder();
            var decoder = Encoding.UTF8.GetDecoder();
            var chars = new char[Encoding.UTF8.GetMaxCharCount(buf.Length)];

            // Простой rate limit: не более 240 команд в секунду с одного клиента.
            var rateWindow = DateTime.UtcNow;
            int rateCount = 0;

            // Session key for HMAC message signing (os.urandom(32))
            var sessionKey = RandomNumberGenerator.GetBytes(32);
            lock (_lock) { _sessionKeys[pid] = sessionKey; _authenticated[pid] = false; }

            // Send welcome with pid, token, session key
            var welcome = new ServerMsg
            {
                Type = "welcome",
                Pid = pid,
                YourToken = $"tok_{pid}_{DateTime.Now.Ticks}",
                SessionKey = Convert.ToBase64String(sessionKey),
            };
            SendMsg(stream, welcome);

            while (_running && client.Connected)
            {
                int read = stream.Read(buf, 0, buf.Length);
                if (read == 0) break;

                lineBuf.Append(chars, 0, decoder.GetChars(buf, 0, read, chars, 0));

                // MAX_MSG_SIZE: буфер без \n больше лимита → разрыв соединения
                if (lineBuf.Length > MaxMsgSize)
                    break;

                if ((DateTime.UtcNow - rateWindow).TotalSeconds >= 1.0)
                {
                    rateWindow = DateTime.UtcNow;
                    rateCount = 0;
                }

                // Process complete lines
                while (true)
                {
                    string data = lineBuf.ToString();
                    int nl = data.IndexOf('\n');
                    if (nl < 0) break;

                    string line = data[..nl];
                    lineBuf.Remove(0, nl + 1);

                    if (line.Length > MaxMsgSize) break;

                    if (++rateCount > 240)
                        break;  // flood — рвём соединение

                    ClientMsg? msg = null;
                    try
                    {
                        msg = Protocol.Deserialize<ClientMsg>(line);
                    }
                    catch { /* invalid JSON — не роняет сервер */ }
                    if (msg == null || string.IsNullOrEmpty(msg.Type)) continue;

                    // HMAC-подпись: проверяем только для аутентифицированных
                    // после hello. Payload — тот же объект без Sig, а не
                    // строковая хирургия исходного JSON.
                    lock (_lock)
                    {
                        if (_authenticated.GetValueOrDefault(pid) && msg.Sig != null)
                        {
                            if (!VerifyHmac(pid, msg))
                                break; // подделка — разрыв
                        }
                    }

                    if (msg.Type == "hello")
                    {
                        if (msg.ProtocolVersion != Protocol.Version)
                        {
                            SendMsg(stream, new ServerMsg
                            {
                                Type = "error",
                                Msg = $"protocol mismatch: server={Protocol.Version}, client={msg.ProtocolVersion}"
                            });
                            break;
                        }
                        // MaxPlayers: пятый клиент получает error
                        lock (_lock)
                        {
                            if (Core.Players.Count >= MaxPlayers)
                            {
                                SendMsg(stream, new ServerMsg { Type = "error", Msg = "server full" });
                                break;
                            }
                        }
                        string classKey = msg.Class ?? "warrior";
                        // Пароль сервера — PBKDF2-хеш + constant-time проверка.
                        if (!string.IsNullOrEmpty(_serverPasswordHash))
                        {
                            if (!PasswordHasher.Verify(msg.ServerPassword ?? "", _serverPasswordHash))
                            {
                                SendMsg(stream, new ServerMsg
                                {
                                    Type = "error",
                                    Msg = "Wrong server password"
                                });
                                break;
                            }
                        }
                        lock (_lock)
                        {
                            Core.AddPlayer(pid, classKey);
                            _authenticated[pid] = true;
                        }
                        // Remember player by token+IP
                        if (!string.IsNullOrEmpty(msg.Token))
                            _savedPlayers[$"{msg.Token}|{remoteIp}"] = classKey;
                    }
                    else if (msg.Type == "input")
                    {
                        lock (_lock)
                        {
                            Core.ProcessInput(pid, msg);
                        }
                    }
                    else if (msg.Type == "chat")
                    {
                        lock (_lock)
                        {
                            string clean = Config.SanitizeChat(msg.ChatText ?? "");
                            if (clean.Length > 0) Core.ChatLog.Add((pid, clean));
                        }
                    }
                    else if (msg.Type == "save_world")
                    {
                        lock (_lock)
                        {
                            Core.SaveWorld();
                        }
                    }
                    else if (msg.Type == "disconnect")
                    {
                        break;
                    }
                    // мусорные пакеты без известного типа молча игнорируются
                }
            }
        }
        catch { }
        finally
        {
            lock (_lock)
            {
                Core.RemovePlayer(pid);
                _clients.Remove(pid);
                _sessionKeys.Remove(pid);
                _authenticated.Remove(pid);
            }
            try { client.Close(); } catch { }
        }
    }

    /// <summary>
    /// Проверка HMAC-подписи: HMACSHA256(session_key, canonical payload).
    /// Payload — тот же DTO без поля Sig, сериализованный теми же опциями,
    /// что и на клиенте. Никакой строковой хирургии исходного JSON.
    /// </summary>
    private bool VerifyHmac(int pid, ClientMsg msg)
    {
        byte[]? key = null;
        lock (_lock)
        {
            _sessionKeys.TryGetValue(pid, out key);
        }
        if (key == null || string.IsNullOrEmpty(msg.Sig)) return false;

        string payload = Protocol.SignedPayload(msg);
        return Protocol.Verify(payload, msg.Sig, key);
    }

    private void UpdateLoop()
    {
        float snapshotTimer = 0;
        var lastTime = DateTime.UtcNow;

        while (_running)
        {
            var now = DateTime.UtcNow;
            float dt = (float)(now - lastTime).TotalSeconds;
            lastTime = now;

            lock (_lock)
            {
                Core.Update(dt);
            }

            snapshotTimer += dt;
            if (snapshotTimer >= SnapshotInterval)
            {
                snapshotTimer = 0;
                SendSnapshots();
            }

            Thread.Sleep(16);  // ~60fps logic
        }
    }

    private void SendSnapshots()
    {
        // Собираем DTO под lock (читаем живое состояние), но сериализуем
        // и пишем в сокет уже вне блокировки — сеть не тормозит симуляцию.
        var snapshots = new List<(TcpClient Client, Snapshot Snap)>();
        lock (_lock)
        {
            foreach (var (pid, client) in _clients)
            {
                if (!client.Connected) continue;
                try { snapshots.Add((client, Core.BuildSnapshot(pid))); }
                catch { }
            }
        }

        foreach (var (client, snap) in snapshots)
        {
            try
            {
                var json = Protocol.Serialize(snap) + "\n";
                var bytes = Encoding.UTF8.GetBytes(json);
                client.GetStream().Write(bytes, 0, bytes.Length);
            }
            catch { }
        }
    }

    private void SendMsg(NetworkStream stream, ServerMsg msg)
    {
        try
        {
            var json = Protocol.Serialize(msg) + "\n";
            var bytes = Encoding.UTF8.GetBytes(json);
            stream.Write(bytes, 0, bytes.Length);
        }
        catch { }
    }
}
