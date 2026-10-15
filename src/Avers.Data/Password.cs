using System.Security.Cryptography;
using System.Text;
using Avers.Domain;

namespace Avers.Data;

public static class Password
{
    public static string Hash(string login, string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{login.ToLowerInvariant()}:{password}:avers"));
        return Convert.ToHexString(bytes);
    }

    public static bool Verify(AppUser user, string password) =>
        string.Equals(user.PasswordHash, Hash(user.Login, password), StringComparison.Ordinal);
}
