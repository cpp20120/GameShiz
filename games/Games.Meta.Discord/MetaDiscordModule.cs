using BotFramework.Discord.Commands;using BotFramework.Discord.Routing;using Games.Meta.Application.Meta;
namespace Games.Meta.Discord;
public static class MetaDiscordModule{public static IServiceCollection AddMetaDiscord(this IServiceCollection s)=>s.AddScoped<IDiscordMessageHandler,MetaDiscordHandler>();}
