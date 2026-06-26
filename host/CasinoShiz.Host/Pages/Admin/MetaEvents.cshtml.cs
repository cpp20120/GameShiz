using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.Host.Pages.Admin;

public sealed class MetaEventsModel(
    IMetaHistoryStore history,
    IMetaReconstructionStore reconstruction,
    IAdminAuditLog audit) : PageModel
{
    public const string ConfirmToken = "META_CORE_REFRESH";

    public IReadOnlyList<MetaHistoryEvent> Events { get; private set; } = [];
    public MetaHistoryStats Stats { get; private set; } = new(0, 0, 0, FirstEventAt: null, LastEventAt: null);
    public MetaReconstructionSummary Reconstruction { get; private set; } = new(0, 0, 0, 0, 0, 0);
    public AdminSession? Actor { get; private set; }
    public bool CanRefreshCore { get; private set; }
    public string? Flash { get; private set; }
    public bool FlashError { get; private set; }

    [BindProperty(SupportsGet = true)] public string? EventType { get; set; }
    [BindProperty(SupportsGet = true)] public string? AggregateType { get; set; }
    [BindProperty(SupportsGet = true)] public string? AggregateId { get; set; }
    [BindProperty(SupportsGet = true)] public string? ChatId { get; set; }
    [BindProperty(SupportsGet = true)] public string? UserId { get; set; }
    [BindProperty(SupportsGet = true)] public int Limit { get; set; } = 200;

    [BindProperty] public string? Confirm { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var init = await InitAsync(ct);
        return init ?? Page();
    }

    public async Task<IActionResult> OnPostRefreshCoreAsync(CancellationToken ct)
    {
        Actor = HttpContext.Session.GetAdminSession();
        if (Actor is null) return RedirectToPage("/Admin/Login");
        if (Actor.Role != AdminRole.SuperAdmin) return Forbid();

        if (!string.Equals(Confirm, ConfirmToken, StringComparison.Ordinal))
        {
            TempData["FlashError"] = true;
            TempData["Flash"] = $"Confirmation token mismatch. Type {ConfirmToken}.";
            return RedirectToPage();
        }

        var result = await reconstruction.ReconstructCoreAsync(ct);
        await audit.LogAsync(Actor.UserId, Actor.Name, "meta.refresh_core", new
        {
            result.BeforePlayers,
            result.BeforeAchievements,
            result.RebuiltPlayers,
            result.RebuiltAchievements,
        }, ct);

        TempData["Flash"] = string.Create(CultureInfo.InvariantCulture, $"Meta core refreshed. Players {result.BeforePlayers} -> {result.RebuiltPlayers}; achievements {result.BeforeAchievements} -> {result.RebuiltAchievements}.");
        return RedirectToPage();
    }

    private async Task<IActionResult?> InitAsync(CancellationToken ct)
    {
        Actor = HttpContext.Session.GetAdminSession();
        if (Actor is null) return RedirectToPage("/Admin/Login");

        CanRefreshCore = Actor.Role == AdminRole.SuperAdmin;
        Flash = TempData["Flash"] as string;
        FlashError = TempData["FlashError"] is not null;

        long? chatId = long.TryParse(ChatId, System.Globalization.CultureInfo.InvariantCulture, out var cid) ? cid : null;
        long? userId = long.TryParse(UserId, System.Globalization.CultureInfo.InvariantCulture, out var uid) ? uid : null;
        Limit = Math.Clamp(Limit, 1, 1000);

        Stats = await history.GetStatsAsync(ct);
        Reconstruction = await reconstruction.GetSummaryAsync(ct);
        Events = await history.ListAsync(EventType, AggregateType, AggregateId, chatId, userId, Limit, ct);
        return null;
    }
}
