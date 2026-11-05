namespace NeoParking.Access.Domain;

using NeoParking.Shared.Kernel.Exceptions;

/// <summary>
/// CPF ou CNPJ, conforme o tipo do cliente. As mensagens de erro e o ToString nunca trazem o valor completo.
/// </summary>
public sealed record Document
{
    private const int CpfLength = 11;
    private const int CnpjLength = 14;

    public ClientType Type { get; }
    public string Value { get; }

    private Document(ClientType type, string value)
    {
        Type = type;
        Value = value;
    }

    public static Document Create(ClientType type, string? raw)
    {
        var normalized = Normalize(raw);

        return new Document(type, type == ClientType.Company
            ? ValidateCnpj(normalized)
            : ValidateCpf(normalized));
    }

    /// <summary>Exemplos: <c>***.456.789-**</c> e <c>**.***.345/01DE-**</c>.</summary>
    public string Masked => Type == ClientType.Company
        ? $"**.***.{Value.Substring(5, 3)}/{Value.Substring(8, 4)}-**"
        : $"***.{Value.Substring(3, 3)}.{Value.Substring(6, 3)}-**";

    public override string ToString() => Masked;

    private static string Normalize(string? raw) =>
        new string((raw ?? "").Where(c => c is not ('.' or '-' or '/') && !char.IsWhiteSpace(c)).ToArray())
            .ToUpperInvariant();

    private static string ValidateCpf(string digits)
    {
        if (digits.Length == 0)
            throw new DomainException("CPF cannot be empty");

        if (digits.Length != CpfLength || !digits.All(char.IsAsciiDigit))
            throw new DomainException("CPF must contain 11 digits");

        if (digits.All(d => d == digits[0]))
            throw new DomainException("Invalid CPF");

        var numbers = digits.Select(d => d - '0').ToList();

        var firstDigit = CheckDigit(numbers.Take(9).Select((n, i) => n * (10 - i)).Sum());
        var secondDigit = CheckDigit(numbers.Take(10).Select((n, i) => n * (11 - i)).Sum());

        if (numbers[9] != firstDigit || numbers[10] != secondDigit)
            throw new DomainException("Invalid CPF");

        return digits;
    }

    /// <summary>
    /// Algoritmo do CNPJ alfanumérico da Receita Federal: o valor de cada caractere é ASCII − 48
    /// (A = 17 ... Z = 42). Os dígitos verificadores continuam numéricos, e o CNPJ numérico é um caso particular.
    /// </summary>
    private static string ValidateCnpj(string value)
    {
        if (value.Length == 0)
            throw new DomainException("CNPJ cannot be empty");

        if (value.Length != CnpjLength)
            throw new DomainException("CNPJ must contain 14 characters");

        if (!value.Take(12).All(c => char.IsAsciiDigit(c) || char.IsAsciiLetterUpper(c))
            || !value.Skip(12).All(char.IsAsciiDigit)
            || value.All(c => c == value[0]))
            throw new DomainException("Invalid CNPJ");

        var firstDigit = CheckDigit(WeightedSum(value, 12));
        var secondDigit = CheckDigit(WeightedSum(value, 13));

        if (value[12] - '0' != firstDigit || value[13] - '0' != secondDigit)
            throw new DomainException("Invalid CNPJ");

        return value;
    }

    // Módulo 11: da direita para a esquerda, os pesos vão de 2 a 9 e recomeçam.
    private static int WeightedSum(string value, int length) =>
        value.Take(length).Select((c, i) => (c - 48) * (2 + (length - 1 - i) % 8)).Sum();

    private static int CheckDigit(int sum)
    {
        var rest = sum % 11;
        return rest < 2 ? 0 : 11 - rest;
    }
}
