using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Aethelgard.Core;

namespace Aethelgard.Render;

/// <summary>
/// Skill tree screen — hexagonal node layout with class and craft tabs.
/// Ported from Python render/screens_renderer.py:draw_skills.
///
/// Layout:
/// - Top: title + class name + skill points badge
/// - Tabs: Class | Blacksmithing | Alchemy | Magic Engineering
/// - Class tab: 7 hexagonal nodes in a tree pattern
/// - Craft tab: branch info + recipe list
/// - Hover: tooltip with node details
///
/// Controls:
/// - Q/E: switch tabs
/// - 1-7: learn node / craft recipe
/// - N: close
/// </summary>
public class SkillScreen
{
    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _sb;
    private SpriteFont? _font;
    private Texture2D? _blank;

    public int CurrentTab = 0;  // 0=class, 1=blacksmith, 2=alchemy, 3=magic_eng
    public string? HoveredNode;

    private static readonly string[] TabNames = { "Class", "Smithing", "Alchemy", "Magic" };
    private static readonly string[] TabKeys = { "", "blacksmith", "alchemy", "magic_engineering" };

    public SkillScreen(GraphicsDevice device, SpriteBatch sb)
    {
        _device = device;
        _sb = sb;
    }

    public void LoadFont(SpriteFont font) => _font = font;

    /// <summary>
    /// Draw the skill tree overlay.
    /// Returns the node key the user clicked on (for learning), or null.
    /// </summary>
    public string? Draw(Player player, MouseState mouse)
    {
        int sw = Game1.ScreenW;
        int sh = Game1.ScreenH;

        // Dark overlay
        _sb.Draw(Blank, new Rectangle(0, 0, sw, sh), new Color(0, 0, 0, 180));

        // Panel
        int panelW = 700, panelH = 500;
        int px = (sw - panelW) / 2;
        int py = (sh - panelH) / 2;
        _sb.Draw(Blank, new Rectangle(px, py, panelW, panelH),
            new Color(20, 18, 14, 240));
        _sb.Draw(Blank, new Rectangle(px, py, panelW, 2), Config.ColGold);
        _sb.Draw(Blank, new Rectangle(px, py + panelH - 2, panelW, 2), Config.ColGold);

        if (_font == null) return null;

        // Title
        _sb.DrawString(_font, "SKILL TREE", new Vector2(px + 16, py + 10), Config.ColGold);
        var classDef = ClassData.Get(player.ClassKey);
        string className = classDef?.Name ?? player.ClassKey;
        _sb.DrawString(_font, $"{className}  Lv.{player.Level}",
            new Vector2(px + panelW - 200, py + 10), Config.ColText);

        // Skill points badge
        if (player.SkillPoints > 0)
        {
            string badge = $"● {player.SkillPoints} skill points";
            _sb.DrawString(_font, badge, new Vector2(px + 16, py + 34), Config.ColGold);
        }

        // Tabs
        HoveredNode = null;
        int tabY = py + 60;
        int tabX = px + 16;
        for (int i = 0; i < TabNames.Length; i++)
        {
            var rect = new Rectangle(tabX, tabY, 140, 24);
            bool active = i == CurrentTab;
            _sb.Draw(Blank, rect,
                active ? new Color(60, 50, 30) : new Color(30, 28, 22));
            _sb.Draw(Blank, new Rectangle(tabX, tabY, 140, 1),
                active ? Config.ColGold : new Color(50, 45, 38));

            var tabColor = active ? Config.ColGold : Config.ColDim;
            // Show craft level if applicable
            string label = TabNames[i];
            if (i > 0)
            {
                int lvl = player.CraftLevels.GetValueOrDefault(TabKeys[i], 1);
                label += $" ({lvl})";
            }
            _sb.DrawString(_font, label, new Vector2(tabX + 8, tabY + 4), tabColor);
            tabX += 150;
        }

        // Content area
        int contentY = tabY + 36;
        int contentH = panelH - (contentY - py) - 16;

        string? clicked = null;
        if (CurrentTab == 0)
            clicked = DrawClassTree(player, px + 16, contentY, panelW - 32, contentH, mouse);
        else
            clicked = DrawCraftTab(player, TabKeys[CurrentTab], px + 16, contentY,
                panelW - 32, contentH, mouse);

        // Tooltip
        if (HoveredNode != null)
            DrawTooltip(HoveredNode, player, mouse);

        return clicked;
    }

