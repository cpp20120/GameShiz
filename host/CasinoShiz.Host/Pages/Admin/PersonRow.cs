using BotFramework.Host;
using BotFramework.Sdk;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.Host.Pages.Admin;

/// <summary>
/// One row per Telegram user — a directory view. Wallets and balance edits live on /admin/users.
/// </summary>
public sealed record PersonRow(
    long UserId,
    string DisplayName,
    int WalletCount,
    DateTimeOffset LastActive);
