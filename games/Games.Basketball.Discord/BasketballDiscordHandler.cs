using System.Security.Cryptography;
using BotFramework.Discord.Commands;
using BotFramework.Discord.Routing;
using Games.Discord.Shared;
using Games.Basketball.Application.Services;
using Games.Basketball.Domain.Results;
using Microsoft.Extensions.DependencyInjection;

namespace Games.Basketball.Discord;

public sealed class BasketballDiscordHandler(IBasketballService service) : IDiscordMessageHandler
{
    public bool CanHandle(DiscordMessageContext context) =>
        DiscordCommand.TryParse(context, out var command) && command.Is("basketball", "basket");

    public async Task HandleAsync(DiscordMessageContext context)
    {
        if (!DiscordCommand.TryParse(context, out var command) || !command.TryGetPositiveInt(0, out var amount))
        {
            await context.ReplyAsync("Usage: `basketball <amount>`");
            return;
        }

        var userId = context.UserId();
        var scopeId = context.ScopeId();
        var sourceId = context.SourceMessageId();
        var result = await service.PlaceBetAsync(userId, context.DisplayName(), scopeId, amount, sourceId, context.CancellationToken);
        if (result.Error != BasketballBetError.None)
        {
            await context.ReplyAsync(DiscordGameFormatting.BetError("Basketball", result.Error, result.Balance, result.PendingAmount, result.BlockingGameId, 0));
            return;
        }

        var face = RandomNumberGenerator.GetInt32(1, 6);
        var settled = await service.ThrowAsync(userId, context.DisplayName(), scopeId, face, sourceId, context.CancellationToken);
        await context.ReplyAsync(DiscordGameFormatting.NativeResult("🏀", settled.Face, settled.Bet, settled.Multiplier, settled.Payout, settled.Balance, settled.DailyRollUsed, settled.DailyRollLimit));
    }
}

public static class BasketballDiscordServiceCollectionExtensions
{
    public static IServiceCollection AddBasketballDiscord(this IServiceCollection services) =>
        services.AddScoped<IDiscordMessageHandler, BasketballDiscordHandler>();
}
