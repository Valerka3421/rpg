using System.Reflection;
using Aethelgard.Systems;

/// <summary>Smoke test: real game loop cycles game → pause → inventory → stats.</summary>
public static class Program
{
    public static void Main()
    {
        var game = new Aethelgard.World.AethelgardGame();

        var t = new System.Threading.Thread(() =>
        {
            System.Threading.Thread.Sleep(1500);
            var g = game.GetType().GetField("_game",
                BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(game) as GameController;
            g!.ScreenMode = ScreenMode.ClassSelect;
            System.Threading.Thread.Sleep(500);
            g.NewGame("warrior");
            Console.WriteLine("[sim] NewGame done");
            System.Threading.Thread.Sleep(1000);

            foreach (var phase in new[] { ScreenMode.Pause, ScreenMode.Inventory,
                ScreenMode.Stats, ScreenMode.Game })
            {
                g.ScreenMode = phase;
                Console.WriteLine($"[sim] phase {phase}");
                System.Threading.Thread.Sleep(1500);
            }
            game.Exit();
        }) { IsBackground = true };
        t.Start();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        game.Run();
        Console.WriteLine($"game exited cleanly after {sw.ElapsedMilliseconds} ms");
    }
}
