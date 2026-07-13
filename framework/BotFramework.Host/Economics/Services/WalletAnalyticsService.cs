using BotFramework.Host.Contracts.Economics;
using BotFramework.Host.Persistence.Connections;
using Dapper;

namespace BotFramework.Host.Economics.Services;

public sealed class WalletAnalyticsService(INpgsqlConnectionFactory connections) : IWalletAnalyticsService
{
    private async Task<T> One<T>(string sql, object? args, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        return await connection.QuerySingleAsync<T>(new CommandDefinition(sql, args, cancellationToken: ct));
    }

    public Task<long> CountCreatedAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct) =>
        One<long>("SELECT count(*) FROM users WHERE created_at >= @from AND created_at < @to", new { from, to }, ct);

    public Task<WalletEconomyTotals> GetTotalsAsync(CancellationToken ct) => One<WalletEconomyTotals>("""
        WITH ranked AS (SELECT coins, row_number() OVER (ORDER BY coins DESC) AS rn FROM users)
        SELECT COALESCE(sum(coins),0)::bigint AS CoinSupply, count(*)::bigint AS Wallets,
          count(*) FILTER (WHERE coins <= 10)::bigint AS NearZeroWallets,
          COALESCE(percentile_disc(.50) WITHIN GROUP (ORDER BY coins),0)::bigint AS P50Coins,
          COALESCE(percentile_disc(.90) WITHIN GROUP (ORDER BY coins),0)::bigint AS P90Coins,
          COALESCE(percentile_disc(.99) WITHIN GROUP (ORDER BY coins),0)::bigint AS P99Coins,
          COALESCE(sum(coins) FILTER (WHERE rn<=1),0)::bigint AS Top1Coins,
          COALESCE(sum(coins) FILTER (WHERE rn<=5),0)::bigint AS Top5Coins,
          COALESCE(sum(coins) FILTER (WHERE rn<=10),0)::bigint AS Top10Coins FROM ranked
        """, null, ct);

    public async Task<IReadOnlyList<WalletWhale>> ListWhalesAsync(int limit, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        return (await connection.QueryAsync<WalletWhale>(new CommandDefinition("""
            SELECT telegram_user_id AS UserId, balance_scope_id AS BalanceScopeId, coins AS Coins,
              row_number() OVER (ORDER BY coins DESC, telegram_user_id)::int AS Rank
            FROM users ORDER BY coins DESC, telegram_user_id LIMIT @limit
            """, new { limit }, cancellationToken: ct))).AsList();
    }

    public Task<WalletEngagement> GetEngagementAsync(CancellationToken ct) => One<WalletEngagement>("""
        SELECT count(*)::bigint AS Wallets, count(DISTINCT telegram_user_id)::bigint AS Users,
          count(DISTINCT balance_scope_id)::bigint AS BalanceScopes,
          count(*) FILTER (WHERE created_at>=now()-interval '24 hours')::bigint AS NewWallets24H,
          count(*) FILTER (WHERE created_at>=now()-interval '7 days')::bigint AS NewWallets7D,
          count(*) FILTER (WHERE updated_at>=now()-interval '24 hours')::bigint AS ActiveWallets24H,
          count(*) FILTER (WHERE last_daily_bonus_on=CURRENT_DATE)::bigint AS DailyClaimersToday,
          (SELECT count(DISTINCT telegram_user_id) FROM economics_ledger WHERE created_at>=now()-interval '24 hours')::bigint AS TransactingUsers24H,
          (SELECT count(DISTINCT balance_scope_id) FROM economics_ledger WHERE created_at>=now()-interval '24 hours')::bigint AS ActiveScopes24H
        FROM users
        """, null, ct);

    public Task<WalletHealth> GetHealthAsync(CancellationToken ct) => One<WalletHealth>("""
        WITH latest AS (SELECT DISTINCT ON (telegram_user_id,balance_scope_id) telegram_user_id,balance_scope_id,balance_after
          FROM economics_ledger ORDER BY telegram_user_id,balance_scope_id,id DESC)
        SELECT count(*) FILTER (WHERE u.coins<0)::bigint AS NegativeWallets,
          count(*) FILTER (WHERE l.balance_after IS NULL OR l.balance_after<>u.coins)::bigint AS MismatchedWallets
        FROM users u LEFT JOIN latest l USING(telegram_user_id,balance_scope_id)
        """, null, ct);

    public Task<WalletIntegrity> GetIntegrityAsync(CancellationToken ct) => One<WalletIntegrity>("""
        WITH latest AS (SELECT DISTINCT ON (telegram_user_id,balance_scope_id) telegram_user_id,balance_scope_id,balance_after
          FROM economics_ledger ORDER BY telegram_user_id,balance_scope_id,id DESC),
        compared AS (SELECT u.coins,l.balance_after,(l.balance_after IS NULL OR l.balance_after<>u.coins)::int mismatch,
          abs(u.coins-COALESCE(l.balance_after,u.coins)) difference FROM users u LEFT JOIN latest l USING(telegram_user_id,balance_scope_id)),
        ranked AS (SELECT coins::numeric,row_number() OVER(ORDER BY coins)::numeric rn,count(*) OVER()::numeric n FROM users)
        SELECT COALESCE((SELECT sum(coins) FROM users),0)::bigint AS WalletCoinSupply,
          COALESCE((SELECT sum(balance_after) FROM latest),0)::bigint AS LatestLedgerSupply,
          count(*) FILTER(WHERE mismatch=1)::bigint AS MismatchedWallets, COALESCE(sum(difference),0)::bigint AS MismatchAbsoluteCoins,
          count(*) FILTER(WHERE balance_after IS NULL)::bigint AS WalletsWithoutLedger,
          COALESCE((SELECT sum((2*rn-n-1)*coins)/NULLIF(max(n)*sum(coins),0) FROM ranked),0)::double precision AS BalanceGini,
          COALESCE((SELECT 100.0*sum(coins) FILTER(WHERE bucket=1)/NULLIF(sum(coins),0) FROM
            (SELECT coins,ntile(10) OVER(ORDER BY coins DESC) bucket FROM users) q),0)::double precision AS TopDecileCoinSharePercent
        FROM compared
        """, null, ct);

    public async Task<IReadOnlyList<LedgerReasonVolume>> ListReasonVolumesAsync(int windowMinutes, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        return (await connection.QueryAsync<LedgerReasonVolume>(new CommandDefinition("""
            SELECT reason AS Reason, count(*)::bigint AS Rows,
              COALESCE(sum(CASE WHEN delta>0 THEN delta ELSE 0 END),0)::bigint AS Credits,
              COALESCE(sum(CASE WHEN delta<0 THEN -delta ELSE 0 END),0)::bigint AS Debits,
              COALESCE(sum(delta),0)::bigint AS Net FROM economics_ledger
            WHERE created_at>=now()-(@windowMinutes||' minutes')::interval GROUP BY reason ORDER BY abs(sum(delta)) DESC,reason
            """, new { windowMinutes }, cancellationToken: ct))).AsList();
    }

    public async Task<IReadOnlyList<LedgerGameVolume>> ListGameVolumesAsync(int windowMinutes, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        return (await connection.QueryAsync<LedgerGameVolume>(new CommandDefinition("""
            SELECT split_part(reason,'.',1) AS Module, count(*)::bigint AS Rows,
              COALESCE(sum(CASE WHEN delta<0 THEN -delta ELSE 0 END),0)::bigint AS Stake,
              COALESCE(sum(CASE WHEN delta>0 THEN delta ELSE 0 END),0)::bigint AS Payout,
              COALESCE(sum(delta),0)::bigint AS Net, count(DISTINCT telegram_user_id)::bigint AS Users
            FROM economics_ledger WHERE created_at>=now()-(@windowMinutes||' minutes')::interval AND reason LIKE '%.%'
              AND split_part(reason,'.',1) NOT IN ('admin','ledger','season') GROUP BY split_part(reason,'.',1)
            """, new { windowMinutes }, cancellationToken: ct))).AsList();
    }

    public Task<LedgerHealth> GetLedgerHealthAsync(int windowMinutes, CancellationToken ct) => One<LedgerHealth>("""
        SELECT count(*) FILTER(WHERE created_at>=now()-(@windowMinutes||' minutes')::interval)::bigint AS RowsWindow,
          count(*) FILTER(WHERE created_at>=now()-(@windowMinutes||' minutes')::interval AND delta>0)::bigint AS CreditsWindow,
          count(*) FILTER(WHERE created_at>=now()-(@windowMinutes||' minutes')::interval AND delta<0)::bigint AS DebitsWindow,
          COALESCE(sum(delta) FILTER(WHERE created_at>=now()-(@windowMinutes||' minutes')::interval),0)::bigint AS NetWindow,
          count(*) FILTER(WHERE operation_id IS NOT NULL)::bigint AS IdempotentRows,
          count(*) FILTER(WHERE balance_after<0)::bigint AS NegativeBalanceRows, count(*) FILTER(WHERE delta=0)::bigint AS ZeroDeltaRows,
          COALESCE(max(created_at),to_timestamp(0)) AS LastLedgerAt,
          COALESCE(EXTRACT(EPOCH FROM now()-max(created_at)),0)::double precision AS LastLedgerAgeSeconds FROM economics_ledger
        """, new { windowMinutes }, ct);

    public async Task<WalletPeriodSummary> GetPeriodSummaryAsync(DateTimeOffset from, DateTimeOffset to, int topGames, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        var totals = await connection.QuerySingleAsync<(long ActiveUsers, long Stake, long Payout)>(new CommandDefinition("""
            SELECT count(DISTINCT telegram_user_id)::bigint AS ActiveUsers,
              COALESCE(sum(-delta) FILTER(WHERE delta<0 AND reason NOT LIKE 'admin.%' AND reason NOT LIKE 'transfer.%' AND reason NOT LIKE '%.rollback'),0)::bigint AS Stake,
              COALESCE(sum(delta) FILTER(WHERE delta>0 AND reason~'(payout|prize|\.win$|winnings|settle)$'),0)::bigint AS Payout
            FROM economics_ledger WHERE created_at>=@from AND created_at<@to
            """, new { from, to }, cancellationToken: ct));
        var games = (await connection.QueryAsync<LedgerGameVolume>(new CommandDefinition("""
            SELECT split_part(reason,'.',1) AS Module, count(*)::bigint AS Rows, sum(-delta)::bigint AS Stake, 0::bigint AS Payout,
              0::bigint AS Net, count(DISTINCT telegram_user_id)::bigint AS Users FROM economics_ledger
            WHERE delta<0 AND created_at>=@from AND created_at<@to AND reason NOT LIKE 'admin.%'
              AND reason NOT LIKE 'transfer.%' AND reason NOT LIKE '%.rollback'
            GROUP BY split_part(reason,'.',1) ORDER BY Stake DESC LIMIT @topGames
            """, new { from, to, topGames }, cancellationToken: ct))).AsList();
        return new WalletPeriodSummary(totals.ActiveUsers, totals.Stake, totals.Payout, games);
    }

    public Task<WalletMutationHealth> GetMutationHealthAsync(int windowMinutes, int hugeThreshold, CancellationToken ct) =>
        One<WalletMutationHealth>("""
            SELECT COALESCE(max(abs(delta)),0)::bigint AS LargestMutation,
              count(*) FILTER(WHERE abs(delta)>=@hugeThreshold)::bigint AS HugeMutations FROM economics_ledger
            WHERE created_at>=now()-(@windowMinutes||' minutes')::interval
            """, new { windowMinutes, hugeThreshold }, ct);

    public Task<WalletSocialActivity> GetSocialActivityAsync(DateTimeOffset from, CancellationToken ct) =>
        One<WalletSocialActivity>("""
            SELECT count(*) FILTER(WHERE reason='transfer.send')::bigint AS Transfers,
              COALESCE(sum(-delta) FILTER(WHERE reason='transfer.send'),0)::bigint AS TransferCoins,
              count(DISTINCT telegram_user_id) FILTER(WHERE reason IN('transfer.send','transfer.receive'))::bigint AS Users
            FROM economics_ledger WHERE created_at>=@from
            """, new { from }, ct);
}
