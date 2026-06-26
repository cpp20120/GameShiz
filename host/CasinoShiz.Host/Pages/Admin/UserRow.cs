using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.Host.Pages.Admin;

public sealed record UserRow(
    long UserId, long BalanceScopeId, string DisplayName, int Coins, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
