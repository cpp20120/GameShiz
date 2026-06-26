using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.Host.Pages.Admin;

public sealed record EconomyTotals(
    int Rows,
    long Credits,
    long Debits,
    long Net,
    long CurrentSupply);
