using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
namespace Aethelgard.Render;

/// <summary>Результат клика по инвентарю (parity game/_input.py _handle_inventory_click).</summary>
public readonly record struct InventoryAction(
    string? Item, string? Unequip, int PageDelta, bool Close);

/// <summary>
/// Экран инвентаря — точный порт python render/screens_renderer/_inventory.py:
/// модальная панель на весь экран, слева силуэт персонажа + ячейки экипировки
/// (Голова/Тело/Руки/Ноги/Обувь) с подсказкой класса, справа сетка 10×10,
/// страницы, тултипы и наведение. Счётчик количества в ячейках намеренно не
/// рисуется — он виден в тултипе.
/// </summary>
public class InventoryScreen
{
    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _sb;
    private readonly ItemRenderer _icons;
    private SpriteFont? _font;
    private Texture2D? _blank;

    public const int GridCols = 10;
    public const int GridRows = 10;

    public int CurrentPage = 0;

    // Один клик = одно действие (Draw выполняется каждый кадр).
    private bool _clickArmed = true;

    // ── Палитра theme.py ──
    private static readonly Color CTitle = new(214, 178, 98);
    private static readonly Color CTitleLight = new(238, 222, 188);
    private static readonly Color CText = new(222, 214, 198);
    private static readonly Color CTextDim = new(150, 140, 120);
    private static readonly Color CTextMuted = new(98, 90, 74);
    private static readonly Color CGold = new(206, 166, 82);
    private static readonly Color CPanel = new(30, 26, 21);
    private static readonly Color CPanelDark = new(18, 16, 13);
    private static readonly Color CBorder = new(78, 66, 48);
    private static readonly Color CBorderDark = new(46, 39, 29);
    private static readonly Color CRowHover = new(54, 45, 33);
    private static readonly Color CRowBorder = new(110, 91, 62);
    private static readonly Color CHpCrit = new(220, 84, 64);
    private static readonly Color CHeal = new(120, 172, 104);

    public InventoryScreen(GraphicsDevice device, SpriteBatch sb)
    {
        _device = device;
        _sb = sb;
        _icons = new ItemRenderer(device);
    }

    public void LoadFont(SpriteFont font) => _font = font;

    private SpriteFont F1 => Fonts.Ready ? Fonts.H1 : _font!;
    private SpriteFont F2 => Fonts.Ready ? Fonts.H2 : _font!;
    private SpriteFont FBody => _font!;
    private SpriteFont FSmall => Fonts.Ready ? Fonts.Small : _font!;
    private SpriteFont FTiny => Fonts.Ready ? Fonts.Tiny : _font!;

    // ── Геометрия модального окна (core/config.py) ──
    private static Rectangle PanelRect()
    {
        int sw = AethelgardGame.ScreenW, sh = AethelgardGame.ScreenH;
        return new Rectangle(40, 20, sw - 80, sh - 40);
    }

    private static Rectangle CloseRect()
    {
        var p = PanelRect();
        return new Rectangle(p.Right - 34 - 10, p.Y + 10, 34, 34);
    }

    private static int ModalRight()
    {
        var p = PanelRect();
        return p.X + p.Width - 20 - 34 - 10 - 6;
    }

