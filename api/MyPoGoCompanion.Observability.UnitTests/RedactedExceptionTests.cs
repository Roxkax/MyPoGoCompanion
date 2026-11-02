namespace MyPoGoCompanion.Observability.UnitTests;

public class RedactedExceptionTests
{
    [Fact]
    public void An_exception_without_personal_data_stays_as_it_is()
    {
        var exception = Thrown(new InvalidOperationException("The database is unreachable"));
        Assert.Same(exception, RedactedException.From(exception));
    }

    [Fact]
    public void Personal_data_in_the_message_is_removed_and_the_type_and_stack_kept()
    {
        var exception = Thrown(new InvalidOperationException("No account for ash.pt@example.com"));

        var redacted = Assert.IsType<RedactedException>(RedactedException.From(exception));

        Assert.Equal("System.InvalidOperationException", redacted.OriginalType);
        Assert.Equal("System.InvalidOperationException: No account for [email]", redacted.Message);
        Assert.Contains(nameof(Thrown), redacted.StackTrace, StringComparison.Ordinal);
        Assert.DoesNotContain("ash.pt", redacted.ToString(), StringComparison.Ordinal);
        Assert.StartsWith(redacted.Message, redacted.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Personal_data_in_an_inner_exception_is_removed()
    {
        var inner = Thrown(new ArgumentException("bad token pX7k2Qm9vB4nR8sT1wY6zA3cF5hJ0lN2dG4eK7oU9iE"));
        var exception = Thrown(new InvalidOperationException("Refresh failed", inner));

        var redacted = RedactedException.From(exception);

        Assert.IsType<RedactedException>(redacted);
        Assert.DoesNotContain("pX7k2Qm9", redacted.ToString(), StringComparison.Ordinal);
        Assert.Contains("[token]", redacted.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_never_thrown_exception_has_no_stack_trace()
    {
        var redacted = RedactedException.From(new InvalidOperationException("ash.pt@example.com"));
        Assert.Null(redacted.StackTrace);
        Assert.Equal(redacted.Message, redacted.ToString());
    }

    [Fact]
    public void The_usual_constructors_work()
    {
        Assert.Equal(typeof(RedactedException).FullName, new RedactedException().OriginalType);
        Assert.Equal("text", new RedactedException("text").Message);
        Assert.NotNull(new RedactedException("text", new InvalidOperationException()).InnerException);
    }

    private static T Thrown<T>(T exception)
        where T : Exception
    {
        try
        {
            throw exception;
        }
        catch (T caught)
        {
            return caught;
        }
    }
}
