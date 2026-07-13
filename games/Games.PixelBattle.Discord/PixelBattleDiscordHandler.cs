using BotFramework.Discord.Commands;
using BotFramework.Discord.Routing;
using Games.PixelBattle.Contracts;

namespace Games.PixelBattle.Discord;

public sealed class PixelBattleDiscordHandler(IPixelBattleService service) : IDiscordMessageHandler
{
    public bool CanHandle(DiscordMessageContext context) => DiscordCommand.Is(context, "pixel", "pixelbattle");

    public async Task HandleAsync(DiscordMessageContext context)
    {
        var parts = DiscordCommand.Parts(context);
        if (parts.Length < 2)
        {
            await DiscordCommand.ReplyAsync(context, "`pixel grid` | `pixel set <index> <#RRGGBB>`");
            return;
        }

        switch (parts[1].ToLowerInvariant())
        {
            case "grid":
                await DiscordCommand.ReplyResultAsync(context, await service.GetGridAsync(context.CancellationToken), "PixelBattle");
                return;
            case "set" when parts.Length >= 4 && int.TryParse(parts[2], out var index):
                await DiscordCommand.ReplyResultAsync(context,
                    await service.UpdateAsync(DiscordCommand.UserId(context), index, parts[3], context.CancellationToken),
                    "PixelBattle");
                return;
            default:
                await DiscordCommand.ReplyAsync(context, "`pixel grid` | `pixel set <index> <#RRGGBB>`");
                return;
        }
    }
}

public static class PixelBattleDiscordModule
{
    public static IServiceCollection AddPixelBattleDiscord(this IServiceCollection services) =>
        services.AddScoped<IDiscordMessageHandler, PixelBattleDiscordHandler>();
}
