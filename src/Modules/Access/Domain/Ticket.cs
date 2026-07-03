namespace NeoParking.Access.Domain;

using NeoParking.Shared.Kernel.Exceptions;
using NeoParking.Shared.Kernel.Primitives;

/// <summary>
/// Aggregate root do Access BC. Representa o "Ticket" na nomenclatura de domínio do
/// README — o registro de entrada e saída de um veículo na cancela.
///
/// Access é quem opera a cancela, então é Access quem sabe, em primeira mão, quando um
/// veículo entra e quando sai. Esse fato é publicado como integração
/// (<see cref="Shared.Kernel.Events.TicketOpenedIntegrationEvent"/> /
/// <see cref="Shared.Kernel.Events.TicketClosedIntegrationEvent"/>) para que o Billing BC
/// reaja calculando a Fee — Billing nunca decide, por conta própria, quando um veículo
/// entrou ou saiu.
/// </summary>
public sealed class Ticket : Entity
{
    public Plate Plate { get; private set; }

    /// <summary>Cliente identificado na entrada, se houver (RFID/QR de um Client cadastrado). Nulo para avulsos da "zona livre".</summary>
    public Guid? ClientId { get; private set; }

    public DateTime EntryAt { get; private set; }
    public DateTime? ExitAt { get; private set; }
    public TicketStatus Status { get; private set; }

    // Construtor para EF Core
    private Ticket() : base(Guid.Empty)
    {
        Plate = null!;
    }

    private Ticket(Plate plate, Guid? clientId, DateTime entryAt) : base()
    {
        Plate    = plate;
        ClientId = clientId;
        EntryAt  = entryAt;
        Status   = TicketStatus.Open;
    }

    private Ticket(
        Guid id,
        Plate plate,
        Guid? clientId,
        DateTime entryAt,
        DateTime? exitAt,
        TicketStatus status) : base(id)
    {
        Plate    = plate;
        ClientId = clientId;
        EntryAt  = entryAt;
        ExitAt   = exitAt;
        Status   = status;
    }

    // ── factory methods ──────────────────────────────────────────────────────

    /// <summary>Registra a entrada de um veículo na cancela.</summary>
    public static Ticket RegisterEntry(Plate plate, Guid? clientId = null, DateTime? entryAt = null)
    {
        if (plate is null)
            throw new DomainException("Plate is required.");

        return new Ticket(plate, clientId, entryAt ?? DateTime.UtcNow);
    }

    /// <summary>Reconstitui a partir do banco sem revalidar nem gerar novo Id.</summary>
    public static Ticket Reconstitute(
        Guid id, Plate plate, Guid? clientId, DateTime entryAt, DateTime? exitAt, TicketStatus status)
        => new(id, plate, clientId, entryAt, exitAt, status);

    // ── comportamentos ───────────────────────────────────────────────────────

    /// <summary>Registra a saída do veículo na cancela.</summary>
    public void RegisterExit(DateTime? exitAt = null)
    {
        if (Status == TicketStatus.Closed)
            throw new DomainException("Ticket is already closed.");

        var resolvedExitAt = exitAt ?? DateTime.UtcNow;

        if (resolvedExitAt <= EntryAt)
            throw new DomainException("Exit time must be after entry time.");

        ExitAt = resolvedExitAt;
        Status = TicketStatus.Closed;
    }
}
