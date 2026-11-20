using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Aethelgard.Core;

namespace Aethelgard.Render;

/// <summary>
/// Procedural item icon renderer — draws 32×32 icons for inventory.
/// Ported from Python render/icons/_items.py (ItemMixin).
///
/// Each item type has a distinct visual:
/// - Weapons: blade/axe/hammer shape
/// - Armor: body shape
/// - Potions: bottle with liquid
/// - Food: mushroom/berry/bread
/// - Resources: wood/stone/ore
/// - Books: rectangle with symbol
/// </summary>
public class ItemRenderer
{
    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _sb;
    private readonly Dictionary<string, Texture2D> _cache = new();
    private Texture2D? _blank;

    public const int IconSize = 32;

    public ItemRenderer(GraphicsDevice device)
    {
        _device = device;
        _sb = new SpriteBatch(device);
    }

    /// <summary>
    /// Get (or generate) an item icon texture.
    /// </summary>
    public Texture2D GetIcon(string itemKey)
    {
        if (_cache.TryGetValue(itemKey, out var tex))
            return tex;

        var def = ItemData.Get(itemKey);
        if (def == null)
            return GetBlank();

        var rt = new RenderTarget2D(_device, IconSize, IconSize,
            false, SurfaceFormat.Color, DepthFormat.None);
        _device.SetRenderTarget(rt);
        _device.Clear(Color.Transparent);
        _sb.Begin();

        switch (def.Type)
        {
            case ItemData.ItemType.Weapon:
                DrawWeapon(_sb, def);
                break;
            case ItemData.ItemType.Armor:
                DrawArmor(_sb, def);
                break;
            case ItemData.ItemType.Helmet:
                DrawHelmet(_sb, def);
                break;
            case ItemData.ItemType.Potion:
                DrawPotion(_sb, def);
                break;
            case ItemData.ItemType.Food:
                DrawFood(_sb, def);
                break;
            case ItemData.ItemType.Resource:
                DrawResource(_sb, def);
                break;
            case ItemData.ItemType.Book:
                DrawBook(_sb, def);
                break;
            case ItemData.ItemType.Key:
                DrawKey(_sb, def);
                break;
            default:
                DrawUnknown(_sb, def);
                break;
        }

        _sb.End();
        _device.SetRenderTarget(null);
        _cache[itemKey] = rt;
        return rt;
    }

    private void DrawWeapon(SpriteBatch sb, ItemData.Def def)
    {
        // Blade — vertical line with crossguard
        var color = def.Color;
        // Handle
        sb.Draw(Blank, new Rectangle(14, 24, 4, 6), new Color(80, 60, 40));
        // Crossguard
        sb.Draw(Blank, new Rectangle(10, 22, 12, 2), new Color(160, 140, 80));
        // Blade
        int bladeLen = def.Key == "warhammer" ? 18 : def.Key == "axe" ? 16 : 20;
        sb.Draw(Blank, new Rectangle(15, 22 - bladeLen, 2, bladeLen), color);
        // Blade tip
        sb.Draw(Blank, new Rectangle(15, 22 - bladeLen - 2, 2, 2), color);

        if (def.Key == "axe")
        {
            // Axe head — triangle
            sb.Draw(Blank, new Rectangle(11, 8, 10, 8), color);
        }
        else if (def.Key == "warhammer")
        {
            // Hammer head — rectangle
            sb.Draw(Blank, new Rectangle(10, 6, 12, 6), color);
        }
    }

    private void DrawArmor(SpriteBatch sb, ItemData.Def def)
    {
        var color = def.Color;
        // Body shape — trapezoid
        sb.Draw(Blank, new Rectangle(10, 8, 12, 4), color);    // shoulders
        sb.Draw(Blank, new Rectangle(11, 12, 10, 12), color);  // torso
        sb.Draw(Blank, new Rectangle(12, 24, 8, 4), color);    // waist
        // Highlight
        var light = new Color(
            Math.Min(255, color.R + 30),
            Math.Min(255, color.G + 30),
            Math.Min(255, color.B + 30));
        sb.Draw(Blank, new Rectangle(12, 10, 3, 14), light);
    }

    private void DrawHelmet(SpriteBatch sb, ItemData.Def def)
    {
        var color = def.Color;
        // Dome shape
        sb.Draw(Blank, new Rectangle(10, 8, 12, 2), color);
        sb.Draw(Blank, new Rectangle(9, 10, 14, 6), color);
        sb.Draw(Blank, new Rectangle(10, 16, 12, 2), color);
        // Visor slit
        sb.Draw(Blank, new Rectangle(11, 12, 10, 2), new Color(20, 20, 20));
    }

