namespace NeoParking.E2E.Tests.ViewRoutes;

using System.Net;
using FluentAssertions;
using NeoParking.E2E.Tests.Security;

/// <summary>TD-804: a navegação pergunta quem é o usuário, e o papel vem do banco.</summary>
[Trait("Category", "Integration")]
[Collection(SecurityCollection.Name)]
public sealed class CurrentOperatorTests
{
    private sealed record Me(Guid Id, string Email, string Role, bool IsActive);

    private readonly SecurityFixture _f;

    public CurrentOperatorTests(SecurityFixture fixture) => _f = fixture;

    [Theory]
    [InlineData("Operator")]
    [InlineData("Admin")]
    public async Task Me_ShouldDescribeTheTokenOwner(string role)
    {
        var actor = await _f.CreateActorAsync(role);

        var me = await _f.ReadAsync<Me>(await _f.SendAsync(HttpMethod.Get, "/operators/me", actor.Token));

        me.Id.Should().Be(actor.Id);
        me.Email.Should().Be(actor.Email);
        me.Role.Should().Be(role);
        me.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Me_ShouldReadTheRoleFromTheDatabase_NotFromTheToken()
    {
        var actor = await _f.CreateActorAsync("Admin");
        await _f.DemoteAsync(actor.Id);

        var me = await _f.ReadAsync<Me>(await _f.SendAsync(HttpMethod.Get, "/operators/me", actor.Token));

        me.Role.Should().Be("Operator");
    }

    [Fact]
    public async Task Me_ForADeactivatedAccount_ShouldBeForbidden()
    {
        var actor = await _f.CreateActorAsync("Operator");
        await _f.DeactivateAsync(actor.Id);

        var response = await _f.SendAsync(HttpMethod.Get, "/operators/me", actor.Token);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
