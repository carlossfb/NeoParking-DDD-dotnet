namespace NeoParking.Tests.Architecture;

using FluentAssertions;
using NetArchTest.Rules;
using System.Reflection;

public class DomainArchitectureTests
{
    private static readonly Assembly AccessAssembly    = typeof(NeoParking.Access.Domain.Client).Assembly;
    private static readonly Assembly ManagementAssembly = typeof(NeoParking.Management.Domain.OperatorUser).Assembly;

    // ── Access BC ────────────────────────────────────────────────────────────

    [Fact]
    public void Access_Domain_ShouldNotDependOnInfrastructure()
    {
        Types.InAssembly(AccessAssembly)
            .That().ResideInNamespaceStartingWith("NeoParking.Access.Domain")
            .ShouldNot().HaveDependencyOn("NeoParking.Access.Infrastructure")
            .GetResult().IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Access_Domain_ShouldNotDependOnApplication()
    {
        Types.InAssembly(AccessAssembly)
            .That().ResideInNamespaceStartingWith("NeoParking.Access.Domain")
            .ShouldNot().HaveDependencyOn("NeoParking.Access.Application")
            .GetResult().IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Access_Domain_ShouldNotHaveExternalDependencies()
    {
        Types.InAssembly(AccessAssembly)
            .That().ResideInNamespaceStartingWith("NeoParking.Access.Domain")
            .ShouldNot().HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "MongoDB.Driver")
            .GetResult().IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Access_Application_ShouldNotDependOnInfrastructure()
    {
        Types.InAssembly(AccessAssembly)
            .That().ResideInNamespaceStartingWith("NeoParking.Access.Application")
            .ShouldNot().HaveDependencyOn("NeoParking.Access.Infrastructure")
            .GetResult().IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Access_Application_ShouldNotHaveExternalDependencies()
    {
        Types.InAssembly(AccessAssembly)
            .That().ResideInNamespaceStartingWith("NeoParking.Access.Application")
            .ShouldNot().HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "MongoDB.Driver")
            .GetResult().IsSuccessful.Should().BeTrue();
    }

    // ── Management BC ────────────────────────────────────────────────────────

    [Fact]
    public void Management_Domain_ShouldNotDependOnInfrastructure()
    {
        Types.InAssembly(ManagementAssembly)
            .That().ResideInNamespaceStartingWith("NeoParking.Management.Domain")
            .ShouldNot().HaveDependencyOn("NeoParking.Management.Infrastructure")
            .GetResult().IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Management_Domain_ShouldNotDependOnApplication()
    {
        Types.InAssembly(ManagementAssembly)
            .That().ResideInNamespaceStartingWith("NeoParking.Management.Domain")
            .ShouldNot().HaveDependencyOn("NeoParking.Management.Application")
            .GetResult().IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Management_Domain_ShouldNotHaveExternalDependencies()
    {
        Types.InAssembly(ManagementAssembly)
            .That().ResideInNamespaceStartingWith("NeoParking.Management.Domain")
            .ShouldNot().HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "BCrypt")
            .GetResult().IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Management_Application_ShouldNotDependOnInfrastructure()
    {
        Types.InAssembly(ManagementAssembly)
            .That().ResideInNamespaceStartingWith("NeoParking.Management.Application")
            .ShouldNot().HaveDependencyOn("NeoParking.Management.Infrastructure")
            .GetResult().IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Management_Application_ShouldNotHaveExternalDependencies()
    {
        Types.InAssembly(ManagementAssembly)
            .That().ResideInNamespaceStartingWith("NeoParking.Management.Application")
            .ShouldNot().HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "BCrypt")
            .GetResult().IsSuccessful.Should().BeTrue();
    }
}
