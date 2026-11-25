using MyPoGoCompanion.Api.Filters;

namespace MyPoGoCompanion.Api.UnitTests.Filters;

public sealed class SavedFilterEndpointsTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData("Fire & Água", true)]
    [InlineData("First\nSecond", false)]
    [InlineData("Nul\0Name", false)]
    public void NamesAreValidatedAfterTrimming(string? name, bool valid) =>
        Assert.Equal(valid, SavedFilterEndpoints.ValidName(name?.Trim()));

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void NameLengthMatchesTheStorageLimit(int length, bool valid) =>
        Assert.Equal(valid, SavedFilterEndpoints.ValidName(new string('a', length)));
}
