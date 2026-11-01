using System.Security.Cryptography;
using System.Text;

namespace MyPoGoCompanion.Api.Auth;

/// <summary>Refresh tokens: 32 random bytes as base64url text; only their SHA-256 hash is stored.</summary>
internal static class RefreshTokenValue
{
    public static string New() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>The shape of a token we issued (43 base64url characters), so junk never reaches the database.</summary>
    public static bool LooksValid(string? token) =>
        token is { Length: 43 } && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
