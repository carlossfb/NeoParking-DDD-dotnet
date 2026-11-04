namespace NeoParking.Shared.Kernel.Outbox;

/// <summary>Backoff exponencial com teto e limite de tentativas antes do dead-letter.</summary>
public sealed record OutboxRetryPolicy(int MaxAttempts, TimeSpan BaseDelay, TimeSpan MaxDelay)
{
    /// <summary>10s, 20s, 40s... até 15 min entre tentativas; dead-letter na 10ª falha (cerca de 52 min).</summary>
    public static OutboxRetryPolicy Default { get; } =
        new(MaxAttempts: 10, BaseDelay: TimeSpan.FromSeconds(10), MaxDelay: TimeSpan.FromMinutes(15));

    public TimeSpan DelayFor(int attempt)
    {
        // Expoente limitado: a partir daí o teto já venceu, e 2^n não estoura o TimeSpan.
        var factor = Math.Pow(2, Math.Clamp(attempt - 1, 0, 30));
        var delay = TimeSpan.FromTicks((long)Math.Min(BaseDelay.Ticks * factor, MaxDelay.Ticks));
        return delay;
    }
}
