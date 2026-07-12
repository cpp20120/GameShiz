using BotFramework.Scheduling.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.Host.Pages.Admin;

public sealed class QuartzModel(IGameSchedulerStatusReader scheduler) : PageModel
{
    public IReadOnlyList<GameScheduledJobStatus> Jobs { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (HttpContext.Session.GetAdminSession() is null)
            return RedirectToPage("/Admin/Login");

        Jobs = await scheduler.SnapshotAsync(ct);
        return Page();
    }
}
