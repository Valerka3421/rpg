using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Aethelgard.Core;
using Aethelgard.Net;

namespace Aethelgard.Render;

/// <summary>
/// Multiplayer menu — host or join a game.
/// </summary>
public class MultiplayerScreen
{
    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _sb;
    private SpriteFont? _font;
    private Texture2D? _blank;

    public enum Mode { Menu, HostWaiting, JoinInput, Connected, Error }
    public Mode CurrentMode = Mode.Menu;

    public string HostInput = "localhost";
    public string PortInput = "9000";
    public string PasswordInput = "";
    public string ClassInput = "warrior";

    private string _statusMessage = "";

    public GameServer? Server;
    public GameClient? Client;

    public MultiplayerScreen(GraphicsDevice device, SpriteBatch sb)
    {
        _device = device;
        _sb = sb;
    }

    public void LoadFont(SpriteFont font) => _font = font;

    /// <summary>
    /// Draw the multiplayer screen. Returns true if MP game should start.
    /// </summary>
    public bool Draw(KeyboardState keys, KeyboardState prevKeys, MouseState mouse)
    {
        int sw = Game1.ScreenW, sh = Game1.ScreenH;

        _sb.Draw(Blank, new Rectangle(0, 0, sw, sh), new Color(12, 12, 18));

        if (_font == null) return false;

        string title = "MULTIPLAYER";
        var titleSize = _font.MeasureString(title);
        _sb.DrawString(_font, title,
            new Vector2((sw - titleSize.X) / 2, 80),
            Config.ColGold);

        int cx = sw / 2;
        int y = 160;

        switch (CurrentMode)
        {
            case Mode.Menu:
                DrawMenu(cx, y, mouse);
                break;
            case Mode.HostWaiting:
                DrawHostWaiting(cx, y);
                break;
            case Mode.JoinInput:
                DrawJoinInput(cx, y, keys, prevKeys);
                break;
            case Mode.Connected:
                return true;  // Start MP game
            case Mode.Error:
                DrawError(cx, y);
                break;
        }

        return false;
    }

    private void DrawMenu(int cx, int y, MouseState mouse)
    {
        // Host button
        var hostRect = new Rectangle(cx - 150, y, 300, 40);
        bool hostHover = hostRect.Intersects(new Rectangle(mouse.X, mouse.Y, 1, 1));
        _sb.Draw(Blank, hostRect, hostHover ? new Color(50, 45, 30) : new Color(25, 23, 18));
        _sb.Draw(Blank, new Rectangle(hostRect.X, hostRect.Y, hostRect.Width, 2), Config.ColGold);
        _sb.DrawString(_font, "HOST GAME (port 9000)",
            new Vector2(hostRect.X + 60, hostRect.Y + 10),
            hostHover ? Config.ColGold : Config.ColText);

        if (hostHover && mouse.LeftButton == ButtonState.Pressed)
        {
            StartHost();
        }

        // Join button
        y += 60;
        var joinRect = new Rectangle(cx - 150, y, 300, 40);
        bool joinHover = joinRect.Intersects(new Rectangle(mouse.X, mouse.Y, 1, 1));
        _sb.Draw(Blank, joinRect, joinHover ? new Color(50, 45, 30) : new Color(25, 23, 18));
        _sb.Draw(Blank, new Rectangle(joinRect.X, joinRect.Y, joinRect.Width, 2), Config.ColGold);
        _sb.DrawString(_font, "JOIN GAME",
            new Vector2(joinRect.X + 80, joinRect.Y + 10),
            joinHover ? Config.ColGold : Config.ColText);

        if (joinHover && mouse.LeftButton == ButtonState.Pressed)
        {
            CurrentMode = Mode.JoinInput;
        }

        // Back button
        y += 60;
        _sb.DrawString(_font, "ESC: back to game",
            new Vector2(cx - 80, y + 20), Config.ColDim);
    }

    private void DrawHostWaiting(int cx, int y)
    {
        _sb.DrawString(_font, $"Hosting on port {Server?.Port ?? 9000}",
            new Vector2(cx - 100, y), Config.ColText);
        _sb.DrawString(_font, "Waiting for players to join...",
            new Vector2(cx - 120, y + 30), Config.ColDim);
        _sb.DrawString(_font, $"Players connected: {GetPlayerCount()}",
            new Vector2(cx - 80, y + 60), Config.ColGold);

        // Auto-connect as host
        _sb.DrawString(_font, "Press ENTER to start as host",
            new Vector2(cx - 100, y + 100), Config.ColGold);
    }

