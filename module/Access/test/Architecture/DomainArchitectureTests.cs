using FluentAssertions;
using NetArchTest.Rules;
using System.Reflection;

namespace Access.Tests.Architecture;

public class DomainArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(main.domain.entity.Client).Assembly;

    [Fact]
    public void Domain_ShouldNotDependOnApplication()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespaceStartingWith("main.domain")
            .ShouldNot().HaveDependencyOn("main.application")
            .GetResult();

        result.IsSuccessful.Should().BeTrue("Domain should not depend on Application layer");
    }

    [Fact]
    public void Domain_ShouldNotDependOnInfrastructure()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespaceStartingWith("main.domain")
            .ShouldNot().HaveDependencyOn("main.infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue("Domain should not depend on Infrastructure layer");
    }

    [Fact]
    public void ValueObjects_ShouldBeClasses()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespace("main.domain.vo")
            .Should().BeClasses()
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Entities_ShouldBeClasses()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespace("main.domain.entity")
            .Should().BeClasses()
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}