namespace BotFramework.Host.TelegramOutbox;

/// <summary>Chooses who delivers durable Telegram outbox rows.</summary>
public sealed class TelegramOutboxTransportOptions
{
    public const string SectionName = "TelegramOutbox";

    /// <summary><c>Local</c> for the monolith, <c>Cap</c> for Backend → Telegram BFF delivery.</summary>
    public string Transport { get; init; } = "Local";

    public bool UsesCap => string.Equals(Transport, "Cap", StringComparison.OrdinalIgnoreCase);
}
