using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Aethelgard.Render;

/// <summary>Действие пользователя в магазине.</summary>
public readonly record struct ShopAction(
    int BuyIndex, int SellIndex, bool ToggleMode, bool Reroll, bool Close);

/// <summary>
/// Экран магазина — новая реализация на UiKit (единая UI-база).
/// Слева — список товаров/рюкзака, справа — «чек» выбранного предмета
/// (характеристики, цена, сравнение с надетым), шапка — торговец, режим
/// ПОКУПКА/ПРОДАЖА и золото. Клавиатурное управление (S/R/Q/E) не
/// изменилось — обрабатывает GameController.
/// </summary>
public class ShopScreen
{
    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _sb;
    private readonly ItemRenderer _icons;
    private SpriteFont? _font;

    private static readonly UiKit K = UiKit.Kit;

    public ShopScreen(GraphicsDevice device, SpriteBatch sb)
    {
        _device = device;
        _sb = sb;
        _icons = new ItemRenderer(device);
    }

    public void LoadFont(SpriteFont font) => _font = font;

    public ShopAction Draw(GameController game, MouseState mouse)
    {
        var player = game.Player;
        K.Begin(_sb, _device, mouse);

        int sw = AethelgardGame.ScreenW, sh = AethelgardGame.ScreenH;
        var panel = new Rectangle(sw / 2 - 620, 60, 1240, sh - 120);
        K.Panel(panel);

        bool buying = game.ShopSubMode == "buy";
        bool cityShop = CityData.IsCityShopNpc(game.ActiveShopNpc);
        string header = (cityShop
            ? CityData.NpcNames.GetValueOrDefault(game.ActiveShopNpc ?? "", "Торговец")
            : "Торговец").ToUpperInvariant();

        // ── Шапка ──
        var headerRect = new Rectangle(panel.X, panel.Y, panel.Width, 72);
        K.Text(header, new Vector2(headerRect.X + 20, headerRect.Y + 14),
            UiKit.Theme.GoldBright, 2);
        K.Text(cityShop ? "Город Этельгард" : $"Глубина {game.Depth}",
            new Vector2(headerRect.X + 20, headerRect.Y + 48),
            UiKit.Theme.TextDim, 4, shadow: false);

        var toggleRect = new Rectangle(panel.Center.X - 90, panel.Y + 18, 180, 36);
        if (K.Button(toggleRect, buying ? "ПОКУПКА" : "ПРОДАЖА", state: 1))
            buying = !buying;

        string goldText = $"Золото: {player.Gold}";
        K.Text(goldText,
            new Vector2(headerRect.Right - 60 - K.FontWidth(goldText, 3),
                headerRect.Y + 24), UiKit.Theme.Gold, 3);

        var closeRect = new Rectangle(panel.Right - 44, panel.Y + 12, 32, 32);
        bool close = false;
        if (K.Button(closeRect, "×", size: 3)) close = true;

        int contentY = headerRect.Bottom + UiKit.Gap;
        int contentH = panel.Bottom - contentY - 34;
        int listW = 520;
        int listX = panel.X + UiKit.Pad;
        int detailX = listX + listW + UiKit.Gap;
        int detailW = panel.Right - UiKit.Pad - detailX;

        var stock = game.CurrentShopStock();
        int page = Math.Min(game.ShopPage, Math.Max(0, game.ShopPagesCount() - 1));
        int pageStart = page * Config.ShopPageSize;

        int buyIndex = -1, sellIndex = -1;
        (string Key, int Price, bool Buy)? hovered = null;

        // ── Список (товары или рюкзак) ──
        var listRect = new Rectangle(listX, contentY, listW, contentH);
        K.Section(listRect, buying ? "Товары торговца" : "Рюкзак — продать");

        List<(string Key, int Count)> items = buying ? new() : player.InventoryStacks();
        int pageCount = Math.Max(1, game.ShopPagesCount());
        int rows = Config.ShopPageSize;
        int rowH = 44;
        int innerY = listRect.Y + 30;

        for (int i = 0; i < rows; i++)
        {
            int idx = pageStart + i;
            int ry = innerY + i * (rowH + 4);
            if (ry + rowH > listRect.Bottom - (pageCount > 1 ? 34 : 6)) break;

            if (buying)
            {
                if (idx >= stock.Count) break;
                var entry = stock[idx];
                var (baseKey, _) = ItemData.ParseItemKey(entry.Key);
                var def = ItemData.Get(baseKey);
                bool learned = def?.Kind == ItemData.ItemType.Book
                    && IsPerkStat(def.Stat) && PerkLearned(player, def.Stat!);
                bool dim = entry.Sold || learned;
                var rowRect = new Rectangle(listRect.X + 8, ry, listRect.Width - 16, rowH);
                K.SlotIcon(rowRect, _icons.GetIcon(entry.Key), 34);
                K.Text(ItemData.DisplayName(entry.Key),
                    new Vector2(rowRect.X + 52, ry + 5),
                    dim ? UiKit.Theme.TextMuted : ItemData.DisplayColor(entry.Key), 4);
                K.Text(entry.Sold ? "продано" : learned ? "уже изучено" : $"{entry.Price} з.",
                    new Vector2(rowRect.X + 52, ry + 24),
                    dim ? UiKit.Theme.TextMuted : UiKit.Theme.Gold, 4, shadow: false);

                if (K.Hoverable(rowRect) && !dim)
                {
                    hovered = (entry.Key, entry.Price, true);
                    if (K.Clicked(rowRect)) buyIndex = i;
                }
            }
            else
            {
                if (idx >= items.Count)
                {
                    if (idx == pageStart)
                        K.TextCentered("Рюкзак пуст",
                            new Rectangle(listRect.X, ry, listRect.Width, rowH),
                            UiKit.Theme.TextMuted, 3);
                    break;
                }
                var (key, count) = items[idx];
                int price = ItemData.SellPrice(key);
                var rowRect = new Rectangle(listRect.X + 8, ry, listRect.Width - 16, rowH);
                K.SlotIcon(rowRect, _icons.GetIcon(key), 34);
                K.Text($"{ItemData.DisplayName(key)}{(count > 1 ? $" ×{count}" : "")}",
                    new Vector2(rowRect.X + 52, ry + 5), ItemData.DisplayColor(key), 4);
                K.Text($"{price} з.", new Vector2(rowRect.X + 52, ry + 24),
                    UiKit.Theme.Gold, 4, shadow: false);

                if (K.Hoverable(rowRect))
                {
                    hovered = (key, price, false);
                    if (K.Clicked(rowRect)) sellIndex = i;
                }
            }
        }

        // ── Пагинация ──
        if (pageCount > 1)
        {
            var pageRect = new Rectangle(listX + 8, listRect.Bottom - 30, listW - 16, 24);
            K.TextCentered($"Стр. {page + 1}/{pageCount}  (Q/E)", pageRect,
                UiKit.Theme.TextMuted, 4);
        }

        // ── Правая панель: «чек» выбранного предмета ──
        var detailRect = new Rectangle(detailX, contentY, detailW, contentH);
        K.Section(detailRect, "Описание");
        if (hovered.HasValue)
            DrawDetail(game, detailRect, hovered.Value.Key, hovered.Value.Price, hovered.Value.Buy);
        else
            K.TextCentered(buying
                    ? "Наведите курсор на товар,\nчтобы увидеть характеристики"
                    : "Наведите курсор на предмет\nрюкзака, чтобы продать",
                new Rectangle(detailRect.X, detailRect.Y + 40, detailRect.Width, 80),
                UiKit.Theme.TextMuted, 4);

        // ── Подсказки внизу ──
        string hint = buying
            ? "Клик — купить · Q/E — страницы · S — режим · Esc — выход"
            : "Клик — продать · Q/E — страницы · S — режим · Esc — выход";
        if (!cityShop) hint += " · R — обновить сток";
        K.TextCentered(hint,
            new Rectangle(panel.X, panel.Bottom - 30, panel.Width, 24),
            UiKit.Theme.TextMuted, 4);

        K.End();
        K.EndInputFrame();
        return new ShopAction(buyIndex, sellIndex,
            buying != (game.ShopSubMode == "buy"), false, close);
    }

