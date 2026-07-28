using BotFramework.Discord.Commands;using BotFramework.Discord.Routing;using Games.Darts.Application.Services;
namespace Games.Darts.Discord;
public static class DartsDiscordModule{public static IServiceCollection AddDartsDiscord(this IServiceCollection s)=>s.AddScoped<IDiscordMessageHandler,DartsDiscordHandler>();}
