using System;
using System.Security.Cryptography;
using System.Text;

namespace Aethelgard.Systems;

/// <summary>
/// Хеширование паролей: PBKDF2-HMAC-SHA256, соль на каждый пароль.
///
/// Формат хранения: <c>pbkdf2_sha256$iterations$salt_base64$hash_base64</c>.
/// Проверка — constant-time (CryptographicOperations.FixedTimeEquals),
/// поэтому по времени ответа нельзя подбирать пароль посимвольно.
/// </summary>
public static class PasswordHasher
{
    private const int DefaultIterations = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const string Prefix = "pbkdf2_sha256";

    public static string Hash(string password, int iterations = DefaultIterations)
    {
        if (password == null) throw new ArgumentNullException(nameof(password));
        iterations = Math.Max(10_000, iterations);

        byte[] salt = CryptoCompat.RandomBytes(SaltSize);
        byte[] hash = CryptoCompat.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return $"{Prefix}${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>Проверяет пароль против строки, созданной <see cref="Hash"/>.</summary>
    public static bool Verify(string password, string? encoded)
    {
        if (string.IsNullOrEmpty(encoded)) return false;
        if (password == null) return false;

        try
        {
            string[] parts = encoded.Split('$');
            if (parts.Length != 4 || parts[0] != Prefix) return false;
            if (!int.TryParse(parts[1], out int iterations) || iterations < 10_000)
                return false;

            byte[] salt = Convert.FromBase64String(parts[2]);
            byte[] expected = Convert.FromBase64String(parts[3]);
            byte[] actual = CryptoCompat.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expected.Length);

            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (Exception ex) when (ex is FormatException
                                   or ArgumentException
                                   or CryptographicException)
        {
            return false;
        }
    }

}
