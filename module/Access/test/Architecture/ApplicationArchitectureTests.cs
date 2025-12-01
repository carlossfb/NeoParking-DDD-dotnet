using FluentAssertions;
using NetArchTest.Rules;
using System.Reflection;

namespace Access.Tests.Architecture;

public class ApplicationArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(main.domain.entity.Client).Assembly;

    [Fact]
    public void Application_ShouldNotDependOnInfrastructure()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespaceStartingWith("main.application")
            .ShouldNot().HaveDependencyOn("main.infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue("Application should not depend on Infrastructure");
    }

    [Fact]
    public void ApplicationServices_ShouldEndWithServiceImpl()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespace("main.application.service")
            .Should().HaveNameEndingWith("ServiceImpl")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void DTOs_ShouldEndWithDTO()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespace("main.common.dto")
            .Should().HaveNameEndingWith("DTO")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}