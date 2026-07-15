using Discord;

namespace BotFramework.Discord;

public sealed class DiscordOptions
{
    public const string SectionName = "Discord";

    public bool Enabled { get; init; }
    public string Token { get; init; } = string.Empty;
    public string CommandPrefix { get; init; } = "!";
    public bool RegisterApplicationCommands { get; init; } = true;
    public ulong? ApplicationCommandsGuildId { get; init; }
    public string DefaultCulture { get; init; } = "ru";
    public IReadOnlyList<ulong> AdminUserIds { get; init; } = [];
    public IReadOnlyList<ulong> AdminRoleIds { get; init; } = [];
    public long AdminActorId { get; init; }
    public int RateLimitWindowSeconds { get; init; } = 10;
    public int RateLimitMaxRequests { get; init; } = 8;
    public int CommandCooldownMilliseconds { get; init; } = 900;
    public int InteractionCooldownMilliseconds { get; init; } = 500;
    public GatewayIntents GatewayIntents { get; init; } =
        GatewayIntents.Guilds |
        GatewayIntents.GuildMessages |
        GatewayIntents.DirectMessages |
        GatewayIntents.MessageContent;
}
