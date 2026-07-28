namespace BotFramework.Telegram.Abstractions.Tenancy;

public sealed record TelegramContainer(
    string ChatId,
    string UserId,
    string? TopicId = null,
    bool IsPrivateChat = false);