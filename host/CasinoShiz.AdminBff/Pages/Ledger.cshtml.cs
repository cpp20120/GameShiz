using BotFramework.Host.Contracts.Economics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.AdminBff.Pages;

public sealed class LedgerModel(
    IWalletAnalyticsService analytics,
    IEconomicsService economics) : PageModel
{
    [BindProperty(SupportsGet = true)] public long? U { get; set; }
    [BindProperty(SupportsGet = true)] public long? S { get; set; }
    public IReadOnlyList<WalletLedgerEntry> Rows { get; private set; } = [];
    public LedgerHealth Health { get; private set; } = new(0, 0, 0, 0, 0, 0, 0, DateTimeOffset.MinValue, 0);
    public bool CanRevert => HttpContext.Session.IsSuperAdmin();
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!HttpContext.Session.IsAuthenticated()) return RedirectToPage("/Login");
        try
        {
            var rowsTask = analytics.ListLedgerAsync(U, S, 500, ct);
            var healthTask = analytics.GetLedgerHealthAsync(24 * 60, ct);
            await Task.WhenAll(rowsTask, healthTask);
            Rows = await rowsTask;
            Health = await healthTask;
        }
        catch (Exception ex)
        {
            Error = $"Wallet service unavailable: {ex.GetType().Name}";
        }
        return Page();
    }

    public async Task<IActionResult> OnPostRevertAsync(long ledgerId, CancellationToken ct)
    {
        if (!HttpContext.Session.IsAuthenticated()) return RedirectToPage("/Login");
        if (!HttpContext.Session.IsSuperAdmin()) return Forbid();

        try
        {
            var result = await economics.RevertLedgerEntryAsync(ledgerId, ct);
            var message = result.Status switch
            {
                LedgerRevertStatus.Ok => $"Ledger line #{ledgerId} reverted. Balance: {result.NewBalance:N0}.",
                LedgerRevertStatus.AlreadyReverted => $"Line #{ledgerId} was already reverted.",
                LedgerRevertStatus.NotFound => $"Line #{ledgerId} was not found.",
                LedgerRevertStatus.UserMissing => $"User for line #{ledgerId} is missing.",
                LedgerRevertStatus.NoEffect => $"Line #{ledgerId} has no effect to reverse.",
                LedgerRevertStatus.CorrectionOutOfRange => $"Line #{ledgerId} cannot be reverted automatically.",
                _ => $"Revert of line #{ledgerId} failed."
            };
            TempData[result.Status == LedgerRevertStatus.Ok || result.Status == LedgerRevertStatus.NoEffect
                ? "flash" : "error-message"] = message;
        }
        catch (Exception ex)
        {
            TempData["error-message"] = $"Wallet service error: {ex.GetType().Name}";
        }

        return RedirectToPage(new { U, S });
    }
}
