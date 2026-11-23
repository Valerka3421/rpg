using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Aethelgard.Render;

/// <summary>
/// Procedural item icon renderer — draws 32×32 icons for inventory.
/// Ported from Python render/icons/_items.py (ItemMixin).
///
/// Иконки генерируются в памяти (PixelCanvas) и кэшируются. Раньше они
/// рисовались через RenderTarget2D прямо внутри активного SpriteBatch,
/// что сбрасывало цель рендера и могло ломать кадр инвентаря/магазина.
/// </summary>
public class ItemRenderer
{
    private readonly GraphicsDevice _device;
    private readonly Dictionary<string, Texture2D> _cache = new();
    private Texture2D? _blank;

    public const int IconSize = 32;

    public ItemRenderer(GraphicsDevice device)
    {
        _device = device;
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

        var canvas = new PixelCanvas(IconSize, IconSize);
        canvas.Clear(Color.Transparent);

        switch (def.Kind)
        {
            case ItemData.ItemType.Weapon:
                DrawWeapon(canvas, def);
                break;
            case ItemData.ItemType.Armor:
                DrawArmor(canvas, def);
                break;
            case ItemData.ItemType.Helmet:
                DrawHelmet(canvas, def);
                break;
            case ItemData.ItemType.Heal:
                DrawPotion(canvas, def);
                break;
            case ItemData.ItemType.Food:
                DrawFood(canvas, def);
                break;
            case ItemData.ItemType.Resource:
                DrawResource(canvas, def);
                break;
            case ItemData.ItemType.Book:
                DrawBook(canvas, def);
                break;
            case ItemData.ItemType.Key:
                DrawKey(canvas, def);
                break;
            default:
                DrawUnknown(canvas, def);
                break;
        }

        tex = canvas.ToTexture(_device);
        _cache[itemKey] = tex;
        return tex;
    }

    private static void DrawWeapon(PixelCanvas canvas, ItemData.Def def)
    {
        var color = def.Color;
        canvas.FillRect(14, 24, 4, 6, new Color(80, 60, 40));
        canvas.FillRect(10, 22, 12, 2, new Color(160, 140, 80));
        int bladeLen = def.Key == "warhammer" ? 18 : def.Key == "axe" ? 16 : 20;
        canvas.FillRect(15, 22 - bladeLen, 2, bladeLen, color);
        canvas.FillRect(15, 22 - bladeLen - 2, 2, 2, color);

        if (def.Key == "axe")
            canvas.FillRect(11, 8, 10, 8, color);
        else if (def.Key == "warhammer")
            canvas.FillRect(10, 6, 12, 6, color);
    }

    private static void DrawArmor(PixelCanvas canvas, ItemData.Def def)
    {
        var color = def.Color;
        canvas.FillRect(10, 8, 12, 4, color);
        canvas.FillRect(11, 12, 10, 12, color);
        canvas.FillRect(12, 24, 8, 4, color);

        var light = new Color(
            Math.Min(255, color.R + 30),
            Math.Min(255, color.G + 30),
            Math.Min(255, color.B + 30));
        canvas.FillRect(12, 10, 3, 14, light);
    }

    private static void DrawHelmet(PixelCanvas canvas, ItemData.Def def)
    {
        var color = def.Color;
        canvas.FillRect(10, 8, 12, 2, color);
        canvas.FillRect(9, 10, 14, 6, color);
        canvas.FillRect(10, 16, 12, 2, color);
        canvas.FillRect(11, 12, 10, 2, new Color(20, 20, 20));
    }

    private static void DrawPotion(PixelCanvas canvas, ItemData.Def def)
    {
        var bottleColor = new Color(180, 180, 190, 180);
        canvas.FillRect(14, 4, 4, 4, bottleColor);
        canvas.FillRect(14, 2, 4, 2, new Color(120, 90, 50));
        canvas.FillRect(10, 8, 12, 16, bottleColor);
        canvas.FillRect(11, 14, 10, 9, def.Color);
        canvas.FillRect(12, 10, 2, 10, new Color(255, 255, 255, 80));
    }

    private static void DrawFood(PixelCanvas canvas, ItemData.Def def)
    {
        var color = def.Color;
        if (def.Key == "mushroom")
        {
            canvas.FillRect(10, 8, 12, 6, color);
            canvas.FillRect(8, 10, 16, 4, color);
            canvas.FillRect(14, 14, 4, 10, new Color(200, 190, 170));
        }
        else if (def.Key == "berry")
        {
            for (int i = 0; i < 3; i++)
            {
                int bx = 8 + i * 8, by = 12 + (i % 2) * 4;
                canvas.FillCircle(new Vector2(bx + 4, by + 4), 4, color);
            }
        }
        else
        {
            canvas.FillRect(8, 12, 16, 8, color);
            canvas.FillRect(10, 10, 12, 4, color);
        }
    }

    private static void DrawResource(PixelCanvas canvas, ItemData.Def def)
    {
        var color = def.Color;
        if (def.Key == "wood")
        {
            canvas.FillRect(6, 12, 20, 8, color);
            canvas.FillRect(6, 12, 20, 1, new Color(60, 40, 20));
            canvas.FillRect(15, 14, 2, 4, new Color(90, 65, 35));
        }
        else
        {
            canvas.FillCircle(new Vector2(16, 16), 10, color);
            var light = new Color(
                Math.Min(255, color.R + 25),
                Math.Min(255, color.G + 25),
                Math.Min(255, color.B + 25));
            canvas.FillCircle(new Vector2(13, 13), 4, light);

            if (def.Key == "ore" || def.Key == "gold_ore")
            {
                var specColor = def.Key == "gold_ore"
                    ? new Color(240, 200, 80) : new Color(140, 100, 60);
                canvas.FillRect(14, 14, 2, 2, specColor);
                canvas.FillRect(18, 16, 2, 2, specColor);
            }
        }
    }

    private static void DrawBook(PixelCanvas canvas, ItemData.Def def)
    {
        var color = def.Color;
        canvas.FillRect(8, 6, 16, 20, color);
        canvas.FillRect(8, 6, 3, 20, new Color(60, 50, 40));
        canvas.FillRect(11, 8, 13, 16, new Color(220, 210, 190));
        canvas.FillRect(15, 14, 3, 3, new Color(60, 40, 20));
    }

    private static void DrawKey(PixelCanvas canvas, ItemData.Def def)
    {
        var color = def.Color;
        canvas.FillCircle(new Vector2(10, 10), 5, color);
        canvas.FillRect(14, 9, 12, 2, color);
        canvas.FillRect(22, 11, 2, 3, color);
        canvas.FillRect(25, 11, 2, 2, color);
    }

    private static void DrawUnknown(PixelCanvas canvas, ItemData.Def def)
    {
        canvas.FillCircle(new Vector2(16, 16), 10, def.Color);
        canvas.FillRect(15, 10, 2, 8, Color.White);
        canvas.FillRect(15, 20, 2, 2, Color.White);
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

    private Texture2D GetBlank() => Blank;
}
