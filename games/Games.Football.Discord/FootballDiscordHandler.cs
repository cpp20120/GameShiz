using System.Security.Cryptography;
using BotFramework.Discord.Commands;
using BotFramework.Discord.Routing;
using Games.Discord.Shared;
using Games.Football.Application.Services;
using Games.Football.Domain.Results;
using Microsoft.Extensions.DependencyInjection;

namespace Games.Football.Discord;

public sealed class FootballDiscordHandler(IFootballService service) : IDiscordMessageHandler
{
    public bool CanHandle(DiscordMessageContext context) =>
        DiscordCommand.TryParse(context, out var command) && command.Is("football", "goal");

    public async Task HandleAsync(DiscordMessageContext context)
    {
        if (!DiscordCommand.TryParse(context, out var command) || !command.TryGetPositiveInt(0, out var amount))
        {
            await context.ReplyAsync("Usage: `football <amount>`");
            return;
        }

        var userId = context.UserId();
        var scopeId = context.ScopeId();
        var sourceId = context.SourceMessageId();
        var result = await service.PlaceBetAsync(userId, context.DisplayName(), scopeId, amount, sourceId, context.CancellationToken);
        if (result.Error != FootballBetError.None)
        {
            await context.ReplyAsync(DiscordGameFormatting.BetError("Football", result.Error, result.Balance, result.PendingAmount, result.BlockingGameId, 0));
            return;
        }

        var face = RandomNumberGenerator.GetInt32(1, 6);
        var settled = await service.ThrowAsync(userId, context.DisplayName(), scopeId, face, sourceId, context.CancellationToken);
        await context.ReplyAsync(DiscordGameFormatting.NativeResult("⚽", settled.Face, settled.Bet, settled.Multiplier, settled.Payout, settled.Balance, settled.DailyRollUsed, settled.DailyRollLimit));
    }
}

public static class FootballDiscordServiceCollectionExtensions
{
    public static IServiceCollection AddFootballDiscord(this IServiceCollection services) =>
        services.AddScoped<IDiscordMessageHandler, FootballDiscordHandler>();
}
