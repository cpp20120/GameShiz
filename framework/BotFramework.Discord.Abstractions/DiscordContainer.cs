namespace BotFramework.Discord.Abstractions;

public sealed record DiscordContainer(
    string GuildId,
    string ChannelId,
    string? ThreadId,
    string UserId,
    bool IsDirectMessage = false);
