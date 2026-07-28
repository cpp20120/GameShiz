namespace BotFramework.Contracts.Transport;

public static class BotFrameworkTransportHeaders
{
    public const string CorrelationId = "X-Correlation-ID";
    public const string RequestId = "X-Request-ID";
    public const string IdempotencyKey = "Idempotency-Key";
    public const string RetryAfter = "Retry-After";
}
