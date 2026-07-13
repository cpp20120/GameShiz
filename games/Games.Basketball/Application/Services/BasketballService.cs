using System.Globalization;
using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Games.Basketball.Application.Execution;

namespace Games.Basketball.Application.Services;

public sealed class BasketballService(
    IBasketballBetStore bets,
    IRuntimeTuningAccessor tuning,
    IMiniGameSessionGhostHeal ghostHeal,
    IAtomicGameExecutor<BasketballPlaceBetCommand, BasketballBetState, BasketballBetResult> placeBetExecutor,
    IAtomicGameExecutor<BasketballThrowCommand, BasketballBetState, BasketballThrowResult> throwExecutor,
    IAtomicGameExecutor<BasketballAbortCommand, BasketballBetState, BasketballAbortResult> abortExecutor,
    IMiniGameSessionStore? sessions = null) : IBasketballService
{
    private IMiniGameSessionStore Sessions => sessions ?? NullMiniGameSessionStore.Instance;

    private BasketballOptions Options => tuning.GetSection<BasketballOptions>(BasketballOptions.SectionName);

    public static IReadOnlyDictionary<int, int> Multipliers => BasketballRules.Multipliers;

    public Task<BasketballBetResult> PlaceBetAsync(
        long userId,
        string displayName,
        long chatId,
        int amount,
        CancellationToken ct) =>
        PlaceBetAsync(userId, displayName, chatId, amount, sourceMessageId: 0, ct);

    public async Task<BasketballBetResult> PlaceBetAsync(
        long userId,
        string displayName,
        long chatId,
        int amount,
        int sourceMessageId,
        CancellationToken ct)
    {
        var options = Options;
        string? blockingGameId = null;
        var sessionClaimed = false;
        if (amount > 0 && amount <= options.MaxBet)
        {
            var session = await BotMiniGamePlaceBetSession.TryBeginWithGhostHealAsync(
                userId,
                chatId,
                MiniGameIds.Basketball,
                async c =>
                {
                    if (await bets.FindAsync(userId, chatId, c).ConfigureAwait(false) is null)
                    {
                        BotMiniGameSession.ClearCompletedRound(userId, chatId, MiniGameIds.Basketball);
                        await Sessions.ClearCompletedRoundAsync(userId, chatId, MiniGameIds.Basketball, c)
                            .ConfigureAwait(false);
                    }
                },
                ghostHeal,
                Sessions,
                ct).ConfigureAwait(false);
            sessionClaimed = session.Ok;
            if (!session.Ok) blockingGameId = session.Blocker;
        }

        var commandId = sourceMessageId != 0
            ? $"basketball:bet:{chatId}:{sourceMessageId}:{userId}"
            : $"basketball:bet:legacy:{chatId}:{userId}:{Guid.NewGuid():N}";
        var result = await placeBetExecutor.ExecuteAsync(
            new GameExecutionEnvelope<BasketballPlaceBetCommand>(new BasketballPlaceBetCommand(
                userId,
                displayName,
                chatId,
                amount,
                commandId,
                options.MaxBet,
                blockingGameId)),
            ct).ConfigureAwait(false);

        if (result.Error == BasketballBetError.None)
        {
            BotMiniGameSession.RegisterPlacedBet(userId, chatId, MiniGameIds.Basketball);
            await Sessions.RegisterPlacedBetAsync(userId, chatId, MiniGameIds.Basketball, ct).ConfigureAwait(false);
        }
        else if (sessionClaimed && result.Error != BasketballBetError.AlreadyPending)
        {
            BotMiniGameSession.ClearCompletedRound(userId, chatId, MiniGameIds.Basketball);
            await Sessions.ClearCompletedRoundAsync(userId, chatId, MiniGameIds.Basketball, ct).ConfigureAwait(false);
        }

        return result;
    }

    public Task<BasketballThrowResult> ThrowAsync(
        long userId,
        string displayName,
        long chatId,
        int face,
        CancellationToken ct) =>
        ThrowAsync(userId, displayName, chatId, face, sourceMessageId: 0, ct);

    public async Task<BasketballThrowResult> ThrowAsync(
        long userId,
        string displayName,
        long chatId,
        int face,
        int sourceMessageId,
        CancellationToken ct)
    {
        var commandId = sourceMessageId != 0
            ? $"basketball:throw:{chatId}:{sourceMessageId}:{userId}"
            : $"basketball:throw:legacy:{chatId}:{userId}:{Guid.NewGuid():N}";
        var result = await throwExecutor.ExecuteAsync(
            new GameExecutionEnvelope<BasketballThrowCommand>(new BasketballThrowCommand(
                userId,
                displayName,
                chatId,
                face,
                commandId,
                Options.RedeemDropChance)),
            ct).ConfigureAwait(false);
        if (result.Outcome != BasketballThrowOutcome.Thrown) return result;

        BotMiniGameSession.ClearCompletedRound(userId, chatId, MiniGameIds.Basketball);
        await Sessions.ClearCompletedRoundAsync(userId, chatId, MiniGameIds.Basketball, ct).ConfigureAwait(false);
        return result;
    }

    public Task AbortPendingBetAfterSendDiceFailedAsync(
        long userId,
        long chatId,
        CancellationToken ct) =>
        AbortPendingBetAfterSendDiceFailedAsync(
            userId,
            string.Create(CultureInfo.InvariantCulture, $"User ID: {userId}"),
            chatId,
            sourceMessageId: 0,
            ct);

    public async Task AbortPendingBetAfterSendDiceFailedAsync(
        long userId,
        string displayName,
        long chatId,
        int sourceMessageId,
        CancellationToken ct)
    {
        var commandId = sourceMessageId != 0
            ? $"basketball:abort:{chatId}:{sourceMessageId}:{userId}"
            : $"basketball:abort:legacy:{chatId}:{userId}:{Guid.NewGuid():N}";
        var result = await abortExecutor.ExecuteAsync(
            new GameExecutionEnvelope<BasketballAbortCommand>(new BasketballAbortCommand(
                userId,
                displayName,
                chatId,
                commandId)),
            ct).ConfigureAwait(false);
        if (!result.Aborted) return;

        BotMiniGameSession.ClearCompletedRound(userId, chatId, MiniGameIds.Basketball);
        await Sessions.ClearCompletedRoundAsync(userId, chatId, MiniGameIds.Basketball, ct).ConfigureAwait(false);
    }
}
