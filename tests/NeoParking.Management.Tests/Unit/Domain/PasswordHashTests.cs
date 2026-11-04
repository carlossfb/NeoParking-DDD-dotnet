namespace NeoParking.Management.Tests.Unit.Domain;

using FluentAssertions;
using NeoParking.Management.Domain.Vo;

public class PasswordHashTests
{
    [Fact]
    public void ToString_ShouldNotContainTheHash()
    {
        const string hash = "$2a$11$abcdefghijklmnopqrstuv";

        PasswordHash.FromHash(hash).ToString().Should().NotContain(hash);
    }
}
