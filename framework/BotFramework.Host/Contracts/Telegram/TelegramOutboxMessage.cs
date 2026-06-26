using Telegram.Bot.Types.Enums;

namespace BotFramework.Host.Contracts.Telegram;

public sealed record TelegramOutboxMessage(
    long ChatId,
    string Text,
    string? DedupeKey = null,
    ParseMode ParseMode = ParseMode.None);
