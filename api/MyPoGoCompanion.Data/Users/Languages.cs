namespace MyPoGoCompanion.Data.Users;

/// <summary>The app's interface languages (decision 2026-10-27): English and Português (Portugal).</summary>
public static class Languages
{
    public const string English = "en";

    public const string Portuguese = "pt-PT";

    public static IReadOnlyList<string> All { get; } = [English, Portuguese];

    /// <summary>Exactly one of the app's language codes (<c>en</c>, <c>pt-PT</c>), as the database's check allows.</summary>
    public static bool IsSupported(string? language) => language is English or Portuguese;
}
