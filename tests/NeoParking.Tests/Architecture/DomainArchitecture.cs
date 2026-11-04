namespace NeoParking.Tests.Architecture;

using FluentAssertions;
using NetArchTest.Rules;
using System.Reflection;

/// <summary>
/// Regras de camadas e de fronteira aplicadas igualmente a todos os Bounded Contexts. Como cada
/// contexto é um único assembly, estas regras são a única proteção das camadas: o compilador
/// não impede o Domain de usar um pacote referenciado pelo módulo.
/// </summary>
[Trait("Category", "Architecture")]
public class DomainArchitectureTests
{
    private const string SharedKernel = "NeoParking.Shared.Kernel";

    // Biblioteca pura de parsing/validação, sem I/O nem framework. Reimplementar as regras de
    // telefone seria pior do que depender dela, e o uso se limita ao VO PhoneNumber.
    private const string PhoneNumberLibrary = "PhoneNumbers";

    private static readonly string[] PersistenceAndCryptoLibraries =
        ["Microsoft.EntityFrameworkCore", "Pomelo", "MySqlConnector", "BCrypt"];

    private static readonly string[] AspNetLibraries = ["Microsoft.AspNetCore"];

    private const string MediatR = "MediatR";

    private static readonly Dictionary<string, BoundedContext> BoundedContexts = new[]
    {
        new BoundedContext("Access", typeof(NeoParking.Access.Domain.Client).Assembly, [PhoneNumberLibrary]),
        new BoundedContext("Billing", typeof(NeoParking.Billing.Domain.PricingPolicy).Assembly, []),
        new BoundedContext("Management", typeof(NeoParking.Management.Domain.OperatorUser).Assembly, []),
    }.ToDictionary(context => context.Name);

    public static TheoryData<string> ContextNames()
    {
        var names = new TheoryData<string>();
        foreach (var name in BoundedContexts.Keys)
            names.Add(name);
        return names;
    }

    // ── direção das dependências ─────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(ContextNames))]
    public void Domain_ShouldNotDependOnInfrastructure(string contextName)
    {
        var context = BoundedContexts[contextName];

        AssertNoViolations(context.DomainTypes(), types => types.ShouldNot().HaveDependencyOn(context.Infrastructure));
    }

    [Theory]
    [MemberData(nameof(ContextNames))]
    public void Domain_ShouldNotDependOnApplication(string contextName)
    {
        var context = BoundedContexts[contextName];

        AssertNoViolations(context.DomainTypes(), types => types.ShouldNot().HaveDependencyOn(context.Application));
    }

    [Theory]
    [MemberData(nameof(ContextNames))]
    public void Application_ShouldNotDependOnInfrastructure(string contextName)
    {
        var context = BoundedContexts[contextName];

        AssertNoViolations(context.ApplicationTypes(), types => types.ShouldNot().HaveDependencyOn(context.Infrastructure));
    }

    // ── dependências externas ────────────────────────────────────────────────

    /// <summary>
    /// Lista de permissão, e não de proibição: qualquer pacote novo no Domain falha até ser
    /// discutido. A única exceção hoje é <see cref="PhoneNumberLibrary"/>, no Access.
    /// </summary>
    [Theory]
    [MemberData(nameof(ContextNames))]
    public void Domain_ShouldOnlyDependOnAllowedLibraries(string contextName)
    {
        var context = BoundedContexts[contextName];
        string[] allowed = ["System", context.Domain, SharedKernel, .. context.AllowedDomainLibraries];

        AssertNoViolations(context.DomainTypes(), types => types.Should().OnlyHaveDependenciesOn(allowed));
    }

    [Theory]
    [MemberData(nameof(ContextNames))]
    public void Domain_ShouldNotDependOnMediatR(string contextName)
    {
        var context = BoundedContexts[contextName];

        AssertNoViolations(context.DomainTypes(), types => types.ShouldNot().HaveDependencyOn(MediatR));
    }

    [Theory]
    [MemberData(nameof(ContextNames))]
    public void Domain_ShouldNotDependOnAspNet(string contextName)
    {
        var context = BoundedContexts[contextName];

        AssertNoViolations(context.DomainTypes(), types => types.ShouldNot().HaveDependencyOnAny(AspNetLibraries));
    }

