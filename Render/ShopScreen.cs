using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
namespace Aethelgard.Render;

/// <summary>Р”РµР№СЃС‚РІРёРµ РїРѕР»СЊР·РѕРІР°С‚РµР»СЏ РІ РјР°РіР°Р·РёРЅРµ.</summary>
public readonly record struct ShopAction(
    int BuyIndex, int SellIndex, bool ToggleMode, bool Reroll, bool Close);

/// <summary>
/// Р­РєСЂР°РЅ РјР°РіР°Р·РёРЅР° вЂ” С‚РѕС‡РЅС‹Р№ РїРѕСЂС‚ python render/screens_renderer/_shop.py:
/// РјРѕРґР°Р»СЊРЅР°СЏ РїР°РЅРµР»СЊ, РєСЂСѓРїРЅС‹Р№ Р·Р°РіРѕР»РѕРІРѕРє С‚РѕСЂРіРѕРІС†Р°, РєРЅРѕРїРєР°-РїРµСЂРµРєР»СЋС‡Р°С‚РµР»СЊ
/// В«РџРћРљРЈРџРљРђ/РџР РћР”РђР–РђВ», СЃРµС‚РєР° С‚РѕРІР°СЂРѕРІ 3 РєРѕР»РѕРЅРєРё РІ СЃС‚РёР»Рµ РёРЅРІРµРЅС‚Р°СЂСЏ,
/// С‚СѓР»С‚РёРїС‹ СЃ С…Р°СЂР°РєС‚РµСЂРёСЃС‚РёРєР°РјРё Рё С†РµРЅРѕР№.
/// </summary>
public class ShopScreen
{
    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _sb;
    private readonly ItemRenderer _icons;
    private SpriteFont? _font;
    private Texture2D? _blank;

    private const int ShopCols = 3;
    private const int ShopGap = 10;

    private bool _clickArmed = true;

    // в”Ђв”Ђ РџР°Р»РёС‚СЂР° theme.py в”Ђв”Ђ
    private static readonly Color CText = new(222, 214, 198);
    private static readonly Color CTextDim = new(150, 140, 120);
    private static readonly Color CTextMuted = new(98, 90, 74);
    private static readonly Color CTitleLight = new(238, 222, 188);
    private static readonly Color CGold = new(206, 166, 82);
    private static readonly Color CMerch = new(206, 170, 96);
    private static readonly Color CBorder = new(78, 66, 48);
    private static readonly Color CRowBorder = new(110, 91, 62);
    private static readonly Color CHpCrit = new(220, 84, 64);
    private static readonly Color CHeal = new(120, 172, 104);
    private static readonly Color CBg = new(14, 12, 10);
    private static readonly Color CKeyText = new(216, 206, 186);

    public ShopScreen(GraphicsDevice device, SpriteBatch sb)
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

