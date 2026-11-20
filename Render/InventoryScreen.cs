using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Aethelgard.Core;

namespace Aethelgard.Render;

/// <summary>
/// Inventory screen renderer — 10×8 grid of items with equipment panel.
/// Ported from Python render/screens_renderer.py:draw_inventory.
///
/// Layout:
/// - Left: equipment panel (weapon, armor, helmet, leggings, boots)
/// - Center: 10×8 inventory grid with page navigation
/// - Bottom: item count / page info
///
/// Controls:
/// - 1-8: use/equip item in slot
/// - Q/E: prev/next page
/// - I: close inventory
/// </summary>
public class InventoryScreen
{
    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _sb;
    private readonly ItemRenderer _icons;
    private SpriteFont? _font;
    private Texture2D? _blank;

    public const int GridCols = 10;
    public const int GridRows = 8;
    public const int CellSize = 40;
    public const int CellGap = 2;

    public int CurrentPage = 0;
    public string? HoveredItem;

    public InventoryScreen(GraphicsDevice device, SpriteBatch sb)
    {
        _device = device;
        _sb = sb;
        _icons = new ItemRenderer(device);
    }

    public void LoadFont(SpriteFont font) => _font = font;

    /// <summary>
    /// Draw the inventory overlay.
    /// Returns the item key the user clicked on (for use/equip), or null.
    /// </summary>
    public string? Draw(Player player, MouseState mouse)
    {
        int sw = Game1.ScreenW;
        int sh = Game1.ScreenH;

        // Dark overlay
        _sb.Draw(Blank, new Rectangle(0, 0, sw, sh), new Color(0, 0, 0, 180));

        // Panel
        int panelW = 600, panelH = 480;
        int px = (sw - panelW) / 2;
        int py = (sh - panelH) / 2;
        _sb.Draw(Blank, new Rectangle(px, py, panelW, panelH),
            new Color(20, 18, 14, 240));

        // Border
        _sb.Draw(Blank, new Rectangle(px, py, panelW, 2), Config.ColGold);
        _sb.Draw(Blank, new Rectangle(px, py + panelH - 2, panelW, 2), Config.ColGold);
        _sb.Draw(Blank, new Rectangle(px, py, 2, panelH), Config.ColGold);
        _sb.Draw(Blank, new Rectangle(px + panelW - 2, py, 2, panelH), Config.ColGold);

        // Title
        if (_font != null)
            _sb.DrawString(_font, "INVENTORY", new Vector2(px + 16, py + 10),
                Config.ColGold);

        // ── Equipment panel (left) ──
        int eqX = px + 16;
        int eqY = py + 50;
        DrawEquipmentPanel(player, eqX, eqY);

        // ── Inventory grid (center-right) ──
        int gridX = px + 180;
        int gridY = py + 50;
        int gridW = GridCols * (CellSize + CellGap);
        string? clickedItem = DrawGrid(player, gridX, gridY, mouse);

        // ── Page info ──
        if (_font != null)
        {
            int totalPages = Math.Max(1, (player.Inventory.Count - 1) / (GridCols * GridRows) + 1);
            string pageInfo = $"Page {CurrentPage + 1}/{totalPages}  (Q/E to switch)";
            _sb.DrawString(_font, pageInfo,
                new Vector2(gridX, gridY + GridRows * (CellSize + CellGap) + 8),
                Config.ColDim);
        }

        // ── Hovered item tooltip ──
        if (HoveredItem != null && _font != null)
        {
            DrawTooltip(HoveredItem, mouse);
        }

        return clickedItem;
    }

    private void DrawEquipmentPanel(Player player, int x, int y)
    {
        if (_font == null) return;

        _sb.DrawString(_font, "Equipment", new Vector2(x, y), Config.ColText);
        y += 24;

        var slots = new (string Label, string? Key, string Slot)[]
        {
            ("Weapon", player.Weapon, "Weapon"),
            ("Armor", player.Armor, "Armor"),
            ("Helmet", player.Head, "Head"),
            ("Legs", player.Legs, "Legs"),
            ("Boots", player.Boots, "Boots"),
        };

        foreach (var (label, key, slot) in slots)
        {
            // Slot background
            _sb.Draw(Blank, new Rectangle(x, y, CellSize, CellSize),
                new Color(30, 28, 22));

            // Item icon or empty
            if (key != null)
            {
                var icon = _icons.GetIcon(key);
                _sb.Draw(icon, new Vector2(x + 4, y + 4), Color.White);
            }
            else
            {
                // Empty slot indicator
                _sb.Draw(Blank, new Rectangle(x + 15, y + 15, 10, 10),
                    new Color(50, 45, 38));
            }

            // Label
            _sb.DrawString(_font, label, new Vector2(x + CellSize + 6, y + 10),
                Config.ColDim);

            y += CellSize + 8;
        }

        // Gold
        y += 8;
        _sb.DrawString(_font, $"Gold: {player.Gold}", new Vector2(x, y), Config.ColGold);
    }

