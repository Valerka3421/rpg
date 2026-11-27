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
/// Экран инвентаря — реализация на UiKit (единая UI-база).
/// Слева — панель снаряжения (силуэт персонажа + ячейки Голова/Тело/Руки/Ноги/Обувь
/// с подсказкой класса), справа — сетка рюкзака 10×10 со страницами. Шапка —
/// заголовок, золото и счётчик страниц; внизу — подсказки клавиш. Тултипы с
/// характеристиками и сравнением с надетым рисуются UiKit поверх всего.
/// Клавиатурное управление (Q/E, U/V/H/B/N, Esc) не изменилось — GameController.
/// </summary>
public class InventoryScreen
{
    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _sb;
    private readonly ItemRenderer _icons;

    private static readonly UiKit K = UiKit.Kit;

    public const int GridCols = 10;
    public const int GridRows = 10;

    /// <summary>Синхронизируется с GameController.CurrentPage (World/Game.cs).</summary>
    public int CurrentPage = 0;

    public InventoryScreen(GraphicsDevice device, SpriteBatch sb)
    {
        _device = device;
        _sb = sb;
        _icons = new ItemRenderer(device);
    }

    /// <summary>Шрифты теперь глобальные (Fonts) — метод оставлен для контракта.</summary>
    public void LoadFont(SpriteFont font) { }

    // ── Геометрия модального окна ──
    private static Rectangle PanelRect()
    {
        int sw = AethelgardGame.ScreenW, sh = AethelgardGame.ScreenH;
        return new Rectangle(60, 40, sw - 120, sh - 80);
    }

    /// <summary>Рисует инвентарь и возвращает действие пользователя (клик).</summary>
    public InventoryAction Draw(Player player, MouseState mouse)
    {
        K.Begin(_sb, _device, mouse);

        var panel = PanelRect();
        K.Panel(panel);

        // ── Шапка ──
        var headerRect = new Rectangle(panel.X, panel.Y, panel.Width, 72);
        K.Text("ИНВЕНТАРЬ", new Vector2(headerRect.X + 20, headerRect.Y + 14),
            UiKit.Theme.GoldBright, 2);
        K.Text(player.ClassKey != null
                ? ClassData.Data(player.ClassKey).Name
                : "Персонаж",
            new Vector2(headerRect.X + 20, headerRect.Y + 48),
            UiKit.Theme.TextDim, 4, shadow: false);

        var stacks = player.InventoryStacks();
        int total = Math.Max(1, (stacks.Count + Config.InventoryPageSize - 1)
            / Config.InventoryPageSize);
        int page = Math.Min(CurrentPage, total - 1);
        CurrentPage = page;

        string goldText = $"Золото: {player.Gold}";
        K.Text(goldText, new Vector2(headerRect.Right - 130 - K.FontWidth(goldText, 3),
            headerRect.Y + 16), UiKit.Theme.Gold, 3);
        K.Text($"Предметов: {stacks.Count}   Стр. {page + 1}/{total}",
            new Vector2(headerRect.Right - 130 - K.FontWidth($"Предметов: {stacks.Count}   Стр. {page + 1}/{total}", 4),
                headerRect.Y + 44), UiKit.Theme.TextDim, 4, shadow: false);

        var closeRect = new Rectangle(panel.Right - 44, panel.Y + 12, 32, 32);
        bool close = K.Button(closeRect, "×", size: 3);

        int contentY = headerRect.Bottom + UiKit.Gap;
        int contentH = panel.Bottom - contentY - 40;

        // ── Две колонки ──
        int equipW = (int)(panel.Width * 0.34);
        int invW = panel.Width - UiKit.Pad * 2 - equipW - UiKit.Gap;
        int equipX = panel.X + UiKit.Pad;
        int invX = equipX + equipW + UiKit.Gap;

        string? usedItem = null;
        string? unequip = null;

        // ═══ Левая колонка: снаряжение ═══
        var equipRect = new Rectangle(equipX, contentY, equipW, contentH);
        K.Section(equipRect, "Снаряжение");
        DrawEquipmentPanel(player, equipRect, ref unequip);

        // ═══ Правая колонка: рюкзак ═══
        var invRect = new Rectangle(invX, contentY, invW, contentH);
        K.Section(invRect, "Рюкзак");

        int start = page * Config.InventoryPageSize;
        const int gapCell = 6;
        const int cols = GridCols, rows = GridRows;
        int innerY = invRect.Y + 34;
        int availH = invRect.Bottom - innerY - 44;
        int cellW = Math.Min(72, (invW - UiKit.Pad * 2 - gapCell * (cols - 1)) / cols);
        int cellH = Math.Min(72, Math.Max(40, (availH - gapCell * (rows - 1)) / rows));
        int gridW = cols * cellW + gapCell * (cols - 1);
        int gridX = invX + (invW - gridW) / 2;

        for (int idx = 0; idx < Config.InventoryPageSize; idx++)
        {
            int row = idx / cols, col = idx % cols;
            var rect = new Rectangle(gridX + col * (cellW + gapCell),
                innerY + row * (cellH + gapCell), cellW, cellH);
            int absolute = start + idx;
            if (absolute < stacks.Count)
            {
                var (key, count) = stacks[absolute];
                // Hover-подсветка золотой рамкой + тултип
                bool hot = K.ItemSlot(rect, _icons.GetIcon(key),
                    count > 1 ? count.ToString() : null, selected: K.Hot(rect));
                if (hot)
                {
                    ShowItemTooltip(player, key, count, rect);
                    if (K.Clicked(rect)) usedItem = key;
                }
            }
            else
            {
                K.ItemSlot(rect, null, null, enabled: false);
            }
        }

        if (stacks.Count == 0)
            K.TextCentered("Рюкзак пуст",
                new Rectangle(invRect.X, innerY, invRect.Width, cellH * 2),
                UiKit.Theme.TextMuted, 3);

        // ── Страницы ──
        int pageY = invRect.Bottom - 38;
        const int bw = 40, bh = 28;
        bool prevEnabled = page > 0, nextEnabled = page < total - 1;
        int pageDelta = 0;
        var prevRect = new Rectangle(gridX, pageY, bw, bh);
        if (K.Button(prevRect, "‹", size: 3, enabled: prevEnabled)) pageDelta = -1;
        var nextRect = new Rectangle(gridX + gridW - bw, pageY, bw, bh);
        if (K.Button(nextRect, "›", size: 3, enabled: nextEnabled)) pageDelta = 1;
        K.TextCentered($"Стр. {page + 1}/{total}",
            new Rectangle(gridX + bw + 8, pageY, gridW - bw * 2 - 16, bh),
            UiKit.Theme.TextDim, 4);

        // ── Подсказки внизу ──
        int hy = panel.Bottom - 30;
        K.KeyHint(panel.X + UiKit.Pad, hy, "ЛКМ", "использовать");
        K.KeyHint(panel.X + UiKit.Pad + 150, hy, "Q/E", "страницы");
        K.KeyHint(panel.X + UiKit.Pad + 270, hy, "U/V/H/B/N", "снять снаряжение");
        K.KeyHint(panel.X + UiKit.Pad + 460, hy, "Esc", "выход");

        K.End();
        K.EndInputFrame();
        return new InventoryAction(usedItem, unequip, pageDelta, close);
    }

