namespace BotFramework.Host;

public interface IAnalyticsService
{
    void Track(string moduleId, string eventName, IReadOnlyDictionary<string, object?> tags);
}
