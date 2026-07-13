using BotFramework.Sdk.Execution;
using Dapper;

namespace BotFramework.Host.Execution;

internal sealed class PostgresAtomicPlayerProtection(TimeProvider timeProvider) : IAtomicPlayerProtection
{
    public async Task EnforceAsync(
        long userId,
        IReadOnlyList<EconomyEffect> effects,
        IGameExecutionSession session,
        CancellationToken ct)
    {
        var stake = effects
            .Where(effect => effect.Kind == EconomyEffectKind.Debit && EconomicsService.IsProtectedWager(effect.Reason))
            .Sum(effect => effect.Amount);
        if (stake <= 0) return;

        // Match the legacy EconomicsService lock while games are migrated incrementally.
        await session.Connection.ExecuteAsync(new CommandDefinition(
            "SELECT pg_advisory_xact_lock(@userId)",
            new { userId },
            session.Transaction,
            cancellationToken: ct)).ConfigureAwait(false);

        const string sql = """
            SELECT p.daily_stake_limit AS DailyLimit,
                   p.cooldown_until AS CooldownUntil,
                   p.self_excluded_until AS SelfExcludedUntil,
                   COALESCE((
                       SELECT sum(-l.delta)
                       FROM economics_ledger l
                       WHERE l.telegram_user_id = @userId
                         AND l.delta < 0
                         AND l.created_at >= date_trunc('day', now() AT TIME ZONE 'UTC') AT TIME ZONE 'UTC'
                         AND l.reason NOT LIKE 'admin.%'
                         AND l.reason NOT LIKE 'transfer.%'
                         AND l.reason NOT LIKE '%.rollback'
                   ), 0)::bigint AS UsedToday
            FROM player_protection p
            WHERE p.telegram_user_id = @userId
            """;
        var protection = await session.Connection.QuerySingleOrDefaultAsync<ProtectionRow>(new CommandDefinition(
            sql,
            new { userId },
            session.Transaction,
            cancellationToken: ct)).ConfigureAwait(false);
        if (protection is null) return;

        var utcNow = timeProvider.GetUtcNow();
        if (protection.SelfExcludedUntil is { } excluded && excluded > utcNow)
            throw new PlayerProtectionException("self_excluded", excluded);
        if (protection.CooldownUntil is { } cooldown && cooldown > utcNow)
            throw new PlayerProtectionException("cooldown", cooldown);
        if (protection.DailyLimit is { } limit && protection.UsedToday + stake > limit)
            throw new PlayerProtectionException("daily_limit", dailyLimit: limit, usedToday: protection.UsedToday);
    }

    private sealed record ProtectionRow(
        int? DailyLimit,
        DateTimeOffset? CooldownUntil,
        DateTimeOffset? SelfExcludedUntil,
        long UsedToday);
}
