using System.Globalization;
using System.Diagnostics;
using Dapper;
using Microsoft.Extensions.DependencyInjection;

namespace Games.Meta.Application.Meta;

public sealed partial class MetaAnalyticsSnapshotJob(
    IServiceScopeFactory scopes,
    ILogger<MetaAnalyticsSnapshotJob> logger) : IBackgroundJob
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private const int WindowMinutes = 5;

    public string Name => "meta.analytics_snapshot";

    public async Task RunAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogSnapshotFailed(ex);
            }
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var connections = scope.ServiceProvider.GetRequiredService<INpgsqlConnectionFactory>();
        var analytics = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();

        var stopwatch = Stopwatch.StartNew();
        var outcome = "ok";
        string? errorCode = null;
        try
        {
            await using var conn = await connections.OpenAsync(ct);

            await PublishEconomyTotalsAsync(conn, analytics, ct);
            await PublishLedgerWindowsAsync(conn, analytics, ct);
            await PublishGameEconomyAsync(conn, analytics, ct);
            await PublishSeasonSnapshotAsync(conn, analytics, ct);
            await PublishQuestSnapshotsAsync(conn, analytics, ct);
            await PublishRiskAndWhalesAsync(conn, analytics, ct);
            await PublishOpsSnapshotAsync(conn, analytics, ct);
            await PublishEngagementSnapshotAsync(conn, analytics, ct);
            await PublishDeliverySnapshotAsync(conn, analytics, ct);
            await PublishGameStateSnapshotAsync(conn, analytics, ct);
            await PublishLedgerHealthSnapshotAsync(conn, analytics, ct);
            await PublishEconomyIntegritySnapshotAsync(conn, analytics, ct);
            await PublishReliabilitySnapshotAsync(conn, analytics, ct);
            await PublishGameHealthSnapshotAsync(conn, analytics, ct);
            await PublishSocialSnapshotsAsync(conn, analytics, ct);

            LogPublished(WindowMinutes);
        }
        catch (Exception ex)
        {
            outcome = "error";
            errorCode = ex.GetType().Name;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            analytics.Track("meta_analytics", "snapshot_job_health", new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["outcome"] = outcome,
                ["error_code"] = errorCode,
                ["duration_ms"] = stopwatch.Elapsed.TotalMilliseconds,
                ["window_minutes"] = WindowMinutes,
                ["completed_at"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            });
        }
    }

    private static async Task PublishEconomyTotalsAsync(
        System.Data.Common.DbConnection conn,
        IAnalyticsService analytics,
        CancellationToken ct)
    {
        const string sql = """
            WITH ranked AS (
                SELECT coins,
                       row_number() OVER (ORDER BY coins DESC) AS rn,
                       sum(coins) OVER () AS total
                FROM users
            )
            SELECT COALESCE(sum(coins), 0)::bigint AS CoinSupply,
                   count(*)::bigint AS Wallets,
                   count(*) FILTER (WHERE coins <= 10)::bigint AS NearZeroWallets,
                   COALESCE(percentile_disc(0.50) WITHIN GROUP (ORDER BY coins), 0)::bigint AS P50Coins,
                   COALESCE(percentile_disc(0.90) WITHIN GROUP (ORDER BY coins), 0)::bigint AS P90Coins,
                   COALESCE(percentile_disc(0.99) WITHIN GROUP (ORDER BY coins), 0)::bigint AS P99Coins,
                   COALESCE(sum(coins) FILTER (WHERE rn <= 1), 0)::bigint AS Top1Coins,
                   COALESCE(sum(coins) FILTER (WHERE rn <= 5), 0)::bigint AS Top5Coins,
                   COALESCE(sum(coins) FILTER (WHERE rn <= 10), 0)::bigint AS Top10Coins
            FROM ranked
            """;

        var row = await conn.QuerySingleAsync<EconomyTotalsSnapshot>(
            new CommandDefinition(sql, cancellationToken: ct));
        analytics.Track("meta_analytics", "economy_totals", Tags(row));
    }

    private static async Task PublishLedgerWindowsAsync(
        System.Data.Common.DbConnection conn,
        IAnalyticsService analytics,
        CancellationToken ct)
    {
        const string sql = """
            SELECT reason AS Reason,
                   count(*)::bigint AS Rows,
                   COALESCE(sum(CASE WHEN delta > 0 THEN delta ELSE 0 END), 0)::bigint AS Credits,
                   COALESCE(sum(CASE WHEN delta < 0 THEN -delta ELSE 0 END), 0)::bigint AS Debits,
                   COALESCE(sum(delta), 0)::bigint AS Net
            FROM economics_ledger
            WHERE created_at >= now() - (@windowMinutes || ' minutes')::interval
            GROUP BY reason
            ORDER BY abs(COALESCE(sum(delta), 0)) DESC, reason
            LIMIT 100
            """;

        var rows = await conn.QueryAsync<LedgerReasonWindow>(
            new CommandDefinition(sql, new { windowMinutes = WindowMinutes }, cancellationToken: ct));
        foreach (var row in rows)
            analytics.Track("meta_analytics", "ledger_reason_window", Tags(row));
    }

    private static async Task PublishGameEconomyAsync(
        System.Data.Common.DbConnection conn,
        IAnalyticsService analytics,
        CancellationToken ct)
    {
        const string sql = """
            SELECT split_part(reason, '.', 1) AS Module,
                   count(*)::bigint AS Rows,
                   COALESCE(sum(CASE WHEN delta < 0 THEN -delta ELSE 0 END), 0)::bigint AS Stake,
                   COALESCE(sum(CASE WHEN delta > 0 THEN delta ELSE 0 END), 0)::bigint AS Payout,
                   COALESCE(sum(delta), 0)::bigint AS Net,
                   count(DISTINCT telegram_user_id)::bigint AS Users
            FROM economics_ledger
            WHERE created_at >= now() - (@windowMinutes || ' minutes')::interval
              AND reason LIKE '%.%'
              AND split_part(reason, '.', 1) NOT IN ('admin', 'ledger', 'season')
            GROUP BY split_part(reason, '.', 1)
            ORDER BY Rows DESC
            LIMIT 50
            """;

        var rows = await conn.QueryAsync<GameEconomyWindow>(
            new CommandDefinition(sql, new { windowMinutes = WindowMinutes }, cancellationToken: ct));
        foreach (var row in rows)
            analytics.Track("meta_analytics", "game_economy_window", Tags(row));
    }

    private static async Task PublishSeasonSnapshotAsync(
        System.Data.Common.DbConnection conn,
        IAnalyticsService analytics,
        CancellationToken ct)
    {
        const string sql = """
            SELECT s.id AS SeasonId,
                   s.name AS SeasonName,
                   s.status AS Status,
                   COALESCE(count(DISTINCT p.user_id), 0)::bigint AS Players,
                   COALESCE(sum(p.games_played), 0)::bigint AS Games,
                   COALESCE(sum(p.wins), 0)::bigint AS Wins,
                   COALESCE(sum(p.losses), 0)::bigint AS Losses,
                   COALESCE(sum(p.xp), 0)::bigint AS Xp,
                   COALESCE(sum(p.total_staked), 0)::bigint AS Stake,
                   COALESCE(sum(p.total_payout), 0)::bigint AS Payout,
                   COALESCE(avg(p.level), 0)::numeric AS AvgLevel,
                   COALESCE(count(DISTINCT c.clan_id), 0)::bigint AS Clans
            FROM meta_seasons s
            LEFT JOIN meta_season_players p ON p.season_id = s.id
            LEFT JOIN meta_season_clans c ON c.season_id = s.id
            WHERE s.status = 'active'
            GROUP BY s.id, s.name, s.status
            ORDER BY s.starts_at DESC
            LIMIT 1
            """;

        var row = await conn.QuerySingleOrDefaultAsync<SeasonSnapshot>(
            new CommandDefinition(sql, cancellationToken: ct));
        if (row is not null)
            analytics.Track("meta_analytics", "season_snapshot", Tags(row));
    }

    private static async Task PublishQuestSnapshotsAsync(
        System.Data.Common.DbConnection conn,
        IAnalyticsService analytics,
        CancellationToken ct)
    {
        const string statusSql = """
            SELECT s.id AS SeasonId,
                   q.period_key AS PeriodKey,
                   count(*)::bigint AS Rows,
                   count(*) FILTER (WHERE q.progress > 0)::bigint AS Started,
                   count(*) FILTER (WHERE q.completed)::bigint AS Completed,
                   count(*) FILTER (WHERE q.claimed)::bigint AS Claimed
            FROM meta_player_quests q
            JOIN meta_seasons s ON s.id = q.season_id
            WHERE s.status = 'active'
            GROUP BY s.id, q.period_key
            ORDER BY q.period_key DESC
            LIMIT 30
            """;

        const string questSql = """
            SELECT q.quest_id AS QuestId,
                   count(*)::bigint AS Rows,
                   count(*) FILTER (WHERE q.progress > 0)::bigint AS Started,
                   count(*) FILTER (WHERE q.completed)::bigint AS Completed,
                   count(*) FILTER (WHERE q.claimed)::bigint AS Claimed,
                   COALESCE(avg(LEAST(q.progress::numeric / NULLIF(q.target, 0), 1)), 0)::numeric AS AvgProgressRatio
            FROM meta_player_quests q
            JOIN meta_seasons s ON s.id = q.season_id
            WHERE s.status = 'active'
            GROUP BY q.quest_id
            ORDER BY Completed DESC, Rows DESC
            LIMIT 100
            """;

        var statuses = await conn.QueryAsync<QuestPeriodSnapshot>(
            new CommandDefinition(statusSql, cancellationToken: ct));
        foreach (var row in statuses)
            analytics.Track("meta_analytics", "quest_period_snapshot", Tags(row));

        var quests = await conn.QueryAsync<QuestSnapshot>(
            new CommandDefinition(questSql, cancellationToken: ct));
        foreach (var row in quests)
            analytics.Track("meta_analytics", "quest_snapshot", Tags(row));
    }

    private static async Task PublishRiskAndWhalesAsync(
        System.Data.Common.DbConnection conn,
        IAnalyticsService analytics,
        CancellationToken ct)
    {
        const string whalesSql = """
            SELECT telegram_user_id AS UserId,
                   balance_scope_id AS BalanceScopeId,
                   coins AS Coins,
                   row_number() OVER (ORDER BY coins DESC, telegram_user_id ASC)::int AS Rank
            FROM users
            ORDER BY coins DESC, telegram_user_id ASC
            LIMIT 25
            """;

        const string rtpSql = """
            SELECT p.season_id AS SeasonId,
                   p.chat_id AS ChatId,
                   p.user_id AS UserId,
                   p.display_name AS DisplayName,
                   p.total_staked AS Stake,
                   p.total_payout AS Payout,
                   CASE WHEN p.total_staked > 0 THEN p.total_payout::numeric / p.total_staked ELSE 0 END AS Rtp
            FROM meta_season_players p
            JOIN meta_seasons s ON s.id = p.season_id
            WHERE s.status = 'active'
              AND p.total_staked >= 1000
            ORDER BY (p.total_payout::numeric / NULLIF(p.total_staked, 0)) DESC, p.total_staked DESC
            LIMIT 25
            """;

        const string riskSql = """
            SELECT severity AS Severity,
                   status AS Status,
                   count(*)::bigint AS Rows
            FROM meta_risk_flags
            WHERE created_at >= now() - interval '30 days'
            GROUP BY severity, status
            """;

        foreach (var row in await conn.QueryAsync<WhaleSnapshot>(new CommandDefinition(whalesSql, cancellationToken: ct)))
            analytics.Track("meta_analytics", "whale_balance", Tags(row));

        foreach (var row in await conn.QueryAsync<PlayerRtpSnapshot>(new CommandDefinition(rtpSql, cancellationToken: ct)))
            analytics.Track("meta_analytics", "player_rtp", Tags(row));

        foreach (var row in await conn.QueryAsync<RiskFlagSnapshot>(new CommandDefinition(riskSql, cancellationToken: ct)))
            analytics.Track("meta_analytics", "risk_flags", Tags(row));
    }

    private static async Task PublishOpsSnapshotAsync(
        System.Data.Common.DbConnection conn,
        IAnalyticsService analytics,
        CancellationToken ct)
    {
        const string sql = """
            SELECT (SELECT count(*)::bigint FROM event_dispatch_failures WHERE resolved_at IS NULL) AS UnresolvedDispatchFailures,
                   (SELECT count(*)::bigint FROM known_chats) AS KnownChats,
                   (SELECT count(*)::bigint FROM processed_updates WHERE completed_at >= now() - (@windowMinutes || ' minutes')::interval) AS ProcessedUpdates,
                   (SELECT count(*)::bigint FROM admin_audit WHERE occurred_at >= now() - (@windowMinutes || ' minutes')::interval) AS AdminActions
            """;

        var row = await conn.QuerySingleAsync<OpsSnapshot>(
            new CommandDefinition(sql, new { windowMinutes = WindowMinutes }, cancellationToken: ct));
        analytics.Track("meta_analytics", "ops_snapshot", Tags(row));
    }

    private static async Task PublishEngagementSnapshotAsync(
        System.Data.Common.DbConnection conn,
        IAnalyticsService analytics,
        CancellationToken ct)
    {
        const string sql = """
            SELECT count(*)::bigint AS Wallets,
                   count(DISTINCT telegram_user_id)::bigint AS Users,
                   count(DISTINCT balance_scope_id)::bigint AS BalanceScopes,
                   count(*) FILTER (WHERE created_at >= now() - interval '24 hours')::bigint AS NewWallets24H,
                   count(*) FILTER (WHERE created_at >= now() - interval '7 days')::bigint AS NewWallets7D,
                   count(*) FILTER (WHERE updated_at >= now() - interval '24 hours')::bigint AS ActiveWallets24H,
                   count(*) FILTER (WHERE last_daily_bonus_on = CURRENT_DATE)::bigint AS DailyClaimersToday,
                   (SELECT count(DISTINCT telegram_user_id)::bigint
                      FROM economics_ledger
                     WHERE created_at >= now() - interval '24 hours') AS TransactingUsers24H,
                   (SELECT count(DISTINCT balance_scope_id)::bigint
                      FROM economics_ledger
                     WHERE created_at >= now() - interval '24 hours') AS ActiveScopes24H,
                   (SELECT count(*)::bigint
                      FROM known_chats
                     WHERE last_seen_at >= now() - interval '24 hours') AS ActiveChats24H
            FROM users
            """;

        var row = await conn.QuerySingleAsync<EngagementSnapshot>(
            new CommandDefinition(sql, cancellationToken: ct));
        analytics.Track("meta_analytics", "engagement_snapshot", Tags(row));
    }

    private static async Task PublishDeliverySnapshotAsync(
        System.Data.Common.DbConnection conn,
        IAnalyticsService analytics,
        CancellationToken ct)
    {
        const string sql = """
            SELECT count(*) FILTER (WHERE status = 'pending')::bigint AS OutboxPending,
                   count(*) FILTER (WHERE status = 'sent' AND sent_at >= now() - interval '5 minutes')::bigint AS OutboxSentWindow,
                   count(*) FILTER (WHERE status = 'failed')::bigint AS OutboxFailed,
                   COALESCE(max(attempts) FILTER (WHERE status = 'pending'), 0)::int AS OutboxMaxAttempts,
                   COALESCE(EXTRACT(EPOCH FROM now() - min(created_at)
                       FILTER (WHERE status = 'pending')), 0)::double precision AS OldestPendingSeconds,
                   (SELECT count(*)::bigint FROM processed_updates
                     WHERE started_at >= now() - interval '5 minutes') AS UpdatesStartedWindow,
                   (SELECT count(*)::bigint FROM processed_updates
                     WHERE completed_at >= now() - interval '5 minutes') AS UpdatesCompletedWindow,
                   (SELECT count(*)::bigint FROM processed_updates
                     WHERE started_at >= now() - interval '5 minutes' AND error IS NOT NULL) AS UpdatesFailedWindow,
                   (SELECT COALESCE(avg(EXTRACT(EPOCH FROM completed_at - started_at) * 1000), 0)::double precision
                      FROM processed_updates
                     WHERE completed_at >= now() - interval '5 minutes') AS UpdateLatencyAvgMs,
                   (SELECT COALESCE(percentile_disc(0.95) WITHIN GROUP
                       (ORDER BY EXTRACT(EPOCH FROM completed_at - started_at) * 1000), 0)::double precision
                      FROM processed_updates
                     WHERE completed_at >= now() - interval '5 minutes') AS UpdateLatencyP95Ms,
                   COALESCE(avg(EXTRACT(EPOCH FROM sent_at - created_at) * 1000)
                       FILTER (WHERE sent_at >= now() - interval '5 minutes'), 0)::double precision AS DeliveryLatencyAvgMs,
                   COALESCE(percentile_disc(0.95) WITHIN GROUP
                       (ORDER BY EXTRACT(EPOCH FROM sent_at - created_at) * 1000)
                       FILTER (WHERE sent_at >= now() - interval '5 minutes'), 0)::double precision AS DeliveryLatencyP95Ms
            FROM telegram_outbox
            """;

        var row = await conn.QuerySingleAsync<DeliverySnapshot>(
            new CommandDefinition(sql, cancellationToken: ct));
        analytics.Track("meta_analytics", "delivery_snapshot", Tags(row));
    }

    private static async Task PublishGameStateSnapshotAsync(
        System.Data.Common.DbConnection conn,
        IAnalyticsService analytics,
        CancellationToken ct)
    {
        const string sql = """
            SELECT (SELECT count(*)::bigint FROM mini_game_sessions WHERE expires_at > now()) AS ActiveMiniGameSessions,
                   (SELECT count(*)::bigint FROM mini_game_roll_gates WHERE expires_at > now()) AS ActiveRollGates,
                   (SELECT count(*)::bigint FROM challenge_duels WHERE status IN ('pending', 'accepted')) AS OpenChallenges,
                   (SELECT count(*)::bigint FROM pick_lottery WHERE status = 'open') AS OpenLotteries,
                   (SELECT COALESCE(sum(pot_total), 0)::bigint FROM pick_lottery
                     WHERE status = 'settled' AND settled_at >= now() - interval '24 hours') AS LotteryPot24H,
                   (SELECT count(*)::bigint FROM pick_daily_lottery WHERE status = 'open') AS OpenDailyLotteries,
                   (SELECT count(*)::bigint FROM poker_tables WHERE status IN (0, 1)) AS ActivePokerTables,
                   (SELECT count(*)::bigint FROM poker_seats s
                      JOIN poker_tables t ON t.invite_code = s.invite_code WHERE t.status IN (0, 1)) AS ActivePokerSeats,
                   (SELECT count(*)::bigint FROM secret_hitler_games WHERE status IN (0, 1)) AS ActiveSecretHitlerGames,
                   (SELECT count(*)::bigint FROM secret_hitler_players p
                      JOIN secret_hitler_games g ON g.invite_code = p.invite_code WHERE g.status IN (0, 1)) AS ActiveSecretHitlerPlayers
            """;

        var row = await conn.QuerySingleAsync<GameStateSnapshot>(
            new CommandDefinition(sql, cancellationToken: ct));
        analytics.Track("meta_analytics", "game_state_snapshot", Tags(row));
    }

    private static async Task PublishLedgerHealthSnapshotAsync(
        System.Data.Common.DbConnection conn,
        IAnalyticsService analytics,
        CancellationToken ct)
    {
        const string sql = """
            SELECT count(*) FILTER (WHERE created_at >= now() - interval '5 minutes')::bigint AS RowsWindow,
                   count(*) FILTER (WHERE created_at >= now() - interval '5 minutes' AND delta > 0)::bigint AS CreditsWindow,
                   count(*) FILTER (WHERE created_at >= now() - interval '5 minutes' AND delta < 0)::bigint AS DebitsWindow,
                   COALESCE(sum(delta) FILTER (WHERE created_at >= now() - interval '5 minutes'), 0)::bigint AS NetWindow,
                   count(*) FILTER (WHERE operation_id IS NOT NULL)::bigint AS IdempotentRows,
                   count(*) FILTER (WHERE balance_after < 0)::bigint AS NegativeBalanceRows,
                   count(*) FILTER (WHERE delta = 0)::bigint AS ZeroDeltaRows,
                   COALESCE(max(created_at), to_timestamp(0)) AS LastLedgerAt,
                   COALESCE(EXTRACT(EPOCH FROM now() - max(created_at)), 0)::double precision AS LastLedgerAgeSeconds
            FROM economics_ledger
            """;

        var row = await conn.QuerySingleAsync<LedgerHealthSnapshot>(
            new CommandDefinition(sql, cancellationToken: ct));
        analytics.Track("meta_analytics", "ledger_health_snapshot", Tags(row));
    }

    private static async Task PublishEconomyIntegritySnapshotAsync(
        System.Data.Common.DbConnection conn,
        IAnalyticsService analytics,
        CancellationToken ct)
    {
        const string sql = """
            WITH latest_ledger AS (
                SELECT DISTINCT ON (telegram_user_id, balance_scope_id)
                       telegram_user_id, balance_scope_id, balance_after
                FROM economics_ledger
                ORDER BY telegram_user_id, balance_scope_id, id DESC
            ), compared AS (
                SELECT u.coins, l.balance_after,
                       CASE WHEN l.balance_after IS NULL OR l.balance_after <> u.coins THEN 1 ELSE 0 END AS mismatch,
                       abs(u.coins - COALESCE(l.balance_after, u.coins)) AS difference
                FROM users u
                LEFT JOIN latest_ledger l USING (telegram_user_id, balance_scope_id)
            ), ranked AS (
                SELECT coins::numeric,
                       row_number() OVER (ORDER BY coins)::numeric AS rn,
                       count(*) OVER ()::numeric AS n
                FROM users
            )
            SELECT COALESCE((SELECT sum(coins) FROM users), 0)::bigint AS WalletCoinSupply,
                   COALESCE((SELECT sum(balance_after) FROM latest_ledger), 0)::bigint AS LatestLedgerSupply,
                   (SELECT count(*) FROM compared WHERE mismatch = 1)::bigint AS MismatchedWallets,
                   COALESCE((SELECT sum(difference) FROM compared), 0)::bigint AS MismatchAbsoluteCoins,
                   (SELECT count(*) FROM compared WHERE balance_after IS NULL)::bigint AS WalletsWithoutLedger,
                   COALESCE((SELECT sum((2 * rn - n - 1) * coins) / NULLIF(max(n) * sum(coins), 0) FROM ranked), 0)::double precision AS BalanceGini,
                   COALESCE((SELECT 100.0 * sum(coins) FILTER (WHERE bucket = 1) / NULLIF(sum(coins), 0)
                       FROM (SELECT coins, ntile(10) OVER (ORDER BY coins DESC) AS bucket FROM users) q), 0)::double precision AS TopDecileCoinSharePercent
            """;

        var row = await conn.QuerySingleAsync<EconomyIntegritySnapshot>(
            new CommandDefinition(sql, cancellationToken: ct));
        analytics.Track("meta_analytics", "economy_integrity_snapshot", Tags(row));
    }

    private static async Task PublishReliabilitySnapshotAsync(
        System.Data.Common.DbConnection conn,
        IAnalyticsService analytics,
        CancellationToken ct)
    {
        const string sql = """
            SELECT count(*) FILTER (WHERE resolved_at IS NULL)::bigint AS UnresolvedFailures,
                   count(*) FILTER (WHERE resolved_at IS NULL AND retry_count > 0)::bigint AS RetryingFailures,
                   COALESCE(max(retry_count) FILTER (WHERE resolved_at IS NULL), 0)::int AS MaxRetryCount,
                   COALESCE(EXTRACT(EPOCH FROM now() - min(created_at) FILTER (WHERE resolved_at IS NULL)), 0)::double precision AS OldestUnresolvedSeconds,
                   count(*) FILTER (WHERE created_at >= now() - interval '5 minutes')::bigint AS NewFailuresWindow,
                   count(*) FILTER (WHERE resolved_at >= now() - interval '5 minutes')::bigint AS ResolvedFailuresWindow,
                   (SELECT count(*)::bigint FROM game_command_idempotency
                     WHERE started_at >= now() - interval '5 minutes' AND error IS NOT NULL) AS IdempotencyFailuresWindow,
                   (SELECT count(*)::bigint FROM game_command_idempotency
                     WHERE status = 'pending' AND started_at < now() - interval '5 minutes') AS StuckIdempotencyOperations,
                   (SELECT count(*)::bigint FROM telegram_outbox
                     WHERE status = 'pending' AND next_attempt_at <= now()) AS DueOutboxRows
            FROM event_dispatch_failures
            """;

        var row = await conn.QuerySingleAsync<ReliabilitySnapshot>(
            new CommandDefinition(sql, cancellationToken: ct));
        analytics.Track("meta_analytics", "reliability_snapshot", Tags(row));
    }

    private static async Task PublishGameHealthSnapshotAsync(
        System.Data.Common.DbConnection conn,
        IAnalyticsService analytics,
        CancellationToken ct)
    {
        const string sql = """
            SELECT (SELECT count(*)::bigint FROM challenge_duels WHERE status IN ('pending', 'accepted') AND expires_at < now()) AS StaleChallenges,
                   (SELECT count(*)::bigint FROM challenge_duels WHERE completed_at >= now() - interval '24 hours') AS ChallengesCompleted24H,
                   (SELECT count(*)::bigint FROM challenge_duels WHERE status IN ('declined', 'expired', 'failed') AND responded_at >= now() - interval '24 hours') AS ChallengesCancelled24H,
                   (SELECT count(*)::bigint FROM pick_lottery WHERE status = 'settled' AND settled_at >= now() - interval '24 hours') AS LotteriesSettled24H,
                   (SELECT count(*)::bigint FROM pick_lottery WHERE status = 'cancelled' AND settled_at >= now() - interval '24 hours') AS LotteriesCancelled24H,
                   (SELECT count(*)::bigint FROM poker_tables WHERE status IN (0, 1) AND last_action_at < (EXTRACT(EPOCH FROM now() - interval '30 minutes') * 1000)::bigint) AS StalePokerTables,
                   (SELECT count(*)::bigint FROM secret_hitler_games WHERE status IN (0, 1) AND last_action_at < (EXTRACT(EPOCH FROM now() - interval '30 minutes') * 1000)::bigint) AS StaleSecretHitlerGames,
                   (SELECT count(*)::bigint FROM mini_game_sessions WHERE expires_at <= now()) AS ExpiredMiniGameSessions,
                   (SELECT count(*)::bigint FROM mini_game_roll_gates WHERE expires_at <= now()) AS ExpiredRollGates
            """;

        var row = await conn.QuerySingleAsync<GameHealthSnapshot>(
            new CommandDefinition(sql, cancellationToken: ct));
        analytics.Track("meta_analytics", "game_health_snapshot", Tags(row));
    }

    private static async Task PublishSocialSnapshotsAsync(
        System.Data.Common.DbConnection conn,
        IAnalyticsService analytics,
        CancellationToken ct)
    {
        const string overviewSql = """
            SELECT (SELECT count(*)::bigint FROM known_chats WHERE first_seen_at >= now() - interval '24 hours') AS NewChats24H,
                   (SELECT count(*)::bigint FROM known_chats WHERE last_seen_at >= now() - interval '24 hours') AS ActiveChats24H,
                   (SELECT count(*)::bigint FROM economics_ledger WHERE reason = 'transfer.send' AND created_at >= now() - interval '24 hours') AS Transfers24H,
                   (SELECT COALESCE(sum(-delta), 0)::bigint FROM economics_ledger WHERE reason = 'transfer.send' AND created_at >= now() - interval '24 hours') AS TransferCoins24H,
                   (SELECT count(*)::bigint FROM challenge_duels WHERE created_at >= now() - interval '24 hours') AS ChallengesCreated24H,
                   (SELECT count(DISTINCT telegram_user_id)::bigint FROM economics_ledger WHERE reason IN ('transfer.send', 'transfer.receive') AND created_at >= now() - interval '24 hours') AS SocialUsers24H
            """;
        const string chatTypesSql = """
            SELECT chat_type AS ChatType,
                   count(*)::bigint AS Chats,
                   count(*) FILTER (WHERE first_seen_at >= now() - interval '24 hours')::bigint AS New24H,
                   count(*) FILTER (WHERE last_seen_at >= now() - interval '24 hours')::bigint AS Active24H
            FROM known_chats
            GROUP BY chat_type
            """;

        var overview = await conn.QuerySingleAsync<SocialOverviewSnapshot>(
            new CommandDefinition(overviewSql, cancellationToken: ct));
        analytics.Track("meta_analytics", "social_overview_snapshot", Tags(overview));

        var chatTypes = await conn.QueryAsync<ChatTypeSnapshot>(
            new CommandDefinition(chatTypesSql, cancellationToken: ct));
        foreach (var row in chatTypes)
            analytics.Track("meta_analytics", "chat_type_snapshot", Tags(row));
    }

    private static Dictionary<string, object?> Tags<T>(T row) where T : notnull
    {
        return typeof(T)
            .GetProperties()
            .ToDictionary(x => ToCamelCase(x.Name), x => FormatTagValue(x.GetValue(row)), StringComparer.Ordinal);
    }

    private static object? FormatTagValue(object? value)
    {
        if (value is null or string)
            return value;
        return value is IFormattable formattable
            ? formattable.ToString(format: null, CultureInfo.InvariantCulture)
            : value;
    }

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];

    [LoggerMessage(LogLevel.Debug, "meta.analytics_snapshot.published window_minutes={WindowMinutes}")]
    partial void LogPublished(int windowMinutes);

    [LoggerMessage(LogLevel.Error, "meta.analytics_snapshot.failed retry_in_minutes=5")]
    partial void LogSnapshotFailed(Exception exception);
}
