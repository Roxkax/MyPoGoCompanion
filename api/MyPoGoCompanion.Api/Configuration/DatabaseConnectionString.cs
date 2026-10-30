using Npgsql;

namespace MyPoGoCompanion.Api.Configuration;

/// <summary>
/// Accepts the database connection string either in Npgsql's "Host=…;Database=…" form or as the
/// postgresql:// URL that Neon (and psql) use, so the same secret works everywhere.
/// Kerberos (GSS) encryption is turned off unless asked for: neither Neon nor the local database uses it, and
/// trying it needs a system library the slim API image doesn't have.
/// </summary>
internal static class DatabaseConnectionString
{
    public static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var builder = value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
            ? FromUrl(value)
            : new NpgsqlConnectionStringBuilder(value);

        if (builder.GssEncryptionMode == GssEncryptionMode.Prefer)
        {
            builder.GssEncryptionMode = GssEncryptionMode.Disable;
        }

        return builder.ConnectionString;
    }

    private static NpgsqlConnectionStringBuilder FromUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
        {
            throw new FormatException("The database URL is not a valid postgresql:// URL.");
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
        };

        var database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        if (database.Length > 0)
        {
            builder.Database = database;
        }

        var userInfo = uri.UserInfo.Split(':', 2);
        if (userInfo[0].Length > 0)
        {
            builder.Username = Uri.UnescapeDataString(userInfo[0]);
        }

        if (userInfo.Length == 2)
        {
            builder.Password = Uri.UnescapeDataString(userInfo[1]);
        }

        foreach (var parameter in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = parameter.Split('=', 2);
            var key = Uri.UnescapeDataString(pair[0]).Replace('_', ' ');
            var setting = pair.Length == 2 ? Uri.UnescapeDataString(pair[1]) : string.Empty;
            try
            {
                builder[key] = setting;
            }
            catch (ArgumentException exception)
            {
                // The message names the parameter only, never the URL (it holds the password).
                throw new FormatException($"The database URL has an unsupported parameter '{pair[0]}'.", exception);
            }
        }

        return builder;
    }
}
