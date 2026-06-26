using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.Host.Pages.Admin;

public sealed record MetaTournamentPlayerRow(
    long TournamentId,
    long UserId,
    string DisplayName,
    string Status,
    DateTimeOffset JoinedAt);
