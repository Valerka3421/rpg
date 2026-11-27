using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Aethelgard.Data;
namespace Aethelgard.Render;

/// <summary>
/// Дерево навыков: вкладки «Класс | Ковка | Алхимия | Инженерия».
/// Узлы классовой ветки — с иконками из assets/skills/class,
/// ветки крафта — из assets/skills/craft. Вкладки и узлы кликабельны.
/// Полоса «ПЕРЕХОД НА НОВЫЙ КЛАСС» видна всегда (до уровня — заблокирована).
/// </summary>
public class SkillScreen
{
    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _sb;
    private SpriteFont? _font;
    private Texture2D? _blank;

    public int CurrentTab = 0;  // 0=class, 1=blacksmith, 2=alchemy, 3=magic_eng
    public string? HoveredNode;

    private static readonly string[] TabNames = { "Класс", "Ковка", "Алхимия", "Инженерия" };
    private static readonly string[] TabKeys = { "", "blacksmith", "alchemy", "magic_engineering" };

    /// <summary>Клик «отпущен и снова нажат»: защита от повтора в каждом кадре.</summary>
    private bool _clickArmed = true;

    public SkillScreen(GraphicsDevice device, SpriteBatch sb)
    {
        _device = device;
        _sb = sb;
    }

    public void LoadFont(SpriteFont font) => _font = font;

    /// <summary>
    /// Draw the skill tree overlay.
    /// Returns the action the user clicked on ("learn:<key>" / "promote:<key>"), or null.
    /// Клик возвращается только один раз на нажатие (не каждый кадр).
    /// </summary>
    public string? Draw(Player player, MouseState mouse)
    {
        int sw = AethelgardGame.ScreenW;
        int sh = AethelgardGame.ScreenH;

        // «Свежий» клик: нажатие, не продолжающееся с прошлого кадра
        bool freshClick = mouse.LeftButton == ButtonState.Pressed && _clickArmed;
        if (mouse.LeftButton == ButtonState.Released) _clickArmed = true;
        else if (freshClick) _clickArmed = false;

        // Dark overlay
        _sb.Draw(Blank, new Rectangle(0, 0, sw, sh), new Color(0, 0, 0, 180));

        // Panel
        int panelW = 1560, panelH = 950;
        int px = (sw - panelW) / 2;
        int py = (sh - panelH) / 2;
        _sb.Draw(Blank, new Rectangle(px, py, panelW, panelH),
            new Color(20, 18, 14, 240));
        _sb.Draw(Blank, new Rectangle(px, py, panelW, 2), Config.ColGold);
        _sb.Draw(Blank, new Rectangle(px, py + panelH - 2, panelW, 2), Config.ColGold);

        if (_font == null) return null;
        var font = _font;   // локаль: вызовы методов сбрасывают null-state поля (_font)

        // Title
        _sb.DrawString(_font, "ДРЕВО НАВЫКОВ", new Vector2(px + 20, py + 14), Config.ColGold);
        var classDef = ClassData.Get(player.ClassKey);
        string className = classDef?.Name ?? player.ClassKey;
        _sb.DrawString(_font, $"{className}  Ур.{player.Level}",
            new Vector2(px + panelW - 320, py + 14), Config.ColText);

        // Skill points badge
        if (player.SkillPoints > 0)
        {
            string badge = $"Очки навыков: {player.SkillPoints}";
            _sb.DrawString(_font, badge, new Vector2(px + 20, py + 46), Config.ColGold);
        }

        // Tabs (кликабельные, текст по центру)
        HoveredNode = null;
        int tabY = py + 78;
        int tabX = px + 20;
        for (int i = 0; i < TabNames.Length; i++)
        {
            string label = TabNames[i];
            if (i > 0)
            {
                int lvl = player.CraftLevels.GetValueOrDefault(TabKeys[i], 1);
                label += $" ({lvl})";
            }
            var size = _font.MeasureString(label);
            var rect = new Rectangle(tabX, tabY, (int)size.X + 36, 34);
            bool active = i == CurrentTab;
            _sb.Draw(Blank, rect,
                active ? new Color(60, 50, 30) : new Color(30, 28, 22));
            var bc = active ? Config.ColGold : new Color(50, 45, 38);
            _sb.Draw(Blank, new Rectangle(tabX, tabY, rect.Width, 1), bc);
            _sb.Draw(Blank, new Rectangle(tabX, tabY + rect.Height - 1, rect.Width, 1), bc);

            Ui.TextCentered(_sb, _font, label, rect, active ? Config.ColGold : Config.ColDim, shadow: false);

            if (rect.Contains(mouse.X, mouse.Y) && freshClick && !active)
                CurrentTab = i;
            tabX += rect.Width + 10;
        }

        // Content area
        int contentY = tabY + 46;
        int contentH = panelH - (contentY - py) - 18;

        string? clicked = null;
        if (CurrentTab == 0)
            clicked = DrawClassTree(player, px + 20, contentY, panelW - 40, contentH, mouse, freshClick);
        else
            clicked = DrawCraftTab(player, TabKeys[CurrentTab], px + 20, contentY,
                panelW - 40, contentH, mouse);

        // Tooltip
        if (HoveredNode != null)
            DrawTooltip(HoveredNode, player, mouse);

        return clicked;
    }

