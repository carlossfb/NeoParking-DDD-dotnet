namespace NeoParking.Billing.Domain;

using NeoParking.Billing.Domain.Vo;
using NeoParking.Shared.Kernel.Exceptions;

/// <summary>
/// Serviço de domínio responsável por aplicar a regra de negócio de tarifação.
/// Mantido como serviço de domínio — e não como método de <see cref="PricingPolicy"/> ou
/// <see cref="Ticket"/> — porque combina duas entidades (a política vigente e a
/// duração da permanência) sem pertencer naturalmente a nenhuma das duas isoladamente.
///
/// Regra aplicada:
///   1. Permanência dentro do período de carência (<see cref="PricingPolicy.GracePeriodMinutes"/>)
///      → cobrança zero (cortesia).
///   2. Fora da carência → aplica-se a taxa mínima (<see cref="PricingPolicy.MinimumFee"/>), que
///      cobre os primeiros <see cref="PricingPolicy.MinimumFeeCoverageMinutes"/> minutos de permanência.
///   3. Cada hora (ou fração) que exceder esse período é cobrada pelo valor por hora
///      (<see cref="PricingPolicy.HourlyRate"/>), sempre arredondada para cima —
///      hora iniciada é hora cobrada, prática padrão em estacionamentos.
/// </summary>
public static class FeeCalculator
{
    public static Fee Calculate(PricingPolicy pricingPolicy, DateTime entryAt, DateTime exitAt)
    {
        if (pricingPolicy is null)
            throw new DomainException("A pricing policy is required to calculate a fee.");

        if (exitAt <= entryAt)
            throw new DomainException("Exit time must be after entry time.");

        var totalMinutes = (exitAt - entryAt).TotalMinutes;

        // Dentro da carência: cortesia, nenhuma cobrança.
        if (totalMinutes <= pricingPolicy.GracePeriodMinutes)
        {
            return Fee.Create(
                pricingPolicy.Id,
                pricingPolicy.MinimumFee,
                pricingPolicy.HourlyRate,
                billedHours: 0,
                amount: Money.Zero,
                calculatedAt: exitAt);
        }

        var extraMinutes = Math.Max(0, totalMinutes - pricingPolicy.MinimumFeeCoverageMinutes);
        var extraHours   = (int)Math.Ceiling(extraMinutes / 60.0);

        var amount = pricingPolicy.MinimumFee + (pricingPolicy.HourlyRate * extraHours);

        return Fee.Create(
            pricingPolicy.Id,
            pricingPolicy.MinimumFee,
            pricingPolicy.HourlyRate,
            billedHours: extraHours,
            amount: amount,
            calculatedAt: exitAt);
    }
}
