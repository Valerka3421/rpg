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
    /// <summary>Заменитель MonoGame Color для Unity-сборки ядра.
    /// Семантика как в MonoGame: каналы хранятся в диапазоне 0..255
    /// (int/byte-конструктор — 0..255, float-конструктор — 0..1, как в MG).</summary>
    public struct Color
    {
        public float R;
        public float G;
        public float B;
        public float A;

        public Color(int r, int g, int b, int a = 255)
        {
            R = r; G = g; B = b; A = a;
        }

        public Color(float r, float g, float b, float a = 1f)
        {
            R = r * 255f; G = g * 255f; B = b * 255f; A = a * 255f;
        }

        public static readonly Color White = new(255, 255, 255);
        public static readonly Color Transparent = new(0, 0, 0, 0);

        public static implicit operator UnityEngine.Color(Color c) =>
            new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
        public static implicit operator Color(UnityEngine.Color c) =>
            new(c.r * 255f, c.g * 255f, c.b * 255f, c.a * 255f);
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
