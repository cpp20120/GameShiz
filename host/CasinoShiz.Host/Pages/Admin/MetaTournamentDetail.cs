using BotFramework.Host;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.Host.Pages.Admin;

public sealed record MetaTournamentDetail(
    long Id,
    long SeasonId,
    string SeasonName,
    long ChatId,
    string GameKey,
    string Type,
    string Status,
    int EntryFee,
    int MaxPlayers,
    long CreatedBy,
    DateTimeOffset CreatedAt,
    int PlayerCount,
    long PrizePool);
