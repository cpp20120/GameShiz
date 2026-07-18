using BotFramework.Contracts.Operations;
using BotFramework.Host.Contracts.Economics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace CasinoShiz.AdminBff.Pages;
public sealed class IndexModel(IWalletAnalyticsService wallet, IOperationsAdminService operations) : PageModel
{
    public WalletEconomyTotals Totals { get; private set; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0);
    public WalletEngagement Engagement { get; private set; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0);
    public WalletIntegrity Integrity { get; private set; } = new(0, 0, 0, 0, 0, 0, 0);
    public LedgerHealth Ledger { get; private set; } = new(0, 0, 0, 0, 0, 0, 0, DateTimeOffset.MinValue, 0);
    public WalletSocialActivity Social { get; private set; } = new(0, 0, 0);
    public WalletPeriodSummary Period { get; private set; } = new(0, 0, 0, []);
    public IReadOnlyList<OperationJob> Jobs { get; private set; } = [];
    public IReadOnlyList<OperationFailure> Failures { get; private set; } = [];
    public IReadOnlyList<OperationOutbox> Outbox { get; private set; } = [];
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!HttpContext.Session.IsAuthenticated()) return RedirectToPage("/Login");
        try
        {
            var totalsTask = wallet.GetTotalsAsync(ct);
            var engagementTask = wallet.GetEngagementAsync(ct);
            var integrityTask = wallet.GetIntegrityAsync(ct);
            var ledgerTask = wallet.GetLedgerHealthAsync(24 * 60, ct);
            var socialTask = wallet.GetSocialActivityAsync(DateTimeOffset.UtcNow.AddDays(-1), ct);
            var periodTask = wallet.GetPeriodSummaryAsync(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow, 8, ct);
            var jobsTask = operations.ListJobsAsync(ct);
            var failuresTask = operations.ListFailuresAsync(8, null, ct);
            var outboxTask = operations.ListOutboxAsync(8, null, ct);
            await Task.WhenAll(totalsTask, engagementTask, integrityTask, ledgerTask, socialTask, periodTask,
                jobsTask, failuresTask, outboxTask);
            Totals = await totalsTask;
            Engagement = await engagementTask;
            Integrity = await integrityTask;
            Ledger = await ledgerTask;
            Social = await socialTask;
            Period = await periodTask;
            Jobs = await jobsTask;
            Failures = await failuresTask;
            Outbox = await outboxTask;
        }
        catch (Exception ex)
        {
            Error = $"Services unavailable: {ex.GetType().Name}";
        }
        return Page();
    }
}
