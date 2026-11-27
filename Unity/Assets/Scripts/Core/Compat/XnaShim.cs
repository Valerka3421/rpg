// Совместимый shim MonoGame для Unity-сборки ядра.
// Чистое ядро (Unity/Assets/Scripts/Core, кроме этого файла) было написано
// под MonoGame и ссылается на Microsoft.Xna.Framework ТОЛЬКО ради struct
// Color. В MonoGame-сборке (Aethelgard.csproj) используется настоящий
// MonoGame.Framework; здесь же тот же тип предоставляет этот shim, поэтому
// исходники ядра не редактируются при порте и остаются одним источником
// правды для обоих движков.
// Неявное приведение к UnityEngine.Color позволяет Unity-рендеру передавать
// цвета ядра в SpriteRenderer/uGUI напрямую.
using UnityEngine;

namespace Microsoft.Xna.Framework
{
    /// <summary>Заменитель MonoGame Color для Unity-сборки ядра.</summary>
    public struct Color
    {
        public float R;
        public float G;
        public float B;
        public float A;

        public Color(int r, int g, int b, int a = 255)
        {
            R = r / 255f;
            G = g / 255f;
            B = b / 255f;
            A = a / 255f;
        }

        public Color(float r, float g, float b, float a = 1f)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public static readonly Color White = new(255, 255, 255);
        public static readonly Color Transparent = new(0f, 0f, 0f, 0f);

        public static implicit operator UnityEngine.Color(Color c) => new(c.R, c.G, c.B, c.A);
        public static implicit operator Color(UnityEngine.Color c) => new(c.r, c.g, c.b, c.a);
    }

    /// <summary>Минимальный Rectangle (используется PixelCanvas/TileArt).</summary>
    public struct Rectangle
    {
        public int X, Y, Width, Height;
        public Rectangle(int x, int y, int width, int height)
        {
            X = x; Y = y; Width = width; Height = height;
        }

        public bool Contains(int x, int y) =>
            x >= X && x < X + Width && y >= Y && y < Y + Height;
    }

    /// <summary>Минимальный Vector2 (FillCircle в PixelCanvas).</summary>
    public struct Vector2
    {
        public float X, Y;
        public Vector2(float x, float y) { X = x; Y = y; }
    }
}
