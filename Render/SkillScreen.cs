using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Aethelgard.Data;
namespace Aethelgard.Render;

/// <summary>
/// Дерево навыков — реализация на UiKit (единая UI-база).
/// Вкладки «Класс | Ковка | Алхимия | Инженерия» через K.Tabs. Узлы классовой
/// ветки — гексагоны (позиции сохранены) с hover-тултипом и золотой рамкой
/// под курсором; ветки крафта — список рецептов с hover-подсветкой.
/// Полоса эволюции класса видна всегда (до уровня — заблокирована).
/// </summary>
public class SkillScreen
{
    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _sb;
    private Texture2D? _blank;

    private static readonly UiKit K = UiKit.Kit;

    public int CurrentTab = 0;  // 0=class, 1=blacksmith, 2=alchemy, 3=magic_eng
    public string? HoveredNode;

    private static readonly string[] TabNames = { "Класс", "Ковка", "Алхимия", "Инженерия" };
    private static readonly string[] TabKeys = { "", "blacksmith", "alchemy", "magic_engineering" };

    public SkillScreen(GraphicsDevice device, SpriteBatch sb)
    {
        _device = device;
        _sb = sb;
    }

    /// <summary>Шрифты теперь глобальные (Fonts) — метод оставлен для контракта.</summary>
    public void LoadFont(SpriteFont font) { }

