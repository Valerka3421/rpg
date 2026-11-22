namespace Aethelgard;

/// <summary>
/// Точка входа. Цикл игры обрабатывает AethelgardGame (World/Game.cs).
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        using var game = new AethelgardGame();
        game.Run();
    }
}
