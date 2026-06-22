using BotFramework.Host;
using Dapper;
using Games.Meta;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.Host.Pages.Admin;

public sealed record AdminEventRow(
    string Source,
    long Id,
    string? StreamId,
    long? Version,
    string EventType,
    string PayloadJson,
    DateTimeOffset OccurredAt);
