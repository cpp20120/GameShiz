using Telegram.Bot.Types.Enums;

namespace BotFramework.Host.TelegramOutbox;

public sealed record TelegramOutboxRow(
    long Id,
    long ChatId,
    string Text,
    ParseMode ParseMode,
    int Attempts);
