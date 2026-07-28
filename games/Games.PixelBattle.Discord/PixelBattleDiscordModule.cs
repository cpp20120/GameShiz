using BotFramework.Discord.Commands;
using BotFramework.Discord.Routing;
using Games.PixelBattle.Contracts;

namespace Games.PixelBattle.Discord;

public static class PixelBattleDiscordModule
{
    public static IServiceCollection AddPixelBattleDiscord(this IServiceCollection services) =>
        services.AddScoped<IDiscordMessageHandler, PixelBattleDiscordHandler>();
}
