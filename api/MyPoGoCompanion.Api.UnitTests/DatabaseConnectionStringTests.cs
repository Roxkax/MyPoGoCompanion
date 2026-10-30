using MyPoGoCompanion.Api.Configuration;
using Npgsql;

namespace MyPoGoCompanion.Api.UnitTests;

public class DatabaseConnectionStringTests
{
    [Fact]
    public void Normalize_KeyValueForm_KeepsSettings()
    {
        var result = new NpgsqlConnectionStringBuilder(DatabaseConnectionString.Normalize(
            "Host=localhost;Port=5432;Database=mypogocompanion;Username=mypogocompanion;Password=localdev"));

        Assert.Equal("localhost", result.Host);
        Assert.Equal(5432, result.Port);
        Assert.Equal("mypogocompanion", result.Database);
        Assert.Equal("mypogocompanion", result.Username);
        Assert.Equal("localdev", result.Password);
    }

    [Theory]
    [InlineData("Host=localhost")]
    [InlineData("postgresql://user@localhost/db")]
    public void Normalize_NoGssSetting_DisablesGssEncryption(string value) =>
        Assert.Equal(GssEncryptionMode.Disable, new NpgsqlConnectionStringBuilder(DatabaseConnectionString.Normalize(value)).GssEncryptionMode);

    [Fact]
    public void Normalize_GssRequired_KeepsRequire() =>
        Assert.Equal(
            GssEncryptionMode.Require,
            new NpgsqlConnectionStringBuilder(DatabaseConnectionString.Normalize("Host=localhost;Gss Encryption Mode=Require")).GssEncryptionMode);

    [Fact]
    public void Normalize_NeonUrl_ReturnsNpgsqlConnectionString()
    {
        var result = new NpgsqlConnectionStringBuilder(DatabaseConnectionString.Normalize(
            "postgresql://neondb_owner:s3cr%40t@ep-cool-name-123.eu-central-1.aws.neon.tech/neondb?sslmode=require&channel_binding=require"));

        Assert.Equal("ep-cool-name-123.eu-central-1.aws.neon.tech", result.Host);
        Assert.Equal(5432, result.Port);
        Assert.Equal("neondb", result.Database);
        Assert.Equal("neondb_owner", result.Username);
        Assert.Equal("s3cr@t", result.Password);
        Assert.Equal(SslMode.Require, result.SslMode);
        Assert.Equal(ChannelBinding.Require, result.ChannelBinding);
    }

    [Fact]
    public void Normalize_PostgresSchemeWithPort_ReturnsHostAndPort()
    {
        var result = new NpgsqlConnectionStringBuilder(DatabaseConnectionString.Normalize("postgres://app@db.local:6543/companion"));

        Assert.Equal("db.local", result.Host);
        Assert.Equal(6543, result.Port);
        Assert.Equal("companion", result.Database);
        Assert.Equal("app", result.Username);
        Assert.Null(result.Password);
    }

    [Fact]
    public void Normalize_UnknownUrlParameter_ThrowsWithoutPassword()
    {
        var error = Assert.Throws<FormatException>(() =>
            DatabaseConnectionString.Normalize("postgresql://user:hunter2@host/db?not_a_setting=1"));

        Assert.Contains("not_a_setting", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_EmptyValue_Throws(string value) =>
        Assert.ThrowsAny<ArgumentException>(() => DatabaseConnectionString.Normalize(value));
}
