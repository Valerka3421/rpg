// Полифиллы .NET Standard 2.1 (Unity): API, появившиеся в .NET 5+.
namespace System.Runtime.CompilerServices
{
    public static class IsExternalInit { }
}

namespace Aethelgard.Systems
{
    public static partial class NetCompat
    {
        /// <summary>Random.Shared — отсутствует в .NET Standard 2.1.</summary>
        public static readonly System.Random RandomShared = new();

        /// <summary>HMACSHA256.HashData — отсутствует в .NET Standard 2.1.</summary>
        public static byte[] HashSha256(byte[] key, byte[] data)
        {
            using var hmac = new System.Security.Cryptography.HMACSHA256(key);
            return hmac.ComputeHash(data);
        }

        /// <summary>RandomNumberGenerator.GetBytes — отсутствует в .NET Standard 2.1.</summary>
        public static byte[] GetRandomBytes(int count)
        {
            var b = new byte[count];
            System.Security.Cryptography.RandomNumberGenerator.Fill(b);
            return b;
        }

        /// <summary>Rfc2898DeriveBytes.Pbkdf2 — отсутствует в .NET Standard 2.1.</summary>
        public static byte[] Pbkdf2(byte[] password, byte[] salt, int iterations,
            System.Security.Cryptography.HashAlgorithmName algo, int length)
        {
            using var derive = new System.Security.Cryptography.Rfc2898DeriveBytes(
                password, salt, iterations, algo);
            return derive.GetBytes(length);
        }
    }
}
