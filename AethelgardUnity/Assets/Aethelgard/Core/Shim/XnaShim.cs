// Шим совместимости: в Core-логике (перенесённой из MonoGame) повсеместно
    // используется XNA Color. Даём собственную реализацию, чтобы ядро собиралось
    // без MonoGame-зависимостей. Game-слой конвертирует в UnityEngine.Color.
    namespace Microsoft.Xna.Framework
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
    public struct Color
    {
        public byte R, G, B, A;

        public Color(int r, int g, int b, int a = 255)
        {
            R = (byte)r; G = (byte)g; B = (byte)b; A = (byte)a;
        }

        public Color(byte r, byte g, byte b, byte a = 255)
        {
            R = r; G = g; B = b; A = a;
        }

        public Color(float r, float g, float b, float a = 1f)
        {
            R = (byte)(r * 255f); G = (byte)(g * 255f);
            B = (byte)(b * 255f); A = (byte)(a * 255f);
        }

        public static readonly Color White = new(255, 255, 255);
        public static readonly Color Black = new(0, 0, 0);

        public static Color operator *(Color c, float f) =>
            new((int)(c.R * f), (int)(c.G * f), (int)(c.B * f), c.A);

        public override string ToString() => $"{R},{G},{B},{A}";
    }
}
