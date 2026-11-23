using System.Reflection;
using Aethelgard.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

/// <summary>Screenshot probe: runs the real loop and saves PNG of every screen.</summary>
public static class Program
{
    private static volatile bool _wantShot;
    private static string _shotName = "";

    public static void Main()
    {
        string outDir = Environment.GetEnvironmentVariable("SHOTS_DIR") ?? "/tmp/shots";
        Directory.CreateDirectory(outDir);

        var game = new Aethelgard.World.AethelgardGame();
        Aethelgard.World.AethelgardGame.CaptureHook = gd => Capture(gd, outDir);

        var t = new Thread(() =>
        {
            Thread.Sleep(1800);
            var g = game.GetType().GetField("_game",
                BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(game) as GameController;

            // 1. Главное меню
            Thread.Sleep(400); Shot("01-menu");
            // 2. Выбор класса
            g!.ScreenMode = ScreenMode.ClassSelect; Thread.Sleep(1200); Shot("02-classselect");
            // 3. Игра (HUD)
            g.NewGame("warrior"); Thread.Sleep(2000); Shot("03-game-hud");
            // 4. Инвентарь
            g.ScreenMode = ScreenMode.Inventory; Thread.Sleep(1200); Shot("04-inventory");
            // 5. Характеристики
            g.ScreenMode = ScreenMode.Stats; Thread.Sleep(1200); Shot("05-stats");
            // 6. Крафт
            g.ScreenMode = ScreenMode.Craft; Thread.Sleep(1200); Shot("06-craft");
            // 7. Древо навыков
            g.ScreenMode = ScreenMode.Skills; Thread.Sleep(1200); Shot("07-skills");
            // 8. Квесты
            g.ScreenMode = ScreenMode.Quests; Thread.Sleep(1200); Shot("08-quests");
            // 9. Пауза
            g.ScreenMode = ScreenMode.Pause; Thread.Sleep(1200); Shot("09-pause");
            g.ScreenMode = ScreenMode.Game;
            Thread.Sleep(500);
            game.Exit();
        }) { IsBackground = true };
        t.Start();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        game.Run();
        Console.WriteLine($"done in {sw.ElapsedMilliseconds} ms");
    }

    private static void Shot(string name)
    {
        _shotName = name;
        _wantShot = true;
        // ждём, пока кадр с этим флагом будет снят
        for (int i = 0; i < 300 && _wantShot; i++) Thread.Sleep(10);
    }

    private static void Capture(GraphicsDevice gd, string outDir)
    {
        if (!_wantShot) return;
        _wantShot = false;
        try
        {
            int w = gd.Viewport.Width, h = gd.Viewport.Height;
            var tex = new Texture2D(gd, w, h, false, SurfaceFormat.Color);
            var data = new Color[w * h];
            gd.GetBackBufferData(data);
            tex.SetData(data);
            using var fs = File.Create(Path.Combine(outDir, _shotName + ".png"));
            tex.SaveAsPng(fs, w, h);
            tex.Dispose();
            Console.WriteLine($"[shot] {_shotName}.png {w}x{h}");
        }
        catch (Exception ex) { Console.WriteLine($"[shot FAIL] {ex.Message}"); }
    }
}
