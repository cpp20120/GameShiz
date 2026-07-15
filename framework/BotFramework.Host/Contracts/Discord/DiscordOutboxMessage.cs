namespace BotFramework.Host.Contracts.Discord;

public sealed record DiscordOutboxMessage(
    long UserId,
    long ChannelId,
    string Text,
    string? Title = null,
    string? DedupeKey = null,
    string Culture = "ru");