    /// <summary>
    /// Рисует дерево навыков. Возвращает действие клика
    /// ("learn:<key>" / "promote:<key>") или null.
    /// </summary>
    public string? Draw(Player player, MouseState mouse)
    {
        K.Begin(_sb, _device, mouse);

        int sw = AethelgardGame.ScreenW, sh = AethelgardGame.ScreenH;
        int panelW = 1560, panelH = 950;
        var panel = new Rectangle((sw - panelW) / 2, (sh - panelH) / 2, panelW, panelH);
        K.Panel(panel);

        // ── Шапка ──
        var headerRect = new Rectangle(panel.X, panel.Y, panel.Width, 72);
        K.Text("ДРЕВО НАВЫКОВ", new Vector2(headerRect.X + 20, headerRect.Y + 14),
            UiKit.Theme.GoldBright, 2);
        var classDef = ClassData.Get(player.ClassKey);
        string className = classDef?.Name ?? player.ClassKey;
        string subText = $"{className}  ·  Уровень {player.Level}";
        K.Text(subText, new Vector2(headerRect.X + 20, headerRect.Y + 48),
            UiKit.Theme.TextDim, 4, shadow: false);

        if (player.SkillPoints > 0)
        {
            string badge = $"Очки навыков: {player.SkillPoints}";
            var badgeRect = new Rectangle(
                headerRect.Right - 60 - (int)K.FontWidth(badge, 3) - 24,
                headerRect.Y + 16, (int)K.FontWidth(badge, 3) + 24, 36);
            K.Button(badgeRect, badge, state: 1, enabled: false);
        }

        // ── Вкладки (кликабельны, hover-подсветка через K.Tabs) ──
        HoveredNode = null;
        int tabY = panel.Y + 84;
        var tabLabels = new string[TabNames.Length];
        for (int i = 0; i < TabNames.Length; i++)
        {
            tabLabels[i] = TabNames[i];
            if (i > 0)
            {
                int lvl = player.CraftLevels.GetValueOrDefault(TabKeys[i], 1);
                tabLabels[i] += $" ({lvl})";
            }
        }
        int tabClicked = K.Tabs(panel.X + UiKit.Pad, tabY, tabLabels, CurrentTab, widthEach: 150);
        if (tabClicked >= 0) CurrentTab = tabClicked;

        // ── Контент ──
        int contentY = tabY + UiKit.TabH + UiKit.Gap + 4;
        int contentH = panel.Bottom - UiKit.Pad - contentY;

        string? clicked = null;
        if (CurrentTab == 0)
            clicked = DrawClassTree(player, panel.X + UiKit.Pad, contentY,
                panel.Width - UiKit.Pad * 2, contentH);
        else
            clicked = DrawCraftTab(player, TabKeys[CurrentTab], panel.X + UiKit.Pad,
                contentY, panel.Width - UiKit.Pad * 2, contentH);

        K.End();
        K.EndInputFrame();
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

    private string? DrawClassTree(Player player, int x, int y, int w, int h)
    {
        var nodes = SkillTree.GetNodes(player);
        if (nodes.Length == 0)
        {
            K.Text("У этого класса нет древа навыков.", new Vector2(x + 10, y + 10),
                UiKit.Theme.TextMuted, 3);
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
        K.Section(new Rectangle(x, y, w, treeH));

        // Hex positions: center + кольца (0 центр, 1-5 ближнее, 6-11 дальнее)
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

        for (int i = 0; i < nodes.Length && i < positions.Count; i++)
        {
            var node = nodes[i];
            var (nx, ny) = positions[i];

            bool learned = player.LearnedNodes.Contains(node.Key);
            bool available = !learned && player.Level >= node.Level && player.SkillPoints > 0;
            bool locked = player.Level < node.Level;

            Color nodeFill = learned ? new Color(70, 110, 66)
                           : available ? new Color(84, 68, 34)
                           : new Color(38, 34, 28);
            Color nodeBorder = learned ? UiKit.Theme.Xp
                             : available ? UiKit.Theme.Gold
                             : UiKit.Theme.BorderSoft;

            var nodeRect = new Rectangle(nx - radius, ny - radius - 8, radius * 2, radius * 2 + 14);
            bool hot = K.Hot(nodeRect);
            if (hot) nodeBorder = UiKit.Theme.GoldBright;   // hover — золотая рамка

            DrawHex(nx, ny, radius, nodeFill, nodeBorder, learned);

            // Иконка узла из assets/skills/class/<базовый класс>_<индекс>.png
            var icon = Ui.SkillIcon(_device, "class", $"{baseKey}_{i}");
            if (icon != null)
            {
                int isz = radius * 2 - 22;
                _sb.Draw(icon, new Rectangle(nx - isz / 2, ny - isz / 2 - 8, isz, isz),
                    learned ? Color.White : Color.Multiply(Color.White, 0.55f));
                string reqText = learned ? "изучено" : $"Ур.{node.Level}";
                K.TextCentered(reqText,
                    new Rectangle(nx - radius, ny + radius - 26, radius * 2, 18),
                    learned ? UiKit.Theme.Xp : locked ? UiKit.Theme.Danger : UiKit.Theme.Text, 4);
            }
            else
            {
                string reqText = learned ? "+" : $"Ур.{node.Level}";
                K.TextCentered(reqText,
                    new Rectangle(nx - radius, ny - 10, radius * 2, 20),
                    learned ? UiKit.Theme.Xp : locked ? UiKit.Theme.Danger : UiKit.Theme.Text, 4);
            }

            // Имя узла — по центру под гексом
            string label = node.Name.Length > 20 ? node.Name[..19] + "…" : node.Name;
            K.TextCentered(label,
                new Rectangle(nx - radius, ny + radius + 6, radius * 2, 18),
                learned ? UiKit.Theme.Xp : hot ? UiKit.Theme.GoldBright : UiKit.Theme.TextDim, 4);

            if (hot)
            {
                HoveredNode = node.Key;
                ShowNodeTooltip(nodeRect, node, player, learned, available, locked);
                if (K.Clicked(nodeRect) && !learned)
                    clicked = "learn:" + node.Key;
            }
        }

        // Подсказка внизу древа
        K.Text("Наведи курсор для подробностей · Клик — изучить (нужны очко навыков и уровень)",
            new Vector2(x + 10, y + treeH - 26), UiKit.Theme.TextMuted, 4, shadow: false);

        // ── Эволюция класса: всегда видна, до нужного уровня — заблокирована ──
        if (promoKeys.Length > 0)
        {
            int promoY = y + treeH + UiKit.Gap;
            var promoRect = new Rectangle(x, promoY, w, promoH - UiKit.Gap);
            K.Section(promoRect, "ЭВОЛЮЦИЯ КЛАССА");
            if (!promosUnlocked)
                K.Text($"доступно с уровня {promoReqLevel}",
                    new Vector2(x + 220, promoY + 8), UiKit.Theme.Danger, 4, shadow: false);

            int bx = x + 10;
            foreach (var pk in promoKeys)
            {
                var pdef = ClassData.Get(pk);
                if (pdef == null) continue;
                var rect = new Rectangle(bx, promoY + 30,
                    (int)K.FontWidth(pdef.Name, 3) + 76, 52);

                // Иконка класса из assets/skills/class/<ключ>_0.png
                var icon = Ui.SkillIcon(_device, "class", pk + "_0");
                bool hot = K.Hot(rect) && promosUnlocked;

                Color bg = !promosUnlocked ? UiKit.Theme.BgSlot
                         : hot ? UiKit.Theme.BgSlotHot : UiKit.Theme.BgSlot;
                Color border = !promosUnlocked ? UiKit.Theme.BorderSoft
                             : hot ? UiKit.Theme.GoldBright : UiKit.Theme.Border;
                Rect(rect, bg);
                Frame(rect, border);

                if (icon != null)
                    _sb.Draw(icon, new Rectangle(rect.X + 8, rect.Y + 8, 36, 36), Color.White);
                else
                    DrawFilledCircle(rect.X + 24, rect.Y + 26, 11, pdef.Color);
                K.Text(pdef.Name, new Vector2(rect.X + 54, rect.Y + 16),
                    !promosUnlocked ? UiKit.Theme.TextMuted
                    : hot ? UiKit.Theme.GoldBright : UiKit.Theme.Text, 3);

                if (hot && K.Clicked(rect))
                    clicked = "promote:" + pk;
                bx += rect.Width + 12;
            }
            K.Text("Клик — сменить класс (навыки и снаряжение сохраняются)",
                new Vector2(x + 10, promoY + promoH - 28),
                UiKit.Theme.TextMuted, 4, shadow: false);
        }

        return clicked;
    }

    // ── Вкладка крафта: шапка ветки, XP-бар, список рецептов ──
    private string? DrawCraftTab(Player player, string branchKey, int x, int y, int w, int h)
    {
        if (!SkillTree.CraftBranches.TryGetValue(branchKey, out var branch))
            return null;

        int lvl = player.CraftLevels.GetValueOrDefault(branchKey, 1);
        var branchIcon = Ui.SkillIcon(_device, "craft",
            branchKey + "_" + Math.Clamp(lvl - 1, 0, 7));

        int headerX = x + 10;
        if (branchIcon != null)
        {
            _sb.Draw(branchIcon, new Rectangle(x + 10, y + 8, 52, 52), Color.White);
            headerX = x + 74;
        }

        K.Text(branch.Name, new Vector2(headerX, y + 10), UiKit.Theme.GoldBright, 3);
        K.Text(branch.Desc, new Vector2(headerX, y + 36), UiKit.Theme.TextDim, 4, shadow: false);
        K.Text($"Уровень: {lvl}/25   Опыт: {player.CraftXp.GetValueOrDefault(branchKey, 0)}/{branch.XpNext * lvl}",
            new Vector2(headerX, y + 56), UiKit.Theme.Text, 4, shadow: false);

        // XP-бар через кит
        int xp = player.CraftXp.GetValueOrDefault(branchKey, 0);
        int xpNext = branch.XpNext * lvl;
        K.Bar(x + 10, y + 84, w - 20, 12, xpNext > 0 ? (double)xp / xpNext : 0, UiKit.Theme.Xp);

        // Список рецептов
        int listY = y + 110;
        K.Text("Рецепты:", new Vector2(x + 10, listY), UiKit.Theme.Text, 4, shadow: false);
        listY += 26;

        foreach (var (reqLevel, recipes) in branch.Recipes)
        {
            foreach (var recipeKey in recipes)
            {
                var rdef = ItemData.Get(recipeKey);
                if (rdef == null) continue;

                // Рецепт открыт, когда и ветка крафта, и уровень персонажа достаточны
                bool byBranch = lvl >= reqLevel;
                bool byChar = player.Level >= rdef.LevelReq;
                bool unlocked = byBranch && byChar;
                string lockText = !byChar ? $"нужен ур. {rdef.LevelReq}"
                    : !byBranch ? $"нужна ветка ур. {reqLevel}" : "";

                const int rowH = 52;
                var rect = new Rectangle(x + 10, listY, w - 20, rowH);
                bool hot = K.Hot(rect);
                Rect(rect, unlocked ? (hot ? UiKit.Theme.BgSlotHot : UiKit.Theme.BgSlot)
                                    : UiKit.Theme.BgSoft);
                Frame(rect, hot ? new Color(UiKit.Theme.Gold, 120) : UiKit.Theme.BorderSoft);

                string name = rdef.Name;
                string reqStr = unlocked ? "" : $"  ({lockText})";
                K.Text($"{name}{reqStr}", new Vector2(rect.X + 8, listY + 8),
                    unlocked ? (hot ? UiKit.Theme.GoldBright : UiKit.Theme.Text)
                             : UiKit.Theme.TextMuted, 4);

                // Материалы (из реальных рецептов) — на второй строке
                var variants = ItemData.RecipesOf(recipeKey);
                if (variants.Count > 0)
                {
                    string mats = string.Join(", ", variants[0].Materials
                        .Select(m => $"{ItemData.DisplayName(m.Key)}×{m.Value}"));
                    bool afford = variants[0].Materials.All(m =>
                        player.Inventory.GetValueOrDefault(m.Key) >= m.Value);
                    K.Text(mats, new Vector2(rect.X + 8, listY + 28),
                        unlocked ? (afford ? UiKit.Theme.Xp : UiKit.Theme.Danger)
                                 : UiKit.Theme.TextMuted, 4, shadow: false);
                }

                listY += rowH + 4;
            }
        }

        return null;
    }

    // ── Тултип узла (рисуется UiKit поверх всего в End) ──
    private void ShowNodeTooltip(Rectangle anchor, SkillTree.Node node, Player player,
        bool learned, bool available, bool locked)
    {
        var lines = new List<(string, Color)>
        {
            (node.Desc, UiKit.Theme.TextMuted),
        };
        if (learned) lines.Add(("● Изучено", UiKit.Theme.Xp));
        else if (locked)
        {
            lines.Add(($"Требуется уровень {node.Level}", UiKit.Theme.Danger));
            lines.Add(("● Заблокировано", UiKit.Theme.TextMuted));
        }
        else if (player.SkillPoints < 1)
        {
            lines.Add(("Нет очков навыков", UiKit.Theme.Danger));
        }
        else
        {
            lines.Add(("● Доступно — кликните, чтобы изучить", UiKit.Theme.Gold));
        }
        K.Tooltip(anchor, node.Name, lines);
    }

    // ── Примитивы рисования (только для гексагонов, которых нет в ките) ──

    private void Rect(Rectangle r, Color c) => _sb.Draw(Blank, r, c);

    private void Frame(Rectangle r, Color c)
    {
        Rect(new Rectangle(r.X, r.Y, r.Width, 1), c);
        Rect(new Rectangle(r.X, r.Bottom - 1, r.Width, 1), c);
        Rect(new Rectangle(r.X, r.Y, 1, r.Height), c);
        Rect(new Rectangle(r.Right - 1, r.Y, 1, r.Height), c);
    }

    private void DrawHex(int cx, int cy, int r, Color fill, Color border, bool filled)
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
            for (int i = 0; i < 6; i++)
                DrawTriangle(new Vector2(cx, cy), points[i],
                    points[(i + 1) % 6], fill);

        for (int i = 0; i < 6; i++)
            DrawLine(points[i], points[(i + 1) % 6], border);
        // Тёплая тень под гексом
        DrawHexOutlineShadow(cx, cy, r + 2);
    }

    private void DrawHexOutlineShadow(int cx, int cy, int r)
    {
        // простая тень: полупрозрачные пиксели контура
        for (int i = 0; i < 6; i++)
        {
            double a0 = Math.PI / 3 * i, a1 = Math.PI / 3 * (i + 1);
            DrawLine(
                new Vector2(cx + r * (float)Math.Cos(a0), cy + 2 + r * (float)Math.Sin(a0)),
                new Vector2(cx + r * (float)Math.Cos(a1), cy + 2 + r * (float)Math.Sin(a1)),
                UiKit.Theme.Shadow);
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
                Rect(new Rectangle(x0, y, x1 - x0, 1), color);
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
            Rect(new Rectangle(x, y, 1, 1), color);
        }
    }

    private void DrawFilledCircle(int cx, int cy, int r, Color color)
    {
        for (int dy = -r; dy <= r; dy++)
        {
            int w = (int)Math.Sqrt(r * r - dy * dy);
            Rect(new Rectangle(cx - w, cy + dy, w * 2, 1), color);
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
