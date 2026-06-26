using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.Host.Pages.Admin;

public sealed record EconomySimulation(
    decimal TotalGames,
    decimal TotalStake,
    decimal GameSink,
    decimal Faucets,
    decimal Sinks,
    decimal TransferFees,
    decimal Net);
