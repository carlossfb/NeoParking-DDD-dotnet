namespace NeoParking.Billing.Application;

using NeoParking.Billing.Domain;

public interface ITicketRepository
{
    Task AddAsync(Ticket ticket);
    Task UpdateAsync(Ticket ticket);
    Task<Ticket?> GetByIdAsync(Guid id);

    /// <summary>Tickets já encerrados (com Fee calculada) num período — histórico do que foi efetivamente cobrado.</summary>
    Task<IReadOnlyList<Ticket>> GetFeeHistoryAsync(DateTime? from = null, DateTime? to = null);
}
