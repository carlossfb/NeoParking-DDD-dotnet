namespace NeoParking.Access.Domain;

using System.Text.RegularExpressions;
using NeoParking.Shared.Kernel.Exceptions;

public sealed record Plate
{
    private static readonly Regex MercosulPattern = new(@"^[A-Z]{3}[0-9][A-Z][0-9]{2}$", RegexOptions.Compiled);
    private static readonly Regex LegacyPattern   = new(@"^[A-Z]{3}[0-9]{4}$",           RegexOptions.Compiled);

    public string Value { get; }

    private Plate(string value) => Value = value;

    public static Plate Create(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new DomainException("Plate cannot be empty");

        var normalized = Normalize(raw);

        if (!MercosulPattern.IsMatch(normalized) && !LegacyPattern.IsMatch(normalized))
            throw new DomainException("Invalid plate format.");

        return new Plate(normalized);
    }

    /// <summary>
    /// Normalização textual (trim/uppercase/sem hífen) SEM validar o formato — Create()
    /// continua sendo o único jeito de obter uma Plate válida de fato. Isto existe à parte
    /// porque a busca de tickets por placa (ver TicketService.ListAsync) precisa normalizar
    /// entradas parciais do usuário (ex.: "abc" pra um Contains), que não são uma placa
    /// completa e válida — Create() rejeitaria antes de chegar na query. Internal porque só
    /// a Infrastructure (mesmo assembly) precisa disso.
    /// </summary>
    internal static string Normalize(string raw) => raw.Trim().ToUpperInvariant().Replace("-", "");

    public override string ToString() => Value;
}