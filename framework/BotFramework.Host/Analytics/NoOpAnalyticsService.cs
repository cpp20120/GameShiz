using BotFramework.Host.Contracts.Analytics;

namespace BotFramework.Host.Analytics;

/// <summary>
/// Keeps edge transports usable when analytics storage belongs to a backend
/// service and is not configured in the transport process.
/// </summary>
public sealed class NoOpAnalyticsService : IAnalyticsService
{
    public void Track(string moduleId, string eventName, IReadOnlyDictionary<string, object?> tags)
    {
    }
}
