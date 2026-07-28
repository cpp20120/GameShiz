using BotFramework.Discord;
using BotFramework.Discord.Commands;
using BotFramework.Discord.Interactions;
using BotFramework.Discord.Routing;
using Discord;
using Discord.WebSocket;
using Games.Blackjack.Contracts;

namespace Games.Blackjack.Discord;

public static class BlackjackDiscordModule
{
    public static IServiceCollection AddBlackjackDiscord(this IServiceCollection services) => services
        .AddScoped<IDiscordMessageHandler, BlackjackDiscordHandler>()
        .AddScoped<IDiscordInteractionHandler, BlackjackDiscordInteractionHandler>();
}
