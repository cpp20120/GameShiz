using System.Globalization;
using BotFramework.Host.Admin.Execution;
using BotFramework.Sdk.Admin.Execution;
using BotFramework.Sdk.Admin.Effects;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.Host.Pages.Admin;

public sealed class UsersModel(
    INpgsqlConnectionFactory connections,
    IAdminEffectExecutor effects) : PageModel
{
    public IReadOnlyList<UserRow> Users { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    public string? Flash { get; set; }
    public bool FlashError { get; set; }
    public AdminSession? Actor { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Actor = HttpContext.Session.GetAdminSession();
        await LoadAsync(ct);
    }

    public async Task<IActionResult> OnPostSetAsync(
        long userId, long balanceScopeId, int coins, string operationId, CancellationToken ct)
    {
        var actor = HttpContext.Session.GetAdminSession();
        if (actor?.Role != AdminRole.SuperAdmin)
            return StatusCode(403);

        if (!IsValidOperationId(operationId))
        {
            TempData["FlashError"] = true;
            TempData["Flash"] = "Invalid operation id.";
            return RedirectToPage(new { q = Q });
        }

        var newCoins = await ApplyAdminSetAsync(
            actor,
            userId,
            balanceScopeId,
            coins,
            "admin.set",
            operationId,
            "users.set_coins",
            ct);

        TempData["Flash"] = string.Create(CultureInfo.InvariantCulture, $"User {userId} scope {balanceScopeId} → {newCoins} coins");
        return RedirectToPage(new { q = Q });
    }

    public async Task<IActionResult> OnPostAdjustAsync(
        long userId, long balanceScopeId, int delta, string operationId, CancellationToken ct)
    {
        var actor = HttpContext.Session.GetAdminSession();
        if (actor?.Role != AdminRole.SuperAdmin)
            return StatusCode(403);

        if (delta == 0)
        {
            TempData["FlashError"] = "Delta must be non-zero";
            return RedirectToPage(new { q = Q });
        }

        if (!IsValidOperationId(operationId))
        {
            TempData["FlashError"] = true;
            TempData["Flash"] = "Invalid operation id.";
            return RedirectToPage(new { q = Q });
        }

        var newCoins = await ApplyAdminAdjustmentAsync(
            actor,
            userId,
            balanceScopeId,
            delta,
            "admin.adjust",
            operationId,
            "users.adjust_coins",
            ct);

        TempData["Flash"] =
            $"User {userId} scope {balanceScopeId}: {(delta > 0 ? "+" : "")}{delta} → {newCoins} coins";
        return RedirectToPage(new { q = Q });
    }

    private async Task<int> ApplyAdminAdjustmentAsync(
        AdminSession actor,
        long userId,
        long balanceScopeId,
        int delta,
        string reason,
        string operationId,
        string action,
        CancellationToken ct)
    {
        return await effects.ExecuteAsync(
            new AdminExecutionEnvelope(
                new(actor.UserId, actor.Name),
                action,
                new { targetUserId = userId, balanceScopeId, delta, operationId }),
            new AdminEffectPlan<int>(
                0,
                [new WalletAdjustmentAdminEffect(
                    userId,
                    balanceScopeId,
                    delta,
                    reason,
                    operationId,
                    AllowNegative: true)],
                outputs => (int)outputs["balance"]!),
            ct).ConfigureAwait(false);
    }

    private async Task<int> ApplyAdminSetAsync(
        AdminSession actor,
        long userId,
        long balanceScopeId,
        int balance,
        string reason,
        string operationId,
        string action,
        CancellationToken ct)
    {
        return await effects.ExecuteAsync(
            new AdminExecutionEnvelope(
                new(actor.UserId, actor.Name),
                action,
                new { targetUserId = userId, balanceScopeId, balance, operationId }),
            new AdminEffectPlan<int>(
                0,
                [new WalletSetAdminEffect(
                    userId,
                    balanceScopeId,
                    balance,
                    reason,
                    operationId,
                    AllowNegative: true)],
                outputs => (int)outputs["balance"]!),
            ct).ConfigureAwait(false);
    }

    private static bool IsValidOperationId(string? operationId) =>
        !string.IsNullOrWhiteSpace(operationId)
        && operationId.Length <= 128
        && Guid.TryParse(operationId, out _);

    private async Task LoadAsync(CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        const string sql = """
            SELECT telegram_user_id AS UserId, balance_scope_id AS BalanceScopeId, display_name AS DisplayName,
                   coins AS Coins, created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM users
            WHERE (@q = '' OR display_name ILIKE '%' || @q || '%'
                  OR telegram_user_id::text = @q
                  OR balance_scope_id::text = @q)
            ORDER BY coins DESC
            LIMIT 500
            """;
        var rows = await conn.QueryAsync<UserRow>(new CommandDefinition(sql, new { q = Q ?? "" }, cancellationToken: ct));
        Users = rows.ToList();

        Flash = TempData["Flash"] as string;
        FlashError = TempData["FlashError"] is not null;
    }
}
