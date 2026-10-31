namespace MyPoGoCompanion.Api.UnitTests;

// Throwaway (E00-F09, issue #54): a deliberately failing test to show the branch rules block the merge.
// This pull request is closed without merging.
public class BranchRulesProofTests
{
    [Fact]
    public void DeliberatelyFails() => Assert.Equal(1, 2);
}