    /// <summary>Рисует инвентарь и возвращает действие пользователя (клик).</summary>
    public InventoryAction Draw(Player player, MouseState mouse)
    {
        var panel = PanelRect();
        int cx0 = panel.X + 20;
        int cy0 = panel.Y + 56;
        int cw = panel.Width - 20 * 2 - 34 - 10;
        int ch = panel.Height - 56 - 20;

        bool freshClick = mouse.LeftButton == ButtonState.Pressed && _clickArmed;
        if (mouse.LeftButton == ButtonState.Released) _clickArmed = true;
        else if (freshClick) _clickArmed = false;

        // ── Затемнение + панель + крестик ──
        _sb.Draw(Blank, new Rectangle(0, 0, AethelgardGame.ScreenW,
            AethelgardGame.ScreenH), new Color(6, 5, 4, 165));
        Fill(panel, new Color(30, 26, 21, 238), CBorder);

        var close = CloseRect();
        bool closeHover = close.Contains(mouse.X, mouse.Y);
        Fill(close, closeHover ? new Color(72, 34, 28) : new Color(54, 26, 22),
            new Color(160, 68, 58));
        DrawCross(close, new Color(200, 74, 64));

        if (_font == null) return default;

        // ── Заголовок и подсказки ──
        _sb.DrawString(F1, "ИНВЕНТАРЬ", new Vector2(cx0, cy0), CTitle);
        DrawRight(FBody, $"${player.Gold}", ModalRight(), cy0 + 8, CGold);
        DrawRight(FSmall, "Q/E — страницы   U/V/H/B/N — снять снаряжение",
            ModalRight(), cy0 + 34, CTextMuted);

        int y = cy0 + 56;

        // ── Две колонки ──
        int equipW = (int)(cw * 0.36);
        const int gap = 20;
        int invW = cw - equipW - gap;
        int equipX = cx0;
        int invX = cx0 + equipW + gap;

        // ═══ Левая колонка: снаряжение ═══
        int y1 = Section(equipX, y, equipW, "Снаряжение");
        int panelH = (cy0 + ch) - y1 - 20;
        string? unequip = null;
        DrawEquipmentPanel(player, equipX, y1, equipW, panelH, mouse, freshClick,
            ref unequip);

        // ═══ Правая колонка: рюкзак ═══
        int y2 = Section(invX, y, invW, "Рюкзак");

        var stacks = player.InventoryStacks();
        int total = Math.Max(1, (stacks.Count + Config.InventoryPageSize - 1)
            / Config.InventoryPageSize);
        int page = Math.Min(CurrentPage, total - 1);
        CurrentPage = page;
        int start = page * Config.InventoryPageSize;

        const int gapCell = 4;
        const int cols = GridCols, rows = GridRows;
        int availH = (cy0 + ch) - y2 - 46;
        int cellW = Math.Min(64, (invW - gapCell * (cols - 1)) / cols);
        int cellH = Math.Min(64, Math.Max(38, (availH - gapCell * (rows - 1)) / rows));
        int gridW = cols * cellW + gapCell * (cols - 1);
        int gridX = invX + (invW - gridW) / 2;

        string? usedItem = null;
        (string Key, int Count)? hovered = null;

        for (int idx = 0; idx < Config.InventoryPageSize; idx++)
        {
            int row = idx / cols, col = idx % cols;
            var rect = new Rectangle(gridX + col * (cellW + gapCell),
                y2 + row * (cellH + gapCell), cellW, cellH);
            int absolute = start + idx;
            if (absolute < stacks.Count)
            {
                var (key, count) = stacks[absolute];
                bool hover = rect.Contains(mouse.X, mouse.Y);
                DrawInvSlot(player, key, count, rect, hover);
                if (hover) hovered = (key, count);
                if (hover && freshClick) usedItem = key;
            }
            else
            {
                Fill(rect, new Color(25, 22, 18), new Color(48, 42, 32));
            }
        }

        int gridBottom = y2 + rows * cellH + (rows - 1) * gapCell;
        if (stacks.Count == 0)
            DrawCentered(FBody, "Рюкзак пуст", gridX + gridW / 2,
                y2 + (rows * cellH + (rows - 1) * gapCell) / 2, CTextMuted);

        // ── Страницы ──
        int pageY = gridBottom + 10;
        const int bw = 38, bh = 28;
        var prevRect = new Rectangle(gridX, pageY, bw, bh);
        var nextRect = new Rectangle(gridX + gridW - bw, pageY, bw, bh);
        bool prevEnabled = page > 0;
        bool nextEnabled = page < total - 1;
        DrawPageButton("‹", prevRect, prevEnabled,
            prevEnabled && prevRect.Contains(mouse.X, mouse.Y));
        DrawPageButton("›", nextRect, nextEnabled,
            nextEnabled && nextRect.Contains(mouse.X, mouse.Y));
        DrawCentered(FSmall, $"Стр. {page + 1}/{total}",
            invX + invW / 2, pageY + 4, CTextDim);

        int pageDelta = 0;
        if (freshClick)
        {
            if (prevEnabled && prevRect.Contains(mouse.X, mouse.Y)) pageDelta = -1;
            else if (nextEnabled && nextRect.Contains(mouse.X, mouse.Y)) pageDelta = 1;
        }

        // ── Тултип ──
        if (hovered.HasValue)
            DrawTooltip(player, hovered.Value.Key, hovered.Value.Count, mouse);

        bool closeAction = freshClick && close.Contains(mouse.X, mouse.Y);
        return new InventoryAction(usedItem, unequip, pageDelta, closeAction);
    }

