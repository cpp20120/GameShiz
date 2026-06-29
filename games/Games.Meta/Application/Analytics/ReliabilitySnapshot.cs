namespace Games.Meta.Application.Analytics;

internal sealed record ReliabilitySnapshot(
    long UnresolvedFailures,
    long RetryingFailures,
    int MaxRetryCount,
    double OldestUnresolvedSeconds,
    long NewFailuresWindow,
    long ResolvedFailuresWindow,
    long IdempotencyFailuresWindow,
    long StuckIdempotencyOperations,
    long DueOutboxRows);