    private void DrawPotion(SpriteBatch sb, ItemData.Def def)
    {
        // Bottle shape
        var bottleColor = new Color(180, 180, 190, 180);
        // Neck
        sb.Draw(Blank, new Rectangle(14, 4, 4, 4), bottleColor);
        // Cork
        sb.Draw(Blank, new Rectangle(14, 2, 4, 2), new Color(120, 90, 50));
        // Body
        sb.Draw(Blank, new Rectangle(10, 8, 12, 16), bottleColor);
        // Liquid
        var liquid = def.Color;
        sb.Draw(Blank, new Rectangle(11, 14, 10, 9), liquid);
        // Shine
        sb.Draw(Blank, new Rectangle(12, 10, 2, 10),
            new Color(255, 255, 255, 80));
    }

    private void DrawFood(SpriteBatch sb, ItemData.Def def)
    {
        var color = def.Color;
        if (def.Key == "mushroom")
        {
            // Mushroom cap
            sb.Draw(Blank, new Rectangle(10, 8, 12, 6), color);
            sb.Draw(Blank, new Rectangle(8, 10, 16, 4), color);
            // Stem
            sb.Draw(Blank, new Rectangle(14, 14, 4, 10), new Color(200, 190, 170));
        }
        else if (def.Key == "berry")
        {
            // Berries — 3 small circles
            for (int i = 0; i < 3; i++)
            {
                int bx = 8 + i * 8, by = 12 + (i % 2) * 4;
                DrawFilledCircle(sb, new Vector2(bx + 4, by + 4), 4, color);
            }
        }
        else
        {
            // Bread — oval
            sb.Draw(Blank, new Rectangle(8, 12, 16, 8), color);
            sb.Draw(Blank, new Rectangle(10, 10, 12, 4), color);
        }
    }

    private void DrawResource(SpriteBatch sb, ItemData.Def def)
    {
        var color = def.Color;
        if (def.Key == "wood")
        {
            // Log — horizontal rectangle with rings
            sb.Draw(Blank, new Rectangle(6, 12, 20, 8), color);
            sb.Draw(Blank, new Rectangle(6, 12, 20, 1), new Color(60, 40, 20));
            // Rings
            sb.Draw(Blank, new Rectangle(15, 14, 2, 4), new Color(90, 65, 35));
        }
        else
        {
            // Stone/ore — irregular blob
            DrawFilledCircle(sb, new Vector2(16, 16), 10, color);
            // Highlights
            var light = new Color(
                Math.Min(255, color.R + 25),
                Math.Min(255, color.G + 25),
                Math.Min(255, color.B + 25));
            DrawFilledCircle(sb, new Vector2(13, 13), 4, light);
            // Ore specks
            if (def.Key == "ore" || def.Key == "gold_ore")
            {
                var specColor = def.Key == "gold_ore"
                    ? new Color(240, 200, 80) : new Color(140, 100, 60);
                sb.Draw(Blank, new Rectangle(14, 14, 2, 2), specColor);
                sb.Draw(Blank, new Rectangle(18, 16, 2, 2), specColor);
            }
        }
    }

    private void DrawBook(SpriteBatch sb, ItemData.Def def)
    {
        var color = def.Color;
        // Book cover
        sb.Draw(Blank, new Rectangle(8, 6, 16, 20), color);
        // Spine
        sb.Draw(Blank, new Rectangle(8, 6, 3, 20), new Color(60, 50, 40));
        // Pages
        sb.Draw(Blank, new Rectangle(11, 8, 13, 16), new Color(220, 210, 190));
        // Symbol — stat letter
        char sym = def.Stat?[..1].ToUpper()[0] ?? '?';
        // Draw symbol as small shape
        sb.Draw(Blank, new Rectangle(15, 14, 3, 3),
            new Color(60, 40, 20));
    }

    private void DrawKey(SpriteBatch sb, ItemData.Def def)
    {
        var color = def.Color;
        // Key head — circle
        DrawFilledCircle(sb, new Vector2(10, 10), 5, color);
        // Shaft
        sb.Draw(Blank, new Rectangle(14, 9, 12, 2), color);
        // Teeth
        sb.Draw(Blank, new Rectangle(22, 11, 2, 3), color);
        sb.Draw(Blank, new Rectangle(25, 11, 2, 2), color);
    }

    private void DrawUnknown(SpriteBatch sb, ItemData.Def def)
    {
        // Question mark in a circle
        DrawFilledCircle(sb, new Vector2(16, 16), 10, def.Color);
        sb.Draw(Blank, new Rectangle(15, 10, 2, 8), Color.White);
        sb.Draw(Blank, new Rectangle(15, 20, 2, 2), Color.White);
    }

    private void DrawFilledCircle(SpriteBatch sb, Vector2 center, int radius, Color color)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            int w = (int)Math.Sqrt(radius * radius - dy * dy);
            int x = (int)center.X - w;
            int y = (int)center.Y + dy;
            sb.Draw(Blank, new Rectangle(x, y, w * 2, 1), color);
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

    private Texture2D GetBlank()
    {
        return Blank;
    }
}
