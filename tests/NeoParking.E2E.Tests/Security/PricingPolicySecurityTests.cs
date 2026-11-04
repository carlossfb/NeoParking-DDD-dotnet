namespace NeoParking.E2E.Tests.Security;

using System.Net;
using FluentAssertions;

// TD-102
[Trait("Category", "Integration")]
[Collection(SecurityCollection.Name)]
public class PricingPolicySecurityTests
{
    private readonly SecurityFixture _f;

    public PricingPolicySecurityTests(SecurityFixture fixture) => _f = fixture;

    private static object Body(Guid ownerId) => new
    {
        ownerId,
        minimumFee = 10m,
        hourlyRate = 4m,
        minimumFeeCoverageMinutes = 60,
        gracePeriodMinutes = 0
    };

    [Fact]
    public async Task OperatorAndAdmin_ShouldBeForbidden_EvenSendingTheOwnerId()
    {
        var byOperator = await _f.SendAsync(HttpMethod.Post, "/billing/pricing-policies", _f.Operator.Token, Body(_f.Owner.Id));
        var byAdmin = await _f.SendAsync(HttpMethod.Post, "/billing/pricing-policies", _f.Admin.Token, Body(_f.Owner.Id));

        byOperator.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        byAdmin.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SavedPolicy_ShouldUseTheOwnerIdFromTheToken_IgnoringTheBody()
    {
        var created = await _f.SendAsync(HttpMethod.Post, "/billing/pricing-policies", _f.Owner.Token, Body(Guid.NewGuid()));

        (await _f.ReadAsync<PolicyResponse>(created)).OwnerId.Should().Be(_f.Owner.Id);
    }

    private sealed record PolicyResponse(Guid OwnerId);
}
