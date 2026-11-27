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
                GpuReset.Register(_device, () =>
                {
                    try { _blank?.Dispose(); } catch { }
                    _blank = null;
                });
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
            var nodes = BuildNodes(game);
            _selected = Math.Clamp(_selected, 0, nodes.Count - 1);

            if (keys.IsKeyDown(Keys.Up) && !prevKeys.IsKeyDown(Keys.Up))
                _selected = (_selected + nodes.Count - 1) % nodes.Count;
            if (keys.IsKeyDown(Keys.Down) && !prevKeys.IsKeyDown(Keys.Down))
                _selected = (_selected + 1) % nodes.Count;

            // Enter — быстрый переход (только посещённые): ставим отложенный
            // переход, его исполняет GameController через ConsumeTravel()
            if (keys.IsKeyDown(Keys.Enter) && !prevKeys.IsKeyDown(Keys.Enter))
            {
                var node = nodes[_selected];
                if (node.Visited && !node.Current)
                    _pendingTravel = node.Key;
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

    // ────────────────────────── Обзор мира (карта) ──────────────────────────

    /// <summary>
    /// Узел на карте мира: локация + её позиция на оверворлде (у врат).
    /// </summary>
    private readonly record struct MapNode(string Key, string Name, int Gx, int Gy, bool Visited, bool Current);

    /// <summary>
    /// Собрать узлы карты: деревня, подземелье и все врата оверворлда.
    /// Кэшируется — Transitions статичны после генерации мира.
    /// </summary>
    private List<MapNode> BuildNodes(GameController game)
    {
        if (_nodeCache != null) return _nodeCache;
        var nodes = new List<MapNode> { new("village", "Город Этельгард",
            Aethelgard.World.Overworld.VillageCenter.Item1,
            Aethelgard.World.Overworld.VillageCenter.Item2,
            true, game.CurrentLocation == "village") };

        var seen = new HashSet<string> { "village" };
        foreach (var ((gx, gy), (to, _)) in game.Overworld.Transitions)
        {
            if (!seen.Add(to)) continue;   // первая пара врат на локацию
            if (!Locations.All.TryGetValue(to, out var def)) continue;
            nodes.Add(new MapNode(to, def.Name, gx, gy,
                game.VisitedLocations.Contains(to), game.CurrentLocation == to));
        }
        _nodeCache = nodes;
        return nodes;
    }

    private List<MapNode>? _nodeCache;

    private void InvalidateNodeCache(GameController game)
    {
        // Пересобрать при смене посещённости/текущей локации
        if (_nodeCache == null) return;
        foreach (var n in _nodeCache)
        {
            bool visited = n.Key == "village" || game.VisitedLocations.Contains(n.Key);
            bool current = game.CurrentLocation == n.Key;
            if (visited != n.Visited || current != n.Current)
            {
                _nodeCache = null;
                return;
            }
        }
    }

    private void DrawWorldOverview(GameController game, int sw, int sh)
    {
        InvalidateNodeCache(game);
        var nodes = BuildNodes(game);
        _selected = Math.Clamp(_selected, 0, nodes.Count - 1);

        K.Begin(_sb, _device, Mouse.GetState());

        var panel = new Rectangle(24, 16, sw - 48, sh - 56);
        K.Panel(panel, dimWorldBehind: false);
        K.Text("Карта мира", new Vector2(panel.X + 20, panel.Y + 14),
            UiKit.Theme.GoldBright, 2);

        // ── Расчёт области карты (вписать оверворлд) ──
        int mapLeft = panel.X + UiKit.Pad;
        int mapTop = panel.Y + 56;
        int sideW = 420;
        int mapW = panel.Width - UiKit.Pad * 2 - sideW - UiKit.Gap;
        int mapH = panel.Height - UiKit.Pad * 2 - 56 - 10;
        var ow = game.Overworld;
        int cell = Math.Max(2, Math.Min(mapW / ow.W, mapH / ow.H));
        int worldW = ow.W * cell, worldH = ow.H * cell;
        int ox = mapLeft + (mapW - worldW) / 2;
        int oy = mapTop + (mapH - worldH) / 2;

        // ── Карта оверворлда: тайлы с туманом войны (общая палитра) ──
        for (int y = 0; y < ow.H; y++)
            for (int x = 0; x < ow.W; x++)
            {
                var color = ow.Explored.Contains((x, y))
                    ? MapPalette.TileColor(ow, x, y, overworld: true)
                    : UiKit.Theme.Bg;
                _sb.Draw(Blank, new Rectangle(ox + x * cell, oy + y * cell, cell, cell), color);
            }

        // ── Врата на карте (даже не исследованные — путь видно по легенде) ──
        foreach (var ((gx, gy), _) in ow.Transitions)
        {
            _sb.Draw(Blank, new Rectangle(ox + gx * cell - 1, oy + gy * cell - 1,
                cell + 2, cell + 2), new Color(MapPalette.Gate, 200));
        }

        // ── Узлы локаций ──
        _nodeRects.Clear();
        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            bool sel = i == _selected;
            var nodeRect = new Rectangle(ox + n.Gx * cell - 9, oy + n.Gy * cell - 9, 18, 18);
            _nodeRects[n.Key] = nodeRect;

            Color nodeColor = sel ? UiKit.Theme.GoldBright
                : n.Current ? UiKit.Theme.Gold
                : n.Visited ? new Color(140, 200, 120)
                : new Color(120, 110, 95);
            // Ромб-маркер локации
            var c = nodeRect.Center;
            for (int r = 0; r < 8; r++)
            {
                _sb.Draw(Blank, new Rectangle(c.X - (8 - r), c.Y + r - 8, (8 - r) * 2, 1), nodeColor);
                _sb.Draw(Blank, new Rectangle(c.X - (8 - r), c.Y - r + 7, (8 - r) * 2, 1), nodeColor);
            }
            Frame(nodeRect, sel ? UiKit.Theme.GoldBright : new Color(0, 0, 0, 200));
            if (sel)
            {
                var pulse = new Rectangle(nodeRect.X - 3, nodeRect.Y - 3,
                    nodeRect.Width + 6, nodeRect.Height + 6);
                Frame(pulse, new Color(UiKit.Theme.Gold, 130));
            }

            // Подпись — у посещённых и у выбранной
            if ((n.Visited || sel) && _font != null && cell >= 4)
            {
                string label = n.Visited ? n.Name : "???";
                var lsz = _font.MeasureString(label);
                float lx = nodeRect.Center.X - lsz.X / 2;
                float ly = nodeRect.Bottom + 3;
                if (ly + lsz.Y > oy + worldH) ly = nodeRect.Y - lsz.Y - 3;
                _sb.DrawString(_font, label, new Vector2(lx + 1, ly + 1), new Color(0, 0, 0, 190));
                _sb.DrawString(_font, label, new Vector2(lx, ly),
                    sel ? UiKit.Theme.GoldBright : UiKit.Theme.Text);
            }

            // Тултип по наведению
            if (K.Hoverable(nodeRect))
            {
                _selected = i;   // наведение = выбор (UX: карта кликабельна)
                K.Tooltip(nodeRect, n.Visited ? n.Name : "???",
                    (n.Current ? "Вы здесь" : n.Visited ? "Посещено" : "Не исследовано",
                        n.Current ? UiKit.Theme.Gold : UiKit.Theme.TextDim),
                    (sel ? "Enter / клик — переход" : "↑↓ или клик — выбрать",
                        UiKit.Theme.TextMuted));
            }
        }

        // ── Маркер игрока (если в локации/подземелье — на деревню не указывает) ──
        if (game.WorldMode == "overworld" && game.CurrentLocation == null)
        {
            var px = ox + game.Player.X * cell;
            var py = oy + game.Player.Y * cell;
            _sb.Draw(Blank, new Rectangle(px - 2, py - 2, cell + 4, cell + 4),
                new Color(20, 16, 8));
            _sb.Draw(Blank, new Rectangle(px, py, cell, cell), MapPalette.Player);
        }

        // ── Правая панель: сводка по выбранной локации ──
        var sideRect = new Rectangle(panel.Right - UiKit.Pad - sideW, mapTop, sideW, mapH);
        DrawSidePanel(game, sideRect, nodes[_selected]);

        // ── Подсказка ──
        K.TextCentered("↑↓ / клик — выбор   Enter — переход (посещённые)   Tab — локальная карта   B — закрыть",
            new Rectangle(panel.X, panel.Bottom - 30, panel.Width, 24),
            UiKit.Theme.TextMuted, 4);

        // ── Клик по узлу — выбор, повторный клик — переход ──
        foreach (var (key, rect) in _nodeRects)
        {
            if (K.Hot(rect) && K.Clicked(rect))
            {
                int idx = nodes.FindIndex(n => n.Key == key);
                if (idx >= 0)
                {
                    if (_selected == idx && nodes[idx].Visited)
                        _pendingTravel = nodes[idx].Key;
                    _selected = idx;
                }
            }
        }

        K.End();
        K.EndInputFrame();
    }

    private void Frame(Rectangle r, Color c)
    {
        _sb.Draw(Blank, new Rectangle(r.X, r.Y, r.Width, 1), c);
        _sb.Draw(Blank, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), c);
        _sb.Draw(Blank, new Rectangle(r.X, r.Y, 1, r.Height), c);
        _sb.Draw(Blank, new Rectangle(r.Right - 1, r.Y, 1, r.Height), c);
    }

    /// <summary>Ключ локации для перехода по клику (обрабатывает GameController).</summary>
    public string? ConsumeTravel()
    {
        var t = _pendingTravel;
        _pendingTravel = null;
        return t;
    }

    private string? _pendingTravel;
    private readonly Dictionary<string, Rectangle> _nodeRects = new();
    private static readonly UiKit K = UiKit.Kit;

    private void DrawSidePanel(GameController game, Rectangle r, MapNode node)
    {
        K.Section(r);
        K.Text(node.Visited ? node.Name : "???",
            new Vector2(r.X + 12, r.Y + 10), UiKit.Theme.GoldBright, 3);
        int y = r.Y + 46;

        if (!node.Visited)
        {
            K.Text("Вы ещё не были здесь.", new Vector2(r.X + 12, y),
                UiKit.Theme.TextDim, 4, shadow: false);
            K.Text("Сведения появятся после визита.", new Vector2(r.X + 12, y + 20),
                UiKit.Theme.TextMuted, 4, shadow: false);
            return;
        }

        string selKey = node.Key;
        var def = Locations.All[selKey];

        string biome = def.Biome switch
        {
            "village" => "Город", "dungeon" => "Подземелье",
            "pine" => "Хвойный лес", "birch" => "Лиственный лес",
            "rock" => "Скалы", "autumn" => "Осенний лес",
            "swamp" => "Болото", "dead" => "Мёртвый лес",
            "meadow" => "Поляна", "beach" => "Побережье",
            "goblin" => "Лагерь гоблинов", "mine" => "Шахта",
            "ruins" => "Руины", _ => def.Biome,
        };
        K.Text($"Тип: {biome}   ·   Размер: {def.W}×{def.H}",
            new Vector2(r.X + 12, y), UiKit.Theme.Text, 4, shadow: false);
        y += 28;

        if (selKey == "dungeon")
        {
            K.Text($"Этажей: {Config.MaxDepth}, боссы на 5/10/15/20",
                new Vector2(r.X + 12, y), UiKit.Theme.TextDim, 4, shadow: false);
            y += 28;
        }
        if (selKey == "village")
        {
            K.Text("Ваш дом: торговцы, кузница,", new Vector2(r.X + 12, y),
                UiKit.Theme.TextDim, 4, shadow: false);
            K.Text("алхимик, доска заданий, старейшина.", new Vector2(r.X + 12, y + 18),
                UiKit.Theme.TextDim, 4, shadow: false);
            y += 46;
        }
        else
        {
            var monsters = Locations.MonstersOf(def.Biome)
                .Where(k => MonsterData.All.ContainsKey(k))
                .Select(k => MonsterData.All[k].Name).Distinct().ToList();
            K.Text("Монстры:", new Vector2(r.X + 12, y), UiKit.Theme.Text, 4, shadow: false);
            y += 20;
            foreach (var line in WrapText(string.Join(", ", monsters), r.Width - 24))
            {
                _sb.DrawString(_font!, line, new Vector2(r.X + 24, y), UiKit.Theme.TextDim);
                y += 18;
            }
            y += 6;

            var items = Locations.ItemsOf(def.Biome)
                .Select(ItemData.DisplayName).Distinct().ToList();
            K.Text("Ресурсы:", new Vector2(r.X + 12, y), UiKit.Theme.Text, 4, shadow: false);
            y += 20;
            foreach (var line in WrapText(string.Join(", ", items), r.Width - 24))
            {
                _sb.DrawString(_font!, line, new Vector2(r.X + 24, y), UiKit.Theme.TextDim);
                y += 18;
            }
        }

        // Кнопка быстрого перехода
        bool current = node.Current;
        if (node.Visited && !current)
        {
            var btn = new Rectangle(r.X + 12, r.Bottom - 44, r.Width - 24, 32);
            if (K.Button(btn, "Быстрый переход (Enter)", state: 1, size: 4))
                _pendingTravel = node.Key;
        }
        else if (current)
        {
            K.TextCentered("Вы находитесь здесь",
                new Rectangle(r.X + 12, r.Bottom - 44, r.Width - 24, 32),
                UiKit.Theme.TextMuted, 4);
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