    // ── Левая панель: силуэт, ячейки экипировки, подсказка класса ──
    private void DrawEquipmentPanel(Player player, int x, int y, int w, int h,
        MouseState mouse, bool freshClick, ref string? unequip)
    {
        var cls = ClassData.Data(player.ClassKey);

        // Подсказка класса — внизу панели.
        string weapons = cls.Weapons.Length > 0
            ? string.Join(", ", Array.ConvertAll(cls.Weapons,
                k => ClassData.WeaponNames.GetValueOrDefault(k, k)))
            : "—";
        string armor = cls.ArmorTypes.Length > 0
            ? string.Join(", ", Array.ConvertAll(cls.ArmorTypes,
                k => ClassData.ArmorNames.GetValueOrDefault(k, k)))
            : "—";
        var hintLines = WrapLines($"Оружие: {weapons}. Броня: {armor}.", FTiny, w - 12);
        int hintH = hintLines.Count * 14 + 6;
        int hintY = y + h - hintH;

        // Силуэт персонажа.
        int charSize = Math.Min(120, Math.Max(80, (int)(h * 0.22)));
        charSize = Math.Min(charSize, w - 40);
        int charX = x + (w - charSize) / 2;
        int charY = y + 20;

        Fill(new Rectangle(charX - 8, charY - 8, charSize + 16, charSize + 16),
            new Color(26, 24, 32), new Color(60, 55, 70));
        var sprite = SpriteStore.Get(_device, "player", player.ClassKey, cls.Color);
        _sb.Draw(sprite, new Rectangle(charX, charY, charSize, charSize), Color.White);

        // Ячейки: голова, тело/руки, ноги, обувь.
        int slotsY = charY + charSize + 24;
        int avail = Math.Max(120, hintY - slotsY - 6);
        int slotSize = Math.Max(30, Math.Min(48,
            Math.Min(avail / 4 - 34, (w - 40) / 2 - 8)));
        const int cellGap = 10;
        int rowStep = slotSize + 34;
        int groupW = 2 * slotSize + cellGap;
        int gx = x + (w - groupW) / 2;

        var slots = new (string Key, string Label, int Col, int Row)[]
        {
            ("head", "Голова", 0, 0),
            ("armor", "Тело", 0, 1),
            ("weapon", "Руки", 1, 1),
            ("legs", "Ноги", 0, 2),
            ("boots", "Обувь", 0, 3),
        };

        foreach (var (slotKey, label, gcol, grow) in slots)
        {
            int sx = gx + gcol * (slotSize + cellGap);
            int sy = slotsY + grow * rowStep;
            var rect = new Rectangle(sx, sy, slotSize, slotSize);
            string? itemKey = slotKey switch
            {
                "head" => player.Head,
                "armor" => player.Armor,
                "weapon" => player.Weapon,
                "legs" => player.Legs,
                "boots" => player.Boots,
                _ => null,
            };
            bool hover = rect.Contains(mouse.X, mouse.Y);

            DrawCentered(FTiny, label, sx + slotSize / 2, sy - 14, CTextDim);
            Fill(rect, hover ? new Color(58, 49, 35) : new Color(32, 28, 22),
                hover ? CRowBorder : CBorder);

            if (!string.IsNullOrEmpty(itemKey))
            {
                int iconSize = slotSize - 10;
                var icon = _icons.GetIcon(itemKey!);
                _sb.Draw(icon, new Rectangle(sx + 5, sy + 5, iconSize, iconSize),
                    Color.White);
                string name = FitText(ItemData.DisplayName(itemKey), slotSize + 8, FTiny);
                DrawCentered(FTiny, name, sx + slotSize / 2, sy + slotSize + 2,
                    ItemData.DisplayColor(itemKey));
                if (hover && freshClick) unequip = slotKey;
            }
            else
            {
                DrawCentered(FSmall, "—", sx + slotSize / 2, sy + slotSize / 2 - 8,
                    CTextMuted);
            }
        }

        for (int i = 0; i < hintLines.Count; i++)
            DrawCentered(FTiny, hintLines[i], x + w / 2, hintY + i * 14,
                new Color(170, 180, 160));
    }

