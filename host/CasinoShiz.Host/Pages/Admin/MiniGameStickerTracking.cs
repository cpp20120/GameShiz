using BotFramework.Host;
using BotFramework.Host.Composition;
using BotFramework.Sdk;
using Dapper;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.Host.Pages.Admin;

public sealed class MiniGameStickerTracking
{
    public string GameId { get; init; } = "";
    public string Label { get; init; } = "";
    public int Plays { get; init; }
    public int PlaysToday { get; init; }
    public DateTimeOffset? LastPlayedAt { get; init; }
}