    // ── Левая панель: силуэт, ячейки экипировки, подсказка класса ──
    private void DrawEquipmentPanel(Player player, Rectangle r, ref string? unequip)
    {
        var cls = ClassData.Data(player.ClassKey);

        // Силуэт персонажа.
        int charSize = Math.Min(140, Math.Max(90, (int)(r.Height * 0.20)));
        charSize = Math.Min(charSize, r.Width - 40);
        int charX = r.X + (r.Width - charSize) / 2;
        int charY = r.Y + 32;
        K.Section(new Rectangle(charX - 8, charY - 8, charSize + 16, charSize + 16));
        var sprite = SpriteStore.Get(_device, "player", player.ClassKey, cls.Color);
        _sb.Draw(sprite, new Rectangle(charX, charY, charSize, charSize), Color.White);

        // Ячейки: голова, тело/руки, ноги, обувь.
        int slotsY = charY + charSize + 28;
        int slotSize = Math.Max(36, Math.Min(52, (r.Width - 40) / 2 - 8));
        const int cellGap = 12;
        int rowStep = slotSize + 34;
        int groupW = 2 * slotSize + cellGap;
        int gx = r.X + (r.Width - groupW) / 2;

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

            K.Text(label, new Vector2(sx, sy - 16), UiKit.Theme.TextDim, 4, shadow: false);
            if (!string.IsNullOrEmpty(itemKey))
            {
                bool hot = K.ItemSlot(rect, _icons.GetIcon(itemKey!), null,
                    selected: K.Hot(rect));
                K.Text(FitText(ItemData.DisplayName(itemKey), slotSize + 24),
                    new Vector2(sx - 12, sy + slotSize + 2),
                    ItemData.DisplayColor(itemKey), 4, shadow: false);
                if (hot)
                {
                    ShowItemTooltip(player, itemKey, 1, rect, unequipHint: true);
                    if (K.Clicked(rect)) unequip = slotKey;
                }
            }
            else
            {
                K.ItemSlot(rect, null, null, enabled: false);
                K.TextCentered("—", rect, UiKit.Theme.TextMuted, 4);
            }
        }

