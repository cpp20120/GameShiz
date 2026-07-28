using BotFramework.Discord.Commands;using BotFramework.Discord.Routing;using Games.Football.Application.Services;
namespace Games.Football.Discord;
public static class FootballDiscordModule{public static IServiceCollection AddFootballDiscord(this IServiceCollection s)=>s.AddScoped<IDiscordMessageHandler,FootballDiscordHandler>();}
