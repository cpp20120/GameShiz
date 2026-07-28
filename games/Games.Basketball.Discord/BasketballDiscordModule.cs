using BotFramework.Discord.Commands;using BotFramework.Discord.Routing;using Games.Basketball.Application.Services;
namespace Games.Basketball.Discord;
public static class BasketballDiscordModule{public static IServiceCollection AddBasketballDiscord(this IServiceCollection s)=>s.AddScoped<IDiscordMessageHandler,BasketballDiscordHandler>();}
