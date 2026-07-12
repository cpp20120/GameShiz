using System.Globalization;
using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Games.Football.Application.Execution;

namespace Games.Football.Application.Services;

public sealed class FootballService(
    IFootballBetStore bets,
    IRuntimeTuningAccessor tuning,
    IMiniGameSessionGhostHeal ghostHeal,
    IAtomicGameExecutor<FootballPlaceBetCommand, FootballBetState, FootballBetResult> placeExecutor,
    IAtomicGameExecutor<FootballThrowCommand, FootballBetState, FootballThrowResult> throwExecutor,
    IAtomicGameExecutor<FootballAbortCommand, FootballBetState, FootballAbortResult> abortExecutor,
    IMiniGameSessionStore? sessions = null) : IFootballService
{
    private IMiniGameSessionStore Sessions => sessions ?? NullMiniGameSessionStore.Instance;
    private FootballOptions Options => tuning.GetSection<FootballOptions>(FootballOptions.SectionName);
    public static IReadOnlyDictionary<int, int> Multipliers => FootballRules.Multipliers;

    public Task<FootballBetResult> PlaceBetAsync(long userId, string displayName, long chatId, int amount, CancellationToken ct) =>
        PlaceBetAsync(userId, displayName, chatId, amount, 0, ct);

    public async Task<FootballBetResult> PlaceBetAsync(
        long userId, string displayName, long chatId, int amount, int sourceMessageId, CancellationToken ct)
    {
        var options = Options;
        string? blocker = null;
        var claimed = false;
        if (amount > 0 && amount <= options.MaxBet)
        {
            var session = await BotMiniGamePlaceBetSession.TryBeginWithGhostHealAsync(
                userId, chatId, MiniGameIds.Football,
                async c =>
                {
                    if (await bets.FindAsync(userId, chatId, c).ConfigureAwait(false) is null)
                    {
                        BotMiniGameSession.ClearCompletedRound(userId, chatId, MiniGameIds.Football);
                        await Sessions.ClearCompletedRoundAsync(userId, chatId, MiniGameIds.Football, c).ConfigureAwait(false);
                    }
                }, ghostHeal, Sessions, ct).ConfigureAwait(false);
            claimed = session.Ok;
            blocker = session.Ok ? null : session.Blocker;
        }
        var commandId = sourceMessageId != 0
            ? $"football:bet:{chatId}:{sourceMessageId}:{userId}"
            : $"football:bet:legacy:{chatId}:{userId}:{Guid.NewGuid():N}";
        var result = await placeExecutor.ExecuteAsync(new(new(
            userId, displayName, chatId, amount, commandId, options.MaxBet, blocker)), ct).ConfigureAwait(false);
        if (result.Error == FootballBetError.None)
        {
            BotMiniGameSession.RegisterPlacedBet(userId, chatId, MiniGameIds.Football);
            await Sessions.RegisterPlacedBetAsync(userId, chatId, MiniGameIds.Football, ct).ConfigureAwait(false);
        }
        else if (claimed && result.Error != FootballBetError.AlreadyPending)
        {
            BotMiniGameSession.ClearCompletedRound(userId, chatId, MiniGameIds.Football);
            await Sessions.ClearCompletedRoundAsync(userId, chatId, MiniGameIds.Football, ct).ConfigureAwait(false);
        }
        return result;
    }

    public Task<FootballThrowResult> ThrowAsync(long userId, string displayName, long chatId, int face, CancellationToken ct) =>
        ThrowAsync(userId, displayName, chatId, face, 0, ct);

    public async Task<FootballThrowResult> ThrowAsync(
        long userId, string displayName, long chatId, int face, int sourceMessageId, CancellationToken ct)
    {
        var commandId = sourceMessageId != 0
            ? $"football:throw:{chatId}:{sourceMessageId}:{userId}"
            : $"football:throw:legacy:{chatId}:{userId}:{Guid.NewGuid():N}";
        var result = await throwExecutor.ExecuteAsync(new(new(
            userId, displayName, chatId, face, commandId, Options.RedeemDropChance)), ct).ConfigureAwait(false);
        if (result.Outcome == FootballThrowOutcome.Thrown)
        {
            BotMiniGameSession.ClearCompletedRound(userId, chatId, MiniGameIds.Football);
            await Sessions.ClearCompletedRoundAsync(userId, chatId, MiniGameIds.Football, ct).ConfigureAwait(false);
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
            ? $"football:abort:{chatId}:{sourceMessageId}:{userId}"
            : $"football:abort:legacy:{chatId}:{userId}:{Guid.NewGuid():N}";
        var result = await abortExecutor.ExecuteAsync(new(new(userId, displayName, chatId, commandId)), ct).ConfigureAwait(false);
        if (!result.Aborted) return;
        BotMiniGameSession.ClearCompletedRound(userId, chatId, MiniGameIds.Football);
        await Sessions.ClearCompletedRoundAsync(userId, chatId, MiniGameIds.Football, ct).ConfigureAwait(false);
    }
}
