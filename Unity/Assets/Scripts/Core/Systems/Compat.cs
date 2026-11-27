using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Aethelgard;

// Кросс-платформенные обёртки BCL-API: ядро компилируется и в MonoGame
// (net8.0), и в Unity (netstandard2.1-профиль), где часть современных API
// отсутствует (Random.Shared, HMACSHA256.HashData, Pbkdf2, File.Move(3)).
// Обёртки реализованы через API, доступные в ОБОИХ профилях, поэтому
// поведение (и форматы — HMAC, PBKDF2, SPKI) идентичны в обоих движках,
// а клиенты MonoGame и Unity совместимы в мультиплеере.

/// <summary>Потокобезопасный доступ к Random (замена Random.Shared).</summary>
public static class Rand
{
    [ThreadStatic] private static Random? _local;
    private static int _seed = Environment.TickCount;

    public static Random Shared =>
        _local ??= new Random(Interlocked.Increment(ref _seed));
}

public static class CryptoCompat
{
    /// <summary>Замена RandomNumberGenerator.GetBytes(int) (net6+, в Unity-профиле нет).</summary>
    public static byte[] RandomBytes(int length)
    {
#if UNITY_5_3_OR_NEWER
        var buffer = new byte[length];
        RandomNumberGenerator.Fill(buffer);
        return buffer;
#else
        return RandomNumberGenerator.GetBytes(length);
#endif
    }

    /// <summary>Замена HMACSHA256.HashData (доступна с net5, в Unity-профиле нет).</summary>
    public static byte[] HmacSha256(byte[] key, byte[] data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(data);
    }

    /// <summary>Замена Rfc2898DeriveBytes.Pbkdf2 (net5+, в Unity-профиле нет).
    /// Формат совпадает с PBKDF2 в .NET 8.</summary>
    public static byte[] Pbkdf2(byte[] password, byte[] salt, int iterations,
        HashAlgorithmName algorithm, int length)
        => new Rfc2898DeriveBytes(password, salt, iterations, algorithm)
            .GetBytes(length);
}

public static class FileCompat
{
    /// <summary>Замена File.Move(src, dst, overwrite) — 3-арг перегрузки
    /// нет в Unity-профиле. Файл dst после вызова существует.</summary>
    public static void MoveReplace(string src, string dst)
    {
#if UNITY_5_3_OR_NEWER
        if (File.Exists(dst)) File.Delete(dst);
        File.Move(src, dst);
#else
        File.Move(src, dst, true);
#endif
    }
}
