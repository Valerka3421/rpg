using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;

namespace Aethelgard.Net;

/// <summary>
/// Единый сетевой контракт: версия протокола, JSON-опции и подпись сообщений.
/// Опции обязаны быть одинаковыми на клиенте и сервере, иначе HMAC и
/// десериализация расходятся.
/// </summary>
public static class Protocol
{
    /// <summary>Версия протокола. Несовпадение → сервер явно отклоняет клиента.</summary>
    public const int Version = 2;

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new ColorJsonConverter() },
    };

    public static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, JsonOptions);

    public static T? Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, JsonOptions);

    /// <summary>Каноническая полезная нагрузка: тот же объект без поля Sig.</summary>
    public static string SignedPayload(ClientMsg msg)
    {
        string? sig = msg.Sig;
        msg.Sig = null;
        try
        {
            return Serialize(msg);
        }
        finally
        {
            msg.Sig = sig;
        }
    }

    public static string Sign(string payload, byte[] key) =>
        Convert.ToBase64String(
            NetCompat.HashSha256(key, Encoding.UTF8.GetBytes(payload)));

    public static bool Verify(string payload, string signature, byte[] key)
    {
        try
        {
            byte[] expected = NetCompat.HashSha256(key, Encoding.UTF8.GetBytes(payload));
            byte[] actual = Convert.FromBase64String(signature);
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

/// <summary>
/// MonoGame Color → "R,G,B,A". Нужен, потому что Color — структура
/// с полями, а не с свойствами, и STJ иначе сериализует её как {}.
/// </summary>
public sealed class ColorJsonConverter : JsonConverter<Color>
{
    public override Color Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        string? text = reader.GetString();
        if (string.IsNullOrWhiteSpace(text))
            return Color.White;

        string[] parts = text.Split(',');
        if (parts.Length < 3)
            return Color.White;

        static byte Parse(string s, byte fallback) =>
            byte.TryParse(s.Trim(), out byte v) ? v : fallback;

        byte a = parts.Length >= 4 ? Parse(parts[3], (byte)255) : (byte)255;
        return new Color(Parse(parts[0], 255), Parse(parts[1], 255),
            Parse(parts[2], 255), a);
    }

    public override void Write(Utf8JsonWriter writer, Color value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue($"{value.R},{value.G},{value.B},{value.A}");
    }
}
