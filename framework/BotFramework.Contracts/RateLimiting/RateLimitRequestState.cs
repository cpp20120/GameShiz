namespace BotFramework.Contracts.RateLimiting;

/// <summary>
/// Request-scoped guard that prevents a transport lease and a nested command
/// lease from charging the same inbound operation twice.
/// </summary>
public sealed class RateLimitRequestState
{
    public bool LeaseGranted { get; set; }
}
