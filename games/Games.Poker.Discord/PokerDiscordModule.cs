using BotFramework.Discord;
using BotFramework.Discord.Commands;
using BotFramework.Discord.Interactions;
using BotFramework.Discord.Routing;
using Discord;
using Discord.WebSocket;
using Games.Poker.Application.Services;

namespace Games.Poker.Discord;

public static class PokerDiscordModule
{
    public static IServiceCollection AddPokerDiscord(this IServiceCollection services) => services
        .AddScoped<IDiscordMessageHandler, PokerDiscordHandler>()
        .AddScoped<IDiscordInteractionHandler, PokerDiscordInteractionHandler>();
}
