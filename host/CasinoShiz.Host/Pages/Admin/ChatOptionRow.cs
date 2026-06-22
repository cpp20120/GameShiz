using BotFramework.Host;
using Dapper;
using Games.Meta;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.Host.Pages.Admin;

public sealed record ChatOptionRow(long ChatId, string Label);
