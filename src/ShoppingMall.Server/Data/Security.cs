using System.Security.Cryptography;
using System.Text;

namespace ShoppingMall.Server.Data;

public static class Security
{
    /// <summary>SHA-256 hex (소문자). member_seed.sql 의 해시와 같은 방식이다.</summary>
    public static string HashPassword(string password) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLowerInvariant();

    public static bool HashEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
