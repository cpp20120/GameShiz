using BotFramework.Host.Contracts.Economics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.AdminBff.Pages;

public sealed class WalletsModel(IWalletReadService wallets, BotFramework.Contracts.Operations.IOperationsAdminService operations) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Query { get; set; }
    public IReadOnlyList<WalletAccount> Rows { get; private set; } = [];
    public string? Error { get; private set; }
    public bool CanMutate => HttpContext.Session.IsSuperAdmin();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!HttpContext.Session.IsAuthenticated()) return RedirectToPage("/Login");
        if (!HttpContext.Session.IsSuperAdmin()) return Forbid();
        await LoadAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(long userId, long scopeId, int delta, string operationId, CancellationToken ct)
    {
        if (!HttpContext.Session.IsAuthenticated()) return RedirectToPage("/Login");
        if (delta == 0) return RedirectToPage(new { Query });
        if (!operationId.StartsWith("admin-bff:", StringComparison.Ordinal) || operationId.Length != 42)
            return BadRequest();
        var result = await operations.AdjustWalletAsync(userId, scopeId, delta, operationId,
            HttpContext.Session.ActorId(), HttpContext.Session.ActorName(), ct);
        if (!result.Success)
        {
            Error = "Adjustment was rejected by Wallet.";
            await LoadAsync(ct);
            return Page();
        }
        return RedirectToPage(new { Query });
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        try
        {
            var all = await wallets.ListAsync(ct);
            var query = Query?.Trim();
            Rows = all.Where(x => string.IsNullOrEmpty(query)
                || x.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture).Contains(query, StringComparison.OrdinalIgnoreCase)
                || x.BalanceScopeId.ToString(System.Globalization.CultureInfo.InvariantCulture).Contains(query, StringComparison.OrdinalIgnoreCase)
                || x.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.UpdatedAt).Take(200).ToList();
        }
        catch (Exception ex)
        {
            Error = $"Wallet service unavailable: {ex.GetType().Name}";
        }
    }
}