    // ── Ячейка рюкзака ──
    private void DrawInvSlot(Player player, string key, int count, Rectangle rect,
        bool hover)
    {
        Fill(rect, hover ? new Color(58, 49, 35) : new Color(32, 28, 22),
            hover ? CRowBorder : CBorder);

        int iconSize = Math.Max(20, Math.Min(56, rect.Height - 24));
        var icon = _icons.GetIcon(key);
        _sb.Draw(icon, new Rectangle(rect.X + (rect.Width - iconSize) / 2,
            rect.Y + 3, iconSize, iconSize), Color.White);

        var (baseKey, _) = ItemData.ParseItemKey(key);
        var def = ItemData.Get(baseKey);
        int lvlReq = def?.LevelReq ?? 1;
        var color = ItemData.DisplayColor(key);
        if (lvlReq > 1 && player.Level < lvlReq) color = CHpCrit;
        if (!ClassData.CanUse(player.ClassKey, key).Ok) color = CHpCrit;

        var font = rect.Height < 70 ? FTiny : FSmall;
        string label = FitText(ItemData.DisplayName(key), rect.Width - 6, font);
        DrawCentered(font, label, rect.X + rect.Width / 2,
            rect.Y + rect.Height - 17, color);
    }

    // ── Тултип ──
    private void DrawTooltip(Player player, string key, int count, MouseState mouse)
    {
        var (baseKey, rarity) = ItemData.ParseItemKey(key);
        var it = ItemData.Get(baseKey);
        if (it == null) return;

        var lines = new List<(string Text, Color Color, SpriteFont Font)>
        {
            (ItemData.DisplayName(key), CTitleLight, FBody),
        };
        if (count > 1)
            lines.Add(($"Количество: {count}", CTextDim, FSmall));
        if (!string.IsNullOrEmpty(it.Desc))
            lines.Add((it.Desc!, CTextMuted, FTiny));
        if (it.LevelReq > 1)
        {
            bool ok = player.Level >= it.LevelReq;
            lines.Add(($"Требуется уровень {it.LevelReq}",
                ok ? CTextDim : CHpCrit, FSmall));
        }
        var (canUse, why) = ClassData.CanUse(player.ClassKey, key);
        if (!canUse)
            lines.Add(($"Класс не может: {why}", CHpCrit, FSmall));

        // Бонус надетого предмета.
        int bonus = it.IntValue + rarity;
        if (bonus != 0 && IsEquipmentType(it.Kind))
        {
            string stat = it.Kind == ItemData.ItemType.Weapon ? "ATK" : "DEF";
            lines.Add(($"+{bonus} {stat}", CHeal, FSmall));
        }

        // Сравнение с надетым (только оружие/броня/шлем/поножи).
        string? slot = it.Kind switch
        {
            ItemData.ItemType.Weapon => "weapon",
            ItemData.ItemType.Armor => "armor",
            ItemData.ItemType.Helmet => "head",
            ItemData.ItemType.Leggings => "legs",
            _ => null,
        };
        if (slot != null)
        {
            var deltas = CompareItem(player, key, slot);
            if (deltas.Count > 0)
            {
                bool positive = false;
                int total = 0;
                foreach (var (_, val, pct) in deltas)
                    if (!pct) { total += val; }
                positive = total > 0;
                var parts = new List<string>();
                foreach (var (name, val, pct) in deltas)
                {
                    string sign = val > 0 ? "+" : "−";
                    parts.Add($"{name} {sign}{Math.Abs(val)}{(pct ? "%" : "")}");
                }
                lines.Add(($"{(positive ? "▲" : "▼")} {string.Join(", ", parts)}",
                    positive ? new Color(140, 212, 116) : new Color(210, 92, 74),
                    FSmall));
            }
        }

        // Измерение.
        int maxW = 0, totalH = 0;
        var measured = new List<(string Text, Color Color, SpriteFont Font, int Dy)>();
        foreach (var (text, color, font) in lines)
        {
            string fitted = FitText(text, 400, font);
            var size = font.MeasureString(fitted);
            measured.Add((fitted, color, font, totalH));
            maxW = Math.Max(maxW, (int)size.X);
            totalH += (int)size.Y + 4;
        }

        const int pad = 10;
        int w = maxW + pad * 2;
        int h = totalH + pad * 2 - 4;
        int x = Math.Min(Math.Max(8, mouse.X + 18),
            AethelgardGame.ScreenW - w - 8);
        int ty = Math.Min(Math.Max(8, mouse.Y + 18),
            AethelgardGame.ScreenH - h - 8);

        Fill(new Rectangle(x, ty, w, h), new Color(18, 16, 13), CRowBorder);
        foreach (var (text, color, font, dy) in measured)
            _sb.DrawString(font, text, new Vector2(x + pad, ty + pad + dy), color);
    }

