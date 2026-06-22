using BotFramework.Host;
using Dapper;
using Games.Meta;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.Host.Pages.Admin;

public sealed record EventTypeCountRow(string Source, string EventType, int Count);
