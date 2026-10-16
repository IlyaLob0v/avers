using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Avers.Domain;

namespace Avers.Data;

/// <summary>
/// Пароли хранятся как PBKDF2-SHA512 с солью: <c>pbkdf2.{итерации}.{соль}.{хеш}</c> (Base64).
/// Старые хеши (SHA-256 без соли, 64 hex-символа) ещё принимаются, при входе их надо пересчитать.
/// </summary>
public static class Password
{
    public const int Iterations = 210_000;
    private const string Scheme = "pbkdf2";
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Derive(password, salt, Iterations);
        return string.Join('.', Scheme, Iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt), Convert.ToBase64String(key));
    }

    public static bool Verify(AppUser user, string password)
    {
        var stored = user.PasswordHash;
        if (IsLegacy(stored))
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(stored), LegacyHash(user.Login, password));

        var parts = stored.Split('.');
        if (parts.Length != 4 || parts[0] != Scheme
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations))
            return false;
        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var key = Convert.FromBase64String(parts[3]);
            return CryptographicOperations.FixedTimeEquals(key, Derive(password, salt, iterations, key.Length));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>Хеш старого формата или с меньшим числом итераций — после успешного входа заменить.</summary>
    public static bool NeedsRehash(string stored)
    {
        if (IsLegacy(stored))
            return true;
        var parts = stored.Split('.');
        return parts.Length != 4
               || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations)
               || iterations < Iterations;
    }

    private static byte[] Derive(string password, byte[] salt, int iterations, int size = KeySize) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA512, size);

    private static bool IsLegacy(string stored) => stored.Length == 64 && stored.All(Uri.IsHexDigit);

    private static byte[] LegacyHash(string login, string password) =>
        SHA256.HashData(Encoding.UTF8.GetBytes($"{login.ToLowerInvariant()}:{password}:avers"));
}
