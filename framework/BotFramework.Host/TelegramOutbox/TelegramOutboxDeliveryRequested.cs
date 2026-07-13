using BotFramework.Host.Contracts.Telegram;

namespace BotFramework.Host.TelegramOutbox;

/// <summary>Durable command for the Telegram BFF to deliver one outbox row.</summary>
public sealed record TelegramOutboxDeliveryRequested(
    long OutboxId,
    long ChatId,
    string Text,
    OutboundParseMode ParseMode);
