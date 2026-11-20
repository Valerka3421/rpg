using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Aethelgard.Core;
using Aethelgard.Game;
using Aethelgard.Net;

namespace Aethelgard.Render;

/// <summary>
/// Multiplayer renderer — draws the game from server snapshots.
/// Ported from Python net/view.py.
///
/// Instead of reading from local Dungeon/Player, reads from Snapshot
/// received over the network.
/// </summary>
public class MpRenderer
{
    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _sb;
    private readonly TileRenderer _tiles;
    private SpriteFont? _font;
    private Texture2D? _blank;

    private int _camX, _camY;

    public MpRenderer(GraphicsDevice device, SpriteBatch sb)
    {
        _device = device;
        _sb = sb;
        _tiles = new TileRenderer(device);
    }

    public void LoadFont(SpriteFont font) => _font = font;

    /// <summary>
    /// Draw multiplayer game from snapshot.
    /// </summary>
    public void Draw(GameController game)
    {
        var snap = game.MpSnapshot;
        if (snap == null || _font == null)
        {
            // Waiting for first snapshot
            _sb.Begin();
            _sb.Draw(Blank, new Rectangle(0, 0, Game1.ScreenW, Game1.ScreenH),
                new Color(12, 12, 18));
            string msg = "Connecting to server...";
            var size = _font?.MeasureString(msg) ?? new Vector2(200, 20);
            if (_font != null)
                _sb.DrawString(_font, msg,
                    new Vector2((Game1.ScreenW - size.X) / 2,
                                (Game1.ScreenH - size.Y) / 2),
                    Config.ColDim);
            _sb.End();
            return;
        }

        // Find my player
        var myState = snap.Players.FirstOrDefault(p => p.Pid == snap.MyPid);
        if (myState == null)
        {
            _sb.Begin();
            _sb.Draw(Blank, new Rectangle(0, 0, Game1.ScreenW, Game1.ScreenH),
                new Color(12, 12, 18));
            _sb.DrawString(_font, "Waiting for player data...",
                new Vector2(Game1.ScreenW / 2 - 100, Game1.ScreenH / 2),
                Config.ColDim);
            _sb.End();
            return;
        }

        // Camera
        int viewW = Config.ViewW;
        _camX = myState.X * Config.TileSize - (Game1.ScreenW - viewW * Config.TileSize) / 2;
        _camY = myState.Y * Config.TileSize;
        _camX = Math.Max(0, Math.Min(_camX, snap.MapW * Config.TileSize - Game1.ScreenW));
        _camY = Math.Max(0, Math.Min(_camY, snap.MapH * Config.TileSize - (Game1.ScreenH - Config.HudHeight)));

        // Draw background tiles (procedural — we don't have the full tile map in snapshot)
        _sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
        DrawTiles(snap, myState);
        DrawItems(snap);
        DrawMonsters(snap);
        DrawPlayers(snap, myState);
        _sb.End();

        // HUD
        DrawHud(snap, myState, game);

        // Chat
        DrawChat(game);
    }

