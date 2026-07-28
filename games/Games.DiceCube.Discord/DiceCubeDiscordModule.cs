using BotFramework.Discord.Commands;using BotFramework.Discord.Routing;using Games.DiceCube.Application.Services;
namespace Games.DiceCube.Discord;
public static class DiceCubeDiscordModule{public static IServiceCollection AddDiceCubeDiscord(this IServiceCollection s)=>s.AddScoped<IDiscordMessageHandler,DiceCubeDiscordHandler>();}
