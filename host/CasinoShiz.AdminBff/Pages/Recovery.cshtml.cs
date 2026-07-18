using BotFramework.Contracts.Operations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.AdminBff.Pages;

public sealed class RecoveryModel(IOperationsAdminService operations) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? EventType { get; set; }
    [BindProperty(SupportsGet = true)] public string? OutboxStatus { get; set; }
    public IReadOnlyList<OperationFailure> Failures { get; private set; } = [];
    public IReadOnlyList<OperationOutbox> Outbox { get; private set; } = [];
    public IReadOnlyList<OperationJob> Jobs { get; private set; } = [];
    public bool CanRetry => HttpContext.Session.IsSuperAdmin();
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!HttpContext.Session.IsAuthenticated()) return RedirectToPage("/Login");
        try
        {
            var failuresTask = operations.ListFailuresAsync(100, Normalize(EventType), ct);
            var outboxTask = operations.ListOutboxAsync(100, Normalize(OutboxStatus), ct);
            var jobsTask = operations.ListJobsAsync(ct);
            await Task.WhenAll(failuresTask, outboxTask, jobsTask);
            Failures = await failuresTask;
            Outbox = await outboxTask;
            Jobs = await jobsTask;
        }
        catch (Exception ex) { Error = $"Operations service unavailable: {ex.GetType().Name}"; }
        return Page();
    }

    public async Task<IActionResult> OnPostRetryEventAsync(long id, CancellationToken ct) =>
        await MutateAsync(() => operations.RetryEventAsync(id, HttpContext.Session.ActorId(), HttpContext.Session.ActorName(), ct), ct);

    public async Task<IActionResult> OnPostRescheduleOutboxAsync(long id, CancellationToken ct) =>
        await MutateAsync(() => operations.RescheduleOutboxAsync(id, HttpContext.Session.ActorId(), HttpContext.Session.ActorName(), ct), ct);

    private async Task<IActionResult> MutateAsync(Func<Task<OperationMutationResult>> action, CancellationToken ct)
    {
        if (!HttpContext.Session.IsAuthenticated()) return RedirectToPage("/Login");
        if (!HttpContext.Session.IsSuperAdmin()) return Forbid();
        try
        {
            var result = await action();
            TempData[result.Success ? "flash" : "error-message"] = result.Message;
        }
        catch (Exception ex) { TempData["error-message"] = $"Operations service error: {ex.GetType().Name}"; }
        return RedirectToPage(new { EventType, OutboxStatus });
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
