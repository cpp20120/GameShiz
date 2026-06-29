namespace Games.Meta.Application.Analytics;

internal sealed record DeliverySnapshot(
    long OutboxPending,
    long OutboxSentWindow,
    long OutboxFailed,
    int OutboxMaxAttempts,
    double OldestPendingSeconds,
    long UpdatesStartedWindow,
    long UpdatesCompletedWindow,
    long UpdatesFailedWindow,
    double UpdateLatencyAvgMs,
    double UpdateLatencyP95Ms,
    double DeliveryLatencyAvgMs,
    double DeliveryLatencyP95Ms);
