using BotFramework.Discord.Commands;using BotFramework.Discord.Routing;using Games.Bowling.Application.Services;
namespace Games.Bowling.Discord;
public static class BowlingDiscordModule{public static IServiceCollection AddBowlingDiscord(this IServiceCollection s)=>s.AddScoped<IDiscordMessageHandler,BowlingDiscordHandler>();}
