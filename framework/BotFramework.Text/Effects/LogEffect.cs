namespace BotFramework.Text;

public sealed record LogEffect(
    string EventName,
    IReadOnlyDictionary<string, object?> Properties) : IMessageEffect
{
    public string Kind => "log";

    public LogEffect(string eventName)
        : this(eventName, new Dictionary<string, object?>(StringComparer.Ordinal))
    {
    }
}
