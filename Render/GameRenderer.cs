using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Aethelgard.Core;
using Aethelgard.Game;

namespace Aethelgard.Render;

/// <summary>
/// Main renderer: draws the game world (tiles, entities, HUD).
/// Ported from Python render/game_renderer.py.
///
/// Camera follows player. Tiles are cached by TileRenderer.
/// </summary>
public class GameRenderer
{
    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _sb;
    private readonly TileRenderer _tiles;
    private SpriteFont? _font;

    // Camera offset (pixels)
    private int _camX, _camY;

    public GameRenderer(GraphicsDevice device, SpriteBatch sb)
    {
        _device = device;
        _sb = sb;
        _tiles = new TileRenderer(device);
    }

    /// <summary>
    /// Main draw: world tiles + entities + HUD.
    /// </summary>
    public void Draw(GameController game)
    {
        var d = game.CurrentMap;
        var player = game.Player;

        // Camera: center on player
        int viewW = Config.ViewW;
        int viewH = Config.ViewH;
        _camX = player.X * Config.TileSize - (Game1.ScreenW - viewW * Config.TileSize) / 2;
        _camY = player.Y * Config.TileSize;

        // Clamp camera
        _camX = Math.Max(0, Math.Min(_camX, d.W * Config.TileSize - Game1.ScreenW));
        _camY = Math.Max(0, Math.Min(_camY, d.H * Config.TileSize - (Game1.ScreenH - Config.HudHeight)));

        // Draw tiles
        _sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
        DrawTiles(d, player);
        DrawItems(d);
        DrawNpcs(game);
        DrawEntities(d, player);
        _sb.End();

        // Draw HUD
        DrawHud(player, game);

        // Draw message
        if (game.MessageTimer > 0 && game.LastMessage != null && _font != null)
        {
            var alpha = Math.Min(1f, game.MessageTimer);
            var color = Config.ColText * alpha;
            _sb.Begin();
            var size = _font.MeasureString(game.LastMessage);
            var pos = new Vector2(
                (Game1.ScreenW - size.X) / 2,
                Game1.ScreenH - Config.HudHeight - 30);
            _sb.Draw(_blankTex,
                new Rectangle((int)pos.X - 8, (int)pos.Y - 4,
                    (int)size.X + 16, (int)size.Y + 8),
                new Color(0, 0, 0, (int)(200 * alpha)));
            _sb.DrawString(_font, game.LastMessage, pos, color);
            _sb.End();
        }
    }

