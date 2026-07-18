using BotFramework.Host.Contracts.Economics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.AdminBff.Pages;

public sealed class MetaEconomyModel(IWalletAnalyticsService wallet) : PageModel
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

    public WalletEconomyTotals Totals { get; private set; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0);
    public WalletEngagement Engagement { get; private set; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0);
    public LedgerHealth Health { get; private set; } = new(0, 0, 0, 0, 0, 0, 0, DateTimeOffset.MinValue, 0);
    public IReadOnlyList<LedgerReasonVolume> Reasons { get; private set; } = [];
    public SimulationResult Simulation { get; private set; } = new(0, 0, 0, 0, 0, 0, 0);
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!HttpContext.Session.IsAuthenticated()) return RedirectToPage("/Login");
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

        try
        {
            var totalsTask = wallet.GetTotalsAsync(ct);
            var engagementTask = wallet.GetEngagementAsync(ct);
            var healthTask = wallet.GetLedgerHealthAsync(Days * 24 * 60, ct);
            var reasonsTask = wallet.ListReasonVolumesAsync(Days * 24 * 60, ct);
            await Task.WhenAll(totalsTask, engagementTask, healthTask, reasonsTask);
            Totals = await totalsTask;
            Engagement = await engagementTask;
            Health = await healthTask;
            Reasons = await reasonsTask;
        }
        catch (Exception ex)
        {
            Error = $"Wallet service unavailable: {ex.GetType().Name}";
        }
        Simulation = Simulate();
        return Page();
    }

    private SimulationResult Simulate()
    {
        var games = Players * Days * GamesPerPlayerPerDay;
        var stake = games * AverageStake;
        var gameSink = stake * (1m - AverageRtpPercent / 100m);
        var bonus = Players * Days * DailyBonusPerPlayer;
        var quests = Players * Days * QuestCoinsPerPlayerPerDay;
        var fees = TransferFeesPerDay * Days;
        var faucets = bonus + quests + SeasonRewardPool + ClanRewardPool;
        var sinks = gameSink + fees;
        return new SimulationResult(games, stake, faucets, sinks, faucets - sinks, bonus + quests, fees);
    }

    public sealed record SimulationResult(decimal Games, decimal Stake, decimal Faucets, decimal Sinks,
        decimal Net, decimal RecurringFaucets, decimal TransferFees);
}
