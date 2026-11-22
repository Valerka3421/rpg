using Microsoft.Xna.Framework;

namespace Aethelgard.Structs;

/// <summary>Всплывающий текст (урон, лечение, золото).</summary>
public class FloatText
{
    public float X, Y;
    public string Text = "";
    public Color Color;
    public float Life = 0.7f;
}

/// <summary>Сообщение в журнале событий.</summary>
public class Message
{
    public string Text = "";
    public Color Color = Config.ColText;

    public Message() { }
    public Message(string text, Color? color = null)
    {
        Text = text;
        Color = color ?? Config.ColText;
    }
}
