using BotFramework.Contracts.Games;
using BotFramework.Contracts.Operations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.AdminBff.Pages;

public sealed class GameAvailabilityModel(IOperationsAdminService operations) : PageModel
{
    [BindProperty(SupportsGet = true)] public long? ChatId { get; set; }
    [BindProperty] public string GameId { get; set; } = "";
    [BindProperty] public bool Enabled { get; set; }
    [BindProperty] public string Reason { get; set; } = "";
    public IReadOnlyList<GameAvailability> Overrides { get; private set; } = [];
    public string? Error { get; private set; }
    public bool CanMutate => HttpContext.Session.IsSuperAdmin();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!HttpContext.Session.IsAuthenticated()) return RedirectToPage("/Login");
        try { Overrides = await operations.ListGameAvailabilityAsync(ChatId, ct); }
        catch (Exception exception) { Error = $"Availability service unavailable: {exception.GetType().Name}"; }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!HttpContext.Session.IsAuthenticated()) return RedirectToPage("/Login");
        if (!HttpContext.Session.IsSuperAdmin()) return Forbid();
        if (ChatId is null || string.IsNullOrWhiteSpace(GameId) || string.IsNullOrWhiteSpace(Reason))
        {
            ModelState.AddModelError(string.Empty, "Chat ID, game ID and reason are required.");
            Overrides = await operations.ListGameAvailabilityAsync(ChatId, ct);
            return Page();
        }
        var result = await operations.SetGameAvailabilityAsync(ChatId.Value, GameId.Trim(), Enabled,
            Reason.Trim(), HttpContext.Session.ActorId(), HttpContext.Session.ActorName(), ct);
        TempData[result.Success ? "flash" : "error-message"] = result.Message;
        return RedirectToPage(new { ChatId });
    }
}
