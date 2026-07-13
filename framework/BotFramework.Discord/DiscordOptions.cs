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
    public List<ulong> AdminUserIds { get; init; } = [];
    public List<ulong> AdminRoleIds { get; init; } = [];
    public long AdminActorId { get; init; }
    public GatewayIntents GatewayIntents { get; init; } =
        GatewayIntents.Guilds |
        GatewayIntents.GuildMessages |
        GatewayIntents.DirectMessages |
        GatewayIntents.MessageContent;
}
