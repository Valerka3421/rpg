// Шим ввода: GameController и экраны работают с KeyboardState/MouseState
    // (перенесено из MonoGame без изменений семантики). Game-слой заполняет
    // состояние из UnityEngine.Input и передаёт дальше.
    using System.Collections.Generic;

    namespace Microsoft.Xna.Framework.Input
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
    public enum Keys
    {
        None = 0,
        Up, Down, Left, Right,
        Enter, Escape, Space, Tab, Backspace, Back, Delete,
        A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
        D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
        F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
        OemMinus, OemPlus, OemPeriod, OemComma, OemQuestion, OemSemicolon,
        OemOpenBrackets, OemCloseBrackets, OemBackslash, OemTilde,
        NumPad0, NumPad1, NumPad2, NumPad3, NumPad4, NumPad5, NumPad6, NumPad7, NumPad8, NumPad9,
        LeftShift, RightShift, LeftControl, RightControl, LeftAlt, RightAlt,
    }

    public struct KeyboardState
    {
        // null = ни одна клавиша не нажата (C#9: без field-initializer у struct)
        private readonly HashSet<Keys> _keys;

        public KeyboardState(IEnumerable<Keys> pressed) => _keys = new HashSet<Keys>(pressed);

        public bool IsKeyDown(Keys k) => _keys != null && _keys.Contains(k);
        public bool IsKeyUp(Keys k) => !IsKeyDown(k);
        public IEnumerable<Keys> GetPressedKeys() => _keys ?? (IEnumerable<Keys>)System.Array.Empty<Keys>();
    }

    public struct MouseState
    {
        public int X, Y;
        public bool LeftPressed, RightPressed;
    }
}
