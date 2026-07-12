using BotFramework.Host.TelegramOutbox;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.Host.Pages.Admin;

public sealed class RecoveryModel(
    IEventDispatchFailureStore failures,
    IEventDispatchRetryService eventRetry,
    ITelegramOutboxStore outbox,
    ITelegramOutboxMonitor outboxMonitor,
    IBackgroundJobStatusService backgroundJobs,
    IAdminAuditLog audit) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? EventType { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? OutboxStatus { get; set; }

    public AdminSession Actor { get; private set; } = null!;
    public bool CanRetry => Actor.Role == AdminRole.SuperAdmin;
    public IReadOnlyList<EventDispatchFailureRow> EventFailures { get; private set; } = [];
    public IReadOnlyList<TelegramOutboxAdminRow> OutboxRecords { get; private set; } = [];
    public TelegramOutboxSummary OutboxSummary { get; private set; } = new(0, 0, 0, 0, null);
    public IReadOnlyList<BackgroundJobStatusSnapshot> BackgroundJobs { get; private set; } = [];
    public string? Flash { get; private set; }
    public bool FlashError { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var actor = HttpContext.Session.GetAdminSession();
        if (actor is null) return RedirectToPage("/Admin/Login");

        Actor = actor;
        EventFailures = await failures.ListUnresolvedAsync(100, EventType, ct);
        OutboxRecords = await outbox.ListUnsentAsync(100, OutboxStatus, ct);
        OutboxSummary = await outboxMonitor.GetSummaryAsync(ct);
        BackgroundJobs = backgroundJobs.Snapshot();
        Flash = TempData["RecoveryFlash"] as string;
        FlashError = TempData["RecoveryFlashError"] is true;
        return Page();
    }

    public async Task<IActionResult> OnPostRetryEventAsync(long id, CancellationToken ct)
    {
        var actor = HttpContext.Session.GetAdminSession();
        if (actor?.Role != AdminRole.SuperAdmin) return StatusCode(403);

        EventDispatchRetryResult result;
        try
        {
            result = await eventRetry.RetryAsync(id, ct);
        }
        catch (Exception ex)
        {
            result = new EventDispatchRetryResult(false, false, $"dispatch failed: {ex.Message}");
        }

        await audit.LogAsync(actor.UserId, actor.Name, "recovery.event_retry",
            new { recordId = id, result = result.Success ? "succeeded" : "rejected", message = result.Message }, ct);
        SetFlash(result.Message ?? "event retry failed without details", !result.Success);
        return RedirectToPage(new { eventType = EventType, outboxStatus = OutboxStatus });
    }

    public async Task<IActionResult> OnPostRescheduleOutboxAsync(long id, CancellationToken ct)
    {
        var actor = HttpContext.Session.GetAdminSession();
        if (actor?.Role != AdminRole.SuperAdmin) return StatusCode(403);

        var result = await outbox.RescheduleNowAsync(id, ct);
        await audit.LogAsync(actor.UserId, actor.Name, "recovery.outbox_reschedule",
            new { recordId = id, result = result.Outcome.ToString(), message = result.Message }, ct);
        SetFlash(result.Message, !result.Success);
        return RedirectToPage(new { eventType = EventType, outboxStatus = OutboxStatus });
    }

    private void SetFlash(string message, bool isError)
    {
        TempData["RecoveryFlash"] = message;
        TempData["RecoveryFlashError"] = isError;
    }
}
