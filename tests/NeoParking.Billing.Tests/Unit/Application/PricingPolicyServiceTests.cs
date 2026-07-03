namespace NeoParking.Billing.Tests.Unit.Application;

using FluentAssertions;
using Moq;
using NeoParking.Billing.Application;
using NeoParking.Billing.Domain;
using NeoParking.Billing.Domain.Vo;
using NeoParking.Shared.Kernel.Events;
using NeoParking.Shared.Kernel.Exceptions;
using NeoParking.Shared.Kernel.Observability;
using NeoParking.Shared.Kernel.Outbox;

public class PricingPolicyServiceTests
{
    private readonly Mock<IPricingPolicyRepository> _mockRepository = new();
    private readonly Mock<IBillingOutboxRepository> _mockOutbox = new();
    private readonly Mock<IUnitOfWork> _mockUnitOfWork = new();
    private readonly Mock<ICorrelationIdProvider> _mockCorrelation = new();
    private readonly PricingPolicyService _service;

    private static readonly Guid OwnerId = Guid.NewGuid();

    public PricingPolicyServiceTests()
    {
        _mockCorrelation.Setup(c => c.GetCorrelationId()).Returns(Guid.NewGuid());
        _mockOutbox.Setup(o => o.AddAsync(It.IsAny<OutboxMessage>())).Returns(Task.CompletedTask);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(default)).Returns(Task.CompletedTask);
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<PricingPolicy>())).Returns(Task.CompletedTask);
        _mockRepository.Setup(r => r.GetHistoryAsync()).ReturnsAsync(new List<PricingPolicy>());

        _service = new PricingPolicyService(
            _mockRepository.Object,
            _mockOutbox.Object,
            _mockUnitOfWork.Object,
            _mockCorrelation.Object);
    }

    private static DefinePricingPolicyRequest MakeRequest(
        decimal minimumFee = 10m, decimal hourlyRate = 4m,
        int coverageMinutes = 60, int graceMinutes = 0)
        => new(OwnerId, minimumFee, hourlyRate, coverageMinutes, graceMinutes);

    [Fact]
    public async Task DefineAsync_WithNoActivePolicy_ShouldCreateNewActivePolicy()
    {
        _mockRepository.Setup(r => r.GetActiveAsync()).ReturnsAsync((PricingPolicy?)null);

        var result = await _service.DefineAsync(MakeRequest());

        result.IsActive.Should().BeTrue();
        result.MinimumFee.Should().Be(10m);
        result.HourlyRate.Should().Be(4m);
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<PricingPolicy>()), Times.Once);
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<PricingPolicy>()), Times.Never);
    }

    [Fact]
    public async Task DefineAsync_WithActivePolicy_ShouldCloseCurrentBeforeCreatingNew()
    {
        var current = PricingPolicy.Define(OwnerId, Money.Create(5m), Money.Create(2m),
            60, 0, DateTime.UtcNow.AddDays(-10));
        _mockRepository.Setup(r => r.GetActiveAsync()).ReturnsAsync(current);
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<PricingPolicy>())).Returns(Task.CompletedTask);

        await _service.DefineAsync(MakeRequest(minimumFee: 20m, hourlyRate: 6m));

        current.IsActive.Should().BeFalse();
        _mockRepository.Verify(r => r.UpdateAsync(current), Times.Once);
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<PricingPolicy>()), Times.Once);
    }

    [Fact]
    public async Task DefineAsync_ShouldEnqueueOutboxMessage()
    {
        _mockRepository.Setup(r => r.GetActiveAsync()).ReturnsAsync((PricingPolicy?)null);

        await _service.DefineAsync(MakeRequest());

        _mockOutbox.Verify(o => o.AddAsync(It.IsAny<OutboxMessage>()), Times.Once);
    }

    [Fact]
    public async Task DefineAsync_ShouldCommitUnitOfWorkOnce()
    {
        _mockRepository.Setup(r => r.GetActiveAsync()).ReturnsAsync((PricingPolicy?)null);

        await _service.DefineAsync(MakeRequest());

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task DefineAsync_AsFirstPolicyEver_ShouldBackdateEffectiveFromRetroactively()
    {
        _mockRepository.Setup(r => r.GetActiveAsync()).ReturnsAsync((PricingPolicy?)null);
        _mockRepository.Setup(r => r.GetHistoryAsync()).ReturnsAsync(new List<PricingPolicy>());

        var result = await _service.DefineAsync(MakeRequest());

        // Precisa cobrir qualquer ticket fechado no Access antes de existir qualquer política —
        // por isso o ano é muito anterior a "agora", não apenas alguns segundos/minutos atrás.
        result.EffectiveFrom.Year.Should().Be(1000);
    }

    [Fact]
    public async Task DefineAsync_WithNoActiveButNonEmptyHistory_ShouldNotBackdate()
    {
        // Não deveria acontecer no fluxo normal (toda política nova fecha a anterior), mas se
        // acontecer, só a primeiríssima política (histórico vazio) pode ser retroativa.
        var olderClosed = PricingPolicy.Reconstitute(Guid.NewGuid(), OwnerId, Money.Create(5m), Money.Create(2m),
            60, 0, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(-10));

        _mockRepository.Setup(r => r.GetActiveAsync()).ReturnsAsync((PricingPolicy?)null);
        _mockRepository.Setup(r => r.GetHistoryAsync()).ReturnsAsync(new List<PricingPolicy> { olderClosed });

        var result = await _service.DefineAsync(MakeRequest());

        result.EffectiveFrom.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetActiveAsync_WithNoActivePolicy_ShouldThrowNotFoundException()
    {
        _mockRepository.Setup(r => r.GetActiveAsync()).ReturnsAsync((PricingPolicy?)null);

        var act = async () => await _service.GetActiveAsync();

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetEffectiveAtAsync_WithNoPolicyAtMoment_ShouldThrowNotFoundException()
    {
        _mockRepository.Setup(r => r.GetEffectiveAtAsync(It.IsAny<DateTime>())).ReturnsAsync((PricingPolicy?)null);

        var act = async () => await _service.GetEffectiveAtAsync(DateTime.UtcNow);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetHistoryAsync_ShouldReturnAllRevisionsMapped()
    {
        var older = PricingPolicy.Reconstitute(Guid.NewGuid(), OwnerId, Money.Create(5m), Money.Create(2m),
            60, 0, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(-10));
        var newer = PricingPolicy.Reconstitute(Guid.NewGuid(), OwnerId, Money.Create(10m), Money.Create(4m),
            60, 0, DateTime.UtcNow.AddDays(-10), null);

        _mockRepository.Setup(r => r.GetHistoryAsync())
            .ReturnsAsync(new List<PricingPolicy> { older, newer });

        var result = await _service.GetHistoryAsync();

        result.Should().HaveCount(2);
        result[0].IsActive.Should().BeFalse();
        result[1].IsActive.Should().BeTrue();
    }
}
