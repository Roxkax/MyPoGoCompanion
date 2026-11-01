namespace MyPoGoCompanion.Data.Users;

public enum Theme
{
    Device,
    Light,
    Dark,
}

/// <summary>
/// A signed-in person (Google account) and their settings. Only the Google name, email and picture are kept.
/// The admin role is not stored: it comes from configuration.
/// </summary>
public class User
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Google's stable account id (the ID token's <c>sub</c>).</summary>
    public required string GoogleSubject { get; set; }

    public required string Email { get; set; }

    public required string DisplayName { get; set; }

    public string? PictureUrl { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastSignInAt { get; set; }

    /// <summary><c>en</c> or <c>pt-PT</c>.</summary>
    public string Language { get; set; } = "en";

    public Theme Theme { get; set; }
}
