using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Aethelgard.Systems;

/// <summary>
/// Game settings — full parity with Python core/settings.py.
/// </summary>
public class Settings
{
    public int Volume = 7;
    public bool Fullscreen = false;
    public bool ShowFps = false;
    public bool ShowMinimap = true;
    public int[] Resolution = { 1920, 1080 };
    public string? DevConsoleSalt;

    public static string SettingsFile => Path.Combine(Config.DataDir, "settings.json");

    public static Settings Load()
    {
        if (!File.Exists(SettingsFile)) return new Settings();
        try
        {
            var json = File.ReadAllText(SettingsFile);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var s = new Settings();
            if (root.TryGetProperty("volume", out var vol)) s.Volume = vol.GetInt32();
            if (root.TryGetProperty("fullscreen", out var fs)) s.Fullscreen = fs.GetBoolean();
            if (root.TryGetProperty("show_fps", out var fps)) s.ShowFps = fps.GetBoolean();
            if (root.TryGetProperty("show_minimap", out var mm)) s.ShowMinimap = mm.GetBoolean();
            if (root.TryGetProperty("resolution", out var res) && res.ValueKind == JsonValueKind.Array)
            {
                var list = new List<int>();
                foreach (var v in res.EnumerateArray()) list.Add(v.GetInt32());
                if (list.Count >= 2) s.Resolution = new[] { list[0], list[1] };
            }
            if (root.TryGetProperty("dev_console_salt", out var salt)) s.DevConsoleSalt = salt.GetString();
            return s;
        }
        catch
        {
            return new Settings();
        }
    }

    public void Save()
    {
        var tmp = SettingsFile + ".tmp";
        try
        {
            var json = JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["volume"] = Volume,
                ["fullscreen"] = Fullscreen,
                ["show_fps"] = ShowFps,
                ["show_minimap"] = ShowMinimap,
                ["resolution"] = Resolution,
                ["dev_console_salt"] = DevConsoleSalt,
            }, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(tmp, json);
            FileCompat.MoveReplace(tmp, SettingsFile);
        }
        catch
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
        }
    }
}
