using BotFramework.Discord.Commands;
using BotFramework.Discord.Routing;
using Games.Pick.Application.Services;

namespace Games.Pick.Discord;
public static class PickDiscordModule{public static IServiceCollection AddPickDiscord(this IServiceCollection s)=>s.AddScoped<IDiscordMessageHandler,PickDiscordHandler>();}
