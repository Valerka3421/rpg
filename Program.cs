using System;

namespace Aethelgard;

/// <summary>
/// Entry point. MonoGame's Game handles the loop.
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        using var game = new Game();
        game.Run();
    }
}
