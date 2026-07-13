using System.Globalization;
using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Games.Bowling.Application.Execution;

namespace Games.Bowling.Application.Services;

public sealed class BowlingService(
    IBowlingBetStore bets,
    IRuntimeTuningAccessor tuning,
    IMiniGameSessionGhostHeal ghostHeal,
    IAtomicGameExecutor<BowlingPlaceBetCommand, BowlingBetState, BowlingBetResult> placeExecutor,
    IAtomicGameExecutor<BowlingRollCommand, BowlingBetState, BowlingRollResult> rollExecutor,
    IAtomicGameExecutor<BowlingAbortCommand, BowlingBetState, BowlingAbortResult> abortExecutor,
    IMiniGameSessionStore? sessions = null) : IBowlingService
{
    private IMiniGameSessionStore Sessions => sessions ?? NullMiniGameSessionStore.Instance;
    private BowlingOptions Options => tuning.GetSection<BowlingOptions>(BowlingOptions.SectionName);
    public static IReadOnlyDictionary<int, int> Multipliers => BowlingRules.Multipliers;

    public async Task<BowlingBetResult> PlaceBetAsync(
        long userId, string displayName, long chatId, int amount, int sourceMessageId, CancellationToken ct)
    {
        var options = Options;
        string? blocker = null;
        var claimed = false;
        if (amount > 0 && amount <= options.MaxBet)
        {
            var session = await BotMiniGamePlaceBetSession.TryBeginWithGhostHealAsync(
                userId, chatId, MiniGameIds.Bowling,
                async c =>
                {
                    if (await bets.FindAsync(userId, chatId, c).ConfigureAwait(false) is null)
                    {
                        BotMiniGameSession.ClearCompletedRound(userId, chatId, MiniGameIds.Bowling);
                        await Sessions.ClearCompletedRoundAsync(userId, chatId, MiniGameIds.Bowling, c).ConfigureAwait(false);
                    }
                }, ghostHeal, Sessions, ct).ConfigureAwait(false);
            claimed = session.Ok;
            blocker = session.Ok ? null : session.Blocker;
        }
        var commandId = sourceMessageId != 0
            ? $"bowling:bet:{chatId}:{sourceMessageId}:{userId}"
            : $"bowling:bet:legacy:{chatId}:{userId}:{Guid.NewGuid():N}";
        var result = await placeExecutor.ExecuteAsync(new(new(
            userId, displayName, chatId, amount, commandId, options.MaxBet, blocker)), ct).ConfigureAwait(false);
        if (result.Error == BowlingBetError.None)
        {
            BotMiniGameSession.RegisterPlacedBet(userId, chatId, MiniGameIds.Bowling);
            await Sessions.RegisterPlacedBetAsync(userId, chatId, MiniGameIds.Bowling, ct).ConfigureAwait(false);
        }
        else if (claimed && result.Error != BowlingBetError.AlreadyPending)
        {
            BotMiniGameSession.ClearCompletedRound(userId, chatId, MiniGameIds.Bowling);
            await Sessions.ClearCompletedRoundAsync(userId, chatId, MiniGameIds.Bowling, ct).ConfigureAwait(false);
        }
        return result;
    }

    public Task<BowlingRollResult> RollAsync(long userId, string displayName, long chatId, int face, CancellationToken ct) =>
        RollAsync(userId, displayName, chatId, face, 0, ct);

    public async Task<BowlingRollResult> RollAsync(
        long userId, string displayName, long chatId, int face, int sourceMessageId, CancellationToken ct)
    {
        var commandId = sourceMessageId != 0
            ? $"bowling:roll:{chatId}:{sourceMessageId}:{userId}"
            : $"bowling:roll:legacy:{chatId}:{userId}:{Guid.NewGuid():N}";
        var result = await rollExecutor.ExecuteAsync(new(new(
            userId, displayName, chatId, face, commandId, Options.RedeemDropChance)), ct).ConfigureAwait(false);
        if (result.Outcome == BowlingRollOutcome.Rolled)
        {
            BotMiniGameSession.ClearCompletedRound(userId, chatId, MiniGameIds.Bowling);
            await Sessions.ClearCompletedRoundAsync(userId, chatId, MiniGameIds.Bowling, ct).ConfigureAwait(false);
        }
        return result;
    }

    public Task AbortPendingBetAfterSendDiceFailedAsync(long userId, long chatId, CancellationToken ct) =>
        AbortPendingBetAfterSendDiceFailedAsync(
            userId, string.Create(CultureInfo.InvariantCulture, $"User ID: {userId}"), chatId, 0, ct);

    public async Task AbortPendingBetAfterSendDiceFailedAsync(
        long userId, string displayName, long chatId, int sourceMessageId, CancellationToken ct)
    {
        var commandId = sourceMessageId != 0
            ? $"bowling:abort:{chatId}:{sourceMessageId}:{userId}"
            : $"bowling:abort:legacy:{chatId}:{userId}:{Guid.NewGuid():N}";
        var result = await abortExecutor.ExecuteAsync(new(new(userId, displayName, chatId, commandId)), ct).ConfigureAwait(false);
        if (!result.Aborted) return;
        BotMiniGameSession.ClearCompletedRound(userId, chatId, MiniGameIds.Bowling);
        await Sessions.ClearCompletedRoundAsync(userId, chatId, MiniGameIds.Bowling, ct).ConfigureAwait(false);
    }
}
