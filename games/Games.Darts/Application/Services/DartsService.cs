using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Games.Darts.Application.Execution;

namespace Games.Darts.Application.Services;

/// <summary>Compatibility facade; all wallet/quota/round mutations are executed atomically.</summary>
public sealed class DartsService(
    IDartsRoundStore rounds,
    IMiniGameSessionGhostHeal ghostHeal,
    IDartsRollQueue rollQueue,
    IRuntimeTuningAccessor tuning,
    IAtomicGameExecutor<DartsPlaceBetCommand, DartsQueuedState, DartsBetResult> placeBetExecutor,
    IAtomicGameExecutor<DartsResolveRoundCommand, DartsQueuedState, DartsThrowResult> resolveExecutor,
    IAtomicGameExecutor<DartsAbortRoundCommand, DartsQueuedState, DartsAbortRoundResult> abortExecutor,
    IAtomicGameExecutor<DartsQuickThrowCommand, NoGameState, DartsThrowResult> quickThrowExecutor,
    IMiniGameSessionStore? sessions = null) : IDartsService
{
    private IMiniGameSessionStore Sessions => sessions ?? NullMiniGameSessionStore.Instance;
    private DartsOptions Options => tuning.GetSection<DartsOptions>(DartsOptions.SectionName);

    public static IReadOnlyDictionary<int, int> Multipliers => DartsRules.Multipliers;

    public async Task<DartsBetResult> PlaceBetAsync(
        long userId, string displayName, long chatId, int amount, int replyToMessageId, CancellationToken ct)
    {
        var options = Options;
        string? blocker = null;
        var sessionClaimed = false;
        if (amount > 0 && amount <= options.MaxBet)
        {
            var session = await BotMiniGamePlaceBetSession.TryBeginWithGhostHealAsync(
                userId, chatId, MiniGameIds.Darts,
                async innerCt =>
                {
                    if (await rounds.CountActiveByUserChatAsync(userId, chatId, innerCt).ConfigureAwait(false) == 0)
                    {
                        BotMiniGameSession.ClearCompletedRound(userId, chatId, MiniGameIds.Darts);
                        await Sessions.ClearCompletedRoundAsync(userId, chatId, MiniGameIds.Darts, innerCt)
                            .ConfigureAwait(false);
                    }
                },
                ghostHeal, Sessions, ct).ConfigureAwait(false);
            sessionClaimed = session.Ok;
            if (!session.Ok) blocker = session.Blocker;
        }

        var commandId = string.Create(CultureInfo.InvariantCulture,
            $"darts:bet:{chatId}:{replyToMessageId}:{userId}");
        var roundId = StablePositiveInt64(commandId);
        var result = await placeBetExecutor.ExecuteAsync(
            new(new DartsPlaceBetCommand(userId, displayName, chatId, amount, replyToMessageId,
                roundId, commandId, options.MaxBet, blocker)), ct).ConfigureAwait(false);

        if (result.Error == DartsBetError.None)
        {
            BotMiniGameSession.RegisterPlacedBet(userId, chatId, MiniGameIds.Darts);
            await Sessions.RegisterPlacedBetAsync(userId, chatId, MiniGameIds.Darts, ct).ConfigureAwait(false);
            rollQueue.Enqueue(new DartsRollJob(
                result.RoundId, chatId, userId, displayName, replyToMessageId));
        }
        else if (sessionClaimed)
        {
            BotMiniGameSession.ClearCompletedRound(userId, chatId, MiniGameIds.Darts);
            await Sessions.ClearCompletedRoundAsync(userId, chatId, MiniGameIds.Darts, ct).ConfigureAwait(false);
        }

        return result;
    }

    public async Task<DartsThrowResult> ThrowAsync(
        long roundId, long userId, string displayName, long chatId, int botDiceMessageId, int face,
        CancellationToken ct)
    {
        var result = await resolveExecutor.ExecuteAsync(
            new(new DartsResolveRoundCommand(roundId, userId, displayName, chatId, botDiceMessageId,
                face, $"darts:throw:{roundId}:{botDiceMessageId}", Options.RedeemDropChance)), ct)
            .ConfigureAwait(false);
        if (result.Outcome == DartsThrowOutcome.Thrown
            && await rounds.CountActiveByUserChatAsync(userId, chatId, ct).ConfigureAwait(false) == 0)
            await ClearSessionAsync(userId, chatId, ct).ConfigureAwait(false);
        return result;
    }

    public async Task AbortQueuedRoundIfBetReplyFailedAsync(
        long roundId, long userId, long chatId, CancellationToken ct)
    {
        var result = await abortExecutor.ExecuteAsync(
            new(new DartsAbortRoundCommand(roundId, userId,
                string.Create(CultureInfo.InvariantCulture, $"User ID: {userId}"), chatId,
                $"darts:abort:{roundId}")), ct).ConfigureAwait(false);
        if (result.Aborted
            && await rounds.CountActiveByUserChatAsync(userId, chatId, ct).ConfigureAwait(false) == 0)
            await ClearSessionAsync(userId, chatId, ct).ConfigureAwait(false);
    }

    public async Task<DartsThrowResult> QuickThrowAsync(
        long userId, string displayName, long chatId, int diceMessageId, int face, int amount,
        CancellationToken ct)
    {
        var options = Options;
        string? blocker = null;
        var sessionClaimed = false;
        if (amount > 0 && amount <= options.MaxBet)
        {
            var session = await BotMiniGamePlaceBetSession.TryBeginWithGhostHealAsync(
                userId, chatId, MiniGameIds.Darts,
                async innerCt =>
                {
                    if (await rounds.CountActiveByUserChatAsync(userId, chatId, innerCt).ConfigureAwait(false) == 0)
                    {
                        BotMiniGameSession.ClearCompletedRound(userId, chatId, MiniGameIds.Darts);
                        await Sessions.ClearCompletedRoundAsync(userId, chatId, MiniGameIds.Darts, innerCt)
                            .ConfigureAwait(false);
                    }
                }, ghostHeal, Sessions, ct).ConfigureAwait(false);
            sessionClaimed = session.Ok;
            if (!session.Ok) blocker = session.Blocker;
        }

        var result = await quickThrowExecutor.ExecuteAsync(
            new(new DartsQuickThrowCommand(userId, displayName, chatId, diceMessageId, face, amount,
                options.MaxBet, options.RedeemDropChance, blocker)), ct).ConfigureAwait(false);
        if ((result.Outcome == DartsThrowOutcome.Thrown || sessionClaimed)
            && await rounds.CountActiveByUserChatAsync(userId, chatId, ct).ConfigureAwait(false) == 0)
            await ClearSessionAsync(userId, chatId, ct).ConfigureAwait(false);
        return result;
    }

    private async Task ClearSessionAsync(long userId, long chatId, CancellationToken ct)
    {
        BotMiniGameRollGate.Clear(MiniGameIds.Darts, userId, chatId);
        BotMiniGameSession.ClearCompletedRound(userId, chatId, MiniGameIds.Darts);
        await Sessions.ClearCompletedRoundAsync(userId, chatId, MiniGameIds.Darts, ct).ConfigureAwait(false);
    }

    private static long StablePositiveInt64(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var result = BinaryPrimitives.ReadInt64LittleEndian(hash) & long.MaxValue;
        return result == 0 ? 1 : result;
    }
}
