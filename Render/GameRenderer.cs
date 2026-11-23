using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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
    /// Main draw: world tiles + entities + HUD. mouse — в дизайн-координатах 1280×720.
    /// </summary>
    public void Draw(GameController game, MouseState? mouse = null)
    {
        var d = game.CurrentMap;
        var player = game.Player;

        // Camera: игрок всегда в центре экрана; у края локации камера
        // упирается в границу карты (игрок смещается от центра)
        int worldW = AethelgardGame.ScreenW;
        int worldH = AethelgardGame.ScreenH - Config.HudHeight;
        _camX = player.X * Config.TileSize + Config.TileSize / 2 - worldW / 2;
        _camY = player.Y * Config.TileSize + Config.TileSize / 2 - worldH / 2;

        // Clamp camera
        _camX = Math.Max(0, Math.Min(_camX, Math.Max(0, d.W * Config.TileSize - worldW)));
        _camY = Math.Max(0, Math.Min(_camY, Math.Max(0, d.H * Config.TileSize - worldH)));

        // Draw world
        _sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
        DrawTiles(d, player);
        DrawItems(d);
        DrawNpcs(game);
        DrawEntities(d, player);
        DrawProjectiles(game);
        _sb.End();

        // Draw HUD + minimap (собственный Begin/End — раньше здесь был
        // вызов _sb.Draw без Begin, что падало в первый же кадр игры
        // сразу после выбора класса: окно зависало).
        _sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
        DrawHud(player, game, mouse);

        // Minimap (M toggles)
        if (game.MinimapVisible)
            DrawMinimap(d, player, game);
        _sb.End();

        // Draw message
        if (game.MessageTimer > 0 && game.LastMessage != null && _font != null)
        {
            var alpha = Math.Min(1f, game.MessageTimer);
            var color = Config.ColText * alpha;
            _sb.Begin();
            var size = _font.MeasureString(game.LastMessage);
            var pos = new Vector2(
                (AethelgardGame.ScreenW - size.X) / 2,
                AethelgardGame.ScreenH - Config.HudHeight - 30);
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

    private void DrawProjectiles(GameController game)
    {
        foreach (var p in game.Projectiles)
        {
            float px = p.X * Config.TileSize - _camX + Config.TileSize / 2f;
            float py = p.Y * Config.TileSize - _camY + Config.TileSize / 2f;
            int size = p.Kind is "fireball" or "lightning" ? 10 : 6;
            _sb.Draw(_blankTex,
                new Rectangle((int)px - size / 2, (int)py - size / 2, size, size),
                p.Color);
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
            // Спрайт монстра: PNG из assets/sprites или процедурный фолбэк
            int spr = m.Boss ? 58 : 46;
            var sprTex = SpriteStore.Get(_device, m.Boss ? "bosses" : "monsters", m.MonsterKey, m.Color);
            _sb.Draw(sprTex,
                new Rectangle((int)pos.X - spr / 2, (int)pos.Y - spr / 2 + 2, spr, spr),
                Color.White);
            // Boss outline
            if (m.Boss)
            {
                DrawCircleOutline(pos, 30, new Color(220, 50, 50));
            }

            // HP bar above monster if damaged
            if (m.Hp < m.MaxHp)
            {
                int barW = m.Boss ? 40 : 24;
                int barH = 3;
                int bx = (int)pos.X - barW / 2;
                int by = (int)pos.Y - (m.Boss ? 32 : 26);
                _sb.Draw(_blankTex, new Rectangle(bx, by, barW, barH),
                    new Color(40, 20, 20));
                int fillW = m.MaxHp > 0 ? barW * m.Hp / m.MaxHp : 0;
                _sb.Draw(_blankTex, new Rectangle(bx, by, fillW, barH),
                    Config.ColHp);
            }
        }

        // Draw player — спрайт класса (PNG или процедурный)
        var ppos = new Vector2(player.X * Config.TileSize - _camX + Config.TileSize / 2,
                                player.Y * Config.TileSize - _camY + Config.TileSize / 2);
        var pTex = SpriteStore.Get(_device, "player", player.ClassKey, Config.ColPlayer);
        _sb.Draw(pTex, new Rectangle((int)ppos.X - 25, (int)ppos.Y - 29, 50, 52), Color.White);
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

            // Спрайт NPC (PNG из assets/sprites/npc или процедурный)
            _sb.Draw(SpriteStore.Get(_device, "npc", npc.NpcType ?? "default", npc.Color),
                new Rectangle((int)pos.X - 26, (int)pos.Y - 30, 52, 54), Color.White);

            // Quest board — special marker
            if (npc.NpcType == "quest_board")
            {
                // Draw "!" mark above
                if (_font != null)
                    _sb.DrawString(_font, "!", new Vector2(pos.X - 3, pos.Y - 42),
                        new Color(240, 200, 90));
            }

            // NPC name above (when close to player)
            int dist = Math.Max(Math.Abs(npc.X - game.Player.X),
                                Math.Abs(npc.Y - game.Player.Y));
            if (dist <= 3 && _font != null)
            {
                var nameSize = _font.MeasureString(npc.Name);
                _sb.DrawString(_font, npc.Name,
                    new Vector2(pos.X - nameSize.X / 2, pos.Y - 42),
                    Config.ColText);
            }
        }
    }

    private bool IsVisible(int x, int y)
    {
        int sx = x * Config.TileSize - _camX;
        int sy = y * Config.TileSize - _camY;
        return sx > -Config.TileSize && sx < AethelgardGame.ScreenW
            && sy > -Config.TileSize && sy < AethelgardGame.ScreenH - Config.HudHeight;
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

    /// <summary>
    /// Мини-карта в правом верхнем углу: окно следует за игроком, показывает
    /// ТОЛЬКО исследованные клетки (тот же туман войны и та же палитра, что
    /// у большой карты — поэтому мини-карта всегда соответствует карте).
    /// </summary>
    private void DrawMinimap(Dungeon d, Player player, GameController game)
    {
        const int scale = Config.MinimapScale;
        const int maxW = 390, maxH = 260;
        int cellsX = Math.Min(d.W, maxW / scale);
        int cellsY = Math.Min(d.H, maxH / scale);

        // Окно миникарты следует за игроком и не выходит за границы карты
        int viewX = Math.Clamp(player.X - cellsX / 2, 0, Math.Max(0, d.W - cellsX));
        int viewY = Math.Clamp(player.Y - cellsY / 2, 0, Math.Max(0, d.H - cellsY));

        int ox = AethelgardGame.ScreenW - cellsX * scale - 12;
        int oy = 30;
        int w = cellsX * scale, h = cellsY * scale;

        // Подложка + рамка
        _sb.Draw(_blankTex, new Rectangle(ox - 4, oy - 4, w + 8, h + 8), new Color(10, 8, 8, 220));
        _sb.Draw(_blankTex, new Rectangle(ox - 2, oy - 2, w + 4, h + 4), new Color(120, 100, 60));
        _sb.Draw(_blankTex, new Rectangle(ox - 1, oy - 1, w + 2, h + 2), new Color(10, 8, 8));

        bool overworld = game.WorldMode != "dungeon" || game.CurrentMap.LocationKey != null;

        for (int y = 0; y < cellsY; y++)
        {
            int my = viewY + y;
            for (int x = 0; x < cellsX; x++)
            {
                int mx = viewX + x;
                Color c = d.Explored.Contains((mx, my))
                    ? MapPalette.TileColor(d, mx, my, overworld)
                    : MapPalette.Unexplored;
                _sb.Draw(_blankTex, new Rectangle(ox + x * scale, oy + y * scale, scale, scale), c);
            }
        }

        // Monsters (только на исследованных клетках)
        foreach (var m in d.Monsters)
        {
            if (!m.Alive || m.IsNpc) continue;
            int mx = m.X - viewX, my = m.Y - viewY;
            if (mx < 0 || my < 0 || mx >= cellsX || my >= cellsY) continue;
            if (!d.Explored.Contains((m.X, m.Y))) continue;
            _sb.Draw(_blankTex, new Rectangle(ox + mx * scale, oy + my * scale, scale, scale),
                m.Boss ? MapPalette.Boss : MapPalette.Monster);
        }

        // NPCs
        foreach (var npc in game.AllNpcs())
        {
            int nx = npc.X - viewX, ny = npc.Y - viewY;
            if (nx < 0 || ny < 0 || nx >= cellsX || ny >= cellsY) continue;
            if (!d.Explored.Contains((npc.X, npc.Y))) continue;
            _sb.Draw(_blankTex, new Rectangle(ox + nx * scale, oy + ny * scale, scale, scale),
                MapPalette.Npc);
        }

        // Items (только на исследованных клетках)
        foreach (var ((x, y), _) in d.Items)
        {
            int ix = x - viewX, iy = y - viewY;
            if (ix < 0 || iy < 0 || ix >= cellsX || iy >= cellsY) continue;
            if (!d.Explored.Contains((x, y))) continue;
            _sb.Draw(_blankTex, new Rectangle(ox + ix * scale, oy + iy * scale, scale, scale),
                MapPalette.Item);
        }

        // Player — золотой маркер 2×2 клетки
        int plx = player.X - viewX, ply = player.Y - viewY;
        if (plx >= 0 && ply >= 0 && plx < cellsX && ply < cellsY)
            _sb.Draw(_blankTex, new Rectangle(ox + plx * scale - 1, oy + ply * scale - 1, scale + 2, scale + 2),
                MapPalette.Player);
    }

    /// <summary>Хотбар (слоты 1–8) и панель активных навыков в HUD.</summary>
    private void DrawHotbar(GameController game, MouseState? mouse = null)
    {
        if (_font == null) return;
        var player = game.Player;
        int hx = 16;
        int hy = AethelgardGame.ScreenH - 44;

        // Общая подложка хотбара + панели навыков
        int skillsCount = Math.Min(player.ActiveSkills().Count, SkillKeysLabels.Length);
        int hotbarW = 8 * (Config.QuickbarSlotSize + Config.QuickbarGap) + 8;
        int skillsW = skillsCount * 74 + 8;
        _sb.Draw(_blankTex, new Rectangle(hx - 8, hy - 8, hotbarW + skillsCount * 74 + 24,
            Config.QuickbarSlotSize + 16), new Color(12, 10, 8, 190));

        for (int i = 0; i < 8; i++)
        {
            var rect = new Rectangle(hx + i * (Config.QuickbarSlotSize + Config.QuickbarGap),
                hy, Config.QuickbarSlotSize, Config.QuickbarSlotSize);
            _sb.Draw(_blankTex, new Rectangle(rect.X, rect.Y, rect.Width, rect.Height), new Color(30, 26, 22));
            // Внутренняя тень сверху (эффект «углубления»)
            _sb.Draw(_blankTex, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Color(0, 0, 0, 90));
            var itemKey = player.Quickbar[i];
            // Рамка слота
            var slotBorder = itemKey != null ? new Color(150, 126, 76) : new Color(64, 56, 46);
            _sb.Draw(_blankTex, new Rectangle(rect.X, rect.Y, rect.Width, 1), slotBorder);
            _sb.Draw(_blankTex, new Rectangle(rect.X, rect.Y + rect.Height - 1, rect.Width, 1), slotBorder);
            _sb.Draw(_blankTex, new Rectangle(rect.X, rect.Y, 1, rect.Height), slotBorder);
            _sb.Draw(_blankTex, new Rectangle(rect.X + rect.Width - 1, rect.Y, 1, rect.Height), slotBorder);

            if (itemKey != null)
            {
                Color c = ItemData.DisplayColor(itemKey);
                _sb.Draw(_blankTex, new Rectangle(rect.X + 8, rect.Y + 8, 32, 32), c);
                // Рамка иконки
                _sb.Draw(_blankTex, new Rectangle(rect.X + 8, rect.Y + 8, 32, 1), new Color(0, 0, 0, 120));
                _sb.Draw(_blankTex, new Rectangle(rect.X + 8, rect.Y + 39, 32, 1), new Color(0, 0, 0, 120));
                _sb.Draw(_blankTex, new Rectangle(rect.X + 8, rect.Y + 8, 1, 32), new Color(0, 0, 0, 120));
                _sb.Draw(_blankTex, new Rectangle(rect.X + 39, rect.Y + 8, 1, 32), new Color(0, 0, 0, 120));
                int count = player.ItemCount(itemKey);
                _sb.DrawString(_font, count > 99 ? "99+" : count.ToString(),
                    new Vector2(rect.X + 6, rect.Y + 30), Config.ColText);
            }
            _sb.DrawString(_font, $"{i + 1}", new Vector2(rect.X + 2, rect.Y + 2), Config.ColDim);

            // Подсказка при наведении на слот (название предмета)
            if (mouse.HasValue && itemKey != null && rect.Contains(mouse.Value.X, mouse.Value.Y))
                DrawTooltip(mouse.Value.X, mouse.Value.Y - 8,
                    ItemData.DisplayName(itemKey), ItemData.DisplayColor(itemKey));
        }

        // Active skills panel (F1–F4/F6–F8/F10)
        var skills = player.ActiveSkills();
        int sx = hx + 8 * (Config.QuickbarSlotSize + Config.QuickbarGap) + 16;
        for (int i = 0; i < skills.Count && i < SkillKeysLabels.Length; i++)
        {
            var skill = skills[i];
            float cd = player.ActiveSkillCd.GetValueOrDefault(skill.Key);
            var rect = new Rectangle(sx + i * 74, hy, 70, Config.QuickbarSlotSize);
            _sb.Draw(_blankTex, rect, cd > 0 ? new Color(24, 20, 18) : new Color(38, 32, 26));
            _sb.DrawString(_font, SkillKeysLabels[i], new Vector2(rect.X + 4, rect.Y + 2), Config.ColDim);
            _sb.DrawString(_font, skill.Name, new Vector2(rect.X + 4, rect.Y + 16), Config.ColText);
            _sb.DrawString(_font, $"{skill.Mana} MP", new Vector2(rect.X + 4, rect.Y + 32),
                new Color(100, 150, 220));
            if (cd > 0)
                _sb.DrawString(_font, $"{cd:F1}s", new Vector2(rect.X + 40, rect.Y + 32), Config.ColDmg);
        }
    }

    private static readonly string[] SkillKeysLabels =
        { "F1", "F2", "F3", "F4", "F6", "F7", "F8", "F10" };

    /// <summary>Тёмная панель-подсказка над курсором (название предмета/навыка).</summary>
    private void DrawTooltip(int x, int y, string text, Color accent)
    {
        var size = _font!.MeasureString(text);
        int w = (int)size.X + 16;
        int h = (int)size.Y + 10;
        x = Math.Clamp(x - w / 2, 4, AethelgardGame.ScreenW - w - 4);
        y = Math.Max(4, y - h);
        _sb.Draw(_blankTex, new Rectangle(x + 2, y + 2, w, h), new Color(0, 0, 0, 130));
        _sb.Draw(_blankTex, new Rectangle(x, y, w, h), new Color(16, 14, 11, 235));
        _sb.Draw(_blankTex, new Rectangle(x, y, w, 2), accent);
        _sb.DrawString(_font, text, new Vector2(x + 8, y + 5), Config.ColText);
    }

    /// <summary>Кнопки интерфейса HUD (как в python-версии UI): ярлык + режим.</summary>
    public static readonly (string Label, string Mode)[] HudButtons =
    {
        ("Инвентарь (I)", ScreenMode.Inventory),
        ("Статы (C)", ScreenMode.Stats),
        ("Навыки (N)", ScreenMode.Skills),
        ("Крафт (K)", ScreenMode.Craft),
        ("Квесты (L)", ScreenMode.Quests),
        ("Карта (M)", ScreenMode.Bigmap),
        ("Пауза (Esc)", ScreenMode.Pause),
    };

    /// <summary>Прямоугольники кнопок из последнего кадра (для обработки кликов).</summary>
    public IReadOnlyList<Rectangle> ButtonRects => _buttonRects;

    private readonly List<Rectangle> _buttonRects = new();

    /// <summary>Клик по кнопкам HUD (только нажатие, не удержание).</summary>
    public bool HandleButtonClick(MouseState mouse, MouseState prevMouse, GameController game)
    {
        if (mouse.LeftButton != ButtonState.Pressed
            || prevMouse.LeftButton == ButtonState.Pressed) return false;
        for (int i = 0; i < _buttonRects.Count && i < HudButtons.Length; i++)
        {
            if (_buttonRects[i].Contains(mouse.X, mouse.Y))
            {
                var mode = HudButtons[i].Mode;
                game.ScreenMode = game.ScreenMode == mode && mode != ScreenMode.Pause
                    ? ScreenMode.Game : mode;
                return true;
            }
        }
        return false;
    }

    private void DrawHud(Player player, GameController game, MouseState? mouse)
    {
        // HUD background
        _sb.Draw(_blankTex,
            new Rectangle(0, AethelgardGame.ScreenH - Config.HudHeight, AethelgardGame.ScreenW, Config.HudHeight),
            new Color(18, 16, 14, 220));

        if (_font == null) return;

        int y = AethelgardGame.ScreenH - Config.HudHeight + 10;
        int x = 16;

        // Class + Level + Location
        string loc = game.WorldMode == "dungeon"
            ? $"Подземелье {game.Depth}/{Config.MaxDepth}"
            : game.CurrentMap.LocationKey != null
                ? Locations.All.GetValueOrDefault(game.CurrentMap.LocationKey)?.Name ?? "Открытый мир"
                : "Открытый мир";
        if (game.WorldMode != "dungeon" && game.CurrentMap.LocationKey == null)
            loc = "Город Этельгард";
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
        _sb.DrawString(_font, $"Золото: {player.Gold}", new Vector2(x, y), Config.ColGold);

        // Equipment summary (right side) — на аккуратной панели
        int ex = AethelgardGame.ScreenW - 260;
        int ey = y - 60;
        int eqW = 246, eqH = 100;
        _sb.Draw(_blankTex, new Rectangle(ex - 8, ey - 6, eqW, eqH), new Color(12, 10, 8, 170));
        _sb.Draw(_blankTex, new Rectangle(ex - 8, ey - 6, eqW, 2), new Color(140, 118, 70, 200));
        _sb.DrawString(_font, "Снаряжение:", new Vector2(ex, ey), Config.ColGold);
        ey += 16;
        var eqSlots = new[] { ("W", player.Weapon), ("A", player.Armor),
            ("H", player.Head), ("L", player.Legs), ("B", player.Boots) };
        foreach (var (label, key) in eqSlots)
        {
            string itemText = key != null ? ItemData.DisplayName(key) : "-";
            _sb.DrawString(_font, $"{label}: {itemText}", new Vector2(ex, ey), Config.ColText);
            ey += 14;
        }

        // FPS (top right) — только при включённой настройке show_fps
        if (game.ShowFps)
            _sb.DrawString(_font, $"FPS: {game.Fps:F0}", new Vector2(AethelgardGame.ScreenW - 80, 8), Config.ColDim);

        // Controls hint — убран (просили); вместо него — кликабельные кнопки UI
        DrawHudButtons(game, mouse);

        // Hotbar + skills panel
        DrawHotbar(game, mouse);
    }

    /// <summary>Ряд кликабельных кнопок интерфейса слева сверху (вместо подсказок).</summary>
    private void DrawHudButtons(GameController game, MouseState? mouse)
    {
        if (_font == null) return;
        _buttonRects.Clear();
        int bx = 8, by = 4;
        int h = 24;
        for (int i = 0; i < HudButtons.Length; i++)
        {
            var label = HudButtons[i].Label;
            var size = _font.MeasureString(label);
            int w = (int)size.X + 14;
            var rect = new Rectangle(bx, by, w, h);
            bool hover = mouse.HasValue && rect.Contains(mouse.Value.X, mouse.Value.Y);
            bool active = game.ScreenMode == HudButtons[i].Mode;

            _sb.Draw(_blankTex, rect, active ? new Color(52, 43, 30)
                            : hover ? new Color(42, 36, 28) : new Color(18, 16, 13, 235));
            _sb.Draw(_blankTex, new Rectangle(rect.X, rect.Y, rect.Width, 1),
                active || hover ? Config.ColGold : new Color(84, 72, 56));
            _sb.Draw(_blankTex, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1),
                new Color(30, 26, 20));
            // Текст кнопки — по центру по горизонтали и вертикали
            Ui.TextCentered(_sb, _font, label, rect,
                active ? Config.ColGold : hover ? new Color(230, 214, 180) : new Color(170, 158, 134),
                shadow: false);

            _buttonRects.Add(rect);
            bx += w + 4;
        }
    }

    private void DrawBar(int x, int y, int w, int h, int value, int max, Color color)
    {
        // Background
        _sb.Draw(_blankTex, new Rectangle(x, y, w, h), new Color(24, 20, 16));
        // Fill + блик сверху для объёма
        int fillW = max > 0 ? w * value / max : 0;
        _sb.Draw(_blankTex, new Rectangle(x, y, fillW, h), color);
        _sb.Draw(_blankTex, new Rectangle(x, y, fillW, 2), Color.White * 0.25f);
        // Border
        _sb.Draw(_blankTex, new Rectangle(x, y, w, 1), Config.ColDim);
        _sb.Draw(_blankTex, new Rectangle(x, y + h - 1, w, 1), Config.ColDim);
        _sb.Draw(_blankTex, new Rectangle(x, y, 1, h), Config.ColDim);
        _sb.Draw(_blankTex, new Rectangle(x + w - 1, y, 1, h), Config.ColDim);
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
