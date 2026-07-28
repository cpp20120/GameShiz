using System.Collections.Concurrent;
using BotFramework.Discord;
using BotFramework.Discord.Commands;
using BotFramework.Discord.Interactions;
using BotFramework.Discord.Routing;
using Discord;
using Discord.WebSocket;
using Games.Redeem.Contracts;

namespace Games.Redeem.Discord;

public static class RedeemDiscordModule
{
    public static IServiceCollection AddRedeemDiscord(this IServiceCollection services) => services
        .AddScoped<IDiscordMessageHandler, RedeemDiscordHandler>()
        .AddScoped<IDiscordInteractionHandler, RedeemDiscordInteractionHandler>();
}
