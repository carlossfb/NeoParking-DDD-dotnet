namespace NeoParking.Integration.Tests.Outbox;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoParking.Access.Infrastructure;
using NeoParking.Billing.Infrastructure;
using NeoParking.Integration.Tests.Support;
using NeoParking.Shared.Kernel.Outbox;

[Collection(MySqlCollection.Name)]
[Trait("Category", "Integration")]
public sealed class OutboxProcessorTests
{
    private static readonly OutboxRetryPolicy NoDelay = new(3, TimeSpan.Zero, TimeSpan.Zero);

    private readonly MySqlFixture _mysql;

    public OutboxProcessorTests(MySqlFixture mysql) => _mysql = mysql;

    private static OutboxMessage Probe(string name) => OutboxMessage.Create(new ProbeEvent(name, Guid.NewGuid()));

    [Fact]
    public async Task Success_ShouldDeliverAndMarkAsPublished()
    {
        await using var host = await NewHostAsync();
        var message = Probe("ok");
        await host.AddToAccessOutboxAsync(message);

        await host.AccessOutbox().ProcessOnceAsync();

        host.Probes.Delivered.Should().ContainSingle().Which.Should().Be("ok");
        (await host.ReloadFromAccessOutboxAsync(message.Id)).IsPublished.Should().BeTrue();
    }

    [Fact]
    public async Task Failure_ShouldRecordAttemptAndScheduleBackoff()
    {
        await using var host = await NewHostAsync();
        host.Probes.Failing["bad"] = 0;
        var message = Probe("bad");
        await host.AddToAccessOutboxAsync(message);

        await host.AccessOutbox().ProcessOnceAsync();
        await host.AccessOutbox().ProcessOnceAsync();

        var saved = await host.ReloadFromAccessOutboxAsync(message.Id);
        saved.IsPublished.Should().BeFalse();
        saved.AttemptCount.Should().Be(1, "the second cycle runs before the backoff expires");
        saved.LastError.Should().Contain("Probe 'bad' failed");
        saved.NextAttemptAt.Should().BeAfter(DateTime.UtcNow);
        saved.DeadLetteredAt.Should().BeNull();
    }

    [Fact]
    public async Task FailingMessage_ShouldNotBlockTheOthersInTheBatch()
    {
        await using var host = await NewHostAsync();
        host.Probes.Failing["bad"] = 0;
        var bad = Probe("bad");
        var good = Probe("good");
        await host.AddToAccessOutboxAsync(bad);
        await host.AddToAccessOutboxAsync(good);

        await host.AccessOutbox().ProcessOnceAsync();

        (await host.ReloadFromAccessOutboxAsync(good.Id)).IsPublished.Should().BeTrue();
        (await host.ReloadFromAccessOutboxAsync(bad.Id)).IsPublished.Should().BeFalse();
    }

    [Fact]
    public async Task FailedWrite_ShouldNotLeakIntoTheNextMessage()
    {
        await using var host = await NewHostAsync();
        // Placa maior que a coluna: o SaveChanges do handler falha e deixa a entidade rastreada.
        var poison = OutboxMessage.Create(new DirtyWriteEvent(new string('X', 40), Guid.NewGuid()));
        var valid = OutboxMessage.Create(new DirtyWriteEvent("ABC1234", Guid.NewGuid()));
        await host.AddToAccessOutboxAsync(poison);
        await host.AddToAccessOutboxAsync(valid);

        await host.AccessOutbox().ProcessOnceAsync();

        (await host.ReloadFromAccessOutboxAsync(valid.Id)).IsPublished.Should().BeTrue();
        (await host.ReloadFromAccessOutboxAsync(poison.Id)).IsPublished.Should().BeFalse();
        var plates = await host.UseAsync<BillingDbContext, List<string>>(c =>
            c.Tickets.AsNoTracking().Select(t => t.Plate).ToListAsync());
        plates.Should().ContainSingle().Which.Should().Be("ABC1234");
    }

    [Fact]
    public async Task ValidMessage_BehindAFullBatchOfFailures_ShouldBePublishedOnTheNextCycle()
    {
        await using var host = await NewHostAsync();
        var failing = Enumerable.Range(0, 50).Select(i => Probe($"bad-{i}")).ToArray();
        foreach (var name in Enumerable.Range(0, 50).Select(i => $"bad-{i}"))
            host.Probes.Failing[name] = 0;
        var valid = Probe("valid");
        await host.AddToAccessOutboxAsync(failing);
        await host.AddToAccessOutboxAsync(valid);

        var outbox = host.AccessOutbox();
        await outbox.ProcessOnceAsync();
        await outbox.ProcessOnceAsync();

        (await host.ReloadFromAccessOutboxAsync(valid.Id)).IsPublished.Should().BeTrue();
    }

    [Fact]
    public async Task ExhaustedMessage_ShouldBeDeadLettered_AndNoLongerAttempted()
    {
        await using var host = await NewHostAsync();
        host.Probes.Failing["bad"] = 0;
        var message = Probe("bad");
        await host.AddToAccessOutboxAsync(message);
        var outbox = host.AccessOutbox(NoDelay);

        for (var cycle = 0; cycle < NoDelay.MaxAttempts + 2; cycle++)
            await outbox.ProcessOnceAsync();

        var saved = await host.ReloadFromAccessOutboxAsync(message.Id);
        saved.DeadLetteredAt.Should().NotBeNull();
        saved.LastError.Should().Contain("Probe 'bad' failed");
        saved.AttemptCount.Should().Be(NoDelay.MaxAttempts);
        host.Probes.Attempted.Should().HaveCount(NoDelay.MaxAttempts);
    }

    [Fact]
    public async Task DocumentedReplay_ShouldPublishADeadLetteredMessageAgain()
    {
        var databases = await _mysql.CreateDatabasesAsync();
        await using var host = await ModuleHost.CreateMigratedAsync(databases);
        host.Probes.Failing["bad"] = 0;
        var message = Probe("bad");
        await host.AddToAccessOutboxAsync(message);
        var outbox = host.AccessOutbox(NoDelay);
        for (var cycle = 0; cycle < NoDelay.MaxAttempts; cycle++)
            await outbox.ProcessOnceAsync();
        (await host.ReloadFromAccessOutboxAsync(message.Id)).DeadLetteredAt.Should().NotBeNull();

        host.Probes.Failing.Clear();
        await _mysql.ExecuteAsync(await RepositoryScripts.ReadAsync("outbox-replay.sql", databases));
        await outbox.ProcessOnceAsync();

        var saved = await host.ReloadFromAccessOutboxAsync(message.Id);
        saved.IsPublished.Should().BeTrue();
        saved.DeadLetteredAt.Should().BeNull();
        host.Probes.Delivered.Should().ContainSingle().Which.Should().Be("bad");
    }

    [Fact]
    public async Task GetPending_ShouldSkipScheduledMessages()
    {
        await using var host = await NewHostAsync();
        host.Probes.Failing["scheduled"] = 0;
        var scheduled = Probe("scheduled");
        var ready = Probe("ready");
        await host.AddToAccessOutboxAsync(scheduled);
        await host.AccessOutbox().ProcessOnceAsync();
        await host.AddToAccessOutboxAsync(ready);

        var pending = await host.UseAsync<OutboxRepository<AccessDbContext>, IReadOnlyList<OutboxMessage>>(
            r => r.GetPendingAsync());

        pending.Should().ContainSingle().Which.Id.Should().Be(ready.Id);
    }

    private async Task<ModuleHost> NewHostAsync() =>
        await ModuleHost.CreateMigratedAsync(await _mysql.CreateDatabasesAsync());
}
