namespace BotFramework.Contracts.RateLimiting;

public sealed record RateLimitPolicy(
    int Capacity,
    double RefillPerSecond)
{
    public static RateLimitPolicy PerMinute(int permits) => new(permits, permits / 60d);

    public static RateLimitPolicy PerWindow(int permits, TimeSpan window) =>
        new(permits, permits / window.TotalSeconds);
}
