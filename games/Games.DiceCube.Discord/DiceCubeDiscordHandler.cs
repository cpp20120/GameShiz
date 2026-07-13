using System.Security.Cryptography;
using BotFramework.Discord.Commands;
using BotFramework.Discord.Routing;
using Games.Discord.Shared;
using Games.DiceCube.Application.Services;
using Games.DiceCube.Domain.Results;
using Microsoft.Extensions.DependencyInjection;

namespace Games.DiceCube.Discord;

public sealed class DiceCubeDiscordHandler(IDiceCubeService service) : IDiscordMessageHandler
{
    public bool CanHandle(DiscordMessageContext context) =>
        DiscordCommand.TryParse(context, out var command) && command.Is("dice", "cube");

    public async Task HandleAsync(DiscordMessageContext context)
    {
        if (!DiscordCommand.TryParse(context, out var command) || !command.TryGetPositiveInt(0, out var amount))
        {
            await context.ReplyAsync("Usage: `dice <amount>`");
            return;
        }

        var userId = context.UserId();
        var scopeId = context.ScopeId();
        var sourceId = context.SourceMessageId();
        var result = await service.PlaceBetAsync(userId, context.DisplayName(), scopeId, amount, sourceId, context.CancellationToken);
        if (result.Error != CubeBetError.None)
        {
            await context.ReplyAsync(DiscordGameFormatting.BetError("DiceCube", result.Error, result.Balance, result.PendingAmount, result.BlockingGameId, result.CooldownSeconds));
            return;
        }

        var face = RandomNumberGenerator.GetInt32(1, 7);
        var settled = await service.RollAsync(userId, context.DisplayName(), scopeId, face, sourceId, context.CancellationToken);
        await context.ReplyAsync(DiscordGameFormatting.NativeResult("🎲", settled.Face, settled.Bet, settled.Multiplier, settled.Payout, settled.Balance, settled.DailyRollUsed, settled.DailyRollLimit));
    }
}

public static class DiceCubeDiscordServiceCollectionExtensions
{
    public static IServiceCollection AddDiceCubeDiscord(this IServiceCollection services) =>
        services.AddScoped<IDiscordMessageHandler, DiceCubeDiscordHandler>();
}
