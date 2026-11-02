namespace MyPoGoCompanion.Jobs;

/// <summary>
/// The scheduled jobs. <c>dotnet MyPoGoCompanion.Jobs.dll daily-import</c> runs the daily game-data import
/// (docs/DAILY-IMPORT.md); the exit code is 0 when the run succeeded, had nothing to do or was held for the super admin, 1 when it failed.
/// A named class, not top-level statements, so the integration tests can reference both this and the API.
/// </summary>
internal static class Program
{
    private static Task<int> Main(string[] args) => JobHost.RunAsync(args);
}
