using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.Host.Pages.Admin;

public sealed class ChallengeChatSummaryRow
{
    public long ChatId { get; init; }
    public string ChatLabel { get; init; } = "";
    public int Total { get; init; }
    public int Pending { get; init; }
    public int Completed { get; init; }
    public long TotalPot { get; init; }
    public DateTimeOffset? LastCreatedAt { get; init; }
}
