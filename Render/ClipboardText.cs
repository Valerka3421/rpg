using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Aethelgard.Render;

/// <summary>
/// Чтение системного буфера обмена через SDL2 (паритет
/// game/menu_controller.py:read_clipboard_text). MonoGame 3.8.1 не
/// предоставляет публичного API буфера, но SDL2 уже загружен игрой —
/// берём символы прямо из него. Любая ошибка возвращает пустую строку,
/// чтобы вставка никогда не роняла игру.
/// </summary>
internal static class ClipboardText
{
    private delegate IntPtr GetClipboardTextFn();
    private delegate void FreeFn(IntPtr mem);

    private static bool _resolved;
    private static GetClipboardTextFn? _getClipboardText;
    private static FreeFn? _free;

    public static string Read()
    {
        try
        {
            Resolve();
            if (_getClipboardText == null) return string.Empty;
            IntPtr ptr = _getClipboardText();
            if (ptr == IntPtr.Zero) return string.Empty;
            try
            {
                string? text = Marshal.PtrToStringUTF8(ptr);
                return (text ?? string.Empty).Replace("\0", string.Empty);
            }
            finally
            {
                _free?.Invoke(ptr);
            }
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void Resolve()
    {
        if (_resolved) return;
        _resolved = true;

        string[] candidates =
        {
            "SDL2.dll",
            "libSDL2-2.0.so.0",
            "libSDL2-2.0.0.dylib",
            Path.Combine(AppContext.BaseDirectory, "SDL2.dll"),
            Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native", "SDL2.dll"),
            Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x86", "native", "SDL2.dll"),
            Path.Combine(AppContext.BaseDirectory, "runtimes", "linux-x64", "native", "libSDL2-2.0.so.0"),
            Path.Combine(AppContext.BaseDirectory, "runtimes", "osx-x64", "native", "libSDL2-2.0.0.dylib"),
        };

        foreach (string candidate in candidates)
        {
            if (!NativeLibrary.TryLoad(candidate, out IntPtr handle)) continue;
            if (!NativeLibrary.TryGetExport(handle, "SDL_GetClipboardText", out IntPtr getPtr)
                || !NativeLibrary.TryGetExport(handle, "SDL_free", out IntPtr freePtr))
                continue;
            _getClipboardText = Marshal.GetDelegateForFunctionPointer<GetClipboardTextFn>(getPtr);
            _free = Marshal.GetDelegateForFunctionPointer<FreeFn>(freePtr);
            return;
        }
    }
}
