namespace NeoParking.Access.Domain;

using System.Security.Cryptography;
using System.Text.RegularExpressions;
using NeoParking.Shared.Kernel.Exceptions;

/// <summary>
/// Código opaco impresso ou mostrado ao motorista: 80 bits de fonte criptográfica em hexadecimal,
/// para não ser adivinhado a partir de outro ticket. A migration AddTicketCode usa o mesmo formato.
/// </summary>
public sealed record TicketCode
{
    public const int Length = 20;
    private const int RandomBytes = 10;

    private static readonly Regex Pattern = new($"^[0-9A-F]{{{Length}}}$", RegexOptions.Compiled);

    public string Value { get; }

    private TicketCode(string value) => Value = value;

    public static TicketCode Generate() =>
        new(Convert.ToHexString(RandomNumberGenerator.GetBytes(RandomBytes)));

    public static TicketCode Create(string raw) =>
        TryParse(raw, out var code) ? code! : throw new DomainException("Invalid ticket code.");

    public static bool TryParse(string? raw, out TicketCode? code)
    {
        var normalized = raw?.Trim().ToUpperInvariant();
        code = normalized is not null && Pattern.IsMatch(normalized) ? new TicketCode(normalized) : null;
        return code is not null;
    }

    public override string ToString() => Value;
}
