namespace NeoParking.Shared.Kernel.Exceptions;

/// <summary>
/// O evento não pode ser processado agora, mas será quando uma condição externa mudar.
/// O outbox continua tentando com backoff e nunca manda essa mensagem para dead-letter.
/// </summary>
public sealed class RetryLaterException : DomainException
{
    public RetryLaterException(string message) : base(message) { }
}
