using Dapper;

namespace BotFramework.Host.Economics.Services;

public sealed class PlayerProtectionService(INpgsqlConnectionFactory connections) : IPlayerProtectionService
{
    public async Task<PlayerStats> GetStatsAsync(long userId, long balanceScopeId, CancellationToken ct)
    {
        const string sql = """
            SELECT @userId AS UserId,
                   @balanceScopeId AS BalanceScopeId,
                   COALESCE((SELECT coins FROM users
                       WHERE telegram_user_id = @userId AND balance_scope_id = @balanceScopeId), 0) AS Balance,
                   COALESCE(sum(-delta) FILTER (WHERE delta < 0 AND created_at >= now() - interval '7 days'), 0)::bigint AS Stake7Days,
                   COALESCE(sum(delta) FILTER (WHERE delta > 0 AND created_at >= now() - interval '7 days'
                       AND reason ~ '(payout|prize|\.win$|winnings|settle)$'), 0)::bigint AS Payout7Days,
                   COALESCE(sum(-delta) FILTER (WHERE delta < 0 AND created_at >= now() - interval '30 days'), 0)::bigint AS Stake30Days,
                   COALESCE(sum(delta) FILTER (WHERE delta > 0 AND created_at >= now() - interval '30 days'
                       AND reason ~ '(payout|prize|\.win$|winnings|settle)$'), 0)::bigint AS Payout30Days,
                   COALESCE(sum(-delta) FILTER (WHERE delta < 0 AND created_at >= date_trunc('day', now() AT TIME ZONE 'UTC') AT TIME ZONE 'UTC'), 0)::bigint AS StakeToday,
                   p.daily_stake_limit AS DailyStakeLimit,
                   p.cooldown_until AS CooldownUntil,
                   p.self_excluded_until AS SelfExcludedUntil
            FROM (SELECT 1) seed
            LEFT JOIN economics_ledger l
              ON l.telegram_user_id = @userId
             AND l.reason NOT LIKE 'admin.%'
             AND l.reason NOT LIKE '%.rollback'
             AND l.reason NOT LIKE 'transfer.%'
            LEFT JOIN player_protection p ON p.telegram_user_id = @userId
            GROUP BY p.daily_stake_limit, p.cooldown_until, p.self_excluded_until
            """;
        await using var conn = await connections.OpenAsync(ct);
        return await conn.QuerySingleAsync<PlayerStats>(new CommandDefinition(
            sql, new { userId, balanceScopeId }, cancellationToken: ct));
    }

    public Task SetDailyLimitAsync(long userId, int? limit, CancellationToken ct)
    {
        if (limit is < 0) throw new ArgumentOutOfRangeException(nameof(limit));
        return UpsertAsync(userId, "daily_stake_limit = @value", limit, ct);
    }

    public Task SetCooldownAsync(long userId, DateTimeOffset until, CancellationToken ct)
    {
        if (until <= DateTimeOffset.UtcNow) throw new ArgumentOutOfRangeException(nameof(until));
        return UpsertAsync(userId, "cooldown_until = GREATEST(player_protection.cooldown_until, @value)", until, ct);
    }

    public Task SetSelfExclusionAsync(long userId, DateTimeOffset until, CancellationToken ct)
    {
        if (until <= DateTimeOffset.UtcNow) throw new ArgumentOutOfRangeException(nameof(until));
        return UpsertAsync(userId, "self_excluded_until = GREATEST(player_protection.self_excluded_until, @value)", until, ct);
    }

    private async Task UpsertAsync(long userId, string updateClause, object? value, CancellationToken ct)
    {
        var column = updateClause[..updateClause.IndexOf(' ', StringComparison.Ordinal)];
        var sql = $"""
            INSERT INTO player_protection (telegram_user_id, {column})
            VALUES (@userId, @value)
            ON CONFLICT (telegram_user_id) DO UPDATE
            SET {updateClause}, updated_at = now()
            """;
        await using var conn = await connections.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { userId, value }, cancellationToken: ct));
    }
}
