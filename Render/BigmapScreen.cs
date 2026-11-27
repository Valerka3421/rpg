using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Aethelgard.Systems;
using Aethelgard.World;

namespace Aethelgard.Render;

/// <summary>
/// Большая карта (клавиши B / M) — два режима на одном экране:
///  • «Обзор мира» (по умолчанию): список всех доступных локаций с краткой
///    сводкой (монстры, предметы, размер), меткой «посещено» и быстрым
///    переходом (Enter — только в посещённые локации);
///  • «Локальная карта» (Tab): тайловый вид текущей локации с той же
///    палитрой (MapPalette) и буквенными глифами, что у легенды.
/// </summary>
public class BigmapScreen
{
    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _sb;
    private SpriteFont? _font;
    private Texture2D? _blank;

    private bool _worldView = true;   // false — локальная карта
    private int _selected = 0;        // индекс в списке локаций (обзор мира)

    // Кэш списка ключей локаций: Locations.All статичен, но раньше оба пути
    // (ввод и отрисовка) строили .ToList() каждый кадр
    private static List<string>? _locKeys;
    private static List<string> LocKeys => _locKeys ??= Locations.All.Keys.ToList();

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

    /// <summary>
    /// Обработка ввода на экране карты. Возвращает true — закрыть карту.
    /// </summary>
    public bool HandleInput(KeyboardState keys, KeyboardState prevKeys, GameController game)
    {
        // B / M — закрыть (открытие обрабатывает GameController)
        if ((keys.IsKeyDown(Keys.B) && !prevKeys.IsKeyDown(Keys.B))
            || (keys.IsKeyDown(Keys.M) && !prevKeys.IsKeyDown(Keys.M)))
            return true;

        // Tab — переключение обзор мира ↔ локальная карта
        if (keys.IsKeyDown(Keys.Tab) && !prevKeys.IsKeyDown(Keys.Tab))
            _worldView = !_worldView;

        if (_worldView)
        {
            var keysList = LocKeys;
            _selected = Math.Clamp(_selected, 0, keysList.Count - 1);

            if (keys.IsKeyDown(Keys.Up) && !prevKeys.IsKeyDown(Keys.Up))
                _selected = (_selected + keysList.Count - 1) % keysList.Count;
            if (keys.IsKeyDown(Keys.Down) && !prevKeys.IsKeyDown(Keys.Down))
                _selected = (_selected + 1) % keysList.Count;

            // Enter — быстрый переход (только посещённые)
            if (keys.IsKeyDown(Keys.Enter) && !prevKeys.IsKeyDown(Keys.Enter))
            {
                string key = keysList[_selected];
                if (game.FastTravel(key))
                    return true; // перешли — закрываем карту
            }
        }
        return false;
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
        int sw = AethelgardGame.ScreenW, sh = AethelgardGame.ScreenH;

        // Фон
        _sb.Draw(Blank, new Rectangle(0, 0, sw, sh), new Color(10, 9, 7));
        if (_font == null) return;

        if (_worldView)
            DrawWorldOverview(game, sw, sh);
        else
            DrawLocalMap(game, sw, sh);

        // Подсказка по клавишам
        string hint = _worldView
            ? "↑↓ — выбор   Enter — переход (посещённые)   Tab — локальная карта   B — закрыть"
            : "Tab — обзор мира   B — закрыть";
        var hintSize = _font.MeasureString(hint);
        _sb.DrawString(_font, hint, new Vector2((sw - hintSize.X) / 2, sh - 26), Config.ColDim);
    }

    // ────────────────────────── Обзор мира ──────────────────────────

