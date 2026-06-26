using System.Globalization;
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
            await TickAsync(stoppingToken);
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var connections = scope.ServiceProvider.GetRequiredService<INpgsqlConnectionFactory>();
        var analytics = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();

        await using var conn = await connections.OpenAsync(ct);

        await PublishEconomyTotalsAsync(conn, analytics, ct);
        await PublishLedgerWindowsAsync(conn, analytics, ct);
        await PublishGameEconomyAsync(conn, analytics, ct);
        await PublishSeasonSnapshotAsync(conn, analytics, ct);
        await PublishQuestSnapshotsAsync(conn, analytics, ct);
        await PublishRiskAndWhalesAsync(conn, analytics, ct);
        await PublishOpsSnapshotAsync(conn, analytics, ct);

        LogPublished(WindowMinutes);
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
}
