using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Aethelgard.Render;

// GPU-часть PixelCanvas (MonoGame): буфер — в Unity-ядре (Core/Render).
public sealed partial class PixelCanvas
{
    public Texture2D ToTexture(GraphicsDevice device)
    {
        var tex = new Texture2D(device, Width, Height, false, SurfaceFormat.Color);
        tex.SetData(Pixels);
        return tex;
    }
}