    private string? DrawClassTree(Player player, int x, int y, int w, int h,
        MouseState mouse)
    {
        var nodes = SkillTree.GetNodes(player);
        if (nodes.Length == 0)
        {
            if (_font != null)
                _sb.DrawString(_font, "No skill tree for this class.",
                    new Vector2(x + 10, y + 10), Config.ColDim);
            return null;
        }

        // Draw tree box
        _sb.Draw(Blank, new Rectangle(x, y, w, h), new Color(17, 15, 12));
        _sb.Draw(Blank, new Rectangle(x, y, w, 1), new Color(48, 42, 33));

        // Hex positions: center + 6 surrounding
        int centerX = x + w / 2;
        int centerY = y + h / 2;
        int radius = 32;
        int ringR = 110;

        var positions = new List<(int X, int Y)>();
        // Center node
        positions.Add((centerX, centerY));
        // Ring of 6
        for (int i = 0; i < 6; i++)
        {
            double angle = Math.PI / 3 * i - Math.PI / 2;
            int rx = centerX + (int)(Math.Cos(angle) * ringR);
            int ry = centerY + (int)(Math.Sin(angle) * ringR);
            positions.Add((rx, ry));
        }

        string? clicked = null;

        // Draw connections first
        for (int i = 1; i < positions.Count && i < nodes.Length; i++)
        {
            var (x0, y0) = positions[0];
            var (x1, y1) = positions[i];
            DrawDottedLine(x0, y0, x1, y1, new Color(60, 55, 45));
        }

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

            // Level requirement
            string reqText = learned ? "✓" : $"Lv{node.Level}";
            _sb.DrawString(_font, reqText, new Vector2(nx - 12, ny - 6),
                learned ? Color.White : (locked ? new Color(180, 100, 80) : Config.ColText));

            // Node name (short)
            string shortName = node.Name.Length > 12
                ? node.Name[..12] + "…" : node.Name;
            _sb.DrawString(_font, shortName, new Vector2(nx - shortName.Length * 3, ny + radius + 4),
                Config.ColDim);

            // Hover and click
            var mouseRect = new Rectangle(mouse.X, mouse.Y, 1, 1);
            var nodeRect = new Rectangle(nx - radius, ny - radius, radius * 2, radius * 2);
            if (nodeRect.Intersects(mouseRect))
            {
                HoveredNode = node.Key;
                _sb.Draw(Blank, new Rectangle(nx - radius - 2, ny - radius - 2,
                    radius * 2 + 4, radius * 2 + 4),
                    new Color(240, 210, 90, 40));
            }
        }

        // Bottom hint
        if (_font != null)
        {
            string hint = "Hover for details. Click node or press 1-7 to learn.";
            _sb.DrawString(_font, hint,
                new Vector2(x + 10, y + h - 20), Config.ColDim);
        }

