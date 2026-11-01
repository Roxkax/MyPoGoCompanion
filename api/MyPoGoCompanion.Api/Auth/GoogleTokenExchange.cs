using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace MyPoGoCompanion.Api.Auth;

/// <summary>A Google account as Google confirmed it: stable id, verified email, name and picture.</summary>
public sealed record GoogleIdentity(string Subject, string Email, string Name, string? PictureUrl);

/// <summary>Swaps the one-time code Google gave the web app for the account it belongs to.</summary>
public interface IGoogleTokenExchange
{
    /// <summary>The account, or null when Google refuses the code or its answer doesn't pass the checks.</summary>
    Task<GoogleIdentity?> ExchangeAsync(string code, string codeVerifier, string redirectUri, CancellationToken cancellationToken);
}

/// <summary>
/// Authorization code + PKCE against Google's token endpoint, with the client secret. The ID token comes straight
/// from Google over TLS in answer to our own authenticated request, so (OpenID Connect Core 3.1.3.7) its issuer,
/// audience, expiry and verified email are checked and the signature check is left to TLS. Google's own access and
/// refresh tokens are thrown away: the app only needs to know who signed in.
/// </summary>
internal sealed partial class GoogleTokenExchange(HttpClient http, IOptions<AuthOptions> options, TimeProvider time, ILogger<GoogleTokenExchange> logger)
    : IGoogleTokenExchange
{
    public static readonly Uri TokenEndpoint = new("https://oauth2.googleapis.com/token");

    private static readonly string[] _issuers = ["https://accounts.google.com", "accounts.google.com"];

    public async Task<GoogleIdentity?> ExchangeAsync(string code, string codeVerifier, string redirectUri, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["code_verifier"] = codeVerifier,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = settings.GoogleClientId ?? string.Empty,
            ["client_secret"] = settings.GoogleClientSecret ?? string.Empty,
        });

        using var response = await http.PostAsync(TokenEndpoint, form, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // Google's error codes (invalid_grant, redirect_uri_mismatch…) carry no secrets and explain failures.
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            LogRefused(logger, (int)response.StatusCode, error.Length > 300 ? error[..300] : error);
            return null;
        }

        var body = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
        var claims = ReadPayload(body?.IdToken);
        if (claims is null || !IsValid(claims, settings.GoogleClientId))
        {
            LogRejected(logger);
            return null;
        }

        return new GoogleIdentity(claims.Subject!, claims.Email!, string.IsNullOrWhiteSpace(claims.Name) ? claims.Email! : claims.Name, claims.Picture);
    }

    private bool IsValid(IdTokenClaims claims, string? clientId) =>
        _issuers.Contains(claims.Issuer, StringComparer.Ordinal)
        && claims.Audience == clientId
        && claims.Expires > time.GetUtcNow().ToUnixTimeSeconds()
        && claims.EmailVerified
        && !string.IsNullOrWhiteSpace(claims.Subject)
        && !string.IsNullOrWhiteSpace(claims.Email);

    /// <summary>The ID token's claims (its middle, base64url part), or null when it isn't a JWT.</summary>
    internal static IdTokenClaims? ReadPayload(string? idToken)
    {
        var parts = idToken?.Split('.');
        if (parts is not { Length: 3 })
        {
            return null;
        }

        try
        {
            var json = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            return JsonSerializer.Deserialize<IdTokenClaims>(json);
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return null;
        }
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '='));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Google refused the sign-in code ({Status}): {Error}")]
    private static partial void LogRefused(ILogger logger, int status, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Google's ID token failed the checks (issuer, audience, expiry or verified email)")]
    private static partial void LogRejected(ILogger logger);

    private sealed record TokenResponse([property: JsonPropertyName("id_token")] string? IdToken);

    internal sealed record IdTokenClaims(
        [property: JsonPropertyName("iss")] string? Issuer,
        [property: JsonPropertyName("aud")] string? Audience,
        [property: JsonPropertyName("sub")] string? Subject,
        [property: JsonPropertyName("exp")] long Expires,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("email_verified"), JsonConverter(typeof(BoolOrStringConverter))] bool EmailVerified,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("picture")] string? Picture);

    /// <summary>Google has sent <c>email_verified</c> both as <c>true</c> and as <c>"true"</c>.</summary>
    internal sealed class BoolOrStringConverter : JsonConverter<bool>
    {
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType switch
            {
                JsonTokenType.True => true,
                JsonTokenType.String => string.Equals(reader.GetString(), "true", StringComparison.OrdinalIgnoreCase),
                _ => false,
            };

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) => writer.WriteBooleanValue(value);
    }
}