    private void DrawWorldOverview(GameController game, int sw, int sh)
    {
        _sb.DrawString(_font!, "Обзор мира", new Vector2(24, 14), Config.ColGold);

        var keysList = LocKeys;
        _selected = Math.Clamp(_selected, 0, keysList.Count - 1);

        // Левая колонка — список локаций
        int listX = 24, listY = 52, rowH = 26, listW = 340;
        int visibleRows = (sh - listY - 40) / rowH;
        int scroll = Math.Max(0, Math.Min(_selected - visibleRows + 1, keysList.Count - visibleRows));

        for (int i = scroll; i < keysList.Count && i < scroll + visibleRows; i++)
        {
            string key = keysList[i];
            var def = Locations.All[key];
            bool visited = game.VisitedLocations.Contains(key);
            bool current = game.CurrentLocation == key;
            bool sel = i == _selected;

            int y = listY + (i - scroll) * rowH;
            if (sel)
                _sb.Draw(Blank, new Rectangle(listX - 6, y - 3, listW, rowH - 2), new Color(50, 42, 26));

            Color nameColor = current ? Config.ColGold
                : visited ? Color.White : new Color(110, 100, 88);
            string marker = current ? "▶ " : visited ? "• " : "  ";
            _sb.DrawString(_font!, marker + (visited ? def.Name : "???"),
                new Vector2(listX, y), nameColor);
        }

        // Правая колонка — сводка по выбранной локации
        int sx = listX + listW + 30;
        int sy = listY;
        string selKey = keysList[_selected];
        var selDef = Locations.All[selKey];
        bool selVisited = game.VisitedLocations.Contains(selKey);

        _sb.DrawString(_font!, selVisited ? selDef.Name : "Неизвестная местность",
            new Vector2(sx, sy), Config.ColGold);
        sy += 30;

        if (!selVisited)
        {
            _sb.DrawString(_font!, "Вы ещё не были здесь.",
                new Vector2(sx, sy), Config.ColDim);
            _sb.DrawString(_font!, "Сведения появятся после визита.",
                new Vector2(sx, sy + 20), Config.ColDim);
            return;
        }

        // Краткая сводка
        string biome = selDef.Biome switch
        {
            "village" => "Город",
            "dungeon" => "Подземелье",
            "pine" => "Хвойный лес", "birch" => "Лиственный лес",
            "rock" => "Скалы", "autumn" => "Осенний лес",
            "swamp" => "Болото", "dead" => "Мёртвый лес",
            "meadow" => "Поляна", "beach" => "Побережье",
            "goblin" => "Лагерь гоблинов", "mine" => "Шахта",
            "ruins" => "Руины",
            _ => selDef.Biome,
        };
        _sb.DrawString(_font!, $"Тип: {biome}", new Vector2(sx, sy), Color.White); sy += 22;
        _sb.DrawString(_font!, $"Размер: {selDef.W}×{selDef.H}", new Vector2(sx, sy), Color.White); sy += 22;

        if (selKey == "dungeon")
        {
            _sb.DrawString(_font!, $"Этажей: {Config.MaxDepth}", new Vector2(sx, sy), Color.White); sy += 22;
        }

        if (selKey == "village")
        {
            _sb.DrawString(_font!, "Ваш дом: торговцы, кузница, доска заданий.",
                new Vector2(sx, sy), Color.White); sy += 22;
        }
        else
        {
            var monsters = Locations.MonstersOf(selDef.Biome)
                .Where(k => MonsterData.All.ContainsKey(k))
                .Select(k => MonsterData.All[k].Name)
                .Distinct().ToList();
            _sb.DrawString(_font!, "Монстры:", new Vector2(sx, sy), Color.White); sy += 22;
            foreach (var line in WrapText(string.Join(", ", monsters), 340))
            {
                _sb.DrawString(_font!, line, new Vector2(sx + 14, sy), Config.ColDim); sy += 20;
            }

            var items = Locations.ItemsOf(selDef.Biome)
                .Select(ItemData.DisplayName).Distinct().ToList();
            _sb.DrawString(_font!, "Ресурсы:", new Vector2(sx, sy + 4), Color.White); sy += 26;
            foreach (var line in WrapText(string.Join(", ", items), 340))
            {
                _sb.DrawString(_font!, line, new Vector2(sx + 14, sy), Config.ColDim); sy += 20;
            }
        }
    }

    private List<string> WrapText(string text, int maxW)
    {
        var lines = new List<string>();
        foreach (var word in text.Split(' '))
        {
            if (lines.Count == 0 || _font!.MeasureString(lines[^1] + " " + word).X > maxW)
                lines.Add(word);
            else
                lines[^1] += " " + word;
        }
        return lines;
    }

    // ────────────────────────── Локальная карта ──────────────────────────

    private void DrawLocalMap(GameController game, int sw, int sh)
    {
        var d = game.CurrentMap;
        bool overworld = game.WorldMode != "dungeon" || d.LocationKey != null;

        // ── Шапка ──
        _sb.DrawString(_font!, LocationName(game), new Vector2(24, 14), Config.ColGold);
        string stats = $"X: {game.Player.X}  Y: {game.Player.Y}";
        var statsSize = _font!.MeasureString(stats);
        _sb.DrawString(_font, stats, new Vector2(sw - 24 - statsSize.X, 14), Config.ColDim);

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

        // ── Глифы тайлов (врата/лестницы/сундуки) — при достаточном масштабе ──
        if (cell >= 6)
            DrawGlyphs(d, ox, oy, cell, x => MapPalette.TileGlyph(d, x.Item1, x.Item2));

        // ── Монстры (только на исследованных клетках) ──
        foreach (var m in d.Monsters)
        {
            if (!m.Alive || m.IsNpc) continue;
            if (!d.Explored.Contains((m.X, m.Y))) continue;
            DrawCell(ox, oy, cell, m.X, m.Y, m.Boss ? MapPalette.Boss : MapPalette.Monster,
                m.Boss ? MapPalette.BossGlyph : MapPalette.MonsterGlyph);
        }

        // ── NPC ──
        foreach (var npc in game.AllNpcs())
        {
            if (npc.X < 0 || npc.X >= d.W || npc.Y < 0 || npc.Y >= d.H) continue;
            if (!d.Explored.Contains((npc.X, npc.Y))) continue;
            DrawCell(ox, oy, cell, npc.X, npc.Y, MapPalette.Npc, MapPalette.NpcGlyph);
        }

        // ── Предметы (только на исследованных клетках) ──
        foreach (var ((x, y), _) in d.Items)
        {
            if (!d.Explored.Contains((x, y))) continue;
            DrawCell(ox, oy, cell, x, y, MapPalette.Item, MapPalette.ItemGlyph);
        }

        // ── Игрок: золотой маркер с обводкой ──
        DrawCell(ox, oy, cell, game.Player.X, game.Player.Y, MapPalette.Player, MapPalette.PlayerGlyph, outlined: true);

        // ── Легенда ──
        DrawLegend(sw, sh - legendH - 6);
    }