        return clicked;
    }

    private string? DrawCraftTab(Player player, string branchKey,
        int x, int y, int w, int h, MouseState mouse)
    {
        if (!SkillTree.CraftBranches.TryGetValue(branchKey, out var branch))
            return null;

        // Branch info
        int lvl = player.CraftLevels.GetValueOrDefault(branchKey, 1);
        int xp = player.CraftXp.GetValueOrDefault(branchKey, 0);
        int xpNext = branch.XpNext * lvl;

        if (_font != null)
        {
            _sb.DrawString(_font, branch.Name, new Vector2(x + 10, y + 10),
                Config.ColGold);
            _sb.DrawString(_font, branch.Desc, new Vector2(x + 10, y + 34),
                Config.ColDim);
            _sb.DrawString(_font, $"Level: {lvl}/25  XP: {xp}/{xpNext}",
                new Vector2(x + 10, y + 56), Config.ColText);
        }

        // XP bar
        int barX = x + 10;
        int barY = y + 78;
        int barW = w - 20;
        _sb.Draw(Blank, new Rectangle(barX, barY, barW, 10),
            new Color(30, 28, 22));
        int fillW = xpNext > 0 ? barW * xp / xpNext : 0;
        _sb.Draw(Blank, new Rectangle(barX, barY, fillW, 10),
            new Color(100, 180, 110));

        // Recipe list
        int listY = y + 100;
        _sb.DrawString(_font, "Recipes:", new Vector2(x + 10, listY),
            Config.ColText);
        listY += 24;

        int recipeIdx = 0;
        foreach (var (reqLevel, recipes) in branch.Recipes)
        {
            bool unlocked = lvl >= reqLevel;
            foreach (var recipeKey in recipes)
            {
                var def = ItemData.Get(recipeKey);
                if (def == null) continue;

                var rect = new Rectangle(x + 10, listY, w - 20, 28);
                _sb.Draw(Blank, rect,
                    unlocked ? new Color(30, 35, 25) : new Color(20, 18, 14));
                _sb.Draw(Blank, new Rectangle(x + 10, listY, w - 20, 1),
                    new Color(50, 45, 38));

                string name = def.Name;
                string reqStr = unlocked ? "" : $" (req Lv.{reqLevel})";
                _sb.DrawString(_font, $"{name}{reqStr}",
                    new Vector2(x + 16, listY + 6),
                    unlocked ? Config.ColText : Config.ColDim);

                // Materials needed (simplified)
                if (unlocked)
                {
                    string mats = GetRecipeMaterials(recipeKey);
                    _sb.DrawString(_font, mats,
                        new Vector2(x + 16, listY + 18),
                        Config.ColDim);

                    // Check if craftable
                    bool canCraft = CanCraft(player, recipeKey);
                    if (canCraft)
                    {
                        _sb.DrawString(_font, "[1] Craft",
                            new Vector2(x + w - 80, listY + 6),
                            Config.ColGold);
                    }
                }

                listY += 32;
                recipeIdx++;
            }
        }

        return null;
    }

    /// <summary>
    /// Get materials needed for a recipe (simplified).
    /// </summary>
    private string GetRecipeMaterials(string recipeKey)
    {
        return recipeKey switch
        {
            "dagger" => "2 wood, 1 stone",
            "sword" => "3 wood, 2 ore",
            "axe" => "4 wood, 3 ore",
            "warhammer" => "5 wood, 5 ore",
            "leather" => "3 wood",
            "chainmail" => "4 ore",
            "platemail" => "6 ore, 2 gold_ore",
            "cap" => "2 wood",
            "helm" => "3 ore",
            "potion_hp_s" => "1 mushroom",
            "potion_hp_m" => "2 mushroom, 1 berry",
            "potion_mana_s" => "1 berry",
            "book_str" => "5 ore, 3 gold_ore",
            "book_int" => "5 ore, 3 gold_ore",
            _ => "",
        };
    }

    /// <summary>
    /// Check if player has materials to craft an item.
    /// </summary>
    private bool CanCraft(Player player, string recipeKey)
    {
        var requirements = recipeKey switch
        {
            "dagger" => new[] { ("wood", 2), ("stone", 1) },
            "sword" => new[] { ("wood", 3), ("ore", 2) },
            "axe" => new[] { ("wood", 4), ("ore", 3) },
            "warhammer" => new[] { ("wood", 5), ("ore", 5) },
            "leather" => new[] { ("wood", 3) },
            "chainmail" => new[] { ("ore", 4) },
            "platemail" => new[] { ("ore", 6), ("gold_ore", 2) },
            "cap" => new[] { ("wood", 2) },
            "helm" => new[] { ("ore", 3) },
            "potion_hp_s" => new[] { ("mushroom", 1) },
            "potion_hp_m" => new[] { ("mushroom", 2), ("berry", 1) },
            "potion_mana_s" => new[] { ("berry", 1) },
            _ => Array.Empty<(string, int)>(),
        };

        foreach (var (mat, count) in requirements)
        {
            if (player.Inventory.GetValueOrDefault(mat) < count)
                return false;
        }
        return true;
    }

    private void DrawHex(int cx, int cy, int r, Color fill, bool filled)
    {
        // Hexagon vertices (flat-top)
        var points = new Vector2[6];
        for (int i = 0; i < 6; i++)
        {
            double angle = Math.PI / 3 * i;
            points[i] = new Vector2(
                cx + r * (float)Math.Cos(angle),
                cy + r * (float)Math.Sin(angle));
        }

        // Fill
        if (filled)
        {
            // Draw as triangles from center
            for (int i = 0; i < 6; i++)
            {
                var p0 = new Vector2(cx, cy);
                var p1 = points[i];
                var p2 = points[(i + 1) % 6];
                DrawTriangle(p0, p1, p2, fill);
            }
        }

        // Outline
        for (int i = 0; i < 6; i++)
        {
            var p1 = points[i];
            var p2 = points[(i + 1) % 6];
            DrawLine(p1, p2, new Color(100, 90, 70));
        }
    }

    private void DrawTriangle(Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        // Fill triangle using scanlines
        float minY = Math.Min(a.Y, Math.Min(b.Y, c.Y));
        float maxY = Math.Max(a.Y, Math.Max(b.Y, c.Y));
        for (int y = (int)minY; y <= (int)maxY; y++)
        {
            // Find intersections at this y
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
        int dashLen = 4, gapLen = 4;
        int pos = 0;
        while (pos < dist)
        {
            int end = Math.Min(pos + dashLen, dist);
            float t0 = (float)pos / dist;
            float t1 = (float)end / dist;
            int sx = (int)(x0 + t0 * dx);
            int sy = (int)(y0 + t0 * dy);
            int ex = (int)(x0 + t1 * dx);
            int ey = (int)(y0 + t1 * dy);
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
            lines.Add("✓ Learned");
        else if (player.Level < node.Level)
            lines.Add($"Requires level {node.Level}");
        else if (player.SkillPoints < 1)
            lines.Add("No skill points");
        else
            lines.Add("Available — click to learn");

        // Tooltip background
        int maxW = 0;
        foreach (var line in lines)
        {
            var size = _font.MeasureString(line);
            if (size.X > maxW) maxW = (int)size.X;
        }
        int ttW = maxW + 16;
        int ttH = lines.Count * 16 + 12;
        int ttX = mouse.X + 16;
        int ttY = mouse.Y + 16;
        if (ttX + ttW > Game1.ScreenW) ttX = mouse.X - ttW - 8;
        if (ttY + ttH > Game1.ScreenH) ttY = mouse.Y - ttH - 8;

        _sb.Draw(Blank, new Rectangle(ttX, ttY, ttW, ttH),
            new Color(15, 13, 10, 230));
        _sb.Draw(Blank, new Rectangle(ttX, ttY, ttW, 1), Config.ColGold);

        for (int i = 0; i < lines.Count; i++)
        {
            var color = i == 0 ? Config.ColGold : Config.ColText;
            if (i == lines.Count - 1 && learned) color = new Color(100, 200, 100);
            _sb.DrawString(_font, lines[i],
                new Vector2(ttX + 8, ttY + 6 + i * 16), color);
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
            }
            return _blank;
        }
    }
}
