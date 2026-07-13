using System.Security.Cryptography;
using BotFramework.Discord.Commands;
using BotFramework.Discord.Routing;
using Games.Darts.Application.Services;
using Games.Darts.Domain.Results;
using Games.Discord.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Games.Darts.Discord;

public sealed class DartsDiscordHandler(IDartsService service) : IDiscordMessageHandler
{
    public bool CanHandle(DiscordMessageContext context) =>
        DiscordCommand.TryParse(context, out var command) && command.Is("darts", "dart");

    public async Task HandleAsync(DiscordMessageContext context)
    {
        if (!DiscordCommand.TryParse(context, out var command) || !command.TryGetPositiveInt(0, out var amount))
        {
            await context.ReplyAsync("Usage: `darts <amount>`");
            return;
        }

        var result = await service.QuickThrowAsync(
            context.UserId(), context.DisplayName(), context.ScopeId(), context.SourceMessageId(),
            RandomNumberGenerator.GetInt32(1, 7), amount, context.CancellationToken);
        if (result.Outcome != DartsThrowOutcome.Thrown)
        {
            await context.ReplyAsync(DiscordGameFormatting.BetError("Darts", result.Outcome, result.Balance,
                blockingGame: result.BlockingGameId));
            return;
        }

        await context.ReplyAsync(DiscordGameFormatting.NativeResult("🎯", result.Face, result.Bet,
            result.Multiplier, result.Payout, result.Balance, result.DailyRollUsed, result.DailyRollLimit));
    }
}

public static class DartsDiscordServiceCollectionExtensions
{
    public static IServiceCollection AddDartsDiscord(this IServiceCollection services) =>
        services.AddScoped<IDiscordMessageHandler, DartsDiscordHandler>();
}
