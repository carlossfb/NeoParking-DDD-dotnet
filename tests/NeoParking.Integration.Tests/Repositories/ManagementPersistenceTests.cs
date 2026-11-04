namespace NeoParking.Integration.Tests.Repositories;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoParking.Integration.Tests.Support;
using NeoParking.Management.Application;
using NeoParking.Management.Domain;
using NeoParking.Management.Domain.Vo;
using NeoParking.Management.Infrastructure;

[Collection(MySqlCollection.Name)]
[Trait("Category", "Integration")]
public sealed class ManagementPersistenceTests
{
    private const string Hash = "$2a$11$abcdefghijklmnopqrstuuABCDEFGHIJKLMNOPQRSTUVWXYZ01234";

    private readonly MySqlFixture _mysql;

    public ManagementPersistenceTests(MySqlFixture mysql) => _mysql = mysql;

    private async Task<ModuleHost> NewHostAsync() =>
        await ModuleHost.CreateMigratedAsync(await _mysql.CreateDatabasesAsync());

    private static OperatorUser NewOperator(OperatorRole role) =>
        OperatorUser.Create("Name", $"{Guid.NewGuid():N}@neoparking.test", PasswordHash.FromHash(Hash), role);

    [Fact]
    public async Task Operators_ShouldBeFoundByIdAndEmail()
    {
        await using var host = await NewHostAsync();
        var user = NewOperator(OperatorRole.Admin);
        await host.UseAsync<IOperatorRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            await repository.AddAsync(user);
            await unitOfWork.SaveChangesAsync();
        });

        await host.UseAsync<IOperatorRepository>(async repository =>
        {
            var byId = await repository.GetByIdAsync(user.Id);
            byId!.Email.Should().Be(user.Email);
            byId.Role.Should().Be(OperatorRole.Admin);
            byId.PasswordHash.Value.Should().Be(Hash);
            (await repository.GetByEmailAsync(user.Email))!.Id.Should().Be(user.Id);
            (await repository.GetByEmailAsync("nobody@neoparking.test")).Should().BeNull();
        });
    }

    [Fact]
    public async Task Update_ShouldPersistTheChange()
    {
        await using var host = await NewHostAsync();
        var user = NewOperator(OperatorRole.Operator);
        await host.UseAsync<IOperatorRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            await repository.AddAsync(user);
            await unitOfWork.SaveChangesAsync();
        });
        await host.UseAsync<IOperatorRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var saved = await repository.GetByIdAsync(user.Id);
            saved!.Deactivate();
            await repository.UpdateAsync(saved);
            await unitOfWork.SaveChangesAsync();
        });

        var reloaded = await host.UseAsync<IOperatorRepository, OperatorUser?>(r => r.GetByIdAsync(user.Id));
        reloaded!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsOwner_ShouldReflectTheOwnerRow()
    {
        await using var host = await NewHostAsync();
        (await host.UseAsync<IOperatorRepository, bool>(r => r.ExistsOwnerAsync())).Should().BeFalse();

        await host.UseAsync<IOperatorRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            await repository.AddAsync(NewOperator(OperatorRole.Owner));
            await unitOfWork.SaveChangesAsync();
        });

        (await host.UseAsync<IOperatorRepository, bool>(r => r.ExistsOwnerAsync())).Should().BeTrue();
    }

    [Fact]
    public async Task UnitOfWork_WhenAWriteFails_ShouldPersistNothing()
    {
        await using var host = await NewHostAsync();
        var taken = NewOperator(OperatorRole.Operator);
        await host.UseAsync<IOperatorRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            await repository.AddAsync(taken);
            await unitOfWork.SaveChangesAsync();
        });

        var act = () => host.UseAsync<IOperatorRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            await repository.AddAsync(NewOperator(OperatorRole.Operator));
            await repository.AddAsync(OperatorUser.Create(
                "Name", taken.Email, PasswordHash.FromHash(Hash), OperatorRole.Operator));
            await unitOfWork.SaveChangesAsync();
        });

        await act.Should().ThrowAsync<NeoParking.Shared.Kernel.Exceptions.ConflictException>();
        (await host.UseAsync<ManagementDbContext, int>(c => c.Operators.CountAsync())).Should().Be(1);
    }
}
