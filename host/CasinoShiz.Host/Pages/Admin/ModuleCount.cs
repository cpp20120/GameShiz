using BotFramework.Host;
using BotFramework.Host.Composition;
using BotFramework.Sdk;
using Dapper;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.Host.Pages.Admin;

public sealed record ModuleCount(string Module, int Count);
