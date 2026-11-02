// The scheduled jobs. `dotnet MyPoGoCompanion.Jobs.dll daily-import` runs the daily game-data import
// (docs/DAILY-IMPORT.md); the exit code is 0 when the run succeeded or had nothing to do, 1 when it failed.
return await MyPoGoCompanion.Jobs.JobHost.RunAsync(args);
