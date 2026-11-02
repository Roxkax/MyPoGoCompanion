namespace MyPoGoCompanion.Jobs.DailyImport;

/// <summary>
/// A run that can't go on, with the reason in plain words: the message is what the run log (and the admin's Data
/// status screen) shows.
/// </summary>
public sealed class ImportException : Exception
{
    public ImportException()
    {
    }

    public ImportException(string message)
        : base(message)
    {
    }

    public ImportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
