using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.Host.Pages.Admin;

public sealed record EconomyReasonRow(
    string Reason,
    int Count,
    long Credits,
    long Debits,
    long Net);
