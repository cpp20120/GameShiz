namespace BotFramework.Host.Contracts.Analytics;

public interface IAnalyticsService
{
    void Track(string moduleId, string eventName, IReadOnlyDictionary<string, object?> tags);
}
