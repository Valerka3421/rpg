using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Aethelgard.Core;
using Aethelgard.Game;

namespace Aethelgard.Render;

/// <summary>
/// Shop screen — buy/sell items with city NPCs.
/// Ported from Python render/screens_renderer.py:draw_shop.
///
/// Two modes: Buy (shop stock) and Sell (player inventory).
/// </summary>
public class ShopScreen
{
    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _sb;
    private readonly ItemRenderer _icons;
    private SpriteFont? _font;
    private Texture2D? _blank;

    public ShopScreen(GraphicsDevice device, SpriteBatch sb)
    {
        _device = device;
        _sb = sb;
        _icons = new ItemRenderer(device);
    }

    public void LoadFont(SpriteFont font) => _font = font;

    public void Draw(GameController game)
    {
        if (game.ActiveShopNpc == null) return;
        var npcDef = CityData.Npcs.GetValueOrDefault(game.ActiveShopNpc);
        if (npcDef == null) return;

        var player = game.Player;
        int sw = Game1.ScreenW, sh = Game1.ScreenH;

        // Dark overlay
        _sb.Draw(Blank, new Rectangle(0, 0, sw, sh), new Color(0, 0, 0, 180));

        // Panel
        int panelW = 600, panelH = 460;
        int px = (sw - panelW) / 2;
        int py = (sh - panelH) / 2;
        _sb.Draw(Blank, new Rectangle(px, py, panelW, panelH), new Color(20, 18, 14, 240));
        _sb.Draw(Blank, new Rectangle(px, py, panelW, 2), Config.ColGold);
        _sb.Draw(Blank, new Rectangle(px, py + panelH - 2, panelW, 2), Config.ColGold);

        if (_font == null) return;

        // Title
        _sb.DrawString(_font, $"{npcDef.Name}'s Shop", new Vector2(px + 16, py + 10), Config.ColGold);
        _sb.DrawString(_font, $"Gold: {player.Gold}", new Vector2(px + panelW - 120, py + 10),
            Config.ColGold);

        // Mode indicator
        string modeText = game.ShopSubMode == "buy" ? "[BUY]  Sell (S)" : "Buy (S)  [SELL]";
        _sb.DrawString(_font, modeText, new Vector2(px + 16, py + 34),
            game.ShopSubMode == "buy" ? new Color(120, 200, 120) : new Color(200, 150, 100));

        // Content
        int listY = py + 60;
        if (game.ShopSubMode == "buy")
            DrawBuyList(npcDef, player, px + 16, listY, panelW - 32);
        else
            DrawSellList(player, px + 16, listY, panelW - 32, game.ShopPage);

        // Hint
        _sb.DrawString(_font, "1-8: buy/sell  S: switch mode  Q/E: pages  Esc: close",
            new Vector2(px + 16, py + panelH - 24), Config.ColDim);
    }

    private void DrawBuyList(CityData.NpcDef npcDef, Player player, int x, int y, int w)
    {
        for (int i = 0; i < npcDef.ShopStock.Length; i++)
        {
            string key = npcDef.ShopStock[i];
            var def = ItemData.Get(key);
            if (def == null) continue;

            int rowY = y + i * 40;
            // Row background
            _sb.Draw(Blank, new Rectangle(x, rowY, w, 36), new Color(30, 28, 22));
            _sb.Draw(Blank, new Rectangle(x, rowY, w, 1), new Color(50, 45, 38));

            // Item icon
            var icon = _icons.GetIcon(key);
            _sb.Draw(icon, new Vector2(x + 4, rowY + 2), Color.White);

            // Name
            _sb.DrawString(_font, $"{i + 1}. {def.Name}", new Vector2(x + 44, rowY + 4),
                Config.ColText);

            // Price
            string priceText = $"{def.Price}g";
            var priceColor = player.Gold >= def.Price ? Config.ColGold : new Color(180, 80, 80);
            _sb.DrawString(_font, priceText, new Vector2(x + w - 60, rowY + 4), priceColor);

            // Description
            string desc = def.Type switch
            {
                ItemData.ItemType.Weapon => $"+{def.Value} ATK",
                ItemData.ItemType.Armor or ItemData.ItemType.Helmet
                    or ItemData.ItemType.Leggings or ItemData.ItemType.Boots => $"+{def.Value} DEF",
                ItemData.ItemType.Potion or ItemData.ItemType.Food
                    => def.Effect == "heal" ? $"+{def.Value} HP" : $"+{def.Value} MP",
                _ => "",
            };
            if (!string.IsNullOrEmpty(desc))
                _sb.DrawString(_font, desc, new Vector2(x + 44, rowY + 20), Config.ColDim);
        }
    }

    private void DrawSellList(Player player, int x, int y, int w, int page)
    {
        var items = player.Inventory.ToList();
        int pageStart = page * 8;

        for (int i = 0; i < 8; i++)
        {
            int idx = pageStart + i;
            if (idx >= items.Count) break;

            var (key, count) = items[idx];
            var def = ItemData.Get(key);
            if (def == null) continue;

            int rowY = y + i * 40;
            _sb.Draw(Blank, new Rectangle(x, rowY, w, 36), new Color(30, 28, 22));
            _sb.Draw(Blank, new Rectangle(x, rowY, w, 1), new Color(50, 45, 38));

            var icon = _icons.GetIcon(key);
            _sb.Draw(icon, new Vector2(x + 4, rowY + 2), Color.White);

            string nameText = $"{i + 1}. {def.Name}";
            if (count > 1) nameText += $" x{count}";
            _sb.DrawString(_font, nameText, new Vector2(x + 44, rowY + 4), Config.ColText);

            string sellText = def.SellPrice > 0 ? $"{def.SellPrice}g" : "No sell";
            _sb.DrawString(_font, sellText, new Vector2(x + w - 60, rowY + 4),
                def.SellPrice > 0 ? Config.ColGold : Config.ColDim);
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
