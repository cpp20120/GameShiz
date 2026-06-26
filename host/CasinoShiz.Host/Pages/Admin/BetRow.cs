using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.Host.Pages.Admin;

public sealed record BetRow(string Game, long UserId, int Amount, long ChatId, string? Note, DateTimeOffset CreatedAt);
