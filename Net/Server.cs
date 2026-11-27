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
    private readonly Dictionary<int, System.Security.Cryptography.ECDiffieHellman> _dhKeys = new();
    private readonly Dictionary<int, bool> _authenticated = new();
    // Ревизия карты, известная каждому клиенту (дельта-снапшоты: тайлы
    // включаются в снапшот только при устаревшей ревизии)
    private readonly Dictionary<int, int> _clientMapRev = new();
    // Реконнект: токен клиента → выпавший персонаж (внутри жизни сервера)
    private readonly Dictionary<string, Player> _retiredPlayers = new();
    private readonly Dictionary<int, string> _pidTokens = new();
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

    // Saved players by token and IP (net/server.py:86-102) — мёртвый код
    // удалён: восстановление идёт через Core.SavedPlayers по PID слота,
    // эта таблица никогда не читалась.

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
                // Таймаут отправки: раньше медленный/полуоткрытый клиент
                // блокировал Write под общим _sendLock — снапшоты вставали
                // у всех, симуляция фризилась навсегда.
                client.SendTimeout = 5000;
                client.NoDelay = true;
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
            bool flooded = false;
            bool helloDone = false;
            int lastSeq = 0;

            // ECDH P-256: сессионный ключ HMAC выводится из общего секрета,
            // plaintext-ключ в welcome больше не передаётся (v4)
            var dh = System.Security.Cryptography.ECDiffieHellman.Create(
                System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
            var dhPublic = Convert.ToBase64String(
                dh.PublicKey.ExportSubjectPublicKeyInfo());
            lock (_lock) { _dhKeys[pid] = dh; _authenticated[pid] = false; }

            // Send welcome with pid, token and server ECDH public key
            var welcome = new ServerMsg
            {
                Type = "welcome",
                Pid = pid,
                YourToken = $"tok_{pid}_{DateTime.Now.Ticks}",
                DhPublic = dhPublic,
            };
            SendMsg(stream, welcome);

            // Таймауты чтения: до hello — 10 с (иначе 4 зависших сокета
            // занимают все слоты), после hello — 60 с (клиент пингует
            // каждые 15 с, любое сообщение сбрасывает таймер).
            client.ReceiveTimeout = 10000;

            while (_running && client.Connected)
            {
                int read;
                try
                {
                    read = stream.Read(buf, 0, buf.Length);
                }
                catch (IOException)
                {
                    break;   // таймаут чтения или разрыв — слот освобождается
                }
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

                    if (line.Length > MaxMsgSize) { flooded = true; break; }

                    if (++rateCount > 240)
                    {
                        flooded = true;   // flood — действительно рвём соединение
                        break;
                    }

                    ClientMsg? msg = null;
                    try
                    {
                        msg = Protocol.Deserialize<ClientMsg>(line);
                    }
                    catch { /* invalid JSON — не роняет сервер */ }
                    if (msg == null || string.IsNullOrEmpty(msg.Type)) continue;

                    // До hello допускается только hello. После hello — только
                    // подписанные (HMAC) сообщения с монотонным Seq: читер,
                    // не шлющий Sig, больше не проходит, replay невозможен.
                    if (!helloDone)
                    {
                        if (msg.Type != "hello") { flooded = true; break; }
                    }
                    else if (msg.Sig == null || !VerifyHmac(pid, msg)
                             || msg.Seq != lastSeq + 1)
                    {
                        break;  // подделка / нет подписи / replay — разрыв
                    }
                    lastSeq = msg.Seq;
                    // Клиент сообщает свою ревизию карты — использём её при
                    // сборке следующего снапшота (дельта-снапшоты).
                    // Запись под _lock: словарь читается в SendSnapshots, а
                    // HandleClient работает в N параллельных потоках.
                    if (msg.MapRev.HasValue)
                    {
                        lock (_lock)
                        {
                            _clientMapRev[pid] = msg.MapRev.Value;
                        }
                    }

                    if (msg.Type == "hello")
                    {
                        // Повторный hello после аутентификации игнорируется:
                        // раньше он выполнял ветку заново и AddPlayer
                        // перезаписывал персонажа новым с полным HP.
                        if (helloDone) continue;
                        if (msg.ProtocolVersion != Protocol.Version)
                        {
                            SendMsg(stream, new ServerMsg
                            {
                                Type = "error",
                                Msg = $"protocol mismatch: server={Protocol.Version}, client={msg.ProtocolVersion}"
                            });
                            break;
                        }
                        // MaxPlayers: пятый клиент получает error.
                        // SendMsg — вне lock: сетевая запись под _lock вместе
                        // с _sendLock могла остановить UpdateLoop при медленном
                        // клиенте (взаимная вложенность блокировок).
                        bool serverFull;
                        lock (_lock)
                        {
                            serverFull = Core.Players.Count >= MaxPlayers;
                        }
                        if (serverFull)
                        {
                            SendMsg(stream, new ServerMsg { Type = "error", Msg = "server full" });
                            break;
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
                            // Реконнект по токену клиента: если игрок с этим
                            // токеном выпал (обрыв/краш клиента), возвращаем
                            // его персонажа с сохранёнными статами. Раньше
                            // каждый реконнект получал свежего персонажа.
                            Player? reattach = null;
                            if (!string.IsNullOrEmpty(msg.Token)
                                && _retiredPlayers.Remove(msg.Token, out var retired))
                            {
                                reattach = retired;
                            }
                            if (reattach != null)
                                Core.ReattachPlayer(pid, reattach);
                            else
                                Core.AddPlayer(pid, classKey);
                            if (!string.IsNullOrEmpty(msg.Token))
                                _pidTokens[pid] = msg.Token;

                            // Сессионный ключ HMAC = SHA256(ECDH(clientPub)).
                            // Клиентский ключ обязателен: без него подписи
                            // проверить не на чем.
                            if (string.IsNullOrEmpty(msg.DhPublic))
                            {
                                SendMsg(stream, new ServerMsg
                                {
                                    Type = "error",
                                    Msg = "hello without ECDH key"
                                });
                                break;
                            }
                            try
                            {
                                using var clientEcdh = System.Security.Cryptography.ECDiffieHellman.Create();
                                clientEcdh.ImportSubjectPublicKeyInfo(
                                    Convert.FromBase64String(msg.DhPublic), out _);
                                var sessionKey = dh.DeriveKeyFromHash(clientEcdh.PublicKey,
                                    System.Security.Cryptography.HashAlgorithmName.SHA256);
                                _sessionKeys[pid] = sessionKey;
                            }
                            catch
                            {
                                SendMsg(stream, new ServerMsg
                                {
                                    Type = "error",
                                    Msg = "invalid ECDH key"
                                });
                                break;
                            }
                            _authenticated[pid] = true;
                        }
                        helloDone = true;
                        client.ReceiveTimeout = 60000;   // дальше пингует клиент
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
                            if (clean.Length > 0)
                            {
                                Core.ChatLog.Add((pid, clean));
                                // Cap: ChatLog копируется в каждый снапшот —
                                // без обрезки он рос бесконечно
                                if (Core.ChatLog.Count > 20)
                                    Core.ChatLog.RemoveRange(0, Core.ChatLog.Count - 20);
                            }
                        }
                    }
                    else if (msg.Type == "save_world")
                    {
                        // Сохранять мир может только хост (первый клиент).
                        // Данные собираются под lock, но записываются на диск
                        // вне блокировки — иначе запись замораживает симуляцию.
                        if (pid != 1)
                        {
                            SendMsg(stream, new ServerMsg
                            {
                                Type = "error",
                                Msg = "save_world доступен только хосту"
                            });
                            break;
                        }
                        Dictionary<string, object?>[] saveData;
                        lock (_lock)
                        {
                            saveData = Core.BuildSaveWorldData();
                        }
                        Core.WriteSaveWorldData(saveData);
                    }
                    else if (msg.Type == "disconnect")
                    {
                        break;
                    }
                    // "ping" и мусорные пакеты без известного типа молча игнорируются
                }

                if (flooded) break;   // rate-limit / oversized line — разрыв
            }
        }
        catch { }
        finally
        {
            lock (_lock)
            {
                // Персонаж выпавшего игрока сохраняется под его токеном —
                // при реконнекте вернём его с теми же статами
                if (_pidTokens.TryGetValue(pid, out var tok))
                {
                    if (Core.Players.TryGetValue(pid, out var retired))
                        _retiredPlayers[tok] = retired;
                    _pidTokens.Remove(pid);
                }
                Core.RemovePlayer(pid);
                _clients.Remove(pid);
                _sessionKeys.Remove(pid);
                _dhKeys.Remove(pid);
                _authenticated.Remove(pid);
                _clientMapRev.Remove(pid);
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
                snapshotTimer -= SnapshotInterval;
                SendSnapshots();
            }

            Thread.Sleep(16);  // ~60fps logic
        }
    }

    // Общий send-lock: раньше UpdateLoop и HandleClient писали в один
    // NetworkStream из разных потоков без синхронизации — записи могли
    // интерливиться и клиент получал склеенный/разорванный JSON.
    private readonly object _sendLock = new();

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
                try
                {
                    // Дельта-снапшот: карта включается только если клиент
                    // отстал по ревизии
                    int rev = _clientMapRev.GetValueOrDefault(pid, -1);
                    snapshots.Add((client, Core.BuildSnapshot(pid, rev)));
                }
                catch { }
            }
        }

        foreach (var (client, snap) in snapshots)
        {
            try
            {
                var json = Protocol.Serialize(snap) + "\n";
                var bytes = Encoding.UTF8.GetBytes(json);
                lock (_sendLock)
                {
                    client.GetStream().Write(bytes, 0, bytes.Length);
                }
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
            lock (_sendLock)
            {
                stream.Write(bytes, 0, bytes.Length);
            }
        }
        catch { }
    }
}
