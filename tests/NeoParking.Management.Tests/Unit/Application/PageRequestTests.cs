namespace NeoParking.Management.Tests.Unit.Application;

using FluentAssertions;
using NeoParking.Shared.Kernel.Primitives;

public class PageRequestTests
{
    [Theory]
    [InlineData(1, 20, 1, 20, 0)]
    [InlineData(3, 10, 3, 10, 20)]
    [InlineData(0, 0, 1, 1, 0)]
    [InlineData(-5, -5, 1, 1, 0)]
    [InlineData(1, 1000, 1, 100, 0)]
    public void Constructor_ShouldKeepValuesInsideTheLimits(int page, int size, int expectedPage, int expectedSize, int skip)
    {
        var request = new PageRequest(page, size);

        request.Page.Should().Be(expectedPage);
        request.PageSize.Should().Be(expectedSize);
        request.Skip.Should().Be(skip);
    }

    [Fact]
    public void Skip_ShouldNotOverflow_WithTheLargestPage()
    {
        new PageRequest(int.MaxValue, int.MaxValue).Skip.Should().BePositive();
    }
}
