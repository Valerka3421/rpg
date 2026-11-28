using System;
    using System.Collections.Generic;
    using System.IO;
    using UnityEngine;
    using XnaColor = Microsoft.Xna.Framework.Color;

    namespace Aethelgard.Render
    {

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Aethelgard.BaseClass;
    using Aethelgard.Entities;
    using Aethelgard.Entities.Monsters;
    using Aethelgard.Structs;
    using Aethelgard.World;
    using Aethelgard.Systems;
    using Aethelgard.Data;
    using Aethelgard.Net;
    using Aethelgard.Audio;
    /// <summary>
    /// Хранилище спрайтов существ и NPC. PNG из Assets/Aethelgard/Sprites/
    /// (если добавлены), иначе процедурная генерация 64×64 в PixelCanvas:
    /// гуманоидные монстры/NPC/игроки — фигурка с головой и конечностями,
    /// четвероногие — корпус, голова, лапы. Цвет — базовый цвет сущности.
    /// </summary>
    public static class SpriteStore
    {
        private static readonly Dictionary<string, Texture2D> Cache = new();

        private static readonly HashSet<string> Humanoid =
            new(StringComparer.Ordinal)
        {
            "goblin", "skeleton", "lich", "vampire", "vampire_lord", "orc",
            "shaman", "wraith", "demon", "goblin_king", "archer", "murloc",
            "troll", "golem", "dragonling",
        };

        /// <summary>Спрайт по категории (player/npc/monsters/bosses) и ключу.</summary>
        public static Sprite Get(string key, XnaColor baseColor, bool boss = false)
        {
            string cacheKey = $"{key}_{baseColor.R}_{baseColor.G}_{baseColor.B}_{(boss ? 1 : 0)}";
            if (Cache.TryGetValue(cacheKey, out var tex)) return ToSprite(tex);

            tex = LoadPng(key) ?? Generate(key, baseColor, boss);
            Cache[cacheKey] = tex;
            return ToSprite(tex);
        }

        private static Sprite ToSprite(Texture2D tex)
        {
            var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 1f); // 64px = 64 мировых единиц (1 тайл)
            sp.name = tex.name;
            return sp;
        }

        private static Texture2D? LoadPng(string key)
        {
            // PNG-ассеты: Assets/Aethelgard/Sprites/{key}.png (по желанию),
            // например Resources path — используем прямую загрузку из StreamingAssets.
            try
            {
                string path = Path.Combine(Application.streamingAssetsPath,
                    "Sprites", key + ".png");
                if (!File.Exists(path)) return null;
                var bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                tex.LoadImage(bytes);
                return tex;
            }
            catch { return null; }
        }

        private static Texture2D Generate(string key, XnaColor baseColor, bool boss)
        {
            var canvas = new PixelCanvas(64, 64);
            var main = baseColor.U();
            var dark = baseColor.Mul(0.65f);
            var light = baseColor.Mul(1.3f);
            var skin = new Color32(230, 200, 170, 255);
            var eye = new Color32(20, 18, 16, 255);
            var outline = new Color32(20, 16, 12, 255);

            bool humanoid = Humanoid.Contains(key) || key == "player";
            // Определяем по типу сущности: игрок и NPC всегда гуманоиды
            if (key == "player") humanoid = true;

            if (humanoid)
            {
                int cx = 32, scale = boss ? 12 : 10;
                // Тени/обводка: тело
                canvas.FillRect(cx - scale, 26, scale * 2, 20, main);          // торс
                canvas.FillRect(cx - scale, 26, scale * 2, 2, light);          // блик
                canvas.FillRect(cx - scale - 3, 28, 3, 12, dark);              // левая рука
                canvas.FillRect(cx + scale, 28, 3, 12, dark);                  // правая рука
                canvas.FillRect(cx - scale + 2, 46, 6, 12, dark);              // ноги
                canvas.FillRect(cx + scale - 8, 46, 6, 12, dark);
                // Голова
                canvas.FillRect(cx - 7, 12, 14, 14, skin);
                canvas.FillRect(cx - 7, 12, 14, 3, main);                      // шлем/волосы
                canvas.FillRect(cx - 4, 19, 3, 2, eye);                        // глаза
                canvas.FillRect(cx + 1, 19, 3, 2, eye);
                // Оружие у гуманоидов с оружием
                canvas.FillRect(cx + scale + 3, 20, 2, 16, new Color32(150, 130, 100, 255));
                // Обводка силуэта
                canvas.FillRect(cx - scale - 4, 26, 1, 20, outline);
                canvas.FillRect(cx + scale + 4, 26, 1, 20, outline);
            }
            else
            {
                // Четвероногое (волк, медведь, кабан, олень, крыса...)
                int cy = 40, bh = boss ? 18 : 14;
                canvas.FillRect(18, cy - bh, 28, bh, main);                    // корпус
                canvas.FillRect(18, cy - bh, 28, 2, light);
                canvas.FillRect(10, cy - bh - 6, 12, 10, dark);                // голова
                canvas.FillRect(12, cy - bh - 3, 3, 2, eye);
                canvas.FillRect(20, cy, 4, 8, dark);                           // лапы
                canvas.FillRect(40, cy, 4, 8, dark);
                canvas.FillRect(18, cy + 1, 28, 3, dark);                      // хвост-тень
            }

            // python-final: глаза боссов светятся золотом
            if (boss)
            {
                canvas.SetPixel(13, 45, new Color32(240, 210, 90, 255));
                canvas.SetPixel(14, 45, new Color32(240, 210, 90, 255));
            }

            StoneshardGrade(canvas); // единая градация спрайтов python-final
            return canvas.ToTexture();
        }

        /// <summary>
        /// Градация спрайтов textures.finish() (1:1): десатурация 0.35,
        /// умножение (244,232,210), вертикальный градиент
        /// (255,250,236)→(126,134,152), контур INK (11,8,6).
        /// </summary>
        public static void StoneshardGrade(PixelCanvas canvas)
        {
            var ink = new Color32(11, 8, 6, 255);
            int h = canvas.Height;
            for (int i = 0; i < canvas.Pixels.Length; i++)
            {
                var c = canvas.Pixels[i];
                if (c.a == 0) continue;
                // десатурация 0.35
                float lum = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
                int r = (int)(c.r * 0.65f + lum * 0.35f);
                int g = (int)(c.g * 0.65f + lum * 0.35f);
                int b = (int)(c.b * 0.65f + lum * 0.35f);
                // умножение (244,232,210)
                r = r * 244 / 255; g = g * 232 / 255; b = b * 210 / 255;
                // вертикальный градиент (низ темнее/холоднее)
                int y = i / canvas.Width;
                float k = 1f - y / (float)h; // верх = 1
                int tr = (int)(255 + (126 - 255) * (1 - k));
                int tg = (int)(250 + (134 - 250) * (1 - k));
                int tb = (int)(236 + (152 - 236) * (1 - k));
                r = r * tr / 255; g = g * tg / 255; b = b * tb / 255;
                canvas.Pixels[i] = new Color32(
                    (byte)Mathf.Clamp(r, 0, 255), (byte)Mathf.Clamp(g, 0, 255),
                    (byte)Mathf.Clamp(b, 0, 255), c.a);
            }
            // Тёмно-коричневый контур силуэта (INK): непрозрачный пиксель рядом
            // с прозрачным → красим прозрачного
            var copy = (Color32[])canvas.Pixels.Clone();
            int w = canvas.Width;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (copy[y * w + x].a != 0) continue;
                    bool edge =
                        (x > 0 && copy[y * w + x - 1].a != 0) ||
                        (x < w - 1 && copy[y * w + x + 1].a != 0) ||
                        (y > 0 && copy[(y - 1) * w + x].a != 0) ||
                        (y < h - 1 && copy[(y + 1) * w + x].a != 0);
                    if (edge) canvas.Pixels[y * w + x] = ink;
                }
        }
    }
}