    private string? DrawGrid(Player player, int x, int y, MouseState mouse)
    {
        var items = player.Inventory.ToList();
        int pageStart = CurrentPage * GridCols * GridRows;
        string? clicked = null;
        HoveredItem = null;

        for (int row = 0; row < GridRows; row++)
        {
            for (int col = 0; col < GridCols; col++)
            {
                int idx = pageStart + row * GridCols + col;
                int cx = x + col * (CellSize + CellGap);
                int cy = y + row * (CellSize + CellGap);
                var rect = new Rectangle(cx, cy, CellSize, CellSize);

                // Slot background
                _sb.Draw(Blank, rect, new Color(30, 28, 22));

                // Border
                _sb.Draw(Blank, new Rectangle(cx, cy, CellSize, 1),
                    new Color(50, 45, 38));
                _sb.Draw(Blank, new Rectangle(cx, cy + CellSize - 1, CellSize, 1),
                    new Color(50, 45, 38));

                if (idx < items.Count)
                {
                    var (key, count) = items[idx];
                    var icon = _icons.GetIcon(key);
                    _sb.Draw(icon, new Vector2(cx + 4, cy + 4), Color.White);

                    // Count badge
                    if (count > 1 && _font != null)
                    {
                        var text = count.ToString();
                        _sb.DrawString(_font, text,
                            new Vector2(cx + CellSize - 16, cy + CellSize - 14),
                            Config.ColText);
                    }

                    // Hover detection
                    var mouseRect = new Rectangle(mouse.X, mouse.Y, 1, 1);
                    if (rect.Intersects(mouseRect))
                    {
                        // Highlight
                        _sb.Draw(Blank, rect, new Color(240, 210, 90, 60));
                        HoveredItem = key;

                        // Click detection
                        // (click handling is in GameController via returned value)
                    }
                }
            }
        }

        return clicked;
    }

    private void DrawTooltip(string itemKey, MouseState mouse)
    {
        var def = ItemData.Get(itemKey);
        if (def == null || _font == null) return;

        var lines = new List<string>
        {
            def.Name,
        };

        // Type-specific info
        switch (def.Type)
        {
            case ItemData.ItemType.Weapon:
                lines.Add($"+{def.Value} ATK");
                break;
            case ItemData.ItemType.Armor:
            case ItemData.ItemType.Helmet:
            case ItemData.ItemType.Leggings:
            case ItemData.ItemType.Boots:
                lines.Add($"+{def.Value} DEF");
                break;
            case ItemData.ItemType.Potion:
            case ItemData.ItemType.Food:
                lines.Add(def.Effect == "heal" ? $"+{def.Value} HP" : $"+{def.Value} MP");
                break;
            case ItemData.ItemType.Book:
                lines.Add($"+{def.Value} {def.Stat?.ToUpper()}");
                break;
        }

        if (def.SellPrice > 0)
            lines.Add($"Sell: {def.SellPrice}g");

        // Tooltip background
        int ttX = mouse.X + 16;
        int ttY = mouse.Y + 16;
        int maxW = 0;
        foreach (var line in lines)
        {
            var size = _font.MeasureString(line);
            if (size.X > maxW) maxW = (int)size.X;
        }
        int ttW = maxW + 16;
        int ttH = lines.Count * 16 + 12;

        // Keep tooltip on screen
        if (ttX + ttW > Game1.ScreenW) ttX = mouse.X - ttW - 8;
        if (ttY + ttH > Game1.ScreenH) ttY = mouse.Y - ttH - 8;

        _sb.Draw(Blank, new Rectangle(ttX, ttY, ttW, ttH),
            new Color(15, 13, 10, 230));
        _sb.Draw(Blank, new Rectangle(ttX, ttY, ttW, 1), Config.ColGold);

        for (int i = 0; i < lines.Count; i++)
        {
            var color = i == 0 ? Config.ColGold : Config.ColText;
            _sb.DrawString(_font, lines[i], new Vector2(ttX + 8, ttY + 6 + i * 16),
                color);
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
