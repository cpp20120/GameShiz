using System.Globalization;
using System.Net;
using Dapper;
using Microsoft.Extensions.Options;

namespace Games.Meta.Application.Meta;

public sealed partial class OperationsReportingJob(
    INpgsqlConnectionFactory connections,
    ITelegramOutbox outbox,
    IOptions<BotFrameworkOptions> botOptions,
    IAnalyticsService analytics,
    IWalletAnalyticsService walletAnalytics,
    ILogger<OperationsReportingJob> logger) : IBackgroundJob
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private readonly BotFrameworkOptions _bot = botOptions.Value;

    public string Name => "meta.operations_reporting";

    public async Task RunAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_bot.Admins.Count > 0)
                {
                    await SendWeeklySummaryIfDueAsync(stoppingToken);
                    await SendEconomyAlertsAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                LogReportingFailed(ex);
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task SendWeeklySummaryIfDueAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var thisWeekStart = StartOfIsoWeek(now.UtcDateTime);
        var from = thisWeekStart.AddDays(-7);
        var to = thisWeekStart;
        var periodKey = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (await IsSentAsync("weekly_admin_summary", periodKey, ct)) return;

        await using var conn = await connections.OpenAsync(ct);
        const string sql = """
            SELECT
                0::bigint AS ActiveUsers,
                0::bigint AS NewWallets,
                0::bigint AS Stake, 0::bigint AS Payout,
                (SELECT count(*) FROM processed_updates WHERE started_at >= @from AND started_at < @to) AS Updates,
                (SELECT count(*) FROM event_dispatch_failures WHERE created_at >= @from AND created_at < @to) AS DispatchFailures,
                (SELECT count(*) FROM telegram_outbox WHERE status = 'failed' AND created_at >= @from AND created_at < @to) AS DeliveryFailures,
                (SELECT count(*) FROM player_protection WHERE updated_at >= @from AND updated_at < @to
                    AND (daily_stake_limit IS NOT NULL OR cooldown_until IS NOT NULL OR self_excluded_until IS NOT NULL)) AS ProtectionChanges
            """;
        var row = await conn.QuerySingleAsync<WeeklySummary>(new CommandDefinition(
            sql, new { from, to }, cancellationToken: ct));
        var period = await walletAnalytics.GetPeriodSummaryAsync(from, to, 5, ct);
        row = row with
        {
            NewWallets = await walletAnalytics.CountCreatedAsync(from, to, ct),
            ActiveUsers = period.ActiveUsers,
            Stake = period.Stake,
            Payout = period.Payout,
        };
        var topGames = period.TopGames.Select(x => new GameVolume(x.Module, x.Stake)).ToList();

        var games = topGames.Count == 0
            ? "нет ставок"
            : string.Join("\n", topGames.Select(x => $"• {WebUtility.HtmlEncode(x.Module)}: <b>{x.Stake}</b>"));
        var net = row.Stake - row.Payout;
        var text = string.Create(CultureInfo.InvariantCulture, $"""
            📅 <b>Еженедельный отчёт CasinoShiz</b>
            <code>{from:yyyy-MM-dd} — {to.AddDays(-1):yyyy-MM-dd} UTC</code>

            👥 Активные игроки: <b>{row.ActiveUsers}</b>
            🆕 Новые кошельки: <b>{row.NewWallets}</b>
            🎮 Обновления Telegram: <b>{row.Updates}</b>
            💸 Ставки: <b>{row.Stake}</b>
            💰 Выплаты: <b>{row.Payout}</b>
            🏦 Результат системы: <b>{net}</b>
            🛡 Изменения лимитов: <b>{row.ProtectionChanges}</b>

            <b>Топ игр по ставкам</b>
            {games}

            ⚠️ Ошибки dispatch: <b>{row.DispatchFailures}</b>
            📤 Ошибки доставки: <b>{row.DeliveryFailures}</b>
            """);

        await EnqueueForAdminsAsync(text, $"weekly-summary:{periodKey}", ct);
        await MarkSentAsync("weekly_admin_summary", periodKey, ct);
        analytics.Track("meta_analytics", "weekly_admin_summary", new Dictionary<string, object?>
        {
            ["period"] = periodKey,
            ["active_users"] = row.ActiveUsers,
            ["stake"] = row.Stake,
            ["payout"] = row.Payout,
            ["outcome"] = "queued",
        });
    }

    private async Task SendEconomyAlertsAsync(CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        var health = new EconomyHealth(0, 0, 0, 0);
        var walletHealth = await walletAnalytics.GetHealthAsync(ct);
        var mutations = await walletAnalytics.GetMutationHealthAsync(15, 100_000, ct);
        health = health with
        {
            NegativeWallets = walletHealth.NegativeWallets,
            MismatchedWallets = walletHealth.MismatchedWallets,
            LargestMutation = mutations.LargestMutation,
            HugeMutations = mutations.HugeMutations,
        };

        var issues = new List<string>();
        if (health.NegativeWallets > 0) issues.Add($"отрицательных кошельков: <b>{health.NegativeWallets}</b>");
        if (health.MismatchedWallets > 0) issues.Add($"расхождений ledger/wallet: <b>{health.MismatchedWallets}</b>");
        if (health.HugeMutations > 0) issues.Add($"изменений ≥100000 за 15 мин: <b>{health.HugeMutations}</b> (макс. {health.LargestMutation})");
        if (issues.Count == 0) return;

        var hourKey = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd-HH", CultureInfo.InvariantCulture);
        var signature = string.Create(CultureInfo.InvariantCulture,
            $"{health.NegativeWallets}:{health.MismatchedWallets}:{health.HugeMutations}");
        var checkpoint = $"economy_anomaly:{signature}";
        if (await IsSentAsync(checkpoint, hourKey, ct)) return;

        var text = "🚨 <b>Economy anomaly</b>\n\n" + string.Join("\n", issues.Select(x => "• " + x));
        await EnqueueForAdminsAsync(text, $"economy-alert:{hourKey}:{signature}", ct);
        await MarkSentAsync(checkpoint, hourKey, ct);
        analytics.Track("meta_analytics", "economy_anomaly_alert", new Dictionary<string, object?>
        {
            ["negative_wallets"] = health.NegativeWallets,
            ["mismatched_wallets"] = health.MismatchedWallets,
            ["huge_mutations"] = health.HugeMutations,
            ["largest_mutation"] = health.LargestMutation,
            ["outcome"] = "queued",
        });
    }

    private async Task EnqueueForAdminsAsync(string text, string dedupePrefix, CancellationToken ct)
    {
        foreach (var adminId in _bot.Admins.Distinct())
            await outbox.EnqueueAsync(new TelegramOutboxMessage(
                adminId, text, $"{dedupePrefix}:{adminId}", OutboundParseMode.Html), ct);
    }

    private async Task<bool> IsSentAsync(string reportKey, string periodKey, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM operations_report_checkpoint WHERE report_key = @reportKey AND period_key = @periodKey)",
            new { reportKey, periodKey }, cancellationToken: ct));
    }

    private async Task MarkSentAsync(string reportKey, string periodKey, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO operations_report_checkpoint (report_key, period_key, updated_at)
            VALUES (@reportKey, @periodKey, now())
            ON CONFLICT (report_key) DO UPDATE SET period_key = EXCLUDED.period_key, updated_at = now()
            """, new { reportKey, periodKey }, cancellationToken: ct));
    }

    private static DateTimeOffset StartOfIsoWeek(DateTime utc)
    {
        var days = ((int)utc.DayOfWeek + 6) % 7;
        return new DateTimeOffset(utc.Date.AddDays(-days), TimeSpan.Zero);
    }

    private sealed record WeeklySummary(
        long ActiveUsers, long NewWallets, long Stake, long Payout, long Updates,
        long DispatchFailures, long DeliveryFailures, long ProtectionChanges);
    private sealed record GameVolume(string Module, long Stake);
    private sealed record EconomyHealth(long NegativeWallets, long MismatchedWallets, long LargestMutation, long HugeMutations);

    [LoggerMessage(LogLevel.Error, "meta.operations_reporting.failed")]
    partial void LogReportingFailed(Exception exception);
}
