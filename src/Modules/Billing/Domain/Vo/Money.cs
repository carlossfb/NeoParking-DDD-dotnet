namespace NeoParking.Billing.Domain.Vo;

using NeoParking.Shared.Kernel.Exceptions;

/// <summary>
/// Value object para valores monetários. Sempre arredondado para 2 casas decimais.
/// Usado tanto para os componentes da PricingPolicy (taxa mínima, valor/hora)
/// quanto para o valor final calculado de uma Fee.
/// </summary>
public sealed record Money
{
    public decimal Value { get; }

    private Money(decimal amount) => Value = amount;

    public static Money Create(decimal amount)
    {
        if (amount < 0)
            throw new DomainException("Amount cannot be negative.");

        return new Money(decimal.Round(amount, 2, MidpointRounding.AwayFromZero));
    }

    public static Money Zero { get; } = new(0m);

    public static Money operator +(Money left, Money right) => new(left.Value + right.Value);

    public static Money operator *(Money money, int factor)
    {
        if (factor < 0)
            throw new DomainException("Multiplication factor cannot be negative.");

        return new Money(money.Value * factor);
    }

    public static bool operator >(Money left, Money right) => left.Value > right.Value;

    public static bool operator <(Money left, Money right) => left.Value < right.Value;

    public override string ToString() => Value.ToString("F2");
}
