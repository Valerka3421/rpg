using System.Collections.Generic;

// Shim ввода MonoGame для Unity-сборки. GameController (дословно
// перенесённый в Unity-слой) принимает KeyboardState/MouseState как
// параметры HandleInput — эти типы просто данные. Кто и как их заполняет —
// дело движкового слоя: MonoGame кладёт настоящий KeyboardState, Unity
// собирает KeyboardState из UnityEngine.Input в GameApp и подставляет
// Provider для Mouse.GetState().
namespace Microsoft.Xna.Framework.Input
{
    /// <summary>Клавиши, используемые игрой (надмножество MonoGame-имён).</summary>
    public enum Keys
    {
        None = 0,
        Back, Tab, Enter, Escape, Space,
        D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
        A, B, C, D, E, F, G, H, I, J, K, L, M,
        N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
        LeftShift, RightShift, LeftControl, RightControl, LeftAlt, RightAlt,
        Up, Down, Left, Right,
        Home, End, PageUp, PageDown, Insert, Delete,
        F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
        OemTilde, OemMinus, OemPlus, OemPeriod, OemComma,
        OemOpenBrackets, OemCloseBrackets, OemSemicolon, OemQuotes, OemQuestion,
        NumPad0, NumPad1, NumPad2, NumPad3, NumPad4, NumPad5, NumPad6, NumPad7, NumPad8, NumPad9,
        Decimal, Add, Subtract, Multiply, Divide,
    }

    /// <summary>Набор нажатых клавиш за кадр (замена MonoGame KeyboardState).</summary>
    public struct KeyboardState
    {
        private HashSet<Keys>? _down;

        public KeyboardState(IEnumerable<Keys>? pressed)
        {
            _down = pressed == null ? null : new HashSet<Keys>(pressed);
        }

        public bool IsKeyDown(Keys key) => _down != null && _down.Contains(key);

        public bool IsKeyUp(Keys key) => !IsKeyDown(key);

        public Keys[] GetPressedKeys()
        {
            if (_down == null || _down.Count == 0) return System.Array.Empty<Keys>();
            var arr = new Keys[_down.Count];
            _down.CopyTo(arr);
            System.Array.Sort(arr);
            return arr;
        }
    }

    public enum ButtonState { Released = 0, Pressed = 1 }

    /// <summary>Данные состояния мыши (замена MonoGame MouseState).</summary>
    public struct MouseState
    {
        public int X, Y;
        public int ScrollWheelValue;
        public ButtonState LeftButton, MiddleButton, RightButton, XButton1, XButton2;

        public MouseState(int x, int y, int scroll,
            ButtonState left, ButtonState middle, ButtonState right,
            ButtonState x1, ButtonState x2)
        {
            X = x; Y = y; ScrollWheelValue = scroll;
            LeftButton = left; MiddleButton = middle; RightButton = right;
            XButton1 = x1; XButton2 = x2;
        }
    }

    /// <summary>Точка подключения движка: GameApp присваивает Provider,
    /// MonoGame-сборка свою версию не использует (там настоящий Mouse).</summary>
    public static class Mouse
    {
        public static System.Func<MouseState>? Provider;

        public static MouseState GetState()
            => Provider != null ? Provider() : default;
    }
}
