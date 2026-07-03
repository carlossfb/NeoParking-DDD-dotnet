namespace NeoParking.Billing.Application;

using NeoParking.Billing.Domain;
using NeoParking.Shared.Kernel.Events;
using NeoParking.Shared.Kernel.Exceptions;

/// <summary>
/// Orquestra o acompanhamento da cobrança de um Ticket. Diferente do que o nome poderia
/// sugerir, este serviço NÃO abre nem fecha tickets por iniciativa própria — quem registra
/// entrada e saída na cancela é o Access BC. Este serviço reage aos eventos de integração
/// publicados pelo Access (ver <see cref="HandleTicketOpenedAsync"/> e
/// <see cref="HandleTicketClosedAsync"/>) e calcula a Fee correspondente, sempre usando a
/// PricingPolicy vigente no momento da saída — a Fee resultante é congelada no próprio
/// Ticket (ver <see cref="Ticket.Close"/>).
/// </summary>
public sealed class TicketService
{
    private readonly ITicketRepository _tickets;
    private readonly IPricingPolicyRepository _pricingPolicies;
    private readonly IUnitOfWork _unitOfWork;

    public TicketService(
        ITicketRepository tickets,
        IPricingPolicyRepository pricingPolicies,
        IUnitOfWork unitOfWork)
    {
        _tickets = tickets;
        _pricingPolicies = pricingPolicies;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Reage ao <see cref="TicketOpenedIntegrationEvent"/> publicado pelo Access, criando a
    /// representação local do ticket para acompanhamento da cobrança. Idempotente: se o
    /// ticket já existe (reentrega do outbox), não faz nada.
    /// </summary>
    public async Task HandleTicketOpenedAsync(TicketOpenedIntegrationEvent @event)
    {
        var existing = await _tickets.GetByIdAsync(@event.TicketId);
        if (existing is not null)
            return;

        var ticket = Ticket.OpenFromAccess(@event.TicketId, @event.Plate, @event.EntryAt);

        await _tickets.AddAsync(ticket);
        await _unitOfWork.SaveChangesAsync();
    }

    /// <summary>
    /// Reage ao <see cref="TicketClosedIntegrationEvent"/> publicado pelo Access, calculando
    /// e congelando a Fee com a PricingPolicy vigente no momento da saída. Idempotente: se o
    /// ticket já está fechado (reentrega do outbox), não recalcula.
    /// </summary>
    public async Task HandleTicketClosedAsync(TicketClosedIntegrationEvent @event)
    {
        var ticket = await _tickets.GetByIdAsync(@event.TicketId)
            ?? throw new NotFoundException(nameof(Ticket), @event.TicketId);

        if (ticket.Status == TicketStatus.Closed)
            return;

        var pricingPolicy = await _pricingPolicies.GetEffectiveAtAsync(@event.ExitAt)
            ?? throw new DomainException("No pricing policy is defined. The Owner must define one before charging fees.");

        ticket.Close(@event.ExitAt, pricingPolicy);

        await _tickets.UpdateAsync(ticket);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<TicketResponseDTO> GetByIdAsync(Guid ticketId)
    {
        var ticket = await _tickets.GetByIdAsync(ticketId)
            ?? throw new NotFoundException(nameof(Ticket), ticketId);

        return ToResponse(ticket);
    }

    /// <summary>
    /// Histórico de cobranças realizadas num período — a resposta direta para
    /// "quanto estava sendo cobrado" ao longo do tempo, ticket por ticket, com o
    /// valor efetivamente congelado em cada fechamento (não recalculado).
    /// </summary>
    public async Task<IReadOnlyList<TicketResponseDTO>> GetFeeHistoryAsync(DateTime? from = null, DateTime? to = null)
    {
        var tickets = await _tickets.GetFeeHistoryAsync(from, to);
        return tickets.Select(ToResponse).ToList();
    }

    private static TicketResponseDTO ToResponse(Ticket t)
    {
        var fee = t.ChargedFee is null
            ? null
            : new FeeResponseDTO(
                t.ChargedFee.PricingPolicyId,
                t.ChargedFee.MinimumFee.Value,
                t.ChargedFee.HourlyRate.Value,
                t.ChargedFee.BilledHours,
                t.ChargedFee.Amount.Value,
                t.ChargedFee.CalculatedAt);

        return new TicketResponseDTO(t.Id, t.Plate, t.EntryAt, t.ExitAt, t.Status.ToString(), fee);
    }
}
