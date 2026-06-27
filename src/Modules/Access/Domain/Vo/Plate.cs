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

        var normalized = raw.Trim().ToUpperInvariant().Replace("-", "");

        if (!MercosulPattern.IsMatch(normalized) && !LegacyPattern.IsMatch(normalized))
            throw new DomainException($"Invalid plate format: '{raw}'");

        return new Plate(normalized);
    }

    public override string ToString() => Value;
}