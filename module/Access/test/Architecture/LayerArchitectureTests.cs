using FluentAssertions;
using NetArchTest.Rules;
using System.Reflection;

namespace Access.Tests.Architecture;

public class LayerArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(main.domain.entity.Client).Assembly;

    [Fact]
    public void Domain_ShouldNotHaveExternalDependencies()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespace("main.domain")
            .ShouldNot().HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "MongoDB.Driver", "System.Data")
            .GetResult();

        result.IsSuccessful.Should().BeTrue("Domain should not have external dependencies");
    }

    [Fact]
    public void Application_ShouldOnlyDependOnDomain()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespace("main.application")
            .ShouldNot().HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "MongoDB.Driver")
            .GetResult();

        result.IsSuccessful.Should().BeTrue("Application should only depend on Domain");
    }

    [Fact]
    public void Infrastructure_ShouldExist()
    {
        var infrastructureTypes = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespaceStartingWith("main.infrastructure")
            .GetTypes();

        infrastructureTypes.Should().NotBeEmpty();
    }

    [Fact]
    public void AllClasses_ShouldBeInCorrectNamespace()
    {
        var domainTypes = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespace("main.domain")
            .GetTypes();

        var applicationTypes = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespace("main.application")
            .GetTypes();

        var infrastructureTypes = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespace("main.infrastructure")
            .GetTypes();

        (domainTypes.Count() + applicationTypes.Count() + infrastructureTypes.Count())
            .Should().BeGreaterThan(0);
    }
}