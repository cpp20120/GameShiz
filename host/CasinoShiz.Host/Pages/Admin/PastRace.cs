using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace CasinoShiz.Host.Pages.Admin;

public sealed record PastRace(string RaceDate, long BalanceScopeId, int Winner);
