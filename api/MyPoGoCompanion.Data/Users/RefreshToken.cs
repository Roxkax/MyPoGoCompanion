namespace MyPoGoCompanion.Data.Users;

/// <summary>
/// One refresh token of a sign-in. A sign-in (one browser or device) is a <see cref="SessionId"/>: every refresh
/// replaces its token with a new one (rotation), so a sign-in is a chain of tokens with one live token at a time.
/// Only the SHA-256 hash is stored, never the token. A token used twice means it was copied: the whole sign-in ends.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>The sign-in this token belongs to (the same for every token in the chain).</summary>
    public Guid SessionId { get; set; }

    /// <summary>SHA-256 of the token, as 64 lowercase hex characters.</summary>
    public required string TokenHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>When it was swapped for the next token; a second use after this ends the sign-in. A concurrency token,
    /// so two requests racing with the same token can't both get a new one.</summary>
    public DateTimeOffset? UsedAt { get; set; }

    /// <summary>When it was cancelled (sign-out, sign-out everywhere, reuse detected). Also a concurrency token.</summary>
    public DateTimeOffset? RevokedAt { get; set; }
}
