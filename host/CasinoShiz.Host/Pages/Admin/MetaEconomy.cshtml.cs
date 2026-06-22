using BotFramework.Host;
using BotFramework.Host.Services;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.Host.Pages.Admin;

public sealed class MetaEconomyModel(INpgsqlConnectionFactory connections) : PageModel
{
    [BindProperty(SupportsGet = true)] public int Days { get; set; } = 14;
    [BindProperty(SupportsGet = true)] public int Players { get; set; } = 40;
    [BindProperty(SupportsGet = true)] public decimal GamesPerPlayerPerDay { get; set; } = 8m;
    [BindProperty(SupportsGet = true)] public int AverageStake { get; set; } = 50;
    [BindProperty(SupportsGet = true)] public decimal AverageRtpPercent { get; set; } = 86m;
    [BindProperty(SupportsGet = true)] public decimal DailyBonusPerPlayer { get; set; } = 4m;
    [BindProperty(SupportsGet = true)] public decimal QuestCoinsPerPlayerPerDay { get; set; } = 20m;
    [BindProperty(SupportsGet = true)] public int SeasonRewardPool { get; set; } = 8_500;
    [BindProperty(SupportsGet = true)] public int ClanRewardPool { get; set; } = 17_500;
    [BindProperty(SupportsGet = true)] public decimal TransferFeesPerDay { get; set; } = 250m;

    public IReadOnlyList<EconomyReasonRow> ReasonRows { get; private set; } = [];
    public EconomyTotals Totals { get; private set; } = new(0, 0, 0, 0, 0);
    public EconomySimulation Simulation { get; private set; } = new(0, 0, 0, 0, 0, 0, 0);

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var actor = HttpContext.Session.GetAdminSession();
        if (actor is null) return RedirectToPage("/Admin/Login");

        Days = Math.Clamp(Days, 1, 365);
        Players = Math.Clamp(Players, 1, 100_000);
        GamesPerPlayerPerDay = Math.Clamp(GamesPerPlayerPerDay, 0, 10_000);
        AverageStake = Math.Clamp(AverageStake, 0, 1_000_000);
        AverageRtpPercent = Math.Clamp(AverageRtpPercent, 0, 500);
        DailyBonusPerPlayer = Math.Clamp(DailyBonusPerPlayer, 0, 1_000_000);
        QuestCoinsPerPlayerPerDay = Math.Clamp(QuestCoinsPerPlayerPerDay, 0, 1_000_000);
        SeasonRewardPool = Math.Clamp(SeasonRewardPool, 0, 100_000_000);
        ClanRewardPool = Math.Clamp(ClanRewardPool, 0, 100_000_000);
        TransferFeesPerDay = Math.Clamp(TransferFeesPerDay, 0, 100_000_000);

        await LoadActualAsync(ct);
        Simulation = Simulate();
        return Page();
    }

    private async Task LoadActualAsync(CancellationToken ct)
    {
        const string reasonSql = """
            SELECT reason AS Reason,
                   count(*)::int AS Count,
                   COALESCE(sum(CASE WHEN delta > 0 THEN delta ELSE 0 END), 0)::bigint AS Credits,
                   COALESCE(sum(CASE WHEN delta < 0 THEN -delta ELSE 0 END), 0)::bigint AS Debits,
                   COALESCE(sum(delta), 0)::bigint AS Net
            FROM economics_ledger
            WHERE created_at >= now() - (@days || ' days')::interval
            GROUP BY reason
            ORDER BY abs(COALESCE(sum(delta), 0)) DESC, reason ASC
            LIMIT 100
            """;

        const string totalsSql = """
            SELECT count(*)::int AS Rows,
                   COALESCE(sum(CASE WHEN delta > 0 THEN delta ELSE 0 END), 0)::bigint AS Credits,
                   COALESCE(sum(CASE WHEN delta < 0 THEN -delta ELSE 0 END), 0)::bigint AS Debits,
                   COALESCE(sum(delta), 0)::bigint AS Net,
                   COALESCE((SELECT sum(coins)::bigint FROM users), 0)::bigint AS CurrentSupply
            FROM economics_ledger
            WHERE created_at >= now() - (@days || ' days')::interval
            """;

        await using var conn = await connections.OpenAsync(ct);
        ReasonRows = (await conn.QueryAsync<EconomyReasonRow>(new CommandDefinition(
            reasonSql,
            new { days = Days },
            cancellationToken: ct))).ToList();
        Totals = await conn.QuerySingleAsync<EconomyTotals>(new CommandDefinition(
            totalsSql,
            new { days = Days },
            cancellationToken: ct));
    }

    private EconomySimulation Simulate()
    {
        var totalGames = Players * Days * GamesPerPlayerPerDay;
        var totalStake = totalGames * AverageStake;
        var gameSink = totalStake * (1m - AverageRtpPercent / 100m);
        var dailyBonus = Players * Days * DailyBonusPerPlayer;
        var questCoins = Players * Days * QuestCoinsPerPlayerPerDay;
        var transferFees = TransferFeesPerDay * Days;
        var faucets = dailyBonus + questCoins + SeasonRewardPool + ClanRewardPool;
        var sinks = gameSink + transferFees;
        var net = faucets - sinks;
        return new EconomySimulation(totalGames, totalStake, gameSink, faucets, sinks, transferFees, net);
    }
}

public sealed record EconomyReasonRow(
    string Reason,
    int Count,
    long Credits,
    long Debits,
    long Net);

public sealed record EconomyTotals(
    int Rows,
    long Credits,
    long Debits,
    long Net,
    long CurrentSupply);

public sealed record EconomySimulation(
    decimal TotalGames,
    decimal TotalStake,
    decimal GameSink,
    decimal Faucets,
    decimal Sinks,
    decimal TransferFees,
    decimal Net);
