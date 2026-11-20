using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using Aethelgard.Core;

namespace Aethelgard.Net;

/// <summary>
/// TCP server for cooperative multiplayer.
/// Ported from Python net/server.py.
///
/// - Accepts connections on port 9000
/// - Assigns player IDs
/// - Receives input from clients
/// - Sends snapshots 20 times/sec
/// - Player save/restore by IP+token
/// </summary>
public class GameServer
{
    public const int DefaultPort = 9000;
    public const int MaxPlayers = 4;
    public const int MaxMsgSize = 65536;
    public const float SnapshotInterval = 0.05f;  // 20 Hz

    private TcpListener? _listener;
    private Thread? _acceptThread;
    private Thread? _updateThread;
    private volatile bool _running;
    private int _nextPid = 1;
    private readonly Dictionary<int, TcpClient> _clients = new();
    private readonly object _lock = new();

    public GameCore Core { get; } = new();
    public int Port { get; private set; }
    public string? ServerPassword { get; set; }

    // Saved players by token
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
        try
        {
            var stream = client.GetStream();
            var buf = new byte[MaxMsgSize];
            var lineBuf = new StringBuilder();

            // Send welcome
            var welcome = new ServerMsg
            {
                Type = "welcome",
                Pid = pid,
                YourToken = $"tok_{pid}_{DateTime.Now.Ticks}",
            };
            SendMsg(stream, welcome);

            while (_running && client.Connected)
            {
                int read = stream.Read(buf, 0, buf.Length);
                if (read == 0) break;

                lineBuf.Append(Encoding.UTF8.GetString(buf, 0, read));

                // Process complete lines
                while (true)
                {
                    string data = lineBuf.ToString();
                    int nl = data.IndexOf('\n');
                    if (nl < 0) break;

                    string line = data[..nl];
                    lineBuf.Remove(0, nl + 1);

                    try
                    {
                        var msg = JsonSerializer.Deserialize<ClientMsg>(line);
                        if (msg == null) continue;

                        if (msg.Type == "hello")
                        {
                            string classKey = msg.Class ?? "warrior";
                            // Check password
                            if (ServerPassword != null)
                            {
                                if (msg.ServerPassword != ServerPassword)
                                {
                                    SendMsg(stream, new ServerMsg
                                    {
                                        Type = "error",
                                        Msg = "Wrong server password"
                                    });
                                    break;
                                }
                            }
                            Core.AddPlayer(pid, classKey);
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
                                Core.ChatLog.Add((pid, msg.ChatText ?? ""));
                            }
                        }
                        else if (msg.Type == "disconnect")
                        {
                            break;
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }
        finally
        {
            Core.RemovePlayer(pid);
            lock (_lock)
            {
                _clients.Remove(pid);
            }
            try { client.Close(); } catch { }
        }
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
        lock (_lock)
        {
            foreach (var (pid, client) in _clients)
            {
                if (!client.Connected) continue;
                try
                {
                    var snap = Core.BuildSnapshot(pid);
                    var json = JsonSerializer.Serialize(snap) + "\n";
                    var bytes = Encoding.UTF8.GetBytes(json);
                    client.GetStream().Write(bytes, 0, bytes.Length);
                }
                catch { }
            }
        }
    }

    private void SendMsg(NetworkStream stream, ServerMsg msg)
    {
        try
        {
            var json = JsonSerializer.Serialize(msg) + "\n";
            var bytes = Encoding.UTF8.GetBytes(json);
            stream.Write(bytes, 0, bytes.Length);
        }
        catch { }
    }
}
