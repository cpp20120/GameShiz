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
        var walletAnalytics = scope.ServiceProvider.GetRequiredService<IWalletAnalyticsService>();

        var stopwatch = Stopwatch.StartNew();
        var outcome = "ok";
        string? errorCode = null;
        try
        {
            await using var conn = await connections.OpenAsync(ct);

            await PublishEconomyTotalsAsync(walletAnalytics, analytics, ct);
            await PublishLedgerWindowsAsync(walletAnalytics, analytics, ct);
            await PublishGameEconomyAsync(walletAnalytics, analytics, ct);
            await PublishSeasonSnapshotAsync(conn, analytics, ct);
            await PublishQuestSnapshotsAsync(conn, analytics, ct);
            await PublishRiskAndWhalesAsync(conn, walletAnalytics, analytics, ct);
            await PublishOpsSnapshotAsync(conn, analytics, ct);
            await PublishEngagementSnapshotAsync(conn, walletAnalytics, analytics, ct);
            await PublishDeliverySnapshotAsync(conn, analytics, ct);
            await PublishGameStateSnapshotAsync(conn, analytics, ct);
            await PublishLedgerHealthSnapshotAsync(walletAnalytics, analytics, ct);
            await PublishEconomyIntegritySnapshotAsync(walletAnalytics, analytics, ct);
            await PublishReliabilitySnapshotAsync(conn, analytics, ct);
            await PublishGameHealthSnapshotAsync(conn, analytics, ct);
            await PublishSocialSnapshotsAsync(conn, walletAnalytics, analytics, ct);

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
        IWalletAnalyticsService wallets,
        IAnalyticsService analytics,
        CancellationToken ct)
    {
        var value = await wallets.GetTotalsAsync(ct);
        var row = new EconomyTotalsSnapshot(value.CoinSupply, value.Wallets, value.NearZeroWallets,
            value.P50Coins, value.P90Coins, value.P99Coins, value.Top1Coins, value.Top5Coins, value.Top10Coins);
        analytics.Track("meta_analytics", "economy_totals", Tags(row));
    }

    private static async Task PublishLedgerWindowsAsync(
        IWalletAnalyticsService wallets,
        IAnalyticsService analytics,
        CancellationToken ct)
    {
        foreach (var value in (await wallets.ListReasonVolumesAsync(WindowMinutes, ct)).Take(100))
            analytics.Track("meta_analytics", "ledger_reason_window", Tags(new LedgerReasonWindow(
                value.Reason, value.Rows, value.Credits, value.Debits, value.Net)));
    }

    private static async Task PublishGameEconomyAsync(
        IWalletAnalyticsService wallets,
        IAnalyticsService analytics,
        CancellationToken ct)
    {
        foreach (var value in (await wallets.ListGameVolumesAsync(WindowMinutes, ct)).OrderByDescending(x => x.Rows).Take(50))
            analytics.Track("meta_analytics", "game_economy_window", Tags(new GameEconomyWindow(
                value.Module, value.Rows, value.Stake, value.Payout, value.Net, value.Users)));
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
        IWalletAnalyticsService wallets,
        IAnalyticsService analytics,
        CancellationToken ct)
    {
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

        foreach (var value in await wallets.ListWhalesAsync(25, ct))
            analytics.Track("meta_analytics", "whale_balance", Tags(new WhaleSnapshot(
                value.UserId, value.BalanceScopeId, value.Coins, value.Rank)));

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
        IWalletAnalyticsService wallets,
        IAnalyticsService analytics,
        CancellationToken ct)
    {
        var value = await wallets.GetEngagementAsync(ct);
        var activeChats = await conn.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT count(*) FROM known_chats WHERE last_seen_at >= now() - interval '24 hours'", cancellationToken: ct));
        var row = new EngagementSnapshot(value.Wallets, value.Users, value.BalanceScopes,
            value.NewWallets24H, value.NewWallets7D, value.ActiveWallets24H, value.DailyClaimersToday,
            value.TransactingUsers24H, value.ActiveScopes24H, activeChats);
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
        IWalletAnalyticsService wallets,
        IAnalyticsService analytics,
        CancellationToken ct)
    {
        var value = await wallets.GetLedgerHealthAsync(WindowMinutes, ct);
        var row = new LedgerHealthSnapshot(value.RowsWindow, value.CreditsWindow, value.DebitsWindow,
            value.NetWindow, value.IdempotentRows, value.NegativeBalanceRows, value.ZeroDeltaRows,
            value.LastLedgerAt.UtcDateTime, value.LastLedgerAgeSeconds);
        analytics.Track("meta_analytics", "ledger_health_snapshot", Tags(row));
    }

    private static async Task PublishEconomyIntegritySnapshotAsync(
        IWalletAnalyticsService wallets,
        IAnalyticsService analytics,
        CancellationToken ct)
    {
        var value = await wallets.GetIntegrityAsync(ct);
        var row = new EconomyIntegritySnapshot(value.WalletCoinSupply, value.LatestLedgerSupply,
            value.MismatchedWallets, value.MismatchAbsoluteCoins, value.WalletsWithoutLedger,
            value.BalanceGini, value.TopDecileCoinSharePercent);
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
        IWalletAnalyticsService wallets,
        IAnalyticsService analytics,
        CancellationToken ct)
    {
        const string overviewSql = """
            SELECT (SELECT count(*)::bigint FROM known_chats WHERE first_seen_at >= now() - interval '24 hours') AS NewChats24H,
                   (SELECT count(*)::bigint FROM known_chats WHERE last_seen_at >= now() - interval '24 hours') AS ActiveChats24H,
                   0::bigint AS Transfers24H, 0::bigint AS TransferCoins24H,
                   (SELECT count(*)::bigint FROM challenge_duels WHERE created_at >= now() - interval '24 hours') AS ChallengesCreated24H,
                   0::bigint AS SocialUsers24H
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
        var social = await wallets.GetSocialActivityAsync(DateTimeOffset.UtcNow.AddHours(-24), ct);
        overview = overview with { Transfers24H = social.Transfers, TransferCoins24H = social.TransferCoins, SocialUsers24H = social.Users };
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
