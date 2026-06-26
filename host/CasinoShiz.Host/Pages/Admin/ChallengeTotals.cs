using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.Host.Pages.Admin;

public sealed class ChallengeTotals
{
    public int Total { get; init; }
    public int Pending { get; init; }
    public int Accepted { get; init; }
    public int Completed { get; init; }
    public int Cancelled { get; init; }
    public long TotalPot { get; init; }
}
