using System.Collections.Generic;
using UnityEngine;
using XnaKeys = Microsoft.Xna.Framework.Input.Keys;

namespace Aethelgard.Runtime;

/// <summary>Текущее и предыдущее состояние клавиатуры (кадровый кэш).</summary>
public static class InputState
{
    public static Microsoft.Xna.Framework.Input.KeyboardState Keys;
    public static Microsoft.Xna.Framework.Input.KeyboardState PrevKeys;
}

/// <summary>
/// Мост ввода: UnityEngine.Input (legacy Input Manager) → шим
/// KeyboardState/MouseState, к которому уже приспособлен GameController
/// и экраны. Клавиши маппятся 1:1 с MonoGame/python-версией.
/// </summary>
public static class UnityInput
{
    private static readonly Dictionary<KeyCode, XnaKeys> Map = new()
    {
        [KeyCode.UpArrow] = XnaKeys.Up,
        [KeyCode.DownArrow] = XnaKeys.Down,
        [KeyCode.LeftArrow] = XnaKeys.Left,
        [KeyCode.RightArrow] = XnaKeys.Right,
        [KeyCode.Return] = XnaKeys.Enter,
        [KeyCode.KeypadEnter] = XnaKeys.Enter,
        [KeyCode.Escape] = XnaKeys.Escape,
        [KeyCode.Space] = XnaKeys.Space,
        [KeyCode.Tab] = XnaKeys.Tab,
        [KeyCode.Backspace] = XnaKeys.Backspace,
        [KeyCode.Delete] = XnaKeys.Delete,
        [KeyCode.A] = XnaKeys.A, [KeyCode.B] = XnaKeys.B, [KeyCode.C] = XnaKeys.C,
        [KeyCode.D] = XnaKeys.D, [KeyCode.E] = XnaKeys.E, [KeyCode.F] = XnaKeys.F,
        [KeyCode.G] = XnaKeys.G, [KeyCode.H] = XnaKeys.H, [KeyCode.I] = XnaKeys.I,
        [KeyCode.J] = XnaKeys.J, [KeyCode.K] = XnaKeys.K, [KeyCode.L] = XnaKeys.L,
        [KeyCode.M] = XnaKeys.M, [KeyCode.N] = XnaKeys.N, [KeyCode.O] = XnaKeys.O,
        [KeyCode.P] = XnaKeys.P, [KeyCode.Q] = XnaKeys.Q, [KeyCode.R] = XnaKeys.R,
        [KeyCode.S] = XnaKeys.S, [KeyCode.T] = XnaKeys.T, [KeyCode.U] = XnaKeys.U,
        [KeyCode.V] = XnaKeys.V, [KeyCode.W] = XnaKeys.W, [KeyCode.X] = XnaKeys.X,
        [KeyCode.Y] = XnaKeys.Y, [KeyCode.Z] = XnaKeys.Z,
        [KeyCode.Alpha0] = XnaKeys.D0, [KeyCode.Alpha1] = XnaKeys.D1,
        [KeyCode.Alpha2] = XnaKeys.D2, [KeyCode.Alpha3] = XnaKeys.D3,
        [KeyCode.Alpha4] = XnaKeys.D4, [KeyCode.Alpha5] = XnaKeys.D5,
        [KeyCode.Alpha6] = XnaKeys.D6, [KeyCode.Alpha7] = XnaKeys.D7,
        [KeyCode.Alpha8] = XnaKeys.D8, [KeyCode.Alpha9] = XnaKeys.D9,
        [KeyCode.F1] = XnaKeys.F1, [KeyCode.F2] = XnaKeys.F2,
        [KeyCode.F3] = XnaKeys.F3, [KeyCode.F4] = XnaKeys.F4,
        [KeyCode.F5] = XnaKeys.F5, [KeyCode.F6] = XnaKeys.F6,
        [KeyCode.F7] = XnaKeys.F7, [KeyCode.F8] = XnaKeys.F8,
        [KeyCode.F9] = XnaKeys.F9, [KeyCode.F10] = XnaKeys.F10,
        [KeyCode.F11] = XnaKeys.F11, [KeyCode.F12] = XnaKeys.F12,
        [KeyCode.Minus] = XnaKeys.OemMinus,
        [KeyCode.Equals] = XnaKeys.OemPlus,
        [KeyCode.Period] = XnaKeys.OemPeriod,
        [KeyCode.Comma] = XnaKeys.OemComma,
        [KeyCode.Slash] = XnaKeys.OemQuestion,
        [KeyCode.Semicolon] = XnaKeys.OemSemicolon,
        [KeyCode.LeftBracket] = XnaKeys.OemOpenBrackets,
        [KeyCode.RightBracket] = XnaKeys.OemCloseBrackets,
        [KeyCode.Backslash] = XnaKeys.OemBackslash,
        [KeyCode.BackQuote] = XnaKeys.OemTilde,
        [KeyCode.Keypad0] = XnaKeys.NumPad0, [KeyCode.Keypad1] = XnaKeys.NumPad1,
        [KeyCode.Keypad2] = XnaKeys.NumPad2, [KeyCode.Keypad3] = XnaKeys.NumPad3,
        [KeyCode.Keypad4] = XnaKeys.NumPad4, [KeyCode.Keypad5] = XnaKeys.NumPad5,
        [KeyCode.Keypad6] = XnaKeys.NumPad6, [KeyCode.Keypad7] = XnaKeys.NumPad7,
        [KeyCode.Keypad8] = XnaKeys.NumPad8, [KeyCode.Keypad9] = XnaKeys.NumPad9,
        [KeyCode.LeftShift] = XnaKeys.LeftShift,
        [KeyCode.RightShift] = XnaKeys.RightShift,
        [KeyCode.LeftControl] = XnaKeys.LeftControl,
        [KeyCode.RightControl] = XnaKeys.RightControl,
        [KeyCode.LeftAlt] = XnaKeys.LeftAlt,
        [KeyCode.RightAlt] = XnaKeys.RightAlt,
    };

    public static Microsoft.Xna.Framework.Input.KeyboardState ReadKeyboard()
    {
        var pressed = new List<XnaKeys>();
        foreach (var kv in Map)
            if (Input.GetKey(kv.Key)) pressed.Add(kv.Value);
        return new Microsoft.Xna.Framework.Input.KeyboardState(pressed);
    }

    /// <summary>Мышь в дизайн-координатах 1920×1080 (начало слева-сверху).</summary>
    public static Microsoft.Xna.Framework.Input.MouseState ReadMouse(float scaleFactor)
    {
        var mp = Input.mousePosition;
        var sh = Screen.height;
        float sf = scaleFactor > 0f ? scaleFactor : 1f;
        return new Microsoft.Xna.Framework.Input.MouseState
        {
            X = Mathf.RoundToInt(mp.x / sf),
            Y = Mathf.RoundToInt((sh - mp.y) / sf),
            LeftPressed = Input.GetMouseButton(0),
            RightPressed = Input.GetMouseButton(1),
        };
    }
}