    private void DrawTiles(Dungeon d, Player player)
    {
        int startX = _camX / Config.TileSize;
        int startY = _camY / Config.TileSize;
        int endX = Math.Min(d.W, startX + Config.ViewW + 2);
        int endY = Math.Min(d.H, startY + Config.ViewH + 2);

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                if (x < 0 || x >= d.W || y < 0 || y >= d.H) continue;

                string kind = TileRenderer.TileKindAt(d, x, y);
                int variant = Config.HashInt(x, y, 13, GetVariantCount(kind));

                // Lighting: distance from player
                float dist = MathF.Sqrt((x - player.X) * (x - player.X)
                    + (y - player.Y) * (y - player.Y));
                float light = Math.Max(0.35f, 1.0f - dist / Config.FovRadius);

                var tex = _tiles.GetTile(kind, variant, light);
                var pos = new Vector2(x * Config.TileSize - _camX,
                                       y * Config.TileSize - _camY);
                _sb.Draw(tex, pos, Color.White);
            }
        }
    }

    private void DrawEntities(Dungeon d, Player player)
    {
        // Draw monsters
        foreach (var m in d.Monsters)
        {
            if (!m.Alive) continue;
            if (!IsVisible(m.X, m.Y)) continue;

            var pos = new Vector2(m.X * Config.TileSize - _camX + Config.TileSize / 2,
                                   m.Y * Config.TileSize - _camY + Config.TileSize / 2);
            // Monster body — circle with size based on boss status
            int radius = m.Boss ? 24 : 16;
            DrawCircle(pos, radius, m.Color);
            // Boss outline
            if (m.Boss)
            {
                DrawCircleOutline(pos, radius + 2, new Color(220, 50, 50));
            }
            // Character letter
            DrawChar(m.Ch, pos, Color.White);

            // HP bar above monster if damaged
            if (m.Hp < m.MaxHp)
            {
                int barW = m.Boss ? 40 : 24;
                int barH = 3;
                int bx = (int)pos.X - barW / 2;
                int by = (int)pos.Y - radius - 8;
                _sb.Draw(_blankTex, new Rectangle(bx, by, barW, barH),
                    new Color(40, 20, 20));
                int fillW = m.MaxHp > 0 ? barW * m.Hp / m.MaxHp : 0;
                _sb.Draw(_blankTex, new Rectangle(bx, by, fillW, barH),
                    Config.ColHp);
            }
        }

        // Draw player
        var ppos = new Vector2(player.X * Config.TileSize - _camX + Config.TileSize / 2,
                                player.Y * Config.TileSize - _camY + Config.TileSize / 2);
        DrawCircle(ppos, 18, Config.ColPlayer);
        DrawChar('@', ppos, Color.White);
    }

    /// <summary>
    /// Draw items on the ground (small colored markers).
    /// </summary>
    private void DrawItems(Dungeon d)
    {
        foreach (var ((x, y), key) in d.Items)
        {
            if (!IsVisible(x, y)) continue;
            var def = ItemData.Get(key);
            if (def == null) continue;

            var pos = new Vector2(x * Config.TileSize - _camX + Config.TileSize / 2,
                                   y * Config.TileSize - _camY + Config.TileSize / 2);
            // Small item icon on ground
            DrawCircle(pos, 8, def.Color);
        }
    }

    /// <summary>
    /// Draw NPCs (guard, blacksmith, alchemist, innkeeper, elder, quest board).
    /// </summary>
    private void DrawNpcs(GameController game)
    {
        foreach (var npc in game.AllNpcs())
        {
            if (!IsVisible(npc.X, npc.Y)) continue;

            var pos = new Vector2(npc.X * Config.TileSize - _camX + Config.TileSize / 2,
                                   npc.Y * Config.TileSize - _camY + Config.TileSize / 2);

            // NPC body — larger circle
            int radius = 20;
            DrawCircle(pos, radius, npc.Color);

            // Quest board — special marker
            if (npc.NpcType == "quest_board")
            {
                // Draw "!" mark above
                if (_font != null)
                    _sb.DrawString(_font, "!", new Vector2(pos.X - 3, pos.Y - 30),
                        new Color(240, 200, 90));
            }

            // Character letter
            DrawChar(npc.Ch, pos, Color.White);

            // NPC name above (when close to player)
            int dist = Math.Max(Math.Abs(npc.X - game.Player.X),
                                Math.Abs(npc.Y - game.Player.Y));
            if (dist <= 3 && _font != null)
            {
                var nameSize = _font.MeasureString(npc.Name);
                _sb.DrawString(_font, npc.Name,
                    new Vector2(pos.X - nameSize.X / 2, pos.Y - radius - 16),
                    Config.ColText);
            }
        }
    }

    private bool IsVisible(int x, int y)
    {
        int sx = x * Config.TileSize - _camX;
        int sy = y * Config.TileSize - _camY;
        return sx > -Config.TileSize && sx < Game1.ScreenW
            && sy > -Config.TileSize && sy < Game1.ScreenH - Config.HudHeight;
    }

    private void DrawCircle(Vector2 center, int radius, Color color)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            int w = (int)Math.Sqrt(radius * radius - dy * dy);
            int x = (int)center.X - w;
            int y = (int)center.Y + dy;
            _sb.Draw(_blankTex, new Rectangle(x, y, w * 2, 1), color);
        }
    }

    private void DrawCircleOutline(Vector2 center, int radius, Color color)
    {
        for (int angle = 0; angle < 360; angle += 5)
        {
            double rad = angle * Math.PI / 180.0;
            int x = (int)(center.X + Math.Cos(rad) * radius);
            int y = (int)(center.Y + Math.Sin(rad) * radius);
            _sb.Draw(_blankTex, new Rectangle(x, y, 2, 2), color);
        }
    }

    private void DrawChar(char ch, Vector2 pos, Color color)
    {
        // Simple: draw a small rectangle as the character marker
        // In full port, use SpriteFont
        if (_font == null) return;
        var text = ch.ToString();
        var size = _font.MeasureString(text);
        _sb.DrawString(_font, text, pos - size / 2, color);
    }

    private void DrawHud(Player player, GameController game)
    {
        // HUD background
        _sb.Draw(_blankTex,
            new Rectangle(0, Game1.ScreenH - Config.HudHeight, Game1.ScreenW, Config.HudHeight),
            new Color(18, 16, 14, 220));

        if (_font == null) return;

        int y = Game1.ScreenH - Config.HudHeight + 10;
        int x = 16;

        // Class + Level + Location
        string loc = game.WorldMode == "dungeon"
            ? $"Dungeon F{game.Depth}"
            : "Overworld";
        _sb.DrawString(_font, $"{player.ClassName}  Lv.{player.Level}  [{loc}]",
            new Vector2(x, y), Config.ColText);
        y += 20;

        // HP bar
        _sb.DrawString(_font, $"HP: {player.Hp}/{player.MaxHp}", new Vector2(x, y), Config.ColHp);
        DrawBar(x + 120, y + 4, 200, 12, player.Hp, player.MaxHp, Config.ColHp);
        y += 20;

        // Mana bar
        _sb.DrawString(_font, $"MP: {player.Mana}/{player.MaxMana}", new Vector2(x, y), new Color(100, 150, 220));
        DrawBar(x + 120, y + 4, 200, 12, player.Mana, player.MaxMana, new Color(100, 150, 220));
        y += 20;

        // XP bar
        _sb.DrawString(_font, $"XP: {player.Xp}/{player.XpNext}", new Vector2(x, y), Config.ColXp);
        DrawBar(x + 120, y + 4, 200, 12, player.Xp, player.XpNext, Config.ColXp);
        y += 20;

        // Gold
        _sb.DrawString(_font, $"Gold: {player.Gold}", new Vector2(x, y), Config.ColGold);

        // Equipment summary (right side)
        int ex = Game1.ScreenW - 260;
        int ey = y - 60;
        _sb.DrawString(_font, "Eq:", new Vector2(ex, ey), Config.ColDim);
        ey += 16;
        var eqSlots = new[] { ("W", player.Weapon), ("A", player.Armor),
            ("H", player.Head), ("L", player.Legs), ("B", player.Boots) };
        foreach (var (label, key) in eqSlots)
        {
            string itemText = key != null ? ItemData.DisplayName(key) : "-";
            _sb.DrawString(_font, $"{label}: {itemText}", new Vector2(ex, ey), Config.ColText);
            ey += 14;
        }

        // FPS (top right)
        _sb.DrawString(_font, $"FPS: {game.Fps:F0}", new Vector2(Game1.ScreenW - 80, 8), Config.ColDim);

        // Controls hint
        _sb.DrawString(_font, "WASD:move  Space:attack  F:stairs  I:inv  N:skills  H:potion  B:map  T:talk",
            new Vector2(16, 8), Config.ColDim);
    }

    private void DrawBar(int x, int y, int w, int h, int value, int max, Color color)
    {
        // Background
        _sb.Draw(_blankTex, new Rectangle(x, y, w, h), new Color(30, 25, 20));
        // Fill
        int fillW = max > 0 ? w * value / max : 0;
        _sb.Draw(_blankTex, new Rectangle(x, y, fillW, h), color);
        // Border
        _sb.Draw(_blankTex, new Rectangle(x, y, w, 1), Config.ColDim);
        _sb.Draw(_blankTex, new Rectangle(x, y + h - 1, w, 1), Config.ColDim);
    }

    private Texture2D _blankTex
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
    private Texture2D? _blank;

    /// <summary>
    /// Load font (called after LoadContent).
    /// </summary>
    public void LoadFont(SpriteFont font)
    {
        _font = font;
    }

    private int GetVariantCount(string kind)
    {
        return kind switch
        {
            "wall" => 3,
            "floor" => 12,
            "grass_pine" or "grass_birch" or "grass_autumn"
                or "grass_swamp" or "grass_dark" or "grass_meadow"
                or "grass_dead" or "grass_rock" => 8,
            "road" => 4,
            "village_floor" => 4,
            _ => 1,
        };
    }
}