    [Theory]
    [MemberData(nameof(ContextNames))]
    public void Application_ShouldNotDependOnAspNet(string contextName)
    {
        var context = BoundedContexts[contextName];

        AssertNoViolations(context.ApplicationTypes(), types => types.ShouldNot().HaveDependencyOnAny(AspNetLibraries));
    }

    [Theory]
    [MemberData(nameof(ContextNames))]
    public void Application_ShouldNotDependOnPersistenceOrCryptoLibraries(string contextName)
    {
        var context = BoundedContexts[contextName];

        AssertNoViolations(
            context.ApplicationTypes(),
            types => types.ShouldNot().HaveDependencyOnAny(PersistenceAndCryptoLibraries));
    }

    /// <summary>
    /// Handlers MediatR são permitidos na Application porque o OutboxProcessor do Kernel entrega
    /// os eventos de integração via IPublisher. A exceção vale só para o namespace EventHandlers:
    /// serviços e contratos continuam sem conhecer o MediatR.
    /// </summary>
    [Theory]
    [MemberData(nameof(ContextNames))]
    public void Application_ShouldOnlyUseMediatRInEventHandlers(string contextName)
    {
        var context = BoundedContexts[contextName];
        var typesOutsideEventHandlers = context.Types()
            .That().ResideInNamespaceStartingWith(context.Application)
            .And().DoNotResideInNamespaceStartingWith($"{context.Application}.EventHandlers");

        AssertNoViolations(typesOutsideEventHandlers, types => types.ShouldNot().HaveDependencyOn(MediatR));
    }

    // ── fronteiras entre contextos ───────────────────────────────────────────

    [Theory]
    [MemberData(nameof(ContextNames))]
    public void BoundedContext_ShouldNotDependOnOtherContexts(string contextName)
    {
        var context = BoundedContexts[contextName];
        var otherContexts = BoundedContexts.Values
            .Where(other => other.Name != contextName)
            .Select(other => other.Root)
            .ToArray();

        var allTypes = context.Types().That().ResideInNamespaceStartingWith(context.Root);

        AssertNoViolations(allTypes, types => types.ShouldNot().HaveDependencyOnAny(otherContexts));
    }

    // ── portas e adaptadores ─────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(ContextNames))]
    public void ConcreteRepositories_ShouldImplementPortOfTheirContext(string contextName)
    {
        var context = BoundedContexts[contextName];

        var repositories = context.Types()
            .That().ResideInNamespaceStartingWith(context.Infrastructure)
            .And().AreClasses()
            .And().AreNotAbstract()
            .And().HaveNameEndingWith("Repository")
            .GetTypes()
            .ToList();

        repositories.Should().NotBeEmpty("a regra precisa encontrar repositórios concretos para ter valor");

        var withoutPort = repositories
            .Where(repository => !repository.GetInterfaces().Any(context.IsPort))
            .Select(repository => repository.FullName)
            .ToList();

        withoutPort.Should().BeEmpty("todo repositório concreto deve implementar uma porta de Domain ou Application");
    }

    private static void AssertNoViolations(PredicateList selectedTypes, Func<PredicateList, ConditionList> rule)
    {
        selectedTypes.GetTypes().Should().NotBeEmpty("uma regra sem tipos selecionados passaria sem verificar nada");

        var result = rule(selectedTypes).GetResult();

        (result.FailingTypeNames ?? []).Should().BeEmpty("estes tipos violam a regra de arquitetura");
        result.IsSuccessful.Should().BeTrue();
    }

    private sealed record BoundedContext(string Name, Assembly Assembly, string[] AllowedDomainLibraries)
    {
        public string Root => $"NeoParking.{Name}";
        public string Domain => $"{Root}.Domain";
        public string Application => $"{Root}.Application";
        public string Infrastructure => $"{Root}.Infrastructure";

        public Types Types() => NetArchTest.Rules.Types.InAssembly(Assembly);

        public PredicateList DomainTypes() => Types().That().ResideInNamespaceStartingWith(Domain);

        public PredicateList ApplicationTypes() => Types().That().ResideInNamespaceStartingWith(Application);

        public bool IsPort(Type candidate) =>
            candidate.Namespace is { } ns && (ns.StartsWith(Domain) || ns.StartsWith(Application));
    }
}
