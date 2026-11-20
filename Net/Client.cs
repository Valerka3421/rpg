using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using Aethelgard.Core;
using Microsoft.Xna.Framework;

namespace Aethelgard.Net;

/// <summary>
/// TCP client for multiplayer.
/// Ported from Python net/client.py.
///
/// - Connects to server
/// - Sends input messages
/// - Receives snapshots in background thread
/// - Stores latest snapshot for rendering
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

    private readonly object _snapLock = new();
    private readonly StringBuilder _lineBuf = new();

    /// <summary>
    /// Connect to a server.
    /// </summary>
    public bool Connect(string host, int port, string classKey,
        string? password = null, string? token = null)
    {
        try
        {
            _tcp = new TcpClient();
            _tcp.Connect(host, port);
            _stream = _tcp.GetStream();
            _running = true;

            // Send hello
            var hello = new ClientMsg
            {
                Type = "hello",
                Class = classKey,
                Token = token ?? $"tok_{Environment.TickCount}",
                ServerPassword = password,
            };
            SendMsg(hello);

            // Start receiving
            _recvThread = new Thread(RecvLoop) { IsBackground = true };
            _recvThread.Start();

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

        while (_running && _tcp?.Connected == true)
        {
            try
            {
                int read = _stream!.Read(buf, 0, buf.Length);
                if (read == 0) break;

                _lineBuf.Append(Encoding.UTF8.GetString(buf, 0, read));

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
                        var snap = JsonSerializer.Deserialize<Snapshot>(line);
                        if (snap != null && snap.Players.Count > 0)
                        {
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
                        var msg = JsonSerializer.Deserialize<ServerMsg>(line);
                        if (msg == null) continue;

                        if (msg.Type == "welcome")
                        {
                            MyPid = msg.Pid ?? -1;
                            MyToken = msg.YourToken;
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
        try
        {
            var json = JsonSerializer.Serialize(msg) + "\n";
            var bytes = Encoding.UTF8.GetBytes(json);
            _stream.Write(bytes, 0, bytes.Length);
        }
        catch { }
    }
}
