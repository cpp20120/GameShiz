using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.Host.Pages.Admin;

public sealed record MetaTournamentMatchRow(
    long Id,
    long TournamentId,
    int Round,
    int MatchIndex,
    string Status,
    long? Player1UserId,
    string? Player1DisplayName,
    long? Player2UserId,
    string? Player2DisplayName,
    long? VictorUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