        // Подсказка класса — внизу панели.
        string weapons = cls.Weapons.Length > 0
            ? string.Join(", ", Array.ConvertAll(cls.Weapons,
                k => ClassData.WeaponNames.GetValueOrDefault(k, k)))
            : "—";
        string armor = cls.ArmorTypes.Length > 0
            ? string.Join(", ", Array.ConvertAll(cls.ArmorTypes,
                k => ClassData.ArmorNames.GetValueOrDefault(k, k)))
            : "—";
        var f = Fonts.Tiny;
        int ly = r.Bottom - 8 - WrapLines($"Оружие: {weapons}. Броня: {armor}.", f, r.Width - 24).Count * 14;
        foreach (var line in WrapLines($"Оружие: {weapons}. Броня: {armor}.", f, r.Width - 24))
        {
            K.TextCentered(line,
                new Rectangle(r.X, ly, r.Width, 14), UiKit.Theme.TextMuted, 5);
            ly += 14;
        }
    }

    // ── Тултипы (рисуются UiKit поверх всего в End) ──

    /// <summary>Тултип предмета: характеристики + сравнение с надетым.</summary>
    private void ShowItemTooltip(Player player, string key, int count, Rectangle anchor,
        bool unequipHint = false)
    {
        var (baseKey, rarity) = ItemData.ParseItemKey(key);
        var it = ItemData.Get(baseKey);
        var lines = new List<(string, Microsoft.Xna.Framework.Color)>();
        if (it == null)
        {
            K.Tooltip(anchor, ItemData.DisplayName(key), lines);
            return;
        }

        if (count > 1) lines.Add(($"Количество: {count}", UiKit.Theme.TextDim));
        if (!string.IsNullOrEmpty(it.Desc))
            foreach (var l in Wrap(it.Desc!, Fonts.Small, 240))
                lines.Add((l, UiKit.Theme.TextMuted));

        int bonus = it.IntValue + rarity;
        if (bonus != 0 && IsEquipmentType(it.Kind))
        {
            string stat = it.Kind == ItemData.ItemType.Weapon ? "ATK" : "DEF";
            lines.Add(($"+{bonus} {stat}", UiKit.Theme.Xp));
        }
        foreach (var (lbl, val) in ExtraStats(it))
            lines.Add(($"{lbl} {val:+0;-0}", UiKit.Theme.Xp));

        if (it.Heal > 0) lines.Add(($"+{it.Heal} HP", UiKit.Theme.Hp));
        if (it.Mana > 0) lines.Add(($"+{it.Mana} маны", UiKit.Theme.Mana));

        if (it.LevelReq > 1)
        {
            bool ok = player.Level >= it.LevelReq;
            lines.Add(($"Требуется уровень {it.LevelReq}",
                ok ? UiKit.Theme.TextDim : UiKit.Theme.Danger));
        }
        var (canUse, why) = ClassData.CanUse(player.ClassKey, key);
        if (!canUse)
            lines.Add(($"Класс не может: {why}", UiKit.Theme.Danger));

        // Сравнение с надетым (только оружие/броня/шлем/поножи/обувь).
        string? slot = it.Kind switch
        {
            ItemData.ItemType.Weapon => "weapon",
            ItemData.ItemType.Armor => "armor",
            ItemData.ItemType.Helmet => "head",
            ItemData.ItemType.Leggings => "legs",
            ItemData.ItemType.Boots => "boots",
            _ => null,
        };
        if (slot != null)
        {
            foreach (var (name, val, pct) in CompareItem(player, key, slot))
            {
                Color c = val > 0 ? UiKit.Theme.Xp : val < 0 ? UiKit.Theme.Danger : UiKit.Theme.TextMuted;
                string sign = val > 0 ? "▲ +" : val < 0 ? "▼ " : "   ";
                lines.Add(($"{sign}{name} {Math.Abs(val)}{(pct ? "%" : "")}", c));
            }
        }

        if (unequipHint) lines.Add(("Клик — снять", UiKit.Theme.Gold));
        else if (it.Kind is not (ItemData.ItemType.Weapon or ItemData.ItemType.Armor
            or ItemData.ItemType.Helmet or ItemData.ItemType.Leggings or ItemData.ItemType.Boots))
            lines.Add(("Клик — использовать", UiKit.Theme.Gold));

        K.Tooltip(anchor, ItemData.DisplayName(key), lines);
    }

    private static bool IsEquipmentType(ItemData.ItemType kind) => kind is
        ItemData.ItemType.Weapon or ItemData.ItemType.Armor
        or ItemData.ItemType.Helmet or ItemData.ItemType.Leggings
        or ItemData.ItemType.Boots;

    private static IEnumerable<(string Label, double Val)> ExtraStats(ItemData.Def it)
    {
        if (it.CritBonus != 0) yield return ("Крит", it.CritBonus * 100);
        if (it.DodgeBonus != 0) yield return ("Уклонение", it.DodgeBonus * 100);
        if (it.DfBonus != 0) yield return ("DEF", it.DfBonus);
        if (it.HpBonus != 0) yield return ("HP", it.HpBonus);
        if (it.ManaBonus != 0) yield return ("Мана", it.ManaBonus);
    }

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

    // ── Текст ──

    private static string FitText(string text, int maxW) =>
        FitText(text, maxW, Fonts.Tiny);

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

    private static IEnumerable<string> Wrap(string text, SpriteFont font, int maxW)
    {
        foreach (var paragraph in text.Split('\n'))
            foreach (var line in WrapLines(paragraph, font, maxW))
                yield return line;
    }
}
