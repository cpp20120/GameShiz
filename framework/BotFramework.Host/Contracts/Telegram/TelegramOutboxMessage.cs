namespace BotFramework.Host.Contracts.Telegram;

public sealed record TelegramOutboxMessage(
    long ChatId,
    string Text,
    string? DedupeKey = null,
    OutboundParseMode ParseMode = OutboundParseMode.None);
