using Dapper;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.Host.Pages.Admin;

public sealed class IndexModel(
    INpgsqlConnectionFactory connections,
    IEnumerable<IModule> modules,
    IBackgroundJobStatusService backgroundJobs) : PageModel
{
    public int PeopleCount { get; private set; }
    public int WalletRowCount { get; private set; }
    public long TotalCoins { get; private set; }
    public int EventCount { get; private set; }
    public int PendingBets { get; private set; }
    public CurrentEconomySnapshot Economy { get; private set; } = null!;
    public IReadOnlyList<IModule> Modules { get; } = modules.ToList();
    public IReadOnlyList<ModuleCount> EventsByModule { get; private set; } = [];
    public IReadOnlyList<MiniGameStickerTracking> StickerGames { get; private set; } = [];
    public IReadOnlyList<BackgroundJobStatusSnapshot> BackgroundJobs { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);

        PeopleCount = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(DISTINCT telegram_user_id)::int FROM users", cancellationToken: ct));
        WalletRowCount = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*)::int FROM users", cancellationToken: ct));
        EventCount = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*)::int FROM event_log", cancellationToken: ct));

        Economy = await conn.QuerySingleAsync<CurrentEconomySnapshot>(new CommandDefinition(
            """
            WITH pending AS (
                SELECT amount::bigint FROM darts_rounds
                UNION ALL SELECT amount::bigint FROM dicecube_bets
                UNION ALL SELECT amount::bigint FROM basketball_bets
                UNION ALL SELECT amount::bigint FROM bowling_bets
                UNION ALL SELECT amount::bigint FROM football_bets
                UNION ALL SELECT bet::bigint FROM blackjack_hands
                UNION ALL SELECT amount::bigint FROM horse_bets
            ),
            wallet_stats AS (
                SELECT COALESCE(sum(coins), 0)::bigint AS WalletSupply,
                       count(*)::int AS Wallets,
                       count(*) FILTER (WHERE coins > 0)::int AS FundedWallets,
                       COALESCE(percentile_disc(0.5) WITHIN GROUP (ORDER BY coins), 0)::bigint AS MedianBalance,
                       COALESCE(max(coins), 0)::bigint AS RichestBalance
                FROM users
            ),
            pending_stats AS (
                SELECT COALESCE(sum(amount), 0)::bigint AS PendingStake,
                       count(*)::int AS PendingBets
                FROM pending
            ),
            flows AS (
                SELECT COALESCE(sum(delta) FILTER (WHERE delta > 0), 0)::bigint AS Credits24H,
                       COALESCE(-sum(delta) FILTER (WHERE delta < 0), 0)::bigint AS Debits24H,
                       COALESCE(sum(delta), 0)::bigint AS Net24H,
                       count(DISTINCT telegram_user_id)::int AS ActiveUsers24H
                FROM economics_ledger
                WHERE created_at >= now() - interval '24 hours'
            )
            SELECT clock_timestamp() AS CapturedAt,
                   w.WalletSupply,
                   p.PendingStake,
                   p.PendingBets,
                   w.Wallets,
                   w.FundedWallets,
                   w.MedianBalance,
                   w.RichestBalance,
                   f.Credits24H,
                   f.Debits24H,
                   f.Net24H,
                   f.ActiveUsers24H
            FROM wallet_stats w CROSS JOIN pending_stats p CROSS JOIN flows f
            """,
            cancellationToken: ct));
        TotalCoins = Economy.WalletSupply;
        PendingBets = Economy.PendingBets;

        var rows = await conn.QueryAsync<(string module, int cnt)>(new CommandDefinition("""
            SELECT split_part(event_type, '.', 1) AS module, count(*)::int AS cnt
            FROM event_log
            GROUP BY 1
            ORDER BY 2 DESC
            """, cancellationToken: ct));
        EventsByModule = rows.Select(r => new ModuleCount(r.module, r.cnt)).ToList();

        var stickerRows = await conn.QueryAsync<MiniGameStickerTracking>(new CommandDefinition("""
            WITH games(game_id, label, play_event) AS (
                VALUES
                    ('dice', 'slots', 'dice.roll_completed'),
                    ('dicecube', 'dicecube', 'dicecube.roll_completed'),
                    ('darts', 'darts', 'darts.throw_completed'),
                    ('football', 'football', 'football.throw_completed'),
                    ('basketball', 'basketball', 'basketball.throw_completed'),
                    ('bowling', 'bowling', 'bowling.roll_completed')
            )
            SELECT
                g.game_id AS GameId,
                g.label AS Label,
                count(e.id)::int AS Plays,
                count(e.id) FILTER (WHERE e.occurred_at >= current_date)::int AS PlaysToday,
                max(e.occurred_at) AS LastPlayedAt
            FROM games g
            LEFT JOIN event_log e ON e.event_type = g.play_event
            GROUP BY g.game_id, g.label
            ORDER BY count(e.id) DESC, g.game_id
            """, cancellationToken: ct));
        StickerGames = stickerRows.ToList();
        BackgroundJobs = backgroundJobs.Snapshot();
    }
}
