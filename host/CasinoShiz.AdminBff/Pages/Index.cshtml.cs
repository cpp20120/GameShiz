using BotFramework.Contracts.Operations;
using BotFramework.Host.Contracts.Economics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace CasinoShiz.AdminBff.Pages;
public sealed class IndexModel(IWalletAnalyticsService wallet, IOperationsAdminService operations) : PageModel
{
    public WalletEconomyTotals Totals { get; private set; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0);
    public WalletEngagement Engagement { get; private set; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0);
    public IReadOnlyList<OperationJob> Jobs { get; private set; } = [];
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!HttpContext.Session.IsAuthenticated()) return RedirectToPage("/Login");
        try
        {
            Totals = await wallet.GetTotalsAsync(ct);
            Engagement = await wallet.GetEngagementAsync(ct);
            Jobs = await operations.ListJobsAsync(ct);
        }
        catch (Exception ex)
        {
            Error = $"Services unavailable: {ex.GetType().Name}";
        }
        return Page();
    }
}