    private static bool IsEquipmentType(ItemData.ItemType kind) => kind is
        ItemData.ItemType.Weapon or ItemData.ItemType.Armor
        or ItemData.ItemType.Helmet or ItemData.ItemType.Leggings
        or ItemData.ItemType.Boots;

    /// <summary>compare_item: разница статов при замене слота (base_renderer.py).</summary>
    public static List<(string Label, int Val, bool Pct)> CompareItem(
        Player p, string newKey, string slot)
    {
        string? equipped = slot switch
        {
            "weapon" => p.Weapon,
            "armor" => p.Armor,
            "head" => p.Head,
            "legs" => p.Legs,
            "boots" => p.Boots,
            _ => null,
        };
        if (equipped == newKey) return new();

        var effects = p.Effects();
        Player.StatResult Stats(string? weapon, string? armor, string? head,
            string? legs, string? boots) =>
            Player.ComputeStats(p.Str, p.Agi, p.Vit, p.Luk, p.DfStat, p.IntStat,
                p.Level, weapon, armor, head, legs, boots, effects);

        var cur = Stats(p.Weapon, p.Armor, p.Head, p.Legs, p.Boots);
        var preview = Stats(
            slot == "weapon" ? newKey : p.Weapon,
            slot == "armor" ? newKey : p.Armor,
            slot == "head" ? newKey : p.Head,
            slot == "legs" ? newKey : p.Legs,
            slot == "boots" ? newKey : p.Boots);

        var deltas = new List<(string, int, bool)>();
        int da = preview.Atk - cur.Atk;
        int dd = preview.Df - cur.Df;
        int dh = preview.MaxHp - cur.MaxHp;
        int dm = preview.MaxMana - cur.MaxMana;
        int dodge = (int)Math.Round((preview.Dodge - cur.Dodge) * 100);
        int crit = (int)Math.Round((preview.Crit - cur.Crit) * 100);
        if (da != 0) deltas.Add(("ATK", da, false));
        if (dd != 0) deltas.Add(("DEF", dd, false));
        if (dh != 0) deltas.Add(("HP", dh, false));
        if (dm != 0) deltas.Add(("Мана", dm, false));
        if (Math.Abs(dodge) >= 1) deltas.Add(("Укл", dodge, true));
        if (Math.Abs(crit) >= 1) deltas.Add(("Крит", crit, true));
        return deltas;
    }

