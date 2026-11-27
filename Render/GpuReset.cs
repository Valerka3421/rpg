using Microsoft.Xna.Framework.Graphics;

namespace Aethelgard.Render;

/// <summary>
/// Единая точка подписки на DeviceReset для ленивых кэшей GPU-ресурсов
/// (1×1 blank-текстуры, фон меню и т.п.). TileRenderer и SpriteStore
/// имеют собственные хуки; всё остальное регистрируется здесь — раньше
/// после смены fullscreen/разрешения эти текстуры оставались инвалидными.
/// </summary>
public static class GpuReset
{
    private static GraphicsDevice? _device;
    private static readonly List<Action> Handlers = new();

    public static void Register(GraphicsDevice device, Action onReset)
    {
        if (_device == null)
        {
            _device = device;
            device.DeviceResetting += (_, _) =>
            {
                foreach (var h in Handlers)
                {
                    try { h(); } catch { }
                }
            };
        }
        Handlers.Add(onReset);
    }
}
