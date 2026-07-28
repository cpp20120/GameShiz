using BotFramework.Discord;
using BotFramework.Discord.Commands;
using BotFramework.Discord.Routing;
using Discord.WebSocket;
using Games.Admin.Application.Services;
using Microsoft.Extensions.Options;

namespace Games.Admin.Discord;

public static class AdminDiscordModule
{
    public static IServiceCollection AddAdminDiscord(this IServiceCollection services) =>
        services.AddScoped<IDiscordMessageHandler, AdminDiscordHandler>();
}
