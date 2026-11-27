using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Aethelgard.Render;

/// <summary>
/// Спрайты сущностей: сначала пробуем файл assets/sprites/{category}/{key}.png
/// (как описано в assets/sprites/structure.txt), если его нет — генерируем
/// базовую процедурную текстуру 64×64 и кэшируем. Так игра выглядит готовой
/// даже без рисованных ассетов, а любой PNG в папке автоматически подхватится.
/// </summary>
public static class SpriteStore
{
    private static readonly Dictionary<string, Texture2D> Cache = new();
    private static GraphicsDevice? _hookedDevice;

    // DeviceReset (fullscreen/разрешение/потеря контекста) инвалидирует
    // текстуры: раньше кэши SpriteStore и Ui никогда не очищались и после
    // сброса рисовались «инвалидные» спрайты — родич бага «невидимый UI».
    private static void HookDevice(GraphicsDevice device)
    {
        if (_hookedDevice != null) return;
        _hookedDevice = device;
        device.DeviceResetting += (_, _) =>
        {
            foreach (var tex in Cache.Values)
            {
                try { tex.Dispose(); } catch { }
            }
            Cache.Clear();
            Ui.ClearCache();
        };
    }

    /// <summary>Категория: "player", "npc", "monsters", "bosses". Ключ — класс/тип.</summary>
    public static Texture2D Get(GraphicsDevice device, string category, string key, Color tint)
    {
        HookDevice(device);
        key = Sanitize(key);
        string cacheKey = $"{category}/{key}";
        if (Cache.TryGetValue(cacheKey, out var tex))
            return tex;

        // 1. Пытаемся загрузить PNG из папки ассетов
        string path = Path.Combine("assets", "sprites", category, key + ".png");
        if (File.Exists(path))
        {
            try
            {
                tex = Texture2D.FromFile(device, path);
                Cache[cacheKey] = tex;
                return tex;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SpriteStore] Не удалось загрузить {path}: {ex.Message}");
            }
        }