    private void DrawTiles(Snapshot snap, Snapshot.PlayerState myState)
    {
        // Without the full tile map, draw a simple floor background
        // In a full implementation, tiles would be delta-compressed in snapshots
        int startX = Math.Max(0, _camX / Config.TileSize);
        int startY = Math.Max(0, _camY / Config.TileSize);
        int endX = Math.Min(snap.MapW, startX + Config.ViewW + 2);
        int endY = Math.Min(snap.MapH, startY + Config.ViewH + 2);

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                // Simple: draw floor everywhere (server doesn't send tile data in this simplified version)
                int variant = Config.HashInt(x, y, 13, 8);
                float dist = MathF.Sqrt((x - myState.X) * (x - myState.X)
                    + (y - myState.Y) * (y - myState.Y));
                float light = Math.Max(0.35f, 1.0f - dist / Config.FovRadius);

                var tex = _tiles.GetTile("floor", variant, light);
                var pos = new Vector2(x * Config.TileSize - _camX,
                                       y * Config.TileSize - _camY);
                _sb.Draw(tex, pos, Color.White);
            }
        }
    }

    private void DrawItems(Snapshot snap)
    {
        foreach (var (x, y, key) in snap.Items)
        {
            var def = ItemData.Get(key);
            if (def == null) continue;
            var pos = new Vector2(x * Config.TileSize - _camX + Config.TileSize / 2,
                                   y * Config.TileSize - _camY + Config.TileSize / 2);
            DrawCircle(pos, 8, def.Color);
        }
    }

    private void DrawMonsters(Snapshot snap)
    {
        foreach (var m in snap.Monsters)
        {
            var pos = new Vector2(m.X * Config.TileSize - _camX + Config.TileSize / 2,
                                   m.Y * Config.TileSize - _camY + Config.TileSize / 2);
            int radius = m.Boss ? 24 : 16;
            DrawCircle(pos, radius, m.Color);
            if (m.Boss)
                DrawCircleOutline(pos, radius + 2, new Color(220, 50, 50));

            if (_font != null)
                _sb.DrawString(_font, m.Ch.ToString(),
                    pos - _font.MeasureString(m.Ch.ToString()) / 2, Color.White);

            // HP bar
            if (m.Hp < m.MaxHp)
            {
                int barW = m.Boss ? 40 : 24;
                int bx = (int)pos.X - barW / 2;
                int by = (int)pos.Y - radius - 8;
                _sb.Draw(Blank, new Rectangle(bx, by, barW, 3), new Color(40, 20, 20));
                int fillW = m.MaxHp > 0 ? barW * m.Hp / m.MaxHp : 0;
                _sb.Draw(Blank, new Rectangle(bx, by, fillW, 3), Config.ColHp);
            }
        }
    }

    private void DrawPlayers(Snapshot snap, Snapshot.PlayerState myState)
    {
        foreach (var p in snap.Players)
        {
            var pos = new Vector2(p.X * Config.TileSize - _camX + Config.TileSize / 2,
                                   p.Y * Config.TileSize - _camY + Config.TileSize / 2);
            bool isMe = p.Pid == snap.MyPid;
            int radius = isMe ? 20 : 18;
            DrawCircle(pos, radius, p.Color);

            // Highlight my player
            if (isMe)
                DrawCircleOutline(pos, radius + 2, Config.ColGold);

            if (_font != null)
            {
                _sb.DrawString(_font, p.Ch.ToString(),
                    pos - _font.MeasureString(p.Ch.ToString()) / 2, Color.White);

                // Player name/PID above
                string nameText = isMe ? "[YOU]" : $"P{p.Pid}";
                var nameSize = _font.MeasureString(nameText);
                _sb.DrawString(_font, nameText,
                    new Vector2(pos.X - nameSize.X / 2, pos.Y - radius - 16),
                    isMe ? Config.ColGold : Config.ColText);

                // HP bar
                if (p.Hp < p.MaxHp)
                {
                    int barW = 30;
                    int bx = (int)pos.X - barW / 2;
                    int by = (int)pos.Y + radius + 4;
                    _sb.Draw(Blank, new Rectangle(bx, by, barW, 3), new Color(40, 20, 20));
                    int fillW = p.MaxHp > 0 ? barW * p.Hp / p.MaxHp : 0;
                    _sb.Draw(Blank, new Rectangle(bx, by, fillW, 3), Config.ColHp);
                }
            }
        }
    }

    private void DrawHud(Snapshot snap, Snapshot.PlayerState myState, GameController game)
    {
        // HUD background
        _sb.Draw(Blank,
            new Rectangle(0, Game1.ScreenH - Config.HudHeight, Game1.ScreenW, Config.HudHeight),
            new Color(18, 16, 14, 220));

        if (_font == null) return;

        int y = Game1.ScreenH - Config.HudHeight + 10;
        int x = 16;

        string loc = snap.WorldMode == "dungeon"
            ? $"Dungeon F{snap.Depth}" : "Overworld";
        _sb.DrawString(_font, $"{myState.ClassName}  Lv.{myState.Level}  [{loc}]  (MP)",
            new Vector2(x, y), Config.ColText);
        y += 20;

        _sb.DrawString(_font, $"HP: {myState.Hp}/{myState.MaxHp}", new Vector2(x, y), Config.ColHp);
        DrawBar(x + 120, y + 4, 200, 12, myState.Hp, myState.MaxHp, Config.ColHp);
        y += 20;

        _sb.DrawString(_font, $"MP: {myState.Mana}/{myState.MaxMana}",
            new Vector2(x, y), new Color(100, 150, 220));
        DrawBar(x + 120, y + 4, 200, 12, myState.Mana, myState.MaxMana, new Color(100, 150, 220));
        y += 20;

        _sb.DrawString(_font, $"XP: {myState.Xp}/{myState.XpNext}", new Vector2(x, y), Config.ColXp);
        DrawBar(x + 120, y + 4, 200, 12, myState.Xp, myState.XpNext, Config.ColXp);
        y += 20;

        _sb.DrawString(_font, $"Gold: {myState.Gold}", new Vector2(x, y), Config.ColGold);

        // Player count
        _sb.DrawString(_font, $"Players: {snap.Players.Count}",
            new Vector2(Game1.ScreenW - 120, y), Config.ColText);

        // Controls
        _sb.DrawString(_font, "WASD:move  Space:attack  E:stairs  H:potion  Y:chat  Esc:menu",
            new Vector2(16, 8), Config.ColDim);
    }

    private void DrawChat(GameController game)
    {
        if (_font == null) return;

        int chatX = 16;
        int chatY = Game1.ScreenH - Config.HudHeight - 200;
        int chatW = 400;
        int chatH = 180;

        // Chat log (last 8 messages)
        var recent = game.ChatLog.TakeLast(8).ToList();
        for (int i = 0; i < recent.Count; i++)
        {
            _sb.DrawString(_font, recent[i], new Vector2(chatX, chatY + i * 18),
                Config.ColText);
        }

        // Chat input
        if (game.ChatActive)
        {
            _sb.Draw(Blank, new Rectangle(chatX - 4, chatY + 8 * 18 + 4, chatW, 24),
                new Color(20, 18, 14, 220));
            _sb.Draw(Blank, new Rectangle(chatX - 4, chatY + 8 * 18 + 4, chatW, 1),
                Config.ColGold);
            _sb.DrawString(_font, $"{game.ChatInput}_",
                new Vector2(chatX, chatY + 8 * 18 + 8), Config.ColText);
        }
    }

    private void DrawCircle(Vector2 center, int radius, Color color)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            int w = (int)Math.Sqrt(radius * radius - dy * dy);
            _sb.Draw(Blank, new Rectangle((int)center.X - w, (int)center.Y + dy, w * 2, 1), color);
        }
    }

    private void DrawCircleOutline(Vector2 center, int radius, Color color)
    {
        for (int angle = 0; angle < 360; angle += 5)
        {
            double rad = angle * Math.PI / 180.0;
            int x = (int)(center.X + Math.Cos(rad) * radius);
            int y = (int)(center.Y + Math.Sin(rad) * radius);
            _sb.Draw(Blank, new Rectangle(x, y, 2, 2), color);
        }
    }

    private void DrawBar(int x, int y, int w, int h, int value, int max, Color color)
    {
        _sb.Draw(Blank, new Rectangle(x, y, w, h), new Color(30, 25, 20));
        int fillW = max > 0 ? w * value / max : 0;
        _sb.Draw(Blank, new Rectangle(x, y, fillW, h), color);
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
}