    private void DrawCell(int ox, int oy, int cell, int x, int y, Color color, char? glyph,
        bool outlined = false)
    {
        if (outlined)
        {
            _sb.Draw(Blank, new Rectangle(ox + x * cell - 2, oy + y * cell - 2, cell + 4, cell + 4),
                new Color(20, 16, 8));
        }
        _sb.Draw(Blank, new Rectangle(ox + x * cell, oy + y * cell, cell, cell), color);

        if (glyph.HasValue && cell >= 6 && _font != null)
        {
            var size = _font.MeasureString(glyph.Value.ToString());
            float gx = ox + x * cell + (cell - size.X) / 2f;
            float gy = oy + y * cell + (cell - size.Y) / 2f;
            // Тёмная обводка-подложка для читаемости на любом фоне
            _sb.DrawString(_font, glyph.Value.ToString(), new Vector2(gx + 1, gy + 1), new Color(0, 0, 0, 180));
            _sb.DrawString(_font, glyph.Value.ToString(), new Vector2(gx, gy), color);
        }
    }

    private void DrawGlyphs(Dungeon d, int ox, int oy, int cell,
        Func<(int, int), char?> glyphAt)
    {
        // Глиф + светлая заливка клетки (как в DrawCell легенды): раньше глиф
        // рисовался только чёрным «подложечным» слоем и на тёмных клетках
        // превращался в невидимую тёмную кляксу.
        for (int y = 0; y < d.H; y++)
            for (int x = 0; x < d.W; x++)
            {
                if (!d.Explored.Contains((x, y))) continue;
                var g = glyphAt((x, y));
                if (g.HasValue && _font != null)
                {
                    var color = MapPalette.GlyphColor(g.Value);
                    _sb.Draw(Blank, new Rectangle(ox + x * cell, oy + y * cell, cell, cell), color);
                    var size = _font.MeasureString(g.Value.ToString());
                    var gx = ox + x * cell + (cell - size.X) / 2f;
                    var gy = oy + y * cell + (cell - size.Y) / 2f;
                    _sb.DrawString(_font, g.Value.ToString(), new Vector2(gx + 1, gy + 1),
                        new Color(0, 0, 0, 180));
                    _sb.DrawString(_font, g.Value.ToString(), new Vector2(gx, gy), Color.White);
                }
            }
    }

    private void DrawLegend(int sw, int y)
    {
        var entries = new (Color, char?, string)[]
        {
            (MapPalette.Player, MapPalette.PlayerGlyph, "Вы"),
            (MapPalette.Npc, MapPalette.NpcGlyph, "NPC"),
            (MapPalette.Monster, MapPalette.MonsterGlyph, "Монстр"),
            (MapPalette.Boss, MapPalette.BossGlyph, "Босс"),
            (MapPalette.Item, MapPalette.ItemGlyph, "Предмет"),
            (MapPalette.Gate, MapPalette.GateGlyph, "Врата"),
            (MapPalette.Stairs, MapPalette.StairsGlyph, "Спуск"),
            (MapPalette.UpStairs, MapPalette.UpStairsGlyph, "Подъём"),
            (MapPalette.Chest, MapPalette.ChestGlyph, "Сундук"),
            (MapPalette.Unexplored, null, "Не исследовано"),
        };

        int x = 24;
        foreach (var (color, glyph, label) in entries)
        {
            _sb.Draw(Blank, new Rectangle(x, y + 3, 10, 10), color);
            if (glyph.HasValue)
            {
                var gs = _font!.MeasureString(glyph.Value.ToString());
                _sb.DrawString(_font, glyph.Value.ToString(),
                    new Vector2(x + 5 - gs.X / 2, y + 2 - gs.Y / 2 + 4), new Color(0, 0, 0, 220));
            }
            var size = _font!.MeasureString(label);
            _sb.DrawString(_font, label, new Vector2(x + 14, y + 2), Config.ColDim);
            x += 14 + (int)size.X + 18;
            if (x > sw - 150) break;
        }
    }
}
