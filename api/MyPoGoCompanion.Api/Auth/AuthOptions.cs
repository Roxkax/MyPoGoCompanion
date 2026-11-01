namespace MyPoGoCompanion.Api.Auth;

/// <summary>
/// Sign-in settings, from the <c>Auth</c> configuration section (environment variables <c>Auth__…</c>).
/// Google's client id and secret and the signing key are secrets; without all three, sign-in reports itself
/// as not configured (503) and the rest of the API works as before.
/// </summary>
public sealed class AuthOptions
{
    public const string Section = "Auth";

    /// <summary>The OAuth client id from Google Cloud (<c>Auth__Google__ClientId</c>). Public: the web app sends it to Google.</summary>
    public string? GoogleClientId { get; set; }

    /// <summary>The OAuth client secret (<c>Auth__Google__ClientSecret</c>). Only the API sees it.</summary>
    public string? GoogleClientSecret { get; set; }

    /// <summary>Base64 key, at least 32 bytes, that signs our access tokens (<c>Auth__SigningKey</c>).</summary>
    public string? SigningKey { get; set; }

    /// <summary>The super admin's Google email (<c>Auth__SuperAdminEmail</c>): the owner's account.</summary>
    public string? SuperAdminEmail { get; set; }

    /// <summary>Where the web app receives Google's answer; must be one of the allowed web origins plus this path.</summary>
    public const string CallbackPath = "/auth/callback";

    public const string Issuer = "mypogocompanion-api";

    public const string Audience = "mypogocompanion";

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(30);

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(GoogleClientId)
        && !string.IsNullOrWhiteSpace(GoogleClientSecret)
        && SigningKeyBytes() is not null;

    /// <summary>The decoded signing key, or null when it is missing, not base64 or shorter than 32 bytes.</summary>
    public byte[]? SigningKeyBytes()
    {
        if (string.IsNullOrWhiteSpace(SigningKey))
        {
            return null;
        }

        var buffer = new byte[SigningKey.Length];
        return Convert.TryFromBase64String(SigningKey.Trim(), buffer, out var written) && written >= 32
            ? buffer[..written]
            : null;
    }

    public bool IsSuperAdmin(string? email) =>
        !string.IsNullOrWhiteSpace(SuperAdminEmail)
        && !string.IsNullOrWhiteSpace(email)
        && string.Equals(SuperAdminEmail.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase);
}
