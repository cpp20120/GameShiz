using BotFramework.Host;
using BotFramework.Sdk;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.Host.Pages.Admin;

public sealed record LedgerRow(
    long Id,
    long TelegramUserId,
    long BalanceScopeId,
    int Delta,
    int BalanceAfter,
    string Reason,
    DateTimeOffset CreatedAt);
