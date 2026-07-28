using BotFramework.Discord.Commands;using BotFramework.Discord.Routing;using Games.Transfer.Application.Services;
namespace Games.Transfer.Discord;
public static class TransferDiscordModule{public static IServiceCollection AddTransferDiscord(this IServiceCollection s)=>s.AddScoped<IDiscordMessageHandler,TransferDiscordHandler>();}
