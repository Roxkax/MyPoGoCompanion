namespace MyPoGoCompanion.Data.Users;

public enum Theme
{
    Device,
    Light,
    Dark,
}

/// <summary>A user's stored role. The super admin is not a stored role: it comes from configuration.</summary>
public enum UserRole
{
    User,
    Admin,
}

/// <summary>
/// A signed-in person (Google account) and their settings. Only the Google name, email and picture are kept.
/// The admin role is stored here and granted by the super admin; the super admin itself comes from configuration
/// (<c>Auth:SuperAdminEmail</c>), so no database change can remove it.
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

    public UserRole Role { get; set; }

    /// <summary>When the current role was granted (null for a user who was never made admin).</summary>
    public DateTimeOffset? RoleGrantedAt { get; set; }

    /// <summary>Who granted the current role; null once that person's account is deleted.</summary>
    public Guid? RoleGrantedByUserId { get; set; }

    public User? RoleGrantedBy { get; set; }
}
