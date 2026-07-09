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
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;

namespace Games.DiceCube.Application.Services;

public sealed class DiceCubeService(
    IEconomicsService economics,
    IAnalyticsService analytics,
    IDiceCubeBetStore bets,
    IDomainEventBus events,
    IMemoryCache cache,
    IRuntimeTuningAccessor tuning,
    IMiniGameSessionGhostHeal ghostHeal,
    ITelegramDiceDailyRollLimiter telegramDiceRolls,
    IMiniGameSessionStore? sessions = null,
    IConnectionMultiplexer? redis = null) : IDiceCubeService
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

    public async Task<CubeBetResult> PlaceBetAsync(long userId, string displayName, long chatId, int amount, int sourceMessageId, CancellationToken ct)
    {
        var cube = Cube;
        if (amount <= 0 || amount > cube.MaxBet) return CubeBetResult.Fail(CubeBetError.InvalidAmount);

        await economics.EnsureUserAsync(userId, chatId, displayName, ct);
        var balance = await economics.GetBalanceAsync(userId, chatId, ct);
        if (amount > balance) return CubeBetResult.Fail(CubeBetError.NotEnoughCoins, balance);

        if (cube.MinSecondsBetweenBets > 0
            && await GetCooldownLastRollAsync(userId, chatId, ct) is { } lastRoll)
        {
            var wait = (lastRoll + TimeSpan.FromSeconds(cube.MinSecondsBetweenBets)) - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                var sec = (int)Math.Ceiling(wait.TotalSeconds);
                return CubeBetResult.CooldownWait(balance, sec);
            }
        }

        var session = await BotMiniGamePlaceBetSession.TryBeginWithGhostHealAsync(
            userId,
            chatId,
            MiniGameIds.DiceCube,
            async c =>
            {
                if (await bets.FindAsync(userId, chatId, c) == null)
                {
                    BotMiniGameSession.ClearCompletedRound(userId, chatId, MiniGameIds.DiceCube);
                    await Sessions.ClearCompletedRoundAsync(userId, chatId, MiniGameIds.DiceCube, c);
                }
            },
            ghostHeal,
            Sessions,
            ct);
        if (!session.Ok)
            return new CubeBetResult(CubeBetError.BusyOtherGame, 0, balance, 0, 0, session.Blocker, 0, 0);

        var existing = await bets.FindAsync(userId, chatId, ct);
        if (existing != null) return CubeBetResult.Fail(CubeBetError.AlreadyPending, balance, existing.Amount);

        var gate = await telegramDiceRolls.TryConsumeRollAsync(userId, chatId, MiniGameIds.DiceCube, ct);
        if (gate.Status == TelegramDiceRollGateStatus.LimitExceeded)
        {
            return new CubeBetResult(
                CubeBetError.DailyRollLimit, 0, balance, 0, 0, BlockingGameId: null, gate.UsedToday, gate.Limit);
        }

        var betOperationId = $"dicecube:bet:{chatId}:{sourceMessageId}:{userId}";
        var debit = await economics.TryDebitOnceAsync(userId, chatId, amount, "dicecube.bet", betOperationId, ct);
        if (debit.Rejected)
        {
            await telegramDiceRolls.TryRefundRollAsync(userId, chatId, MiniGameIds.DiceCube, ct);
            return CubeBetResult.Fail(CubeBetError.NotEnoughCoins, balance);
        }

        cube = Cube;
        var createdAt = DateTimeOffset.UtcNow;
        if (!await bets.InsertAsync(
                new DiceCubeBet(
                    userId,
                    chatId,
                    amount,
                    createdAt,
                    cube.Mult4,
                    cube.Mult5,
                    cube.Mult6),
                ct))
        {
            await telegramDiceRolls.TryRefundRollAsync(userId, chatId, MiniGameIds.DiceCube, ct);
            await economics.CreditOnceAsync(userId, chatId, amount, "dicecube.bet.refund", $"{betOperationId}:insert-refund", ct);
            return CubeBetResult.Fail(CubeBetError.AlreadyPending, balance);
        }

        BotMiniGameSession.RegisterPlacedBet(userId, chatId, MiniGameIds.DiceCube);
        await Sessions.RegisterPlacedBetAsync(userId, chatId, MiniGameIds.DiceCube, ct);

        analytics.Track("dicecube", "bet", new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["user_id"] = userId, ["chat_id"] = chatId, ["amount"] = amount,
        });

        return new CubeBetResult(CubeBetError.None, amount, debit.NewBalance, 0, 0, BlockingGameId: null, 0, 0);
    }

    public async Task<CubeRollResult> RollAsync(long userId, string displayName, long chatId, int face, CancellationToken ct)
    {
        var bet = await bets.FindAsync(userId, chatId, ct);
        if (bet == null) return new CubeRollResult(CubeRollOutcome.NoBet);

        await economics.EnsureUserAsync(userId, chatId, displayName, ct);
        var rule = new DiceCubeOptions
        {
            Mult4 = bet.Mult4,
            Mult5 = bet.Mult5,
            Mult6 = bet.Mult6,
        };
        var mults = BuildMultipliers(rule);
        var multiplier = mults.TryGetValue(face, out var m) ? m : 0;
        var payout = bet.Amount * multiplier;

        if (payout > 0)
            await economics.CreditOnceAsync(userId, chatId, payout, "dicecube.payout", BuildBetOperationId(bet, "payout"), ct);

        await bets.DeleteAsync(userId, chatId, ct);
        BotMiniGameSession.ClearCompletedRound(userId, chatId, MiniGameIds.DiceCube);
        await Sessions.ClearCompletedRoundAsync(userId, chatId, MiniGameIds.DiceCube, ct);
        var cube = Cube;
        if (cube.MinSecondsBetweenBets > 0)
            await SetCooldownLastRollAsync(userId, chatId, DateTimeOffset.UtcNow, ct);

        var balance = await economics.GetBalanceAsync(userId, chatId, ct);

        analytics.Track("dicecube", "roll", new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["user_id"] = userId, ["chat_id"] = chatId, ["face"] = face,
            ["bet"] = bet.Amount, ["multiplier"] = multiplier, ["payout"] = payout,
        });

        var occurredAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await events.PublishAsync(
            new DiceCubeRollCompleted(userId, chatId, face, bet.Amount, multiplier, payout, occurredAt),
            ct);
        await events.PublishAsync(
            new GameCompletedMetaEvent(
                ChatId: chatId,
                UserId: userId,
                DisplayName: displayName,
                GameKey: MiniGameIds.DiceCube,
                Stake: bet.Amount,
                Payout: payout,
                IsWin: payout > bet.Amount,
                Multiplier: bet.Amount > 0 ? decimal.Divide(payout, bet.Amount) : 0m,
                OccurredAt: occurredAt),
            ct);
        await TelegramMiniGameRedeemDrops.MaybePublishAsync(
            events, cube.RedeemDropChance, userId, chatId, MiniGameIds.DiceCube, occurredAt, ct);

        var daily = await telegramDiceRolls.GetRollStatusAsync(userId, chatId, MiniGameIds.DiceCube, ct);
        return new CubeRollResult(
            CubeRollOutcome.Rolled,
            face,
            bet.Amount,
            multiplier,
            payout,
            balance,
            daily.UsedToday,
            daily.Limit);
    }

    public async Task AbortPendingBetAfterSendDiceFailedAsync(long userId, long chatId, CancellationToken ct)
    {
        var bet = await bets.FindAsync(userId, chatId, ct);
        if (bet == null) return;

        await economics.CreditOnceAsync(userId, chatId, bet.Amount, "dicecube.bot_dice.failed", BuildBetOperationId(bet, "bot-dice-failed"), ct);
        await bets.DeleteAsync(userId, chatId, ct);
        BotMiniGameSession.ClearCompletedRound(userId, chatId, MiniGameIds.DiceCube);
        await Sessions.ClearCompletedRoundAsync(userId, chatId, MiniGameIds.DiceCube, ct);
        await telegramDiceRolls.TryRefundRollAsync(userId, chatId, MiniGameIds.DiceCube, ct);
    }

    private static string BuildBetOperationId(DiceCubeBet bet, string action) =>
        $"dicecube:{action}:{bet.UserId}:{bet.ChatId}:{bet.CreatedAt.ToUnixTimeMilliseconds()}";

    private async Task<DateTimeOffset?> GetCooldownLastRollAsync(long userId, long chatId, CancellationToken ct)
    {
        var key = CooldownCacheKey(userId, chatId);
        if (redis is not null)
        {
            try
            {
                var value = await redis.GetDatabase().StringGetAsync(key);
                if (long.TryParse((ReadOnlySpan<byte>)value, CultureInfo.InvariantCulture, out var unixMilliseconds))
                    return DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds);
            }
            catch (RedisException)
            {
                // Redis is an optimisation for this soft anti-spam cooldown.
                // Local memory keeps a single-node deployment useful during an outage.
            }
        }

        return cache.TryGetValue(key, out DateTimeOffset lastRoll) ? lastRoll : null;
    }

    private async Task SetCooldownLastRollAsync(
        long userId,
        long chatId,
        DateTimeOffset lastRoll,
        CancellationToken ct)
    {
        var key = CooldownCacheKey(userId, chatId);
        cache.Set(key, lastRoll, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6),
        });

        if (redis is null) return;

        try
        {
            await redis.GetDatabase().StringSetAsync(
                key,
                lastRoll.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
                TimeSpan.FromHours(6));
        }
        catch (RedisException)
        {
            // The local entry above is the intentional fallback.
        }
    }
}
