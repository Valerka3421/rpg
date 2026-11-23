using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Aethelgard.Systems;
using Aethelgard.World;

namespace Aethelgard.Render;

/// <summary>
/// Большая карта (клавиша B) — полноэкранный вид текущей локации.
/// Полностью переписана: тот же туман войны и та же палитра, что у
/// мини-карты (MapPalette), поэтому обе карты всегда совпадают с миром.
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

    private string LocationName(GameController game)
    {
        var d = game.CurrentMap;
        if (!string.IsNullOrEmpty(d.LocationKey) && Locations.All.TryGetValue(d.LocationKey, out var def))
            return def.Name;
        if (game.WorldMode == "dungeon")
            return $"Подземелье — этаж {game.Depth}";
        return "Открытый мир";
    }

    public void Draw(GameController game)
    {
        var d = game.CurrentMap;
        int sw = AethelgardGame.ScreenW, sh = AethelgardGame.ScreenH;

        // Фон
        _sb.Draw(Blank, new Rectangle(0, 0, sw, sh), new Color(10, 9, 7));

        if (_font == null) return;

        bool overworld = game.WorldMode != "dungeon" || d.LocationKey != null;

        // ── Расчёт масштаба: вписать карту в доступную область ──
        int margin = 24;
        int topH = 52;
        int legendH = 34;
        int availW = sw - margin * 2;
        int availH = sh - margin * 2 - topH - legendH;
        int cell = Math.Max(2, Math.Min(availW / d.W, availH / d.H));
        int mapW = d.W * cell, mapH = d.H * cell;
        int ox = (sw - mapW) / 2;
        int oy = topH + (sh - topH - legendH - mapH) / 2;

        // ── Шапка: название локации + координаты игрока ──
        string name = LocationName(game);
        _sb.DrawString(_font, name, new Vector2(margin, 14), Config.ColGold);

        string stats = $"X: {game.Player.X}  Y: {game.Player.Y}";
        var statsSize = _font.MeasureString(stats);
        _sb.DrawString(_font, stats, new Vector2(sw - margin - statsSize.X, 14), Config.ColDim);

        // ── Рамка карты ──
        _sb.Draw(Blank, new Rectangle(ox - 4, oy - 4, mapW + 8, mapH + 8), new Color(120, 100, 60));
        _sb.Draw(Blank, new Rectangle(ox - 1, oy - 1, mapW + 2, mapH + 2), new Color(15, 13, 10));

        // ── Тайлы: только исследованные (туман войны), палитра общая с мини-картой ──
        for (int y = 0; y < d.H; y++)
        {
            for (int x = 0; x < d.W; x++)
            {
                Color color = d.Explored.Contains((x, y))
                    ? MapPalette.TileColor(d, x, y, overworld)
                    : MapPalette.Unexplored;
                _sb.Draw(Blank, new Rectangle(ox + x * cell, oy + y * cell, cell, cell), color);
            }
        }

        // ── Монстры (только на исследованных клетках) ──
        foreach (var m in d.Monsters)
        {
            if (!m.Alive || m.IsNpc) continue;
            if (!d.Explored.Contains((m.X, m.Y))) continue;
            _sb.Draw(Blank, new Rectangle(ox + m.X * cell, oy + m.Y * cell, cell, cell),
                m.Boss ? MapPalette.Boss : MapPalette.Monster);
        }

        // ── NPC ──
        foreach (var npc in game.AllNpcs())
        {
            if (npc.X < 0 || npc.X >= d.W || npc.Y < 0 || npc.Y >= d.H) continue;
            if (!d.Explored.Contains((npc.X, npc.Y))) continue;
            _sb.Draw(Blank, new Rectangle(ox + npc.X * cell, oy + npc.Y * cell, cell, cell),
                MapPalette.Npc);
        }

        // ── Предметы (только на исследованных клетках) ──
        foreach (var ((x, y), _) in d.Items)
        {
            if (!d.Explored.Contains((x, y))) continue;
            int s = Math.Max(2, cell / 2);
            _sb.Draw(Blank, new Rectangle(ox + x * cell + (cell - s) / 2, oy + y * cell + (cell - s) / 2, s, s),
                MapPalette.Item);
        }

        // ── Игрок: золотой маркер с обводкой ──
        _sb.Draw(Blank, new Rectangle(ox + game.Player.X * cell - 2, oy + game.Player.Y * cell - 2, cell + 4, cell + 4),
            new Color(20, 16, 8));
        _sb.Draw(Blank, new Rectangle(ox + game.Player.X * cell - 1, oy + game.Player.Y * cell - 1, cell + 2, cell + 2),
            MapPalette.Player);

        // ── Легенда ──
        DrawLegend(sw, sh - legendH - 6);
    }

    private void DrawLegend(int sw, int y)
    {
        var entries = new (Color, string)[]
        {
            (MapPalette.Player, "Вы"),
            (MapPalette.Npc, "NPC"),
            (MapPalette.Monster, "Монстр"),
            (MapPalette.Boss, "Босс"),
            (MapPalette.Item, "Предмет"),
            (MapPalette.Gate, "Врата"),
            (MapPalette.Stairs, "Лестница"),
            (MapPalette.Chest, "Сундук"),
            (MapPalette.Tree, "Дерево"),
            (MapPalette.Unexplored, "Не исследовано"),
        };

        int x = 24;
        foreach (var (color, label) in entries)
        {
            _sb.Draw(Blank, new Rectangle(x, y + 3, 10, 10), color);
            var size = _font!.MeasureString(label);
            _sb.DrawString(_font, label, new Vector2(x + 14, y + 2), Config.ColDim);
            x += 14 + (int)size.X + 18;
            if (x > sw - 150) break;
        }
    }
}