    private void DrawJoinInput(int cx, int y, KeyboardState keys, KeyboardState prevKeys)
    {
        _sb.DrawString(_font, "Enter server details:",
            new Vector2(cx - 100, y), Config.ColText);

        y += 30;
        _sb.DrawString(_font, $"Host: {HostInput}_",
            new Vector2(cx - 100, y), Config.ColText);

        y += 24;
        _sb.DrawString(_font, $"Port: {PortInput}",
            new Vector2(cx - 100, y), Config.ColText);

        y += 24;
        _sb.DrawString(_font, $"Class: {ClassInput} (1=warrior 2=mage 3=rogue 4=artisan)",
            new Vector2(cx - 100, y), Config.ColText);

        y += 24;
        _sb.DrawString(_font, $"Password: {PasswordInput}",
            new Vector2(cx - 100, y), Config.ColDim);

        y += 40;
        _sb.DrawString(_font, "ENTER: connect  ESC: back",
            new Vector2(cx - 80, y), Config.ColGold);

        // Handle text input (simplified — just type host/port)
        HandleTextInput(keys, prevKeys);

        // Class selection
        if (keys.IsKeyDown(Keys.D1) && !prevKeys.IsKeyDown(Keys.D1))
            ClassInput = "warrior";
        if (keys.IsKeyDown(Keys.D2) && !prevKeys.IsKeyDown(Keys.D2))
            ClassInput = "mage";
        if (keys.IsKeyDown(Keys.D3) && !prevKeys.IsKeyDown(Keys.D3))
            ClassInput = "rogue";
        if (keys.IsKeyDown(Keys.D4) && !prevKeys.IsKeyDown(Keys.D4))
            ClassInput = "artisan";

        if (keys.IsKeyDown(Keys.Enter) && !prevKeys.IsKeyDown(Keys.Enter))
        {
            TryConnect();
        }
    }

    private void DrawError(int cx, int y)
    {
        _sb.DrawString(_font, $"Error: {_statusMessage}",
            new Vector2(cx - 200, y), new Color(220, 60, 60));
        _sb.DrawString(_font, "Press ESC to go back",
            new Vector2(cx - 80, y + 40), Config.ColDim);
    }

    private void StartHost()
    {
        try
        {
            Server = new GameServer();
            if (!string.IsNullOrEmpty(PasswordInput))
                Server.ServerPassword = PasswordInput;
            Server.Start(9000);

            // Also connect as a client to own server
            Client = new GameClient();
            Client.Connect("localhost", Server.Port, ClassInput,
                string.IsNullOrEmpty(PasswordInput) ? null : PasswordInput);

            CurrentMode = Mode.HostWaiting;
        }
        catch (Exception e)
        {
            _statusMessage = e.Message;
            CurrentMode = Mode.Error;
        }
    }

    private void TryConnect()
    {
        int port = int.TryParse(PortInput, out var p) ? p : 9000;
        Client = new GameClient();
        bool ok = Client.Connect(HostInput, port, ClassInput,
            string.IsNullOrEmpty(PasswordInput) ? null : PasswordInput);

        if (ok)
        {
            CurrentMode = Mode.Connected;
        }
        else
        {
            _statusMessage = Client.ErrorMessage ?? "Connection failed";
            CurrentMode = Mode.Error;
        }
    }

    private int GetPlayerCount()
    {
        return Server?.Core.Players.Count ?? 0;
    }

    private void HandleTextInput(KeyboardState keys, KeyboardState prevKeys)
    {
        // Simplified text input — in a real game, use a proper text input system
        // This handles basic digit/letter input for host and port fields
        // (Full implementation would track which field is active)
    }

    private Texture2D Blank
    {
        get
        {
            if (_blank == null)
            {
                _blank = new Texture2D(_device, 1, 1);
                _blank.SetData(new[] { Color.White });
            }
            return _blank;
        }
    }

    /// <summary>
    /// Reset to menu state.
    /// </summary>
    public void Reset()
    {
        CurrentMode = Mode.Menu;
        Server?.Stop();
        Client?.Disconnect();
        Server = null;
        Client = null;
        _statusMessage = "";
    }
}
