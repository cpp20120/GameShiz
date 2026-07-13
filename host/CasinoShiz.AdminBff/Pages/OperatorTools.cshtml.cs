using BotFramework.Contracts.Operations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.AdminBff.Pages;

public sealed class OperatorToolsModel(IOperationsAdminService operations) : PageModel
{
    [BindProperty] public string StreamId { get; set; } = "";
    [BindProperty] public int Players { get; set; } = 100;
    [BindProperty] public int Rounds { get; set; } = 10_000;
    [BindProperty] public int Seed { get; set; } = 1;
    [BindProperty] public int StartingBalance { get; set; } = 100;
    [BindProperty] public int Stake { get; set; } = 10;
    [BindProperty] public int WinPayout { get; set; } = 18;
    [BindProperty] public double WinProbability { get; set; } = 0.5;
    public EventReplayReport? Replay { get; private set; }
    public EconomySimulationReport? Simulation { get; private set; }
    public IReadOnlyList<FairnessCommitment> Incomplete { get; private set; } = [];
    public bool CanUse => HttpContext.Session.IsSuperAdmin();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!HttpContext.Session.IsAuthenticated()) return RedirectToPage("/Login");
        Incomplete = await operations.ListIncompleteFairnessAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostReplayAsync(CancellationToken ct)
    {
        if (!Authorized()) return Forbid();
        Replay = await operations.ReplayEventStreamAsync(StreamId, HttpContext.Session.ActorId(),
            HttpContext.Session.ActorName(), ct);
        Incomplete = await operations.ListIncompleteFairnessAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostSimulateAsync(CancellationToken ct)
    {
        if (!Authorized()) return Forbid();
        var request = new EconomySimulationRequest(new(StartingBalance, Stake, WinPayout,
            WinProbability, 1_000), Players, Rounds, Seed);
        Simulation = await operations.SimulateEconomyAsync(request, HttpContext.Session.ActorId(),
            HttpContext.Session.ActorName(), ct);
        Incomplete = await operations.ListIncompleteFairnessAsync(ct);
        return Page();
    }

    private bool Authorized() => HttpContext.Session.IsAuthenticated() && HttpContext.Session.IsSuperAdmin();
}
