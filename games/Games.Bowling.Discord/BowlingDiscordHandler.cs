using System.Security.Cryptography;
using BotFramework.Discord.Commands;
using BotFramework.Discord.Routing;
using Games.Discord.Shared;
using Games.Bowling.Application.Services;
using Games.Bowling.Domain.Results;
using Microsoft.Extensions.DependencyInjection;

namespace Games.Bowling.Discord;

public sealed class BowlingDiscordHandler(IBowlingService service) : IDiscordMessageHandler
{
    public bool CanHandle(DiscordMessageContext context) =>
        DiscordCommand.TryParse(context, out var command) && command.Is("bowling", "bowl");

    public async Task HandleAsync(DiscordMessageContext context)
    {
        if (!DiscordCommand.TryParse(context, out var command) || !command.TryGetPositiveInt(0, out var amount))
        {
            await context.ReplyAsync("Usage: `bowling <amount>`");
            return;
        }

        var userId = context.UserId();
        var scopeId = context.ScopeId();
        var sourceId = context.SourceMessageId();
        var result = await service.PlaceBetAsync(userId, context.DisplayName(), scopeId, amount, sourceId, context.CancellationToken);
        if (result.Error != BowlingBetError.None)
        {
            await context.ReplyAsync(DiscordGameFormatting.BetError("Bowling", result.Error, result.Balance, result.PendingAmount, result.BlockingGameId, 0));
            return;
        }

        var face = RandomNumberGenerator.GetInt32(1, 7);
        var settled = await service.RollAsync(userId, context.DisplayName(), scopeId, face, sourceId, context.CancellationToken);
        await context.ReplyAsync(DiscordGameFormatting.NativeResult("🎳", settled.Face, settled.Bet, settled.Multiplier, settled.Payout, settled.Balance, settled.DailyRollUsed, settled.DailyRollLimit));
    }
}

public static class BowlingDiscordServiceCollectionExtensions
{
    public static IServiceCollection AddBowlingDiscord(this IServiceCollection services) =>
        services.AddScoped<IDiscordMessageHandler, BowlingDiscordHandler>();
}
