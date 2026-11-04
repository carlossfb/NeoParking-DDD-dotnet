namespace NeoParking.Tests;

using Xunit;

public class Td001CiProofTests
{
    [Fact]
    public void BrokenTest_ShouldTurnThePullRequestRed() => Assert.True(false, "TD-001: intentional failure, do not merge");
}