    public ShopAction Draw(GameController game, MouseState mouse)
    {
        var player = game.Player;
        var panel = PanelRect();
        int cx0 = panel.X + 20;
        int cy0 = panel.Y + 56;
        int cw = panel.Width - 20 * 2 - 34 - 10;
        int ch = panel.Height - 56 - 20;

        bool freshClick = mouse.LeftButton == ButtonState.Pressed && _clickArmed;
        if (mouse.LeftButton == ButtonState.Released) _clickArmed = true;
        else if (freshClick) _clickArmed = false;

        // в”Ђв”Ђ Р¤РѕРЅ в”Ђв”Ђ
        _sb.Draw(Blank, new Rectangle(0, 0, AethelgardGame.ScreenW,
            AethelgardGame.ScreenH), new Color(6, 5, 4, 165));
        Fill(panel, new Color(30, 26, 21, 238), CBorder);

        var close = CloseRect();
        bool closeHover = close.Contains(mouse.X, mouse.Y);
        Fill(close, closeHover ? new Color(72, 34, 28) : new Color(54, 26, 22),
            new Color(160, 68, 58));
        DrawCross(close, new Color(200, 74, 64));

        if (_font == null) return default;

        bool buying = game.ShopSubMode == "buy";
        bool cityShop = CityData.IsCityShopNpc(game.ActiveShopNpc);
        string header = (cityShop
            ? CityData.NpcNames.GetValueOrDefault(game.ActiveShopNpc ?? "", "РўРѕСЂРіРѕРІРµС†")
            : "РўРѕСЂРіРѕРІРµС†").ToUpperInvariant();

        // в”Ђв”Ђ Р—Р°РіРѕР»РѕРІРѕРє в”Ђв”Ђ
        var headerSize = F1.MeasureString(header);
        _sb.DrawString(F1, header, new Vector2(cx0, cy0), CMerch);

        // в”Ђв”Ђ РџРµСЂРµРєР»СЋС‡Р°С‚РµР»СЊ СЂРµР¶РёРјР° в”Ђв”Ђ
        const int bw = 150, bh = 30;
        var modeRect = new Rectangle(cx0 + (int)headerSize.X + 16, cy0 + 2, bw, bh);
        bool modeHover = modeRect.Contains(mouse.X, mouse.Y);
        var modeBg = buying ? CMerch : CHpCrit;
        if (modeHover)
            modeBg = Shade(modeBg, 1.25f);
        Fill(modeRect, modeBg, Shade(modeBg, 1.5f));
        DrawCentered(FBody, buying ? "РџРћРљРЈРџРљРђ" : "РџР РћР”РђР–Рђ",
            modeRect.X + modeRect.Width / 2, modeRect.Y + modeRect.Height / 2 - 11,
            buying ? CBg : new Color(255, 245, 235));

        // в”Ђв”Ђ РЎРїСЂР°РІР°: РјРµСЃС‚Рѕ Рё Р·РѕР»РѕС‚Рѕ в”Ђв”Ђ
        string where = cityShop ? "Р“РѕСЂРѕРґ Р­С‚РµР»СЊРіР°СЂРґ" : $"Р“Р»СѓР±РёРЅР° {game.Depth}";
        DrawRight(FBody, where, ModalRight(), cy0 + 8, CTextDim);
        DrawRight(FBody, $"${player.Gold}", ModalRight(), cy0 + 34, CGold);

        int y = cy0 + 62;
        var stock = game.CurrentShopStock();
        int page = Math.Min(game.ShopPage, game.ShopPagesCount() - 1);
        int pageStart = page * Config.ShopPageSize;
        int buyIndex = -1, sellIndex = -1;
        (string Key, int Price, string Mode)? hovered = null;

        if (buying)
        {
            y = Section(cx0, y, cw, "РўРѕРІР°СЂС‹");
            var m = ComputeGrid(cx0, y, cw, ch, cy0);
            for (int i = 0; i < Config.ShopPageSize; i++)
            {
                int idx = pageStart + i;
                if (idx >= stock.Count) break;
                var entry = stock[idx];
                string key = entry.Key;
                var (baseKey, _) = ItemData.ParseItemKey(key);
                var def = ItemData.Get(baseKey);
                bool learned = false;
                if (def?.Kind == ItemData.ItemType.Book && IsPerkStat(def.Stat))
                    learned = PerkLearned(player, def.Stat!);
                var rect = SlotRect(i, m, y);
                bool hover = rect.Contains(mouse.X, mouse.Y);
                bool dim = entry.Sold || learned;
                string status = entry.Sold ? "РїСЂРѕРґР°РЅРѕ" : learned ? "РёР·СѓС‡РµРЅРѕ" : "";
                DrawSlot(key, 1, rect, entry.Price, hover, dim, status);
                if (hover && !dim) hovered = (key, entry.Price, "buy");
                if (hover && !dim && freshClick) buyIndex = i;
            }
            if (hovered.HasValue)
                DrawTooltip(game, hovered.Value.Key, hovered.Value.Price,
                    hovered.Value.Mode, mouse);
            if (game.ShopPagesCount() > 1)
                DrawCentered(FSmall, $"РЎС‚СЂ. {page + 1}/{game.ShopPagesCount()}",
                    cx0 + cw / 2, cy0 + ch - 44, CTextDim);
        }
        else
        {
            y = Section(cx0, y, cw, "РџСЂРѕРґР°С‚СЊ");
            var items = player.InventoryStacks();
            var m = ComputeGrid(cx0, y, cw, ch, cy0);
            if (items.Count == 0)
            {
                DrawCentered(FBody, "Р СЋРєР·Р°Рє РїСѓСЃС‚", cx0 + cw / 2, y + 40, CTextMuted);
            }
            else
            {
                for (int i = 0; i < Config.ShopPageSize; i++)
                {
                    int idx = pageStart + i;
                    if (idx >= items.Count) break;
                    var (key, count) = items[idx];
                    int price = ItemData.SellPrice(key);
                    var rect = SlotRect(i, m, y);
                    bool hover = rect.Contains(mouse.X, mouse.Y);
                    DrawSlot(key, count, rect, price, hover, false, "");
                    if (hover) hovered = (key, price, "sell");
                    if (hover && freshClick) sellIndex = i;
                }
                if (hovered.HasValue)
                    DrawTooltip(game, hovered.Value.Key, hovered.Value.Price,
                        hovered.Value.Mode, mouse);
                if (game.ShopPagesCount() > 1)
                    DrawCentered(FSmall, $"РЎС‚СЂ. {page + 1}/{game.ShopPagesCount()}",
                        cx0 + cw / 2, cy0 + ch - 44, CTextDim);
            }
        }

        // в”Ђв”Ђ РџРѕРґСЃРєР°Р·РєР° в”Ђв”Ђ
        string action = buying ? "РєСѓРїРёС‚СЊ" : "РїСЂРѕРґР°С‚СЊ";
        string hint = $"S / РєРЅРѕРїРєР° вЂ” СЂРµР¶РёРј В· РєР»РёРє вЂ” {action} В· Q/E вЂ” СЃС‚СЂР°РЅРёС†С‹";
        if (!cityShop) hint += " В· R вЂ” РѕР±РЅРѕРІРёС‚СЊ";
        DrawCentered(FSmall, hint, cx0 + cw / 2, cy0 + ch - 22, CTextMuted);

        bool toggle = freshClick && modeRect.Contains(mouse.X, mouse.Y);
        bool reroll = false; // R РѕР±СЂР°Р±Р°С‚С‹РІР°РµС‚СЃСЏ РєР»Р°РІРёР°С‚СѓСЂРѕР№ РІ GameController
        bool closeAction = freshClick && close.Contains(mouse.X, mouse.Y);
        return new ShopAction(buyIndex, sellIndex, toggle, reroll, closeAction);
    }

