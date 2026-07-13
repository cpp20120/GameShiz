// ─────────────────────────────────────────────────────────────────────────────
// DiceCubeService — place a cube bet, resolve on 🎲 throw.
//
// Payout: credit = bet × mult(face). Defaults 1,2,2 ⇒ uniform d6 EV = 5/6 of stake
// (house +EV; old 1,2,3 was break-even for the house).
//
// MinSecondsBetweenBets: optional per-(user, chat) delay after a completed roll
// before the next /dice bet to reduce leaderboard / chat spam.
//
// Mult4/5/6 are snapshotted on the bet row so pending rolls keep the odds from
// when the bet was placed after runtime config changes.
// ─────────────────────────────────────────────────────────────────────────────

using System.Globalization;
using BotFramework.Contracts.Caching;
using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Games.DiceCube.Application.Execution;
using Microsoft.Extensions.Caching.Memory;

namespace Games.DiceCube.Application.Services;

public sealed class DiceCubeService(
    IDiceCubeBetStore bets,
    IMemoryCache cache,
    IRuntimeTuningAccessor tuning,
    IMiniGameSessionGhostHeal ghostHeal,
    IAtomicGameExecutor<DiceCubePlaceBetCommand, DiceCubePlaceBetState, CubeBetResult> placeBetExecutor,
    IAtomicGameExecutor<DiceCubeRollCommand, DiceCubePlaceBetState, CubeRollResult> rollExecutor,
    IAtomicGameExecutor<DiceCubeAbortCommand, DiceCubePlaceBetState, DiceCubeAbortResult> abortExecutor,
    IMiniGameSessionStore? sessions = null,
    ICacheStore? distributedCache = null)
    : IDiceCubeService
{
    private IMiniGameSessionStore Sessions => sessions ?? NullMiniGameSessionStore.Instance;

    private DiceCubeOptions Cube => tuning.GetSection<DiceCubeOptions>(DiceCubeOptions.SectionName);

    public int Mult4 => Cube.Mult4;
    public int Mult5 => Cube.Mult5;
    public int Mult6 => Cube.Mult6;

    public static IReadOnlyDictionary<int, int> BuildMultipliers(DiceCubeOptions o) =>
        new Dictionary<int, int>
        {
            [1] = 0, [2] = 0, [3] = 0, [4] = o.Mult4, [5] = o.Mult5, [6] = o.Mult6,
        };

    private static string CooldownCacheKey(long userId, long chatId) => string.Create(CultureInfo.InvariantCulture, $"dicecube:lastroll:{userId}:{chatId}");

    public Task<CubeBetResult> PlaceBetAsync(long userId, string displayName, long chatId, int amount, CancellationToken ct) =>
        PlaceBetAsync(userId, displayName, chatId, amount, sourceMessageId: 0, ct);

    public Task<CubeBetResult> PlaceBetAsync(
        long userId,
        string displayName,
        long chatId,
        int amount,
        int sourceMessageId,
        CancellationToken ct) =>
        PlaceBetAtomicAsync(userId, displayName, chatId, amount, sourceMessageId, ct);

    private async Task<CubeBetResult> PlaceBetAtomicAsync(
        long userId,
        string displayName,
        long chatId,
        int amount,
        int sourceMessageId,
        CancellationToken ct)
    {
        var cube = Cube;
        var cooldownSeconds = 0;
        string? blockingGameId = null;
        var canEnterSession = amount > 0 && amount <= cube.MaxBet;

        if (canEnterSession && cube.MinSecondsBetweenBets > 0
            && await GetCooldownLastRollAsync(userId, chatId, ct).ConfigureAwait(false) is { } lastRoll)
        {
            var wait = (lastRoll + TimeSpan.FromSeconds(cube.MinSecondsBetweenBets)) - DateTimeOffset.UtcNow;
            cooldownSeconds = wait > TimeSpan.Zero ? Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds)) : 0;
        }

        if (canEnterSession && cooldownSeconds == 0)
        {
            var session = await BotMiniGamePlaceBetSession.TryBeginWithGhostHealAsync(
                userId,
                chatId,
                MiniGameIds.DiceCube,
                async c =>
                {
                    if (await bets.FindAsync(userId, chatId, c).ConfigureAwait(false) == null)
                    {
                        BotMiniGameSession.ClearCompletedRound(userId, chatId, MiniGameIds.DiceCube);
                        await Sessions.ClearCompletedRoundAsync(userId, chatId, MiniGameIds.DiceCube, c)
                            .ConfigureAwait(false);
                    }
                },
                ghostHeal,
                Sessions,
                ct).ConfigureAwait(false);
            if (!session.Ok) blockingGameId = session.Blocker;
        }

        var operationId = sourceMessageId != 0
            ? $"dicecube:bet:{chatId}:{sourceMessageId}:{userId}"
            : $"dicecube:bet:legacy:{chatId}:{userId}:{Guid.NewGuid():N}";
        var command = new DiceCubePlaceBetCommand(
            userId,
            displayName,
            chatId,
            amount,
            operationId,
            cube.MaxBet,
            cube.Mult4,
            cube.Mult5,
            cube.Mult6,
            cooldownSeconds,
            blockingGameId);
        var result = await placeBetExecutor
            .ExecuteAsync(new GameExecutionEnvelope<DiceCubePlaceBetCommand>(command), ct)
            .ConfigureAwait(false);

        if (result.Error == CubeBetError.None)
        {
            BotMiniGameSession.RegisterPlacedBet(userId, chatId, MiniGameIds.DiceCube);
            await Sessions.RegisterPlacedBetAsync(userId, chatId, MiniGameIds.DiceCube, ct).ConfigureAwait(false);
        }
        return result;
    }

    public Task<CubeRollResult> RollAsync(
        long userId,
        string displayName,
        long chatId,
        int face,
        CancellationToken ct) =>
        RollAsync(userId, displayName, chatId, face, sourceMessageId: 0, ct);

    public Task<CubeRollResult> RollAsync(
        long userId,
        string displayName,
        long chatId,
        int face,
        int sourceMessageId,
        CancellationToken ct) =>
        RollAtomicAsync(userId, displayName, chatId, face, sourceMessageId, ct);

    private async Task<CubeRollResult> RollAtomicAsync(
        long userId,
        string displayName,
        long chatId,
        int face,
        int sourceMessageId,
        CancellationToken ct)
    {
        var operationId = sourceMessageId != 0
            ? $"dicecube:roll:{chatId}:{sourceMessageId}:{userId}"
            : $"dicecube:roll:legacy:{chatId}:{userId}:{Guid.NewGuid():N}";
        var cube = Cube;
        var command = new DiceCubeRollCommand(
            userId,
            displayName,
            chatId,
            face,
            operationId,
            cube.RedeemDropChance);
        var result = await rollExecutor
            .ExecuteAsync(new GameExecutionEnvelope<DiceCubeRollCommand>(command), ct)
            .ConfigureAwait(false);
        if (result.Outcome != CubeRollOutcome.Rolled) return result;

        BotMiniGameSession.ClearCompletedRound(userId, chatId, MiniGameIds.DiceCube);
        await Sessions.ClearCompletedRoundAsync(userId, chatId, MiniGameIds.DiceCube, ct).ConfigureAwait(false);
        if (cube.MinSecondsBetweenBets > 0)
            await SetCooldownLastRollAsync(userId, chatId, DateTimeOffset.UtcNow, CooldownTtl(cube), ct)
                .ConfigureAwait(false);
        return result;
    }

    public Task AbortPendingBetAfterSendDiceFailedAsync(long userId, long chatId, CancellationToken ct) =>
        AbortPendingBetAfterSendDiceFailedAsync(
            userId,
            string.Create(CultureInfo.InvariantCulture, $"User ID: {userId}"),
            chatId,
            sourceMessageId: 0,
            ct);

    public Task AbortPendingBetAfterSendDiceFailedAsync(
        long userId,
        string displayName,
        long chatId,
        int sourceMessageId,
        CancellationToken ct) =>
        AbortAtomicAsync(userId, displayName, chatId, sourceMessageId, ct);

    private async Task AbortAtomicAsync(
        long userId,
        string displayName,
        long chatId,
        int sourceMessageId,
        CancellationToken ct)
    {
        var operationId = sourceMessageId != 0
            ? $"dicecube:abort:{chatId}:{sourceMessageId}:{userId}"
            : $"dicecube:abort:legacy:{chatId}:{userId}:{Guid.NewGuid():N}";
        var result = await abortExecutor
            .ExecuteAsync(
                new GameExecutionEnvelope<DiceCubeAbortCommand>(
                    new DiceCubeAbortCommand(userId, displayName, chatId, operationId)),
                ct)
            .ConfigureAwait(false);
        if (!result.Aborted) return;

        BotMiniGameSession.ClearCompletedRound(userId, chatId, MiniGameIds.DiceCube);
        await Sessions.ClearCompletedRoundAsync(userId, chatId, MiniGameIds.DiceCube, ct).ConfigureAwait(false);
    }

    private async Task<DateTimeOffset?> GetCooldownLastRollAsync(long userId, long chatId, CancellationToken ct)
    {
        var key = CooldownCacheKey(userId, chatId);
        if (distributedCache is not null)
        {
            var value = await distributedCache.GetStringAsync(key, ct);
            if (long.TryParse(value, CultureInfo.InvariantCulture, out var unixMilliseconds))
                return DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds);
        }

        return cache.TryGetValue(key, out DateTimeOffset lastRoll) ? lastRoll : null;
    }

    private async Task SetCooldownLastRollAsync(
        long userId,
        long chatId,
        DateTimeOffset lastRoll,
        TimeSpan ttl,
        CancellationToken ct)
    {
        var key = CooldownCacheKey(userId, chatId);
        cache.Set(key, lastRoll, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl,
        });

        if (distributedCache is not null)
            await distributedCache.SetStringAsync(
                key,
                lastRoll.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
                ttl,
                ct);
    }

    private static TimeSpan CooldownTtl(DiceCubeOptions options) =>
        TimeSpan.FromSeconds(Math.Clamp(
            Math.Max(options.CooldownCacheTtlSeconds, options.MinSecondsBetweenBets + 5),
            1,
            86_400));
}
