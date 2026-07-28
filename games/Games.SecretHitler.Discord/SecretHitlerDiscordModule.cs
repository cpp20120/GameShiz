using BotFramework.Discord;
using BotFramework.Discord.Commands;
using BotFramework.Discord.Interactions;
using BotFramework.Discord.Routing;
using Discord;
using Discord.WebSocket;
using Games.SecretHitler.Application.Services;
using Games.SecretHitler.Domain.Results;

namespace Games.SecretHitler.Discord;

public static class SecretHitlerDiscordModule
{
    public static IServiceCollection AddSecretHitlerDiscord(this IServiceCollection services) => services
        .AddScoped<IDiscordMessageHandler, SecretHitlerDiscordHandler>()
        .AddScoped<IDiscordInteractionHandler, SecretHitlerDiscordInteractionHandler>();
}
