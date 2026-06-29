namespace NeoParking.Access.Tests.Integration.Repository;

using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoParking.Access.Domain;
using NeoParking.Access.Infrastructure;
using Testcontainers.MySql;

public class MySqlFixture : IAsyncLifetime
{
    private readonly MySqlContainer _mysql = new MySqlBuilder()
        .WithDatabase("neoparking_test")
        .WithUsername("root")
        .WithPassword("password")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(3306))
        .Build();

    public string ConnectionString { get; private set; } = null!;
    public AccessDbContext Context { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _mysql.StartAsync();
        ConnectionString = _mysql.GetConnectionString();

        var options = new DbContextOptionsBuilder<AccessDbContext>()
            .UseMySql(ConnectionString, ServerVersion.AutoDetect(ConnectionString))
            .Options;

        Context = new AccessDbContext(options);
        await Context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await Context.DisposeAsync();
        await _mysql.DisposeAsync();
    }
}

public class ClientRepositoryTests : IClassFixture<MySqlFixture>
{
    private readonly MySqlFixture _fixture;

    public ClientRepositoryTests(MySqlFixture fixture)
    {
        _fixture = fixture;
        fixture.Context.Vehicles.RemoveRange(fixture.Context.Vehicles);
        fixture.Context.Clients.RemoveRange(fixture.Context.Clients);
        fixture.Context.SaveChanges();
    }

    private (AccessDbContext context, MySqlClientRepository repository) CreateFresh()
    {
        var options = new DbContextOptionsBuilder<AccessDbContext>()
            .UseMySql(_fixture.ConnectionString, ServerVersion.AutoDetect(_fixture.ConnectionString))
            .Options;

        var context = new AccessDbContext(options);
        return (context, new MySqlClientRepository(context));
    }

