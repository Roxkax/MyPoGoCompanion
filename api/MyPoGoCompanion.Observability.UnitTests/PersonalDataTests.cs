namespace MyPoGoCompanion.Observability.UnitTests;

public class PersonalDataTests
{
    [Theory]
    [InlineData("Signed in ash.pt@example.com", "Signed in [email]")]
    [InlineData("Owner: Diogo.Goncalves+mpgc@gmail.co.uk.", "Owner: [email].")]
    [InlineData("Authorization: Bearer eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.c2ln", "Authorization: Bearer [token]")]
    [InlineData("token eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.c2lnbmF0dXJl here", "token [token] here")]
    [InlineData("refresh pX7k2Qm9vB4nR8sT1wY6zA3cF5hJ0lN2dG4eK7oU9iE", "refresh [token]")]
    [InlineData("code 4/0AeanS0a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6", "code 4/[token]")]
    [InlineData("from 203.0.113.42 via 10.0.0.1", "from [ip] via [ip]")]
    public void Removes_personal_data(string text, string expected) => Assert.Equal(expected, PersonalData.Scrub(text));

    [Theory]
    [InlineData("User 0f8c2d1e-5b7a-4c39-9e61-2a4d8b3f7c10 changed the language")]
    [InlineData("Trace 4bf92f3577b34da6a3ce929d0e0e4736 span 00f067aa0ba902b7")]
    [InlineData("Could not load MyPoGoCompanion.Data, Version=10.0.14.0, Culture=neutral")]
    [InlineData("Web app error 7f3a2c91 (Screen) on /battle/raids/mewtwo, web version 3cd4b0c")]
    [InlineData("at MyPoGoCompanion.Api.Auth.Sessions.RefreshAsync(String token) in /src/Sessions.cs:line 120")]
    [InlineData("Version 1.2.3 and 10.0.14")]
    public void Keeps_what_is_not_personal(string text) => Assert.Equal(text, PersonalData.Scrub(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Leaves_nothing_as_it_is(string? text) => Assert.Equal(text, PersonalData.Scrub(text));

    [Fact]
    public void Says_whether_text_holds_personal_data()
    {
        Assert.True(PersonalData.Contains("ash.pt@example.com"));
        Assert.False(PersonalData.Contains("nothing here"));
        Assert.False(PersonalData.Contains(null));
    }
}
