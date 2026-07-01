using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.Host.Pages.Admin;

public sealed class AuditModel(IAdminAuditReader reader) : PageModel
{
    private const int PageLimit = 200;
    private const int ExportLimit = 1_000;

    [BindProperty(SupportsGet = true)] public string? Actor { get; set; }
    [BindProperty(SupportsGet = true)] public string? Action { get; set; }
    [BindProperty(SupportsGet = true)] public string? Details { get; set; }
    [BindProperty(SupportsGet = true)] public DateTimeOffset? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateTimeOffset? To { get; set; }

    public IReadOnlyList<AdminAuditRow> Rows { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (HttpContext.Session.GetAdminSession() is null)
            return RedirectToPage("/Admin/Login");

        Rows = await LoadAsync(PageLimit, ct);
        return Page();
    }

    public async Task<IActionResult> OnGetCsvAsync(CancellationToken ct)
    {
        if (HttpContext.Session.GetAdminSession() is null)
            return RedirectToPage("/Admin/Login");

        var rows = await LoadAsync(ExportLimit, ct);
        var content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(AdminAuditExportFormatter.ToCsv(rows))).ToArray();
        return File(content, "text/csv; charset=utf-8", FileName("csv"));
    }

    public async Task<IActionResult> OnGetJsonAsync(CancellationToken ct)
    {
        if (HttpContext.Session.GetAdminSession() is null)
            return RedirectToPage("/Admin/Login");

        var rows = await LoadAsync(ExportLimit, ct);
        return File(AdminAuditExportFormatter.ToJson(rows), "application/json; charset=utf-8", FileName("json"));
    }

    private Task<IReadOnlyList<AdminAuditRow>> LoadAsync(int limit, CancellationToken ct) =>
        reader.ListAsync(limit, Actor, Action, Details, From, To, ct);

    private static string FileName(string extension) =>
        $"admin-audit-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.{extension}";
}
