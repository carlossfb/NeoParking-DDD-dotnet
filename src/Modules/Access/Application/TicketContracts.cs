namespace NeoParking.Access.Application;

using NeoParking.Access.Domain;

// ── requests ─────────────────────────────────────────────────────────────────

public sealed record RegisterEntryRequest(string Plate, Guid? ClientId = null);

public sealed record RegisterExitRequest(DateTime? ExitAt = null);

/// <summary>
/// Filtro de listagem de tickets — visão operacional (quem está no pátio, histórico de
/// entradas). Todos os campos são opcionais. Page é 1-based.
/// </summary>
public sealed record ListTicketsRequest(
    TicketStatus? Status = null,
    string? Plate = null,
    DateTime? EntryFrom = null,
    DateTime? EntryTo = null,
    int Page = 1,
    int PageSize = 20);

// ── responses ────────────────────────────────────────────────────────────────

public sealed record TicketResponseDTO(
    Guid Id,
    string Plate,
    Guid? ClientId,
    DateTime EntryAt,
    DateTime? ExitAt,
    string Status);
