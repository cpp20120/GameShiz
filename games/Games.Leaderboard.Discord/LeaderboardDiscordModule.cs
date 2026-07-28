using BotFramework.Discord.Commands;
using BotFramework.Discord.Interactions;
using BotFramework.Discord.Routing;
using Discord;
using Discord.WebSocket;
using Games.Leaderboard.Contracts;

namespace Games.Leaderboard.Discord;

public static class LeaderboardDiscordModule
{
    public static IServiceCollection AddLeaderboardDiscord(this IServiceCollection services) => services
        .AddScoped<IDiscordMessageHandler, LeaderboardDiscordHandler>()
        .AddScoped<IDiscordInteractionHandler, LeaderboardDiscordInteractionHandler>();
}