    private readonly record struct ShopGrid(
        int Cols, int Rows, int Gap, int CellW, int CellH, int GridX);

    private static ShopGrid ComputeGrid(int x0, int y, int cw, int ch, int cy0)
    {
        const int cols = ShopCols, gap = ShopGap;
        int rows = Math.Max(1, (Config.ShopPageSize + cols - 1) / cols);
        int availH = (cy0 + ch - 34) - y - 8;
        int cellW = Math.Min(112, (cw - gap * (cols - 1)) / cols);
        int cellH = Math.Min(96, Math.Max(64, (availH - gap * (rows - 1)) / rows));
        int gridW = cols * cellW + gap * (cols - 1);
        int gridX = x0 + (cw - gridW) / 2;
        return new ShopGrid(cols, rows, gap, cellW, cellH, gridX);
    }

    private static Rectangle SlotRect(int i, ShopGrid m, int y)
    {
        int row = i / m.Cols, col = i % m.Cols;
        return new Rectangle(m.GridX + col * (m.CellW + m.Gap),
            y + row * (m.CellH + m.Gap), m.CellW, m.CellH);
    }

    private void DrawSlot(string key, int count, Rectangle rect, int price,
        bool hover, bool dim, string status)
    {
        var bg = dim ? new Color(26, 24, 20)
            : hover ? new Color(58, 49, 35) : new Color(32, 28, 22);
        Fill(rect, bg, hover ? CRowBorder : CBorder);

        int iconSize = Math.Max(24, Math.Min(52, rect.Height - 34));
        var icon = _icons.GetIcon(key);
        _sb.Draw(icon, new Rectangle(rect.X + (rect.Width - iconSize) / 2,
            rect.Y + 5, iconSize, iconSize), Color.White);

        var color = dim ? CTextMuted : ItemData.DisplayColor(key);
        string label = FitText(ItemData.DisplayName(key), rect.Width - 8, FSmall);
        DrawCentered(FSmall, label, rect.X + rect.Width / 2,
            rect.Y + rect.Height - 30, color);
        if (status.Length > 0)
            DrawCentered(FTiny, status, rect.X + rect.Width / 2,
                rect.Y + rect.Height - 14, CHpCrit);
        else if (price > 0)
            DrawCentered(FTiny, $"{price} Р·.", rect.X + rect.Width / 2,
                rect.Y + rect.Height - 14, dim ? CTextMuted : CGold);
        if (count > 1)
            DrawRight(FTiny, $"Г—{count}", rect.Right - 6, rect.Y + 3, CText);
    }

