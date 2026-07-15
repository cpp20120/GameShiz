namespace BotFramework.Contracts.Transport;

/// <summary>Stable cursor request shared by list endpoints and generated clients.</summary>
public readonly record struct CursorPageRequest(string? Cursor = null, int Limit = 50)
{
    public CursorPageRequest Normalize()
    {
        if (Limit is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(Limit), "Limit must be between 1 and 100.");

        return this with { Cursor = string.IsNullOrWhiteSpace(Cursor) ? null : Cursor.Trim() };
    }
}

/// <summary>Stable cursor response shared by list endpoints and generated clients.</summary>
public sealed record CursorPage<T>(
    IReadOnlyList<T> Items,
    string? NextCursor,
    bool HasMore);

public static class BotFrameworkTransportHeaders
{
    public const string CorrelationId = "X-Correlation-ID";
    public const string RequestId = "X-Request-ID";
    public const string IdempotencyKey = "Idempotency-Key";
    public const string RetryAfter = "Retry-After";
}