    /// <summary>Базовый класс игрока (ветка древа) для иконок.</summary>
    private static string BaseKeyOf(Player player)
    {
        var def = ClassData.Get(player.ClassKey);
        while (def != null && def.Tier > 1)
            def = ClassData.Get(def.Base);
        return def?.Key ?? "warrior";
    }

    private static string? IconKeyOf(string nodeKey)
    {
        // w_0 → warrior_0, m_3 → mage_3, r_5 → rogue_5, a_7 → artisan_7, d_1 → default_1
        if (nodeKey.Length >= 2 && nodeKey[1] == '_')
            return nodeKey[0] switch
            {
                'w' => "warrior_" + nodeKey[2..],
                'm' => "mage_" + nodeKey[2..],
                'r' => "rogue_" + nodeKey[2..],
                'a' => "artisan_" + nodeKey[2..],
                'd' => "default_" + nodeKey[2..],
                _ => null,
            };
        return null;
    }

    private string? DrawClassTree(Player player, int x, int y, int w, int h,
        MouseState mouse, bool freshClick)
    {
        var nodes = SkillTree.GetNodes(player);
        if (nodes.Length == 0)
        {
            if (_font != null)
                _sb.DrawString(_font, "У этого класса нет древа навыков.",
                    new Vector2(x + 10, y + 10), Config.ColDim);
            return null;
        }

        var def = ClassData.Data(player.ClassKey);

        // Полоса эволюции видна всегда, если у класса есть следующие ступени
        var promoKeys = def.Next.Length > 0 ? def.Next : Array.Empty<string>();
        int idx = Math.Min(player.ClassChanges, ClassData.ClassChangeLevels.Length - 1);
        int promoReqLevel = ClassData.ClassChangeLevels[idx];
        bool promosUnlocked = ClassData.AvailablePromotions(player).Length > 0;
        int promoH = promoKeys.Length > 0 ? 110 : 0;
        int treeH = h - promoH;
        _sb.Draw(Blank, new Rectangle(x, y, w, treeH), new Color(17, 15, 12));
        _sb.Draw(Blank, new Rectangle(x, y, w, 1), new Color(48, 42, 33));

        // Hex positions: center + ring of 11 → 2 кольца (0 центр, 1-6 ближнее, 7-11 дальнее)
        int centerX = x + w / 2;
        int centerY = y + treeH / 2 - 8;
        int radius = 56;

        var positions = new List<(int X, int Y)>();
        positions.Add((centerX, centerY));
        for (int i = 0; i < 5; i++)
        {
            double angle = 2 * Math.PI * i / 5 - Math.PI / 2;
            positions.Add((centerX + (int)(Math.Cos(angle) * 190), centerY + (int)(Math.Sin(angle) * 150)));
        }
        for (int i = 0; i < 6; i++)
        {
            double angle = 2 * Math.PI * i / 6 + Math.PI / 6;
            positions.Add((centerX + (int)(Math.Cos(angle) * 380), centerY + (int)(Math.Sin(angle) * 220)));
        }

        string? clicked = null;
        string baseKey = BaseKeyOf(player);

        // Соединительные линии между узлами убраны: пунктирные линии
        // артефактили плотной сеткой и мешали читать дерево. Кольца
        // и иконки сами показывают структуру ветки.

        // Draw nodes
        for (int i = 0; i < nodes.Length && i < positions.Count; i++)
        {
            var node = nodes[i];
            var (nx, ny) = positions[i];

            bool learned = player.LearnedNodes.Contains(node.Key);
            bool available = !learned && player.Level >= node.Level && player.SkillPoints > 0;
            bool locked = player.Level < node.Level;

            Color nodeColor;
            if (learned) nodeColor = new Color(80, 160, 80);
            else if (available) nodeColor = new Color(200, 170, 60);
            else if (locked) nodeColor = new Color(60, 55, 45);
            else nodeColor = new Color(80, 75, 65);

            DrawHex(nx, ny, radius, nodeColor, learned);

            // Иконка узла из assets/skills/class/<базовый класс>_<индекс>.png
            var icon = Ui.SkillIcon(_device, "class", $"{baseKey}_{i}");
            if (icon != null)
            {
                int isz = radius * 2 - 22;
                _sb.Draw(icon, new Rectangle(nx - isz / 2, ny - isz / 2 - 8, isz, isz),
                    learned ? Color.White : Color.Multiply(Color.White, 0.55f));
                // Уровень требования — под иконкой
                string reqText = learned ? "изучено" : $"Ур.{node.Level}";
                var reqSize = _font!.MeasureString(reqText);
                _sb.DrawString(_font, reqText, new Vector2(nx - reqSize.X / 2, ny + radius - 26),
                    learned ? Color.White : (locked ? new Color(200, 110, 90) : Config.ColText));
            }
            else
            {
                string reqText = learned ? "+" : $"Ур.{node.Level}";
                var reqSize = _font!.MeasureString(reqText);
                _sb.DrawString(_font, reqText, new Vector2(nx - reqSize.X / 2, ny - reqSize.Y / 2 - 2),
                    learned ? Color.White : (locked ? new Color(200, 110, 90) : Config.ColText));
            }

            // Node name — по центру под гексом
            string label = node.Name.Length > 20 ? node.Name[..19] + "…" : node.Name;
            var labelSize = _font!.MeasureString(label);
            _sb.DrawString(_font, label, new Vector2(nx - labelSize.X / 2, ny + radius + 6),
                learned ? new Color(120, 190, 110) : Config.ColDim);

            // Hover and click
            var nodeRect = new Rectangle(nx - radius, ny - radius - 8, radius * 2, radius * 2 + 14);
            if (nodeRect.Contains(mouse.X, mouse.Y))
            {
                HoveredNode = node.Key;
                _sb.Draw(Blank, new Rectangle(nx - radius - 2, ny - radius - 10,
                    radius * 2 + 4, radius * 2 + 18),
                    new Color(240, 210, 90, 40));
                if (freshClick && !learned)
                    clicked = "learn:" + node.Key;
            }
        }

        // Bottom hint
        if (_font != null)
        {
            string hint = "Наведи для подробностей. Клик — изучить (нужно очко навыков и уровень).";
            _sb.DrawString(_font, hint,
                new Vector2(x + 10, y + treeH - 26), Config.ColDim);
        }

        // ── Эволюция класса: всегда видна, до нужного уровня — заблокирована ──
        if (promoKeys.Length > 0 && _font != null)
        {
            int promoY = y + treeH + 8;
            _sb.Draw(Blank, new Rectangle(x, promoY, w, promoH - 8),
                new Color(30, 24, 14));
            _sb.Draw(Blank, new Rectangle(x, promoY, w, 1), new Color(150, 120, 60));
            _sb.DrawString(_font, "ЭВОЛЮЦИЯ КЛАССА:",
                new Vector2(x + 10, promoY + 8), Config.ColGold);
            if (!promosUnlocked)
                _sb.DrawString(_font, $"доступно с уровня {promoReqLevel}",
                    new Vector2(x + 240, promoY + 8), new Color(200, 120, 90));

            int bx = x + 10;
            foreach (var pk in promoKeys)
            {
                var pdef = ClassData.Get(pk);
                if (pdef == null) continue;
                var size = _font.MeasureString(pdef.Name);
                var rect = new Rectangle(bx, promoY + 36, (int)size.X + 76, 52);
                bool hover = rect.Contains(mouse.X, mouse.Y);
                _sb.Draw(Blank, rect, hover ? new Color(64, 50, 26) : new Color(44, 36, 24));
                var bc = promosUnlocked ? (hover ? Config.ColGold : new Color(150, 120, 60))
                                        : new Color(80, 70, 55);
                _sb.Draw(Blank, new Rectangle(rect.X, rect.Y, rect.Width, 1), bc);
                _sb.Draw(Blank, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), bc);
                _sb.Draw(Blank, new Rectangle(rect.X, rect.Y, 1, rect.Height), bc);
                _sb.Draw(Blank, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), bc);

                // Иконка класса из assets/skills/class/<ключ>_0.png
                var icon = Ui.SkillIcon(_device, "class", pk + "_0");
                if (icon != null)
                    _sb.Draw(icon, new Rectangle(rect.X + 8, rect.Y + 8, 36, 36), Color.White);
                else
                    DrawFilledCircle(rect.X + 24, rect.Y + 26, 11, pdef.Color);
                _sb.DrawString(_font, pdef.Name,
                    new Vector2(rect.X + 54, rect.Y + 16),
                    promosUnlocked ? (hover ? Config.ColGold : Config.ColText)
                                   : new Color(130, 120, 105));
                bx += rect.Width + 12;

                if (hover && freshClick && promosUnlocked)
                    clicked = "promote:" + pk;
            }
            _sb.DrawString(_font, "Клик — сменить класс (навыки и снаряжение сохраняются)",
                new Vector2(x + 10, promoY + promoH - 22), Config.ColDim);
        }

        return clicked;
    }

    private string? DrawCraftTab(Player player, string branchKey,
        int x, int y, int w, int h, MouseState mouse)
    {
        if (!SkillTree.CraftBranches.TryGetValue(branchKey, out var branch))
            return null;

        // Branch icon
        int lvl = player.CraftLevels.GetValueOrDefault(branchKey, 1);
        var branchIcon = Ui.SkillIcon(_device, "craft",
            branchKey + "_" + Math.Clamp(lvl - 1, 0, 7));

        int headerX = x + 10;
        if (branchIcon != null)
        {
            _sb.Draw(branchIcon, new Rectangle(x + 10, y + 8, 52, 52), Color.White);
            headerX = x + 74;
        }

        if (_font != null)
        {
            _sb.DrawString(_font, branch.Name, new Vector2(headerX, y + 10), Config.ColGold);
            _sb.DrawString(_font, branch.Desc, new Vector2(headerX, y + 34),
                Config.ColDim);
            _sb.DrawString(_font, $"Уровень: {lvl}/25  Опыт: {player.CraftXp.GetValueOrDefault(branchKey, 0)}/{branch.XpNext * lvl}",
                new Vector2(headerX, y + 56), Config.ColText);
        }

        // XP bar
        int barX = x + 10;
        int barY = y + 82;
        int barW = w - 20;
        int xp = player.CraftXp.GetValueOrDefault(branchKey, 0);
        int xpNext = branch.XpNext * lvl;
        _sb.Draw(Blank, new Rectangle(barX, barY, barW, 12),
            new Color(30, 28, 22));
        int fillW = xpNext > 0 ? barW * xp / xpNext : 0;
        _sb.Draw(Blank, new Rectangle(barX, barY, fillW, 12),
            new Color(100, 180, 110));

        // Recipe list
        int listY = y + 108;
        _sb.DrawString(_font, "Рецепты:", new Vector2(x + 10, listY),
            Config.ColText);
        listY += 30;

        foreach (var (reqLevel, recipes) in branch.Recipes)
        {
            foreach (var recipeKey in recipes)
            {
                var def = ItemData.Get(recipeKey);
                if (def == null) continue;

                // Прогрессия: рецепт открыт, когда и ветка крафта, и уровень
                // персонажа достаточны
                bool byBranch = lvl >= reqLevel;
                bool byChar = player.Level >= def.LevelReq;
                bool unlocked = byBranch && byChar;
                string lockText = !byChar ? $"нужен ур. {def.LevelReq}"
                    : !byBranch ? $"нужна ветка ур. {reqLevel}" : "";

                int rowH = 52;
                var rect = new Rectangle(x + 10, listY, w - 20, rowH);
                _sb.Draw(Blank, rect,
                    unlocked ? new Color(30, 35, 25) : new Color(20, 18, 14));
                _sb.Draw(Blank, new Rectangle(x + 10, listY, w - 20, 1),
                    new Color(50, 45, 38));

                string name = def.Name;
                string reqStr = unlocked ? "" : $"  ({lockText})";
                _sb.DrawString(_font, $"{name}{reqStr}",
                    new Vector2(x + 18, listY + 8),
                    unlocked ? Config.ColText : new Color(150, 140, 125));

                // Materials needed (из реальных рецептов) — на второй строке
                var variants = ItemData.RecipesOf(recipeKey);
                if (variants.Count > 0)
                {
                    string mats = string.Join(", ", variants[0].Materials
                        .Select(m => $"{ItemData.DisplayName(m.Key)}×{m.Value}"));
                    bool afford = variants[0].Materials.All(m =>
                        player.Inventory.GetValueOrDefault(m.Key) >= m.Value);
                    _sb.DrawString(_font, mats,
                        new Vector2(x + 18, listY + 30),
                        unlocked ? (afford ? new Color(120, 180, 110) : new Color(160, 130, 110))
                                 : new Color(110, 100, 88));
                }

                listY += rowH + 4;
            }
        }

        return null;
    }

    private void DrawFilledCircle(int cx, int cy, int r, Color color)
    {
        for (int dy = -r; dy <= r; dy++)
        {
            int w = (int)Math.Sqrt(r * r - dy * dy);
            _sb.Draw(Blank, new Rectangle(cx - w, cy + dy, w * 2, 1), color);
        }
    }

    private void DrawHex(int cx, int cy, int r, Color fill, bool filled)
    {
        var points = new Vector2[6];
        for (int i = 0; i < 6; i++)
        {
            double angle = Math.PI / 3 * i;
            points[i] = new Vector2(
                cx + r * (float)Math.Cos(angle),
                cy + r * (float)Math.Sin(angle));
        }

        if (filled)
        {
            for (int i = 0; i < 6; i++)
            {
                var p0 = new Vector2(cx, cy);
                var p1 = points[i];
                var p2 = points[(i + 1) % 6];
                DrawTriangle(p0, p1, p2, fill);
            }
        }

        for (int i = 0; i < 6; i++)
        {
            var p1 = points[i];
            var p2 = points[(i + 1) % 6];
            DrawLine(p1, p2, new Color(100, 90, 70));
        }
    }

    private void DrawTriangle(Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        float minY = Math.Min(a.Y, Math.Min(b.Y, c.Y));
        float maxY = Math.Max(a.Y, Math.Max(b.Y, c.Y));
        for (int y = (int)minY; y <= (int)maxY; y++)
        {
            var intersections = new List<float>();
            CheckEdge(a, b, y, intersections);
            CheckEdge(b, c, y, intersections);
            CheckEdge(c, a, y, intersections);
            if (intersections.Count >= 2)
            {
                intersections.Sort();
                int x0 = (int)intersections[0];
                int x1 = (int)intersections[^1];
                _sb.Draw(Blank, new Rectangle(x0, y, x1 - x0, 1), color);
            }
        }
    }

    private void CheckEdge(Vector2 a, Vector2 b, int y, List<float> intersections)
    {
        if ((a.Y <= y && b.Y > y) || (b.Y <= y && a.Y > y))
        {
            float t = (y - a.Y) / (b.Y - a.Y);
            intersections.Add(a.X + t * (b.X - a.X));
        }
    }

    private void DrawLine(Vector2 a, Vector2 b, Color color)
    {
        int steps = (int)Vector2.Distance(a, b);
        for (int i = 0; i <= steps; i++)
        {
            float t = steps > 0 ? (float)i / steps : 0;
            int x = (int)(a.X + t * (b.X - a.X));
            int y = (int)(a.Y + t * (b.Y - a.Y));
            _sb.Draw(Blank, new Rectangle(x, y, 1, 1), color);
        }
    }

    private void DrawDottedLine(int x0, int y0, int x1, int y1, Color color)
    {
        int dx = x1 - x0, dy = y1 - y0;
        int dist = (int)Math.Sqrt(dx * dx + dy * dy);
        int dashLen = 3, gapLen = 9;
        int pos = 0;
        while (pos < dist)
        {
            int end = Math.Min(pos + dashLen, dist);
            float t0 = (float)pos / dist;
            float t1 = (float)end / dist;
            int sx = (int)(x0 + t0 * dx);
            int sy = (int)(y0 + t0 * dy);
            int ex = (int)(x0 + t1 * dx);
            int ey = (int)(x0 + t1 * dy);
            DrawLine(new Vector2(sx, sy), new Vector2(ex, ey), color);
            pos = end + gapLen;
        }
    }

    private void DrawTooltip(string nodeKey, Player player, MouseState mouse)
    {
        if (_font == null) return;
        var nodes = SkillTree.GetNodes(player);
        var node = nodes.FirstOrDefault(n => n.Key == nodeKey);
        if (node == null) return;

        var lines = new List<string> { node.Name, node.Desc };

        bool learned = player.LearnedNodes.Contains(node.Key);
        if (learned)
            lines.Add("Изучено");
        else if (player.Level < node.Level)
            lines.Add($"Требуется уровень {node.Level}");
        else if (player.SkillPoints < 1)
            lines.Add("Нет очков навыков");
        else
            lines.Add("Доступно — кликните, чтобы изучить");

        int maxW = 0;
        foreach (var line in lines)
        {
            var size = _font.MeasureString(line);
            if (size.X > maxW) maxW = (int)size.X;
        }
        int ttW = maxW + 20;
        int ttH = lines.Count * 24 + 14;
        int ttX = mouse.X + 20;
        int ttY = mouse.Y + 20;
        if (ttX + ttW > AethelgardGame.ScreenW) ttX = mouse.X - ttW - 8;
        if (ttY + ttH > AethelgardGame.ScreenH) ttY = mouse.Y - ttH - 8;

        _sb.Draw(Blank, new Rectangle(ttX, ttY, ttW, ttH),
            new Color(15, 13, 10, 230));
        _sb.Draw(Blank, new Rectangle(ttX, ttY, ttW, 1), Config.ColGold);

        for (int i = 0; i < lines.Count; i++)
        {
            var color = i == 0 ? Config.ColGold : Config.ColText;
            if (i == lines.Count - 1 && learned) color = new Color(100, 200, 100);
            _sb.DrawString(_font, lines[i],
                new Vector2(ttX + 10, ttY + 7 + i * 24), color);
        }
    }

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
}
