using BotFramework.Host.Contracts.Telegram;

namespace BotFramework.Host.TelegramOutbox;

public sealed record TelegramOutboxRow(
    long Id,
    long ChatId,
    string Text,
    OutboundParseMode ParseMode,
    int Attempts);
