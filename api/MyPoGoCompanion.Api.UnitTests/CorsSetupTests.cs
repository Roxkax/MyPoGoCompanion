using MyPoGoCompanion.Api.Configuration;

namespace MyPoGoCompanion.Api.UnitTests;

public class CorsSetupTests
{
    [Fact]
    public void ParseOrigins_CommaSeparated_TrimsAndDeduplicates()
    {
        var origins = CorsSetup.ParseOrigins(" https://app.example/ , http://localhost:5173,https://APP.example ,, ");

        Assert.Equal(["https://app.example", "http://localhost:5173"], origins);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ParseOrigins_Empty_ReturnsNoOrigins(string? value) => Assert.Empty(CorsSetup.ParseOrigins(value));
}
