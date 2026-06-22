using BotFramework.Host;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.Host.Pages.Admin;

public sealed record HistoryRow(
    long Id,
    string Game,
    string EventType,
    DateTimeOffset OccurredAt,
    long UserId,
    int Bet,
    int Payout,
    int? Multiplier,
    int? Face,
    long ChatScopeId,
    string PayloadJson);