    // в”Ђв”Ђ РўСѓР»С‚РёРї РјР°РіР°Р·РёРЅР° в”Ђв”Ђ
    private void DrawTooltip(GameController game, string key, int price,
        string mode, MouseState mouse)
    {
        var player = game.Player;
        var (baseKey, rarity) = ItemData.ParseItemKey(key);
        var it = ItemData.Get(baseKey);
        if (it == null) return;

        var lines = new List<(string Text, Color Color, SpriteFont Font)>
        {
            (ItemData.DisplayName(key), CTitleLight, FBody),
        };
        string? typeName = it.Type switch
        {
            "weapon" => "РћСЂСѓР¶РёРµ", "armor" => "Р‘СЂРѕРЅСЏ", "head" or "helmet" => "РЁР»РµРј",
            "legs" or "leggings" => "РЁС‚Р°РЅС‹", "boots" => "РћР±СѓРІСЊ",
            "heal" => "Р—РµР»СЊРµ Р»РµС‡РµРЅРёСЏ", "mana" => "Р—РµР»СЊРµ РјР°РЅС‹",
            "poison" => "РЇРґ", "buff" => "Р­Р»РёРєСЃРёСЂ", "resource" => "Р РµСЃСѓСЂСЃ",
            "book" => "РљРЅРёРіР°", "key" => "РљР»СЋС‡",
            _ => null,
        };
        if (typeName != null) lines.Add((typeName, CTextDim, FSmall));
        if (!string.IsNullOrEmpty(it.Desc)) lines.Add((it.Desc!, CTextMuted, FTiny));

        bool equipment = it.Type is "weapon" or "armor" or "head" or "helmet"
            or "legs" or "leggings" or "boots";
        if (equipment)
        {
            int bonus = it.IntValue + rarity;
            string stat = it.Type == "weapon" ? "ATK" : "DEF";
            if (bonus != 0) lines.Add(($"+{bonus} {stat}", CHeal, FSmall));
            string? slot = it.Type switch
            {
                "weapon" => "weapon", "armor" => "armor",
                "head" or "helmet" => "head",
                "legs" or "leggings" => "legs",
                _ => null,
            };
            if (slot != null)
            {
                var deltas = InventoryScreen.CompareItem(player, key, slot);
                if (deltas.Count > 0)
                {
                    int total = 0;
                    var parts = new List<string>();
                    foreach (var (name, val, pct) in deltas)
                    {
                        if (!pct) total += val;
                        string sign = val > 0 ? "+" : "в€’";
                        parts.Add($"{name} {sign}{Math.Abs(val)}{(pct ? "%" : "")}");
                    }
                    bool positive = total > 0;
                    lines.Add(($"{(positive ? "в–І" : "в–ј")} {string.Join(", ", parts)}",
                        positive ? new Color(140, 212, 116) : new Color(210, 92, 74),
                        FSmall));
                }
            }
        }

        if (it.LevelReq > 1)
        {
            bool ok = player.Level >= it.LevelReq;
            lines.Add(($"РўСЂРµР±СѓРµС‚СЃСЏ СѓСЂРѕРІРµРЅСЊ {it.LevelReq}",
                ok ? CTextDim : CHpCrit, FSmall));
        }
        var (canUse, why) = ClassData.CanUse(player.ClassKey, key);
        if (!canUse) lines.Add(($"РљР»Р°СЃСЃ РЅРµ РјРѕР¶РµС‚: {why}", CHpCrit, FSmall));

        if (mode == "buy")
        {
            bool afford = player.Gold >= price;
            lines.Add(($"Р¦РµРЅР°: {price} Р·.", afford ? CGold : CHpCrit, FBody));
        }
        else
        {
            lines.Add(($"РџСЂРѕРґР°Р¶Р°: {price} Р·.", CGold, FBody));
        }

        int maxW = 0, totalH = 0;
        var measured = new List<(string Text, Color Color, SpriteFont Font, int Dy)>();
        foreach (var (text, color, font) in lines)
        {
            string fitted = FitText(text, 380, font);
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

    // в”Ђв”Ђ РџСЂРёРјРёС‚РёРІС‹ в”Ђв”Ђ
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

    private static string FitText(string text, int maxW, SpriteFont font)
    {
        if (font.MeasureString(text).X <= maxW) return text;
        string cut = text.TrimEnd();
        while (cut.Length > 0 && font.MeasureString(cut + "вЂ¦").X > maxW)
            cut = cut[..^1].TrimEnd();
        return cut + "вЂ¦";
    }

    private static Color Shade(Color c, float k) => new(
        Math.Clamp((int)(c.R * k), 0, 255),
        Math.Clamp((int)(c.G * k), 0, 255),
        Math.Clamp((int)(c.B * k), 0, 255));

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


