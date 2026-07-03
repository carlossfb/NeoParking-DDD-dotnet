namespace NeoParking.Billing.Application;

using NeoParking.Billing.Domain;
using NeoParking.Billing.Domain.Vo;
using NeoParking.Shared.Kernel.Events;
using NeoParking.Shared.Kernel.Exceptions;
using NeoParking.Shared.Kernel.Observability;
using NeoParking.Shared.Kernel.Outbox;

/// <summary>
/// Orquestra a definição de tarifação pelo Owner (taxa mínima + valor por hora) e a
/// consulta ao histórico de revisões.
///
/// Cada chamada a <see cref="DefineAsync"/> encerra a revisão vigente e cria uma NOVA —
/// nunca edita uma política existente. Esse é o mecanismo que sustenta a rastreabilidade
/// histórica de "quanto estava sendo cobrado" em qualquer data passada: a resposta não
/// vem de recalcular com a tarifa de hoje, vem de consultar qual revisão estava vigente
/// (ou qual Fee já foi congelada em cada ticket — ver <see cref="TicketService"/>).
/// </summary>
public sealed class PricingPolicyService
{
    private readonly IPricingPolicyRepository _repository;
    private readonly IBillingOutboxRepository _outbox;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICorrelationIdProvider _correlationIdProvider;

    public PricingPolicyService(
        IPricingPolicyRepository repository,
        IBillingOutboxRepository outbox,
        IUnitOfWork unitOfWork,
        ICorrelationIdProvider correlationIdProvider)
    {
        _repository = repository;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
        _correlationIdProvider = correlationIdProvider;
    }

    /// <summary>
    /// Sentinela usada como EffectiveFrom só da primeiríssima política que o Owner define
    /// (quando não existe nenhuma política ativa nem no histórico). Sem isso, qualquer ticket
    /// fechado no Access antes do Owner configurar a tarifação fica travado pra sempre — o
    /// evento de fechamento fica reentregando no outbox porque GetEffectiveAtAsync nunca acha
    /// nada pra um instante anterior à criação da política. A primeira política, por não ter
    /// nenhuma revisão anterior com quem conflitar, pode assumir vigência retroativa com segurança.
    /// Revisões seguintes continuam começando em DateTime.UtcNow normalmente.
    /// </summary>
    private static readonly DateTime RetroactiveEffectiveFrom = new(1000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public async Task<PricingPolicyResponseDTO> DefineAsync(DefinePricingPolicyRequest request)
    {
        var now = DateTime.UtcNow;

        var current = await _repository.GetActiveAsync();
        if (current is not null)
        {
            current.Close(now);
            await _repository.UpdateAsync(current);
        }

        var isFirstPolicyEver = current is null && (await _repository.GetHistoryAsync()).Count == 0;
        var effectiveFrom = isFirstPolicyEver ? RetroactiveEffectiveFrom : now;

        var policy = PricingPolicy.Define(
            request.OwnerId,
            Money.Create(request.MinimumFee),
            Money.Create(request.HourlyRate),
            request.MinimumFeeCoverageMinutes,
            request.GracePeriodMinutes,
            effectiveFrom);

        // Ambos os Add antes do SaveChanges — atomicidade garantida (mesmo padrão do Access)
        await _repository.AddAsync(policy);
        await _outbox.AddAsync(OutboxMessage.Create(new PricingPolicyChangedIntegrationEvent(
            PricingPolicyId: policy.Id,
            OwnerId:         policy.OwnerId,
            MinimumFee:      policy.MinimumFee.Value,
            HourlyRate:      policy.HourlyRate.Value,
            EffectiveFrom:   policy.EffectiveFrom,
            CorrelationId:   _correlationIdProvider.GetCorrelationId())));

        await _unitOfWork.SaveChangesAsync();

        return ToResponse(policy);
    }

    public async Task<PricingPolicyResponseDTO> GetActiveAsync()
    {
        var policy = await _repository.GetActiveAsync()
            ?? throw new NotFoundException("No active pricing policy has been defined yet.");

        return ToResponse(policy);
    }

    public async Task<PricingPolicyResponseDTO> GetEffectiveAtAsync(DateTime moment)
    {
        var policy = await _repository.GetEffectiveAtAsync(moment)
            ?? throw new NotFoundException($"No pricing policy was effective at '{moment:O}'.");

        return ToResponse(policy);
    }

    /// <summary>Histórico completo de revisões de tarifação — o que o Owner cobrava, e desde quando.</summary>
    public async Task<IReadOnlyList<PricingPolicyResponseDTO>> GetHistoryAsync()
    {
        var history = await _repository.GetHistoryAsync();
        return history.Select(ToResponse).ToList();
    }

    private static PricingPolicyResponseDTO ToResponse(PricingPolicy p)
        => new(p.Id, p.OwnerId, p.MinimumFee.Value, p.HourlyRate.Value,
               p.MinimumFeeCoverageMinutes, p.GracePeriodMinutes, p.EffectiveFrom, p.EffectiveTo, p.IsActive);
}