    // Helper: AddAsync não faz SaveChanges por design (atomicidade delegada ao chamador).
    // Nos testes de repositório isolado, chamamos SaveChangesAsync explicitamente,
    // espelhando o que o ClientService faz em produção.
    private static async Task AddAndSaveAsync(AccessDbContext ctx, MySqlClientRepository repo, Client client)
    {
        await repo.AddAsync(client);
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task AddAsync_WithValidClient_ShouldPersistClient()
    {
        var (ctx, repo) = CreateFresh();
        var client = Client.Create("João Silva", PhoneNumber.Create("+5511999999999"), Cpf.Create("11144477735"));

        await AddAndSaveAsync(ctx, repo, client);
        var result = await repo.GetByIdAsync(client.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("João Silva");
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetAllAsync_WithMultipleClients_ShouldReturnAll()
    {
        var (ctx, repo) = CreateFresh();
        await AddAndSaveAsync(ctx, repo, Client.Create("João", PhoneNumber.Create("+5511999999999"), Cpf.Create("11144477735")));
        await AddAndSaveAsync(ctx, repo, Client.Create("Maria", PhoneNumber.Create("+5521987654321"), Cpf.Create("98765432100")));

        var result = await repo.GetAllAsync();

        result.Should().HaveCount(2);
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task DeleteAsync_WithExistingClient_ShouldRemoveClient()
    {
        var (ctx1, repo1) = CreateFresh();
        var client = Client.Create("João", PhoneNumber.Create("+5511999999999"), Cpf.Create("11144477735"));
        await AddAndSaveAsync(ctx1, repo1, client);
        await ctx1.DisposeAsync();

        var (ctx2, repo2) = CreateFresh();
        await repo2.DeleteAsync((await repo2.GetByIdAsync(client.Id))!);
        await ctx2.DisposeAsync();

        var (ctx3, repo3) = CreateFresh();
        (await repo3.GetByIdAsync(client.Id)).Should().BeNull();
        await ctx3.DisposeAsync();
    }

    [Fact]
    public async Task UpdateAsync_WithNewVehicle_ShouldPersistVehicle()
    {
        var (ctx1, repo1) = CreateFresh();
        var client = Client.Create("João", PhoneNumber.Create("+5511999999999"), Cpf.Create("11144477735"));
        await AddAndSaveAsync(ctx1, repo1, client);
        await ctx1.DisposeAsync();

        var (ctx2, repo2) = CreateFresh();
        var saved = await repo2.GetByIdAsync(client.Id);
        saved!.RegisterVehicle(Plate.Create("ABC1D24"));
        await repo2.UpdateAsync(saved);
        await ctx2.DisposeAsync();

        var (ctx3, repo3) = CreateFresh();
        var result = await repo3.GetByIdAsync(client.Id);
        result!.Vehicles.Should().HaveCount(1);
        result.Vehicles[0].Plate.Value.Should().Be("ABC1D24");
        await ctx3.DisposeAsync();
    }

    [Fact]
    public async Task UpdateAsync_WithDuplicatePlate_ShouldThrowDomainException()
    {
        var (ctx1, repo1) = CreateFresh();
        var client = Client.Create("João", PhoneNumber.Create("+5511999999999"), Cpf.Create("11144477735"));
        await AddAndSaveAsync(ctx1, repo1, client);
        var saved = await repo1.GetByIdAsync(client.Id);
        saved!.RegisterVehicle(Plate.Create("ABC1234"));
        await repo1.UpdateAsync(saved);
        await ctx1.DisposeAsync();

        var (ctx2, repo2) = CreateFresh();
        var act = async () =>
        {
            var reloaded = await repo2.GetByIdAsync(client.Id);
            reloaded!.RegisterVehicle(Plate.Create("ABC1234"));
            await repo2.UpdateAsync(reloaded);
        };

        await act.Should().ThrowAsync<NeoParking.Shared.Kernel.Exceptions.DomainException>()
            .WithMessage("*ABC1234*");
        await ctx2.DisposeAsync();
    }

    [Fact]
    public async Task UpdateAsync_WithRemovedVehicle_ShouldDeleteVehicle()
    {
        var (ctx1, repo1) = CreateFresh();
        var client = Client.Create("João", PhoneNumber.Create("+5511999999999"), Cpf.Create("11144477735"));
        await AddAndSaveAsync(ctx1, repo1, client);
        var saved = await repo1.GetByIdAsync(client.Id);
        saved!.RegisterVehicle(Plate.Create("ABC1234"));
        await repo1.UpdateAsync(saved);
        await ctx1.DisposeAsync();

        var (ctx2, repo2) = CreateFresh();
        var withVehicle = await repo2.GetByIdAsync(client.Id);
        withVehicle!.RemoveVehicle(withVehicle.Vehicles[0].Id);
        await repo2.UpdateAsync(withVehicle);
        await ctx2.DisposeAsync();

        var (ctx3, repo3) = CreateFresh();
        var result = await repo3.GetByIdAsync(client.Id);
        result!.Vehicles.Should().BeEmpty();
        await ctx3.DisposeAsync();
    }

    [Fact]
    public async Task UpdateAsync_WithExistingVehicleAndNewVehicle_ShouldPersistNewVehicle()
    {
        var (ctx1, repo1) = CreateFresh();
        var client = Client.Create("João", PhoneNumber.Create("+5511999999999"), Cpf.Create("11144477735"));
        await AddAndSaveAsync(ctx1, repo1, client);
        var saved = await repo1.GetByIdAsync(client.Id);
        saved!.RegisterVehicle(Plate.Create("ABC1234"));
        await repo1.UpdateAsync(saved);
        await ctx1.DisposeAsync();

        var (ctx2, repo2) = CreateFresh();
        var withVehicle = await repo2.GetByIdAsync(client.Id);
        withVehicle!.RegisterVehicle(Plate.Create("ABC1D23"));
        await repo2.UpdateAsync(withVehicle);
        await ctx2.DisposeAsync();

        var (ctx3, repo3) = CreateFresh();
        var result = await repo3.GetByIdAsync(client.Id);
        result!.Vehicles.Should().HaveCount(2);
        await ctx3.DisposeAsync();
    }
}
