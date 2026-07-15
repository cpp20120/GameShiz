namespace BotFramework.Host.DiscordOutbox;

public sealed record DiscordOutboxRow(
    long Id,
    long UserId,
    long ChannelId,
    string Text,
    string? Title,
    string Culture,
    int Attempts);
