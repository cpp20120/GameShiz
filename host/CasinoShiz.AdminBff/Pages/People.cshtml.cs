using Games.Admin.Infrastructure.Persistence;
using BotFramework.Host.Contracts.Economics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.AdminBff.Pages;

public sealed class PeopleModel(IWalletReadService wallets, IChatsStore chats) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    public IReadOnlyList<AdminPersonRow> People { get; private set; } = [];
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!HttpContext.Session.IsAuthenticated()) return RedirectToPage("/Login");
        try
        {
            var walletsTask = wallets.ListAsync(ct);
            var chatsTask = chats.ListChatsAsync(null, 0, ct);
            await Task.WhenAll(walletsTask, chatsTask);
            var accounts = await walletsTask;
            var chatLabels = (await chatsTask).ToDictionary(x => x.ChatId, ChatLabel);
            var query = (Q ?? string.Empty).Trim();

            People = accounts
                .GroupBy(x => x.UserId)
                .Select(group =>
                {
                    var scopes = group.OrderByDescending(x => x.UpdatedAt).Select(account =>
                    {
                        var label = chatLabels.TryGetValue(account.BalanceScopeId, out var known)
                            ? known : $"scope {account.BalanceScopeId}";
                        return new AdminPersonScope(account.BalanceScopeId, label, account.Coins, account.UpdatedAt);
                    }).ToList();
                    var latest = group.OrderByDescending(x => x.UpdatedAt).First();
                    return new AdminPersonRow(group.Key, latest.DisplayName, group.Count(),
                        group.Sum(x => (long)x.Coins), latest.UpdatedAt, scopes);
                })
                .Where(row => query.Length == 0
                    || row.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture) == query
                    || row.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || row.Scopes.Any(scope => scope.Label.Contains(query, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(x => x.LastActive)
                .Take(500)
                .ToList();
        }
        catch (Exception ex)
        {
            Error = $"Identity/wallet services unavailable: {ex.GetType().Name}";
        }
        return Page();
    }

    private static string ChatLabel(KnownChatRow chat)
    {
        var name = string.IsNullOrWhiteSpace(chat.Title) ? chat.Username : chat.Title;
        return string.IsNullOrWhiteSpace(name) ? $"{chat.ChatType} {chat.ChatId}" : $"{name} · {chat.ChatType}";
    }
}

public sealed record AdminPersonRow(long UserId, string DisplayName, int WalletCount, long TotalCoins,
    DateTimeOffset LastActive, IReadOnlyList<AdminPersonScope> Scopes);
public sealed record AdminPersonScope(long ScopeId, string Label, int Coins, DateTimeOffset LastActive);
