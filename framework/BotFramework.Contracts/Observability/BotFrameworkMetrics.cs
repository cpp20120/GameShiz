using System.Diagnostics.Metrics;

namespace BotFramework.Contracts.Observability;

/// <summary>
/// Product-neutral instruments shared by framework hosts and adapters.
/// Tenant, scope and player identifiers are intentionally not metric labels.
/// </summary>
public static class BotFrameworkMetrics
{
    public const string RequestsMeterName = "BotFramework.Requests";
    public const string RateLimitingMeterName = "BotFramework.RateLimiting";
    public const string GameExecutionMeterName = "BotFramework.GameExecution";
    public const string OutboxMeterName = "BotFramework.Outbox";
    public const string ProvisioningMeterName = "BotFramework.Provisioning";

    private static readonly Meter RequestsMeter = new(RequestsMeterName);
    private static readonly Meter RateLimitingMeter = new(RateLimitingMeterName);
    private static readonly Meter GameMeter = new(GameExecutionMeterName);
    private static readonly Meter OutboxMeter = new(OutboxMeterName);
    private static readonly Meter ProvisioningMeter = new(ProvisioningMeterName);
    private static int rateLimitFallbackActive;
    private static long outboxDepth;

    public static readonly Counter<long> Requests =
        RequestsMeter.CreateCounter<long>("botframework_requests");

    public static readonly Counter<long> RequestErrors =
        RequestsMeter.CreateCounter<long>("botframework_request_errors");

    public static readonly Histogram<double> RequestDuration =
        RequestsMeter.CreateHistogram<double>("botframework_request_duration_seconds", "s");

    public static readonly Counter<long> GameFailures =
        GameMeter.CreateCounter<long>("game.command.failures");

    public static readonly Histogram<double> GameDuration =
        GameMeter.CreateHistogram<double>("game.command.execution.duration", "s");

    public static readonly Histogram<double> OutboxLag =
        OutboxMeter.CreateHistogram<double>("game.outbox.delivery.lag", "s");

    public static readonly ObservableGauge<long> OutboxDepth =
        OutboxMeter.CreateObservableGauge("botframework_outbox_depth", static () => Volatile.Read(ref outboxDepth));

    public static readonly ObservableGauge<long> QueueBacklog =
        OutboxMeter.CreateObservableGauge("botframework_queue_backlog", static () => Volatile.Read(ref outboxDepth));

    public static readonly Counter<long> TenantProvisioningAttempts =
        ProvisioningMeter.CreateCounter<long>("botframework_tenant_provisioning_attempts");

    public static readonly Counter<long> TenantProvisioningFailures =
        ProvisioningMeter.CreateCounter<long>("botframework_tenant_provisioning_failures");

    public static readonly Histogram<double> TenantProvisioningDuration =
        ProvisioningMeter.CreateHistogram<double>("botframework_tenant_provisioning_duration_seconds", "s");

    public static readonly Counter<long> RateLimitDecisions =
        RateLimitingMeter.CreateCounter<long>("botframework_rate_limit_decisions");

    public static readonly Counter<long> RateLimitPolicyCacheHits =
        RateLimitingMeter.CreateCounter<long>("botframework_rate_limit_policy_cache_hits");

    public static readonly Counter<long> RateLimitPolicyCacheMisses =
        RateLimitingMeter.CreateCounter<long>("botframework_rate_limit_policy_cache_misses");

    public static readonly Counter<long> RateLimitPolicyCacheInvalidations =
        RateLimitingMeter.CreateCounter<long>("botframework_rate_limit_policy_cache_invalidations");

    public static readonly ObservableGauge<int> RateLimitFallback =
        RateLimitingMeter.CreateObservableGauge(
            "botframework_rate_limit_fallback_active",
            static () => Volatile.Read(ref rateLimitFallbackActive));

    public static void SetRateLimitFallback(bool active) =>
        Volatile.Write(ref rateLimitFallbackActive, active ? 1 : 0);

    public static void SetOutboxDepth(long depth) =>
        Volatile.Write(ref outboxDepth, Math.Max(0, depth));
}