    /// <summary>Правая панель: характеристики, цена, сравнение с надетым.</summary>
    private void DrawDetail(GameController game, Rectangle r, string key, int price, bool buy)
    {
        var player = game.Player;
        var (baseKey, rarity) = ItemData.ParseItemKey(key);
        var it = ItemData.Get(baseKey);
        if (it == null) return;

        int x = r.X + 12;
        int y = r.Y + 32;
        int w = r.Width - 24;

        // Иконка + имя + тип
        _sb.Draw(_icons.GetIcon(key), new Rectangle(x, y, 48, 48), Color.White);
        K.Text(ItemData.DisplayName(key), new Vector2(x + 58, y + 2),
            ItemData.DisplayColor(key), 3);
        string typeName = it.Type switch
        {
            "weapon" => "Оружие", "armor" => "Броня", "head" or "helmet" => "Шлем",
            "legs" or "leggings" => "Поножи", "boots" => "Обувь",
            "heal" => "Зелье лечения", "mana" => "Зелье маны",
            "poison" => "Яд", "buff" => "Эликсир", "resource" => "Ресурс",
            "material" => "Материал", "book" => "Книга", "scroll" => "Свиток",
            "rune" => "Руна", "key" => "Ключ", "blueprint" => "Чертёж",
            "food" => "Еда", "gold" => "Золото", _ => "Предмет",
        };
        K.Text(typeName + (rarity > 0 ? $"  +{rarity}" : ""),
            new Vector2(x + 58, y + 28), UiKit.Theme.TextDim, 4, shadow: false);
        y += 62;

        if (!string.IsNullOrEmpty(it.Desc))
            y = DrawWrapped(it.Desc, x, y, w, UiKit.Theme.TextMuted) + 6;

        // Характеристики снаряжения
        bool equipment = it.Type is "weapon" or "armor" or "head" or "helmet"
            or "legs" or "leggings" or "boots";
        if (equipment)
        {
            int bonus = it.IntValue + rarity;
            if (bonus != 0)
            {
                string stat = it.Type == "weapon" ? "ATK" : "DEF";
                K.Text($"+{bonus} {stat}", new Vector2(x, y), UiKit.Theme.Xp, 4, shadow: false);
                y += 20;
            }
            foreach (var (lbl, val) in ExtraStats(it))
            {
                K.Text($"{lbl} {val:+0;-0}", new Vector2(x, y), UiKit.Theme.Xp, 4, shadow: false);
                y += 20;
            }

            // Сравнение с надетым
            string? slot = it.Type switch
            {
                "weapon" => "weapon", "armor" => "armor",
                "head" or "helmet" => "head", "legs" or "leggings" => "legs", _ => null,
            };
            if (slot != null)
            {
                var deltas = InventoryScreen.CompareItem(player, key, slot);
                foreach (var (name, val, pct) in deltas)
                {
                    Color c = val > 0 ? UiKit.Theme.Xp : val < 0 ? UiKit.Theme.Danger : UiKit.Theme.TextMuted;
                    string sign = val > 0 ? "▲ +" : val < 0 ? "▼ " : "   ";
                    K.Text($"{sign}{name} {Math.Abs(val)}{(pct ? "%" : "")}",
                        new Vector2(x, y), c, 4, shadow: false);
                    y += 20;
                }
                y += 4;
            }
        }
        else if (it.Heal > 0 || it.Mana > 0)
        {
            if (it.Heal > 0) { K.Text($"+{it.Heal} HP", new Vector2(x, y), UiKit.Theme.Hp, 4, shadow: false); y += 20; }
            if (it.Mana > 0) { K.Text($"+{it.Mana} маны", new Vector2(x, y), UiKit.Theme.Mana, 4, shadow: false); y += 20; }
        }

        if (it.LevelReq > 1)
        {
            bool ok = player.Level >= it.LevelReq;
            K.Text($"Требуется уровень {it.LevelReq}", new Vector2(x, y),
                ok ? UiKit.Theme.TextMuted : UiKit.Theme.Danger, 4, shadow: false);
            y += 20;
        }
        var (canUse, why) = ClassData.CanUse(player.ClassKey, key);
        if (!canUse)
            y = DrawWrapped($"Класс не может носить: {why}", x, y, w, UiKit.Theme.Danger) + 4;

        // Цена — внизу крупно
        string priceText = buy ? $"Цена: {price} з." : $"Продажа: {price} з.";
        bool afford = !buy || player.Gold >= price;
        K.Text(priceText, new Vector2(x, r.Bottom - 44),
            afford ? UiKit.Theme.GoldBright : UiKit.Theme.Danger, 3);
        if (buy && !afford)
            K.Text("Не хватает золота", new Vector2(x, r.Bottom - 22),
                UiKit.Theme.Danger, 4, shadow: false);
    }

