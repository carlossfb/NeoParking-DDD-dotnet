using FluentAssertions;
using NetArchTest.Rules;
using System.Reflection;

namespace Access.Tests.Architecture;

public class InfrastructureArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(main.domain.entity.Client).Assembly;

    [Fact]
    public void Repositories_ShouldEndWithRepositoryImpl()
    {
        var repositoryTypes = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespaceStartingWith("main.infrastructure.persistence")
            .And().AreClasses()
            .And().AreNotAbstract()
            .GetTypes()
            .Where(t => t.Name.EndsWith("RepositoryImpl"));

        repositoryTypes.Should().NotBeEmpty();
    }

    [Fact]
    public void Repositories_ShouldImplementDomainInterfaces()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That().HaveNameEndingWith("RepositoryImpl")
            .Should().ImplementInterface(typeof(main.domain.ports.IClientRepository))
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void DbContext_ShouldEndWithDbContext()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespace("main.infrastructure")
            .And().Inherit(typeof(Microsoft.EntityFrameworkCore.DbContext))
            .Should().HaveNameEndingWith("DbContext")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}