    // ── Примитивы ──

    private int Section(int x, int y, int width, string title)
    {
        var f = F2;
        string text = $"  {title}  ";
        var size = f.MeasureString(text);
        int cx = x + width / 2;
        _sb.DrawString(f, text, new Vector2(cx - size.X / 2, y),
            new Color(190, 172, 138));
        int lineY = y + (int)size.Y / 2;
        int side = (width - (int)size.X) / 2 - 6;
        if (side > 0)
        {
            _sb.Draw(Blank, new Rectangle(x, lineY, side, 1), CBorder);
            _sb.Draw(Blank, new Rectangle(cx + (int)size.X / 2, lineY, side, 1), CBorder);
        }
        return y + (int)size.Y + 6;
    }

    private void DrawPageButton(string label, Rectangle rect, bool enabled, bool hover)
    {
        Fill(rect, enabled && hover ? CRowHover : new Color(30, 27, 21),
            enabled ? CRowBorder : CBorderDark);
        DrawCentered(FBody, label, rect.X + rect.Width / 2,
            rect.Y + rect.Height / 2 - 2, enabled ? CText : CTextMuted);
    }

    private void DrawCross(Rectangle rect, Color color)
    {
        int cx = rect.X + rect.Width / 2, cy = rect.Y + rect.Height / 2;
        const int r = 9, th = 3;
        for (int i = -r; i <= r; i++)
        {
            _sb.Draw(Blank, new Rectangle(cx + i, cy + i, th, th), color);
            _sb.Draw(Blank, new Rectangle(cx + i, cy - i, th, th), color);
        }
    }

    private void Fill(Rectangle r, Color bg, Color border)
    {
        _sb.Draw(Blank, r, bg);
        _sb.Draw(Blank, new Rectangle(r.X, r.Y, r.Width, 2), border);
        _sb.Draw(Blank, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), border);
        _sb.Draw(Blank, new Rectangle(r.X, r.Y, 2, r.Height), border);
        _sb.Draw(Blank, new Rectangle(r.Right - 2, r.Y, 2, r.Height), border);
    }

    private void DrawCentered(SpriteFont font, string text, int cx, int y, Color color)
    {
        var size = font.MeasureString(text);
        _sb.DrawString(font, text, new Vector2(cx - size.X / 2, y), color);
    }

    private void DrawRight(SpriteFont font, string text, int right, int y, Color color)
    {
        var size = font.MeasureString(text);
        _sb.DrawString(font, text, new Vector2(right - size.X, y), color);
    }

    // ── Текст ──

    private static string FitText(string text, int maxW, SpriteFont font)
    {
        if (font.MeasureString(text).X <= maxW) return text;
        string cut = text.TrimEnd();
        while (cut.Length > 0 && font.MeasureString(cut + "…").X > maxW)
            cut = cut[..^1].TrimEnd();
        return cut + "…";
    }

    private static List<string> WrapLines(string text, SpriteFont font, int maxW)
    {
        var lines = new List<string>();
        string current = "";
        foreach (var word in text.Split(' '))
        {
            string candidate = current.Length > 0 ? current + " " + word : word;
            if (font.MeasureString(candidate).X > maxW && current.Length > 0)
            {
                lines.Add(current);
                current = word;
            }
            else current = candidate;
        }
        if (current.Length > 0) lines.Add(current);
        return lines;
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
