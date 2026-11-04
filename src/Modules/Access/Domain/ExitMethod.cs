namespace NeoParking.Access.Domain;

/// <summary>Como a saída foi identificada. Nulo em tickets encerrados antes deste registro existir.</summary>
public enum ExitMethod
{
    TicketId,
    Code,
    Plate
}