        // 2. Процедурный фолбэк
        tex = Generate(device, category, key, tint);
        Cache[cacheKey] = tex;
        return tex;
    }

    private static string Sanitize(string key)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            key = key.Replace(c, '_');
        return key;
    }

    private enum Shape { Humanoid, Beast, Brute, Undead, Floater, Colossus }

    private static Shape ShapeOf(string category, string key)
    {
        if (category == "player" || category == "npc") return Shape.Humanoid;
        return key switch
        {
            "rat" => Shape.Beast,
            "goblin" or "archer" or "orc" or "shaman" => Shape.Brute,
            "skeleton" or "wraith" or "vampire" => Shape.Undead,
            "dragonling" or "demon" => Shape.Floater,
            "troll" or "golem" or "dragon" or "golem_king" => Shape.Colossus,
            "lich" or "vampire_lord" => Shape.Undead,
            "goblin_king" => Shape.Brute,
            _ => Shape.Humanoid,
        };
    }

    private static Color Shift(Color c, int dr, int dg, int db)
    {
        static int Cl(int v, int d) => Math.Clamp(v + d, 0, 255);
        return new Color(Cl(c.R, dr), Cl(c.G, dg), Cl(c.B, db));
    }

    private static Texture2D Generate(GraphicsDevice device, string category, string key, Color tint)
    {
        var canvas = new PixelCanvas(64, 64);
        canvas.Clear(Color.Transparent);

        Color main = tint;
        Color dark = Shift(tint, -45, -45, -45);
        Color light = Shift(tint, 35, 35, 30);
        Color skin = category == "monsters" || category == "bosses"
            ? main : new Color(224, 186, 150);
        Color metal = new Color(150, 148, 160);

        switch (ShapeOf(category, key))
        {
            case Shape.Humanoid:
            {
                // Ноги
                canvas.FillRect(24, 52, 6, 10, dark);
                canvas.FillRect(34, 52, 6, 10, dark);
                // Тело / одежда
                canvas.FillRect(20, 30, 24, 24, main);
                canvas.FillRect(20, 30, 24, 4, light);
                // Руки
                canvas.FillRect(14, 32, 6, 16, dark);
                canvas.FillRect(44, 32, 6, 16, dark);
                // Голова
                canvas.FillRect(22, 12, 20, 18, skin);
                canvas.FillRect(22, 10, 20, 4, dark);       // волосы / шлем
                canvas.SetPixel(27, 22, new Color(30, 20, 15));
                canvas.SetPixel(28, 22, new Color(30, 20, 15));
                canvas.SetPixel(36, 22, new Color(30, 20, 15));
                canvas.SetPixel(37, 22, new Color(30, 20, 15));
                // Оружие у игрока и NPC
                if (category == "player" || category == "npc")
                {
                    canvas.FillRect(52, 22, 3, 26, metal);  // древко/клинок
                    canvas.FillRect(50, 20, 7, 3, new Color(110, 85, 50));
                }
                break;
            }
            case Shape.Beast:
            {
                // Тело четвероногое
                canvas.FillRect(14, 34, 34, 16, main);
                canvas.FillRect(14, 34, 34, 3, light);
                // Голова
                canvas.FillRect(42, 28, 14, 12, main);
                canvas.FillRect(52, 32, 6, 4, dark);        // морда
                // Уши
                canvas.FillRect(44, 24, 4, 5, dark);
                canvas.FillRect(50, 24, 4, 5, dark);
                // Хвост
                canvas.FillRect(8, 38, 7, 3, dark);
                // Лапы
                canvas.FillRect(18, 50, 5, 9, dark);
                canvas.FillRect(38, 50, 5, 9, dark);
                // Глаз
                canvas.SetPixel(48, 32, new Color(20, 10, 5));
                canvas.SetPixel(49, 32, new Color(20, 10, 5));
                break;
            }
            case Shape.Brute:
            {
                // Приземистое крепкое тело
                canvas.FillRect(16, 28, 32, 26, main);
                canvas.FillRect(16, 28, 32, 5, light);
                canvas.FillRect(12, 32, 6, 18, dark);       // руки
                canvas.FillRect(46, 32, 6, 18, dark);
                canvas.FillRect(20, 54, 9, 8, dark);        // ноги
                canvas.FillRect(35, 54, 9, 8, dark);
                canvas.FillRect(24, 12, 18, 16, skin);      // голова
                canvas.FillRect(24, 10, 18, 3, dark);
                canvas.SetPixel(29, 20, new Color(20, 10, 5));
                canvas.SetPixel(30, 20, new Color(20, 10, 5));
                canvas.SetPixel(35, 20, new Color(20, 10, 5));
                canvas.SetPixel(36, 20, new Color(20, 10, 5));
                // Дубина
                canvas.FillRect(54, 24, 4, 26, new Color(110, 80, 45));
                canvas.FillRect(52, 18, 8, 8, Shift(main, -20, -10, 20));
                break;
            }
            case Shape.Undead:
            {
                // Худое тело с рёбрами
                canvas.FillRect(22, 28, 20, 24, main);
                canvas.FillRect(24, 32, 16, 2, dark);
                canvas.FillRect(24, 38, 16, 2, dark);
                canvas.FillRect(24, 44, 16, 2, dark);
                canvas.FillRect(16, 30, 5, 20, dark);       // руки
                canvas.FillRect(43, 30, 5, 20, dark);
                canvas.FillRect(24, 52, 6, 10, dark);
                canvas.FillRect(34, 52, 6, 10, dark);
                canvas.FillRect(24, 12, 16, 16, light);     // череп
                canvas.FillRect(28, 18, 3, 4, new Color(20, 10, 10));
                canvas.FillRect(34, 18, 3, 4, new Color(20, 10, 10));
                canvas.FillRect(28, 26, 9, 2, dark);        // зубы
                break;
            }
            case Shape.Floater:
            {
                // Летающее существо с крыльями
                canvas.FillRect(24, 24, 16, 20, main);      // тело
                canvas.FillRect(24, 24, 16, 4, light);
                // Крылья
                for (int i = 0; i < 10; i++)
                {
                    canvas.FillRect(10 - i / 2, 24 + i, 14, 2, dark);
                    canvas.FillRect(40 + i / 2, 24 + i, 14, 2, dark);
                }
                canvas.FillRect(26, 14, 12, 10, skin);      // голова
                canvas.FillRect(24, 12, 4, 4, dark);        // рога
                canvas.FillRect(36, 12, 4, 4, dark);
                canvas.SetPixel(30, 19, new Color(220, 190, 60));
                canvas.SetPixel(31, 19, new Color(220, 190, 60));
                canvas.SetPixel(33, 19, new Color(220, 190, 60));
                canvas.SetPixel(34, 19, new Color(220, 190, 60));
                // Хвост
                canvas.FillRect(28, 44, 8, 10, dark);
                break;
            }
            case Shape.Colossus:
            {
                // Огромное каменное тело
                canvas.FillRect(14, 24, 36, 30, main);
                canvas.FillRect(14, 24, 36, 6, light);
                canvas.FillRect(8, 28, 8, 24, dark);        // руки
                canvas.FillRect(48, 28, 8, 24, dark);
                canvas.FillRect(18, 54, 11, 8, dark);
                canvas.FillRect(35, 54, 11, 8, dark);
                canvas.FillRect(24, 10, 16, 14, skin);
                canvas.SetPixel(28, 16, new Color(210, 180, 60));
                canvas.SetPixel(29, 16, new Color(210, 180, 60));
                canvas.SetPixel(34, 16, new Color(210, 180, 60));
                canvas.SetPixel(35, 16, new Color(210, 180, 60));
                // Трещины (для голема/дракона — фактура)
                canvas.SetPixel(22, 34, dark);
                canvas.SetPixel(23, 35, dark);
                canvas.SetPixel(40, 40, dark);
                canvas.SetPixel(41, 41, dark);
                canvas.SetPixel(30, 46, dark);
                canvas.SetPixel(31, 47, dark);
                break;
            }
        }

        return canvas.ToTexture(device);
    }
}
