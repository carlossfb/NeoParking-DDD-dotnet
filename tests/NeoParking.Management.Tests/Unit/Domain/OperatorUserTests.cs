namespace NeoParking.Management.Tests.Unit.Domain;

using FluentAssertions;
using NeoParking.Management.Domain;
using NeoParking.Management.Domain.Vo;
using NeoParking.Shared.Kernel.Exceptions;

public class OperatorUserTests
{
    private static OperatorUser MakeUser(OperatorRole role = OperatorRole.Operator)
        => OperatorUser.Create("Test User", "test@neo.com", PasswordHash.FromHash("hash"), role);

    // ── criação ─────────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithValidData_ShouldBeActiveByDefault()
    {
        var user = MakeUser();
        user.IsActive.Should().BeTrue();
        user.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_WithEmptyName_ShouldThrowDomainException()
    {
        var act = () => OperatorUser.Create("", "test@neo.com", PasswordHash.FromHash("hash"), OperatorRole.Operator);
        act.Should().Throw<DomainException>().WithMessage("*Name*");
    }

    [Fact]
    public void Create_WithInvalidEmail_ShouldThrowDomainException()
    {
        var act = () => OperatorUser.Create("Test", "not-an-email", PasswordHash.FromHash("hash"), OperatorRole.Operator);
        act.Should().Throw<DomainException>().WithMessage("*email*");
    }

    [Fact]
    public void Create_ShouldNormalizeEmailToLowercase()
    {
        var user = OperatorUser.Create("Test", "Test@NEO.COM", PasswordHash.FromHash("hash"), OperatorRole.Operator);
        user.Email.Should().Be("test@neo.com");
    }

    // ── promoção/rebaixamento ────────────────────────────────────────────────

    [Fact]
    public void PromoteToAdmin_FromOperator_ShouldChangeRole()
    {
        var user = MakeUser(OperatorRole.Operator);
        user.PromoteToAdmin();
        user.Role.Should().Be(OperatorRole.Admin);
    }

    [Fact]
    public void PromoteToAdmin_OnOwner_ShouldThrowDomainException()
    {
        var owner = MakeUser(OperatorRole.Owner);
        var act = () => owner.PromoteToAdmin();
        act.Should().Throw<DomainException>().WithMessage("*Owner*");
    }

    [Fact]
    public void DemoteToOperator_FromAdmin_ShouldChangeRole()
    {
        var user = MakeUser(OperatorRole.Admin);
        user.DemoteToOperator();
        user.Role.Should().Be(OperatorRole.Operator);
    }

    [Fact]
    public void DemoteToOperator_OnOwner_ShouldThrowDomainException()
    {
        var owner = MakeUser(OperatorRole.Owner);
        var act = () => owner.DemoteToOperator();
        act.Should().Throw<DomainException>().WithMessage("*Owner*");
    }

    // ── ativação/desativação ─────────────────────────────────────────────────

    [Fact]
    public void Deactivate_RegularOperator_ShouldSetIsActiveFalse()
    {
        var user = MakeUser(OperatorRole.Operator);
        user.Deactivate();
        user.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Deactivate_Owner_ShouldThrowDomainException()
    {
        var owner = MakeUser(OperatorRole.Owner);
        var act = () => owner.Deactivate();
        act.Should().Throw<DomainException>().WithMessage("*Owner*");
    }

    [Fact]
    public void Activate_DeactivatedOperator_ShouldSetIsActiveTrue()
    {
        var user = MakeUser(OperatorRole.Operator);
        user.Deactivate();
        user.Activate();
        user.IsActive.Should().BeTrue();
    }

    // ── senha ────────────────────────────────────────────────────────────────

    [Fact]
    public void UpdatePassword_ShouldReplaceHash()
    {
        var user = MakeUser();
        var newHash = PasswordHash.FromHash("new-hash");
        user.UpdatePassword(newHash);
        user.PasswordHash.Value.Should().Be("new-hash");
    }
}
