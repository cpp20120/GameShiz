using BotFramework.Host;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.Host.Pages.Admin;

public sealed record GroupRow(
    long ChatId,
    string ChatType,
    string? Title,
    string? Username,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt);
