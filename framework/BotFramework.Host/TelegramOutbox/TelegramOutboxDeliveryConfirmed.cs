namespace BotFramework.Host.TelegramOutbox;

/// <summary>Telegram BFF acknowledgement after Telegram accepted an outbox message.</summary>
public sealed record TelegramOutboxDeliveryConfirmed(long OutboxId, int TelegramMessageId);
