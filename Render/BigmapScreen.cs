using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
namespace Aethelgard.Render;

/// <summary>
/// Bigmap screen — full-screen map of current location.
/// Ported from Python render/bigmap_renderer.py.
///
/// Shows the entire current map with tiles, player position, monsters.
/// Key: B (was M in old version).
/// </summary>
public class BigmapScreen
{
    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _sb;
    private SpriteFont? _font;
    private Texture2D? _blank;

    public BigmapScreen(GraphicsDevice device, SpriteBatch sb)
    {
        _device = device;
        _sb = sb;
    }

    public void LoadFont(SpriteFont font) => _font = font;

    public void Draw(GameController game)
    {
        var d = game.CurrentMap;
        int sw = AethelgardGame.ScreenW, sh = AethelgardGame.ScreenH;

        // Dark background
        _sb.Draw(Blank, new Rectangle(0, 0, sw, sh), new Color(8, 7, 5));

        if (_font == null) return;

        // Title
        string title = game.WorldMode == "dungeon"
            ? $"Подземелье — этаж {game.Depth}"
            : "Карта открытого мира";
        _sb.DrawString(_font, title, new Vector2(20, 10), Config.ColGold);

        // Calculate scale to fit map on screen
        int margin = 40;
        int titleH = 40;
        int availW = sw - margin * 2;
        int availH = sh - margin * 2 - titleH;
        int cellW = availW / d.W;
        int cellH = availH / d.H;
        int cellSize = Math.Min(cellW, cellH);
        cellSize = Math.Max(1, cellSize);

        int mapW = d.W * cellSize;
        int mapH = d.H * cellSize;
        int offsetX = (sw - mapW) / 2;
        int offsetY = titleH + (sh - titleH - mapH) / 2;

        // Draw tiles
        for (int y = 0; y < d.H; y++)
        {
            for (int x = 0; x < d.W; x++)
            {
                var tile = d.Tiles[y][x];
                Color color;

                if (tile == Tile.Wall)
                {
                    // Check if it's a tree or rock for color variation
                    if (game.WorldMode == "overworld")
                    {
                        string kind = Overworld.WallKind(x, y);
                        color = kind.StartsWith("tree") ? new Color(40, 55, 30)
                              : kind == "rock" ? new Color(70, 65, 55)
                              : new Color(50, 45, 38);
                    }
                    else
                        color = new Color(35, 32, 28);
                }
                else if (tile == Tile.Floor)
                    color = new Color(55, 50, 42);
                else if (tile == Tile.Stairs)
                    color = Config.ColStairs;
                else if (tile == Tile.Exit)
                    color = new Color(180, 160, 100);
                else if (tile == Tile.UpStairs)
                    color = new Color(200, 180, 80);
                else if (tile == Tile.Gate)
                    color = new Color(140, 120, 70);
                else if (tile == Tile.Chest)
                    color = new Color(200, 170, 90);
                else if (tile == Tile.LockedDoor)
                    color = new Color(120, 50, 50);
                else
                    color = new Color(40, 35, 30);

                // If explored, show; else dark
                bool explored = d.Explored.Contains((x, y));
                if (!explored && game.WorldMode == "overworld")
                    color = new Color(20, 18, 15);
                else if (!explored)
                    color = new Color(15, 13, 10);

                _sb.Draw(Blank,
                    new Rectangle(offsetX + x * cellSize, offsetY + y * cellSize,
                        cellSize, cellSize), color);
            }
        }

        // Draw monsters (only on explored tiles)
        foreach (var m in d.Monsters)
        {
            if (!m.Alive) continue;
            if (!d.Explored.Contains((m.X, m.Y))) continue;
            Color mc = m.Boss ? new Color(220, 50, 50) : new Color(200, 100, 80);
            _sb.Draw(Blank,
                new Rectangle(offsetX + m.X * cellSize, offsetY + m.Y * cellSize,
                    cellSize, cellSize), mc);
        }

        // Draw NPCs
        foreach (var npc in game.AllNpcs())
        {
            if (npc.X >= 0 && npc.X < d.W && npc.Y >= 0 && npc.Y < d.H)
            {
                _sb.Draw(Blank,
                    new Rectangle(offsetX + npc.X * cellSize, offsetY + npc.Y * cellSize,
                        cellSize, cellSize), new Color(200, 180, 100));
            }
        }

        // Draw player (always visible — gold marker)
        _sb.Draw(Blank,
            new Rectangle(offsetX + game.Player.X * cellSize - 1,
                offsetY + game.Player.Y * cellSize - 1,
                cellSize + 2, cellSize + 2), Config.ColGold);

        // Draw items
        foreach (var ((x, y), _) in d.Items)
        {
            if (d.Explored.Contains((x, y)))
            {
                _sb.Draw(Blank,
                    new Rectangle(offsetX + x * cellSize + cellSize / 4,
                        offsetY + y * cellSize + cellSize / 4,
                        cellSize / 2, cellSize / 2),
                    new Color(180, 150, 80));
            }
        }

        // Hint
        _sb.DrawString(_font, "B/Esc: закрыть · Золотой — вы · Красный — босс · Жёлтый — NPC/предметы",
            new Vector2(20, sh - 24), Config.ColDim);
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
