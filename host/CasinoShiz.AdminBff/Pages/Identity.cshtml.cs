using BotFramework.Contracts.Identity;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.AdminBff.Pages;

public sealed class IdentityModel(IPlayerDirectory players) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Query { get; set; }
    public PlayerIdentity? Identity { get; private set; }
    public string? Error { get; private set; }
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!HttpContext.Session.IsAuthenticated()) return RedirectToPage("/Login");
        if (string.IsNullOrWhiteSpace(Query)) return Page();
        try
        {
            Identity = long.TryParse(Query, CultureInfo.InvariantCulture, out var userId)
                ? await players.GetAsync(userId, ct)
                : await players.FindByUsernameAsync(Query, ct);
            if (Identity is null) Error = "Identity not found.";
        }
        catch (Exception ex)
        {
            Error = $"Identity service unavailable: {ex.GetType().Name}";
        }
        return Page();
    }
}
