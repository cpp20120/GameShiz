using System.Text;
using System.Text.Json;
using BotFramework.Contracts.Operations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.AdminBff.Pages;

public sealed class AuditModel(IOperationsAdminService operations) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Actor { get; set; }
    [BindProperty(SupportsGet = true)] public string? Action { get; set; }
    [BindProperty(SupportsGet = true)] public string? Details { get; set; }
    [BindProperty(SupportsGet = true)] public DateTimeOffset? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateTimeOffset? Until { get; set; }
    public IReadOnlyList<OperationAudit> Rows { get; private set; } = [];
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!HttpContext.Session.IsAuthenticated())
            return RedirectToPage("/Login");
        try
        {
            Rows = await LoadAsync(200, ct);
        }
        catch (Exception ex)
        {
            Error = $"Operations service unavailable: {ex.GetType().Name}";
        }
        return Page();
    }

    public async Task<IActionResult> OnGetJsonAsync(CancellationToken ct)
    {
        if (!HttpContext.Session.IsAuthenticated())
            return RedirectToPage("/Login");
        return File(JsonSerializer.SerializeToUtf8Bytes(await LoadAsync(1000, ct)),
            "application/json", "admin-audit.json");
    }

    public async Task<IActionResult> OnGetCsvAsync(CancellationToken ct)
    {
        if (!HttpContext.Session.IsAuthenticated())
            return RedirectToPage("/Login");
        var builder = new StringBuilder("id,actor_id,actor_name,action,details,occurred_at\r\n");
        foreach (var row in await LoadAsync(1000, ct))
        {
            builder.Append(row.Id).Append(',').Append(row.ActorId).Append(',')
                .Append(Quote(row.ActorName)).Append(',').Append(Quote(row.Action)).Append(',')
                .Append(Quote(row.DetailsJson)).Append(',').Append(row.OccurredAt.ToString("O")).Append("\r\n");
        }
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
        return File(bytes, "text/csv", "admin-audit.csv");
    }

    private Task<IReadOnlyList<OperationAudit>> LoadAsync(int limit, CancellationToken ct) =>
        operations.ListAuditAsync(limit, Normalize(Actor), Normalize(Action), Normalize(Details), From, Until, ct);

    private static string Quote(string value) => '"' + value.Replace("\"", "\"\"") + '"';
    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
