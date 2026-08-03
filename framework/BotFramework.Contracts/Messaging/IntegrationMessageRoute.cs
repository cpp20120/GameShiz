namespace BotFramework.Contracts.Messaging;

public sealed record IntegrationMessageRoute
{
    public IntegrationMessageRoute(string topic, string messageKey)
    {
        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentException("Integration topic is required.", nameof(topic));
        if (string.IsNullOrWhiteSpace(messageKey))
            throw new ArgumentException("Integration message key is required.", nameof(messageKey));

        Topic = topic;
        MessageKey = messageKey;
    }

    public string Topic { get; }

    public string MessageKey { get; }
}
