// Architecture/DomainArchitectureTests.cs
namespace NeoParking.Access.Tests.Architecture;

using NetArchTest.Rules;
using FluentAssertions;
using System.Reflection;

public class DomainArchitectureTests
{
    private static readonly Assembly AccessAssembly = typeof(NeoParking.Access.Domain.Client).Assembly;

    [Fact]
    public void Domain_ShouldNotDependOnInfrastructure()
    {
        var result = Types.InAssembly(AccessAssembly)
            .That().ResideInNamespaceStartingWith("NeoParking.Access.Domain")
            .ShouldNot().HaveDependencyOn("NeoParking.Access.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Domain_ShouldNotDependOnApplication()
    {
        var result = Types.InAssembly(AccessAssembly)
            .That().ResideInNamespaceStartingWith("NeoParking.Access.Domain")
            .ShouldNot().HaveDependencyOn("NeoParking.Access.Application")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Domain_ShouldNotHaveExternalDependencies()
    {
        var result = Types.InAssembly(AccessAssembly)
            .That().ResideInNamespaceStartingWith("NeoParking.Access.Domain")
            .ShouldNot().HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "MongoDB.Driver")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Application_ShouldNotDependOnInfrastructure()
    {
        var result = Types.InAssembly(AccessAssembly)
            .That().ResideInNamespaceStartingWith("NeoParking.Access.Application")
            .ShouldNot().HaveDependencyOn("NeoParking.Access.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Application_ShouldNotHaveExternalDependencies()
    {
        var result = Types.InAssembly(AccessAssembly)
            .That().ResideInNamespaceStartingWith("NeoParking.Access.Application")
            .ShouldNot().HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "MongoDB.Driver")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Repositories_ShouldImplementIClientRepository()
    {
        var result = Types.InAssembly(AccessAssembly)
            .That().HaveNameEndingWith("Repository")
            .And().AreClasses()
            .Should().ImplementInterface(typeof(NeoParking.Access.Domain.IClientRepository))
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}