    private static IEnumerable<(string Label, double Val)> ExtraStats(ItemData.Def it)
    {
        if (it.CritBonus != 0) yield return ("Крит", it.CritBonus * 100);
        if (it.DodgeBonus != 0) yield return ("Уклонение", it.DodgeBonus * 100);
        if (it.DfBonus != 0) yield return ("DEF", it.DfBonus);
        if (it.HpBonus != 0) yield return ("HP", it.HpBonus);
        if (it.ManaBonus != 0) yield return ("Мана", it.ManaBonus);
    }

    private int DrawWrapped(string text, int x, int y, int maxW, Color color)
    {
        var f = Fonts.Small;
        foreach (var line in Wrap(text, maxW, f))
        {
            _sb.DrawString(f, line, new Vector2(x, y), color);
            y += 18;
        }
        return y;
    }

    private static IEnumerable<string> Wrap(string text, int maxW, SpriteFont f)
    {
        foreach (var paragraph in text.Split('\n'))
        {
            string line = "";
            foreach (var word in paragraph.Split(' '))
            {
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (f.MeasureString(candidate).X > maxW && line.Length > 0)
                {
                    yield return line;
                    line = word;
                }
                else line = candidate;
            }
            if (line.Length > 0) yield return line;
        }
    }

    private static bool IsPerkStat(string? stat) => stat is
        "double_strike" or "regen" or "vampire" or "thorns" or "greedy";

    private static bool PerkLearned(Player p, string stat) => stat switch
    {
        "double_strike" => p.DoubleStrike,
        "regen" => p.Regen,
        "vampire" => p.Vampire,
        "thorns" => p.Thorns,
        "greedy" => p.Greedy,
        _ => false,
    };
}
